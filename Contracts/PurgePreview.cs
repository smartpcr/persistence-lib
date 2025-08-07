// -----------------------------------------------------------------------
// <copyright file="PurgePreview.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

using System.Collections.Generic;

/// <summary>
/// Preview data for safe mode purge operations.
/// </summary>
public class PurgePreview
{
    /// <summary>
    /// Gets or sets the count of entities that would be purged.
    /// </summary>
    public long AffectedEntityCount { get; set; }

    /// <summary>
    /// Gets or sets the count of versions that would be purged.
    /// </summary>
    public long AffectedVersionCount { get; set; }

    /// <summary>
    /// Gets or sets sample entities that would be purged.
    /// </summary>
    public List<PurgeSampleEntity> SampleEntities { get; set; } = new List<PurgeSampleEntity>();

    /// <summary>
    /// Gets or sets the estimated space that would be reclaimed.
    /// </summary>
    public long EstimatedSpaceToReclaim { get; set; }

    /// <summary>
    /// Gets or sets statistics by entity state.
    /// </summary>
    public Dictionary<string, long> StatsByState { get; set; } = new Dictionary<string, long>();
}