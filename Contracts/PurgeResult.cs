//-------------------------------------------------------------------------------
// <copyright file="PurgeResult.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Result of a purge operation.
    /// </summary>
    public class PurgeResult
    {
        /// <summary>
        /// Gets or sets the count of entities purged.
        /// </summary>
        public long EntitiesPurged { get; set; }

        /// <summary>
        /// Gets or sets the count of versions purged (for soft-delete).
        /// </summary>
        public long VersionsPurged { get; set; }

        /// <summary>
        /// Gets or sets the space reclaimed in bytes.
        /// </summary>
        public long SpaceReclaimedBytes { get; set; }

        /// <summary>
        /// Gets or sets the operation duration.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets whether this was a preview (safe mode) operation.
        /// </summary>
        public bool IsPreview { get; set; }

        /// <summary>
        /// Gets or sets the preview data (if in safe mode).
        /// </summary>
        public PurgePreview Preview { get; set; }

        /// <summary>
        /// Gets or sets the backup result (if backup was performed).
        /// </summary>
        public BackupResult Backup { get; set; }

        /// <summary>
        /// Gets or sets purge statistics.
        /// </summary>
        public PurgeStatistics Statistics { get; set; } = new PurgeStatistics();

        /// <summary>
        /// Gets or sets any errors encountered.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets whether the purge was aborted.
        /// </summary>
        public bool Aborted { get; set; }

        /// <summary>
        /// Gets or sets the abort reason.
        /// </summary>
        public string AbortReason { get; set; }

        /// <summary>
        /// Gets or sets audit information.
        /// </summary>
        public PurgeAudit Audit { get; set; }
    }
}