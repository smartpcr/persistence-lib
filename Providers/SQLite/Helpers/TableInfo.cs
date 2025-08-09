// -----------------------------------------------------------------------
// <copyright file="TableInfo.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Helpers
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents table information.
    /// </summary>
    public class TableInfo
    {
        /// <summary>
        /// Gets or sets the table name.
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Gets or sets the SQL create statement.
        /// </summary>
        public string CreateSql { get; set; }

        /// <summary>
        /// Gets or sets the number of rows.
        /// </summary>
        public long RowCount { get; set; }

        /// <summary>
        /// Gets or sets the root page number.
        /// </summary>
        public int RootPage { get; set; }

        /// <summary>
        /// Gets or sets the list of columns.
        /// </summary>
        public List<ColumnInfo> Columns { get; set; } = new List<ColumnInfo>();

        /// <summary>
        /// Gets or sets the list of indexes on this table.
        /// </summary>
        public List<IndexInfo> Indexes { get; set; } = new List<IndexInfo>();

        /// <summary>
        /// Gets or sets the list of foreign keys referencing this table.
        /// </summary>
        public List<ForeignKeyInfo> ForeignKeys { get; set; } = new List<ForeignKeyInfo>();

        /// <summary>
        /// Gets or sets whether this table has a primary key.
        /// </summary>
        public bool HasPrimaryKey { get; set; }

        /// <summary>
        /// Gets or sets whether this is a WITHOUT ROWID table.
        /// </summary>
        public bool IsWithoutRowId { get; set; }

        /// <summary>
        /// Gets or sets whether this table is strict.
        /// </summary>
        public bool IsStrict { get; set; }
    }
}