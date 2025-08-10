//-------------------------------------------------------------------------------
// <copyright file="EntryListMappingMapper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Entities
{
    using System.Data;
    using System.Data.SQLite;
    using Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Resilience;

    /// <summary>
    /// Provides mapping functionality for EntryListMapping entities.
    /// </summary>
    public class EntryListMappingMapper : SQLiteEntityMapper<EntryListMapping, string>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EntryListMappingMapper"/> class.
        /// </summary>
        public EntryListMappingMapper(RetryPolicy retryPolicy) : base(retryPolicy)
        {
        }

        /// <summary>
        /// Creates a SELECT command for retrieving an EntryListMapping by list and entry cache keys.
        /// </summary>
        /// <param name="listCacheKey">The list cache key.</param>
        /// <param name="entryCacheKey">The entry cache key.</param>
        /// <returns>The SQL command.</returns>
        public IDbCommand CreateSelectByKeysCommand(string listCacheKey, string entryCacheKey)
        {
            var sql = $"SELECT * FROM {this.GetTableName()} WHERE ListCacheKey = @ListCacheKey AND EntryCacheKey = @EntryCacheKey";
            var cmd = new ResilientSQLiteCommand(new SQLiteCommand(sql), this.RetryPolicy);
            cmd.Parameters.AddWithValue("@ListCacheKey", listCacheKey);
            cmd.Parameters.AddWithValue("@EntryCacheKey", entryCacheKey);
            return cmd;
        }

        /// <summary>
        /// Creates a SELECT command for retrieving all entries for a list cache key.
        /// </summary>
        /// <param name="listCacheKey">The list cache key.</param>
        /// <returns>The SQL command.</returns>
        public IDbCommand CreateSelectByListKeyCommand(string listCacheKey)
        {
            var sql = $"SELECT * FROM {this.GetTableName()} WHERE ListCacheKey = @ListCacheKey ORDER BY EntryCacheKey";
            var cmd = new ResilientSQLiteCommand(new SQLiteCommand(sql), this.RetryPolicy);
            cmd.Parameters.AddWithValue("@ListCacheKey", listCacheKey);
            return cmd;
        }

        /// <summary>
        /// Creates a DELETE command for removing all entries for a list cache key.
        /// </summary>
        /// <param name="listCacheKey">The list cache key.</param>
        /// <returns>The SQL command.</returns>
        public IDbCommand CreateDeleteByListKeyCommand(string listCacheKey)
        {
            var sql = $"DELETE FROM {this.GetTableName()} WHERE ListCacheKey = @ListCacheKey";
            var cmd = new ResilientSQLiteCommand(new SQLiteCommand(sql), this.RetryPolicy);
            cmd.Parameters.AddWithValue("@ListCacheKey", listCacheKey);
            return cmd;
        }
    }
}