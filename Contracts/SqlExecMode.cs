//-------------------------------------------------------------------------------
// <copyright file="SqlExecMode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    public enum SqlExecMode
    {
        ExecuteNonQuery = 0,
        ExecuteScalar = 1,
        ExecuteReader = 2,
    }
}