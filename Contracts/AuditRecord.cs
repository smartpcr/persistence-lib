//-------------------------------------------------------------------------------
// <copyright file="AuditRecord.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    using System;
    using System.Data;
    using System.Runtime.Serialization;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Newtonsoft.Json;

    /// <summary>
    /// Unified audit record for all entity operations.
    /// </summary>
    [Table("Audit", EnableAuditTrail = false, SoftDeleteEnabled = false)]
    public class AuditRecord : BaseEntity<long>
    {
        [PrimaryKey(Order = 1, IsAutoIncrement = true)]
        public new long Id { get; set; }

        [NotMapped]
        public new long Version { get; set; }

        [Column("EntityType", NotNull = true)]
        [Index("IX_Audit_EntityType_EntityId", Order = 1)]
        public string EntityType { get; set; }

        [Column("EntityId", NotNull = true)]
        [Index("IX_Audit_EntityType_EntityId", Order = 2)]
        public string EntityId { get; set; }

        [Column("Operation", NotNull = true)]
        [Index("IX_Audit_Operation")]
        public string Operation { get; set; }

        [Column("OldVersion")]
        public long? OldVersion { get; set; }

        [Column("CallerFile")]
        public string CallerFile { get; set; }

        [Column("CallerMember")]
        public string CallerMember { get; set; }

        [Column("CallerLineNumber")]
        public int CallerLineNumber { get; set; }

        [Column("Size")]
        public long? Size { get; set; }

        [Column("UserId")]
        public string UserId { get; set; }

        [Column("OldValue", SqlDbType.NVarChar)]
        public string OldValue { get; set; }

        [Column("NewValue", SqlDbType.NVarChar)]
        public string NewValue { get; set; }

        public static AuditRecord CreateAuditRecord<T, TKey>(
            TKey id,
            string operation,
            CallerInfo callerInfo,
            long? newVersion,
            long? oldVersion,
            long? size)
            where T : class, IEntity<TKey>
            where TKey : IEquatable<TKey>
        {
            var audit = new AuditRecord
            {
                EntityType = typeof(T).Name,
                EntityId = id?.ToString() ?? string.Empty,
                Operation = operation,
                CreatedTime = DateTimeOffset.UtcNow,
                LastWriteTime = DateTimeOffset.UtcNow,
                Version = newVersion ?? 0,
                OldVersion = oldVersion ?? 0,
                Size = size ?? 0,
                CallerFile = callerInfo?.CallerFilePath,
                CallerMember = callerInfo?.CallerMemberName,
                CallerLineNumber = callerInfo?.CallerLineNumber ?? 0,
                UserId = callerInfo?.UserId
            };

            return audit;
        }
    }
}