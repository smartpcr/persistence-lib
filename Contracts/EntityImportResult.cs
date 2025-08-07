// -----------------------------------------------------------------------
// <copyright file="EntityImportResult.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    public class EntityImportResult
    {
        public ImportAction Action { get; set; }
        public ConflictDetail Conflict { get; set; }
        public string Error { get; set; }
    }
}
