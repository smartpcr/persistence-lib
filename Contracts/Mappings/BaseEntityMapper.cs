//-------------------------------------------------------------------------------
// <copyright file="BaseEntityMapper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Extensions;

    /// <summary>
    /// Base mapper that uses reflection and attributes to create mappings between C# properties and database columns.
    /// </summary>
    /// <typeparam name="T">The entity type to map</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    public class BaseEntityMapper<T, TKey> : IEntityMapper<T, TKey> where T : class, IEntity<TKey> where TKey : IEquatable<TKey>
    {
        #region Protected Fields

        protected readonly Type EntityType;
        protected readonly Dictionary<PropertyInfo, PropertyMapping> PropertyMappings;
        protected readonly List<IndexDefinition> Indexes;
        protected readonly List<ForeignKeyDefinition> ForeignKeys;
        protected PropertyInfo PrimaryKeyProperty;
        protected bool HasCompositeKey;
        protected readonly List<PropertyInfo> CompositeKeyProperties;
        protected readonly ISerializer<T> Serializer;

        // List of SQL reserved keywords that need escaping
        protected readonly HashSet<string> ReservedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "FROM", "WHERE", "ORDER", "BY", "GROUP", "HAVING", "INSERT", "UPDATE", "DELETE",
            "CREATE", "DROP", "ALTER", "TABLE", "INDEX", "VIEW", "TRIGGER", "PROCEDURE", "FUNCTION",
            "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "CROSS", "ON", "AS", "AND", "OR", "NOT",
            "IN", "EXISTS", "BETWEEN", "LIKE", "IS", "NULL", "TRUE", "FALSE", "CASE", "WHEN", "THEN",
            "ELSE", "END", "UNION", "ALL", "DISTINCT", "TOP", "LIMIT", "OFFSET", "FETCH", "NEXT",
            "FIRST", "LAST", "ROW", "ROWS", "ONLY", "WITH", "RECURSIVE", "CTE", "TEMP", "TEMPORARY",
            "IF", "BEGIN", "COMMIT", "ROLLBACK", "TRANSACTION", "SAVEPOINT", "RELEASE", "PRAGMA",
            "VACUUM", "ANALYZE", "EXPLAIN", "PLAN", "DESC", "DESCRIBE", "SHOW", "USE", "DATABASE",
            "SCHEMA", "GRANT", "REVOKE", "DENY", "USER", "ROLE", "PRIVILEGE", "PASSWORD", "IDENTIFIED",
            "TO", "FOR", "EACH", "ROW", "STATEMENT", "EXECUTE", "DECLARE", "CURSOR", "OPEN", "CLOSE",
            "FETCH", "INTO", "VALUES", "DEFAULT", "PRIMARY", "FOREIGN", "KEY", "REFERENCES", "CASCADE",
            "RESTRICT", "SET", "CHECK", "UNIQUE", "CONSTRAINT", "ADD", "COLUMN", "MODIFY", "RENAME"
        };

        #endregion

        #region Properties

        public string TableName { get; set; }
        public string SchemaName { get; set; }
        public bool EnableSoftDelete { get; set; }
        public bool EnableExpiry { get; set; }
        public TimeSpan? ExpirySpan { get; set; }
        public bool EnableArchive { get; set; }
        public bool SyncWithList { get; set; }
        public bool EnableAuditTrail { get; set; }
        public Type[] Depends { get; set; }
        #endregion

        #region Constructor

        public BaseEntityMapper()
        {
            this.EntityType = typeof(T);
            this.PropertyMappings = new Dictionary<PropertyInfo, PropertyMapping>();
            this.Indexes = new List<IndexDefinition>();
            this.ForeignKeys = new List<ForeignKeyDefinition>();
            this.CompositeKeyProperties = new List<PropertyInfo>();

            // Extract table information
            var tableAttr = this.EntityType.GetCustomAttribute<TableAttribute>();
            if (tableAttr != null)
            {
                this.TableName = string.IsNullOrEmpty(tableAttr.Name) ? this.EntityType.Name : tableAttr.Name;
                this.SchemaName = tableAttr.Schema;
                this.EnableSoftDelete = tableAttr.SoftDeleteEnabled;
                this.EnableExpiry = tableAttr.ExpirySpan.HasValue;
                this.ExpirySpan = tableAttr.ExpirySpan;
                this.EnableArchive = tableAttr.EnableArchive;
                this.SyncWithList = tableAttr.SyncWithList;
                this.EnableAuditTrail = tableAttr.EnableAuditTrail;
                this.Depends = tableAttr.Depends ?? Array.Empty<Type>();
            }
            else
            {
                throw new InvalidOperationException(
                    $"Entity type {this.EntityType.Name} must have [Table] attribute at class declaration");
            }

            this.Serializer = SerializerResolver.GetSerializer<T>();

            // Build property mappings
            this.BuildPropertyMappings();

            // Validate primary key
            if (this.PrimaryKeyProperty == null && !this.HasCompositeKey)
            {
                throw new InvalidOperationException(
                    $"Entity type {this.EntityType.Name} must have at least one property marked with [PrimaryKey] or properties named 'Id' or 'Key'");
            }
        }

        #endregion

        #region SQL Generation - Virtual for Database-Specific Override

        protected virtual string GetParameterPrefix() => "@";
        protected virtual string GetAutoIncrementSyntax() => "IDENTITY(1,1)";
        protected virtual string GetCurrentTimestampFunction() => "GETDATE()";
        protected virtual string GetBooleanLiteral(bool value) => value ? "1" : "0";

        /// <summary>
        /// Gets the SQL condition for filtering expired entities.
        /// </summary>
        /// <returns>The SQL condition for expiry filtering.</returns>
        protected virtual string GetExpiryFilterCondition()
        {
            return $"{this.EscapeIdentifier("AbsoluteExpiration")} > {this.GetCurrentTimestampFunction()}";
        }

        /// <summary>
        /// Gets the SQL condition for filtering expired entities with a table alias.
        /// </summary>
        /// <param name="tableAlias">The table alias to use.</param>
        /// <returns>The SQL condition for expiry filtering with alias.</returns>
        protected virtual string GetExpiryFilterConditionWithAlias(string tableAlias)
        {
            return $"({tableAlias}.{this.EscapeIdentifier("AbsoluteExpiration")} IS NULL OR {tableAlias}.{this.EscapeIdentifier("AbsoluteExpiration")} > {this.GetCurrentTimestampFunction()})";
        }

        /// <summary>
        /// Escapes SQL reserved keywords or identifiers that need special handling.
        /// </summary>
        /// <param name="identifier">The identifier to escape.</param>
        /// <returns>The escaped identifier.</returns>
        protected virtual string EscapeIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return identifier;
            }

            // Check if the identifier is a reserved keyword or contains special characters
            if (this.ReservedKeywords.Contains(identifier) ||
                identifier.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
            {
                // Use double quotes for SQLite/standard SQL
                return $"[{identifier}]";
            }

            return identifier;
        }

        #endregion

        #region Public Methods - Table Information

        public virtual string GetSqlTypeString(PropertyMapping mapping)
        {
            string typeStr;

            // Handle special cases for SQLite
            switch (mapping.SqlType)
            {
                case SqlDbType.NVarChar:
                case SqlDbType.VarChar:
                case SqlDbType.NChar:
                case SqlDbType.Char:
                    typeStr = "TEXT";
                    break;
                case SqlDbType.Int:
                case SqlDbType.BigInt:
                case SqlDbType.SmallInt:
                case SqlDbType.TinyInt:
                case SqlDbType.Bit:
                    typeStr = "INTEGER";
                    break;
                case SqlDbType.Float:
                case SqlDbType.Real:
                case SqlDbType.Decimal:
                case SqlDbType.Money:
                case SqlDbType.SmallMoney:
                    typeStr = "REAL";
                    break;
                case SqlDbType.Binary:
                case SqlDbType.VarBinary:
                case SqlDbType.Image:
                    typeStr = "BLOB";
                    break;
                case SqlDbType.DateTime:
                case SqlDbType.DateTime2:
                case SqlDbType.DateTimeOffset:
                case SqlDbType.Date:
                case SqlDbType.Time:
                    typeStr = "TEXT"; // SQLite stores dates as text
                    break;
                case SqlDbType.UniqueIdentifier:
                    typeStr = "TEXT";
                    break;
                default:
                    typeStr = "TEXT";
                    break;
            }

            return typeStr;
        }

        /// <summary>
        /// Gets the fully qualified table name including schema.
        /// </summary>
        public virtual string GetFullTableName() => !string.IsNullOrEmpty(this.SchemaName)
            ? $"{this.SchemaName}.{this.TableName}"
            : this.TableName;

        /// <summary>
        /// Gets the table name without schema.
        /// </summary>
        public virtual string GetTableName() => this.TableName;

        #endregion

        #region Public Methods - Mapping Information

        /// <summary>
        /// Gets all property mappings.
        /// </summary>
        public IReadOnlyDictionary<PropertyInfo, PropertyMapping> GetPropertyMappings() => this.PropertyMappings;

        /// <summary>
        /// Gets the primary key column name.
        /// When PK is composite, return PK column not Version.
        /// When PK is single, return the primary key column name.
        /// </summary>
        public string GetPrimaryKeyColumn()
        {
            if (this.HasCompositeKey)
            {
                // For composite keys, return the first primary key column that is not "Version"
                return this.CompositeKeyProperties
                    .Select(p => this.PropertyMappings[p].ColumnName)
                    .FirstOrDefault(c => !c.Equals("Version", StringComparison.OrdinalIgnoreCase));
            }

            return this.PropertyMappings.FirstOrDefault(m => m.Value.IsPrimaryKey).Value?.ColumnName;
        }

        /// <summary>
        /// Gets the primary key column names.
        /// </summary>
        public List<string> GetPrimaryKeyColumns()
        {
            return this.PropertyMappings.Values
                .Where(m => m.IsPrimaryKey)
                .OrderBy(m => m.PrimaryKeyOrder)
                .Select(m => m.ColumnName)
                .ToList();
        }

        #endregion

        #region Public Methods - SQL Generation

        /// <summary>
        /// Generates CREATE TABLE SQL statement for the entity.
        /// </summary>
        public virtual string GenerateCreateTableSql(bool includeIfNotExists = true)
        {
            var sql = new StringBuilder();

            if (includeIfNotExists)
            {
                sql.AppendLine($"CREATE TABLE IF NOT EXISTS {this.GetFullTableName()} (");
            }
            else
            {
                sql.AppendLine($"CREATE TABLE {this.GetFullTableName()} (");
            }

            // Add column definitions
            var columnDefinitions = new List<string>();
            foreach (var mapping in this.PropertyMappings.Values.Where(m => !m.IsNotMapped))
            {
                columnDefinitions.Add(this.GenerateColumnDefinition(mapping));
            }

            // Add primary key constraint
            var pkSql = this.GeneratePrimaryKeyDefinition();
            if (!string.IsNullOrEmpty(pkSql))
            {
                columnDefinitions.Add(pkSql);
            }

            // add check constraints
            var checkConstraints = this.PropertyMappings.Values
                .Where(m => !string.IsNullOrEmpty(m.CheckConstraint) && !string.IsNullOrEmpty(m.CheckConstraintName))
                .Select(m => $"CONSTRAINT {m.CheckConstraintName} CHECK ({m.CheckConstraint})").ToList();
            if (checkConstraints.Any())
            {
                foreach (var checkConstraint in checkConstraints)
                {
                    columnDefinitions.Add(checkConstraint);
                }
            }

            // Add foreign key constraints
            foreach (var fk in this.ForeignKeys)
            {
                columnDefinitions.Add(this.GenerateForeignKeyConstraint(fk));
            }

            sql.AppendLine(string.Join(",\n", columnDefinitions.Select(d => $"    {d}")));
            sql.AppendLine(");");

            return sql.ToString();
        }

        /// <summary>
        /// Generates CREATE INDEX SQL statements for the entity.
        /// </summary>
        public virtual IEnumerable<string> GenerateCreateIndexSql()
        {
            var indexSql = new List<string>();

            foreach (var index in this.Indexes)
            {
                var sql = new StringBuilder();
                sql.Append("CREATE ");

                if (index.IsUnique)
                    sql.Append("UNIQUE ");

                sql.Append($"INDEX IF NOT EXISTS {index.Name} ");
                sql.Append($"ON {this.GetFullTableName()} (");
                sql.Append(string.Join(", ", index.Columns.OrderBy(c => c.Order).Select(c => this.EscapeIdentifier(c.ColumnName))));
                sql.Append(");");

                indexSql.Add(sql.ToString());
            }

            return indexSql;
        }

        public string GenerateWhereClause(Expression<Func<T, bool>> predicate)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Generates ORDER BY SQL clause from a LINQ ordering function.
        /// </summary>
        /// <param name="orderBy">The ordering function that takes IQueryable and returns IOrderedQueryable</param>
        /// <returns>The ORDER BY SQL clause, or empty string if no ordering specified</returns>
        public virtual string GenerateOrderBySql(Func<IQueryable<T>, IOrderedQueryable<T>> orderBy)
        {
            if (orderBy == null)
            {
                return string.Empty;
            }

            // Create a spy queryable to capture the ordering expressions
            var spyQueryable = new OrderBySpyQueryable<T, TKey>(this);

            try
            {
                // Execute the orderBy function with our spy queryable
                orderBy(spyQueryable);

                // Get the captured ORDER BY clause
                return spyQueryable.GetOrderBySql();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to generate ORDER BY SQL from the provided ordering function.", ex);
            }
        }

        #endregion

        #region Public Methods - Column Selection

        /// <summary>
        /// Gets the columns to select in SELECT queries.
        /// </summary>
        public virtual List<string> GetSelectColumns()
        {
            return this.PropertyMappings.Values
                .Where(m => !m.IsNotMapped)
                .Select(m => m.ColumnName)
                .ToList();
        }

        /// <summary>
        /// Gets the columns to include in INSERT statements.
        /// </summary>
        public virtual List<string> GetInsertColumns()
        {
            return this.PropertyMappings.Values
                .Where(m => !m.IsNotMapped && !m.IsComputed && !m.IsAutoIncrement)
                .Select(m => m.ColumnName)
                .ToList();
        }

        /// <summary>
        /// Gets the columns to include in UPDATE statements.
        /// </summary>
        public virtual List<string> GetUpdateColumns()
        {
            return this.PropertyMappings.Values
                .Where(m => !m.IsNotMapped && !m.IsComputed && !m.IsPrimaryKey)
                .Select(m => m.ColumnName)
                .ToList();
        }

        #endregion

        #region Protected Methods - Parameter Value Conversion

        /// <summary>
        /// Converts a parameter value to the appropriate format for SQLite storage.
        /// Handles special cases like DateTime and DateTimeOffset conversion to ISO 8601 format.
        /// Also handles nullable types by unwrapping their underlying values.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The converted value suitable for SQLite parameter.</returns>
        protected virtual object ConvertParameterValue(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return DBNull.Value;
            }

            // Convert DateTime and DateTimeOffset to ISO 8601 format for SQLite
            if (value is DateTime dateTime)
            {
                return dateTime.ToString("O");
            }

            if (value is DateTimeOffset dateTimeOffset)
            {
                return dateTimeOffset.ToString("O");
            }

            return value;
        }

        #endregion

        #region Public Methods - Parameter Handling

        /// <summary>
        /// Adds parameters to a SQLite command for the given entity.
        /// </summary>
        public void AddParameters(IDbCommand command, T entity)
        {
            foreach (var mapping in this.PropertyMappings.Values.Where(m => !m.IsNotMapped && !m.IsComputed))
            {
                var value = mapping.PropertyInfo.GetValue(entity);
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@{mapping.ColumnName}";
                parameter.Value = this.ConvertParameterValue(value);
                command.Parameters.Add(parameter);
            }
        }

        /// <summary>
        /// Adds parameters to a command for the given entity.
        /// </summary>
        public virtual void AddParameters(System.Data.Common.DbCommand command, T entity)
        {
            foreach (var mapping in this.PropertyMappings.Values.Where(m => !m.IsNotMapped && !m.IsComputed))
            {
                var value = mapping.PropertyInfo.GetValue(entity);
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@{mapping.ColumnName}";
                parameter.Value = this.ConvertParameterValue(value);
                command.Parameters.Add(parameter);
            }
        }

        #endregion

        #region Public Methods - Data Mapping

        /// <summary>
        /// Maps a data reader row to an entity instance.
        /// Supports entities with parameterized constructors by collecting property values
        /// and using reflection to find and invoke the appropriate constructor.
        /// </summary>
        public virtual T MapFromReader(IDataReader reader)
        {
            // Step 1: Collect all property values from the reader into a dictionary
            var propertyValues = new Dictionary<string, object>();

            foreach (var mapping in this.PropertyMappings.Values.Where(m => !m.IsNotMapped))
            {
                try
                {
                    var ordinal = reader.GetOrdinal(mapping.ColumnName);
                    if (!reader.IsDBNull(ordinal))
                    {
                        var dbValue = reader.GetValue(ordinal);
                        // Convert database value to appropriate C# type
                        var convertedValue = this.ConvertDbValueToCSharpType(dbValue, mapping.PropertyType);
                        propertyValues[mapping.PropertyName] = convertedValue;
                    }
                    else
                    {
                        propertyValues[mapping.PropertyName] = null;
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    // Column not in result set, skip
                    propertyValues[mapping.PropertyName] = null;
                }
            }

            // Step 2: Try to create instance using constructor mapping
            var entity = this.CreateInstanceWithConstructor(propertyValues);

            // Step 3: Set any remaining properties that weren't set by constructor
            this.SetRemainingProperties(entity, propertyValues);

            return entity;
        }

        /// <summary>
        /// Maps an entity to a dictionary of parameter names and values.
        /// </summary>
        /// <param name="entity">The entity to map.</param>
        /// <returns>A dictionary containing parameter names (with @ prefix) and their values.</returns>
        public virtual Dictionary<string, object> MapEntityToParameters(T entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            var parameters = new Dictionary<string, object>();

            foreach (var mapping in this.PropertyMappings.Values.Where(m => !m.IsNotMapped))
            {
                var parameterName = $"@{mapping.ColumnName}";
                var value = mapping.PropertyInfo.GetValue(entity);

                // Convert null to DBNull.Value
                parameters[parameterName] = value ?? DBNull.Value;
            }

            return parameters;
        }

        /// <summary>
        /// Maps an ID value to a dictionary of parameter names and values.
        /// </summary>
        /// <param name="id">The ID value to map.</param>
        /// <returns>A dictionary containing the ID parameter name and value.</returns>
        public virtual Dictionary<string, object> MapIdToParameters(TKey id)
        {
            var parameters = new Dictionary<string, object>();
            var pkColName = this.GetPrimaryKeyColumn();
            parameters["@" + pkColName] = id ?? (object)DBNull.Value;
            return parameters;
        }

        /// <summary>
        /// Gets the column name for a given property name.
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <returns>The column name, or null if the property is not mapped.</returns>
        public virtual string GetColumnName(string propertyName)
        {
            var mapping = this.PropertyMappings.Values
                .FirstOrDefault(m => m.PropertyName == propertyName && !m.IsNotMapped);

            return mapping?.ColumnName;
        }

        /// <summary>
        /// Generates the SELECT ALL SQL statement.
        /// </summary>
        /// <returns>The SELECT ALL SQL statement.</returns>
        public virtual string GenerateSelectAllSql()
        {
            return this.GenerateSelectSql(new SelectOptions());
        }

        /// <summary>
        /// Generates the SELECT BY ID SQL statement.
        /// </summary>
        /// <returns>The SELECT BY ID SQL statement.</returns>
        public virtual string GenerateSelectByIdSql()
        {
            var pkColName = this.GetPrimaryKeyColumn();
            return this.GenerateSelectSql(new SelectOptions
            {
                WhereClause = $"{this.EscapeIdentifier(pkColName)} = @{pkColName}"
            });
        }

        /// <summary>
        /// Generates the INSERT SQL statement.
        /// </summary>
        /// <returns>The INSERT SQL statement.</returns>
        public virtual string GenerateInsertSql()
        {
            var insertColumns = this.GetInsertColumns();
            var escapedColumnNames = string.Join(", ", insertColumns.Select(col => this.EscapeIdentifier(col)));
            var parameterNames = string.Join(", ", insertColumns.Select(col => $"@{col}"));

            if (this.EnableSoftDelete)
            {
                insertColumns = insertColumns
                    .Where(c =>
                        !c.Equals("IsDeleted", StringComparison.OrdinalIgnoreCase) &&
                        !c.Equals("Version", StringComparison.Ordinal))
                    .ToList();
                escapedColumnNames = string.Join(", ", insertColumns.Select(col => this.EscapeIdentifier(col)));
                parameterNames = string.Join(", ", insertColumns.Select(col => $"@{col}"));

                // version
                escapedColumnNames += ", " + this.EscapeIdentifier("Version");
                parameterNames += ", @NextVersion";

                // isDeleted
                escapedColumnNames += ", " + this.EscapeIdentifier("IsDeleted");
                parameterNames += ", 0"; // Default value for IsDeleted
            }


            return $"INSERT INTO {this.GetFullTableName()} ({escapedColumnNames}) VALUES ({parameterNames})";
        }

        /// <summary>
        /// Generates the UPDATE SQL statement.
        /// When soft-delete is enabled, it assumes NextVersion has been retrieved.
        /// </summary>
        /// <returns>The UPDATE SQL statement.</returns>
        public virtual string GenerateUpdateSql()
        {
            if (!this.EnableSoftDelete)
            {
                var updateColumns = this.GetUpdateColumns().Where(c => !c.Equals("Version", StringComparison.Ordinal)).ToList();
                var setClause = string.Join(", ", updateColumns.Select(col => $"{this.EscapeIdentifier(col)} = @{col}"));

                if (this.PropertyMappings.Values.Any(m => m.ColumnName.Equals("Version", StringComparison.Ordinal)))
                {
                    setClause += $", {this.EscapeIdentifier("Version")} = @Version + 1"; // Increment version if not soft-deleting
                }

                var pkColumnName = this.GetPrimaryKeyColumn();
                var sql = $"UPDATE {this.GetFullTableName()} SET {setClause} WHERE {this.EscapeIdentifier(pkColumnName)} = @{pkColumnName}";

                return sql;
            }

            return this.GenerateInsertSql();
        }

        /// <summary>
        /// Generates the DELETE SQL statement.
        /// When soft-delete is enabled, it assumes NextVersion has been retrieved.
        /// </summary>
        /// <returns>The DELETE SQL statement.</returns>
        public virtual string GenerateDeleteSql()
        {
            var whereClause = string.Join(" AND ", this.GetPrimaryKeyColumns().Select(c => $"{this.EscapeIdentifier(c)} = @{c}"));

            if (this.EnableSoftDelete)
            {
                return $"UPDATE {this.GetFullTableName()} SET {this.EscapeIdentifier("IsDeleted")} = 1, {this.EscapeIdentifier("Version")} = @NextVersion WHERE {whereClause}";
            }

            return $"DELETE FROM {this.GetFullTableName()} WHERE {whereClause}";
        }

        /// <summary>
        /// Generates a batch INSERT SQL statement for multiple entities.
        /// </summary>
        /// <param name="entityCount">The number of entities to insert.</param>
        /// <returns>The batch INSERT SQL statement.</returns>
        public virtual string GenerateBatchInsertSql(int entityCount)
        {
            if (entityCount <= 0)
            {
                throw new ArgumentException("Entity count must be greater than zero.", nameof(entityCount));
            }

            var insertColumns = this.GetInsertColumns();
            var escapedColumnNames = string.Join(", ", insertColumns.Select(col => this.EscapeIdentifier(col)));

            var valueClauses = new List<string>();
            for (var i = 0; i < entityCount; i++)
            {
                var i1 = i;
                var parameterNames = string.Join(", ", insertColumns.Select(col => $"@{col}{i1}"));
                valueClauses.Add($"({parameterNames})");
            }

            return $"INSERT INTO {this.GetFullTableName()} ({escapedColumnNames}) VALUES {string.Join(", ", valueClauses)}";
        }

        /// <summary>
        /// Adds INSERT parameters to a command.
        /// </summary>
        public virtual void AddInsertParameters(System.Data.Common.DbCommand command, T entity)
        {
            foreach (var mapping in this.PropertyMappings.Values.Where(m => !m.IsNotMapped && !m.IsComputed && !m.IsAutoIncrement))
            {
                var value = mapping.PropertyInfo.GetValue(entity);
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@{mapping.ColumnName}";
                parameter.Value = this.ConvertParameterValue(value);
                command.Parameters.Add(parameter);
            }
        }

        /// <summary>
        /// Adds UPDATE parameters to a command.
        /// </summary>
        public virtual void AddUpdateParameters(System.Data.Common.DbCommand command, T entity)
        {
            // Add update column parameters
            foreach (var mapping in this.PropertyMappings.Values.Where(m => !m.IsNotMapped && !m.IsComputed && !m.IsPrimaryKey))
            {
                var value = mapping.PropertyInfo.GetValue(entity);
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@{mapping.ColumnName}";
                parameter.Value = this.ConvertParameterValue(value);
                command.Parameters.Add(parameter);
            }

            // Add WHERE clause parameters
            var idMapping = this.PropertyMappings.Values.FirstOrDefault(m => m.PropertyName == "Id");
            if (idMapping != null)
            {
                var idValue = idMapping.PropertyInfo.GetValue(entity);
                var idParameter = command.CreateParameter();
                idParameter.ParameterName = "@Id";
                idParameter.Value = idValue ?? DBNull.Value;
                command.Parameters.Add(idParameter);
            }

            if (this.EnableSoftDelete)
            {
                var versionMapping = this.PropertyMappings.Values.FirstOrDefault(m => m.PropertyName == "Version");
                if (versionMapping != null)
                {
                    var versionValue = versionMapping.PropertyInfo.GetValue(entity);
                    var versionParameter = command.CreateParameter();
                    versionParameter.ParameterName = "@Version";
                    versionParameter.Value = versionValue ?? DBNull.Value;
                    command.Parameters.Add(versionParameter);
                }
            }
        }

        /// <summary>
        /// Adds DELETE parameters to a command.
        /// </summary>
        public virtual void AddDeleteParameters(System.Data.Common.DbCommand command, TKey id, long? version)
        {
            var idParameter = command.CreateParameter();
            idParameter.ParameterName = "@Id";
            idParameter.Value = (object)id ?? DBNull.Value;
            command.Parameters.Add(idParameter);

            if (this.EnableSoftDelete)
            {
                var versionParameter = command.CreateParameter();
                versionParameter.ParameterName = "@Version";
                versionParameter.Value = (object)version ?? DBNull.Value;
                command.Parameters.Add(versionParameter);
            }
        }

        #endregion

        #region Public Methods - Serialization

        /// <summary>
        /// Serializes an entity to a byte array.
        /// </summary>
        public byte[] SerializeEntity(T entity)
        {
            return this.Serializer.Serialize(entity);
        }

        /// <summary>
        /// Serializes a key value to a string representation for database storage.
        /// </summary>
        public virtual string SerializeKey(TKey key)
        {
            if (key == null)
                return null;

            var keyType = typeof(TKey);
            var underlyingType = Nullable.GetUnderlyingType(keyType) ?? keyType;

            // If TKey is string, return as-is
            if (underlyingType == typeof(string))
            {
                return key.ToString();
            }
            // For numeric types, use ToString()
            else if (underlyingType == typeof(int) ||
                     underlyingType == typeof(long) ||
                     underlyingType == typeof(short) ||
                     underlyingType == typeof(byte) ||
                     underlyingType == typeof(decimal) ||
                     underlyingType == typeof(double) ||
                     underlyingType == typeof(float))
            {
                return key.ToString();
            }
            // For DateTime types, use ISO 8601 format for SQLite compatibility
            else if (underlyingType == typeof(DateTime))
            {
                return ((DateTime)(object)key).ToString("yyyy-MM-dd HH:mm:ss.fff");
            }
            else if (underlyingType == typeof(DateTimeOffset))
            {
                return ((DateTimeOffset)(object)key).ToString("yyyy-MM-dd HH:mm:ss.fffzzz");
            }
            // For boolean values
            else if (underlyingType == typeof(bool))
            {
                return ((bool)(object)key) ? "1" : "0";
            }
            // For enum values, use the underlying integer value
            else if (underlyingType.IsEnum)
            {
                return Convert.ToInt32(key).ToString();
            }
            // For Guid values
            else if (underlyingType == typeof(Guid))
            {
                return ((Guid)(object)key).ToString();
            }
            // For other types, fall back to ToString()
            else
            {
                return key.ToString();
            }
        }

        /// <summary>
        /// Deserializes a string representation back to the original key type.
        /// </summary>
        public virtual TKey DeserializeKey(string serialized)
        {
            if (string.IsNullOrEmpty(serialized))
                return default(TKey);

            var keyType = typeof(TKey);
            var underlyingType = Nullable.GetUnderlyingType(keyType) ?? keyType;

            try
            {
                // If TKey is string, return as-is
                if (underlyingType == typeof(string))
                {
                    return (TKey)(object)serialized;
                }
                // For numeric types, parse appropriately
                else if (underlyingType == typeof(int))
                {
                    return (TKey)(object)int.Parse(serialized);
                }
                else if (underlyingType == typeof(long))
                {
                    return (TKey)(object)long.Parse(serialized);
                }
                else if (underlyingType == typeof(short))
                {
                    return (TKey)(object)short.Parse(serialized);
                }
                else if (underlyingType == typeof(byte))
                {
                    return (TKey)(object)byte.Parse(serialized);
                }
                else if (underlyingType == typeof(decimal))
                {
                    return (TKey)(object)decimal.Parse(serialized);
                }
                else if (underlyingType == typeof(double))
                {
                    return (TKey)(object)double.Parse(serialized);
                }
                else if (underlyingType == typeof(float))
                {
                    return (TKey)(object)float.Parse(serialized);
                }
                // For DateTime types, parse from ISO 8601 format
                else if (underlyingType == typeof(DateTime))
                {
                    return (TKey)(object)DateTime.Parse(serialized);
                }
                else if (underlyingType == typeof(DateTimeOffset))
                {
                    return (TKey)(object)DateTimeOffset.Parse(serialized);
                }
                // For boolean values
                else if (underlyingType == typeof(bool))
                {
                    // Handle both "1"/"0" and "true"/"false" formats
                    if (serialized == "1" || string.Equals(serialized, "true", StringComparison.OrdinalIgnoreCase))
                        return (TKey)(object)true;
                    else if (serialized == "0" || string.Equals(serialized, "false", StringComparison.OrdinalIgnoreCase))
                        return (TKey)(object)false;
                    else
                        return (TKey)(object)bool.Parse(serialized);
                }
                // For enum values, parse from integer representation
                else if (underlyingType.IsEnum)
                {
                    var enumValue = int.Parse(serialized);
                    return (TKey)Enum.ToObject(underlyingType, enumValue);
                }
                // For Guid values
                else if (underlyingType == typeof(Guid))
                {
                    return (TKey)(object)Guid.Parse(serialized);
                }
                // For other types, try Convert.ChangeType as fallback
                else
                {
                    return (TKey)Convert.ChangeType(serialized, underlyingType);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Unable to deserialize key value '{serialized}' to type {keyType.Name}. " +
                    $"Original error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a SELECT command for retrieving an entity by key.
        /// </summary>
        /// <param name="key">The entity key.</param>
        /// <param name="includeAllVersions">Whether to include all versions.</param>
        /// <param name="includeDeleted">Whether to include deleted entities.</param>
        /// <param name="includeExpired">Whether to include expired entities.</param>
        /// <returns>The configured SELECT command.</returns>
        public virtual IDbCommand CreateSelectCommand(TKey key, bool includeAllVersions = false, bool includeDeleted = false,
            bool includeExpired = false)
        {
            var pkColName = this.GetPrimaryKeyColumn();
            var context = new CommandContext<T, TKey>
            {
                Id = key,
                SelectOptions = new SelectOptions
                {
                    IncludeAllVersions = includeAllVersions,
                    IncludeDeleted = includeDeleted,
                    IncludeExpired = includeExpired,
                    WhereClause = $"{this.EscapeIdentifier(pkColName)} = @{pkColName}"
                }
            };

            return this.CreateCommand(DbOperationType.Select, context);
        }

        #endregion

        #region Public Methods - Command Creation

        /// <summary>
        /// Creates a database command with embedded SQL and parameters using context.
        /// This is the improved method that combines SQL generation with parameter binding.
        /// </summary>
        public virtual IDbCommand CreateCommand(DbOperationType operation, CommandContext<T, TKey> context)
        {
            var command = this.CreateDbCommand();

            if (context.CommandTimeout.HasValue)
            {
                command.CommandTimeout = context.CommandTimeout.Value;
            }

            if (context.Transaction != null)
            {
                command.Transaction = context.Transaction;
            }

            switch (operation)
            {
                case DbOperationType.Select:
                    this.ConfigureSelectCommand(command, context);
                    break;
                case DbOperationType.Insert:
                    this.ConfigureInsertCommand(command, context);
                    break;
                case DbOperationType.Update:
                    this.ConfigureUpdateCommand(command, context);
                    break;
                case DbOperationType.Delete:
                    this.ConfigureDeleteCommand(command, context);
                    break;
                case DbOperationType.BatchInsert:
                    this.ConfigureBatchInsertCommand(command, context);
                    break;
                case DbOperationType.Upsert:
                    this.ConfigureUpsertCommand(command, context);
                    break;
                default:
                    throw new NotSupportedException($"Operation {operation} is not supported");
            }

            return command;
        }

        protected virtual void ConfigureSelectCommand(IDbCommand command, CommandContext<T, TKey> context)
        {
            var options = context.SelectOptions ?? new SelectOptions();
            command.CommandText = this.GenerateSelectSql(options);

            if (context.Id != null)
            {
                this.AddParameter(command, this.GetPrimaryKeyColumn(), context.Id);
            }

            if (context.WhereParameters != null)
            {
                foreach (var param in context.WhereParameters)
                {
                    this.AddParameter(command, param.Key, param.Value);
                }
            }
        }

        protected virtual void ConfigureInsertCommand(IDbCommand command, CommandContext<T, TKey> context)
        {
            if (context.Entity == null)
                throw new ArgumentException("Entity is required for insert operation");

            command.CommandText = this.GenerateInsertStatement();
            this.AddEntityParameters(command, context.Entity);

            if (this.EnableSoftDelete)
            {
                this.AddParameter(command, "NextVersion", this.GetNextVersion(context));
            }
        }

        protected virtual void ConfigureUpdateCommand(IDbCommand command, CommandContext<T, TKey> context)
        {
            if (context.Entity == null)
                throw new ArgumentException("Entity is required for update operation");

            if (this.EnableSoftDelete)
            {
                // For soft delete, update is actually an insert of a new version
                command.CommandText = this.GenerateInsertStatement();
                this.AddEntityParameters(command, context.Entity);
                this.AddParameter(command, "NextVersion", this.GetNextVersion(context));
            }
            else
            {
                command.CommandText = this.GenerateUpdateStatement();
                this.AddEntityParameters(command, context.Entity);
            }
        }

        protected virtual void ConfigureDeleteCommand(IDbCommand command, CommandContext<T, TKey> context)
        {
            if (context.Id == null && context.Entity == null)
                throw new ArgumentException("Id or Entity is required for delete operation");

            command.CommandText = this.GenerateDeleteStatement();

            var id = context.Id ?? context.Entity.Id;
            this.AddParameter(command, this.GetPrimaryKeyColumn(), id);

            if (this.EnableSoftDelete && context.Entity != null)
            {
                this.AddParameter(command, "Version", context.Entity.Version);
            }
        }

        protected virtual void ConfigureBatchInsertCommand(IDbCommand command, CommandContext<T, TKey> context)
        {
            if (context.Entities == null || !context.Entities.Any())
                throw new ArgumentException("Entities are required for batch insert");

            command.CommandText = this.GenerateBatchInsertSql(context.Entities.Count());

            int index = 0;
            foreach (var entity in context.Entities)
            {
                this.AddEntityParametersWithPrefix(command, entity, index++);
            }
        }

        protected virtual void ConfigureUpsertCommand(IDbCommand command, CommandContext<T, TKey> context)
        {
            // Database-specific implementation - must be overridden
            throw new NotSupportedException("Upsert must be implemented by database-specific mapper");
        }

        protected virtual long GetNextVersion(CommandContext<T, TKey> context)
        {
            if (context.OldEntity != null)
            {
                return context.OldEntity.Version + 1;
            }
            return 1;
        }

        protected virtual IDbCommand CreateDbCommand()
        {
            return new SqlCommand();
        }

        protected virtual void AddParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = this.GetParameterPrefix() + name.TrimStart('@', ':', '$');
            parameter.Value = this.ConvertParameterValue(value);
            command.Parameters.Add(parameter);
        }

        protected virtual void AddEntityParameters(IDbCommand command, T entity)
        {
            foreach (var mapping in this.PropertyMappings.Values.Where(m => !m.IsNotMapped && !m.IsComputed))
            {
                var value = mapping.PropertyInfo.GetValue(entity);
                this.AddParameter(command, mapping.ColumnName, value);
            }
        }

        protected virtual void AddEntityParametersWithPrefix(IDbCommand command, T entity, int index)
        {
            foreach (var mapping in this.PropertyMappings.Values.Where(m => !m.IsNotMapped && !m.IsComputed))
            {
                var value = mapping.PropertyInfo.GetValue(entity);
                this.AddParameter(command, $"{mapping.ColumnName}_{index}", value);
            }
        }

        /// <summary>
        /// Generates a SELECT SQL statement based on the provided options.
        /// This is the single comprehensive method for all SELECT query generation.
        /// </summary>
        /// <param name="options">The select options to use.</param>
        /// <returns>The generated SELECT SQL statement.</returns>
        protected virtual string GenerateSelectSql(SelectOptions options)
        {
            options = options ?? new SelectOptions();

            var tableName = this.GetFullTableName();
            var primaryKeyColumn = this.GetPrimaryKeyColumn();

            // Check if WHERE clause specifies a version
            bool hasVersionInWhere = !string.IsNullOrEmpty(options.WhereClause) &&
                                   options.WhereClause.Contains("Version");

            // Determine query strategy for soft-delete scenarios
            // Single entity query: has WHERE clause that filters to a specific ID
            bool isSingleEntityQuery = !string.IsNullOrEmpty(options.WhereClause) &&
                                     options.WhereClause.Contains(primaryKeyColumn) &&
                                     options.WhereClause.Contains("@");

            // Use JOIN for queries that:
            // 1. Have soft delete enabled
            // 2. Want only latest version (not all versions)
            // 3. Don't already specify a version in WHERE
            // Note: We always use JOIN to ensure correct soft delete behavior
            bool needsLatestVersionJoin = this.EnableSoftDelete &&
                                         !options.IncludeAllVersions &&
                                         !hasVersionInWhere;

            // Determine final table aliasing strategy upfront
            // Single entity queries with versioning don't need JOIN, so no alias needed
            bool useTableAlias = needsLatestVersionJoin && !isSingleEntityQuery;

            // Build column list with proper aliasing
            string selectColumns;
            string fromClause;

            if (useTableAlias)
            {
                // Use table alias 't' for JOIN queries
                selectColumns = string.Join(", ", this.GetSelectColumns().Select(c => $"t.{this.EscapeIdentifier(c)}"));
                fromClause = $"{tableName} t";
            }
            else
            {
                // Simple query without alias
                selectColumns = string.Join(", ", this.GetSelectColumns().Select(this.EscapeIdentifier));
                fromClause = tableName;
            }

            // Build WHERE conditions
            var conditions = new List<string>();

            // Add custom WHERE clause
            if (!string.IsNullOrEmpty(options.WhereClause))
            {
                if (useTableAlias && !options.WhereClause.Contains("."))
                {
                    // Add table alias to WHERE clause if not already present
                    // This handles simple cases like "Id = @Id"
                    conditions.Add($"t.{options.WhereClause}");
                }
                else
                {
                    conditions.Add(options.WhereClause);
                }
            }

            // Add soft delete filter (IsDeleted = 0) by default when soft delete is enabled
            if (!options.IncludeDeleted && this.EnableSoftDelete)
            {
                var isDeletedCondition = useTableAlias
                    ? $"t.{this.EscapeIdentifier("IsDeleted")} = {this.GetBooleanLiteral(false)}"
                    : $"{this.EscapeIdentifier("IsDeleted")} = {this.GetBooleanLiteral(false)}";
                conditions.Add(isDeletedCondition);
            }

            // Add expiry filter
            if (!options.IncludeExpired && this.EnableExpiry)
            {
                var expiryCondition = useTableAlias
                    ? this.GetExpiryFilterConditionWithAlias("t")
                    : this.GetExpiryFilterCondition();
                conditions.Add(expiryCondition);
            }

            // Build JOIN clause for latest version queries
            string joinClause = "";
            if (needsLatestVersionJoin)
            {
                if (isSingleEntityQuery)
                {
                    // For single entity, use a more efficient subquery in WHERE clause
                    // Add version filter to WHERE conditions instead of JOIN
                    var versionSubquery = $@"{this.EscapeIdentifier("Version")} = (
        SELECT MAX({this.EscapeIdentifier("Version")})
        FROM {tableName}
        WHERE {this.EscapeIdentifier(primaryKeyColumn)} = {this.GetParameterPrefix()}{primaryKeyColumn}
    )";
                    conditions.Add(versionSubquery);
                    // No JOIN needed for single entity
                }
                else
                {
                    // For multiple entities, use JOIN to get latest version of each
                    // This JOIN finds the latest version of each entity, regardless of deletion status
                    // The deletion filter is applied in the outer WHERE clause
                    joinClause = $@"
INNER JOIN (
    SELECT {this.EscapeIdentifier(primaryKeyColumn)}, MAX({this.EscapeIdentifier("Version")}) AS MAX_VERSION
    FROM {tableName}
    GROUP BY {this.EscapeIdentifier(primaryKeyColumn)}
) latest ON
    t.{this.EscapeIdentifier(primaryKeyColumn)} = latest.{this.EscapeIdentifier(primaryKeyColumn)} AND
    t.{this.EscapeIdentifier("Version")} = latest.MAX_VERSION";
                }
            }

            // Build WHERE clause after all conditions are added
            var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

            // Build ORDER BY clause
            string orderByClause = "";
            if (!string.IsNullOrEmpty(options.OrderBy))
            {
                // User-specified ordering
                orderByClause = $"ORDER BY {options.OrderBy}";
            }
            else if (this.EnableSoftDelete && options.IncludeAllVersions)
            {
                // All versions query - order by ID and version
                orderByClause = $"ORDER BY {this.EscapeIdentifier(primaryKeyColumn)}, {this.EscapeIdentifier("Version")} DESC";
            }
            else if (useTableAlias)
            {
                // Multi-entity query with JOIN
                orderByClause = $"ORDER BY t.{this.EscapeIdentifier(primaryKeyColumn)}";
            }

            // Build LIMIT clause
            var limitClause = this.BuildLimitClause(options);

            // Construct final query
            var sql = $@"SELECT {selectColumns}
FROM {fromClause}{joinClause}
{whereClause}
{orderByClause}
{limitClause}".Trim();

            // Clean up extra whitespace
            return System.Text.RegularExpressions.Regex.Replace(sql, @"\s+", " ").Trim();
        }

        /// <summary>
        /// Must DB use LIMIT and OFFSET for pagination. SQL Server and Oracle uses TOP and OFFSET-FETCH.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        protected virtual string BuildLimitClause(SelectOptions options)
        {
            if (options.Limit.HasValue)
            {
                var limit = $"LIMIT {options.Limit.Value}";
                if (options.Offset.HasValue)
                    limit += $" OFFSET {options.Offset.Value}";
                return limit;
            }

            return string.Empty;
        }


        #endregion

        #region Protected Methods

        protected virtual string GenerateColumnDefinition(PropertyMapping mapping)
        {
            var sql = new StringBuilder();
            sql.Append($"{this.EscapeIdentifier(mapping.ColumnName)} ");

            // Data type
            sql.Append(this.GetSqlTypeString(mapping));

            // auto-increment
            if (mapping.IsAutoIncrement)
            {
                sql.Append(" AUTOINCREMENT");
            }

            // Nullability
            if (mapping.IsNotNull || mapping.IsPrimaryKey)
            {
                sql.Append(" NOT NULL");
            }

            // Unique constraint
            if (mapping.IsUnique && !mapping.IsPrimaryKey)
            {
                sql.Append(" UNIQUE");
            }

            // Default value, we do not use the following syntax for SQLite
            // 1. GENERATED ALWAYS
            // 2. STORED OR VIRTUAL
            if (mapping.DefaultValue != null)
            {
                sql.Append($" DEFAULT {this.FormatDefaultValue(mapping.DefaultValue, mapping.SqlType ?? SqlDbType.Text)}");
            }

            // Computed column
            if (mapping.IsComputed && !string.IsNullOrEmpty(mapping.ComputedExpression))
            {
                sql.Append($" AS ({mapping.ComputedExpression})");
                if (mapping.IsPersisted)
                {
                    sql.Append(" PERSISTED");
                }
            }

            return sql.ToString();
        }

        protected virtual string GeneratePrimaryKeyDefinition()
        {
            if (this.HasCompositeKey)
            {
                var keyColumns = string.Join(", ", this.CompositeKeyProperties
                    .OrderBy(p => this.PropertyMappings[p].PrimaryKeyOrder)
                    .Select(p => this.EscapeIdentifier(this.PropertyMappings[p].ColumnName)));
                return $"PRIMARY KEY ({keyColumns})";
            }
            else
            {
                return $"PRIMARY KEY ({this.EscapeIdentifier(this.GetPrimaryKeyColumn())})";
            }
        }

        /// <summary>
        /// Converts a database value to the appropriate C# type.
        /// </summary>
        protected virtual object ConvertDbValueToCSharpType(object dbValue, Type targetType)
        {
            if (dbValue == null || dbValue == DBNull.Value)
                return null;

            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            // Handle common type conversions
            if (underlyingType == typeof(string))
            {
                return dbValue.ToString();
            }
            else if (underlyingType == typeof(int))
            {
                return Convert.ToInt32(dbValue);
            }
            else if (underlyingType == typeof(long))
            {
                return Convert.ToInt64(dbValue);
            }
            else if (underlyingType == typeof(short))
            {
                return Convert.ToInt16(dbValue);
            }
            else if (underlyingType == typeof(byte))
            {
                return Convert.ToByte(dbValue);
            }
            else if (underlyingType == typeof(bool))
            {
                // SQLite stores booleans as integers
                return Convert.ToBoolean(dbValue);
            }
            else if (underlyingType == typeof(decimal))
            {
                return Convert.ToDecimal(dbValue);
            }
            else if (underlyingType == typeof(double))
            {
                return Convert.ToDouble(dbValue);
            }
            else if (underlyingType == typeof(float))
            {
                return Convert.ToSingle(dbValue);
            }
            else if (underlyingType == typeof(DateTime))
            {
                if (dbValue is string dateStr)
                {
                    return DateTime.Parse(dateStr);
                }
                return Convert.ToDateTime(dbValue);
            }
            else if (underlyingType == typeof(DateTimeOffset))
            {
                if (dbValue is string dateOffsetStr)
                {
                    return DateTimeOffset.Parse(dateOffsetStr);
                }
                else if (dbValue is long unixTime)
                {
                    return DateTimeOffset.FromUnixTimeSeconds(unixTime);
                }
                return (DateTimeOffset)dbValue;
            }
            else if (underlyingType == typeof(TimeSpan))
            {
                if (dbValue is string timeStr)
                {
                    return TimeSpan.Parse(timeStr);
                }
                return (TimeSpan)dbValue;
            }
            else if (underlyingType == typeof(Guid))
            {
                if (dbValue is string guidStr)
                {
                    return Guid.Parse(guidStr);
                }
                return (Guid)dbValue;
            }
            else if (underlyingType.IsEnum)
            {
                return Enum.ToObject(underlyingType, dbValue);
            }
            else if (underlyingType == typeof(byte[]))
            {
                return (byte[])dbValue;
            }

            // For complex types, try direct conversion
            try
            {
                return Convert.ChangeType(dbValue, underlyingType);
            }
            catch
            {
                // If conversion fails, return the raw value
                return dbValue;
            }
        }

        /// <summary>
        /// Creates an instance of T using the most appropriate constructor based on available property values.
        /// </summary>
        protected virtual T CreateInstanceWithConstructor(Dictionary<string, object> propertyValues)
        {
            // First, look for a constructor marked with JsonConstructor attribute
            var jsonConstructor = typeof(T).GetConstructors()
                .FirstOrDefault(c => c.GetCustomAttribute<System.Text.Json.Serialization.JsonConstructorAttribute>() != null ||
                                   c.GetCustomAttribute<Newtonsoft.Json.JsonConstructorAttribute>() != null);

            if (jsonConstructor != null)
            {
                // Try to use the JsonConstructor first
                if (this.TryInvokeConstructor(jsonConstructor, propertyValues, out T result))
                {
                    return result;
                }
            }

            // Fallback: try other constructors in order of parameter count
            var constructors = typeof(T).GetConstructors()
                .Where(c => c != jsonConstructor) // Exclude the JsonConstructor we already tried
                .OrderByDescending(c => c.GetParameters().Length)
                .ToArray();

            foreach (var constructor in constructors)
            {
                if (this.TryInvokeConstructor(constructor, propertyValues, out T result))
                {
                    return result;
                }
            }

            // Fallback: try parameterless constructor
            try
            {
                return Activator.CreateInstance<T>();
            }
            catch
            {
                throw new InvalidOperationException(
                    $"Unable to create instance of type {typeof(T).Name}. " +
                    "No suitable constructor found that matches the available property values, " +
                    "and no parameterless constructor is available.");
            }
        }

        /// <summary>
        /// Attempts to invoke a constructor with the given property values.
        /// </summary>
        protected virtual bool TryInvokeConstructor(ConstructorInfo constructor, Dictionary<string, object> propertyValues, out T result)
        {
            result = default(T);
            var parameters = constructor.GetParameters();
            var args = new object[parameters.Length];
            bool canUseConstructor = true;

            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var parameterName = this.GetParameterMappingName(param);

                if (propertyValues.ContainsKey(parameterName) && propertyValues[parameterName] != null)
                {
                    try
                    {
                        // Convert the value to the parameter type if needed
                        args[i] = this.ConvertValueToParameterType(propertyValues[parameterName], param.ParameterType);
                    }
                    catch
                    {
                        // If conversion fails, check if parameter has default value
                        if (param.HasDefaultValue)
                        {
                            args[i] = param.DefaultValue;
                        }
                        else
                        {
                            canUseConstructor = false;
                            break;
                        }
                    }
                }
                else if (param.HasDefaultValue)
                {
                    args[i] = param.DefaultValue;
                }
                else if (param.ParameterType.IsValueType)
                {
                    args[i] = Activator.CreateInstance(param.ParameterType);
                }
                else
                {
                    args[i] = null;
                }
            }

            if (canUseConstructor)
            {
                try
                {
                    result = (T)constructor.Invoke(args);
                    return true;
                }
                catch
                {
                    // Constructor invocation failed
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the property name that a constructor parameter maps to.
        /// For constructors marked with JsonConstructor, uses standard property name matching.
        /// </summary>
        protected virtual string GetParameterMappingName(ParameterInfo parameter)
        {
            // Check if the constructor is marked with JsonConstructor
            var constructor = parameter.Member as ConstructorInfo;
            bool isJsonConstructor = constructor?.GetCustomAttribute<System.Text.Json.Serialization.JsonConstructorAttribute>() != null ||
                                   constructor?.GetCustomAttribute<Newtonsoft.Json.JsonConstructorAttribute>() != null;

            if (isJsonConstructor)
            {
                // For JsonConstructor, we rely on parameter name matching to properties
                // Look for a property that matches the parameter name (case-insensitive)
                var matchingProperty = typeof(T).GetProperties()
                    .FirstOrDefault(p => string.Equals(p.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));

                if (matchingProperty != null)
                {
                    return matchingProperty.Name;
                }

                // If no exact match, try with proper casing (parameter name -> Property name)
                return char.ToUpper(parameter.Name[0]) + parameter.Name.Substring(1);
            }

            // For non-JsonConstructor constructors, use the original logic
            // Check for JsonPropertyName attribute on the parameter (legacy support)
            var jsonPropertyAttr = parameter.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>();
            if (jsonPropertyAttr != null)
            {
                return jsonPropertyAttr.Name;
            }

            // Check for Newtonsoft.Json JsonProperty attribute (legacy support)
            var newtonsoftJsonAttr = parameter.GetCustomAttribute<Newtonsoft.Json.JsonPropertyAttribute>();
            if (newtonsoftJsonAttr != null && !string.IsNullOrEmpty(newtonsoftJsonAttr.PropertyName))
            {
                return newtonsoftJsonAttr.PropertyName;
            }

            // Check if there's a matching property
            var matchingProp = typeof(T).GetProperties()
                .FirstOrDefault(p => string.Equals(p.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));

            if (matchingProp != null)
            {
                return matchingProp.Name;
            }

            // Default: use parameter name with proper casing
            return char.ToUpper(parameter.Name[0]) + parameter.Name.Substring(1);
        }

        /// <summary>
        /// Converts a value to the specified parameter type.
        /// </summary>
        protected virtual object ConvertValueToParameterType(object value, Type parameterType)
        {
            if (value == null)
                return null;

            if (parameterType.IsAssignableFrom(value.GetType()))
                return value;

            var underlyingType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;
            return Convert.ChangeType(value, underlyingType);
        }

        /// <summary>
        /// Sets any properties that weren't set by the constructor.
        /// </summary>
        protected virtual void SetRemainingProperties(T entity, Dictionary<string, object> propertyValues)
        {
            foreach (var mapping in this.PropertyMappings.Values.Where(m => !m.IsNotMapped))
            {
                if (propertyValues.ContainsKey(mapping.PropertyName))
                {
                    try
                    {
                        var currentValue = mapping.PropertyInfo.GetValue(entity);
                        var newValue = propertyValues[mapping.PropertyName];

                        // Only set if the property wasn't already set by constructor
                        // or if the current value is the default value
                        if (currentValue == null ||
                            (currentValue.Equals(this.GetDefaultValue(mapping.PropertyType)) && newValue != null))
                        {
                            var convertedValue = this.ConvertValueToParameterType(newValue, mapping.PropertyType);
                            mapping.PropertyInfo.SetValue(entity, convertedValue);
                        }
                    }
                    catch
                    {
                        // Property setting failed, skip
                    }
                }
            }
        }

        /// <summary>
        /// Gets the default value for a type.
        /// </summary>
        protected virtual object GetDefaultValue(Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            return null;
        }

        #endregion

        #region Private Methods - SQL Generation

        /// <summary>
        /// Generates SELECT command SQL. This is the legacy method kept for backward compatibility.
        /// Use GenerateSelectSql with SelectOptions for new code.
        /// </summary>
        protected string GenerateSelectStatement(
            bool includeAllVersions = false,
            bool includeDeleted = false,
            bool includeExpired = false)
        {
            // Convert legacy parameters to SelectOptions and use the unified method
            var options = new SelectOptions
            {
                IncludeAllVersions = includeAllVersions,
                IncludeDeleted = includeDeleted,
                IncludeExpired = includeExpired,
                WhereClause = $"{this.EscapeIdentifier(this.GetPrimaryKeyColumn())} = @{this.GetPrimaryKeyColumn()}"
            };

            return this.GenerateSelectSql(options);
        }

        /// <summary>
        /// Generates INSERT command SQL.
        /// </summary>
        protected string GenerateInsertStatement()
        {
            var tableName = this.GetTableName();
            var insertColumns = this.GetInsertColumns();
            var escapedColumnNames = string.Join(", ", insertColumns.Select(col => this.EscapeIdentifier(col)));
            var parameterNames = string.Join(", ", insertColumns.Select(col => $"@{col}"));

            return $"INSERT INTO {tableName} ({escapedColumnNames}) VALUES ({parameterNames})";
        }

        /// <summary>
        /// Generates UPDATE command SQL.
        /// </summary>
        protected string GenerateUpdateStatement()
        {
            var tableName = this.GetTableName();
            var updateColumns = this.GetUpdateColumns();
            var primaryKeyColumns = this.GetPrimaryKeyColumns();

            var setClause = string.Join(", ", updateColumns.Select(col => $"{this.EscapeIdentifier(col)} = @{col}"));
            var whereClause = string.Join(" AND ", primaryKeyColumns.Select(col => $"{this.EscapeIdentifier(col)} = @{col}"));

            return $"UPDATE {tableName} SET {setClause} WHERE {whereClause}";
        }

        /// <summary>
        /// Generates DELETE command SQL.
        /// </summary>
        protected string GenerateDeleteStatement()
        {
            var tableName = this.GetTableName();
            var primaryKeyColumns = this.GetPrimaryKeyColumns();
            var whereClause = string.Join(" AND ", primaryKeyColumns.Select(col => $"{this.EscapeIdentifier(col)} = @{col}"));

            return $"DELETE FROM {tableName} WHERE {whereClause}";
        }

        /// <summary>
        /// Adds parameters for SELECT operation.
        /// </summary>
        protected void AddSelectParameters(IDbCommand command, TKey entityKey)
        {
            var primaryKeyMappings = this.PropertyMappings.Values
                .Where(m => m.IsPrimaryKey)
                .OrderBy(m => m.PrimaryKeyOrder);

            foreach (var mapping in primaryKeyMappings)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@{mapping.ColumnName}";
                parameter.Value = entityKey;
                command.Parameters.Add(parameter);
            }
        }

        /// <summary>
        /// Adds parameters for UPDATE operation.
        /// </summary>
        protected void AddUpdateParameters(IDbCommand command, T fromValue, T toValue)
        {
            // Add parameters for the SET clause (new values)
            foreach (var mapping in this.PropertyMappings.Values.Where(m => !m.IsNotMapped && !m.IsComputed && !m.IsPrimaryKey))
            {
                var value = mapping.PropertyInfo.GetValue(toValue);
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@{mapping.ColumnName}";
                parameter.Value = this.ConvertParameterValue(value);
                command.Parameters.Add(parameter);
            }

            // Add parameters for the WHERE clause (old primary key values)
            var primaryKeyMappings = this.PropertyMappings.Values
                .Where(m => m.IsPrimaryKey)
                .OrderBy(m => m.PrimaryKeyOrder);

            foreach (var mapping in primaryKeyMappings)
            {
                var value = mapping.PropertyInfo.GetValue(fromValue);
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@old_{mapping.ColumnName}";
                parameter.Value = value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
        }

        /// <summary>
        /// Adds parameters for DELETE operation.
        /// </summary>
        private void AddDeleteParameters(IDbCommand command, T entity)
        {
            var primaryKeyMappings = this.PropertyMappings.Values
                .Where(m => m.IsPrimaryKey)
                .OrderBy(m => m.PrimaryKeyOrder);

            foreach (var mapping in primaryKeyMappings)
            {
                var value = mapping.PropertyInfo.GetValue(entity);
                var parameter = command.CreateParameter();
                parameter.ParameterName = $"@{mapping.ColumnName}";
                parameter.Value = value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// When property is overriden with new, we should only keep the most derived property.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        private void BuildPropertyMappings()
        {
            var properties = this.EntityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var indexGroups = new Dictionary<string, List<IndexColumn>>();
            var foreignKeyGroups = new Dictionary<string, List<(PropertyInfo Property, ForeignKeyAttribute Attribute, string ColumnName)>>();

            // Lists to collect primary key information during property evaluation
            var primaryKeyPropertiesWithAttributes = new List<(PropertyInfo Property, PrimaryKeyAttribute Attribute)>();
            PropertyInfo conventionBasedPrimaryKey = null;

            // Group properties by name to handle property hiding scenarios
            var propertyGroups = properties.GroupBy(p => p.Name);

            foreach (var propertyGroup in propertyGroups)
            {
                // For properties with the same name (property hiding), use the most derived one
                var property = propertyGroup.OrderBy(p => p.DeclaringType, new TypeHierarchyComparer(this.EntityType)).First();

                // Check if property should be excluded - check on the most derived property
                if (property.GetCustomAttribute<NotMappedAttribute>() != null)
                {
                    continue;
                }

                // Create property mapping
                var mapping = this.CreatePropertyMapping(property);
                this.PropertyMappings[property] = mapping;

                // Collect primary key information (don't set flags yet)
                var pkAttr = property.GetCustomAttribute<PrimaryKeyAttribute>();
                if (pkAttr != null)
                {
                    primaryKeyPropertiesWithAttributes.Add((property, pkAttr));
                }
                else if (property.Name == "Id" || property.Name == "Key")
                {
                    // Remember potential convention-based primary key
                    conventionBasedPrimaryKey = property;
                }

                // Process indexes
                var indexAttrs = property.GetCustomAttributes<IndexAttribute>();
                foreach (var indexAttr in indexAttrs)
                {
                    var indexName = indexAttr.Name ?? $"IX_{this.TableName}_{mapping.ColumnName}";

                    if (!indexGroups.ContainsKey(indexName))
                    {
                        indexGroups[indexName] = new List<IndexColumn>();
                    }

                    indexGroups[indexName].Add(new IndexColumn
                    {
                        ColumnName = mapping.ColumnName,
                        Order = indexAttr.Order,
                        IsIncluded = indexAttr.IsIncluded
                    });
                }

                // Process foreign keys
                var fkAttr = property.GetCustomAttribute<ForeignKeyAttribute>();
                if (fkAttr != null)
                {
                    var constraintName = fkAttr.Name ?? $"FK_{this.TableName}_{property.Name}";

                    if (!foreignKeyGroups.ContainsKey(constraintName))
                    {
                        foreignKeyGroups[constraintName] = new List<(PropertyInfo, ForeignKeyAttribute, string)>();
                    }

                    foreignKeyGroups[constraintName].Add((property, fkAttr, mapping.ColumnName));
                }
            }

            // Process primary keys after all properties have been evaluated
            if (primaryKeyPropertiesWithAttributes.Any())
            {
                // Check if any property has IsComposite=true
                if (primaryKeyPropertiesWithAttributes.Any(pk => pk.Attribute.IsComposite))
                {
                    this.HasCompositeKey = true;

                    // Add all properties with [PrimaryKey] to composite key list
                    foreach (var (property, attr) in primaryKeyPropertiesWithAttributes)
                    {
                        this.CompositeKeyProperties.Add(property);
                        // Ensure the mapping is marked as primary key
                        if (this.PropertyMappings.TryGetValue(property, out var mapping))
                        {
                            mapping.IsPrimaryKey = true;
                            mapping.PrimaryKeyOrder = attr.Order;
                        }
                    }
                }
                else if (primaryKeyPropertiesWithAttributes.Count == 1)
                {
                    // Single primary key with explicit attribute
                    var (property, attr) = primaryKeyPropertiesWithAttributes.First();
                    this.PrimaryKeyProperty = property;
                    if (this.PropertyMappings.TryGetValue(property, out var mapping))
                    {
                        mapping.IsPrimaryKey = true;
                        mapping.PrimaryKeyOrder = attr.Order;
                    }
                }
                else
                {
                    // Multiple [PrimaryKey] attributes without IsComposite=true means composite key
                    this.HasCompositeKey = true;
                    foreach (var (property, attr) in primaryKeyPropertiesWithAttributes)
                    {
                        this.CompositeKeyProperties.Add(property);
                        if (this.PropertyMappings.TryGetValue(property, out var mapping))
                        {
                            mapping.IsPrimaryKey = true;
                            mapping.PrimaryKeyOrder = attr.Order;
                        }
                    }
                }
            }
            else if (conventionBasedPrimaryKey != null)
            {
                // Use convention-based primary key only if no explicit [PrimaryKey] attributes found
                this.PrimaryKeyProperty = conventionBasedPrimaryKey;
                if (this.PropertyMappings.TryGetValue(conventionBasedPrimaryKey, out var mapping))
                {
                    mapping.IsPrimaryKey = true;
                    mapping.PrimaryKeyOrder = 0;
                }
            }

            // Special handling for soft delete with Version as part of composite key
            if (this.EnableSoftDelete && this.PrimaryKeyProperty != null)
            {
                // When soft delete is enabled, the primary key becomes composite (Id + Version)
                this.HasCompositeKey = true;

                // Add existing primary key to composite keys if not already there
                if (!this.CompositeKeyProperties.Contains(this.PrimaryKeyProperty))
                {
                    this.CompositeKeyProperties.Add(this.PrimaryKeyProperty);
                }

                // Find and add Version property
                var versionProperty = this.PropertyMappings
                    .FirstOrDefault(m => m.Value.PropertyName == "Version").Key;

                if (versionProperty != null && !this.CompositeKeyProperties.Contains(versionProperty))
                {
                    this.CompositeKeyProperties.Add(versionProperty);
                    if (this.PropertyMappings.TryGetValue(versionProperty, out var versionMapping))
                    {
                        versionMapping.IsPrimaryKey = true;
                        versionMapping.PrimaryKeyOrder = 1; // Version comes after Id
                    }
                }

                // Clear single primary key reference when it becomes composite
                this.PrimaryKeyProperty = null;
            }

            // Build foreign key definitions from grouped columns
            foreach (var fkGroup in foreignKeyGroups)
            {
                var orderedItems = fkGroup.Value.OrderBy(x => x.Attribute.Ordinal).ToList();
                var firstAttr = orderedItems.First().Attribute;

                if (orderedItems.Count > 1)
                {
                    // Composite foreign key - validate all attributes have same table and actions
                    var allSameTable = orderedItems.All(x => x.Attribute.ReferencedTable == firstAttr.ReferencedTable);
                    var allSameDelete = orderedItems.All(x => x.Attribute.OnDelete == firstAttr.OnDelete);
                    var allSameUpdate = orderedItems.All(x => x.Attribute.OnUpdate == firstAttr.OnUpdate);

                    if (!allSameTable || !allSameDelete || !allSameUpdate)
                    {
                        throw new InvalidOperationException(
                            $"Composite foreign key '{fkGroup.Key}' has inconsistent attributes. " +
                            "All properties must reference the same table and have the same ON DELETE/UPDATE actions.");
                    }

                    // Build arrays of columns and referenced columns in ordinal order
                    var columns = orderedItems.Select(x => x.ColumnName).ToArray();
                    var referencedColumns = orderedItems.Select(x => x.Attribute.ReferencedColumn).ToArray();

                    this.ForeignKeys.Add(new ForeignKeyDefinition
                    {
                        ConstraintName = fkGroup.Key,
                        ColumnNames = columns,
                        ReferencedTable = firstAttr.ReferencedTable,
                        ReferencedColumns = referencedColumns,
                        OnDelete = firstAttr.OnDelete,
                        OnUpdate = firstAttr.OnUpdate
                    });
                }
                else
                {
                    // Single column foreign key
                    var item = orderedItems.First();
                    this.ForeignKeys.Add(new ForeignKeyDefinition
                    {
                        ConstraintName = fkGroup.Key,
                        ColumnName = item.ColumnName,
                        ReferencedTable = item.Attribute.ReferencedTable,
                        ReferencedColumn = item.Attribute.ReferencedColumn,
                        OnDelete = item.Attribute.OnDelete,
                        OnUpdate = item.Attribute.OnUpdate
                    });
                }
            }

            // Build index definitions from grouped columns
            foreach (var group in indexGroups)
            {
                var firstColumn = group.Value.First();
                var firstIndexAttr = properties
                    .SelectMany(p => p.GetCustomAttributes<IndexAttribute>()
                        .Where(a => (a.Name ?? $"IX_{this.TableName}_{this.PropertyMappings[p].ColumnName}") == group.Key))
                    .First();

                this.Indexes.Add(new IndexDefinition
                {
                    Name = group.Key,
                    Columns = group.Value,
                    IsUnique = firstIndexAttr.IsUnique,
                    IsClustered = firstIndexAttr.IsClustered,
                    Filter = firstIndexAttr.Filter
                });
            }
        }

        private PropertyMapping CreatePropertyMapping(PropertyInfo property)
        {
            var mapping = new PropertyMapping
            {
                PropertyInfo = property,
                PropertyName = property.Name,
                PropertyType = property.PropertyType,
                IsNotMapped = false
            };

            // Get column attribute
            var columnAttr = property.GetCustomAttribute<ColumnAttribute>();

            // Column name
            mapping.ColumnName = columnAttr?.Name ?? property.Name;

            // Data type
            if (columnAttr?.SqlType != null)
            {
                mapping.SqlType = columnAttr.SqlType.Value;
                mapping.Size = columnAttr.Size;
                mapping.Precision = columnAttr.Precision;
                mapping.Scale = columnAttr.Scale;
            }
            else
            {
                // Infer SQL type from property type
                this.InferSqlType(property.PropertyType, mapping);
            }

            // Nullability - Use NotNull property (inverted logic)
            mapping.IsNotNull = columnAttr?.NotNull == true;

            // Default value
            mapping.DefaultValue = columnAttr?.DefaultValue;
            mapping.DefaultConstraintName = columnAttr?.DefaultConstraintName;

            // Check constraint
            var checkAttr = property.GetCustomAttribute<CheckAttribute>();
            if (checkAttr != null)
            {
                mapping.CheckConstraint = checkAttr.Expression;
                mapping.CheckConstraintName = checkAttr.Name ?? $"CK_{this.TableName}_{mapping.ColumnName}";
            }

            // Computed column
            var computedAttr = property.GetCustomAttribute<ComputedAttribute>();
            if (computedAttr != null)
            {
                mapping.IsComputed = true;
                mapping.ComputedExpression = computedAttr.Expression;
                mapping.IsPersisted = computedAttr.IsPersisted;
            }

            // Audit fields
            var auditAttr = property.GetCustomAttribute<AuditFieldAttribute>();
            if (auditAttr != null)
            {
                mapping.IsAuditField = true;
                mapping.AuditFieldType = auditAttr.FieldType;
            }

            // Primary key
            var pkAttr = property.GetCustomAttribute<PrimaryKeyAttribute>();
            if (pkAttr != null)
            {
                mapping.IsPrimaryKey = true;
                mapping.PrimaryKeyOrder = pkAttr.Order;
                mapping.IsAutoIncrement = pkAttr.IsAutoIncrement;
                mapping.SequenceName = pkAttr.SequenceName;
            }

            // Unique constraint
            var uniqueAttr = property.GetCustomAttribute<UniqueAttribute>();
            if (uniqueAttr != null)
            {
                mapping.IsUnique = true;
                mapping.UniqueConstraintName = uniqueAttr.Name ?? $"UQ_{this.TableName}_{mapping.ColumnName}";
            }

            return mapping;
        }

        private void InferSqlType(Type clrType, PropertyMapping mapping)
        {
            // Use the new extension method to get SqlDbType with metadata
            mapping.SqlType = clrType.ToSqlDbType(out var size, out var precision, out var scale);

            // Apply the metadata if provided
            if (size.HasValue)
            {
                mapping.Size = size.Value;
            }

            if (precision.HasValue)
            {
                mapping.Precision = precision.Value;
            }

            if (scale.HasValue)
            {
                mapping.Scale = scale.Value;
            }
        }

        private bool IsNullableType(Type type)
        {
            return type.IsNullable();
        }

        private string FormatDefaultValue(object value, SqlDbType sqlType)
        {
            if (value == null)
                return "NULL";

            if (value is string strValue)
            {
                return $"'{strValue.Replace("'", "''")}'";
            }

            if (value is bool boolValue)
            {
                return boolValue ? "1" : "0";
            }

            if (value is DateTime || value is DateTimeOffset)
            {
                return "datetime('now')";
            }

            if (value.GetType().IsEnum)
            {
                return ((int)value).ToString();
            }

            return value.ToString();
        }

        private string GenerateForeignKeyConstraint(ForeignKeyDefinition fk)
        {
            var sql = new StringBuilder();
            sql.Append($"CONSTRAINT {fk.ConstraintName} ");

            // Handle composite foreign keys
            if (fk.IsComposite)
            {
                var columns = string.Join(", ", fk.ColumnNames.Select(c => this.EscapeIdentifier(c)));
                var referencedColumns = string.Join(", ", fk.ReferencedColumns.Select(c => this.EscapeIdentifier(c)));
                sql.Append($"FOREIGN KEY ({columns}) ");
                sql.Append($"REFERENCES {fk.ReferencedTable}({referencedColumns})");
            }
            else
            {
                sql.Append($"FOREIGN KEY ({this.EscapeIdentifier(fk.ColumnName)}) ");
                sql.Append($"REFERENCES {fk.ReferencedTable}({this.EscapeIdentifier(fk.ReferencedColumn)})");
            }

            if (fk.OnDelete != ForeignKeyAction.NoAction)
            {
                sql.Append($" ON DELETE {this.ConvertForeignKeyActionToSql(fk.OnDelete)}");
            }

            if (fk.OnUpdate != ForeignKeyAction.NoAction)
            {
                sql.Append($" ON UPDATE {this.ConvertForeignKeyActionToSql(fk.OnUpdate)}");
            }

            return sql.ToString();
        }

        private string ConvertForeignKeyActionToSql(ForeignKeyAction action)
        {
            switch (action)
            {
                case ForeignKeyAction.NoAction:
                    return "NO ACTION";
                case ForeignKeyAction.Cascade:
                    return "CASCADE";
                case ForeignKeyAction.SetNull:
                    return "SET NULL";
                case ForeignKeyAction.SetDefault:
                    return "SET DEFAULT";
                case ForeignKeyAction.Restrict:
                    return "RESTRICT";
                default:
                    return "NO ACTION";
            }
        }

        #endregion
    }
}