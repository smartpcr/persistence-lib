// -----------------------------------------------------------------------
// <copyright file="IListOperation.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines specialized operations for managing collections of related entities as logical lists.
    /// This interface provides atomic operations on entity groups identified by cache keys,
    /// enabling efficient management of related data sets with consistency guarantees.
    /// 
    /// Core Concept:
    /// - List Cache Key: A string identifier that groups related entities together
    /// - Atomic List Operations: All entities in a list are processed as a unit
    /// - List-Entity Mapping: Maintains relationships through EntryListMapping table
    /// - Cache Integration: Lists are cached for improved read performance
    /// 
    /// List Semantics:
    /// - Lists are identified by unique cache keys (e.g., "user:123:orders")
    /// - Entities can belong to multiple lists simultaneously
    /// - List membership is tracked separately from entity data
    /// - Deleting a list removes associations but preserves entities
    /// 
    /// Atomicity Guarantees:
    /// - CreateList: All entities created or none (transaction rollback on failure)
    /// - UpdateList: Replaces entire list atomically
    /// - DeleteList: Removes all list associations in single transaction
    /// - GetList: Returns consistent snapshot of list state
    /// 
    /// Caching Strategy:
    /// - Lists are cached with configurable TTL (default 1 hour)
    /// - Cache invalidated on any list modification
    /// - Cache key includes list identifier and version
    /// - Supports cache warming for frequently accessed lists
    /// 
    /// Mapping Table Structure:
    /// - EntryListMapping stores (ListCacheKey, EntityId, Order)
    /// - Maintains insertion order for ordered lists
    /// - Indexed on both ListCacheKey and EntityId for fast lookups
    /// - Supports many-to-many relationships between lists and entities
    /// 
    /// Use Cases:
    /// - Shopping cart items for a user
    /// - Related configuration settings
    /// - Grouped notification messages
    /// - Batch processing job items
    /// - Cached query results
    /// 
    /// Performance Optimization:
    /// - Bulk operations reduce database round trips
    /// - Prepared statements for repeated operations
    /// - Efficient JOIN queries for list retrieval
    /// - Index optimization for list operations
    /// </summary>
    /// <typeparam name="T">The entity type that implements IEntity<TKey></typeparam>
    /// <typeparam name="TKey">The primary key type that implements IEquatable<TKey></typeparam>
    public interface IListOperation<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        #region list operations 
        /// <summary>
        /// Creates multiple entities in a single batch operation associated with a list cache key.
        /// Implementation:
        /// 1. Opens transaction for atomic operation across all entities and mappings
        /// 2. Deletes existing EntryListMapping records for the listCacheKey
        /// 3. For each entity:
        ///    - Creates entity using standard create logic (version tracking, etc.)
        ///    - Inserts EntryListMapping record linking entity ID to listCacheKey
        /// 4. Sets consistent tracking fields (CreatedTime, LastWriteTime) for all entities
        /// 5. Commits transaction ensuring all entities and mappings are created atomically
        /// 6. If any entity fails, entire operation rolls back
        /// 7. Writes audit records if audit trail enabled
        /// 8. Returns all created entities with their list associations
        /// </summary>
        /// <param name="entities">The entities to create</param>
        /// <param name="listCacheKey">The list cache key to associate all entities with</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The created entities with updated tracking fields</returns>
        Task<IEnumerable<T>> CreateListAsync(string listCacheKey, IEnumerable<T> entities, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all entities associated with a list cache key.
        /// Implementation:
        /// 1. Queries EntryListMapping table for all entity IDs with matching listCacheKey
        /// 2. For each mapped entity ID:
        ///    - Performs JOIN with main entity table
        ///    - Filters by latest version if soft-delete enabled
        ///    - Excludes deleted entities (IsDeleted=true)
        ///    - Excludes expired entities if expiry enabled
        /// 3. Maps all results using entity mapper
        /// 4. Maintains original insertion order from EntryListMapping
        /// 5. Writes audit record for list read if audit trail enabled
        /// 6. Returns empty collection if listCacheKey not found
        /// 7. Caches results in memory with configurable expiration
        /// </summary>
        /// <param name="listCacheKey">The list cache key to associate all entities with</param>
        /// <param name="callerInfo">Information about the caller.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The found entities</returns>
        Task<IEnumerable<T>> GetListAsync(string listCacheKey, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates multiple entities in a single batch operation associated with a list cache key.
        /// Implementation:
        /// 1. Opens transaction for atomic update of all entities and mappings
        /// 2. Deletes existing EntryListMapping records for the listCacheKey
        /// 3. For each entity:
        ///    - Updates entity using standard update logic with concurrency checks
        ///    - Re-inserts EntryListMapping record with updated association
        /// 4. Maintains version consistency across all entities in the list
        /// 5. Updates LastWriteTime for all entities to same timestamp
        /// 6. Commits transaction ensuring atomicity
        /// 7. If any entity fails concurrency check, entire operation rolls back
        /// 8. Invalidates cached list results
        /// 9. Writes audit records with old values if audit trail enabled
        /// 10. Returns all updated entities with refreshed list associations
        /// </summary>
        /// <param name="entities">The entities to update</param>
        /// <param name="listCacheKey">The list cache key to associate all entities with</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The updated entities</returns>
        Task<IEnumerable<T>> UpdateListAsync(string listCacheKey, IEnumerable<T> entities, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes all entities associated with a list cache key.
        /// Implementation:
        /// 1. Opens transaction for atomic deletion
        /// 2. Queries EntryListMapping for all entity IDs with matching listCacheKey
        /// 3. For each mapped entity:
        ///    - If soft-delete enabled, creates new version with IsDeleted=true
        ///    - If hard delete, executes DELETE SQL
        /// 4. Deletes all EntryListMapping records for the listCacheKey
        /// 5. Commits transaction ensuring all deletions are atomic
        /// 6. Invalidates cached list results
        /// 7. Writes audit records for all deleted entities if audit trail enabled
        /// 8. Returns count of entities deleted (excludes already deleted entities)
        /// 9. Returns 0 if listCacheKey not found
        /// </summary>
        /// <param name="listCacheKey">The list cache key to associate all entities with</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The number of entities deleted</returns>
        Task<int> DeleteListAsync(string listCacheKey, CallerInfo callerInfo, CancellationToken cancellationToken = default);

        #endregion
    }
}
