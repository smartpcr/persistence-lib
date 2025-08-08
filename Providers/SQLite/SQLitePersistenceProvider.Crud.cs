// -----------------------------------------------------------------------
// <copyright file="SQLitePersistenceProvider.Crud.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite
{
    using System;
    using System.Collections.Generic;
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
        #region CRUD Operations

        /// <summary>
        /// Implements Create = Func&lt;T, T&gt; by translating to parameterized SQL INSERT.
        /// </summary>
        public async Task<T> CreateAsync(T entity, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity), "Entity cannot be null");
            }

            var stopwatch = Stopwatch.StartNew();
            var keyString = this.Mapper.SerializeKey(entity.Id);
            var versionEntity = this.Mapper.EnableSoftDelete ? entity as IVersionedEntity<TKey> : null;
            long newVersion = 1; // initial version for new entity when soft-delete is disabled

            PersistenceLogger.CreateStart(keyString, this.Mapper.TableName);

            try
            {
                await using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                // Note: SQLite transactions use synchronous Commit/Rollback methods
                // as the underlying SQLite library doesn't provide true async transaction operations
                await using var transaction = connection.BeginTransaction();
                try
                {
                    // step 1, if soft-delete is enabled, get next version
                    if (this.Mapper.EnableSoftDelete)
                    {
                        await using var versionCommand = this.versionMapper.CreateGetNextVersionCommand();
                        versionCommand.Connection = connection;
                        versionCommand.Transaction = transaction;
                        newVersion = Convert.ToInt64(await versionCommand.ExecuteScalarAsync(cancellationToken));
                        entity.Version = newVersion;
                    }

                    // Step 2: Check if entity already exists, if soft-delete enabled, read the latest version
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
                    checkCommand.Parameters.AddWithValue("@key", this.Mapper.SerializeKey(entity.Id));

                    await using var checkReader = await checkCommand.ExecuteReaderAsync(cancellationToken);
                    if (await checkReader.ReadAsync(cancellationToken))
                    {
                        // step 3. when entity already exists, it must be soft-deleted
                        var existingEntity = this.Mapper.MapFromReader(checkReader);
                        if (versionEntity != null && existingEntity is IVersionedEntity<TKey> existingVersionedEntity && existingVersionedEntity.IsDeleted)
                        {
                            // this is valid scenario, since latest version is marked as deleted,
                        }
                        else
                        {
                            throw new EntityAlreadyExistsException(
                                entity.Id.ToString(),
                                $"Entity with key '{entity.Id}' already exists. Use UpdateAsync to modify existing entities.");
                        }
                    }

                    // Set tracking fields before insert
                    entity.CreatedTime = DateTimeOffset.UtcNow;
                    entity.LastWriteTime = entity.CreatedTime;
                    entity.Version = newVersion;
                    if (this.Mapper.EnableExpiry && entity is IExpirableEntity<TKey> expirable && this.Mapper.ExpirySpan.HasValue)
                    {
                        // Only set AbsoluteExpiration if it's null or default
                        if (expirable.AbsoluteExpiration == null || expirable.AbsoluteExpiration == default(DateTimeOffset))
                        {
                            expirable.AbsoluteExpiration = entity.CreatedTime + this.Mapper.ExpirySpan.Value;
                        }
                    }

                    // Step 4: Insert entity using new command pattern
                    var context = CommandContext<T, TKey>.ForInsert(entity);
                    context.CommandTimeout = this.configuration.CommandTimeout;
                    context.Transaction = transaction;

                    using var insertCommand = this.Mapper.CreateCommand(DbOperationType.Insert, context);
                    insertCommand.Connection = connection;
                    insertCommand.ExecuteNonQuery();

                    // Step 5: ensure the entity is inserted before commit
                    var selectSql = $@"
SELECT {string.Join(",\n  ", this.Mapper.GetSelectColumns())}
FROM {this.Mapper.TableName}
WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key
  AND Version = @version;";

                    await using var selectCommand = this.CreateCommand(selectSql, connection, transaction);
                    selectCommand.Parameters.AddWithValue("@key", this.Mapper.SerializeKey(entity.Id));
                    selectCommand.Parameters.AddWithValue("@version", entity.Version);

                    await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
                    T result;
                    if (await reader.ReadAsync(cancellationToken))
                    {
                        result = this.Mapper.MapFromReader(reader);
                        transaction.Commit();
                    }
                    else
                    {
                        throw new InvalidOperationException("Failed to retrieve created entity");
                    }

                    // Write to audit trail if enabled
                    if (this.Mapper.EnableAuditTrail)
                    {
                        try
                        {
                            // step 6. add audit trail
                            await this.WriteAuditRecordAsync(result, "CREATE", callerInfo, null, null, cancellationToken);
                        }
                        catch
                        {
                            // Log but don't fail the operation if audit fails
                        }
                    }

                    stopwatch.Stop();
                    PersistenceLogger.CreateStop(keyString, this.Mapper.TableName, stopwatch);

                    return result;
                }
                catch (EntityAlreadyExistsException)
                {
                    stopwatch.Stop();
                    PersistenceLogger.CreateFailed(keyString, this.Mapper.TableName, stopwatch, new InvalidOperationException($"Entity with key '{keyString}' already exists"));
                    // Transaction rollback will undo both Version and entity inserts
                    transaction.Rollback();
                    throw;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    PersistenceLogger.CreateFailed(keyString, this.Mapper.TableName, stopwatch, ex);
                    // Transaction rollback will undo both Version and entity inserts
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PersistenceLogger.CreateFailed(keyString, this.Mapper.TableName, stopwatch, ex);
                throw;
            }
        }

        /// <summary>
        /// Implements Get = Func&lt;TKey, T&gt; by translating to parameterized SQL SELECT.
        /// It assumes the table has a single PK (soft-delete disabled) or
        /// composite PK (+Version, soft-delete enabled).
        /// Returns the latest version of the entity (highest version number).
        /// </summary>
        public async Task<T> GetAsync(TKey key, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var keyString = this.Mapper.SerializeKey(key);
            PersistenceLogger.GetStart(keyString, this.Mapper.TableName);

            try
            {
                // When soft-delete is enabled,
                // Order by Version DESC to get the latest version first
                // IMPORTANT: We do NOT filter by IsDeleted = 0 here because:
                // 1. We need to return the latest version regardless of deletion status
                // 2. The caller needs to know if an entity was deleted (by checking IsDeleted flag)
                // 3. Filtering would hide deleted entities and make it impossible to distinguish
                //    between "never existed" and "was deleted"
                T result = null;

                await using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                // Use CreateSelectCommand which internally uses the new CreateCommand
                // includeAllVersions=false: only get latest version
                // includeDeleted=false: filter out deleted entities at SQL level
                // includeExpired=false: filter out expired entities at SQL level
                using var command = this.Mapper.CreateSelectCommand(key, includeAllVersions: false, includeDeleted: false, includeExpired: false);
                command.Connection = connection;

                var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    result = this.Mapper.MapFromReader(reader);
                    // No need to check IsDeleted - the mapper already filtered at SQL level
                }

                stopwatch.Stop();

                if (result == null)
                {
                    PersistenceLogger.GetNotFound(keyString, this.Mapper.TableName);
                }
                else
                {
                    PersistenceLogger.GetStop(keyString, this.Mapper.TableName, stopwatch);
                }

                // Write to audit trail if enabled
                if (this.Mapper.EnableAuditTrail && result != null)
                {
                    try
                    {
                        await this.WriteAuditRecordAsync(result, "READ", callerInfo, null, true, cancellationToken);
                    }
                    catch
                    {
                        // Log but don't fail the operation if audit fails
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PersistenceLogger.GetFailed(keyString, this.Mapper.TableName, stopwatch, ex);
                throw;
            }
        }

        public async Task<IEnumerable<T>> GetByKeyAsync(
            TKey key,
            CallerInfo callerInfo,
            bool includeAllVersions = false,
            bool includeDeleted = false,
            bool includeExpired = false,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var keyString = this.Mapper.SerializeKey(key);
            PersistenceLogger.GetStart(keyString, this.Mapper.TableName);

            try
            {
                // When soft-delete is enabled,
                // Order by Version DESC to get the latest version first
                // IMPORTANT: We do NOT filter by IsDeleted = 0 here because:
                // 1. We need to return the latest version regardless of deletion status
                // 2. The caller needs to know if an entity was deleted (by checking IsDeleted flag)
                // 3. Filtering would hide deleted entities and make it impossible to distinguish
                //    between "never existed" and "was deleted"
                var results = new List<T>();

                using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                // Use CreateSelectCommand which internally uses the new CreateCommand
                // The mapper now handles all filtering at the database level
                using var command = this.Mapper.CreateSelectCommand(key, includeAllVersions, includeDeleted, includeExpired);
                command.Connection = connection;

                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var foundEntity = this.Mapper.MapFromReader(reader);
                    if (foundEntity != null)
                    {
                        results.Add(foundEntity);
                    }
                }

                stopwatch.Stop();

                PersistenceLogger.GetStop(keyString, this.Mapper.TableName, stopwatch);

                // Write to audit trail if enabled
                if (this.Mapper.EnableAuditTrail && results.Any())
                {
                    try
                    {
                        await this.WriteAuditRecordAsync(results.First(), "READ", callerInfo, null, true, cancellationToken);
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
                PersistenceLogger.GetFailed(keyString, this.Mapper.TableName, stopwatch, ex);
                throw;
            }
        }

        /// <summary>
        /// Implements Update = Func&lt;T, T&gt; by translating to parameterized SQL UPDATE.
        /// </summary>
        public async Task<T> UpdateAsync(T entity, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var keyString = this.Mapper.SerializeKey(entity.Id);
            var versionedEntity = this.Mapper.EnableSoftDelete ? entity as IVersionedEntity<TKey> : null;

            PersistenceLogger.UpdateStart(keyString, this.Mapper.TableName);

            try
            {
                // Store original version for optimistic concurrency check
                var originalVersion = entity.Version;

                await using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                await using var transaction = connection.BeginTransaction();
                try
                {
                    // Step 1: concurrency check
                    T oldValue = null;
                    // Note: Here we DO filter by IsDeleted = 0 because we're verifying
                    // the entity exists and matches the expected version for update
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

                    await using var selectOldCommand = this.CreateCommand(selectOldSql, connection, transaction);
                    selectOldCommand.Parameters.AddWithValue("@key", this.Mapper.SerializeKey(entity.Id));

                    await using var oldReader = await selectOldCommand.ExecuteReaderAsync(cancellationToken);
                    if (await oldReader.ReadAsync(cancellationToken))
                    {
                        oldValue = this.Mapper.MapFromReader(oldReader);
                    }

                    if (oldValue == null)
                    {
                        throw new EntityNotFoundException(this.Mapper.SerializeKey(entity.Id), "Entity not found during update");
                    }

                    if (oldValue.Version != entity.Version)
                    {
                        throw new ConcurrencyConflictException(
                            this.Mapper.SerializeKey(entity.Id),
                            oldValue.Version,
                            entity.Version,
                            "Version does not match during update");
                    }

                    // Step 2: Insert into Version table to get next version
                    if (versionedEntity != null)
                    {
                        await using var versionCommand = this.versionMapper.CreateGetNextVersionCommand();
                        versionCommand.Connection = connection;
                        versionCommand.Transaction = transaction;
                        var newVersion = Convert.ToInt64(await versionCommand.ExecuteScalarAsync(cancellationToken));
                        entity.Version = newVersion;
                    }
                    else
                    {
                        entity.Version += 1;
                    }

                    // Update tracking fields
                    entity.LastWriteTime = DateTime.UtcNow;
                    int rowsAffected;

                    // Use new command pattern for update
                    var context = CommandContext<T, TKey>.ForUpdate(entity, oldValue);
                    context.CommandTimeout = this.configuration.CommandTimeout;
                    context.Transaction = transaction;

                    using var command = this.Mapper.CreateCommand(
                        versionedEntity == null ? DbOperationType.Update : DbOperationType.Insert,
                        context);
                    command.Connection = connection;

                    // For versioned entities (soft delete), we need to track changes
                    if (versionedEntity != null || command.CommandText.Contains("SELECT changes()"))
                    {
                        var cmdText = command.CommandText;
                        if (!cmdText.Contains("SELECT changes()"))
                        {
                            command.CommandText = cmdText + ";\nSELECT changes();";
                        }
                        rowsAffected = Convert.ToInt32(command.ExecuteNonQuery());
                    }
                    else
                    {
                        rowsAffected = command.ExecuteNonQuery();
                    }

                    if (rowsAffected == 0)
                    {
                        throw new EntityWriteException(
                            this.Mapper.SerializeKey(entity.Id),
                            $"Entity with key '{entity.Id}' has been modified by another process.");
                    }

                    transaction.Commit();

                    // Write to audit trail if enabled
                    if (this.Mapper.EnableAuditTrail)
                    {
                        try
                        {
                            await this.WriteAuditRecordAsync(entity, "UPDATE", callerInfo, oldValue, null, cancellationToken);
                        }
                        catch
                        {
                            // Log but don't fail the operation if audit fails
                        }
                    }

                    stopwatch.Stop();
                    PersistenceLogger.UpdateStop(keyString, this.Mapper.TableName, stopwatch);
                    return entity;
                }
                catch (ConcurrencyException)
                {
                    stopwatch.Stop();
                    PersistenceLogger.UpdateConcurrencyConflict(keyString, this.Mapper.TableName);
                    transaction.Rollback();
                    throw;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    PersistenceLogger.UpdateFailed(keyString, this.Mapper.TableName, stopwatch, ex);
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PersistenceLogger.UpdateFailed(keyString, this.Mapper.TableName, stopwatch, ex);
                throw;
            }
        }

        /// <summary>
        /// Implements Delete = Func&lt;TKey, bool&gt; by translating to SQL UPDATE (soft delete) or DELETE.
        /// </summary>
        public async Task<bool> DeleteAsync(TKey key, CallerInfo callerInfo, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var keyString = this.Mapper.SerializeKey(key);

            PersistenceLogger.DeleteStart(keyString, this.Mapper.TableName);

            try
            {
                await using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);
                await using var transaction = connection.BeginTransaction();

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

                await using var selectOldCommand = this.CreateCommand(selectOldSql, connection, transaction);
                selectOldCommand.Parameters.AddWithValue("@key", this.Mapper.SerializeKey(key));

                await using var oldReader = await selectOldCommand.ExecuteReaderAsync(cancellationToken);
                if (await oldReader.ReadAsync(cancellationToken))
                {
                    oldValue = this.Mapper.MapFromReader(oldReader);
                }

                if (this.Mapper.EnableSoftDelete)
                {
                    if (oldValue == null)
                    {
                        throw new EntityNotFoundException(
                            this.Mapper.SerializeKey(key),
                            $"Entity with key '{key}' not found for deletion.");
                    }

                    if (oldValue is IVersionedEntity<TKey> versionedEntity && versionedEntity.IsDeleted)
                    {
                        // If already soft-deleted, nothing to do
                        return true;
                    }
                }
                else if (oldValue == null)
                {
                    // If hard delete and entity not found, nothing to do
                    return true;
                }

                string sql;

                if (this.Mapper.EnableSoftDelete)
                {
                    // Step 2. insert into Version table to get next version
                    await using var versionCommand = this.versionMapper.CreateGetNextVersionCommand();
                    versionCommand.Connection = connection;
                    versionCommand.Transaction = transaction;
                    var newVersion = Convert.ToInt64(await versionCommand.ExecuteScalarAsync(cancellationToken));

                    // Step 3. insert with new version and IsDeleted = 1
                    var deletedEntity = oldValue;
                    var newValue = deletedEntity as IVersionedEntity<TKey>;
                    if (newValue == null)
                    {
                        throw new InvalidOperationException("Entity does not implement IVersionedEntity for soft delete.");
                    }

                    newValue.IsDeleted = true;
                    deletedEntity.Version = newVersion;
                    deletedEntity.LastWriteTime = DateTime.UtcNow;

                    var columns = this.Mapper.GetInsertColumns();
                    var parameters = columns.Select(c => $"@{c}").ToList();
                    var insertEntitySql = $@"
INSERT INTO {this.Mapper.TableName} ({string.Join(", ", columns)})
VALUES ({string.Join(", ", parameters)});";

                    await using var insertCommand = this.CreateCommand(insertEntitySql, connection, transaction);
                    this.Mapper.AddParameters(insertCommand, deletedEntity);
                    await insertCommand.ExecuteNonQueryAsync(cancellationToken);
                }
                else
                {
                    sql = $@"
DELETE FROM {this.Mapper.TableName}
WHERE {this.Mapper.GetPrimaryKeyColumn()} = @key;
SELECT changes();";
                    await using var command = this.CreateCommand(sql, connection, transaction);
                    command.Parameters.AddWithValue("@key", this.Mapper.SerializeKey(key));
                    await command.ExecuteScalarAsync(cancellationToken);
                }

                transaction.Commit();

                // Write to audit trail if enabled
                if (this.Mapper.EnableAuditTrail)
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

                stopwatch.Stop();
                PersistenceLogger.DeleteStop(keyString, this.Mapper.TableName, stopwatch);
                return true;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PersistenceLogger.DeleteFailed(keyString, this.Mapper.TableName, stopwatch, ex);
                throw;
            }
        }

        #endregion
    }
}
