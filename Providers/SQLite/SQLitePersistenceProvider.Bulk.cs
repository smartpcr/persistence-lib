// -----------------------------------------------------------------------
// <copyright file="SQLitePersistenceProvider.Bulk.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite
{
    using System;
    using System.Collections.Generic;
    using System.Data.SQLite;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using CsvHelper;
    using CsvHelper.Configuration;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using SystemJson = System.Text.Json;
    using SystemJsonSerializer = System.Text.Json.JsonSerializer;
    using Newtonsoft.Json;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Traces;

    public partial class SQLitePersistenceProvider<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        #region Bulk operations

        public async Task<BulkImportResult> BulkImportAsync(
            IEnumerable<T> entities,
            BulkImportOptions options = null,
            IProgress<BulkOperationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new BulkImportOptions();
            var result = new BulkImportResult
            {
                Metadata = new ImportMetadata
                {
                    ImportTimestamp = DateTime.UtcNow,
                    Strategy = options.Strategy
                }
            };
            var startTime = DateTime.UtcNow;

            if (entities == null)
            {
                result.Errors.Add("Entities collection is null");
                return result;
            }

            var entityList = entities.ToList();
            var totalCount = entityList.Count;

            if (totalCount == 0)
            {
                return result;
            }

            var stopwatch = Stopwatch.StartNew();
            PersistenceLogger.BulkOperationStart("Import", totalCount);

            try
            {
                await using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                // Handle Replace strategy - clear existing data
                if (options.Strategy == ImportStrategy.Replace)
                {
                    await this.ClearExistingDataAsync(connection, cancellationToken);
                }

                // Get next version for versioned entities
                long nextVersion = 1;
                if (this.Mapper.EnableSoftDelete)
                {
                    await using var versionCmd = this.versionMapper.CreateGetNextVersionCommand();
                    versionCmd.Connection = connection;
                    var versionResult = await versionCmd.ExecuteScalarAsync(cancellationToken);
                    nextVersion = Convert.ToInt64(versionResult);
                }

                // Load existing entities for merge/conflict resolution
                var existingEntities = new Dictionary<string, T>();
                if (options.Strategy != ImportStrategy.Replace)
                {
                    existingEntities = await this.LoadExistingEntitiesAsync(connection, cancellationToken);
                }

                // Process entities
                var processedCount = 0;
                for (var i = 0; i < totalCount; i += options.BatchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var batch = entityList.Skip(i).Take(options.BatchSize).ToList();

                    foreach (var entity in batch)
                    {
                        try
                        {
                            // Validate entity
                            if (options.ValidateBeforeImport && entity == null)
                            {
                                result.FailureCount++;
                                result.Errors.Add($"Entity at index {processedCount} is null");
                                processedCount++;
                                continue;
                            }

                            var importResult = await this.ProcessEntityImportAsync(
                                entity,
                                existingEntities,
                                options,
                                connection,
                                nextVersion,
                                cancellationToken);

                            // Update statistics
                            switch (importResult.Action)
                            {
                                case ImportAction.Created:
                                    result.SuccessCount++;
                                    result.Statistics.EntitiesCreated++;
                                    break;
                                case ImportAction.Updated:
                                    result.SuccessCount++;
                                    result.Statistics.EntitiesUpdated++;
                                    break;
                                case ImportAction.Skipped:
                                    result.Statistics.EntitiesSkipped++;
                                    break;
                                case ImportAction.Failed:
                                    result.FailureCount++;
                                    result.Errors.Add(importResult.Error);
                                    break;
                            }

                            if (importResult.Conflict != null)
                            {
                                result.Conflicts.Add(importResult.Conflict);
                                if (importResult.Conflict.Resolution != ConflictResolution.Manual)
                                {
                                    result.Statistics.ConflictsResolved++;
                                }
                            }

                            processedCount++;

                            // Report progress
                            if (progress != null && processedCount % 100 == 0)
                            {
                                var progressInfo = new BulkOperationProgress
                                {
                                    ProcessedCount = processedCount,
                                    TotalCount = totalCount,
                                    ElapsedTime = DateTime.UtcNow - startTime,
                                    CurrentOperation = $"Importing entities ({processedCount}/{totalCount})"
                                };
                                progress.Report(progressInfo);
                                PersistenceLogger.BulkOperationProgress((int)progressInfo.PercentComplete, processedCount, totalCount);
                            }
                        }
                        catch (Exception ex)
                        {
                            result.FailureCount++;
                            var keyString = this.Mapper.SerializeKey(entity.Id);
                            result.Errors.Add($"Error importing entity {keyString}: {ex.Message}");

                            throw;
                        }
                    }
                }

                result.Statistics.TotalEntitiesProcessed = processedCount;
                result.Duration = DateTime.UtcNow - startTime;

                // Write audit log
                await this.WriteImportAuditAsync(result, cancellationToken);

                stopwatch.Stop();
                PersistenceLogger.BulkOperationStop("Import", processedCount, stopwatch);
                return result;
            }
            catch (Exception ex)
            {
                result.RolledBack = true;
                stopwatch.Stop();
                PersistenceLogger.BulkOperationFailed("Import", stopwatch, ex);
                throw;
            }
        }

        public async Task<BulkImportResult> BulkImportFromFileAsync(
            string filePath,
            BulkImportOptions options = null,
            IProgress<BulkOperationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new BulkImportOptions();

            try
            {
                // Determine file format
                var detectedFormat = options.FileFormat;
                if (detectedFormat == FileFormat.Auto)
                {
                    detectedFormat = this.DetermineFileFormat(filePath);
                }

                // Check if this is a manifest file (JSON with specific structure)
                bool isManifest = false;
                if (detectedFormat == FileFormat.Json && filePath.Contains("manifest"))
                {
                    isManifest = true;
                }

                var result = new BulkImportResult
                {
                    Metadata = new ImportMetadata
                    {
                        ImportTimestamp = DateTime.UtcNow,
                        SourceManifestPath = filePath,
                        Strategy = options.Strategy
                    }
                };

                var allEntities = new List<T>();

                if (isManifest)
                {
                    // Read and validate manifest
                    var manifestJson = await filePath.ReadAllTextAsync(cancellationToken);
                    var manifest = SystemJsonSerializer.Deserialize<ExportManifest>(manifestJson);

                    result.Metadata.SourceSchemaVersion = manifest.Metadata.SchemaVersion;

                    // Validate schema if requested
                    if (options.ValidateSchema)
                    {
                        if (!string.IsNullOrEmpty(options.ExpectedSchemaVersion) &&
                            manifest.Metadata.SchemaVersion != options.ExpectedSchemaVersion)
                        {
                            result.Errors.Add($"Schema version mismatch. Expected: {options.ExpectedSchemaVersion}, Found: {manifest.Metadata.SchemaVersion}");
                            result.Metadata.SchemaValidationPassed = false;
                            return result;
                        }
                        result.Metadata.SchemaValidationPassed = true;
                    }

                    // Read entities from data files
                    var manifestDir = Path.GetDirectoryName(filePath);

                    foreach (var dataFile in manifest.DataFiles)
                    {
                        var dataPath = Path.Combine(manifestDir!, dataFile.FileName);
                        var checksum = await dataPath.GetFileHashAsync();
                        if (checksum != dataFile.Checksum)
                        {
                            result.Errors.Add($"Checksum mismatch for file {dataFile.FileName}");
                            continue;
                        }

                        // Read entities with format detection
                        var entities = await this.ReadDataFileWithFormat(dataPath, dataFile.IsCompressed, options, cancellationToken);
                        allEntities.AddRange(entities);
                    }
                }
                else
                {
                    // Direct file import (single CSV or JSON file)
                    var isCompressed = Path.GetExtension(filePath).ToLowerInvariant() == ".gz";
                    allEntities = await this.ReadDataFileWithFormat(filePath, isCompressed, options, cancellationToken);
                }

                // Import entities using the main import method
                return await this.BulkImportAsync(allEntities, options, progress, cancellationToken);
            }
            catch (Exception ex)
            {
                var result = new BulkImportResult();
                result.Errors.Add($"Failed to import from file: {ex.Message}");
                return result;
            }
        }

        private async Task<List<T>> ReadDataFileWithFormat(
            string filePath,
            bool isCompressed,
            BulkImportOptions options,
            CancellationToken cancellationToken)
        {
            // Determine format
            var format = options.FileFormat;
            if (format == FileFormat.Auto)
            {
                format = this.DetermineFileFormat(filePath);
            }

            string content;

            if (isCompressed)
            {
                await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                using var reader = new StreamReader(gzipStream);
                content = await reader.ReadToEndAsync(cancellationToken);
            }
            else
            {
                content = await filePath.ReadAllTextAsync(cancellationToken);
            }

            if (format == FileFormat.Csv)
            {
                return this.ParseCsvContent(content, options.CsvOptions);
            }
            else
            {
                // Try Newtonsoft.Json first since our entities use JsonProperty attributes
                // This ensures compatibility with existing code and tests
                try
                {
                    var result = JsonConvert.DeserializeObject<List<T>>(content);
                    if (result != null && result.Count > 0)
                    {
                        return result;
                    }
                }
                catch (JsonException)
                {
                    // Fall through to try System.Text.Json
                }
                
                // If Newtonsoft fails or returns empty, try System.Text.Json
                // with case-insensitive property matching
                var jsonOptions = new SystemJson.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                return SystemJsonSerializer.Deserialize<List<T>>(content, jsonOptions);
            }
        }

        public async Task<BulkExportResult<T>> BulkExportAsync(
            Expression<Func<T, bool>> predicate = null,
            BulkExportOptions options = null,
            IProgress<BulkOperationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new BulkExportOptions();
            var startTime = DateTime.UtcNow;
            var result = new BulkExportResult<T>
            {
                Metadata = new ExportMetadata
                {
                    SchemaVersion = "1.0",
                    ExportTimestamp = startTime,
                    EntityType = typeof(T).Name,
                    SoftDeleteEnabled = this.Mapper.EnableSoftDelete,
                    ExportMode = options.Mode
                }
            };

            var stopwatch = Stopwatch.StartNew();
            PersistenceLogger.BulkOperationStart("Export", 0); // Count unknown at start

            try
            {
                // Validate export path if file export is requested
                if (!string.IsNullOrEmpty(options.ExportFolder))
                {
                    Directory.CreateDirectory(options.ExportFolder);
                }

                await using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                // Build base WHERE clause from predicate
                var whereClause = "1=1"; // Default condition
                var parameters = new Dictionary<string, object>();

                if (predicate != null)
                {
                    var translator = new SQLiteExpressionTranslator<T>(
                        this.Mapper.GetPropertyMappings(),
                        () => this.Mapper.GetPrimaryKeyColumn());
                    var translationResult = translator.Translate(predicate);
                    whereClause = translationResult.Sql;
                    parameters = translationResult.Parameters;
                    result.Metadata.FilterCriteria = whereClause;
                }

                // Apply mode-specific filters
                whereClause = this.ApplyExportModeFilters(whereClause, options);

                // Apply soft delete filter if needed
                if (this.Mapper.EnableSoftDelete && !options.IncludeDeleted)
                {
                    whereClause += " AND IsDeleted = 0";
                }

                if (this.Mapper.EnableExpiry && !options.IncludeExpired)
                {
                    whereClause += @$" AND (
    AbsoluteExpiration IS NULL OR datetime(AbsoluteExpiration) > datetime('{DateTime.UtcNow:O}')
  )";
                }

                // Get total count for progress reporting
                var totalCount = await this.GetExportCountAsync(connection, whereClause, parameters, options, cancellationToken);
                result.Metadata.EntityCount = totalCount;

                // Export entities based on mode
                var exportedEntities = await this.ExportEntitiesAsync(
                    connection,
                    whereClause,
                    parameters,
                    options,
                    progress,
                    totalCount,
                    startTime,
                    cancellationToken);

                // Always populate ExportedEntities for both in-memory and file exports
                result.ExportedEntities = exportedEntities;
                
                // Write to files if export path is specified
                if (!string.IsNullOrEmpty(options.ExportFolder))
                {
                    await this.WriteExportFilesAsync(exportedEntities, options, result, cancellationToken);
                }

                // Mark entities as exported if in archive mode
                if (options.Mode == ExportMode.Archive && options.MarkAsExported)
                {
                    await this.MarkEntitiesAsExportedAsync(connection, whereClause, parameters, cancellationToken);
                    result.EntitiesMarkedAsExported = true;
                }

                result.ExportedCount = exportedEntities.Count;
                result.Duration = DateTime.UtcNow - startTime;

                stopwatch.Stop();
                PersistenceLogger.BulkOperationStop("Export", result.ExportedCount, stopwatch);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PersistenceLogger.BulkOperationFailed("Export", stopwatch, ex);
                throw;
            }
        }

        public async Task<PurgeResult> PurgeAsync(
            Expression<Func<T, bool>> predicate = null,
            PurgeOptions options = null,
            IProgress<BulkOperationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new PurgeOptions();
            var result = new PurgeResult
            {
                IsPreview = options.SafeMode,
                Audit = new PurgeAudit
                {
                    PurgeTimestamp = DateTime.UtcNow,
                    InitiatedBy = "System"
                }
            };
            var startTime = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            SQLiteConnection connection = null;
            SQLiteTransaction transaction = null;

            try
            {
                connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                // Build WHERE clause
                var whereClause = this.BuildPurgeWhereClause(options);
                var parameters = new Dictionary<string, object>();

                if (predicate != null)
                {
                    var translator = new SQLiteExpressionTranslator<T>(
                        this.Mapper.GetPropertyMappings(),
                        () => this.Mapper.GetPrimaryKeyColumn());
                    var translationResult = translator.Translate(predicate);
                    whereClause = this.CombineWhereClauses(whereClause, translationResult.Sql);
                    parameters = translationResult.Parameters;
                }

                result.Audit.PurgeCriteria = whereClause;

                // Preview mode - just analyze what would be purged
                if (options.SafeMode)
                {
                    result.Preview = await this.GeneratePurgePreviewAsync(
                        connection,
                        whereClause,
                        parameters,
                        options,
                        cancellationToken);

                    result.Duration = DateTime.UtcNow - startTime;
                    return result;
                }

                // Backup before purge if requested
                if (options.BackupBeforePurge)
                {
                    result.Backup = await this.CreateBackupBeforePurgeAsync(
                        whereClause,
                        parameters,
                        options,
                        progress,
                        cancellationToken);

                    if (!result.Backup.Success)
                    {
                        result.Aborted = true;
                        result.AbortReason = "Backup failed";
                        result.Errors.Add(result.Backup.Error);
                        return result;
                    }

                    result.Statistics.BackupTime = result.Backup.BackupDuration;
                }

                // Start transaction if requested
                if (options.UseTransaction)
                {
                    transaction = connection.BeginTransaction();
                }

                // Perform the actual purge
                var purgeStats = await this.ExecutePurgeAsync(
                    connection,
                    transaction,
                    whereClause,
                    parameters,
                    options,
                    progress,
                    cancellationToken);

                result.EntitiesPurged = purgeStats.EntitiesPurged;
                result.VersionsPurged = purgeStats.VersionsPurged;
                result.SpaceReclaimedBytes = purgeStats.SpaceReclaimed;
                result.Statistics = purgeStats.Statistics;

                // Commit transaction if used
                if (options.UseTransaction && transaction != null)
                {
                    transaction.Commit();
                }

                // Optimize storage if requested
                if (options.OptimizeStorage)
                {
                    var optimizeStart = DateTime.UtcNow;
                    await this.OptimizeStorageAsync(connection, cancellationToken);
                    result.Statistics.OptimizationTime = DateTime.UtcNow - optimizeStart;
                }

                result.Duration = DateTime.UtcNow - startTime;
                result.Audit.Completed = true;

                // Write audit log
                await this.WritePurgeAuditAsync(result, cancellationToken);

                stopwatch.Stop();
                PersistenceLogger.BulkOperationStop("Purge", result.EntitiesPurged, stopwatch);

                return result;
            }
            catch (Exception ex)
            {
                transaction?.Rollback();
                result.Aborted = true;
                result.AbortReason = ex.Message;
                result.Errors.Add($"Purge failed: {ex.Message}");

                stopwatch.Stop();
                PersistenceLogger.BulkOperationFailed("Purge", stopwatch, ex);

                throw;
            }
            finally
            {
                transaction?.Dispose();
                connection.Dispose();
            }
        }

        #region private methods

        private async Task<Dictionary<string, T>> LoadExistingEntitiesAsync(
            SQLiteConnection connection,
            CancellationToken cancellationToken)
        {
            var existingEntities = new Dictionary<string, T>();

            var sql = this.Mapper.EnableSoftDelete
                ? $@"
                    WITH LatestVersions AS (
                        SELECT *, ROW_NUMBER() OVER (PARTITION BY {this.Mapper.GetPrimaryKeyColumn()} ORDER BY Version DESC) as rn
                        FROM {this.Mapper.TableName}
                    )
                    SELECT {string.Join(", ", this.Mapper.GetSelectColumns().Select(c => $"lv.{c}"))}
                    FROM LatestVersions lv
                    WHERE lv.rn = 1"
                : $"SELECT {string.Join(", ", this.Mapper.GetSelectColumns())} FROM {this.Mapper.TableName}";

            using var cmd = this.CreateCommand(sql, connection);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var entity = this.Mapper.MapFromReader(reader);
                if (entity != null)
                {
                    var key = this.Mapper.SerializeKey(entity.Id);
                    existingEntities[key] = entity;
                }
            }

            return existingEntities;
        }

        private async Task ClearExistingDataAsync(
            SQLiteConnection connection,
            CancellationToken cancellationToken)
        {
            var sql = $"DELETE FROM {this.Mapper.TableName}";
            using var cmd = this.CreateCommand(sql, connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<EntityImportResult> ProcessEntityImportAsync(
            T entity,
            Dictionary<string, T> existingEntities,
            BulkImportOptions options,
            SQLiteConnection connection,
            long nextVersion,
            CancellationToken cancellationToken)
        {
            var result = new EntityImportResult();
            var key = this.Mapper.SerializeKey(entity.Id);
            var existingEntity = existingEntities.TryGetValue(key, out var entity1) ? entity1 : null;

            // Handle based on strategy
            if (existingEntity == null)
            {
                // New entity - insert
                await this.InsertEntityAsync(entity, connection, nextVersion, cancellationToken);
                result.Action = ImportAction.Created;
            }
            else
            {
                // Existing entity - handle based on strategy
                switch (options.Strategy)
                {
                    case ImportStrategy.Merge:
                        // Skip existing entities in merge mode
                        result.Action = ImportAction.Skipped;
                        break;

                    case ImportStrategy.Upsert:
                        // Check for conflicts and resolve
                        var conflict = this.DetectConflict(entity, existingEntity);
                        if (conflict != null)
                        {
                            conflict.EntityKey = key;
                            result.Conflict = conflict;

                            var resolved = await this.ResolveConflictAsync(
                                entity,
                                existingEntity,
                                options.ConflictResolution,
                                options.FieldMergePriorities,
                                connection,
                                nextVersion,
                                cancellationToken);

                            if (resolved)
                            {
                                result.Action = ImportAction.Updated;
                                conflict.Resolution = options.ConflictResolution;
                            }
                            else
                            {
                                result.Action = ImportAction.Skipped;
                                conflict.Resolution = ConflictResolution.Manual;
                            }
                        }
                        else
                        {
                            // No conflict - update
                            await this.UpdateEntityAsync(entity, existingEntity, connection, nextVersion, cancellationToken);
                            result.Action = ImportAction.Updated;
                        }
                        break;
                }
            }

            return result;
        }

        private ConflictDetail DetectConflict(T sourceEntity, T targetEntity)
        {
            // Check version conflict
            if (sourceEntity.Version != targetEntity.Version)
            {
                return new ConflictDetail
                {
                    Type = ConflictType.Version,
                    SourceVersion = sourceEntity.Version,
                    TargetVersion = targetEntity.Version,
                    Details = "Version numbers do not match"
                };
            }

            // Check data conflicts (simplified - could be enhanced with field-by-field comparison)
            var sourceJson = SystemJsonSerializer.Serialize(sourceEntity);
            var targetJson = SystemJsonSerializer.Serialize(targetEntity);

            if (sourceJson != targetJson)
            {
                return new ConflictDetail
                {
                    Type = ConflictType.Data,
                    SourceVersion = sourceEntity.Version,
                    TargetVersion = targetEntity.Version,
                    Details = "Entity data differs"
                };
            }

            return null;
        }

        private async Task<bool> ResolveConflictAsync(
            T sourceEntity,
            T targetEntity,
            ConflictResolution resolution,
            string[] fieldPriorities,
            SQLiteConnection connection,
            long nextVersion,
            CancellationToken cancellationToken)
        {
            switch (resolution)
            {
                case ConflictResolution.UseSource:
                    if (this.Mapper.EnableSoftDelete)
                    {
                        await this.InsertEntityAsync(sourceEntity, connection, nextVersion, cancellationToken);
                    }
                    else
                    {
                        await this.UpdateEntityAsync(sourceEntity, targetEntity, connection, nextVersion, cancellationToken);
                    }

                    return true;

                case ConflictResolution.UseTarget:
                    // Keep existing - no action needed
                    return false;

                case ConflictResolution.Merge:
                    // Simple merge - could be enhanced with field-level merging
                    var mergedEntity = this.MergeEntities(sourceEntity, targetEntity, fieldPriorities);
                    await this.UpdateEntityAsync(mergedEntity, targetEntity, connection, nextVersion, cancellationToken);
                    return true;

                case ConflictResolution.Manual:
                    // Log for manual resolution
                    return false;

                default:
                    return false;
            }
        }

        private T MergeEntities(T source, T target, string[] fieldPriorities)
        {
            // Simple implementation - take newer LastWriteTime
            // Could be enhanced to merge based on field priorities
            return source.LastWriteTime > target.LastWriteTime ? source : target;
        }

        private Task InsertEntityAsync(
            T entity,
            SQLiteConnection connection,
            long version,
            CancellationToken cancellationToken)
        {
            entity.Version = version;
            entity.CreatedTime = entity.CreatedTime == default ? DateTime.UtcNow : entity.CreatedTime;
            entity.LastWriteTime = DateTime.UtcNow;

            if (this.Mapper.EnableSoftDelete && entity is IVersionedEntity<TKey> versionedEntity)
            {
                versionedEntity.IsDeleted = false;
            }

            if (this.Mapper.EnableExpiry && entity is IExpirableEntity<TKey> expirableEntity)
            {
                expirableEntity.AbsoluteExpiration = entity.CreatedTime.Add(this.Mapper.ExpirySpan ?? TimeSpan.FromDays(7));
                if (expirableEntity.AbsoluteExpiration < DateTime.UtcNow)
                {
                    expirableEntity.AbsoluteExpiration = DateTime.UtcNow.Add(this.Mapper.ExpirySpan ?? TimeSpan.FromDays(7));
                }
            }

            if (this.Mapper.EnableArchive && entity is IArchivableEntity<TKey> archivableEntity)
            {
                archivableEntity.IsArchived = false;
            }

            var context = CommandContext<T, TKey>.ForInsert(entity);
            context.CommandTimeout = this.configuration.CommandTimeout;
            using var cmd = this.Mapper.CreateCommand(DbOperationType.Insert, context);
            cmd.Connection = connection;
            cmd.ExecuteNonQuery();

            return Task.CompletedTask;
        }

        private Task UpdateEntityAsync(
            T entity,
            T oldEntity,
            SQLiteConnection connection,
            long version,
            CancellationToken cancellationToken)
        {
            entity.Version = version;
            entity.LastWriteTime = DateTime.UtcNow;

            var context = CommandContext<T, TKey>.ForUpdate(entity, oldEntity);
            context.CommandTimeout = this.configuration.CommandTimeout;
            using var cmd = this.Mapper.CreateCommand(DbOperationType.Update, context);
            cmd.Connection = connection;
            cmd.ExecuteNonQuery();

            return Task.CompletedTask;
        }

        private async Task<List<T>> ReadDataFileAsync(
            string filePath,
            bool isCompressed,
            CancellationToken cancellationToken)
        {
            // Determine format from file extension
            var format = this.DetermineFileFormat(filePath);
            string content;

            if (isCompressed)
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                using var reader = new StreamReader(gzipStream);
                content = await reader.ReadToEndAsync();
            }
            else
            {
                content = await filePath.ReadAllTextAsync(cancellationToken);
            }

            if (format == FileFormat.Csv)
            {
                return this.ParseCsvContent(content, new CsvOptions());
            }
            else
            {
                // Try Newtonsoft.Json first since our entities use JsonProperty attributes
                // This ensures compatibility with existing code and tests
                try
                {
                    var result = JsonConvert.DeserializeObject<List<T>>(content);
                    if (result != null && result.Count > 0)
                    {
                        return result;
                    }
                }
                catch (JsonException)
                {
                    // Fall through to try System.Text.Json
                }
                
                // If Newtonsoft fails or returns empty, try System.Text.Json
                // with case-insensitive property matching
                var jsonOptions = new SystemJson.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                return SystemJsonSerializer.Deserialize<List<T>>(content, jsonOptions);
            }
        }

        private FileFormat DetermineFileFormat(string filePath)
        {
            // First try to determine from file extension
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            // Remove compression extension if present
            if (extension == ".gz")
            {
                var uncompressedPath = Path.GetFileNameWithoutExtension(filePath);
                extension = Path.GetExtension(uncompressedPath).ToLowerInvariant();
            }

            // If we have a clear extension, use it
            if (extension == ".csv")
                return FileFormat.Csv;
            if (extension == ".json")
                return FileFormat.Json;

            // Otherwise, peek at the file content to determine format
            try
            {
                string firstLine = null;
                
                // Check if file is compressed
                if (Path.GetExtension(filePath).ToLowerInvariant() == ".gz")
                {
                    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                    using var reader = new StreamReader(gzipStream);
                    
                    // Read first non-empty line
                    while ((firstLine = reader.ReadLine()) != null)
                    {
                        firstLine = firstLine.Trim();
                        if (!string.IsNullOrWhiteSpace(firstLine))
                            break;
                    }
                }
                else
                {
                    using var reader = new StreamReader(filePath);
                    
                    // Read first non-empty line
                    while ((firstLine = reader.ReadLine()) != null)
                    {
                        firstLine = firstLine.Trim();
                        if (!string.IsNullOrWhiteSpace(firstLine))
                            break;
                    }
                }

                if (string.IsNullOrWhiteSpace(firstLine))
                    return FileFormat.Json; // Default for empty files

                // Check if it's JSON by looking for JSON markers
                if (firstLine.StartsWith("{") || firstLine.StartsWith("["))
                    return FileFormat.Json;

                // Check if it looks like CSV (contains commas or is a header row)
                if (firstLine.Contains(",") || firstLine.Contains("\t"))
                    return FileFormat.Csv;

                // Default to JSON if we can't determine
                return FileFormat.Json;
            }
            catch
            {
                // If we can't read the file, default to JSON
                return FileFormat.Json;
            }
        }

        private List<T> ParseCsvContent(string csvContent, CsvOptions options)
        {
            var result = new List<T>();

            using (var reader = new StringReader(csvContent))
            using (var csv = new CsvReader(reader, this.GetCsvConfiguration(options)))
            {
                // Configure date format
                csv.Context.TypeConverterOptionsCache.GetOptions<DateTime>().Formats = new[] { options.DateFormat };
                csv.Context.TypeConverterOptionsCache.GetOptions<DateTime?>().Formats = new[] { options.DateFormat };

                // Read all records
                result = csv.GetRecords<T>().ToList();
            }

            return result;
        }

        private CsvConfiguration GetCsvConfiguration(CsvOptions options)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = options.Delimiter.ToString(),
                HasHeaderRecord = options.HasHeaders,
                Quote = options.QuoteCharacter,
                TrimOptions = options.TrimFields ? TrimOptions.Trim : TrimOptions.None,
                IgnoreBlankLines = options.SkipEmptyRows,
                MissingFieldFound = null, // Don't throw on missing fields
                HeaderValidated = null,   // Don't validate headers
                BadDataFound = null       // Don't throw on bad data
            };

            return config;
        }

        private async Task WriteImportAuditAsync(BulkImportResult result, CancellationToken cancellationToken)
        {
            // Implementation would write to audit log
            await Task.CompletedTask;
        }

        private string ApplyExportModeFilters(string baseWhereClause, BulkExportOptions options)
        {
            switch (options.Mode)
            {
                case ExportMode.Incremental:
                    if (options.IncrementalFromDate.HasValue)
                    {
                        var dateStr = options.IncrementalFromDate.Value.ToString("yyyy-MM-dd HH:mm:ss");
                        return $"({baseWhereClause}) AND LastWriteTime > '{dateStr}'";
                    }
                    break;

                case ExportMode.Archive:
                    if (options.ArchiveOlderThan.HasValue)
                    {
                        var cutoffDate = DateTime.UtcNow.Subtract(options.ArchiveOlderThan.Value);
                        var dateStr = cutoffDate.ToString("yyyy-MM-dd HH:mm:ss");
                        return $"({baseWhereClause}) AND LastWriteTime < '{dateStr}'";
                    }
                    break;
            }

            return baseWhereClause;
        }

        private async Task<long> GetExportCountAsync(
            SQLiteConnection connection,
            string whereClause,
            Dictionary<string, object> parameters,
            BulkExportOptions options,
            CancellationToken cancellationToken)
        {
            string countSql;

            if (this.Mapper.EnableSoftDelete && options.IncludeAllVersions)
            {
                // Count all versions
                countSql = $"SELECT COUNT(*) FROM {this.Mapper.TableName} WHERE {whereClause}";
            }
            else
            {
                // Count only latest versions
                countSql = $@"
WITH LatestVersions AS (
    SELECT {this.Mapper.GetPrimaryKeyColumn()}, MAX(Version) as MaxVersion
    FROM {this.Mapper.TableName}
    WHERE {whereClause}
    GROUP BY {this.Mapper.GetPrimaryKeyColumn()}
)
SELECT COUNT(*) FROM LatestVersions";
            }

            using var countCmd = this.CreateCommand(countSql, connection);
            foreach (var param in parameters)
            {
                countCmd.Parameters.AddWithValue($"{param.Key}", param.Value ?? DBNull.Value);
            }

            return Convert.ToInt64(await countCmd.ExecuteScalarAsync(cancellationToken));
        }

        private async Task<List<T>> ExportEntitiesAsync(
            SQLiteConnection connection,
            string whereClause,
            Dictionary<string, object> parameters,
            BulkExportOptions options,
            IProgress<BulkOperationProgress> progress,
            long totalCount,
            DateTime startTime,
            CancellationToken cancellationToken)
        {
            var exportedEntities = new List<T>();
            string sql;

            if (this.Mapper.EnableSoftDelete && options.IncludeAllVersions)
            {
                // Export all versions
                sql = $@"
                SELECT {string.Join(", ", this.Mapper.GetSelectColumns())}
                FROM {this.Mapper.TableName}
                WHERE {whereClause}
                ORDER BY {this.Mapper.GetPrimaryKeyColumn()}, Version";
            }
            else
            {
                // Export only latest versions
                sql = $@"
                WITH LatestVersions AS (
                    SELECT *, ROW_NUMBER() OVER (PARTITION BY {this.Mapper.GetPrimaryKeyColumn()} ORDER BY Version DESC) as rn
                    FROM {this.Mapper.TableName}
                    WHERE {whereClause}
                )
                SELECT {string.Join(", ", this.Mapper.GetSelectColumns().Select(c => $"lv.{c}"))}
                FROM LatestVersions lv
                WHERE lv.rn = 1
                ORDER BY lv.{this.Mapper.GetPrimaryKeyColumn()}";
            }

            await using var command = this.CreateCommand(sql, connection);
            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue($"{param.Key}", param.Value ?? DBNull.Value);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var processedCount = 0;
            var batch = new List<T>();

            while (await reader.ReadAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entity = this.Mapper.MapFromReader(reader);
                if (entity != null)
                {
                    batch.Add(entity);
                    processedCount++;

                    // Process in batches to manage memory
                    if (batch.Count >= options.BatchSize)
                    {
                        exportedEntities.AddRange(batch);
                        batch.Clear();

                        // Report progress
                        if (progress != null)
                        {
                            var progressInfo = new BulkOperationProgress
                            {
                                ProcessedCount = processedCount,
                                TotalCount = totalCount,
                                ElapsedTime = DateTime.UtcNow - startTime,
                                CurrentOperation = $"Exporting entities ({processedCount}/{totalCount})"
                            };
                            progress.Report(progressInfo);
                            PersistenceLogger.BulkOperationProgress((int)progressInfo.PercentComplete, processedCount, totalCount);
                        }
                    }
                }
            }

            // Add remaining entities
            if (batch.Count > 0)
            {
                exportedEntities.AddRange(batch);

                // Report progress
                if (progress != null)
                {
                    var progressInfo = new BulkOperationProgress
                    {
                        ProcessedCount = processedCount,
                        TotalCount = totalCount,
                        ElapsedTime = DateTime.UtcNow - startTime,
                        CurrentOperation = $"Exporting entities ({processedCount}/{totalCount})"
                    };
                    progress.Report(progressInfo);
                    PersistenceLogger.BulkOperationProgress((int)progressInfo.PercentComplete, processedCount, totalCount);
                }
            }

            return exportedEntities;
        }

        private async Task WriteExportFilesAsync(
            List<T> entities,
            BulkExportOptions options,
            BulkExportResult<T> result,
            CancellationToken cancellationToken)
        {
            // Use provided prefix or default to entity type name
            var filePrefix = !string.IsNullOrEmpty(options.FileNamePrefix)
                ? options.FileNamePrefix
                : typeof(T).Name;
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

            // Write metadata file - use consistent naming pattern
            var metadataFileName = $"{filePrefix}_{timestamp}_metadata.json";
            var metadataPath = Path.Combine(options.ExportFolder, metadataFileName);
            await metadataPath.WriteAllTextAsync(SystemJsonSerializer.Serialize(result.Metadata), cancellationToken);
            result.ExportedFiles.Add(metadataPath);

            // Calculate statistics
            var statistics = new ExportStatistics
            {
                TotalEntitiesProcessed = entities.Count
            };

            if (this.Mapper.EnableSoftDelete)
            {
                statistics.DeletedEntitiesIncluded = entities.Count(e =>
                {
                    var isDeletedProp = typeof(T).GetProperty("IsDeleted");
                    return isDeletedProp != null && (bool)isDeletedProp.GetValue(e);
                });

                if (options.IncludeAllVersions)
                {
                    statistics.TotalVersionsExported = entities.Count;
                }
            }

            // Write data files (partition if needed)
            var fileIndex = 0;
            var currentBatch = new List<T>();
            var manifest = new ExportManifest
            {
                Metadata = result.Metadata,
                Statistics = statistics
            };

            foreach (var entity in entities)
            {
                currentBatch.Add(entity);

                if (currentBatch.Count >= options.BatchSize)
                {
                    var fileInfo = await this.WriteDataFileAsync(currentBatch, options, filePrefix, timestamp, fileIndex++, cancellationToken);
                    manifest.DataFiles.Add(fileInfo);
                    result.ExportedFiles.Add(Path.Combine(options.ExportFolder, fileInfo.FileName));
                    statistics.TotalFileSizeBytes += fileInfo.FileSizeBytes;
                    currentBatch.Clear();
                }
            }

            // Write remaining entities
            if (currentBatch.Count > 0)
            {
                var fileInfo = await this.WriteDataFileAsync(currentBatch, options, filePrefix, timestamp, fileIndex, cancellationToken);
                manifest.DataFiles.Add(fileInfo);
                result.ExportedFiles.Add(Path.Combine(options.ExportFolder, fileInfo.FileName));
                statistics.TotalFileSizeBytes += fileInfo.FileSizeBytes;
            }

            // Calculate compression ratio if compression was used
            if (options.CompressOutput)
            {
                var uncompressedSize = entities.Sum(this.EstimateEntitySize);
                statistics.CompressionRatio = uncompressedSize > 0
                    ? (double)statistics.TotalFileSizeBytes / uncompressedSize
                    : 1.0;
            }

            // Write manifest file
            var manifestPath = Path.Combine(options.ExportFolder, $"{filePrefix}_{timestamp}_manifest.json");
            await manifestPath.WriteAllTextAsync(SystemJsonSerializer.Serialize(manifest), cancellationToken);
            result.ManifestPath = manifestPath;
        }

        private async Task<ExportFileInfo> WriteDataFileAsync(
            List<T> entities,
            BulkExportOptions options,
            string filePrefix,
            string timestamp,
            int fileIndex,
            CancellationToken cancellationToken)
        {
            // Determine file extension based on format
            var extension = options.FileFormat == FileFormat.Csv ? ".csv" : ".json";
            var fileName = $"{filePrefix}_{timestamp}_{fileIndex:D4}{extension}";

            if (options.CompressOutput)
            {
                fileName += ".gz";
            }

            var filePath = Path.Combine(options.ExportFolder, fileName);

            // Serialize based on format
            string content;
            byte[] contentBytes;

            if (options.FileFormat == FileFormat.Csv)
            {
                content = this.SerializeEntitiesToCsv(entities, options.CsvOptions);
            }
            else
            {
                content = SystemJsonSerializer.Serialize(entities, new SystemJson.JsonSerializerOptions { WriteIndented = true });
            }

            contentBytes = Encoding.UTF8.GetBytes(content);

            if (options.CompressOutput)
            {
                using var fileStream = new FileStream(filePath, FileMode.Create);
                using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
                await gzipStream.WriteAsync(contentBytes, 0, contentBytes.Length, cancellationToken);
            }
            else
            {
                await filePath.WriteAllTextAsync(content, cancellationToken);
            }

            var fileInfo = new FileInfo(filePath);
            var checksum = await filePath.GetFileHashAsync();

            return new ExportFileInfo
            {
                FileName = fileName,
                FileSizeBytes = fileInfo.Length,
                EntityCount = entities.Count,
                Checksum = checksum,
                IsCompressed = options.CompressOutput
            };
        }

        private string SerializeEntitiesToCsv<TEntity>(List<TEntity> entities, CsvOptions options) where TEntity : class
        {
            if (entities == null || entities.Count == 0)
                return string.Empty;

            using (var writer = new StringWriter())
            using (var csv = new CsvWriter(writer, this.GetCsvConfiguration(options)))
            {
                // Configure date format
                csv.Context.TypeConverterOptionsCache.GetOptions<DateTime>().Formats = new[] { options.DateFormat };
                csv.Context.TypeConverterOptionsCache.GetOptions<DateTime?>().Formats = new[] { options.DateFormat };

                // Write all records
                csv.WriteRecords(entities);
                csv.Flush();

                return writer.ToString();
            }
        }

        private async Task MarkEntitiesAsExportedAsync(
            SQLiteConnection connection,
            string whereClause,
            Dictionary<string, object> parameters,
            CancellationToken cancellationToken)
        {
            // Add ExportedDate column if it doesn't exist
            var checkColumnSql = $@"
                SELECT COUNT(*)
                FROM pragma_table_info('{this.Mapper.TableName}')
                WHERE name = 'ExportedDate'";

            using var checkCmd = this.CreateCommand(checkColumnSql, connection);
            var columnExists = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(cancellationToken)) > 0;

            if (!columnExists)
            {
                var addColumnSql = $"ALTER TABLE {this.Mapper.TableName} ADD COLUMN ExportedDate TEXT";
                using var addCmd = this.CreateCommand(addColumnSql, connection);
                await addCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Update entities with export date
            var updateSql = $@"
                UPDATE {this.Mapper.TableName}
                SET ExportedDate = @exportDate
                WHERE {whereClause}";

            using var updateCmd = this.CreateCommand(updateSql, connection);
            updateCmd.Parameters.AddWithValue("@exportDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

            foreach (var param in parameters)
            {
                updateCmd.Parameters.AddWithValue($"{param.Key}", param.Value ?? DBNull.Value);
            }

            await updateCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private string BuildPurgeWhereClause(PurgeOptions options)
        {
            var clauses = new List<string>();

            // Handle PurgeExpired strategy
            if (options.Strategy == PurgeStrategy.PurgeExpired)
            {
                if (!this.Mapper.EnableExpiry)
                {
                    throw new InvalidOperationException("PurgeExpired strategy requires EnableExpiry to be true on the entity mapper.");
                }

                // Query for expired entities (AbsoluteExpiration < NOW and not archived)
                clauses.Add($"datetime(AbsoluteExpiration) < datetime('{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}')");

                // Only purge non-archived entities if archive is enabled
                if (this.Mapper.EnableArchive)
                {
                    clauses.Add("IsArchived = 0");
                }
            }
            else
            {
                // Age threshold
                if (options.AgeThreshold.HasValue)
                {
                    var cutoffDate = DateTime.UtcNow.Subtract(options.AgeThreshold.Value);
                    clauses.Add($"LastWriteTime < '{cutoffDate:yyyy-MM-dd HH:mm:ss}'");
                }
                else if (options.CutoffDate.HasValue)
                {
                    clauses.Add($"LastWriteTime < '{options.CutoffDate.Value:yyyy-MM-dd HH:mm:ss}'");
                }

                // Soft delete strategy
                if (this.Mapper.EnableSoftDelete)
                {
                    switch (options.Strategy)
                    {
                        case PurgeStrategy.PurgeDeletedOnly:
                            clauses.Add("IsDeleted = 1");
                            break;
                            // Other strategies handled in execution
                    }
                }
            }

            return clauses.Count > 0 ? string.Join(" AND ", clauses) : "1=1";
        }

        private string CombineWhereClauses(string clause1, string clause2)
        {
            if (string.IsNullOrEmpty(clause1) || clause1 == "1=1")
                return clause2;
            if (string.IsNullOrEmpty(clause2) || clause2 == "1=1")
                return clause1;
            return $"({clause1}) AND ({clause2})";
        }

        private async Task<PurgePreview> GeneratePurgePreviewAsync(
            SQLiteConnection connection,
            string whereClause,
            Dictionary<string, object> parameters,
            PurgeOptions options,
            CancellationToken cancellationToken)
        {
            var preview = new PurgePreview();

            // Count affected entities
            if (this.Mapper.EnableSoftDelete)
            {
                // For soft delete, need to analyze version chains
                var countSql = $@"
                    WITH AffectedEntities AS (
                        SELECT DISTINCT {this.Mapper.GetPrimaryKeyColumn()}
                        FROM {this.Mapper.TableName}
                        WHERE {whereClause}
                    )
                    SELECT
                        (SELECT COUNT(*) FROM AffectedEntities) as EntityCount,
                        (SELECT COUNT(*) FROM {this.Mapper.TableName} t
                         WHERE EXISTS (SELECT 1 FROM AffectedEntities a
                                      WHERE a.{this.Mapper.GetPrimaryKeyColumn()} = t.{this.Mapper.GetPrimaryKeyColumn()})) as VersionCount";

                using var countCmd = this.CreateCommand(countSql, connection);
                foreach (var param in parameters)
                {
                    countCmd.Parameters.AddWithValue($"{param.Key}", param.Value ?? DBNull.Value);
                }

                using var reader = await countCmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    preview.AffectedEntityCount = reader.GetInt64(0);
                    preview.AffectedVersionCount = reader.GetInt64(1);
                }
            }
            else
            {
                // Simple count for non-versioned
                var countSql = $"SELECT COUNT(*) FROM {this.Mapper.TableName} WHERE {whereClause}";
                using var countCmd = this.CreateCommand(countSql, connection);
                foreach (var param in parameters)
                {
                    countCmd.Parameters.AddWithValue($"{param.Key}", param.Value ?? DBNull.Value);
                }

                preview.AffectedEntityCount = Convert.ToInt64(await countCmd.ExecuteScalarAsync(cancellationToken));
                preview.AffectedVersionCount = preview.AffectedEntityCount;
            }

            // Get sample entities if requested
            if (options.IncludeSampleDataInPreview && preview.AffectedEntityCount > 0)
            {
                var sampleSql = $@"
                    SELECT {string.Join(", ", this.Mapper.GetSelectColumns())}
                    FROM {this.Mapper.TableName}
                    WHERE {whereClause}
                    LIMIT {options.MaxPreviewSamples}";

                using var sampleCmd = this.CreateCommand(sampleSql, connection);
                foreach (var param in parameters)
                {
                    sampleCmd.Parameters.AddWithValue($"{param.Key}", param.Value ?? DBNull.Value);
                }

                using var sampleReader = await sampleCmd.ExecuteReaderAsync(cancellationToken);
                while (await sampleReader.ReadAsync(cancellationToken))
                {
                    var entity = this.Mapper.MapFromReader(sampleReader);
                    if (entity != null)
                    {
                        var sample = new PurgeSampleEntity
                        {
                            EntityKey = this.Mapper.SerializeKey(entity.Id),
                            EntityType = typeof(T).Name,
                            CreatedTime = entity.CreatedTime,
                            LastWriteTime = entity.LastWriteTime,
                            Version = entity.Version,
                            SizeBytes = this.EstimateEntitySize(entity),
                            PurgeReason = this.DeterminePurgeReason(entity, options)
                        };

                        if (this.Mapper.EnableSoftDelete && entity is IVersionedEntity<TKey> versionedEntity)
                        {
                            sample.IsDeleted = versionedEntity.IsDeleted;
                        }

                        preview.SampleEntities.Add(sample);
                    }
                }
            }

            // Estimate space to reclaim
            preview.EstimatedSpaceToReclaim = preview.AffectedVersionCount * 1024; // Rough estimate

            return preview;
        }

        private string DeterminePurgeReason(T entity, PurgeOptions options)
        {
            // Check if purging expired entities
            if (options.Strategy == PurgeStrategy.PurgeExpired)
            {
                if (this.Mapper.EnableExpiry)
                {
                    // Check if entity has AbsoluteExpiration property
                    var expirationProp = typeof(T).GetProperty("AbsoluteExpiration");
                    if (expirationProp != null)
                    {
                        var expiration = expirationProp.GetValue(entity);
                        if (expiration is DateTime expirationDate)
                        {
                            return $"Expired on {expirationDate:yyyy-MM-dd HH:mm:ss}";
                        }
                        else if (expiration is DateTimeOffset expirationOffset)
                        {
                            return $"Expired on {expirationOffset:yyyy-MM-dd HH:mm:ss}";
                        }
                    }
                }
                return "Entity has expired";
            }

            if (options.AgeThreshold.HasValue &&
                entity.LastWriteTime < DateTime.UtcNow.Subtract(options.AgeThreshold.Value))
            {
                return $"Older than {options.AgeThreshold.Value.TotalDays} days";
            }

            if (options.CutoffDate.HasValue && entity.LastWriteTime < options.CutoffDate.Value)
            {
                return $"Before cutoff date {options.CutoffDate.Value:yyyy-MM-dd}";
            }

            if (this.Mapper.EnableSoftDelete && entity is IVersionedEntity<TKey> versionedEntity && versionedEntity.IsDeleted)
            {
                return "Entity is marked as deleted";
            }

            return "Matches purge criteria";
        }

        private async Task<BackupResult> CreateBackupBeforePurgeAsync(
            string whereClause,
            Dictionary<string, object> parameters,
            PurgeOptions options,
            IProgress<BulkOperationProgress> progress,
            CancellationToken cancellationToken)
        {
            var backupResult = new BackupResult();
            var backupStart = DateTime.UtcNow;

            try
            {
                // Create export options for backup
                var exportOptions = new BulkExportOptions
                {
                    Mode = ExportMode.Full,
                    ExportFolder = options.BackupPath ?? Path.Combine(Path.GetTempPath(), $"purge_backup_{DateTime.UtcNow:yyyyMMddHHmmss}"),
                    IncludeDeleted = true,
                    IncludeAllVersions = true,
                    IncludeExpired = true,
                    CompressOutput = true
                };

                // Build predicate from where clause (simplified - in real implementation would need proper conversion)
                Expression<Func<T, bool>> exportPredicate = null;
                if (!string.IsNullOrEmpty(whereClause) && whereClause != "1=1")
                {
                    // This is simplified - would need proper SQL to expression conversion
                    exportPredicate = entity => true;
                }

                var exportResult = await this.BulkExportAsync(exportPredicate, exportOptions, progress, cancellationToken);

                backupResult.Success = true;
                backupResult.BackupPath = exportOptions.ExportFolder;
                backupResult.EntitiesBackedUp = exportResult.ExportedCount;
                backupResult.BackupSizeBytes = exportResult.ExportedFiles
                    .Select(f => new FileInfo(f).Length)
                    .Sum();
                backupResult.BackupDuration = DateTime.UtcNow - backupStart;
            }
            catch (Exception ex)
            {
                backupResult.Success = false;
                backupResult.Error = $"Backup failed: {ex.Message}";
                backupResult.BackupDuration = DateTime.UtcNow - backupStart;
            }

            return backupResult;
        }

        private async Task<PurgeExecutionResult> ExecutePurgeAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            string whereClause,
            Dictionary<string, object> parameters,
            PurgeOptions options,
            IProgress<BulkOperationProgress> progress,
            CancellationToken cancellationToken)
        {
            var result = new PurgeExecutionResult
            {
                Statistics = new PurgeStatistics()
            };
            var deleteStart = DateTime.UtcNow;

            // PurgeExpired works regardless of soft delete setting
            if (options.Strategy == PurgeStrategy.PurgeExpired)
            {
                // For expired entities, delete all versions regardless of soft delete
                var deleteSql = $"DELETE FROM {this.Mapper.TableName} WHERE {whereClause}";
                using var deleteCmd = this.CreateCommand(deleteSql, connection, transaction);

                foreach (var param in parameters)
                {
                    deleteCmd.Parameters.AddWithValue($"{param.Key}", param.Value ?? DBNull.Value);
                }

                var rowsDeleted = await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
                result.EntitiesPurged = rowsDeleted;
                result.VersionsPurged = rowsDeleted;

                // Track expired entities separately
                result.Statistics.StatsByReason["Expired"] = rowsDeleted;
            }
            else if (this.Mapper.EnableSoftDelete)
            {
                // Complex purge for soft-delete entities
                result = await this.ExecuteSoftDeletePurgeAsync(
                    connection,
                    transaction,
                    whereClause,
                    parameters,
                    options,
                    progress,
                    cancellationToken);
            }
            else
            {
                // Simple delete for non-versioned entities
                var deleteSql = $"DELETE FROM {this.Mapper.TableName} WHERE {whereClause}";
                using var deleteCmd = this.CreateCommand(deleteSql, connection, transaction);

                foreach (var param in parameters)
                {
                    deleteCmd.Parameters.AddWithValue($"{param.Key}", param.Value ?? DBNull.Value);
                }

                var rowsDeleted = await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
                result.EntitiesPurged = rowsDeleted;
                result.VersionsPurged = rowsDeleted;
            }

            // Remove orphaned list mappings
            if (this.Mapper.SyncWithList)
            {
                var listCleanupSql = $@"
                    DELETE FROM EntryListMapping
                    WHERE EntityKey NOT IN (
                        SELECT DISTINCT {this.Mapper.GetPrimaryKeyColumn()}
                        FROM {this.Mapper.TableName}
                    )";

                using var listCmd = this.CreateCommand(listCleanupSql, connection, transaction);
                result.Statistics.ListMappingsRemoved = await listCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            result.Statistics.DeletionTime = DateTime.UtcNow - deleteStart;
            result.SpaceReclaimed = result.VersionsPurged * 1024; // Rough estimate

            return result;
        }

        private async Task<PurgeExecutionResult> ExecuteSoftDeletePurgeAsync(
            SQLiteConnection connection,
            SQLiteTransaction transaction,
            string whereClause,
            Dictionary<string, object> parameters,
            PurgeOptions options,
            IProgress<BulkOperationProgress> progress,
            CancellationToken cancellationToken)
        {
            var result = new PurgeExecutionResult
            {
                Statistics = new PurgeStatistics()
            };

            // Identify entities to purge based on strategy
            string deleteSql;

            switch (options.Strategy)
            {
                case PurgeStrategy.PreserveActiveVersions:
                    // Delete all versions if newest is deleted, preserve if active
                    deleteSql = $@"
                        DELETE FROM {this.Mapper.TableName} t1
                        WHERE {whereClause}
                        AND (
                            -- Entity's newest version is deleted
                            EXISTS (
                                SELECT 1 FROM {this.Mapper.TableName} t2
                                WHERE t2.{this.Mapper.GetPrimaryKeyColumn()} = t1.{this.Mapper.GetPrimaryKeyColumn()}
                                AND t2.Version = (
                                    SELECT MAX(Version) FROM {this.Mapper.TableName} t3
                                    WHERE t3.{this.Mapper.GetPrimaryKeyColumn()} = t1.{this.Mapper.GetPrimaryKeyColumn()}
                                )
                                AND t2.IsDeleted = 1
                            )
                            OR
                            -- Old versions of active entities
                            (
                                t1.Version < (
                                    SELECT MAX(Version) FROM {this.Mapper.TableName} t4
                                    WHERE t4.{this.Mapper.GetPrimaryKeyColumn()} = t1.{this.Mapper.GetPrimaryKeyColumn()}
                                )
                                AND NOT EXISTS (
                                    SELECT 1 FROM {this.Mapper.TableName} t5
                                    WHERE t5.{this.Mapper.GetPrimaryKeyColumn()} = t1.{this.Mapper.GetPrimaryKeyColumn()}
                                    AND t5.Version = (
                                        SELECT MAX(Version) FROM {this.Mapper.TableName} t6
                                        WHERE t6.{this.Mapper.GetPrimaryKeyColumn()} = t1.{this.Mapper.GetPrimaryKeyColumn()}
                                    )
                                    AND t5.IsDeleted = 1
                                )
                            )
                        )";
                    break;

                case PurgeStrategy.PurgeAllOldVersions:
                    // Delete all matching versions
                    deleteSql = $"DELETE FROM {this.Mapper.TableName} WHERE {whereClause}";
                    break;

                case PurgeStrategy.PurgeDeletedOnly:
                    // Already handled in where clause
                    deleteSql = $"DELETE FROM {this.Mapper.TableName} WHERE {whereClause}";
                    break;

                case PurgeStrategy.PurgeExpired:
                    // Delete all expired entities regardless of version
                    // The where clause already contains the expiry condition
                    deleteSql = $"DELETE FROM {this.Mapper.TableName} WHERE {whereClause}";
                    break;

                default:
                    throw new NotSupportedException($"Purge strategy {options.Strategy} is not supported");
            }

            using var deleteCmd = this.CreateCommand(deleteSql, connection, transaction);
            foreach (var param in parameters)
            {
                deleteCmd.Parameters.AddWithValue($"{param.Key}", param.Value ?? DBNull.Value);
            }

            result.VersionsPurged = await deleteCmd.ExecuteNonQueryAsync(cancellationToken);

            // Count unique entities purged
            var entityCountSql = $@"
                SELECT COUNT(DISTINCT {this.Mapper.GetPrimaryKeyColumn()})
                FROM {this.Mapper.TableName}
                WHERE {whereClause}";

            using var countCmd = this.CreateCommand(entityCountSql, connection, transaction);
            foreach (var param in parameters)
            {
                countCmd.Parameters.AddWithValue($"{param.Key}", param.Value ?? DBNull.Value);
            }

            // This count is before deletion, so we need to adjust
            var beforeCount = Convert.ToInt64(await countCmd.ExecuteScalarAsync(cancellationToken));
            result.EntitiesPurged = beforeCount; // Approximate

            return result;
        }

        private async Task OptimizeStorageAsync(SQLiteConnection connection, CancellationToken cancellationToken)
        {
            // Rebuild indexes
            using var rebuildCmd = this.CreateCommand($"REINDEX {this.Mapper.TableName}", connection);
            await rebuildCmd.ExecuteNonQueryAsync(cancellationToken);

            // Reclaim space
            using var vacuumCmd = this.CreateCommand("VACUUM", connection);
            await vacuumCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task WritePurgeAuditAsync(PurgeResult result, CancellationToken cancellationToken)
        {
            // Implementation would write to audit log
            await Task.CompletedTask;
        }


        #endregion

        #endregion
    }
}
