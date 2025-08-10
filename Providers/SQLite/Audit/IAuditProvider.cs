// -----------------------------------------------------------------------
// <copyright file="IAuditProvider.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Audit
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

    public interface IAuditProvider
    {
        /// <summary>
        /// Adds an audit record to the database.
        /// This method is used to log changes made to entities that have audit trail enabled.
        /// It captures the entity type, operation performed, and any additional details.
        /// </summary>
        /// <param name="auditRecord">The audit record.</param>
        /// <param name="cancel">The cancellation token.</param>
        /// <returns>Completed task.</returns>
        Task AddAuditRecordAsync(AuditRecord auditRecord, CancellationToken cancel = default);

        /// <summary>
        /// Retrieves all audit records for a specific entity type and operations.
        /// </summary>
        /// <param name="entityType">The entity type.</param>
        /// <param name="operations">The audit operations, if null or empty, return all operations.</param>
        /// <param name="cancel">The cancellation token.</param>
        /// <returns>List of audit records.</returns>
        Task<List<AuditRecord>> GetAuditRecordsAsync(string entityType, List<AuditOperation> operations, CancellationToken cancel = default);
    }
}