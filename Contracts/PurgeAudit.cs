// -----------------------------------------------------------------------
// <copyright file="PurgeAudit.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

using System;
using System.Collections.Generic;

/// <summary>
/// Audit information for purge operations.
/// </summary>
public class PurgeAudit
{
    /// <summary>
    /// Gets or sets the purge timestamp.
    /// </summary>
    public DateTime PurgeTimestamp { get; set; }

    /// <summary>
    /// Gets or sets who initiated the purge.
    /// </summary>
    public string InitiatedBy { get; set; }

    /// <summary>
    /// Gets or sets the purge criteria used.
    /// </summary>
    public string PurgeCriteria { get; set; }


    /// <summary>
    /// Gets or sets the purge options used.
    /// </summary>
    public string OptionsUsed { get; set; }

    /// <summary>
    /// Gets or sets whether the purge completed successfully.
    /// </summary>
    public bool Completed { get; set; }

    /// <summary>
    /// Gets or sets additional audit metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}