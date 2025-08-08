//-------------------------------------------------------------------------------
// <copyright file="IPersistenceProvider.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    /// <summary>
    /// Defines the contract for persistence providers that handle entity storage and retrieval.
    /// This is the main interface that aggregates all persistence operations and provides a unified
    /// abstraction layer over different storage backends (SQLite, SQL Server, MongoDB, etc.).
    /// 
    /// Key Features:
    /// - CRUD Operations: Basic Create, Read, Update, Delete operations with optimistic concurrency
    /// - Batch Operations: Efficient bulk operations for multiple entities
    /// - List Operations: Specialized operations for managing entity collections with cache keys
    /// - Query Operations: Advanced querying with LINQ expressions, pagination, and filtering
    /// - Bulk Operations: High-performance import/export and data purging capabilities
    /// - Transaction Support: ACID transactions through ITransactionScope
    /// - Soft Delete: Optional versioning system that preserves entity history
    /// - Expiration: Time-based entity expiration with automatic cleanup
    /// - Audit Trail: Optional tracking of all entity modifications
    /// - Caching: Built-in caching support for improved read performance
    /// 
    /// Implementation Requirements:
    /// - Thread-safe: All operations must be thread-safe for concurrent access
    /// - Async: All operations are async-first for scalability
    /// - Idempotent: Delete operations should be idempotent
    /// - Atomic: Operations within transactions must be atomic
    /// - Consistent: Version tracking ensures consistency across operations
    /// 
    /// Type Constraints:
    /// - T must be a reference type implementing IEntity&lt;TKey&gt;
    /// - TKey must implement IEquatable for efficient key comparisons
    /// </summary>
    /// <typeparam name="T">The entity type that implements IEntity&lt;TKey&gt;</typeparam>
    /// <typeparam name="TKey">The primary key type that implements IEquatable&lt;TKey&gt;</typeparam>
    public interface IPersistenceProvider<T, TKey> :
        ICrudOperation<T, TKey>,
        IBatchOperation<T, TKey>,
        IListOperation<T, TKey>,
        IQueryOperation<T, TKey>,
        IBulkOperation<T, TKey>,
        IAsyncDisposable
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        #region props

        IEntityMapper<T, TKey> Mapper { get; }

        #endregion

        #region init
        /// <summary>
        /// Setup underlying persistence store.
        /// Implementation:
        /// 1. Creates database file if it doesn't exist (for file-based providers)
        /// 2. Opens connection and applies PRAGMA settings (page size, journal mode, cache size, etc.)
        /// 3. Creates main entity table with columns mapped from entity properties
        /// 4. Creates Version table if soft-delete is enabled (tracks version history)
        /// 5. Creates EntryListMapping table if SyncWithList is enabled (for list operations)
        /// 6. Creates AuditRecord table if audit trail is enabled (tracks all CRUD operations)
        /// 7. Creates indexes for optimized query performance
        /// 8. Sets initialization flag to prevent redundant initialization
        /// </summary>
        /// <param name="cancel">The cancel token.</param>
        /// <returns>The completion task.</returns>
        Task InitializeAsync(CancellationToken cancel = default);

        #endregion

        #region Transaction Operations

        /// <summary>
        /// Begins a new transaction scope.
        /// Implementation:
        /// 1. Creates a new database connection with the same connection string
        /// 2. Opens the connection and applies connection-specific PRAGMA settings
        /// 3. Begins a database transaction with isolation level
        /// 4. Returns a TransactionScope wrapper that manages the connection lifecycle
        /// 5. Operations within the scope share the same connection and transaction
        /// 6. Commit/Rollback is handled by the scope's Dispose method
        /// 7. Connection pool is cleared on disposal to release file locks
        /// </summary>
        /// <returns>Transaction scope for managing transactional operations</returns>
        ITransactionScope<T, TKey> BeginTransaction(CancellationToken cancellationToken = default);

        #endregion

    }
}