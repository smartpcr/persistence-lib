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

        /// <summary>
        /// Gets or sets the file format for import operations.
        /// </summary>
        public FileFormat FileFormat { get; set; } = FileFormat.Auto;

        /// <summary>
        /// Gets or sets CSV-specific options.
        /// </summary>
        public CsvOptions CsvOptions { get; set; } = new CsvOptions();
    }

    /// <summary>
    /// CSV-specific import/export options.
    /// </summary>
    public class CsvOptions
    {
        /// <summary>
        /// Gets or sets the delimiter character for CSV files.
        /// </summary>
        public char Delimiter { get; set; } = ',';

        /// <summary>
        /// Gets or sets whether the first row contains headers.
        /// </summary>
        public bool HasHeaders { get; set; } = true;

        /// <summary>
        /// Gets or sets the quote character for CSV fields.
        /// </summary>
        public char QuoteCharacter { get; set; } = '"';

        /// <summary>
        /// Gets or sets whether to skip empty rows.
        /// </summary>
        public bool SkipEmptyRows { get; set; } = true;

        /// <summary>
        /// Gets or sets the date format for parsing date fields.
        /// </summary>
        public string DateFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// Gets or sets whether to trim whitespace from fields.
        /// </summary>
        public bool TrimFields { get; set; } = true;
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
        /// Default to Version when soft-delete is enabled, and LastWriteTime when not.
        /// </summary>
        Merge,

        /// <summary>
        /// Log conflicts for manual resolution.
        /// </summary>
        Manual
    }
}