//-------------------------------------------------------------------------------
// <copyright file="DbOperationType.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    public enum DbOperationType
    {
        Select,
        Insert,
        Update,
        Delete,
        BatchInsert,
        Upsert,
        Merge,
    }
}