// -----------------------------------------------------------------------
// <copyright file="ExportMetadata.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

using System;
using System.Collections.Generic;

/// <summary>
/// Metadata for export operations.
/// </summary>
public class ExportMetadata
{
    /// <summary>
    /// Gets or sets the schema version.
    /// </summary>
    public string SchemaVersion { get; set; }

    /// <summary>
    /// Gets or sets the export timestamp.
    /// </summary>
    public DateTime ExportTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the entity type name.
    /// </summary>
    public string EntityType { get; set; }

    /// <summary>
    /// Gets or sets the total entity count.
    /// </summary>
    public long EntityCount { get; set; }

    /// <summary>
    /// Gets or sets whether soft delete is enabled.
    /// </summary>
    public bool SoftDeleteEnabled { get; set; }

    /// <summary>
    /// Gets or sets the export mode used.
    /// </summary>
    public ExportMode ExportMode { get; set; }

    /// <summary>
    /// Gets or sets the filter criteria used.
    /// </summary>
    public string FilterCriteria { get; set; }

    /// <summary>
    /// Gets or sets additional properties.
    /// </summary>
    public Dictionary<string, object> AdditionalProperties { get; set; } = new Dictionary<string, object>();
}