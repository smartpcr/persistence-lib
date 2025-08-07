//-------------------------------------------------------------------------------
// <copyright file="BulkImportOptions.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    using System;

    /// <summary>
    /// Options for bulk import operations.
    /// </summary>
    public class BulkImportOptions
    {
        /// <summary>
        /// Gets or sets the batch size for processing entities.
        /// </summary>
        public int BatchSize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets whether to ignore duplicate keys (deprecated - use ImportStrategy instead).
        /// </summary>
        [Obsolete("Use ImportStrategy instead")]
        public bool IgnoreDuplicates { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to validate entities before import.
        /// </summary>
        public bool ValidateBeforeImport { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to update existing entities (deprecated - use ImportStrategy instead).
        /// </summary>
        [Obsolete("Use ImportStrategy instead")]
        public bool UpdateExisting { get; set; } = false;

        /// <summary>
        /// Gets or sets the operation timeout.
        /// </summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets the import strategy.
        /// </summary>
        public ImportStrategy Strategy { get; set; } = ImportStrategy.Upsert;

        /// <summary>
        /// Gets or sets the conflict resolution strategy.
        /// </summary>
        public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.UseSource;

        /// <summary>
        /// Gets or sets whether to validate schema compatibility (for file imports).
        /// </summary>
        public bool ValidateSchema { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to preserve version chains for soft-delete entities.
        /// </summary>
        public bool PreserveVersionChains { get; set; } = true;

        /// <summary>
        /// Gets or sets the expected schema version (for validation).
        /// </summary>
        public string ExpectedSchemaVersion { get; set; }

        /// <summary>
        /// Gets or sets custom field merge priorities for merge conflict resolution.
        /// </summary>
        public string[] FieldMergePriorities { get; set; }
    }

    /// <summary>
    /// Import strategy for bulk operations.
    /// </summary>
    public enum ImportStrategy
    {
        /// <summary>
        /// Clear existing data and import all entities.
        /// </summary>
        Replace,

        /// <summary>
        /// Keep existing data and only add new entities.
        /// </summary>
        Merge,

        /// <summary>
        /// Update existing entities or insert new ones.
        /// </summary>
        Upsert
    }

    /// <summary>
    /// Conflict resolution strategy for imports.
    /// </summary>
    public enum ConflictResolution
    {
        /// <summary>
        /// Use the imported version.
        /// </summary>
        UseSource,

        /// <summary>
        /// Keep the existing version.
        /// </summary>
        UseTarget,

        /// <summary>
        /// Merge changes by field priority.
        /// </summary>
        Merge,

        /// <summary>
        /// Log conflicts for manual resolution.
        /// </summary>
        Manual
    }
}