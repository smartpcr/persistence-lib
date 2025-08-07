//-------------------------------------------------------------------------------
// <copyright file="AuditMapper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings
{
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

    /// <summary>
    /// Maps audit records to the unified Audit table.
    /// </summary>
    public class AuditMapper : BaseEntityMapper<AuditRecord, long>
    {
    }
}