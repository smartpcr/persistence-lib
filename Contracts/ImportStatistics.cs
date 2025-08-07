// -----------------------------------------------------------------------
// <copyright file="ImportStatistics.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

/// <summary>
/// Import operation statistics.
/// </summary>
public class ImportStatistics
{
    /// <summary>
    /// Gets or sets the total entities processed.
    /// </summary>
    public long TotalEntitiesProcessed { get; set; }

    /// <summary>
    /// Gets or sets the count of new entities created.
    /// </summary>
    public long EntitiesCreated { get; set; }

    /// <summary>
    /// Gets or sets the count of entities updated.
    /// </summary>
    public long EntitiesUpdated { get; set; }

    /// <summary>
    /// Gets or sets the count of entities skipped.
    /// </summary>
    public long EntitiesSkipped { get; set; }

    /// <summary>
    /// Gets or sets the count of conflicts resolved.
    /// </summary>
    public long ConflictsResolved { get; set; }

    /// <summary>
    /// Gets or sets the total versions imported (for soft-delete).
    /// </summary>
    public long VersionsImported { get; set; }

    /// <summary>
    /// Gets or sets the count of version chains validated.
    /// </summary>
    public long VersionChainsValidated { get; set; }
}