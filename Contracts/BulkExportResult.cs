//-------------------------------------------------------------------------------
// <copyright file="BulkExportResult.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Result of a bulk export operation.
    /// </summary>
    public class BulkExportResult<T>
    {
        /// <summary>
        /// Gets or sets the exported entities (for in-memory exports).
        /// </summary>
        public IEnumerable<T> ExportedEntities { get; set; }

        /// <summary>
        /// Gets or sets the count of exported entities.
        /// </summary>
        public long ExportedCount { get; set; }

        /// <summary>
        /// Gets or sets the operation duration.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the export metadata.
        /// </summary>
        public ExportMetadata Metadata { get; set; }

        /// <summary>
        /// Gets or sets the list of exported files.
        /// </summary>
        public List<string> ExportedFiles { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the manifest file path.
        /// </summary>
        public string ManifestPath { get; set; }

        /// <summary>
        /// Gets or sets whether entities were marked as exported (archive mode).
        /// </summary>
        public bool EntitiesMarkedAsExported { get; set; }
    }
}