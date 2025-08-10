// -----------------------------------------------------------------------
// <copyright file="SQLiteAuditProvider.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Audit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Traces;

    public class SQLiteAuditProvider : SQLitePersistenceProvider<AuditRecord, long>, IAuditProvider
    {
        public SQLiteAuditProvider(string connectionString, SqliteConfiguration configuration = null) : base(connectionString, configuration)
        {
        }

        public async Task AddAuditRecordAsync(AuditRecord auditRecord, CancellationToken cancel = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var entityType = auditRecord.EntityType ?? "Unknown";
            var entityId = auditRecord.EntityId ?? "Unknown";
            var auditOperation = auditRecord.Operation.ToString();

            PersistenceLogger.AuditWriteStart(entityType, entityId, auditOperation);

            try
            {
                await using var connection = await this.CreateAndOpenConnectionAsync(cancel);
                var context = CommandContext<AuditRecord, long>.ForInsert(auditRecord);
                context.CommandTimeout = this.Configuration.CommandTimeout;
                using var command = this.AuditMapper.CreateCommand(DbOperationType.Insert, context);
                command.Connection = connection;
                command.ExecuteNonQuery();

                stopwatch.Stop();
                PersistenceLogger.AuditWriteStop(entityType, entityId, auditOperation, stopwatch);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                PersistenceLogger.AuditWriteError(entityType, entityId, auditOperation, stopwatch, ex);
                throw;
            }
        }

        public async Task<List<AuditRecord>> GetAuditRecordsAsync(string entityType, List<AuditOperation> operations, CancellationToken cancel = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var entityId = "*"; // Reading all records for the entity type

            PersistenceLogger.AuditReadStart(entityType, entityId);

            try
            {
                await using var connection = await this.CreateAndOpenConnectionAsync(cancel);
                var records = new List<AuditRecord>();

                // Build the query based on operations filter
                var sql = "SELECT * FROM Audit WHERE EntityType = @entityType";
                if (operations?.Count > 0)
                {
                    var operationValues = string.Join(",", operations.Select(op => $"'{op.ToString()}'"));
                    sql += $" AND Operation IN ({operationValues})";
                }
                sql += " ORDER BY CreatedTime DESC";

                await using var command = new System.Data.SQLite.SQLiteCommand(sql, (System.Data.SQLite.SQLiteConnection)connection);
                command.Parameters.AddWithValue("@entityType", entityType);
                if (this.Configuration?.CommandTimeout != null)
                {
                    command.CommandTimeout = this.Configuration.CommandTimeout;
                }

                await using var reader = await command.ExecuteReaderAsync(cancel);
                while (await reader.ReadAsync(cancel))
                {
                    var record = this.AuditMapper.MapFromReader(reader);
                    records.Add(record);
                }

                stopwatch.Stop();
                PersistenceLogger.AuditReadStop(entityType, entityId, stopwatch, records.Count);

                return records;
            }
            catch (Exception)
            {
                stopwatch.Stop();
                // Since we don't have an AuditReadError method, we'll use the generic logging
                throw;
            }
        }
    }
}