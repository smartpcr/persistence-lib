// -----------------------------------------------------------------------
// <copyright file="ImportAction.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    public enum ImportAction
    {
        Created,
        Updated,
        Skipped,
        Failed
    }
}
