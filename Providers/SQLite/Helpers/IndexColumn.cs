// -----------------------------------------------------------------------
// <copyright file="IndexColumn.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Helpers
{
    /// <summary>
    /// Represents a column in an index.
    /// </summary>
    public class IndexColumn
    {
        /// <summary>
        /// Gets or sets the sequence number in the index.
        /// </summary>
        public int SequenceNumber { get; set; }

        /// <summary>
        /// Gets or sets the column ID.
        /// </summary>
        public int ColumnId { get; set; }

        /// <summary>
        /// Gets or sets the column name.
        /// </summary>
        public string ColumnName { get; set; }

        /// <summary>
        /// Gets or sets whether this column is sorted descending.
        /// </summary>
        public bool IsDescending { get; set; }

        /// <summary>
        /// Gets or sets the collation sequence.
        /// </summary>
        public string Collation { get; set; }

        /// <summary>
        /// Gets or sets whether this is a key column (vs included column).
        /// </summary>
        public bool IsKey { get; set; }
    }
}