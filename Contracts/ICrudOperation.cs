// -----------------------------------------------------------------------
// <copyright file="ICrudOperation.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the fundamental CRUD (Create, Read, Update, Delete) operations for entity persistence.
    /// This interface provides the core data manipulation operations that form the foundation
    /// of the persistence layer.
    /// 
    /// Key Concepts:
    /// - Optimistic Concurrency: All update operations use version checking to prevent lost updates
    /// - Soft Delete Support: When enabled, delete operations preserve data by marking as deleted
    /// - Audit Trail: All operations can be tracked with CallerInfo for compliance and debugging
    /// - Idempotency: Operations are designed to be safely retryable
    /// 
    /// Concurrency Model:
    /// - Version field is automatically incremented on each update
    /// - ConcurrencyException thrown when version conflicts detected
    /// - No pessimistic locking to maintain scalability
    /// 
    /// Soft Delete Behavior:
    /// - When enabled, entities are never physically removed
    /// - Delete creates a new version with IsDeleted flag set
    /// - Historical versions are preserved for recovery
    /// - GetAsync returns null for soft-deleted entities by default
    /// 
    /// Error Handling:
    /// - EntityNotFoundException: Entity doesn't exist for update/delete
    /// - EntityAlreadyExistsException: Entity with same key already exists
    /// - ConcurrencyException: Version mismatch during update
    /// - EntityWriteException: General write operation failure
    /// 
    /// Performance Considerations:
    /// - Single entity operations are optimized for low latency
    /// - Use batch operations for multiple entities to reduce round trips
    /// - Indexes on primary key and version columns for fast lookups
    /// </summary>
    /// <typeparam name="T">The entity type that implements IEntity<TKey></typeparam>
    /// <typeparam name="TKey">The primary key type that implements IEquatable<TKey></typeparam>
    public interface ICrudOperation<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        #region CRUD Operations

        /// <summary>
        /// Creates a new entity in the persistence store.
        /// Implementation:
        /// 1. Opens connection and begins transaction for atomic operation
        /// 2. If soft-delete enabled, gets next version from Version table
        /// 3. Checks if entity with same key already exists (throws EntityAlreadyExistsException if found and not soft-deleted)
        /// 4. Sets tracking fields: CreatedTime, LastWriteTime = UtcNow, Version = 1 or next version
        /// 5. If expiry enabled, sets AbsoluteExpiration = CreatedTime + ExpirySpan
        /// 6. Executes parameterized INSERT SQL with all entity columns
        /// 7. Retrieves and returns the created entity to confirm insertion
        /// 8. Commits transaction on success, rolls back on failure
        /// 9. Writes audit record if audit trail is enabled (CREATE action)
        /// </summary>
        /// <param name="entity">The entity to create</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The created entity with updated tracking fields</returns>
        Task<T> CreateAsync(T entity, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves an entity by its primary key.
        /// Implementation:
        /// 1. Opens connection with applied PRAGMA settings
        /// 2. Executes parameterized SELECT with primary key filter
        /// 3. If soft-delete enabled, orders by Version DESC to get latest version
        /// 4. If expiry enabled, filters out expired entities (AbsoluteExpiration < UtcNow)
        /// 5. Checks IsDeleted flag for soft-deleted entities, returns null if deleted
        /// 6. Maps result from SQLiteDataReader using entity mapper
        /// 7. Writes audit record if audit trail is enabled (READ action)
        /// 8. Returns null if entity not found or is deleted/expired
        /// </summary>
        /// <param name="key">The primary key</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The entity if found; otherwise null</returns>
        Task<T> GetAsync(TKey key, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves entities by primary key with advanced filtering options.
        /// Implementation:
        /// 1. Opens connection and builds SELECT query based on flags
        /// 2. If includeAllVersions=true, returns all versions of the entity
        /// 3. If includeAllVersions=false, only returns latest version (ORDER BY Version DESC LIMIT 1)
        /// 4. If includeDeleted=false, filters out entities where IsDeleted=true
        /// 5. If includeExpired=false, filters out entities where AbsoluteExpiration < UtcNow
        /// 6. Maps all matching records from result set
        /// 7. Writes audit record for first result if audit trail is enabled
        /// 8. Returns empty collection if no matching entities found
        /// </summary>
        /// <param name="key">The primary key</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="includeExpired">When expiration is enabled, a flag indicating if expired entities should be included in results.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="includeAllVersions">When soft-delete is enabled, a flag indicating if to include entities with previous version as well.</param>
        /// <param name="includeDeleted">When soft-delete is enabled, a flag indicating if entities marked as deleted to be included in retrieval.</param>
        /// <returns>Collection of entities matching the criteria</returns>
        Task<IEnumerable<T>> GetByKeyAsync(
            TKey key,
            CallerInfo callerInfo,
            bool includeAllVersions = false,
            bool includeDeleted = false,
            bool includeExpired = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing entity with optimistic concurrency control.
        /// Implementation:
        /// 1. Opens connection and begins transaction for atomic operation
        /// 2. Retrieves current entity to verify existence and version match
        /// 3. Throws EntityNotFoundException if entity doesn't exist
        /// 4. Throws ConcurrencyConflictException if versions don't match (optimistic locking)
        /// 5. If soft-delete enabled, gets next version from Version table and INSERTs new version
        /// 6. If soft-delete disabled, performs in-place UPDATE with Version = Version + 1
        /// 7. Updates LastWriteTime = UtcNow
        /// 8. Executes UPDATE/INSERT with WHERE clause checking original version
        /// 9. Verifies rows affected > 0, throws EntityWriteException if concurrent modification
        /// 10. Commits transaction on success, rolls back on failure
        /// 11. Writes audit record if audit trail is enabled (UPDATE action with old value)
        /// </summary>
        /// <param name="entity">The entity to update</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The updated entity with incremented version</returns>
        /// <exception cref="ConcurrencyException">Thrown when version conflict detected</exception>
        Task<T> UpdateAsync(T entity, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an entity by its primary key (soft delete by default).
        /// Implementation:
        /// 1. Opens connection and begins transaction
        /// 2. Retrieves entity to verify it exists and get current state
        /// 3. If soft-delete enabled:
        ///    - Returns true if already soft-deleted (IsDeleted=true)
        ///    - Gets next version from Version table
        ///    - INSERTs new version with IsDeleted=true, updated LastWriteTime
        ///    - Preserves all entity data for recovery
        /// 4. If soft-delete disabled (hard delete):
        ///    - Executes DELETE SQL to permanently remove entity
        ///    - Returns true if entity not found (idempotent)
        /// 5. Commits transaction on success
        /// 6. Writes audit record if audit trail is enabled (DELETE action)
        /// 7. Returns true if successfully deleted or already deleted
        /// </summary>
        /// <param name="key">The primary key</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if deleted; false if not found</returns>
        Task<bool> DeleteAsync(TKey key, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        #endregion
    }
}
