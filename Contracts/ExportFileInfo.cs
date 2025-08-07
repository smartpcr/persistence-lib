// -----------------------------------------------------------------------
// <copyright file="ExportFileInfo.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

/// <summary>
/// Information about an exported file.
/// </summary>
public class ExportFileInfo
{
    /// <summary>
    /// Gets or sets the file name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the entity count in this file.
    /// </summary>
    public int EntityCount { get; set; }

    /// <summary>
    /// Gets or sets the file checksum.
    /// </summary>
    public string Checksum { get; set; }

    /// <summary>
    /// Gets or sets whether the file is compressed.
    /// </summary>
    public bool IsCompressed { get; set; }
}