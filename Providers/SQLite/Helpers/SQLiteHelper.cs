// -----------------------------------------------------------------------
// <copyright file="SQLiteHelper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SQLite;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Helper class for SQLite database inspection and management.
    /// </summary>
    public class SQLiteHelper : IAsyncDisposable
    {
        private readonly string connectionString;
        private readonly SemaphoreSlim connectionSemaphore = new SemaphoreSlim(1, 1);
        private SQLiteConnection connection;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SQLiteHelper"/> class.
        /// </summary>
        /// <param name="connectionString">The SQLite connection string.</param>
        public SQLiteHelper(string connectionString)
        {
            this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        #region Public Methods

        /// <summary>
        /// Gets comprehensive database information including stats, tables, indexes, and constraints.
        /// </summary>
        /// <returns>Database information.</returns>
        public async Task<DatabaseInfo> GetDatabaseInfoAsync()
        {
            var dbInfo = new DatabaseInfo();
            var connection = await this.GetConnectionAsync();

            // Get file path from connection string
            var builder = new SQLiteConnectionStringBuilder(this.connectionString);
            dbInfo.FilePath = builder.DataSource;

            // Get database statistics
            dbInfo.Stats = await this.GetDatabaseStatsAsync(dbInfo.FilePath);

            // Get SQLite version and pragma settings
            dbInfo.SqliteVersion = await this.GetScalarStringAsync(connection, "SELECT sqlite_version()");
            dbInfo.JournalMode = await this.GetScalarStringAsync(connection, "PRAGMA journal_mode");
            dbInfo.PageSize = await this.GetScalarIntAsync(connection, "PRAGMA page_size");
            dbInfo.CacheSize = await this.GetScalarIntAsync(connection, "PRAGMA cache_size");
            dbInfo.ForeignKeysEnabled = await this.GetScalarIntAsync(connection, "PRAGMA foreign_keys") == 1;

            // Get tables
            dbInfo.Tables = await this.GetTablesAsync();

            // Get all indexes
            dbInfo.Indexes = await this.GetAllIndexesAsync();

            // Get all foreign keys
            dbInfo.ForeignKeys = await this.GetAllForeignKeysAsync();

            return dbInfo;
        }

        /// <summary>
        /// Gets database statistics.
        /// </summary>
        public async Task<DatabaseStats> GetDatabaseStatsAsync()
        {
            var builder = new SQLiteConnectionStringBuilder(this.connectionString);
            return await this.GetDatabaseStatsAsync(builder.DataSource);
        }

        /// <summary>
        /// Gets all tables in the database.
        /// </summary>
        public async Task<List<TableInfo>> GetTablesAsync()
        {
            var tables = new List<TableInfo>();
            var connection = await this.GetConnectionAsync();

            var sql = @"
                SELECT name, sql, rootpage, type
                FROM sqlite_master
                WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
                ORDER BY name";

            using (var cmd = new SQLiteCommand(sql, connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var tableName = reader.GetString(0);
                    var table = new TableInfo
                    {
                        TableName = tableName,
                        CreateSql = reader.IsDBNull(1) ? null : reader.GetString(1),
                        RootPage = reader.GetInt32(2)
                    };

                    // Check for WITHOUT ROWID and STRICT
                    if (!string.IsNullOrEmpty(table.CreateSql))
                    {
                        table.IsWithoutRowId = table.CreateSql.Contains("WITHOUT ROWID", StringComparison.OrdinalIgnoreCase);
                        table.IsStrict = table.CreateSql.Contains("STRICT", StringComparison.OrdinalIgnoreCase);
                    }

                    // Get columns
                    table.Columns = await this.GetTableColumnsAsync(tableName);
                    table.HasPrimaryKey = table.Columns.Any(c => c.IsPrimaryKey);

                    // Get row count
                    table.RowCount = await this.GetScalarLongAsync(connection, $"SELECT COUNT(*) FROM [{tableName}]");

                    // Get table-specific indexes
                    table.Indexes = await this.GetTableIndexesAsync(tableName);

                    // Get table-specific foreign keys
                    table.ForeignKeys = await this.GetTableForeignKeysAsync(tableName);

                    tables.Add(table);
                }
            }

            return tables;
        }

        /// <summary>
        /// Gets columns for a specific table.
        /// </summary>
        public async Task<List<ColumnInfo>> GetTableColumnsAsync(string tableName)
        {
            var columns = new List<ColumnInfo>();
            var connection = await this.GetConnectionAsync();

            using (var cmd = new SQLiteCommand($"PRAGMA table_info([{tableName}])", connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var column = new ColumnInfo
                    {
                        ColumnId = reader.GetInt32(0),
                        ColumnName = reader.GetString(1),
                        DataType = reader.IsDBNull(2) ? null : reader.GetString(2),
                        IsNullable = reader.GetInt32(3) == 0,
                        DefaultValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                        IsPrimaryKey = reader.GetInt32(5) == 1
                    };

                    // Check for autoincrement
                    if (column.IsPrimaryKey && column.DataType?.ToUpper().Contains("INT") == true)
                    {
                        column.IsAutoIncrement = await this.IsColumnAutoIncrementAsync(tableName, column.ColumnName);
                    }

                    columns.Add(column);
                }
            }

            // Get extended column info (hidden, generated columns)
            using (var cmd = new SQLiteCommand($"PRAGMA table_xinfo([{tableName}])", connection))
            {
                try
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var columnName = reader.GetString(1);
                            var column = columns.FirstOrDefault(c => c.ColumnName == columnName);
                            if (column != null && reader.FieldCount > 6)
                            {
                                column.IsHidden = reader.GetInt32(6) == 1;
                                if (reader.FieldCount > 7)
                                {
                                    column.IsGenerated = reader.GetInt32(7) == 2 || reader.GetInt32(7) == 3;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // table_xinfo might not be available in older SQLite versions
                }
            }

            return columns;
        }

        /// <summary>
        /// Gets all indexes in the database.
        /// </summary>
        public async Task<List<IndexInfo>> GetAllIndexesAsync()
        {
            var indexes = new List<IndexInfo>();
            var connection = await this.GetConnectionAsync();

            var sql = @"
                SELECT name, tbl_name, sql, rootpage
                FROM sqlite_master
                WHERE type = 'index' AND name NOT LIKE 'sqlite_%'
                ORDER BY tbl_name, name";

            using (var cmd = new SQLiteCommand(sql, connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var indexName = reader.GetString(0);
                    var index = new IndexInfo
                    {
                        IndexName = indexName,
                        TableName = reader.GetString(1),
                        CreateSql = reader.IsDBNull(2) ? null : reader.GetString(2),
                        RootPage = reader.GetInt32(3),
                        IsAutoIndex = reader.IsDBNull(2)
                    };

                    // Parse CREATE INDEX statement for details
                    if (!string.IsNullOrEmpty(index.CreateSql))
                    {
                        index.IsUnique = index.CreateSql.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
                        index.IsPartial = index.CreateSql.Contains("WHERE", StringComparison.OrdinalIgnoreCase);

                        // Extract WHERE clause for partial indexes
                        if (index.IsPartial)
                        {
                            var whereIndex = index.CreateSql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
                            if (whereIndex > 0)
                            {
                                index.WhereClause = index.CreateSql.Substring(whereIndex + 5).Trim();
                            }
                        }
                    }

                    // Get index columns
                    index.Columns = await this.GetIndexColumnsAsync(indexName);

                    indexes.Add(index);
                }
            }

            return indexes;
        }

        /// <summary>
        /// Gets indexes for a specific table.
        /// </summary>
        public async Task<List<IndexInfo>> GetTableIndexesAsync(string tableName)
        {
            var indexes = new List<IndexInfo>();
            var connection = await this.GetConnectionAsync();

            using (var cmd = new SQLiteCommand($"PRAGMA index_list([{tableName}])", connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var indexName = reader.GetString(1);
                    var index = new IndexInfo
                    {
                        IndexName = indexName,
                        TableName = tableName,
                        IsUnique = reader.GetInt32(2) == 1,
                        IsPartial = reader.GetInt32(4) == 1
                    };

                    // Get index columns
                    index.Columns = await this.GetIndexColumnsAsync(indexName);

                    // Get CREATE SQL
                    var sqlQuery = "SELECT sql FROM sqlite_master WHERE type = 'index' AND name = @name";
                    using (var sqlCmd = new SQLiteCommand(sqlQuery, connection))
                    {
                        sqlCmd.Parameters.AddWithValue("@name", indexName);
                        index.CreateSql = await sqlCmd.ExecuteScalarAsync() as string;
                    }

                    indexes.Add(index);
                }
            }

            return indexes;
        }

        /// <summary>
        /// Gets columns for a specific index.
        /// </summary>
        public async Task<List<IndexColumn>> GetIndexColumnsAsync(string indexName)
        {
            var columns = new List<IndexColumn>();
            var connection = await this.GetConnectionAsync();

            using (var cmd = new SQLiteCommand($"PRAGMA index_xinfo([{indexName}])", connection))
            {
                try
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var column = new IndexColumn
                            {
                                SequenceNumber = reader.GetInt32(0),
                                ColumnId = reader.GetInt32(1),
                                ColumnName = reader.IsDBNull(2) ? null : reader.GetString(2),
                                IsDescending = reader.GetInt32(3) == 1,
                                Collation = reader.GetString(4),
                                IsKey = reader.GetInt32(5) == 1
                            };
                            columns.Add(column);
                        }
                    }
                }
                catch
                {
                    // Fallback to index_info if index_xinfo is not available
                    using (var fallbackCmd = new SQLiteCommand($"PRAGMA index_info([{indexName}])", connection))
                    using (var reader = await fallbackCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var column = new IndexColumn
                            {
                                SequenceNumber = reader.GetInt32(0),
                                ColumnId = reader.GetInt32(1),
                                ColumnName = reader.GetString(2),
                                IsKey = true
                            };
                            columns.Add(column);
                        }
                    }
                }
            }

            return columns;
        }

        /// <summary>
        /// Gets all foreign keys in the database.
        /// </summary>
        public async Task<List<ForeignKeyInfo>> GetAllForeignKeysAsync()
        {
            var foreignKeys = new List<ForeignKeyInfo>();

            // Get all tables
            var tables = await this.GetTableNamesAsync();

            foreach (var tableName in tables)
            {
                var tableForeignKeys = await this.GetTableForeignKeysAsync(tableName);
                foreignKeys.AddRange(tableForeignKeys);
            }

            return foreignKeys;
        }

        /// <summary>
        /// Gets foreign keys for a specific table.
        /// </summary>
        public async Task<List<ForeignKeyInfo>> GetTableForeignKeysAsync(string tableName)
        {
            var foreignKeys = new List<ForeignKeyInfo>();
            var connection = await this.GetConnectionAsync();

            await using var cmd = new SQLiteCommand($"PRAGMA foreign_key_list([{tableName}])", connection);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var foreignKey = new ForeignKeyInfo
                {
                    Id = reader.GetInt32(0),
                    Sequence = reader.GetInt32(1),
                    ToTable = reader.GetString(2),
                    FromColumn = reader.GetString(3),
                    ToColumn = reader.GetString(4),
                    OnUpdate = reader.GetString(5),
                    OnDelete = reader.GetString(6),
                    Match = reader.GetString(7),
                    FromTable = tableName
                };
                foreignKeys.Add(foreignKey);
            }

            return foreignKeys;
        }

        /// <summary>
        /// Gets check constraints for a table.
        /// </summary>
        public async Task<List<CheckConstraintInfo>> GetTableCheckConstraintsAsync(string tableName)
        {
            var constraints = new List<CheckConstraintInfo>();
            var connection = await this.GetConnectionAsync();

            // Check constraints are embedded in the CREATE TABLE statement
            var sql = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = @name";
            using (var cmd = new SQLiteCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@name", tableName);
                var createSql = await cmd.ExecuteScalarAsync() as string;

                if (!string.IsNullOrEmpty(createSql))
                {
                    // Parse CHECK constraints from CREATE TABLE statement
                    // This is a simplified parser - a full implementation would need more robust SQL parsing
                    var checkIndex = 0;
                    while ((checkIndex = createSql.IndexOf("CHECK", checkIndex, StringComparison.OrdinalIgnoreCase)) != -1)
                    {
                        var startParen = createSql.IndexOf('(', checkIndex);
                        if (startParen != -1)
                        {
                            var endParen = this.FindMatchingParenthesis(createSql, startParen);
                            if (endParen != -1)
                            {
                                var checkExpression = createSql.Substring(startParen + 1, endParen - startParen - 1);
                                constraints.Add(new CheckConstraintInfo
                                {
                                    TableName = tableName,
                                    CheckExpression = checkExpression
                                });
                            }
                        }
                        checkIndex++;
                    }
                }
            }

            return constraints;
        }

        /// <summary>
        /// Gets triggers for a table.
        /// </summary>
        public async Task<List<TriggerInfo>> GetTableTriggersAsync(string tableName)
        {
            var triggers = new List<TriggerInfo>();
            var connection = await this.GetConnectionAsync();

            var sql = @"
                SELECT name, sql
                FROM sqlite_master
                WHERE type = 'trigger' AND tbl_name = @tableName
                ORDER BY name";

            using (var cmd = new SQLiteCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@tableName", tableName);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var trigger = new TriggerInfo
                        {
                            TriggerName = reader.GetString(0),
                            TableName = tableName,
                            CreateSql = reader.GetString(1)
                        };

                        // Parse trigger details from CREATE TRIGGER statement
                        if (!string.IsNullOrEmpty(trigger.CreateSql))
                        {
                            var upperSql = trigger.CreateSql.ToUpper();

                            // Determine trigger timing
                            if (upperSql.Contains("BEFORE"))
                                trigger.TriggerTiming = "BEFORE";
                            else if (upperSql.Contains("AFTER"))
                                trigger.TriggerTiming = "AFTER";
                            else if (upperSql.Contains("INSTEAD OF"))
                                trigger.TriggerTiming = "INSTEAD OF";

                            // Determine trigger event
                            if (upperSql.Contains("INSERT"))
                                trigger.TriggerEvent = "INSERT";
                            else if (upperSql.Contains("UPDATE"))
                                trigger.TriggerEvent = "UPDATE";
                            else if (upperSql.Contains("DELETE"))
                                trigger.TriggerEvent = "DELETE";
                        }

                        triggers.Add(trigger);
                    }
                }
            }

            return triggers;
        }

        /// <summary>
        /// Gets all views in the database.
        /// </summary>
        public async Task<List<ViewInfo>> GetViewsAsync()
        {
            var views = new List<ViewInfo>();
            var connection = await this.GetConnectionAsync();

            var sql = @"
                SELECT name, sql, rootpage
                FROM sqlite_master
                WHERE type = 'view'
                ORDER BY name";

            using (var cmd = new SQLiteCommand(sql, connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var view = new ViewInfo
                    {
                        ViewName = reader.GetString(0),
                        CreateSql = reader.GetString(1),
                        RootPage = reader.GetInt32(2)
                    };
                    views.Add(view);
                }
            }

            return views;
        }

        /// <summary>
        /// Generates a formatted report of the database structure.
        /// </summary>
        public string GenerateDatabaseReport(DatabaseInfo dbInfo)
        {
            var sb = new StringBuilder();

            sb.AppendLine("=" . PadRight(80, '='));
            sb.AppendLine($"DATABASE REPORT: {Path.GetFileName(dbInfo.FilePath)}");
            sb.AppendLine("=" . PadRight(80, '='));
            sb.AppendLine();

            // Database Settings
            sb.AppendLine("DATABASE SETTINGS:");
            sb.AppendLine("-" . PadRight(40, '-'));
            sb.AppendLine($"SQLite Version:     {dbInfo.SqliteVersion}");
            sb.AppendLine($"File Size:          {dbInfo.Stats.FormattedFileSize}");
            sb.AppendLine($"Page Size:          {dbInfo.PageSize} bytes");
            sb.AppendLine($"Page Count:         {dbInfo.Stats.PageCount:N0}");
            sb.AppendLine($"Free Pages:         {dbInfo.Stats.FreePageCount:N0}");
            sb.AppendLine($"Cache Size:         {dbInfo.CacheSize}");
            sb.AppendLine($"Journal Mode:       {dbInfo.JournalMode}");
            sb.AppendLine($"Encoding:           {dbInfo.Stats.Encoding}");
            sb.AppendLine($"Auto Vacuum:        {dbInfo.Stats.AutoVacuum}");
            sb.AppendLine($"Foreign Keys:       {(dbInfo.ForeignKeysEnabled ? "Enabled" : "Disabled")}");
            sb.AppendLine($"Last Modified:      {dbInfo.Stats.LastModified:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // Object Summary
            sb.AppendLine("OBJECT SUMMARY:");
            sb.AppendLine("-" . PadRight(40, '-'));
            sb.AppendLine($"Tables:             {dbInfo.Stats.TableCount}");
            sb.AppendLine($"Indexes:            {dbInfo.Stats.IndexCount}");
            sb.AppendLine($"Triggers:           {dbInfo.Stats.TriggerCount}");
            sb.AppendLine($"Views:              {dbInfo.Stats.ViewCount}");
            sb.AppendLine();

            // Tables
            sb.AppendLine("TABLES:");
            sb.AppendLine("-" . PadRight(80, '-'));
            foreach (var table in dbInfo.Tables.OrderBy(t => t.TableName))
            {
                sb.AppendLine($"\n[{table.TableName}] - {table.RowCount:N0} rows");

                if (table.IsWithoutRowId)
                    sb.AppendLine("  * WITHOUT ROWID table");
                if (table.IsStrict)
                    sb.AppendLine("  * STRICT table");

                sb.AppendLine("  Columns:");
                foreach (var col in table.Columns)
                {
                    var flags = new List<string>();
                    if (col.IsPrimaryKey) flags.Add("PK");
                    if (col.IsAutoIncrement) flags.Add("AUTO");
                    if (col.IsUnique) flags.Add("UNIQUE");
                    if (!col.IsNullable) flags.Add("NOT NULL");
                    if (col.IsGenerated) flags.Add("GENERATED");
                    if (col.IsHidden) flags.Add("HIDDEN");

                    var flagStr = flags.Count > 0 ? $" [{string.Join(", ", flags)}]" : "";
                    var defaultStr = !string.IsNullOrEmpty(col.DefaultValue) ? $" DEFAULT {col.DefaultValue}" : "";

                    sb.AppendLine($"    - {col.ColumnName}: {col.DataType}{flagStr}{defaultStr}");
                }

                if (table.Indexes.Count > 0)
                {
                    sb.AppendLine("  Indexes:");
                    foreach (var idx in table.Indexes)
                    {
                        var uniqueStr = idx.IsUnique ? "UNIQUE " : "";
                        var partialStr = idx.IsPartial ? " (PARTIAL)" : "";
                        var columns = string.Join(", ", idx.Columns.Where(c => c.IsKey).Select(c => c.ColumnName));
                        sb.AppendLine($"    - {idx.IndexName}: {uniqueStr}({columns}){partialStr}");
                    }
                }

                if (table.ForeignKeys.Count > 0)
                {
                    sb.AppendLine("  Foreign Keys:");
                    foreach (var fk in table.ForeignKeys)
                    {
                        sb.AppendLine($"    - {fk.FromColumn} -> {fk.ToTable}({fk.ToColumn}) " +
                                    $"ON DELETE {fk.OnDelete} ON UPDATE {fk.OnUpdate}");
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Disposes the SQLite connection asynchronously.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (this.disposed)
            {
                return;
            }

            await this.connectionSemaphore.WaitAsync();
            try
            {
                if (this.connection != null)
                {
                    if (this.connection.State == ConnectionState.Open)
                    {
                        await this.connection.CloseAsync();
                    }
                    await this.connection.DisposeAsync();
                    this.connection = null;
                }

                this.disposed = true;
            }
            finally
            {
                this.connectionSemaphore.Release();
                this.connectionSemaphore?.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Ensures the connection is open and returns it.
        /// </summary>
        private async Task<SQLiteConnection> GetConnectionAsync()
        {
            await this.connectionSemaphore.WaitAsync();
            try
            {
                if (this.disposed)
                {
                    throw new ObjectDisposedException(nameof(SQLiteHelper));
                }

                if (this.connection == null)
                {
                    this.connection = new SQLiteConnection(this.connectionString);
                    await this.connection.OpenAsync();
                }
                else if (this.connection.State != ConnectionState.Open)
                {
                    await this.connection.OpenAsync();
                }

                return this.connection;
            }
            finally
            {
                this.connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets database statistics.
        /// </summary>
        private async Task<DatabaseStats> GetDatabaseStatsAsync(string filePath)
        {
            var stats = new DatabaseStats();

            // Get file info
            if (File.Exists(filePath))
            {
                var fileInfo = new FileInfo(filePath);
                stats.FileSizeBytes = fileInfo.Length;
                stats.FormattedFileSize = this.FormatFileSize(fileInfo.Length);
                stats.LastModified = fileInfo.LastWriteTime;
            }

            var connection = await this.GetConnectionAsync();

            // Get page statistics
            stats.PageCount = await this.GetScalarIntAsync(connection, "PRAGMA page_count");
            stats.FreePageCount = await this.GetScalarIntAsync(connection, "PRAGMA freelist_count");

            // Get encoding and other settings
            stats.Encoding = await this.GetScalarStringAsync(connection, "PRAGMA encoding");
            stats.AutoVacuum = await this.GetAutoVacuumModeAsync();
            stats.UserVersion = await this.GetScalarIntAsync(connection, "PRAGMA user_version");
            stats.ApplicationId = await this.GetScalarIntAsync(connection, "PRAGMA application_id");

            // Count database objects
            stats.TableCount = await this.GetScalarIntAsync(connection,
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'");
            stats.IndexCount = await this.GetScalarIntAsync(connection,
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name NOT LIKE 'sqlite_%'");
            stats.TriggerCount = await this.GetScalarIntAsync(connection,
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'trigger'");
            stats.ViewCount = await this.GetScalarIntAsync(connection,
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'view'");

            return stats;
        }

        private async Task<string> GetScalarStringAsync(SQLiteConnection connection, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, connection))
            {
                var result = await cmd.ExecuteScalarAsync();
                return result?.ToString();
            }
        }

        private async Task<int> GetScalarIntAsync(SQLiteConnection connection, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, connection))
            {
                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result ?? 0);
            }
        }

        private async Task<long> GetScalarLongAsync(SQLiteConnection connection, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, connection))
            {
                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt64(result ?? 0);
            }
        }

        private async Task<List<string>> GetTableNamesAsync()
        {
            var tables = new List<string>();
            var connection = await this.GetConnectionAsync();
            var sql = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";

            using (var cmd = new SQLiteCommand(sql, connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    tables.Add(reader.GetString(0));
                }
            }

            return tables;
        }

        private async Task<bool> IsColumnAutoIncrementAsync(string tableName, string columnName)
        {
            // Check if the table uses AUTOINCREMENT
            var connection = await this.GetConnectionAsync();
            var sql = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = @tableName";
            using (var cmd = new SQLiteCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@tableName", tableName);
                var createSql = await cmd.ExecuteScalarAsync() as string;
                if (!string.IsNullOrEmpty(createSql))
                {
                    return createSql.Contains($"{columnName}") &&
                           createSql.Contains("AUTOINCREMENT", StringComparison.OrdinalIgnoreCase);
                }
            }
            return false;
        }

        private async Task<string> GetAutoVacuumModeAsync()
        {
            var connection = await this.GetConnectionAsync();
            var mode = await this.GetScalarIntAsync(connection, "PRAGMA auto_vacuum");
            return mode switch
            {
                0 => "NONE",
                1 => "FULL",
                2 => "INCREMENTAL",
                _ => $"UNKNOWN ({mode})"
            };
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private int FindMatchingParenthesis(string sql, int startIndex)
        {
            int depth = 1;
            for (int i = startIndex + 1; i < sql.Length; i++)
            {
                if (sql[i] == '(')
                    depth++;
                else if (sql[i] == ')')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }
            return -1;
        }

        #endregion
    }
}