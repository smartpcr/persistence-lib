// -----------------------------------------------------------------------
// <copyright file="PurgeStatistics.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

using System;
using System.Collections.Generic;

/// <summary>
/// Purge operation statistics.
/// </summary>
public class PurgeStatistics
{
    /// <summary>
    /// Gets or sets the total entities examined.
    /// </summary>
    public long TotalEntitiesExamined { get; set; }

    /// <summary>
    /// Gets or sets the count of entities that met purge criteria.
    /// </summary>
    public long EntitiesMatchingCriteria { get; set; }

    /// <summary>
    /// Gets or sets the count of list mappings removed.
    /// </summary>
    public long ListMappingsRemoved { get; set; }

    /// <summary>
    /// Gets or sets the count of version table entries updated.
    /// </summary>
    public long VersionTableUpdates { get; set; }

    /// <summary>
    /// Gets or sets the time spent on backup.
    /// </summary>
    public TimeSpan BackupTime { get; set; }

    /// <summary>
    /// Gets or sets the time spent on deletion.
    /// </summary>
    public TimeSpan DeletionTime { get; set; }

    /// <summary>
    /// Gets or sets the time spent on optimization.
    /// </summary>
    public TimeSpan OptimizationTime { get; set; }

    /// <summary>
    /// Gets or sets statistics by purge reason.
    /// </summary>
    public Dictionary<string, long> StatsByReason { get; set; } = new Dictionary<string, long>();
}