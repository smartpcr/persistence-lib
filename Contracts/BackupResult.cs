// -----------------------------------------------------------------------
// <copyright file="BackupResult.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

using System;

/// <summary>
/// Backup result information.
/// </summary>
public class BackupResult
{
    /// <summary>
    /// Gets or sets whether the backup was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the backup path.
    /// </summary>
    public string BackupPath { get; set; }

    /// <summary>
    /// Gets or sets the count of entities backed up.
    /// </summary>
    public long EntitiesBackedUp { get; set; }

    /// <summary>
    /// Gets or sets the backup file size.
    /// </summary>
    public long BackupSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the backup duration.
    /// </summary>
    public TimeSpan BackupDuration { get; set; }

    /// <summary>
    /// Gets or sets any backup errors.
    /// </summary>
    public string Error { get; set; }
}