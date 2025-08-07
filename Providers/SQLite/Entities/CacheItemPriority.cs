//-------------------------------------------------------------------------------
// <copyright file="CacheItemPriority.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Entities
{
    /// <summary>
    /// Cache item priority for eviction policies.
    /// </summary>
    public enum CacheItemPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        NeverRemove = 3
    }
}