// -----------------------------------------------------------------------
// <copyright file="ConflictDetail.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

/// <summary>
/// Details about a conflict during import.
/// </summary>
public class ConflictDetail
{
    /// <summary>
    /// Gets or sets the entity key that had a conflict.
    /// </summary>
    public string EntityKey { get; set; }

    /// <summary>
    /// Gets or sets the type of conflict.
    /// </summary>
    public ConflictType Type { get; set; }

    /// <summary>
    /// Gets or sets how the conflict was resolved.
    /// </summary>
    public ConflictResolution Resolution { get; set; }

    /// <summary>
    /// Gets or sets the source entity version.
    /// </summary>
    public long SourceVersion { get; set; }

    /// <summary>
    /// Gets or sets the target entity version.
    /// </summary>
    public long TargetVersion { get; set; }

    /// <summary>
    /// Gets or sets additional details about the conflict.
    /// </summary>
    public string Details { get; set; }
}