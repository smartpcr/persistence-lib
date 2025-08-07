// -----------------------------------------------------------------------
// <copyright file="IBatchOperation.cs" company="Microsoft Corp.">
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
    /// Defines batch operations for efficient processing of multiple entities in a single operation.
    /// This interface extends basic CRUD with optimized bulk operations that minimize database
    /// round trips and improve throughput for large-scale data operations.
    /// 
    /// Key Features:
    /// - Batch Processing: Process multiple entities in configurable batch sizes
    /// - Transaction Support: Each batch executes within a single transaction
    /// - Partial Success: Some operations allow partial success with error tracking
    /// - Memory Efficiency: Streaming and pagination prevent memory exhaustion
    /// 
    /// Batch Size Optimization:
    /// - Default batch size varies by operation (typically 100-1000 entities)
    /// - Larger batches reduce overhead but increase memory usage
    /// - Smaller batches improve responsiveness and reduce lock contention
    /// - Automatic splitting when batch size exceeds database limits
    /// 
    /// Transaction Behavior:
    /// - Each batch runs in its own transaction for isolation
    /// - Batch failure rolls back only that batch, not entire operation
    /// - Atomic within batch: all entities in batch succeed or fail together
    /// - Cross-batch consistency not guaranteed without external transaction
    /// 
    /// Performance Characteristics:
    /// - Bulk inserts use prepared statements for speed
    /// - Updates may use CASE statements for single round trip
    /// - Deletes can use IN clauses for efficiency
    /// - GetAll streams results to avoid loading entire dataset
    /// 
    /// Filtering Options:
    /// - includeAllVersions: Returns historical versions when soft-delete enabled
    /// - includeDeleted: Returns soft-deleted entities
    /// - includeExpired: Returns expired entities when expiration enabled
    /// 
    /// Use Cases:
    /// - Data migration and synchronization
    /// - Bulk data imports from external systems
    /// - Periodic cleanup and maintenance operations
    /// - Report generation requiring full dataset access
    /// </summary>
    /// <typeparam name="T">The entity type that implements IEntity<TKey></typeparam>
    /// <typeparam name="TKey">The primary key type that implements IEquatable<TKey></typeparam>
    public interface IBatchOperation<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        #region Batch Operations

        /// <summary>
        /// Retrieves all entities in the persistence store.
        /// Implementation:
        /// 1. Opens connection and builds SELECT * query with optional filters
        /// 2. If soft-delete enabled and includeAllVersions=false, groups by key and takes latest version
        /// 3. If includeDeleted=false, adds WHERE IsDeleted = 0 filter
        /// 4. If includeExpired=false, adds WHERE AbsoluteExpiration > UtcNow filter
        /// 5. Executes query and maps all results using entity mapper
        /// 6. For performance, results are streamed rather than loaded all at once
        /// 7. Writes audit record for batch read if audit trail is enabled
        /// 8. Returns empty collection if no entities exist
        /// </summary>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="includeExpired">When expiration is enabled, a flag indicating if expired entities should be included in results.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="includeAllVersions">When soft-delete is enabled, a flag indicating if to include entities with previous version as well.</param>
        /// <param name="includeDeleted">When soft-delete is enabled, a flag indicating if entities marked as deleted to be included in retrieval.</param>
        /// <returns>All entities</returns>
        Task<IEnumerable<T>> GetAllAsync(
            CallerInfo callerInfo,
            bool includeAllVersions = false,
            bool includeDeleted = false,
            bool includeExpired = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates multiple entities in a single batch operation.
        /// Implementation:
        /// 1. Splits entities into batches based on batchSize parameter (default: all in one batch)
        /// 2. For each batch, opens a single transaction for atomicity
        /// 3. If soft-delete enabled, gets next version from Version table (same for all in batch)
        /// 4. For each entity in batch:
        ///    - Checks if entity exists (throws EntityAlreadyExistsException if not soft-deleted)
        ///    - Sets tracking fields: CreatedTime, LastWriteTime, Version
        ///    - Sets expiration if enabled
        ///    - Executes INSERT SQL
        ///    - Retrieves created entity
        /// 5. Commits transaction for entire batch (rollback if any entity fails)
        /// 6. Writes audit records for all created entities if audit trail enabled
        /// 7. If batchSize specified, processes multiple transactions sequentially
        /// 8. Returns all successfully created entities or throws if any batch fails
        /// </summary>
        /// <param name="entities">The entities to create</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="batchSize">The maximum number of entities to create in a single batch</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The created entities with updated tracking fields</returns>
        Task<IEnumerable<T>> CreateAsync(IEnumerable<T> entities, CallerInfo callerInfo, int? batchSize = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates multiple entities in a single batch operation using a custom update function.
        /// Implementation:
        /// 1. Splits entities into batches based on batchSize parameter
        /// 2. For each batch, opens a single transaction for atomicity
        /// 3. For each entity in batch:
        ///    - Applies updateFunc to transform the entity
        ///    - Retrieves current version for optimistic concurrency check
        ///    - Throws ConcurrencyConflictException if versions don't match
        ///    - If soft-delete enabled, gets next version and INSERTs new record
        ///    - If soft-delete disabled, performs in-place UPDATE
        ///    - Updates LastWriteTime, increments Version
        /// 4. Commits transaction for entire batch (rollback if any entity fails)
        /// 5. Writes audit records with old values if audit trail enabled
        /// 6. Continues with next batch even if current batch fails (partial success)
        /// 7. Returns all successfully updated entities
        /// </summary>
        /// <param name="entities">The entities to update</param>
        /// <param name="updateFunc">The function to apply to each entity</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="batchSize">The maximum number of entities to update in a single batch</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The updated entities</returns>
        Task<IEnumerable<T>> UpdateAsync(IEnumerable<T> entities, Func<T, T> updateFunc, CallerInfo callerInfo, int? batchSize = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes multiple entities by their primary keys (soft delete by default).
        /// Implementation:
        /// 1. Splits keys into batches based on batchSize parameter
        /// 2. For each batch, opens a single transaction for atomicity
        /// 3. For each key in batch:
        ///    - Retrieves entity to verify existence
        ///    - If soft-delete enabled:
        ///      * Skips if already deleted (IsDeleted=true)
        ///      * Gets next version from Version table
        ///      * INSERTs new record with IsDeleted=true
        ///    - If hard delete:
        ///      * Executes DELETE SQL to permanently remove
        ///      * Skips if entity doesn't exist (idempotent)
        /// 4. Commits transaction for entire batch
        /// 5. Writes audit records for deleted entities if audit trail enabled
        /// 6. Continues with next batch even if current batch fails
        /// 7. Returns total count of successfully deleted entities
        /// </summary>
        /// <param name="keys">The primary keys of the entities to delete</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="batchSize">The maximum number of entities to delete in a single batch</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The number of entities deleted</returns>
        Task<int> DeleteAsync(IEnumerable<TKey> keys, CallerInfo callerInfo, int? batchSize = null, CancellationToken cancellationToken = default);

        #endregion
    }
}
