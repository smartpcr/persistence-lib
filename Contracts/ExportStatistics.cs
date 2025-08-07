// -----------------------------------------------------------------------
// <copyright file="ExportStatistics.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

/// <summary>
/// Export operation statistics.
/// </summary>
public class ExportStatistics
{
    /// <summary>
    /// Gets or sets the total entities processed.
    /// </summary>
    public long TotalEntitiesProcessed { get; set; }

    /// <summary>
    /// Gets or sets the total versions exported (for soft-delete).
    /// </summary>
    public long TotalVersionsExported { get; set; }

    /// <summary>
    /// Gets or sets the deleted entities included.
    /// </summary>
    public long DeletedEntitiesIncluded { get; set; }

    /// <summary>
    /// Gets or sets the total file size in bytes.
    /// </summary>
    public long TotalFileSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the compression ratio achieved.
    /// </summary>
    public double CompressionRatio { get; set; }
}