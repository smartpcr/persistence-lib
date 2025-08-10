// -----------------------------------------------------------------------
// <copyright file="SQLiteAuditMapper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Mappings
{
    using Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Resilience;

    public class SQLiteAuditMapper : SQLiteEntityMapper<AuditRecord, long>
    {
        public SQLiteAuditMapper(RetryPolicy retryPolicy) : base(retryPolicy)
        {
        }
    }
}