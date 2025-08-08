// -----------------------------------------------------------------------
// <copyright file="SQLitePersistenceProvider.Batch.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite
{
    using System;
    using System.Collections.Generic;
    using System.Data.SQLite;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Errors;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Traces;

    public partial class SQLitePersistenceProvider<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {

        #region batch operations

        public async Task<IEnumerable<T>> CreateAsync(
            IEnumerable<T> entities,
            CallerInfo callerInfo,
            int? batchSize = null,
            CancellationToken cancellationToken = default)
        {
            if (entities == null)
            {
                return Enumerable.Empty<T>();
            }

            var entityList = entities.ToList();
            if (!entityList.Any())
            {
                return Enumerable.Empty<T>();
            }

            var stopwatch = Stopwatch.StartNew();
            PersistenceLogger.BatchOperationStart("CreateBatch", entityList.Count, null);

            var results = new List<T>();
            var errors = new List<Exception>();

            try
            {
                await using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                // Determine batch processing
                var effectiveBatchSize = batchSize ?? entityList.Count;
                var batches = entityList.Select((entity, index) => new { entity, index })
                    .GroupBy(x => x.index / effectiveBatchSize)
                    .Select(g => g.Select(x => x.entity).ToList())
                    .ToList();

                foreach (var batch in batches)
                {
                    await using var transaction = connection.BeginTransaction();
                    try
                    {
                        // Get next version for all entities in the batch (same version for all)
                        long newVersion = 1;
                        if (this.Mapper.EnableSoftDelete || typeof(IVersionedEntity<TKey>).IsAssignableFrom(typeof(T)))
                        {
                            await using var versionCommand = this.versionMapper.CreateGetNextVersionCommand();
                            versionCommand.Connection = connection;
                            versionCommand.Transaction = transaction;
                            newVersion = Convert.ToInt64(await versionCommand.ExecuteScalarAsync(cancellationToken));
                        }

                        var batchResults = new List<T>();

                        foreach (var entity in batch)
                        {
                            try
                            {
                                var keyString = this.Mapper.SerializeKey(entity.Id);
                                var versionEntity = this.Mapper.EnableSoftDelete ? entity as IVersionedEntity<TKey> : null;

                                // Step 1: Check if entity already exists
                                var checkExistsSql = $@"
SELECT {string.Join(",\n  ", this.Mapper.GetSelectColumns())}
FROM {this.Mapper.TableName}
WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key";
                                if (this.Mapper.EnableSoftDelete || versionEntity != null)
                                {
                                    checkExistsSql += @"
ORDER BY Version DESC
LIMIT 1";
                                }

                                await using var checkCommand = this.CreateCommand(checkExistsSql, connection, transaction);
                                checkCommand.Parameters.AddWithValue("@key", keyString);

                                await using var checkReader = await checkCommand.ExecuteReaderAsync(cancellationToken);
                                if (await checkReader.ReadAsync(cancellationToken))
                                {
                                    // Entity already exists
                                    var existingEntity = this.Mapper.MapFromReader(checkReader);
                                    if (versionEntity != null && existingEntity is IVersionedEntity<TKey> existingVersionedEntity && existingVersionedEntity.IsDeleted)
                                    {
                                        // Allow recreation of soft-deleted entity
                                        entity.Version = newVersion;
                                    }
                                    else
                                    {
                                        throw new EntityAlreadyExistsException(
                                            entity.Id.ToString(),
                                            $"Entity with key '{entity.Id}' already exists. Use UpdateAsync to modify existing entities.");
                                    }
                                }
                                else
                                {
                                    // New entity
                                    entity.Version = newVersion;
                                }

                                // Set tracking fields before insert
                                entity.CreatedTime = DateTimeOffset.UtcNow;
                                entity.LastWriteTime = entity.CreatedTime;
                                if (this.Mapper.EnableExpiry && entity is IExpirableEntity<TKey> expirable)
                                {
                                    // Only set AbsoluteExpiration if it's null or default
                                    if (expirable.AbsoluteExpiration == null || expirable.AbsoluteExpiration == default(DateTimeOffset))
                                    {
                                        expirable.AbsoluteExpiration = entity.CreatedTime + this.Mapper.ExpirySpan.Value;
                                    }
                                }

                                // Insert entity
                                var columns = this.Mapper.GetInsertColumns();
                                var parameters = columns.Select(c => $"@{c}").ToList();
                                var insertEntitySql = $@"
INSERT INTO {this.Mapper.TableName} ({string.Join(", ", columns)})
VALUES ({string.Join(", ", parameters)});";

                                await using var insertCommand = this.CreateCommand(insertEntitySql, connection, transaction);
                                this.Mapper.AddParameters(insertCommand, entity);
                                await insertCommand.ExecuteNonQueryAsync(cancellationToken);

                                // Retrieve the inserted entity
                                var selectSql = $@"
SELECT {string.Join(",\n  ", this.Mapper.GetSelectColumns())}
FROM {this.Mapper.TableName}
WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key
  AND Version = @version;";

                                await using var selectCommand = this.CreateCommand(selectSql, connection, transaction);
                                selectCommand.Parameters.AddWithValue("@key", keyString);
                                selectCommand.Parameters.AddWithValue("@version", entity.Version);

                                await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
                                if (await reader.ReadAsync(cancellationToken))
                                {
                                    var result = this.Mapper.MapFromReader(reader);
                                    batchResults.Add(result);
                                }
                                else
                                {
                                    throw new InvalidOperationException($"Failed to retrieve created entity with key '{entity.Id}'");
                                }
                            }
                            catch (Exception ex)
                            {
                                // Log error for this entity but continue with others in batch
                                errors.Add(new InvalidOperationException($"Failed to create entity with key '{entity.Id}': {ex.Message}", ex));
                                throw; // Re-throw to trigger transaction rollback
                            }
                        }

                        // Commit the batch transaction
                        transaction.Commit();
                        results.AddRange(batchResults);

                        // Write to audit trail if enabled (batch audit)
                        if (this.Mapper.EnableAuditTrail)
                        {
                            foreach (var result in batchResults)
                            {
                                try
                                {
                                    await this.WriteAuditRecordAsync(result, "CREATE", callerInfo, null, null, cancellationToken);
                                }
                                catch
                                {
                                    // Log but don't fail the operation if audit fails
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Rollback the current batch transaction
                        transaction.Rollback();
                        stopwatch.Stop();
                        PersistenceLogger.BatchOperationFailed("CreateBatch", null, stopwatch, ex);

                        // For batch operations, we continue with next batch even if one fails
                        // But we need to track which entities failed
                        errors.Add(ex);
                    }
                }

                stopwatch.Stop();
                PersistenceLogger.BatchOperationStop("CreateBatch", results.Count, null, stopwatch);

                // If any errors occurred, throw an aggregate exception
                if (errors.Any())
                {
                    throw new AggregateException($"Failed to create {errors.Count} entities in batch", errors);
                }

                return results;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PersistenceLogger.BatchOperationFailed("CreateBatch", null, stopwatch, ex);
                throw;
            }
        }

        public async Task<IEnumerable<T>> GetAllAsync(
            CallerInfo callerInfo,
            bool includeAllVersions = false,
            bool includeDeleted = false,
            bool includeExpired = false,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            PersistenceLogger.GetStart("all", this.Mapper.TableName);

            try
            {
                var results = new List<T>();

                using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                // Use mapper's CreateCommand with SelectOptions for consistency
                var context = new CommandContext<T, TKey>
                {
                    SelectOptions = new SelectOptions
                    {
                        IncludeAllVersions = includeAllVersions,
                        IncludeDeleted = includeDeleted,
                        IncludeExpired = includeExpired
                        // No WHERE clause - we want all entities
                    },
                    CommandTimeout = this.configuration.CommandTimeout
                };

                using var command = this.Mapper.CreateCommand(DbOperationType.Select, context);
                command.Connection = connection;

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    var entity = this.Mapper.MapFromReader(reader);
                    if (entity != null)
                    {
                        results.Add(entity);
                    }
                }

                stopwatch.Stop();
                PersistenceLogger.GetStop("all", this.Mapper.TableName, stopwatch);

                // Write to audit trail if enabled (batch read operation)
                if (this.Mapper.EnableAuditTrail && results.Any())
                {
                    try
                    {
                        // Log a single audit entry for the GetAll operation
                        var firstEntity = results.First();
                        await this.WriteAuditRecordAsync(firstEntity, "READ_ALL", callerInfo, null, true, cancellationToken);
                    }
                    catch
                    {
                        // Log but don't fail the operation if audit fails
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PersistenceLogger.GetFailed("all", this.Mapper.TableName, stopwatch, ex);
                throw;
            }
        }

        public async Task<IEnumerable<T>> UpdateAsync(
            IEnumerable<T> entities,
            Func<T, T> updateFunc, CallerInfo callerInfo,
            int? batchSize = null,
            CancellationToken cancellationToken = default)
        {
            if (entities == null)
            {
                return Enumerable.Empty<T>();
            }

            if (updateFunc == null)
            {
                throw new ArgumentNullException(nameof(updateFunc));
            }

            var entityList = entities.ToList();
            if (!entityList.Any())
            {
                return Enumerable.Empty<T>();
            }

            var stopwatch = Stopwatch.StartNew();
            PersistenceLogger.BatchOperationStart("UpdateBatch", entityList.Count, null);

            var results = new List<T>();
            var errors = new List<Exception>();

            try
            {
                using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                // Determine batch processing
                var effectiveBatchSize = batchSize ?? entityList.Count;
                var batches = entityList.Select((entity, index) => new { entity, index })
                    .GroupBy(x => x.index / effectiveBatchSize)
                    .Select(g => g.Select(x => x.entity).ToList())
                    .ToList();

                foreach (var batch in batches)
                {
                    using var transaction = connection.BeginTransaction();
                    try
                    {
                        // Get next version for all entities in the batch (same version for all when soft delete is enabled)
                        long newVersion = 0;
                        if (this.Mapper.EnableSoftDelete || typeof(IVersionedEntity<TKey>).IsAssignableFrom(typeof(T)))
                        {
                            using var versionCommand = this.versionMapper.CreateGetNextVersionCommand();
                            versionCommand.Connection = connection;
                            versionCommand.Transaction = transaction;
                            newVersion = Convert.ToInt64(await versionCommand.ExecuteScalarAsync(cancellationToken));
                        }

                        var batchResults = new List<T>();

                        foreach (var entity in batch)
                        {
                            try
                            {
                                var keyString = this.Mapper.SerializeKey(entity.Id);
                                var versionedEntity = this.Mapper.EnableSoftDelete ? entity as IVersionedEntity<TKey> : null;

                                // Store original version for optimistic concurrency check
                                var originalVersion = entity.Version;

                                // Step 1: Get the current entity for validation and history tracking
                                T oldValue = null;
                                var selectOldSql = $@"
SELECT {string.Join(",\n  ", this.Mapper.GetSelectColumns())}
FROM {this.Mapper.TableName}
WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key";
                                if (versionedEntity != null)
                                {
                                    selectOldSql += @"
ORDER BY Version DESC
LIMIT 1";
                                }

                                using var selectOldCommand = this.CreateCommand(selectOldSql, connection, transaction);
                                selectOldCommand.Parameters.AddWithValue("@key", keyString);

                                using var oldReader = await selectOldCommand.ExecuteReaderAsync(cancellationToken);
                                if (await oldReader.ReadAsync(cancellationToken))
                                {
                                    oldValue = this.Mapper.MapFromReader(oldReader);
                                }

                                if (oldValue == null)
                                {
                                    throw new EntityNotFoundException(keyString, "Entity not found during update");
                                }

                                // Check if entity is deleted
                                if (oldValue is IVersionedEntity<TKey> oldVersionedEntity && oldVersionedEntity.IsDeleted)
                                {
                                    throw new InvalidOperationException($"Cannot update deleted entity with key '{entity.Id}'");
                                }

                                if (oldValue.Version != entity.Version)
                                {
                                    throw new ConcurrencyConflictException(
                                        keyString,
                                        oldValue.Version,
                                        entity.Version,
                                        "Version does not match during update");
                                }

                                // Apply the update function to get the updated entity
                                var updatedEntity = updateFunc(entity);
                                if (updatedEntity == null)
                                {
                                    throw new InvalidOperationException($"Update function returned null for entity with key '{entity.Id}'");
                                }

                                // Ensure the key hasn't changed
                                if (!updatedEntity.Id.Equals(entity.Id))
                                {
                                    throw new InvalidOperationException($"Entity key cannot be changed during update. Original: '{entity.Id}', Updated: '{updatedEntity.Id}'");
                                }

                                // Update version and tracking fields
                                if (versionedEntity != null)
                                {
                                    updatedEntity.Version = newVersion;
                                }
                                else
                                {
                                    updatedEntity.Version = originalVersion + 1;
                                }
                                updatedEntity.LastWriteTime = DateTime.UtcNow;

                                int rowsAffected;

                                if (versionedEntity == null)
                                {
                                    // In-place update for non-versioned entities
                                    var updateColumns = this.Mapper.GetUpdateColumns()
                                        .Select(c => $"{c} = @{c}")
                                        .ToList();

                                    var updateSql = $@"
UPDATE {this.Mapper.TableName}
SET {string.Join(", ", updateColumns)}
WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key
      AND Version = @originalVersion;
SELECT changes();";

                                    using var updateCommand = this.CreateCommand(updateSql, connection, transaction);
                                    this.Mapper.AddParameters(updateCommand, updatedEntity);
                                    updateCommand.Parameters.AddWithValue("@key", keyString);
                                    updateCommand.Parameters.AddWithValue("@originalVersion", originalVersion);
                                    rowsAffected = Convert.ToInt32(await updateCommand.ExecuteScalarAsync(cancellationToken));
                                }
                                else
                                {
                                    // Insert new version for soft-delete enabled entities
                                    var columns = this.Mapper.GetInsertColumns();
                                    var parameters = columns.Select(c => $"@{c}").ToList();
                                    var insertEntitySql = $@"
INSERT INTO {this.Mapper.TableName} ({string.Join(", ", columns)})
VALUES ({string.Join(", ", parameters)});
SELECT changes();";

                                    using var insertCommand = this.CreateCommand(insertEntitySql, connection, transaction);
                                    this.Mapper.AddParameters(insertCommand, updatedEntity);
                                    rowsAffected = Convert.ToInt32(await insertCommand.ExecuteScalarAsync(cancellationToken));
                                }

                                if (rowsAffected == 0)
                                {
                                    throw new EntityWriteException(
                                        keyString,
                                        $"Entity with key '{entity.Id}' has been modified by another process.");
                                }

                                batchResults.Add(updatedEntity);
                            }
                            catch (Exception ex)
                            {
                                // Log error for this entity but continue with transaction rollback
                                errors.Add(new InvalidOperationException($"Failed to update entity with key '{entity.Id}': {ex.Message}", ex));
                                throw; // Re-throw to trigger transaction rollback
                            }
                        }

                        // Commit the batch transaction
                        transaction.Commit();
                        results.AddRange(batchResults);

                        // Write to audit trail if enabled (batch audit)
                        if (this.Mapper.EnableAuditTrail)
                        {
                            for (int i = 0; i < batchResults.Count; i++)
                            {
                                try
                                {
                                    var oldEntity = batch[i]; // Original entity before update
                                    await this.WriteAuditRecordAsync(batchResults[i], "UPDATE", callerInfo, oldEntity, null, cancellationToken);
                                }
                                catch
                                {
                                    // Log but don't fail the operation if audit fails
                                }
                            }
                        }
                    }
                    catch (ConcurrencyException ex)
                    {
                        // Rollback the current batch transaction
                        transaction.Rollback();
                        stopwatch.Stop();
                        PersistenceLogger.UpdateConcurrencyConflict(ex.Message, this.Mapper.TableName);
                        errors.Add(ex);
                    }
                    catch (Exception ex)
                    {
                        // Rollback the current batch transaction
                        transaction.Rollback();
                        stopwatch.Stop();
                        PersistenceLogger.BatchOperationFailed("UpdateBatch", null, stopwatch, ex);
                        errors.Add(ex);
                    }
                }

                stopwatch.Stop();
                PersistenceLogger.BatchOperationStop("UpdateBatch", results.Count, null, stopwatch);

                // If any errors occurred, throw an aggregate exception
                if (errors.Any())
                {
                    throw new AggregateException($"Failed to update {errors.Count} entities in batch", errors);
                }

                return results;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PersistenceLogger.BatchOperationFailed("UpdateBatch", null, stopwatch, ex);
                throw;
            }
        }

        public async Task<int> DeleteAsync(
            IEnumerable<TKey> keys,
            CallerInfo callerInfo,
            int? batchSize = null,
            CancellationToken cancellationToken = default)
        {
            if (keys == null)
            {
                return 0;
            }

            var keyList = keys.ToList();
            if (!keyList.Any())
            {
                return 0;
            }

            var stopwatch = Stopwatch.StartNew();
            PersistenceLogger.BatchOperationStart("DeleteBatch", keyList.Count, null);

            var totalDeleted = 0;
            var errors = new List<Exception>();

            try
            {
                using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                // Determine batch processing
                var effectiveBatchSize = batchSize ?? keyList.Count;
                var batches = keyList.Select((key, index) => new { key, index })
                    .GroupBy(x => x.index / effectiveBatchSize)
                    .Select(g => g.Select(x => x.key).ToList())
                    .ToList();

                foreach (var batch in batches)
                {
                    using var transaction = connection.BeginTransaction();
                    try
                    {
                        // Get next version for all entities in the batch (same version for all when soft delete is enabled)
                        long newVersion = 0;
                        if (this.Mapper.EnableSoftDelete)
                        {
                            using var versionCommand = this.versionMapper.CreateGetNextVersionCommand();
                            versionCommand.Connection = connection;
                            versionCommand.Transaction = transaction;
                            newVersion = Convert.ToInt64(await versionCommand.ExecuteScalarAsync(cancellationToken));
                        }

                        var batchDeleted = 0;

                        foreach (var key in batch)
                        {
                            try
                            {
                                var keyString = this.Mapper.SerializeKey(key);

                                // Step 1: Check if entity exists and get the old value
                                T oldValue = null;
                                var selectOldSql = $@"
                                    SELECT {string.Join(",\n  ", this.Mapper.GetSelectColumns())}
                                    FROM {this.Mapper.TableName}
                                    WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key";
                                if (this.Mapper.EnableSoftDelete)
                                {
                                    selectOldSql += @"
ORDER BY Version DESC
LIMIT 1";
                                }

                                using var selectOldCommand = this.CreateCommand(selectOldSql, connection, transaction);
                                selectOldCommand.Parameters.AddWithValue("@key", keyString);

                                using var oldReader = await selectOldCommand.ExecuteReaderAsync(cancellationToken);
                                if (await oldReader.ReadAsync(cancellationToken))
                                {
                                    oldValue = this.Mapper.MapFromReader(oldReader);
                                }

                                if (this.Mapper.EnableSoftDelete)
                                {
                                    if (oldValue == null)
                                    {
                                        // Entity not found, skip without error
                                        continue;
                                    }

                                    if (oldValue is IVersionedEntity<TKey> versionedEntity && versionedEntity.IsDeleted)
                                    {
                                        // If already soft-deleted, skip
                                        continue;
                                    }

                                    // Insert new version with IsDeleted = true
                                    var deletedEntity = oldValue;
                                    var newValue = deletedEntity as IVersionedEntity<TKey>;
                                    if (newValue == null)
                                    {
                                        throw new InvalidOperationException($"Entity with key '{key}' does not implement IVersionedEntity for soft delete.");
                                    }

                                    newValue.IsDeleted = true;
                                    deletedEntity.Version = newVersion;
                                    deletedEntity.LastWriteTime = DateTime.UtcNow;

                                    var columns = this.Mapper.GetInsertColumns();
                                    var parameters = columns.Select(c => $"@{c}").ToList();
                                    var insertEntitySql = $@"
INSERT INTO {this.Mapper.TableName} ({string.Join(", ", columns)})
VALUES ({string.Join(", ", parameters)});";

                                    using var insertCommand = this.CreateCommand(insertEntitySql, connection, transaction);
                                    this.Mapper.AddParameters(insertCommand, deletedEntity);
                                    await insertCommand.ExecuteNonQueryAsync(cancellationToken);
                                    batchDeleted++;
                                }
                                else
                                {
                                    // Hard delete
                                    if (oldValue == null)
                                    {
                                        // Entity not found, skip without error
                                        continue;
                                    }

                                    var sql = $@"
DELETE FROM {this.Mapper.TableName}
WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key;
SELECT changes();";
                                    using var command = this.CreateCommand(sql, connection, transaction);
                                    command.Parameters.AddWithValue("@key", keyString);
                                    var affected = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
                                    if (affected > 0)
                                    {
                                        batchDeleted++;
                                    }
                                }

                                // Write to audit trail if enabled
                                if (this.Mapper.EnableAuditTrail && oldValue != null)
                                {
                                    try
                                    {
                                        await this.WriteAuditRecordAsync(oldValue, "DELETE", callerInfo, null, null, cancellationToken);
                                    }
                                    catch
                                    {
                                        // Log but don't fail the operation if audit fails
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // Log error for this entity but continue with transaction rollback
                                errors.Add(new InvalidOperationException($"Failed to delete entity with key '{key}': {ex.Message}", ex));
                                throw; // Re-throw to trigger transaction rollback
                            }
                        }

                        // Commit the batch transaction
                        transaction.Commit();
                        totalDeleted += batchDeleted;
                    }
                    catch (Exception ex)
                    {
                        // Rollback the current batch transaction
                        transaction.Rollback();
                        stopwatch.Stop();
                        PersistenceLogger.BatchOperationFailed("DeleteBatch", null, stopwatch, ex);
                        errors.Add(ex);
                    }
                }

                stopwatch.Stop();
                PersistenceLogger.BatchOperationStop("DeleteBatch", totalDeleted, null, stopwatch);

                // If any errors occurred, throw an aggregate exception
                if (errors.Any())
                {
                    throw new AggregateException($"Failed to delete {errors.Count} entities in batch", errors);
                }

                return totalDeleted;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PersistenceLogger.BatchOperationFailed("DeleteBatch", null, stopwatch, ex);
                throw;
            }
        }

        #endregion

    }
}
