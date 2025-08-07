// -----------------------------------------------------------------------
// <copyright file="SQLitePersistenceProvider.List.cs" company="Microsoft Corp.">
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
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Entities;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Errors;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Traces;

    public partial class SQLitePersistenceProvider<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        #region List operations

        public async Task<IEnumerable<T>> CreateListAsync(
            string listCacheKey,
            IEnumerable<T> entities,
            CallerInfo callerInfo,
            CancellationToken cancellationToken = default)
        {
            if (!this.Mapper.SyncWithList)
            {
                throw new NotSupportedException("List operations require SyncWithList to be enabled for this entity type.");
            }

            if (string.IsNullOrEmpty(listCacheKey))
            {
                throw new ArgumentNullException(nameof(listCacheKey));
            }

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
            PersistenceLogger.BatchOperationStart("CreateList", entityList.Count, listCacheKey);

            try
            {
                var results = new List<T>();

                using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                using var transaction = connection.BeginTransaction();
                try
                {
                    // Step 1: Check if listCacheKey already exists in EntryListMapping
                    var checkListExistsSql = @"
                        SELECT COUNT(*) 
                        FROM EntryListMapping 
                        WHERE ListCacheKey = @listCacheKey";

                    using (var checkListCmd = this.CreateCommand(checkListExistsSql, connection, transaction))
                    {
                        checkListCmd.Parameters.AddWithValue("@listCacheKey", listCacheKey);
                        var count = Convert.ToInt64(await checkListCmd.ExecuteScalarAsync(cancellationToken));
                        if (count > 0)
                        {
                            throw new InvalidOperationException($"List with key '{listCacheKey}' already exists.");
                        }
                    }

                    // Step 2: Get next version for all entities in the list
                    long version = 1;
                    if (this.Mapper.EnableSoftDelete || typeof(IVersionedEntity<TKey>).IsAssignableFrom(typeof(T)))
                    {
                        using var versionCmd = this.versionMapper.CreateGetNextVersionCommand();
                        versionCmd.Connection = connection;
                        versionCmd.Transaction = transaction;
                        version = Convert.ToInt64(await versionCmd.ExecuteScalarAsync(cancellationToken));
                    }

                    var now = DateTime.UtcNow;

                    // Step 3: Process each entity
                    foreach (var entity in entityList)
                    {
                        var keyString = this.Mapper.SerializeKey(entity.Id);
                        var versionedEntity = this.Mapper.EnableSoftDelete ? entity as IVersionedEntity<TKey> : null;

                        // Check if entity already exists
                        var checkEntitySql = $@"
                            SELECT {string.Join(",\n  ", this.Mapper.GetSelectColumns())}
                            FROM {this.Mapper.TableName}
                            WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key";

                        if (this.Mapper.EnableSoftDelete || versionedEntity != null)
                        {
                            checkEntitySql += @"
                                ORDER BY Version DESC
                                LIMIT 1";
                        }

                        T existingEntity = null;
                        using (var checkCmd = this.CreateCommand(checkEntitySql, connection, transaction))
                        {
                            checkCmd.Parameters.AddWithValue("@key", keyString);
                            using var reader = await checkCmd.ExecuteReaderAsync(cancellationToken);
                            if (await reader.ReadAsync(cancellationToken))
                            {
                                existingEntity = this.Mapper.MapFromReader(reader);
                            }
                        }

                        // Determine if we need to create entity
                        if (existingEntity == null)
                        {
                            // Entity doesn't exist, create it
                        }
                        else if (versionedEntity != null && existingEntity is IVersionedEntity<TKey> existingVersioned && existingVersioned.IsDeleted)
                        {
                            // Entity exists but is soft-deleted, create new version with IsDeleted=false
                            versionedEntity.IsDeleted = false;
                        }
                        else
                        {
                            // Entity exists and is not deleted
                            throw new EntityAlreadyExistsException(
                                keyString,
                                $"Entity with key '{entity.Id}' already exists and is not deleted. Cannot add to list '{listCacheKey}'.");
                        }

                        // Set tracking fields
                        entity.Version = version;
                        entity.CreatedTime = DateTimeOffset.UtcNow;
                        entity.LastWriteTime = entity.CreatedTime;
                        if (this.Mapper.EnableExpiry && entity is IExpirableEntity<TKey> expirable)
                        {
                            // Only set AbsoluteExpiration if it's null or default
                            if (expirable.AbsoluteExpiration == default(DateTimeOffset))
                            {
                                expirable.AbsoluteExpiration = entity.CreatedTime + (this.Mapper.ExpirySpan ?? DefaultCacheExpiration);
                            }
                        }

                        // Insert entity
                        var columns = this.Mapper.GetInsertColumns();
                        var parameters = columns.Select(c => $"@{c}").ToList();
                        var insertEntitySql = $@"
                                INSERT INTO {this.Mapper.TableName} ({string.Join(", ", columns)})
                                VALUES ({string.Join(", ", parameters)});";

                        using var insertCmd = this.CreateCommand(insertEntitySql, connection, transaction);
                        this.Mapper.AddParameters(insertCmd, entity);
                        await insertCmd.ExecuteNonQueryAsync(cancellationToken);

                        // Step 4: Add to EntryListMapping
                        var listMapping = new EntryListMapping
                        {
                            ListCacheKey = listCacheKey,
                            EntryCacheKey = keyString,
                            Version = version,
                            CreatedTime = now,
                            LastWriteTime = now,
                            CallerFile = callerInfo?.CallerFilePath,
                            CallerMember = callerInfo?.CallerMemberName,
                            CallerLineNumber = callerInfo?.CallerLineNumber
                        };

                        var mappingContext = CommandContext<EntryListMapping, string>.ForInsert(listMapping);
                        mappingContext.CommandTimeout = this.configuration.CommandTimeout;
                        mappingContext.Transaction = transaction;
                        using var listMappingCmd = this.entryListMappingMapper.CreateCommand(DbOperationType.Insert, mappingContext);
                        listMappingCmd.Connection = connection;
                        listMappingCmd.ExecuteNonQuery();

                        results.Add(entity);
                    }

                    transaction.Commit();

                    // Write to audit trail if enabled
                    if (this.Mapper.EnableAuditTrail && results.Any())
                    {
                        try
                        {
                            foreach (var createdEntity in results)
                            {
                                await this.WriteAuditRecordAsync(createdEntity, "CREATE_LIST", callerInfo, null, null,
                                    cancellationToken);
                            }
                        }
                        catch
                        {
                            // Log but don't fail the operation if audit fails
                        }
                    }

                    stopwatch.Stop();
                    PersistenceLogger.BatchOperationStop("CreateList", results.Count, listCacheKey, stopwatch);
                    return results;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    stopwatch.Stop();
                    PersistenceLogger.BatchOperationFailed("CreateList", listCacheKey, stopwatch, ex);
                    throw;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PersistenceLogger.BatchOperationFailed("CreateList", listCacheKey, stopwatch, ex);
                throw;
            }
        }

        public async Task<IEnumerable<T>> GetListAsync(
            string listCacheKey,
            CallerInfo callerInfo,
            CancellationToken cancellationToken = default)
        {
            if (!this.Mapper.SyncWithList)
            {
                throw new NotSupportedException("List operations require SyncWithList to be enabled for this entity type.");
            }

            if (string.IsNullOrEmpty(listCacheKey))
            {
                throw new ArgumentNullException(nameof(listCacheKey));
            }

            var stopwatch = Stopwatch.StartNew();
            PersistenceLogger.BatchOperationStart("GetList", 0, listCacheKey);

            try
            {
                var results = new List<T>();

                using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                // Get all entity keys and versions from the list mapping
                var mappings = new List<(string EntryCacheKey, long Version)>();
                var listSql = @"
                    SELECT EntryCacheKey, Version
                    FROM EntryListMapping 
                    WHERE ListCacheKey = @listCacheKey
                    ORDER BY EntryCacheKey";

                using (var listCmd = this.CreateCommand(listSql, connection))
                {
                    listCmd.Parameters.AddWithValue("@listCacheKey", listCacheKey);
                    using var listReader = await listCmd.ExecuteReaderAsync(cancellationToken);
                    while (await listReader.ReadAsync(cancellationToken))
                    {
                        mappings.Add((listReader.GetString(0), listReader.GetInt64(1)));
                    }
                }

                if (!mappings.Any())
                {
                    return results; // Return empty list if no mappings found
                }

                // Process each entity
                foreach (var (entryCacheKey, mappingVersion) in mappings)
                {
                    var key = this.Mapper.DeserializeKey(entryCacheKey);

                    var sql = $@"
                        SELECT {string.Join(",\n  ", this.Mapper.GetSelectColumns())}
                        FROM {this.Mapper.TableName}
                        WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key";

                    if (this.Mapper.EnableSoftDelete || typeof(IVersionedEntity<TKey>).IsAssignableFrom(typeof(T)))
                    {
                        sql += @"
                            ORDER BY Version DESC
                            LIMIT 1";
                    }

                    T entity = null;
                    using (var command = this.CreateCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@key", entryCacheKey);
                        using var reader = await command.ExecuteReaderAsync(cancellationToken);
                        if (await reader.ReadAsync(cancellationToken))
                        {
                            entity = this.Mapper.MapFromReader(reader);
                        }
                    }

                    // Validate entity exists
                    if (entity == null)
                    {
                        throw new EntityNotFoundException(entryCacheKey,
                            $"Entity with key '{entryCacheKey}' not found in list '{listCacheKey}'");
                    }

                    // Check if entity is soft-deleted
                    if (entity is IVersionedEntity<TKey> versionedEntity)
                    {
                        if (versionedEntity.IsDeleted)
                        {
                            throw new EntityNotFoundException(entryCacheKey,
                                $"Entity with key '{entryCacheKey}' is deleted in list '{listCacheKey}'");
                        }
                    }

                    // Version consistency check
                    if (entity.Version > mappingVersion)
                    {
                        // Update mapping with newer version
                        using var transaction = connection.BeginTransaction();
                        try
                        {
                            var updateSql = @"
                                UPDATE EntryListMapping 
                                SET Version = @version, LastWriteTime = @lastWriteTime
                                WHERE ListCacheKey = @listCacheKey 
                                AND EntryCacheKey = @entryCacheKey";

                            using var updateCmd = this.CreateCommand(updateSql, connection, transaction);
                            updateCmd.Parameters.AddWithValue("@version", entity.Version);
                            updateCmd.Parameters.AddWithValue("@lastWriteTime", DateTime.UtcNow);
                            updateCmd.Parameters.AddWithValue("@listCacheKey", listCacheKey);
                            updateCmd.Parameters.AddWithValue("@entryCacheKey", entryCacheKey);
                            await updateCmd.ExecuteNonQueryAsync(cancellationToken);

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                    else if (entity.Version < mappingVersion)
                    {
                        throw new ConcurrencyConflictException(entryCacheKey, entity.Version, mappingVersion,
                            $"Entity version {entity.Version} is lower than mapping version {mappingVersion} for key '{entryCacheKey}' in list '{listCacheKey}'");
                    }

                    results.Add(entity);
                }

                // Write to audit trail if enabled
                if (this.Mapper.EnableAuditTrail && results.Any())
                {
                    try
                    {
                        var firstEntity = results.First();
                        await this.WriteAuditRecordAsync(firstEntity, "READ_LIST", callerInfo, null, true, cancellationToken);
                    }
                    catch
                    {
                        // Log but don't fail the operation if audit fails
                    }
                }

                stopwatch.Stop();
                PersistenceLogger.BatchOperationStop("GetList", results.Count, listCacheKey, stopwatch);
                return results;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PersistenceLogger.BatchOperationFailed("GetList", listCacheKey, stopwatch, ex);
                throw;
            }
        }

        public async Task<IEnumerable<T>> UpdateListAsync(
            string listCacheKey,
            IEnumerable<T> entities,
            CallerInfo callerInfo,
            CancellationToken cancellationToken = default)
        {
            if (!this.Mapper.SyncWithList)
            {
                throw new NotSupportedException("List operations require SyncWithList to be enabled for this entity type.");
            }

            if (string.IsNullOrEmpty(listCacheKey))
            {
                throw new ArgumentNullException(nameof(listCacheKey));
            }

            if (entities == null)
            {
                return Enumerable.Empty<T>();
            }

            var entityList = entities.ToList();

            var stopwatch = Stopwatch.StartNew();
            PersistenceLogger.BatchOperationStart("UpdateList", entityList.Count, listCacheKey);

            try
            {
                var results = new List<T>();

                using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                using var transaction = connection.BeginTransaction();
                try
                {
                    // Step 1: Get all existing mappings from EntryListMapping
                    var existingMappings = new Dictionary<string, long>(); // EntryCacheKey -> Version
                    var getMappingsSql = @"
                        SELECT EntryCacheKey, Version
                        FROM EntryListMapping
                        WHERE ListCacheKey = @listCacheKey";

                    using (var getMappingsCmd = this.CreateCommand(getMappingsSql, connection, transaction))
                    {
                        getMappingsCmd.Parameters.AddWithValue("@listCacheKey", listCacheKey);
                        using var reader = await getMappingsCmd.ExecuteReaderAsync(cancellationToken);
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            existingMappings[reader.GetString(0)] = reader.GetInt64(1);
                        }
                    }

                    // Step 2: Get next version for soft-delete enabled entities
                    long batchVersion = 1;
                    if (this.Mapper.EnableSoftDelete || typeof(IVersionedEntity<TKey>).IsAssignableFrom(typeof(T)))
                    {
                        using var versionCmd = this.versionMapper.CreateGetNextVersionCommand();
                        versionCmd.Connection = connection;
                        versionCmd.Transaction = transaction;
                        batchVersion = Convert.ToInt64(await versionCmd.ExecuteScalarAsync(cancellationToken));
                    }

                    // Step 3: Delete existing list mappings (we'll recreate them)
                    using var deleteListCmd = this.entryListMappingMapper.CreateDeleteByListKeyCommand(listCacheKey);
                    deleteListCmd.Connection = connection;
                    deleteListCmd.Transaction = transaction;
                    await deleteListCmd.ExecuteNonQueryAsync(cancellationToken);

                    var now = DateTime.UtcNow;

                    // Step 4: Process each entity
                    foreach (var entity in entityList)
                    {
                        var keyString = this.Mapper.SerializeKey(entity.Id);

                        // Read existing entity
                        var existingSql = $@"
                            SELECT {string.Join(",\n  ", this.Mapper.GetSelectColumns())}
                            FROM {this.Mapper.TableName}
                            WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key";

                        if (this.Mapper.EnableSoftDelete || typeof(IVersionedEntity<TKey>).IsAssignableFrom(typeof(T)))
                        {
                            existingSql += @"
                                ORDER BY Version DESC
                                LIMIT 1";
                        }

                        T existingEntity = null;
                        using (var existingCmd = this.CreateCommand(existingSql, connection, transaction))
                        {
                            existingCmd.Parameters.AddWithValue("@key", keyString);
                            using var reader = await existingCmd.ExecuteReaderAsync(cancellationToken);
                            if (await reader.ReadAsync(cancellationToken))
                            {
                                existingEntity = this.Mapper.MapFromReader(reader);
                            }
                        }

                        long entityVersion;

                        if (existingEntity == null)
                        {
                            // New entity - create it
                            if (this.Mapper.EnableSoftDelete || typeof(IVersionedEntity<TKey>).IsAssignableFrom(typeof(T)))
                            {
                                entity.Version = batchVersion;
                            }
                            else
                            {
                                entity.Version = 1;
                            }
                            entity.CreatedTime = DateTimeOffset.UtcNow;
                            entity.LastWriteTime = entity.CreatedTime;

                            var columns = this.Mapper.GetInsertColumns();
                            var parameters = columns.Select(c => $"@{c}").ToList();
                            var insertSql = $@"
                                INSERT INTO {this.Mapper.TableName} ({string.Join(", ", columns)})
                                VALUES ({string.Join(", ", parameters)});";

                            using var insertCmd = this.CreateCommand(insertSql, connection, transaction);
                            this.Mapper.AddParameters(insertCmd, entity);
                            await insertCmd.ExecuteNonQueryAsync(cancellationToken);

                            entityVersion = entity.Version;
                        }
                        else
                        {
                            // Existing entity - compare and update if different
                            // Simple comparison - in real implementation, should do deep object graph comparison
                            // For now, we'll use serialization comparison excluding version and timestamps
                            var existingJson = System.Text.Json.JsonSerializer.Serialize(existingEntity);
                            var newJson = System.Text.Json.JsonSerializer.Serialize(entity);

                            if (existingJson != newJson)
                            {
                                // Entity has changes - update it
                                if (this.Mapper.EnableSoftDelete || typeof(IVersionedEntity<TKey>).IsAssignableFrom(typeof(T)))
                                {
                                    // Soft delete - insert new version
                                    entity.Version = batchVersion;
                                    entity.LastWriteTime = DateTimeOffset.UtcNow;

                                    var columns = this.Mapper.GetInsertColumns();
                                    var parameters = columns.Select(c => $"@{c}").ToList();
                                    var insertSql = $@"
                                        INSERT INTO {this.Mapper.TableName} ({string.Join(", ", columns)})
                                        VALUES ({string.Join(", ", parameters)});";

                                    using var insertCmd = this.CreateCommand(insertSql, connection, transaction);
                                    this.Mapper.AddParameters(insertCmd, entity);
                                    await insertCmd.ExecuteNonQueryAsync(cancellationToken);

                                    entityVersion = batchVersion;
                                }
                                else
                                {
                                    // Non-soft delete - update in place
                                    entity.Version = existingEntity.Version + 1;
                                    entity.LastWriteTime = DateTimeOffset.UtcNow;

                                    var updateColumns = this.Mapper.GetUpdateColumns()
                                        .Select(c => $"{c} = @{c}")
                                        .ToList();

                                    var updateSql = $@"
                                        UPDATE {this.Mapper.TableName}
                                        SET {string.Join(", ", updateColumns)}
                                        WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key;";

                                    using var updateCmd = this.CreateCommand(updateSql, connection, transaction);
                                    this.Mapper.AddParameters(updateCmd, entity);
                                    updateCmd.Parameters.AddWithValue("@key", keyString);
                                    await updateCmd.ExecuteNonQueryAsync(cancellationToken);

                                    entityVersion = entity.Version;
                                }
                            }
                            else
                            {
                                // No changes - keep existing
                                entityVersion = existingEntity.Version;
                                entity.Version = existingEntity.Version;
                            }
                        }

                        // Step 5: Add to EntryListMapping
                        var listMapping = new EntryListMapping
                        {
                            ListCacheKey = listCacheKey,
                            EntryCacheKey = keyString,
                            Version = entityVersion,
                            CreatedTime = now,
                            LastWriteTime = now,
                            CallerFile = callerInfo?.CallerFilePath,
                            CallerMember = callerInfo?.CallerMemberName,
                            CallerLineNumber = callerInfo?.CallerLineNumber
                        };

                        var mappingContext = CommandContext<EntryListMapping, string>.ForInsert(listMapping);
                        mappingContext.CommandTimeout = this.configuration.CommandTimeout;
                        mappingContext.Transaction = transaction;
                        using var listMappingCmd = this.entryListMappingMapper.CreateCommand(DbOperationType.Insert, mappingContext);
                        listMappingCmd.Connection = connection;
                        listMappingCmd.ExecuteNonQuery();

                        results.Add(entity);
                    }

                    transaction.Commit();

                    // Write to audit trail if enabled
                    if (this.Mapper.EnableAuditTrail && results.Any())
                    {
                        try
                        {
                            foreach (var updatedEntity in results)
                            {
                                await this.WriteAuditRecordAsync(updatedEntity, "UPDATE_LIST", callerInfo, null, null,
                                    cancellationToken);
                            }
                        }
                        catch
                        {
                            // Log but don't fail the operation if audit fails
                        }
                    }

                    stopwatch.Stop();
                    PersistenceLogger.BatchOperationStop("UpdateList", results.Count, listCacheKey, stopwatch);
                    return results;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    stopwatch.Stop();
                    PersistenceLogger.BatchOperationFailed("UpdateList", listCacheKey, stopwatch, ex);
                    throw;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PersistenceLogger.BatchOperationFailed("UpdateList", listCacheKey, stopwatch, ex);
                throw;
            }
        }

        public async Task<int> DeleteListAsync(
            string listCacheKey,
            CallerInfo callerInfo,
            CancellationToken cancellationToken = default)
        {
            if (!this.Mapper.SyncWithList)
            {
                throw new NotSupportedException("List operations require SyncWithList to be enabled for this entity type.");
            }

            if (string.IsNullOrEmpty(listCacheKey))
            {
                throw new ArgumentNullException(nameof(listCacheKey));
            }

            var stopwatch = Stopwatch.StartNew();
            PersistenceLogger.BatchOperationStart("DeleteList", 0, listCacheKey);

            try
            {
                using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                using var transaction = connection.BeginTransaction();
                try
                {
                    // Only delete the list mappings - DO NOT delete entities
                    var deleteListSql = @"
                        DELETE FROM EntryListMapping 
                        WHERE ListCacheKey = @listCacheKey;
                        SELECT changes();";

                    using var deleteListCmd = this.CreateCommand(deleteListSql, connection, transaction);
                    deleteListCmd.Parameters.AddWithValue("@listCacheKey", listCacheKey);

                    var result = await deleteListCmd.ExecuteScalarAsync(cancellationToken);
                    var deletedCount = Convert.ToInt32(result);

                    transaction.Commit();

                    stopwatch.Stop();
                    PersistenceLogger.BatchOperationStop("DeleteList", deletedCount, listCacheKey, stopwatch);
                    return deletedCount;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    stopwatch.Stop();
                    PersistenceLogger.BatchOperationFailed("DeleteList", listCacheKey, stopwatch, ex);
                    throw;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PersistenceLogger.BatchOperationFailed("DeleteList", listCacheKey, stopwatch, ex);
                throw;
            }
        }

        #endregion
    }
}
