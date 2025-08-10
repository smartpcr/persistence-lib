// -----------------------------------------------------------------------
// <copyright file="SQLitePersistenceProvider.Query.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Traces;

    public partial class SQLitePersistenceProvider<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        #region Query operations

        public async Task<IEnumerable<T>> QueryAsync(
            Expression<Func<T, bool>> predicate,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy,
            CallerInfo callerInfo,
            int? skip = null,
            int? take = null,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            PersistenceLogger.QueryStart(this.EscapedTableName);

            try
            {
                var results = new List<T>();

                await using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                // Translate the expression to SQL
                var translator = new SQLiteExpressionTranslator<T>(
                    this.Mapper.GetPropertyMappings(),
                    () => this.Mapper.GetPrimaryKeyColumn());

                var whereClause = "";
                var parameters = new Dictionary<string, object>();
                if (predicate != null)
                {
                    var translationResult = translator.Translate(predicate);
                    whereClause = "WHERE " + translationResult.Sql;
                    parameters = translationResult.Parameters;
                }

                if (this.Mapper.EnableSoftDelete)
                {
                    if (!string.IsNullOrEmpty(whereClause))
                    {
                        whereClause += " AND IsDeleted = 0";
                    }
                    else
                    {
                        whereClause = "WHERE IsDeleted = 0";
                    }
                }

                var orderByClause = "";
                if (orderBy != null)
                {
                    // this adds "ORDER BY"
                    orderByClause = translator.TranslateOrderBy(orderBy);
                }

                // Build the query
                var sql = $@"
SELECT {string.Join(", ", this.Mapper.GetSelectColumns())}
FROM {this.EscapedTableName}
{whereClause}
{orderByClause}";

                // Apply LIMIT and OFFSET for pagination
                if (take.HasValue)
                {
                    sql += $" LIMIT {take.Value}";
                }
                if (skip.HasValue)
                {
                    sql += $" OFFSET {skip.Value}";
                }

                await using var command = this.CreateCommand(sql, connection);

                // Add parameters from the translation
                foreach (var param in parameters)
                {
                    // param.Key already has '@' prefix
                    command.Parameters.AddWithValue($"{param.Key}", param.Value ?? DBNull.Value);
                }

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                // Track the latest version for each unique key
                var latestVersions = new Dictionary<TKey, long>();
                var entities = new List<T>();

                while (await reader.ReadAsync(cancellationToken))
                {
                    var entity = this.Mapper.MapFromReader(reader);
                    if (entity != null)
                    {
                        // Check if we already have this key and if this version is newer
                        if (!latestVersions.ContainsKey(entity.Id) || entity.Version > latestVersions[entity.Id])
                        {
                            latestVersions[entity.Id] = entity.Version;

                            // Remove any previous version of this entity
                            entities.RemoveAll(e => e.Id.Equals(entity.Id));
                            entities.Add(entity);
                        }
                    }
                }

                // Record access history for query operation if needed
                if (this.Mapper.EnableAuditTrail && entities.Any())
                {
                    try
                    {
                        await this.WriteAuditRecordAsync(entities[0], AuditOperation.Read, callerInfo, null, null, cancellationToken);
                    }
                    catch
                    {
                        // Ignore history recording failures
                    }
                }

                stopwatch.Stop();
                PersistenceLogger.QueryStop(this.EscapedTableName, entities.Count, stopwatch);

                // Check for slow queries (> 1 second)
                if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    PersistenceLogger.SlowQuery(this.EscapedTableName, stopwatch.ElapsedMilliseconds);
                }

                return entities;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PersistenceLogger.QueryFailed(this.EscapedTableName, stopwatch, ex);
                throw;
            }
        }

        public async Task<PagedResult<T>> QueryPagedAsync(
            Expression<Func<T, bool>> predicate,
            int pageSize,
            int pageNumber,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
            bool ascending = true,
            CancellationToken cancellationToken = default)
        {
            if (pageSize <= 0)
                throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));
            if (pageNumber <= 0)
                throw new ArgumentException("Page number must be greater than 0", nameof(pageNumber));

            var stopwatch = Stopwatch.StartNew();
            PersistenceLogger.QueryStart(this.EscapedTableName);

            try
            {
                await using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                // Translate the predicate expression to SQL
                var translator = new SQLiteExpressionTranslator<T>(
                    this.Mapper.GetPropertyMappings(),
                    () => this.Mapper.GetPrimaryKeyColumn());
                var whereClause = "";
                var parameters = new Dictionary<string, object>();
                if (predicate != null)
                {
                    var translationResult = translator.Translate(predicate);
                    whereClause = "WHERE " + translationResult.Sql;
                    parameters = translationResult.Parameters;
                }

                if (this.Mapper.EnableSoftDelete)
                {
                    if (string.IsNullOrEmpty(whereClause))
                    {
                        whereClause = "WHERE IsDeleted = 0";
                    }
                    else
                    {
                        whereClause += "AND IsDeleted = 0";
                    }
                }

                // First, get the total count of matching records (only counting latest versions)
                var countSql = $@"
WITH LatestVersions AS (
    SELECT {this.Mapper.GetPrimaryKeyColumn()}, MAX(Version) as MaxVersion
    FROM {this.EscapedTableName}
    {whereClause}
    GROUP BY {this.Mapper.GetPrimaryKeyColumn()}
)
SELECT COUNT(*) FROM LatestVersions;";

                long totalCount;
                await using (var countCommand = this.CreateCommand(countSql, connection))
                {
                    // Add parameters for count query
                    foreach (var param in parameters)
                    {
                        countCommand.Parameters.AddWithValue($"{param.Key}", param.Value ?? DBNull.Value);
                    }

                    var countResult = await countCommand.ExecuteScalarAsync(cancellationToken);
                    totalCount = Convert.ToInt64(countResult);
                }

                // Build ORDER BY clause using the translator
                var orderByClause = "";
                if (orderBy != null)
                {
                    orderByClause = translator.TranslateOrderBy(orderBy, ascending);
                }

                // Always add Version DESC as secondary sort to ensure consistent ordering
                if (string.IsNullOrEmpty(orderByClause))
                {
                    orderByClause = "ORDER BY Version DESC";
                }
                else
                {
                    orderByClause += ", Version DESC";
                }

                // Calculate offset for pagination
                var offset = (pageNumber - 1) * pageSize;

                // Build the main query with pagination
                var sql = $@"
WITH LatestVersions AS (
    SELECT *, ROW_NUMBER() OVER (PARTITION BY {this.Mapper.GetPrimaryKeyColumn()} ORDER BY Version DESC) as rn
    FROM {this.EscapedTableName}
    {whereClause}
)
SELECT {string.Join(", ", this.Mapper.GetSelectColumns().Select(c => $"lv.{c}"))}
FROM LatestVersions lv
WHERE lv.rn = 1
{orderByClause}
LIMIT @pageSize OFFSET @offset";

                var items = new List<T>();
                await using (var command = this.CreateCommand(sql, connection))
                {
                    // Add parameters from the translation
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue($"{param.Key}", param.Value ?? DBNull.Value);
                    }

                    // Add pagination parameters
                    command.Parameters.AddWithValue("@pageSize", pageSize);
                    command.Parameters.AddWithValue("@offset", offset);

                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var entity = this.Mapper.MapFromReader(reader);
                        if (entity != null)
                        {
                            items.Add(entity);
                        }
                    }
                }

                stopwatch.Stop();
                PersistenceLogger.QueryStop(this.EscapedTableName, items.Count, stopwatch);

                // Check for slow queries (> 1 second)
                if (stopwatch.ElapsedMilliseconds > 1000)
                {
                    PersistenceLogger.SlowQuery(this.EscapedTableName, stopwatch.ElapsedMilliseconds);
                }

                return new PagedResult<T>
                {
                    Items = items,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PersistenceLogger.QueryFailed(this.EscapedTableName, stopwatch, ex);
                throw;
            }
        }

        public async Task<long> CountAsync(Expression<Func<T, bool>> predicate = null, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            PersistenceLogger.QueryStart(this.EscapedTableName);

            try
            {
                await using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                var whereClause = "WHERE 1=1"; // Default condition if no predicate
                var parameters = new Dictionary<string, object>();

                // If predicate is provided, translate it to SQL
                if (predicate != null)
                {
                    var translator = new SQLiteExpressionTranslator<T>(
                        this.Mapper.GetPropertyMappings(),
                        () => this.Mapper.GetPrimaryKeyColumn());
                    var translationResult = translator.Translate(predicate);
                    whereClause = "WHERE " + translationResult.Sql;
                    parameters = translationResult.Parameters;
                }


                if (this.Mapper.EnableSoftDelete)
                {
                    whereClause += @" AND IsDeleted = 0";
                }

                // Count only the latest version of each entity (excluding soft-deleted)
                var sql = $@"
WITH LatestVersions AS (
    SELECT {this.Mapper.GetPrimaryKeyColumn()}, MAX(Version) as MaxVersion
    FROM {this.EscapedTableName}
    {whereClause}
    GROUP BY {this.Mapper.GetPrimaryKeyColumn()}
)
SELECT COUNT(*) FROM LatestVersions";


                await using var command = this.CreateCommand(sql, connection);

                // Add parameters from the translation
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue($"{param.Key}", param.Value ?? DBNull.Value);
                }

                var result = await command.ExecuteScalarAsync(cancellationToken);
                stopwatch.Stop();
                PersistenceLogger.QueryStop(this.EscapedTableName, 1, stopwatch); // Count queries return single value
                return Convert.ToInt64(result);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PersistenceLogger.QueryFailed(this.EscapedTableName, stopwatch, ex);
                throw;
            }
        }

        public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            var stopwatch = Stopwatch.StartNew();
            PersistenceLogger.QueryStart(this.EscapedTableName);

            try
            {
                await using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                // Check if any entity exists matching the predicate (only checking latest versions)
                var (selectSql, parameters) = this.Mapper.GenerateSelectSql(predicate);
                var sql = $@"
SELECT EXISTS (
    {selectSql}
    LIMIT 1
)";

                await using var command = this.CreateCommand(sql, connection);

                // Add parameters from the translation
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue($"{param.Key}", param.Value ?? DBNull.Value);
                }

                var result = await command.ExecuteScalarAsync(cancellationToken);
                stopwatch.Stop();
                PersistenceLogger.QueryStop(this.EscapedTableName, 1, stopwatch); // Exists queries return single value
                return Convert.ToBoolean(result);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PersistenceLogger.QueryFailed(this.EscapedTableName, stopwatch, ex);
                throw;
            }
        }

        #endregion
    }
}
