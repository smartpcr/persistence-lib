//-------------------------------------------------------------------------------
// <copyright file="TransactionState.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    /// <summary>
    /// Transaction states.
    /// </summary>
    public enum TransactionState
    {
        Active,
        Committing,
        Committed,
        RollingBack,
        Failed // after rollback is complete, set to Failed, no RolledBack state
    }
}