// -----------------------------------------------------------------------
// <copyright file="SQLitePersistenceProvider.Audit.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    public partial class SQLitePersistenceProvider<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        #region Audit Trail Operations

        /// <summary>
        /// Writes an audit record for an entity operation.
        /// </summary>
        private async Task WriteAuditRecordAsync(T entity, string action, CallerInfo callerInfo, T oldEntity, bool? cacheHit, CancellationToken cancellationToken)
        {
            if (this.auditMapper == null) return;
            if (entity == null) return;

            try
            {
                using var connection = await this.CreateAndOpenConnectionAsync(cancellationToken);

                // Create the audit record
                var auditRecord = AuditRecord.CreateAuditRecord<T, TKey>(
                    entity.Id,
                    action,
                    callerInfo,
                    entity.Version,
                    oldEntity?.Version ?? 0,
                    entity.EstimateEntitySize());

                // Set base entity tracking fields (CreatedTime and LastWriteTime will track when audit was created)
                auditRecord.CreatedTime = DateTimeOffset.UtcNow;
                auditRecord.LastWriteTime = auditRecord.CreatedTime;
                auditRecord.Version = 1; // Audit records don't need versioning

                // Insert the audit record
                var context = CommandContext<AuditRecord, long>.ForInsert(auditRecord);
                context.CommandTimeout = this.configuration.CommandTimeout;
                using var command = this.auditMapper.CreateCommand(DbOperationType.Insert, context);
                command.Connection = connection;
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                // Log the exception if a logger is available
                // For now, just swallow the exception to not affect main operation
                Debug.WriteLine($"Failed to write audit record: {ex.Message}");
            }
        }

        #endregion
    }
}
