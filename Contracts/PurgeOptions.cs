//-------------------------------------------------------------------------------
// <copyright file="PurgeOptions.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    using System;
    using System.Linq.Expressions;

    /// <summary>
    /// Options for data purge operations.
    /// </summary>
    public class PurgeOptions
    {
        /// <summary>
        /// Gets or sets the age threshold for purging entities.
        /// </summary>
        public TimeSpan? AgeThreshold { get; set; }

        /// <summary>
        /// Gets or sets the cutoff date for purging (alternative to AgeThreshold).
        /// </summary>
        public DateTime? CutoffDate { get; set; }

        /// <summary>
        /// Gets or sets whether safe mode is enabled (preview only, no actual deletion).
        /// </summary>
        public bool SafeMode { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to create a backup before purging.
        /// </summary>
        public bool BackupBeforePurge { get; set; } = true;

        /// <summary>
        /// Gets or sets the backup export path.
        /// </summary>
        public string BackupPath { get; set; }

        /// <summary>
        /// Gets or sets whether to optimize storage after purging (rebuild indexes, reclaim space).
        /// </summary>
        public bool OptimizeStorage { get; set; } = false;

        /// <summary>
        /// Gets or sets the batch size for purge operations.
        /// </summary>
        public int BatchSize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the operation timeout.
        /// </summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets whether to use transactions for atomic purges.
        /// </summary>
        public bool UseTransaction { get; set; } = true;

        /// <summary>
        /// Gets or sets the purge strategy for soft-delete entities.
        /// </summary>
        public PurgeStrategy Strategy { get; set; } = PurgeStrategy.PreserveActiveVersions;

        /// <summary>
        /// Gets or sets whether to include sample data in preview mode.
        /// </summary>
        public bool IncludeSampleDataInPreview { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of sample entities to include in preview.
        /// </summary>
        public int MaxPreviewSamples { get; set; } = 10;

    }
}