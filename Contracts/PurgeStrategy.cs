// -----------------------------------------------------------------------
// <copyright file="PurgeStrategy.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

/// <summary>
/// Purge strategy for handling soft-delete entities.
/// </summary>
public enum PurgeStrategy
{
    /// <summary>
    /// Purge all versions if the newest is deleted, keep old versions if entity is active.
    /// </summary>
    PreserveActiveVersions,

    /// <summary>
    /// Purge all old versions regardless of entity state.
    /// </summary>
    PurgeAllOldVersions,

    /// <summary>
    /// Only purge entities marked as deleted.
    /// </summary>
    PurgeDeletedOnly,

    /// <summary>
    /// Purge entities that have exceeded their AbsoluteExpiration (requires EnableExpiry = true).
    /// </summary>
    PurgeExpired
}