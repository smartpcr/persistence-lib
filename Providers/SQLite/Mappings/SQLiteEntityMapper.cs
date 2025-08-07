//-------------------------------------------------------------------------------
// <copyright file="SQLiteEntityMapper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Mappings
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Data.SQLite;
    using System.Linq;
    using System.Text;
    using Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;


    /// <summary>
    /// SQLite-specific implementation of the entity mapper.
    /// </summary>
    /// <typeparam name="T">The entity type to map</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    public class SQLiteEntityMapper<T, TKey> : BaseEntityMapper<T, TKey> where T : class, IEntity<TKey> where TKey : IEquatable<TKey>
    {

        #region SQLite-Specific Overrides

        protected override string EscapeIdentifier(string identifier) //=> $"`{identifier}`";
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
                return $"\"{identifier}\"";
            }

            return identifier;
        }
        
        protected override string GetAutoIncrementSyntax() => "AUTOINCREMENT";
        
        protected override string GetCurrentTimestampFunction() => "datetime('now')";
        
        protected override string GetBooleanLiteral(bool value) => value ? "1" : "0";
        
        protected override IDbCommand CreateDbCommand() => new SQLiteCommand();

        /// <summary>
        /// SQLite stores datetime as text, so we need to use datetime() function for proper comparison.
        /// </summary>
        protected override string GetExpiryFilterCondition()
        {
            return $"datetime({this.EscapeIdentifier("AbsoluteExpiration")}) > {this.GetCurrentTimestampFunction()}";
        }

        /// <summary>
        /// SQLite stores datetime as text, so we need to use datetime() function for proper comparison.
        /// </summary>
        protected override string GetExpiryFilterConditionWithAlias(string tableAlias)
        {
            return $"({tableAlias}.{this.EscapeIdentifier("AbsoluteExpiration")} IS NULL OR datetime({tableAlias}.{this.EscapeIdentifier("AbsoluteExpiration")}) > {this.GetCurrentTimestampFunction()})";
        }

        protected override object ConvertParameterValue(object value)
        {
            if (value == null) return DBNull.Value;
            if (value is bool b) return b ? 1 : 0;
            if (value is DateTimeOffset dto) return dto.ToString("O");
            if (value is DateTime dt) return dt.ToString("O");
            if (value is TimeSpan ts) return ts.TotalSeconds;
            if (value is Guid g) return g.ToString();
            if (value is Enum e) return Convert.ToInt32(e);
            return base.ConvertParameterValue(value);
        }

        protected override void ConfigureUpsertCommand(IDbCommand command, CommandContext<T, TKey> context)
        {
            if (context.Entity == null)
                throw new ArgumentException("Entity is required for upsert operation");

            // SQLite specific: INSERT OR REPLACE
            var columns = this.GetInsertColumns();
            
            if (this.EnableSoftDelete)
            {
                // For soft delete, we need to handle versioning differently
                throw new NotSupportedException("Upsert with soft delete is not yet implemented for SQLite");
            }
            
            var columnList = string.Join(", ", columns.Select(this.EscapeIdentifier));
            var paramList = string.Join(", ", columns.Select(c => this.GetParameterPrefix() + c));
            
            command.CommandText = $"INSERT OR REPLACE INTO {this.GetFullTableName()} ({columnList}) VALUES ({paramList})";
            this.AddEntityParameters(command, context.Entity);
        }

        #endregion

        #region table definition
        public override string GetSqlTypeString(PropertyMapping mapping)
        {
            var sqliteDbType = (mapping.SqlType ?? SqlDbType.Text).ToSQLiteDbType();
            switch (sqliteDbType)
            {
                case SQLiteDbType.Integer:
                    return "INTEGER";
                case SQLiteDbType.Real:
                    return "REAL";
                case SQLiteDbType.Text:
                    return "TEXT";
                case SQLiteDbType.Blob:
                    return "BLOB";
                case SQLiteDbType.Numeric:
                    return "NUMERIC";
                default:
                    return "TEXT";
            }
        }

        /// <summary>
        /// When autoincrement is enabled, table can only have one primary key, and
        /// can no longer use PRIMARY KEY (Id) as separate definition, PK must be defined
        /// as Id INTEGER PRIMARY KEY AUTOINCREMENT, without NOT NULL, UNIQUE or other constraints.
        /// </summary>
        /// <param name="mapping"></param>
        /// <returns></returns>
        protected override string GenerateColumnDefinition(PropertyMapping mapping)
        {
            if (!mapping.IsPrimaryKey || this.HasCompositeKey)
            {
                return base.GenerateColumnDefinition(mapping);
            }

            var sql = new StringBuilder();
            sql.Append($"{this.EscapeIdentifier(mapping.ColumnName)} ");

            // Data type
            sql.Append(this.GetSqlTypeString(mapping));

            // auto-increment
            if (mapping.IsAutoIncrement)
            {
                sql.Append(" PRIMARY KEY AUTOINCREMENT");
            }

            return sql.ToString();
        }

        protected override string GeneratePrimaryKeyDefinition()
        {
            if (this.PropertyMappings.Values.Any(m => m.IsAutoIncrement))
            {
                return string.Empty;
            }

            return base.GeneratePrimaryKeyDefinition();
        }

        #endregion 
    }
}