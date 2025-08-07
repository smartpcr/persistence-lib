//-------------------------------------------------------------------------------
// <copyright file="BulkImportResult.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Result of a bulk import operation.
    /// </summary>
    public class BulkImportResult
    {
        /// <summary>
        /// Gets or sets the count of successfully imported entities.
        /// </summary>
        public long SuccessCount { get; set; }

        /// <summary>
        /// Gets or sets the count of failed imports.
        /// </summary>
        public long FailureCount { get; set; }

        /// <summary>
        /// Gets or sets the count of duplicate entities encountered.
        /// </summary>
        public long DuplicateCount { get; set; }

        /// <summary>
        /// Gets or sets the operation duration.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the list of errors encountered.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets import statistics.
        /// </summary>
        public ImportStatistics Statistics { get; set; } = new ImportStatistics();

        /// <summary>
        /// Gets or sets conflict resolution details.
        /// </summary>
        public List<ConflictDetail> Conflicts { get; set; } = new List<ConflictDetail>();

        /// <summary>
        /// Gets or sets whether the import was rolled back.
        /// </summary>
        public bool RolledBack { get; set; }

        /// <summary>
        /// Gets or sets the import metadata (for file imports).
        /// </summary>
        public ImportMetadata Metadata { get; set; }
    }
}