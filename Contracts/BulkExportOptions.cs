//-------------------------------------------------------------------------------
// <copyright file="BulkExportOptions.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    using System;

    /// <summary>
    /// Options for bulk export operations.
    /// </summary>
    public class BulkExportOptions
    {
        /// <summary>
        /// Gets or sets the batch size for processing entities.
        /// </summary>
        public int BatchSize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the operation timeout.
        /// </summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets the export mode (Full, Incremental, Archive).
        /// </summary>
        public ExportMode Mode { get; set; } = ExportMode.Full;

        /// <summary>
        /// Gets or sets the export directory path. If this is not specified, in-memory export will be performed.
        /// </summary>
        public string ExportFolder { get; set; }

        /// <summary>
        /// Gets or sets whether to include soft-deleted entities.
        /// </summary>
        public bool IncludeDeleted { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to include all versions (for soft-delete enabled entities).
        /// </summary>
        public bool IncludeAllVersions { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to include expired entities (for soft-delete enabled entities).
        /// </summary>
        public bool IncludeExpired { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to compress the output files.
        /// </summary>
        public bool CompressOutput { get; set; } = true;

        /// <summary>
        /// Gets or sets the starting timestamp for incremental exports.
        /// </summary>
        public DateTime? IncrementalFromDate { get; set; }

        /// <summary>
        /// Gets or sets the age threshold for archive exports.
        /// </summary>
        public TimeSpan? ArchiveOlderThan { get; set; }

        /// <summary>
        /// Gets or sets whether to mark entities as exported (for archive mode).
        /// </summary>
        public bool MarkAsExported { get; set; } = false;
    }
}