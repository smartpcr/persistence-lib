// -----------------------------------------------------------------------
// <copyright file="IndexInfo.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Helpers
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents index information.
    /// </summary>
    public class IndexInfo
    {
        /// <summary>
        /// Gets or sets the index name.
        /// </summary>
        public string IndexName { get; set; }

        /// <summary>
        /// Gets or sets the table name.
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Gets or sets whether this is a unique index.
        /// </summary>
        public bool IsUnique { get; set; }

        /// <summary>
        /// Gets or sets whether this is a partial index.
        /// </summary>
        public bool IsPartial { get; set; }

        /// <summary>
        /// Gets or sets the SQL create statement.
        /// </summary>
        public string CreateSql { get; set; }

        /// <summary>
        /// Gets or sets the root page number.
        /// </summary>
        public int RootPage { get; set; }

        /// <summary>
        /// Gets or sets the list of indexed columns.
        /// </summary>
        public List<IndexColumn> Columns { get; set; } = new List<IndexColumn>();

        /// <summary>
        /// Gets or sets the WHERE clause for partial indexes.
        /// </summary>
        public string WhereClause { get; set; }

        /// <summary>
        /// Gets or sets whether this is an auto-created index.
        /// </summary>
        public bool IsAutoIndex { get; set; }
    }
}