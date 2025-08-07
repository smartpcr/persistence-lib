// -----------------------------------------------------------------------
// <copyright file="ExportManifest.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

using System.Collections.Generic;

/// <summary>
/// Manifest for exported data.
/// </summary>
public class ExportManifest
{
    /// <summary>
    /// Gets or sets the export metadata.
    /// </summary>
    public ExportMetadata Metadata { get; set; }

    /// <summary>
    /// Gets or sets the list of data files.
    /// </summary>
    public List<ExportFileInfo> DataFiles { get; set; } = new List<ExportFileInfo>();

    /// <summary>
    /// Gets or sets export statistics.
    /// </summary>
    public ExportStatistics Statistics { get; set; }
}