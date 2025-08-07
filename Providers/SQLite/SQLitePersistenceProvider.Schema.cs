// -----------------------------------------------------------------------
// <copyright file="SQLitePersistenceProvider.Schema.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite
{
    using System;
    using System.Collections.Generic;
    using System.Data.SQLite;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    public partial class SQLitePersistenceProvider<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        #region db config/table schema

        private void EnsureDatabaseExistsAsync(CancellationToken cancellationToken)
        {
            var builder = new SQLiteConnectionStringBuilder(this.connectionString);
            var databasePath = builder.DataSource;

            if (string.IsNullOrEmpty(databasePath) || databasePath == ":memory:")
            {
                // In-memory database, nothing to create
                return;
            }

            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(databasePath))
            {
                // Create empty database file
                SQLiteConnection.CreateFile(databasePath);
            }
        }

        private async Task InitializeDatabaseAsync(CancellationToken cancellationToken)
        {
            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync(cancellationToken);

            // Apply PRAGMA settings from configuration
            await this.ApplyPragmaSettingsAsync(connection, cancellationToken);

            // Create main entity table
            await this.CreateTableAsync(connection, this.Mapper, cancellationToken);

            // Create Version table if entity supports versioning
            if (this.Mapper.EnableSoftDelete || typeof(IVersionedEntity<TKey>).IsAssignableFrom(typeof(T)))
            {
                await this.CreateTableAsync(connection, this.versionMapper, cancellationToken);
            }

            // Create EntryListMapping table if entity syncs with list
            if (this.Mapper.SyncWithList)
            {
                await this.CreateTableAsync(connection, this.entryListMappingMapper, cancellationToken);
            }

            // Create audit table if audit trail is enabled (only create once)
            if (this.Mapper.EnableAuditTrail)
            {
                lock (SQLiteProviderSharedState.AuditTableLock)
                {
                    if (!SQLiteProviderSharedState.AuditTableCreated)
                    {
                        var createAuditTableSql = this.auditMapper.GenerateCreateTableSql();
                        using var createAuditCmd = this.CreateCommand(createAuditTableSql, connection);
                        createAuditCmd.ExecuteNonQuery(); // Use synchronous inside lock
                        SQLiteProviderSharedState.AuditTableCreated = true;
                    }
                }
            }

            // Create indexes
            await this.CreateIndexesAsync(connection, cancellationToken);
        }

        /// <summary>
        /// Applies both database-specific and connection-specific pragma settings.
        /// This should be called only during database initialization.
        /// </summary>
        private async Task ApplyPragmaSettingsAsync(SQLiteConnection connection, CancellationToken cancellationToken)
        {
            // Database-specific pragmas (persist across connections)
            var databasePragmas = new Dictionary<string, object>
            {
                { "page_size", this.configuration.PageSize },
                { "journal_mode", this.configuration.JournalMode.ToString().ToUpper() }
            };

            foreach (var pragma in databasePragmas)
            {
                using var command = this.CreateCommand($"PRAGMA {pragma.Key} = {pragma.Value}", connection);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            // Also apply connection-specific pragmas for this initial connection
            await this.ApplyConnectionPragmasAsync(connection, cancellationToken);
        }

        /// <summary>
        /// Applies connection-specific pragma settings.
        /// Must be called for every new connection.
        /// </summary>
        private async Task ApplyConnectionPragmasAsync(SQLiteConnection connection, CancellationToken cancellationToken)
        {
            // Connection-specific pragmas (must be set for each connection)
            var connectionPragmas = new Dictionary<string, object>
            {
                { "cache_size", this.configuration.CacheSize },
                { "synchronous", this.configuration.SynchronousMode.ToString().ToUpper() },
                { "busy_timeout", this.configuration.BusyTimeout },
                { "foreign_keys", this.configuration.EnableForeignKeys ? "ON" : "OFF" }
            };

            foreach (var pragma in connectionPragmas)
            {
                using var command = this.CreateCommand($"PRAGMA {pragma.Key} = {pragma.Value}", connection);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Creates a new SQLite connection and applies connection-specific pragmas.
        /// </summary>
        internal async Task<SQLiteConnection> CreateAndOpenConnectionAsync(CancellationToken cancellationToken)
        {
            var connection = new SQLiteConnection(this.connectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                await this.ApplyConnectionPragmasAsync(connection, cancellationToken);
                return connection;
            }
            catch
            {
                connection?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Creates a new SQLiteCommand with the configured command timeout.
        /// </summary>
        internal SQLiteCommand CreateCommand(string commandText, SQLiteConnection connection, SQLiteTransaction transaction = null)
        {
            var command = new SQLiteCommand(commandText, connection, transaction);
            command.CommandTimeout = this.configuration.CommandTimeout;
            return command;
        }

        /// <summary>
        /// This method creates the table for the specified entity type if it does not already exist.
        /// However, it does not guarantee the dependent table has been created,
        /// so if current table references another table, it must be created first.
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TEntityKey"></typeparam>
        /// <param name="connection"></param>
        /// <param name="mapper"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task CreateTableAsync<TEntity, TEntityKey>(SQLiteConnection connection, IEntityMapper<TEntity, TEntityKey> mapper, CancellationToken cancellationToken)
            where TEntity : class, IEntity<TEntityKey>
            where TEntityKey : IEquatable<TEntityKey>
        {
            if (mapper.SyncWithList)
            {
                var createEntityListMappingSql = this.entryListMappingMapper.GenerateCreateTableSql();
                using var createListMappingCmd = this.CreateCommand(createEntityListMappingSql, connection);
                await createListMappingCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            if (mapper.EnableSoftDelete)
            {
                var createVersionTableSql = this.versionMapper.GenerateCreateTableSql(includeIfNotExists: true);
                using var createVersionCmd = this.CreateCommand(createVersionTableSql, connection);
                await createVersionCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            var createTableSql = mapper.GenerateCreateTableSql(includeIfNotExists: true);
            using var command = this.CreateCommand(createTableSql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task CreateIndexesAsync(SQLiteConnection connection, CancellationToken cancellationToken)
        {
            var indexSqls = this.Mapper.GenerateCreateIndexSql();
            foreach (var indexSql in indexSqls)
            {
                using var command = this.CreateCommand(indexSql, connection);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        #endregion
    }
}
