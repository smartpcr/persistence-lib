//-------------------------------------------------------------------------------
// <copyright file="SQLitePersistenceProvider.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite
{
    using System;
    using System.Data.SQLite;
    using System.Threading;
    using System.Threading.Tasks;
    using Config;
    using Contracts;
    using Entities;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Resilience;

    /// <summary>
    /// Non-generic static class to hold shared state across all SQLitePersistenceProvider instances.
    /// This ensures the audit table is only created once regardless of how many different
    /// generic type instances are created.
    /// </summary>
    internal static class SQLiteProviderSharedState
    {
        public static readonly TimeSpan DefaultCacheExpiration = TimeSpan.FromHours(1);

        internal static readonly object VersionTableLock = new object();
        internal static bool VersionTableCreated;

        internal static readonly object AuditTableLock = new object();
        internal static bool AuditTableCreated;

        internal static readonly object EntryListMappingLock = new object();
        internal static bool EntryListMappingCreated;

        public static void ClearState()
        {
            VersionTableCreated = false;
            AuditTableCreated = false;
            EntryListMappingCreated = false;
        }
    }

    /// <summary>
    /// SQLite implementation of IPersistenceProvider that translates CRUD operations to SQL.
    /// </summary>
    public partial class SQLitePersistenceProvider<T, TKey> : IPersistenceProvider<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {

        private readonly string connectionString;
        private readonly VersionMapper versionMapper;
        private readonly EntryListMappingMapper entryListMappingMapper;
        private readonly SqliteConfiguration configuration;
        private readonly IEntityMapper<AuditRecord, long> auditMapper;
        private readonly RetryPolicy retryPolicy;
        private bool isInitialized;
        private bool isDisposed;

        public IEntityMapper<T, TKey> Mapper { get; private set; }

        /// <summary>
        /// Gets the properly escaped table name for use in SQL statements.
        /// </summary>
        private string EscapedTableName => this.Mapper.GetFullTableName();

        public SQLitePersistenceProvider(string connectionString, SqliteConfiguration configuration = null)
        {
            this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            this.configuration = configuration ?? new SqliteConfiguration();
            this.retryPolicy = RetryPolicy.FromConfiguration(this.configuration.RetryPolicy);

            this.Mapper = new SQLiteEntityMapper<T, TKey>(this.retryPolicy);
            this.versionMapper = new VersionMapper(this.retryPolicy);
            this.entryListMappingMapper = new EntryListMappingMapper(this.retryPolicy);
            this.auditMapper = new SQLiteAuditMapper(this.retryPolicy);
            this.isInitialized = false;
        }

        /// <summary>
        /// Creates a new SQLitePersistenceProvider with configuration loaded from a JSON file.
        /// </summary>
        /// <param name="connectionString">The SQLite connection string</param>
        /// <param name="configFilePath">Path to JSON config file. If null, looks for 'sqlite.json' in current directory.</param>
        /// <returns>A new SQLitePersistenceProvider instance</returns>
        public static SQLitePersistenceProvider<T, TKey> CreateWithJsonConfig(string connectionString, string configFilePath = null)
        {
            var configuration = SqliteConfiguration.FromJsonFile(configFilePath);
            return new SQLitePersistenceProvider<T, TKey>(connectionString, configuration);
        }

        #region init

        public async Task InitializeAsync(CancellationToken cancel = default)
        {
            if (this.isInitialized)
            {
                return;
            }

            // Create database file if it doesn't exist
            this.EnsureDatabaseExistsAsync(cancel);

            // Initialize database with configuration and create schema
            await this.InitializeDatabaseAsync(cancel);

            this.isInitialized = true;
        }

        #endregion

        public ITransactionScope BeginTransaction(CancellationToken cancellationToken = default)
        {
            return new TransactionScope(this.connectionString);
        }

        public ValueTask DisposeAsync()
        {
            if (!this.isDisposed)
            {
                // Async cleanup
                // Clear the connection pool for this specific database to release file locks
                // This is especially important for unit tests to allow database file deletion
                try
                {
                    using var connection = new SQLiteConnection(this.connectionString);
                    SQLiteConnection.ClearPool(connection);
                }
                catch
                {
                    // Ignore any errors during disposal
                }

                // Suppress finalizer
                GC.SuppressFinalize(this);

                this.isDisposed = true;
            }

            return new ValueTask(Task.CompletedTask);
        }

        #region size estimation

        /// <summary>
        /// Estimates the size of an entity by serializing it.
        /// </summary>
        private long EstimateEntitySize(T entity)
        {
            if (entity == null) return 0;

            try
            {
                var serialized = this.Mapper.SerializeEntity(entity);
                return serialized?.Length ?? 0;
            }
            catch
            {
                // If serialization fails, return 0
                return 0;
            }
        }

        #endregion

        #region Helper Methods
        //
        // /// <summary>
        // /// Creates a SQLite command with retry policy if enabled.
        // /// This is the primary method for creating commands throughout the provider.
        // /// </summary>
        // internal SQLiteCommand CreateCommand(string commandText, SQLiteConnection connection, SQLiteTransaction transaction = null)
        // {
        //     var command = transaction != null
        //         ? new SQLiteCommand(commandText, connection, transaction)
        //         : new SQLiteCommand(commandText, connection);
        //
        //     command.CommandTimeout = this.configuration.CommandTimeout;
        //
        //     // If retry policy is enabled, wrap the command
        //     // Note: For backward compatibility and for operations that handle their own retry logic,
        //     // we return the base SQLiteCommand type but it may be wrapped with retry logic internally
        //     return command;
        // }

        /// <summary>
        /// Creates a resilient SQLite command with retry policy if enabled.
        /// Returns a ResilientSQLiteCommand wrapper that provides retry logic.
        /// </summary>
        internal ResilientSQLiteCommand CreateCommand(string commandText, SQLiteConnection connection, SQLiteTransaction transaction = null)
        {
            var command = transaction != null
                ? new SQLiteCommand(commandText, connection, transaction)
                : new SQLiteCommand(commandText, connection);

            command.CommandTimeout = this.configuration.CommandTimeout;

            return new ResilientSQLiteCommand(command, this.retryPolicy);
        }

        #endregion

    }
}