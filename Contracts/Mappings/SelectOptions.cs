// -----------------------------------------------------------------------
// <copyright file="SelectOptions.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

/// <summary>
/// Options for SELECT operations.
/// </summary>
public class SelectOptions
{
    /// <summary>
    /// Gets or sets whether to include all versions of versioned entities.
    /// </summary>
    public bool IncludeAllVersions { get; set; }

    /// <summary>
    /// Gets or sets whether to include soft-deleted entities.
    /// </summary>
    public bool IncludeDeleted { get; set; }

    /// <summary>
    /// Gets or sets whether to include expired entities.
    /// </summary>
    public bool IncludeExpired { get; set; }

    /// <summary>
    /// Gets or sets the ORDER BY clause.
    /// </summary>
    public string OrderBy { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of records to return.
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets the number of records to skip.
    /// </summary>
    public int? Offset { get; set; }

    /// <summary>
    /// Gets or sets custom WHERE clause conditions.
    /// </summary>
    public string WhereClause { get; set; }

    /// <summary>
    /// Gets a hash code for caching purposes.
    /// </summary>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 23 + this.IncludeAllVersions.GetHashCode();
            hash = hash * 23 + this.IncludeDeleted.GetHashCode();
            hash = hash * 23 + this.IncludeExpired.GetHashCode();
            hash = hash * 23 + (this.OrderBy?.GetHashCode() ?? 0);
            hash = hash * 23 + (this.Limit?.GetHashCode() ?? 0);
            hash = hash * 23 + (this.Offset?.GetHashCode() ?? 0);
            hash = hash * 23 + (this.WhereClause?.GetHashCode() ?? 0);
            return hash;
        }
    }
}