// -----------------------------------------------------------------------
// <copyright file="ImportMetadata.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

using System;

/// <summary>
/// Metadata about the import operation.
/// </summary>
public class ImportMetadata
{
    /// <summary>
    /// Gets or sets the import timestamp.
    /// </summary>
    public DateTime ImportTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the source manifest path (for file imports).
    /// </summary>
    public string SourceManifestPath { get; set; }

    /// <summary>
    /// Gets or sets the source schema version.
    /// </summary>
    public string SourceSchemaVersion { get; set; }

    /// <summary>
    /// Gets or sets whether schema validation passed.
    /// </summary>
    public bool SchemaValidationPassed { get; set; }

    /// <summary>
    /// Gets or sets the import strategy used.
    /// </summary>
    public ImportStrategy Strategy { get; set; }

    /// <summary>
    /// Gets or sets user information.
    /// </summary>
    public string ImportedBy { get; set; }
}