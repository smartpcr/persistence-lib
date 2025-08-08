// -----------------------------------------------------------------------
// <copyright file="IQueryOperation.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines advanced query operations with LINQ expression support for flexible data retrieval.
    /// This interface provides powerful querying capabilities including filtering, sorting,
    /// pagination, and aggregation while maintaining type safety through expression trees.
    /// 
    /// Expression Support:
    /// - LINQ Expressions: Type-safe queries using lambda expressions
    /// - Expression Translation: Converts LINQ to provider-specific query language (SQL)
    /// - Predicate Composition: Combine multiple conditions with AND/OR logic
    /// - Dynamic Queries: Build queries at runtime based on user input
    /// 
    /// Query Capabilities:
    /// - Filtering: Complex WHERE clauses with multiple conditions
    /// - Sorting: Single or multi-column ordering with custom comparers
    /// - Pagination: Efficient data paging with skip/take semantics
    /// - Projection: Select specific fields to reduce data transfer
    /// - Aggregation: Count, exists, and other aggregate operations
    /// 
    /// Performance Features:
    /// - Query Optimization: Expression trees analyzed for optimal execution
    /// - Index Hints: Automatic index selection based on query predicates
    /// - Query Caching: Results cached with query hash as key
    /// - Lazy Loading: Results streamed to minimize memory usage
    /// - Query Plan Reuse: Prepared statements for repeated queries
    /// 
    /// Pagination Model:
    /// - PagedResult: Encapsulates page data with metadata
    /// - Total Count: Efficient count query runs in parallel
    /// - Page Navigation: Includes total pages, current page info
    /// - Stable Pagination: Consistent results across page requests
    /// 
    /// Expression Limitations:
    /// - Not all LINQ operators may be supported by provider
    /// - Complex expressions may require client-side evaluation
    /// - Some functions may not translate to database queries
    /// - Provider-specific extensions may be available
    /// 
    /// Query Execution:
    /// - Deferred Execution: Queries built but not executed until enumerated
    /// - Single Execution: Each query executed once per call
    /// - Streaming Results: Large result sets streamed efficiently
    /// - Timeout Control: Configurable query timeout settings
    /// 
    /// Use Cases:
    /// - Dynamic search interfaces with user-defined filters
    /// - Report generation with complex criteria
    /// - Data export with selective field inclusion
    /// - Real-time dashboards with aggregated data
    /// - API endpoints with OData-style queries
    /// 
    /// Best Practices:
    /// - Use indexes on frequently queried columns
    /// - Avoid complex expressions that prevent index usage
    /// - Implement pagination for large result sets
    /// - Cache frequently executed queries
    /// - Monitor query performance and optimize hot paths
    /// </summary>
    /// <typeparam name="T">The entity type that implements IEntity&lt;TKey&gt;</typeparam>
    /// <typeparam name="TKey">The primary key type that implements IEquatable&lt;TKey&gt;</typeparam>
    public interface IQueryOperation<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        #region Query Operations

        /// <summary>
        /// Queries entities based on a predicate expression.
        /// Implementation:
        /// 1. Translates LINQ Expression predicate to SQL WHERE clause using expression visitor
        /// 2. Builds parameterized SQL query with proper escaping
        /// 3. If soft-delete enabled, adds filter for latest version and IsDeleted=false
        /// 4. If expiry enabled, filters out expired entities
        /// 5. Applies orderBy expression to SQL ORDER BY clause
        /// 6. Implements pagination with LIMIT and OFFSET for skip/take
        /// 7. Executes query and maps results using entity mapper
        /// 8. Caches query results with hash of predicate as cache key
        /// 9. Writes audit record for query execution if audit trail enabled
        /// 10. Returns empty collection if no matches found
        /// </summary>
        /// <param name="predicate">The filter expression</param>
        /// <param name="orderBy">The order by expression</param>
        /// <param name="callerInfo">Information about the caller for auditing and tracking purposes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="skip">Number of items to skip</param>
        /// <param name="take">Number of items to take</param>
        /// <returns>Entities matching the predicate</returns>
        Task<IEnumerable<T>> QueryAsync(
            Expression<Func<T, bool>> predicate,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy,
            CallerInfo callerInfo,
            int? skip = null,
            int? take = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries entities with pagination support.
        /// Implementation:
        /// 1. Validates pageNumber > 0 and pageSize > 0
        /// 2. Calculates skip = (pageNumber - 1) * pageSize
        /// 3. Executes COUNT query to get total matching records
        /// 4. Builds main query with predicate filter
        /// 5. Applies orderBy or default ordering by primary key
        /// 6. Adds ASC/DESC based on ascending parameter
        /// 7. Applies LIMIT pageSize OFFSET skip for pagination
        /// 8. Maps results and creates PagedResult with:
        ///    - Items: current page entities
        ///    - TotalCount: total matching records
        ///    - PageNumber: current page
        ///    - PageSize: items per page
        ///    - TotalPages: calculated from totalCount/pageSize
        /// 9. Caches paged results with composite key of predicate+page
        /// 10. Returns empty PagedResult if no matches
        /// </summary>
        /// <param name="predicate">The filter expression</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="orderBy">Order by expression</param>
        /// <param name="ascending">Sort direction</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Page of entities</returns>
        Task<PagedResult<T>> QueryPagedAsync(
            Expression<Func<T, bool>> predicate,
            int pageSize,
            int pageNumber,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
            bool ascending = true,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts entities matching a predicate.
        /// Implementation:
        /// 1. Builds SELECT COUNT(*) query
        /// 2. If predicate provided, translates to WHERE clause
        /// 3. If soft-delete enabled, counts only non-deleted entities (IsDeleted=false)
        /// 4. If expiry enabled, excludes expired entities
        /// 5. For soft-delete with versions, groups by key and counts distinct keys
        /// 6. Executes scalar query returning Int64 count
        /// 7. Caches count result with predicate hash as key
        /// 8. Returns 0 if no matching entities
        /// 9. Does not write audit records for count operations
        /// </summary>
        /// <param name="predicate">The filter expression</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Count of matching entities</returns>
        Task<long> CountAsync(Expression<Func<T, bool>> predicate = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if any entity exists matching a predicate.
        /// Implementation:
        /// 1. Builds SELECT EXISTS(SELECT 1 FROM table WHERE ...) query
        /// 2. Translates predicate to WHERE clause
        /// 3. If soft-delete enabled, checks only non-deleted entities
        /// 4. If expiry enabled, excludes expired entities
        /// 5. Uses LIMIT 1 optimization for early termination
        /// 6. Executes scalar query returning boolean
        /// 7. More efficient than Count > 0 as stops at first match
        /// 8. Caches existence result briefly (short TTL)
        /// 9. Returns false if no matching entities
        /// 10. Does not write audit records for existence checks
        /// </summary>
        /// <param name="predicate">The filter expression</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if any entity matches</returns>
        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

        #endregion
    }
}
