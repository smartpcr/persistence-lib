//-------------------------------------------------------------------------------
// <copyright file="SqlServerEntityMapper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SqlServer.Mappings
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Text;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    /// <summary>
    /// SQL Server-specific implementation of the improved entity mapper.
    /// </summary>
    /// <typeparam name="T">The entity type to map</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    public class SqlServerEntityMapper<T, TKey> : BaseEntityMapperV2<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        #region Factory Method

        protected override IDbCommand CreateDbCommand() => new SqlCommand();

        #endregion

        #region SQL Server-Specific SQL Generation

        protected override string EscapeIdentifier(string identifier) => $"[{identifier}]";

        protected override string GetAutoIncrementSyntax() => "IDENTITY(1,1)";

        protected override string GetCurrentTimestampFunction() => "GETUTCDATE()";

        protected override string GetBooleanLiteral(bool value) => value ? "1" : "0";

        protected override string BuildLimitClause(SelectOptions options)
        {
            // SQL Server 2012+ syntax with OFFSET/FETCH
            if (options.Limit.HasValue)
            {
                // SQL Server requires ORDER BY when using OFFSET/FETCH
                if (string.IsNullOrEmpty(options.OrderBy))
                {
                    // Default ordering by primary key if not specified
                    var pkColumn = GetPrimaryKeyColumn();
                    return $"ORDER BY {EscapeIdentifier(pkColumn)} " +
                           $"OFFSET {options.Offset ?? 0} ROWS " +
                           $"FETCH NEXT {options.Limit.Value} ROWS ONLY";
                }
                else
                {
                    return $"OFFSET {options.Offset ?? 0} ROWS " +
                           $"FETCH NEXT {options.Limit.Value} ROWS ONLY";
                }
            }
            return string.Empty;
        }

        protected override string BuildOrderByClause(SelectOptions options)
        {
            if (!string.IsNullOrEmpty(options.OrderBy))
            {
                return $"ORDER BY {options.OrderBy}";
            }

            // SQL Server requires ORDER BY for OFFSET/FETCH
            if (options.Limit.HasValue)
            {
                var pkColumn = GetPrimaryKeyColumn();
                return $"ORDER BY {EscapeIdentifier(pkColumn)}";
            }

            if (this.EnableSoftDelete && !options.IncludeAllVersions)
            {
                return $"ORDER BY {EscapeIdentifier("Version")} DESC";
            }

            return string.Empty;
        }

        #endregion

        #region SQL Server-Specific Type Mapping

        public override string GetSqlTypeString(PropertyMapping mapping)
        {
            switch (mapping.SqlType)
            {
                case SqlDbType.NVarChar:
                    return mapping.MaxLength.HasValue 
                        ? $"NVARCHAR({mapping.MaxLength})" 
                        : "NVARCHAR(MAX)";
                        
                case SqlDbType.VarChar:
                    return mapping.MaxLength.HasValue 
                        ? $"VARCHAR({mapping.MaxLength})" 
                        : "VARCHAR(MAX)";
                        
                case SqlDbType.NChar:
                    return $"NCHAR({mapping.MaxLength ?? 1})";
                    
                case SqlDbType.Char:
                    return $"CHAR({mapping.MaxLength ?? 1})";
                    
                case SqlDbType.Binary:
                    return mapping.MaxLength.HasValue 
                        ? $"BINARY({mapping.MaxLength})" 
                        : "BINARY(1)";
                        
                case SqlDbType.VarBinary:
                    return mapping.MaxLength.HasValue 
                        ? $"VARBINARY({mapping.MaxLength})" 
                        : "VARBINARY(MAX)";
                        
                case SqlDbType.Int:
                    return "INT";
                    
                case SqlDbType.BigInt:
                    return "BIGINT";
                    
                case SqlDbType.SmallInt:
                    return "SMALLINT";
                    
                case SqlDbType.TinyInt:
                    return "TINYINT";
                    
                case SqlDbType.Bit:
                    return "BIT";
                    
                case SqlDbType.Float:
                    return "FLOAT";
                    
                case SqlDbType.Real:
                    return "REAL";
                    
                case SqlDbType.Decimal:
                    return $"DECIMAL({mapping.Precision ?? 18},{mapping.Scale ?? 2})";
                    
                case SqlDbType.Money:
                    return "MONEY";
                    
                case SqlDbType.SmallMoney:
                    return "SMALLMONEY";
                    
                case SqlDbType.DateTime:
                    return "DATETIME";
                    
                case SqlDbType.DateTime2:
                    return $"DATETIME2({mapping.Precision ?? 7})";
                    
                case SqlDbType.DateTimeOffset:
                    return $"DATETIMEOFFSET({mapping.Precision ?? 7})";
                    
                case SqlDbType.Date:
                    return "DATE";
                    
                case SqlDbType.Time:
                    return $"TIME({mapping.Precision ?? 7})";
                    
                case SqlDbType.UniqueIdentifier:
                    return "UNIQUEIDENTIFIER";
                    
                case SqlDbType.Xml:
                    return "XML";
                    
                case SqlDbType.Text:
                    return "TEXT";
                    
                case SqlDbType.NText:
                    return "NTEXT";
                    
                case SqlDbType.Image:
                    return "IMAGE";
                    
                default:
                    return "NVARCHAR(MAX)";
            }
        }

        #endregion

        #region SQL Server-Specific Parameter Conversion

        protected override object ConvertParameterValue(object value)
        {
            if (value == null) return DBNull.Value;
            
            // SQL Server handles most types natively
            if (value is DateTimeOffset dto)
                return dto;
            if (value is DateTime dt)
                return dt;
            if (value is TimeSpan ts)
                return ts;
            if (value is Guid g)
                return g;
            if (value is bool b)
                return b;
            if (value is Enum e)
                return Convert.ToInt32(e);
                
            return base.ConvertParameterValue(value);
        }

        protected override object ConvertDbValueToCSharpType(object dbValue, Type targetType)
        {
            if (dbValue == null || dbValue == DBNull.Value)
                return null;

            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            // SQL Server returns SqlTypes, so we need to handle them
            if (dbValue.GetType().Namespace == "System.Data.SqlTypes")
            {
                // Convert SqlTypes to CLR types
                if (dbValue is System.Data.SqlTypes.SqlBoolean sqlBool)
                    dbValue = sqlBool.Value;
                else if (dbValue is System.Data.SqlTypes.SqlDateTime sqlDateTime)
                    dbValue = sqlDateTime.Value;
                else if (dbValue is System.Data.SqlTypes.SqlGuid sqlGuid)
                    dbValue = sqlGuid.Value;
                else if (dbValue is System.Data.SqlTypes.SqlInt32 sqlInt32)
                    dbValue = sqlInt32.Value;
                else if (dbValue is System.Data.SqlTypes.SqlInt64 sqlInt64)
                    dbValue = sqlInt64.Value;
                else if (dbValue is System.Data.SqlTypes.SqlString sqlString)
                    dbValue = sqlString.Value;
                else if (dbValue is System.Data.SqlTypes.SqlDecimal sqlDecimal)
                    dbValue = sqlDecimal.Value;
                else if (dbValue is System.Data.SqlTypes.SqlMoney sqlMoney)
                    dbValue = sqlMoney.Value;
            }

            return base.ConvertDbValueToCSharpType(dbValue, targetType);
        }

        #endregion

        #region SQL Server-Specific Operations

        protected override void ConfigureUpsertCommand(IDbCommand command, CommandContext<T, TKey> context)
        {
            if (context.Entity == null)
                throw new ArgumentException("Entity is required for upsert operation");

            // SQL Server specific: MERGE statement
            var pkColumn = GetPrimaryKeyColumn();
            var insertColumns = GetInsertColumns();
            var updateColumns = GetUpdateColumns();

            var mergeStatement = new StringBuilder();
            mergeStatement.AppendLine($"MERGE {GetFullTableName()} AS target");
            mergeStatement.AppendLine($"USING (SELECT {GetParameterPrefix()}{pkColumn} AS {EscapeIdentifier(pkColumn)}) AS source");
            mergeStatement.AppendLine($"ON target.{EscapeIdentifier(pkColumn)} = source.{EscapeIdentifier(pkColumn)}");
            
            // WHEN MATCHED - Update existing record
            if (updateColumns.Any())
            {
                mergeStatement.AppendLine("WHEN MATCHED THEN");
                mergeStatement.Append("    UPDATE SET ");
                mergeStatement.AppendLine(string.Join(", ", updateColumns.Select(c => 
                    $"{EscapeIdentifier(c)} = {GetParameterPrefix()}{c}")));
            }
            
            // WHEN NOT MATCHED - Insert new record
            mergeStatement.AppendLine("WHEN NOT MATCHED THEN");
            mergeStatement.Append($"    INSERT ({string.Join(", ", insertColumns.Select(EscapeIdentifier))})");
            mergeStatement.AppendLine($"    VALUES ({string.Join(", ", insertColumns.Select(c => GetParameterPrefix() + c))})");
            
            // Add OUTPUT clause if needed
            if (this.PropertyMappings.Values.Any(m => m.IsAutoIncrement))
            {
                var autoIncrementColumn = this.PropertyMappings.Values
                    .First(m => m.IsAutoIncrement).ColumnName;
                mergeStatement.AppendLine($"OUTPUT INSERTED.{EscapeIdentifier(autoIncrementColumn)}");
            }
            
            mergeStatement.Append(";");

            command.CommandText = mergeStatement.ToString();
            AddEntityParameters(command, context.Entity);
        }

        public override string GenerateBatchInsertSql(int entityCount)
        {
            var columns = GetInsertColumns();
            
            if (this.EnableSoftDelete)
            {
                columns = columns
                    .Where(c => !c.Equals("IsDeleted", StringComparison.OrdinalIgnoreCase) && 
                               !c.Equals("Version", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                columns.Add("Version");
                columns.Add("IsDeleted");
            }
            
            var columnList = string.Join(", ", columns.Select(EscapeIdentifier));
            
            // SQL Server supports table value constructors
            var valueRows = new List<string>();
            for (int i = 0; i < entityCount; i++)
            {
                var paramList = string.Join(", ", columns.Select(c => 
                {
                    if (c == "IsDeleted" && this.EnableSoftDelete)
                        return GetBooleanLiteral(false);
                    else if (c == "Version" && this.EnableSoftDelete)
                        return $"{GetParameterPrefix()}NextVersion_{i}";
                    else
                        return $"{GetParameterPrefix()}{c}_{i}";
                }));
                valueRows.Add($"({paramList})");
            }
            
            var sql = new StringBuilder();
            sql.AppendLine($"INSERT INTO {GetFullTableName()} ({columnList})");
            
            // Use OUTPUT clause to return inserted IDs if there's an identity column
            if (this.PropertyMappings.Values.Any(m => m.IsAutoIncrement))
            {
                var autoIncrementColumn = this.PropertyMappings.Values
                    .First(m => m.IsAutoIncrement).ColumnName;
                sql.AppendLine($"OUTPUT INSERTED.{EscapeIdentifier(autoIncrementColumn)}");
            }
            
            sql.Append($"VALUES {string.Join(", ", valueRows)}");
            
            return sql.ToString();
        }

        #endregion

        #region SQL Server-Specific DDL

        public override string GenerateCreateTableSql(bool includeIfNotExists = true)
        {
            var sql = new StringBuilder();
            
            // SQL Server doesn't support IF NOT EXISTS in CREATE TABLE directly
            if (includeIfNotExists)
            {
                sql.AppendLine($"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '{this.TableName}'");
                if (!string.IsNullOrEmpty(this.SchemaName))
                {
                    sql.AppendLine($"    AND schema_id = SCHEMA_ID('{this.SchemaName}'))");
                }
                else
                {
                    sql.AppendLine(")");
                }
                sql.AppendLine("BEGIN");
            }
            
            sql.AppendLine($"CREATE TABLE {GetFullTableName()} (");

            // Generate column definitions
            var columnDefinitions = new List<string>();
            foreach (var mapping in this.PropertyMappings.Values.Where(m => !m.IsNotMapped))
            {
                columnDefinitions.Add(GenerateColumnDefinition(mapping));
            }

            sql.Append("    " + string.Join(",\n    ", columnDefinitions));

            // Add primary key constraint if not using identity
            var pkDefinition = GeneratePrimaryKeyDefinition();
            if (!string.IsNullOrEmpty(pkDefinition))
            {
                sql.Append($",\n    {pkDefinition}");
            }

            // Add foreign key constraints
            foreach (var fk in this.ForeignKeys)
            {
                sql.Append($",\n    {GenerateForeignKeyDefinition(fk)}");
            }

            sql.AppendLine("\n)");
            
            if (includeIfNotExists)
            {
                sql.AppendLine("END");
            }

            return sql.ToString();
        }

        protected override string GenerateColumnDefinition(PropertyMapping mapping)
        {
            var sql = new StringBuilder();
            sql.Append($"{EscapeIdentifier(mapping.ColumnName)} ");
            sql.Append(GetSqlTypeString(mapping));

            if (mapping.IsPrimaryKey && mapping.IsAutoIncrement)
            {
                sql.Append($" {GetAutoIncrementSyntax()}");
            }

            if (mapping.IsRequired || mapping.IsPrimaryKey)
            {
                sql.Append(" NOT NULL");
            }
            else
            {
                sql.Append(" NULL");
            }

            if (mapping.IsUnique && !mapping.IsPrimaryKey)
            {
                sql.Append(" UNIQUE");
            }

            if (mapping.DefaultValue != null)
            {
                sql.Append($" DEFAULT {mapping.DefaultValue}");
            }

            return sql.ToString();
        }

        protected override string GenerateForeignKeyDefinition(ForeignKeyDefinition fk)
        {
            var fkName = fk.Name ?? $"FK_{this.TableName}_{fk.ForeignTable}_{fk.LocalColumn}";
            var onDelete = fk.OnDelete.HasValue ? $" ON DELETE {fk.OnDelete.Value.ToString().Replace("_", " ")}" : "";
            var onUpdate = fk.OnUpdate.HasValue ? $" ON UPDATE {fk.OnUpdate.Value.ToString().Replace("_", " ")}" : "";
            
            return $"CONSTRAINT {EscapeIdentifier(fkName)} FOREIGN KEY ({EscapeIdentifier(fk.LocalColumn)}) " +
                   $"REFERENCES {fk.ForeignTable} ({EscapeIdentifier(fk.ForeignColumn)}){onDelete}{onUpdate}";
        }

        #endregion

        #region SQL Server-Specific Index Creation

        public override IEnumerable<string> GenerateCreateIndexSql()
        {
            var indexSqls = new List<string>();

            foreach (var index in this.Indexes)
            {
                var indexName = index.Name ?? $"IX_{this.TableName}_{string.Join("_", index.Columns)}";
                var unique = index.IsUnique ? "UNIQUE " : "";
                var clustered = index.IsClustered ? "CLUSTERED " : "NONCLUSTERED ";
                var columns = string.Join(", ", index.Columns.Select(c => 
                    $"{EscapeIdentifier(c)} {(index.IsDescending ? "DESC" : "ASC")}"));
                
                // SQL Server supports filtered indexes
                var where = !string.IsNullOrEmpty(index.WhereClause) ? $" WHERE {index.WhereClause}" : "";
                
                // SQL Server supports included columns
                var include = index.IncludedColumns?.Any() == true
                    ? $" INCLUDE ({string.Join(", ", index.IncludedColumns.Select(EscapeIdentifier))})"
                    : "";
                
                indexSqls.Add($"CREATE {unique}{clustered}INDEX {EscapeIdentifier(indexName)} " +
                             $"ON {GetFullTableName()} ({columns}){include}{where};");
            }

            // Add default indexes for soft delete if enabled
            if (this.EnableSoftDelete)
            {
                var pkColumn = GetPrimaryKeyColumn();
                indexSqls.Add($"CREATE NONCLUSTERED INDEX {EscapeIdentifier($"IX_{this.TableName}_SoftDelete")} " +
                             $"ON {GetFullTableName()} ({EscapeIdentifier(pkColumn)}, {EscapeIdentifier("Version")} DESC) " +
                             $"WHERE {EscapeIdentifier("IsDeleted")} = 0;");
            }

            // Add index for expiry if enabled
            if (this.EnableExpiry)
            {
                indexSqls.Add($"CREATE NONCLUSTERED INDEX {EscapeIdentifier($"IX_{this.TableName}_Expiry")} " +
                             $"ON {GetFullTableName()} ({EscapeIdentifier("ExpiryTime")}) " +
                             $"INCLUDE ({EscapeIdentifier(GetPrimaryKeyColumn())});");
            }

            return indexSqls;
        }

        #endregion

        #region SQL Server-Specific Features

        /// <summary>
        /// Generates a temporal table (system-versioned table) for SQL Server 2016+.
        /// </summary>
        public string GenerateTemporalTableSql()
        {
            var sql = new StringBuilder();
            sql.AppendLine($"CREATE TABLE {GetFullTableName()} (");
            
            // Add regular columns
            var columnDefinitions = new List<string>();
            foreach (var mapping in this.PropertyMappings.Values.Where(m => !m.IsNotMapped))
            {
                columnDefinitions.Add(GenerateColumnDefinition(mapping));
            }
            
            // Add temporal columns
            columnDefinitions.Add("SysStartTime DATETIME2 GENERATED ALWAYS AS ROW START NOT NULL");
            columnDefinitions.Add("SysEndTime DATETIME2 GENERATED ALWAYS AS ROW END NOT NULL");
            
            sql.Append("    " + string.Join(",\n    ", columnDefinitions));
            
            // Add period for system time
            sql.AppendLine(",");
            sql.AppendLine("    PERIOD FOR SYSTEM_TIME (SysStartTime, SysEndTime)");
            
            // Add primary key
            var pkDefinition = GeneratePrimaryKeyDefinition();
            if (!string.IsNullOrEmpty(pkDefinition))
            {
                sql.Append($",\n    {pkDefinition}");
            }
            
            sql.AppendLine("\n)");
            sql.AppendLine($"WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = {this.SchemaName ?? "dbo"}.{this.TableName}_History));");
            
            return sql.ToString();
        }

        #endregion
    }
}