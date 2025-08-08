// -----------------------------------------------------------------------
// <copyright file="IBulkOperation.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines high-performance bulk operations for large-scale data import, export, and maintenance.
    /// This interface provides specialized operations optimized for processing massive datasets
    /// with minimal resource consumption and maximum throughput.
    /// 
    /// Core Capabilities:
    /// - Bulk Import: High-speed data ingestion from various sources
    /// - Bulk Export: Efficient data extraction with streaming support
    /// - Data Purging: Automated cleanup based on retention policies
    /// - Progress Tracking: Real-time monitoring of long-running operations
    /// 
    /// Import Features:
    /// - Staging Tables: Temporary tables for validation before commit
    /// - Conflict Resolution: Configurable handling of duplicate keys
    /// - Data Validation: Schema and constraint checking before import
    /// - Rollback Support: Atomic import with full rollback on failure
    /// - Format Support: JSON, CSV, Binary, and custom formats
    /// 
    /// Export Features:
    /// - Streaming Export: Memory-efficient export of large datasets
    /// - Chunked Files: Split large exports into manageable files
    /// - Compression: Optional gzip/zip compression for storage efficiency
    /// - Manifest Generation: Metadata file describing export contents
    /// - Incremental Export: Export only changed data since last export
    /// 
    /// Purge Operations:
    /// - Retention Policies: Age-based and condition-based cleanup
    /// - Soft Delete Support: Purge old versions while keeping current
    /// - Space Reclamation: VACUUM operations to free disk space
    /// - Preview Mode: Dry run to see what would be deleted
    /// - Backup Integration: Optional backup before destructive operations
    /// 
    /// Performance Optimization:
    /// - Bulk Insert: Uses database-specific bulk loading mechanisms
    /// - Minimal Logging: Reduced transaction log overhead
    /// - Parallel Processing: Multi-threaded import/export when possible
    /// - Index Management: Temporary index disable during bulk operations
    /// - Connection Pooling: Efficient connection reuse
    /// 
    /// Progress Reporting:
    /// - BulkOperationProgress: Real-time status updates
    /// - Percentage Complete: Based on row count or byte size
    /// - ETA Calculation: Estimated time to completion
    /// - Error Tracking: Detailed error information per entity
    /// - Cancellation Support: Graceful operation cancellation
    /// 
    /// Options and Configuration:
    /// - BulkImportOptions: Batch size, parallelism, conflict handling
    /// - BulkExportOptions: Format, compression, chunking settings
    /// - PurgeOptions: Retention period, preview mode, vacuum settings
    /// - Timeout Settings: Configurable timeouts for long operations
    /// 
    /// Result Objects:
    /// - BulkImportResult: Success/failure counts, error details
    /// - BulkExportResult: File paths, entity count, total size
    /// - PurgeResult: Deleted count, space reclaimed, duration
    /// 
    /// Use Cases:
    /// - Initial data migration from legacy systems
    /// - Periodic data synchronization between systems
    /// - Compliance-driven data retention and cleanup
    /// - Backup and restore operations
    /// - Data warehouse ETL processes
    /// - Archive old data to cold storage
    /// 
    /// Best Practices:
    /// - Test import/export with small datasets first
    /// - Use preview mode for purge operations
    /// - Monitor disk space during bulk operations
    /// - Schedule during maintenance windows
    /// - Implement retry logic for transient failures
    /// - Validate data integrity after bulk operations
    /// </summary>
    /// <typeparam name="T">The entity type that implements IEntity&lt;TKey&gt;</typeparam>
    /// <typeparam name="TKey">The primary key type that implements IEquatable&lt;TKey&gt;</typeparam>
    public interface IBulkOperation<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        #region Bulk Operations

        /// <summary>
        /// Imports entities in bulk from an external source.
        /// Implementation:
        /// 1. Validates import options (conflict resolution, batch size, parallelism)
        /// 2. Creates temporary staging table for bulk data
        /// 3. Uses SQLite bulk insert with prepared statements for performance
        /// 4. For each batch (default 1000 entities):
        ///    - Begins transaction with PRAGMA synchronous = OFF for speed
        ///    - Inserts into staging table
        ///    - Handles conflicts based on options (Skip, Overwrite, Merge)
        ///    - For Merge: compares versions and merges fields
        ///    - Moves from staging to main table
        ///    - Reports progress via IProgress callback
        /// 5. Validates data integrity after import
        /// 6. Creates audit records for imported entities if enabled
        /// 7. Returns BulkImportResult with:
        ///    - SuccessCount: entities imported successfully
        ///    - FailureCount: entities that failed
        ///    - SkippedCount: entities skipped due to conflicts
        ///    - Errors: detailed error information
        /// </summary>
        /// <param name="entities">The entities to import</param>
        /// <param name="options">Import options</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Import result with statistics</returns>
        Task<BulkImportResult> BulkImportAsync(
            IEnumerable<T> entities,
            BulkImportOptions options = null,
            IProgress<BulkOperationProgress> progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Imports entities in bulk from export files.
        /// Implementation:
        /// 1. Reads manifest JSON file containing export metadata
        /// 2. Validates manifest version and entity type compatibility
        /// 3. Locates data files referenced in manifest (supports chunked files)
        /// 4. For each data file:
        ///    - Deserializes entities from JSON/CSV/Binary format
        ///    - Validates entity schema matches current version
        ///    - Performs data migration if schema version differs
        /// 5. Delegates to BulkImportAsync for actual import
        /// 6. Handles compressed files (gzip, zip) transparently
        /// 7. Validates checksums if present in manifest
        /// 8. Supports incremental import with resume capability
        /// 9. Cleans up temporary files after successful import
        /// 10. Returns aggregated BulkImportResult from all files
        /// </summary>
        /// <param name="manifestPath">Path to the export manifest file</param>
        /// <param name="options">Import options</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Import result with statistics</returns>
        Task<BulkImportResult> BulkImportFromFileAsync(
            string manifestPath,
            BulkImportOptions options = null,
            IProgress<BulkOperationProgress> progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports entities in bulk based on criteria.
        /// Implementation:
        /// 1. Builds query with predicate filter (exports all if null)
        /// 2. Counts total entities for progress reporting
        /// 3. Based on export options format (JSON, CSV, Binary):
        ///    - Creates appropriate serializer
        ///    - Sets up file writer with buffering
        /// 4. Exports in batches to manage memory:
        ///    - Default batch size 1000 entities
        ///    - Uses SELECT with LIMIT/OFFSET for pagination
        ///    - Streams directly to file without loading all in memory
        /// 5. If ChunkSize specified, splits into multiple files
        /// 6. Creates manifest file with:
        ///    - Export timestamp, entity count, schema version
        ///    - File paths, checksums, compression info
        /// 7. Optionally compresses output files (gzip, zip)
        /// 8. Reports progress for each batch exported
        /// 9. Returns BulkExportResult with:
        ///    - ExportedEntities: collection of exported entities (if IncludeData=true)
        ///    - ManifestPath: path to manifest file
        ///    - FileCount: number of files created
        ///    - TotalSize: total bytes written
        /// </summary>
        /// <param name="predicate">Filter for entities to export</param>
        /// <param name="options">Export options</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Export result with exported entities</returns>
        Task<BulkExportResult<T>> BulkExportAsync(
            Expression<Func<T, bool>> predicate = null,
            BulkExportOptions options = null,
            IProgress<BulkOperationProgress> progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Purges entities based on retention policies.
        /// Implementation:
        /// 1. Validates purge options (retention period, max age, conditions)
        /// 2. Builds purge query combining:
        ///    - User predicate (if provided)
        ///    - Age filter (LastWriteTime &lt; UtcNow - RetentionPeriod)
        ///    - Soft-delete filter (only purge already deleted if specified)
        /// 3. If PreviewMode=true, only counts and returns without deleting
        /// 4. Creates backup before purge if BackupBeforePurge=true
        /// 5. For soft-delete enabled:
        ///    - Permanently removes old versions keeping latest N versions
        ///    - Or removes all versions older than retention period
        /// 6. For hard delete:
        ///    - Executes DELETE in batches to avoid lock timeout
        ///    - Uses DELETE with LIMIT for controlled deletion
        /// 7. Performs VACUUM if VacuumAfterPurge=true to reclaim space
        /// 8. Reports progress for each batch purged
        /// 9. Writes audit records for purge operation if enabled
        /// 10. Returns PurgeResult with:
        ///     - PurgedCount: number of entities/versions removed
        ///     - SpaceReclaimed: bytes freed (after VACUUM)
        ///     - BackupPath: path to backup if created
        ///     - Duration: time taken for purge operation
        /// </summary>
        /// <param name="predicate">Filter for entities to purge</param>
        /// <param name="options">Purge options</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Purge result with statistics</returns>
        Task<PurgeResult> PurgeAsync(
            Expression<Func<T, bool>> predicate = null,
            PurgeOptions options = null,
            IProgress<BulkOperationProgress> progress = null,
            CancellationToken cancellationToken = default);

        #endregion
    }
}
