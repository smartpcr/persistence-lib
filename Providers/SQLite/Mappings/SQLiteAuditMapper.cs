// -----------------------------------------------------------------------
// <copyright file="SQLiteAuditMapper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Mappings
{
    using Contracts;

    public class SQLiteAuditMapper : SQLiteEntityMapper<AuditRecord, long>
    {
    }
}