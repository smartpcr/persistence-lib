// -----------------------------------------------------------------------
// <copyright file="ExportMode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

/// <summary>
/// Export mode for bulk operations.
/// </summary>
public enum ExportMode
{
    /// <summary>
    /// Export all entities matching the filter.
    /// </summary>
    Full,

    /// <summary>
    /// Export only entities modified since a specific date.
    /// </summary>
    Incremental,

    /// <summary>
    /// Export entities older than a threshold for archival.
    /// </summary>
    Archive
}