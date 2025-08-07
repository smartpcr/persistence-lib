// -----------------------------------------------------------------------
// <copyright file="ConflictType.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

/// <summary>
/// Type of conflict encountered during import.
/// </summary>
public enum ConflictType
{
    /// <summary>
    /// Version number conflict.
    /// </summary>
    Version,

    /// <summary>
    /// Data field conflict.
    /// </summary>
    Data,

    /// <summary>
    /// Schema mismatch.
    /// </summary>
    Schema,

    /// <summary>
    /// Constraint violation.
    /// </summary>
    Constraint
}