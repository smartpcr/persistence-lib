// -----------------------------------------------------------------------
// <copyright file="DatabaseInfo.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Helpers
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents comprehensive database information including stats, tables, indexes, and constraints.
    /// </summary>
    public class DatabaseInfo
    {
        /// <summary>
        /// Gets or sets the database file path.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Gets or sets the database statistics.
        /// </summary>
        public DatabaseStats Stats { get; set; }

        /// <summary>
        /// Gets or sets the list of tables in the database.
        /// </summary>
        public List<TableInfo> Tables { get; set; } = new List<TableInfo>();

        /// <summary>
        /// Gets or sets the list of indexes in the database.
        /// </summary>
        public List<IndexInfo> Indexes { get; set; } = new List<IndexInfo>();

        /// <summary>
        /// Gets or sets the list of foreign key constraints.
        /// </summary>
        public List<ForeignKeyInfo> ForeignKeys { get; set; } = new List<ForeignKeyInfo>();

        /// <summary>
        /// Gets or sets the SQLite version.
        /// </summary>
        public string SqliteVersion { get; set; }

        /// <summary>
        /// Gets or sets the journal mode.
        /// </summary>
        public string JournalMode { get; set; }

        /// <summary>
        /// Gets or sets the page size.
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Gets or sets the cache size.
        /// </summary>
        public int CacheSize { get; set; }

        /// <summary>
        /// Gets or sets whether foreign keys are enabled.
        /// </summary>
        public bool ForeignKeysEnabled { get; set; }
    }
}