// -----------------------------------------------------------------------
// <copyright file="DatabaseStats.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Helpers
{
    using System;

    /// <summary>
    /// Represents database statistics.
    /// </summary>
    public class DatabaseStats
    {
        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the formatted file size.
        /// </summary>
        public string FormattedFileSize { get; set; }

        /// <summary>
        /// Gets or sets the total number of pages.
        /// </summary>
        public int PageCount { get; set; }

        /// <summary>
        /// Gets or sets the number of free pages.
        /// </summary>
        public int FreePageCount { get; set; }

        /// <summary>
        /// Gets or sets the database encoding.
        /// </summary>
        public string Encoding { get; set; }

        /// <summary>
        /// Gets or sets the auto vacuum mode.
        /// </summary>
        public string AutoVacuum { get; set; }

        /// <summary>
        /// Gets or sets the user version.
        /// </summary>
        public int UserVersion { get; set; }

        /// <summary>
        /// Gets or sets the application ID.
        /// </summary>
        public int ApplicationId { get; set; }

        /// <summary>
        /// Gets or sets the last modified date.
        /// </summary>
        public DateTime? LastModified { get; set; }

        /// <summary>
        /// Gets or sets the total number of tables.
        /// </summary>
        public int TableCount { get; set; }

        /// <summary>
        /// Gets or sets the total number of indexes.
        /// </summary>
        public int IndexCount { get; set; }

        /// <summary>
        /// Gets or sets the total number of triggers.
        /// </summary>
        public int TriggerCount { get; set; }

        /// <summary>
        /// Gets or sets the total number of views.
        /// </summary>
        public int ViewCount { get; set; }
    }
}