// -----------------------------------------------------------------------
// <copyright file="PurgeSampleEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

using System;

/// <summary>
/// Sample entity information for purge preview.
/// </summary>
public class PurgeSampleEntity
{
    /// <summary>
    /// Gets or sets the entity key.
    /// </summary>
    public string EntityKey { get; set; }

    /// <summary>
    /// Gets or sets the entity type.
    /// </summary>
    public string EntityType { get; set; }

    /// <summary>
    /// Gets or sets the created time.
    /// </summary>
    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// Gets or sets the last write time.
    /// </summary>
    public DateTimeOffset LastWriteTime { get; set; }

    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    public long Version { get; set; }

    /// <summary>
    /// Gets or sets whether the entity is deleted.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the entity size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets why this entity is being purged.
    /// </summary>
    public string PurgeReason { get; set; }
}