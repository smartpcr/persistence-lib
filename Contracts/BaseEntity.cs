//-------------------------------------------------------------------------------
// <copyright file="BaseEntity.cs" company="Microsoft Corp.">
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

    public abstract class BaseEntity<TKey> : IEntity<TKey> where TKey : IEquatable<TKey>
    {
        /// <summary>
        /// Gets or sets the cache key (primary key).
        /// </summary>
        [DataMember]
        [JsonProperty("CacheKey")]
        [PrimaryKey(Order = 1)]
        [Column("CacheKey", SqlDbType.Text, NotNull = true)]
        [Index("IX_CacheEntry_Key")]
        public TKey Id { get; set; }

        /// <summary>
        /// used for concurrency control, if soft-delete is enabled, this field is used to
        /// track the version of the entity and becomes part of the primary key and the
        /// [PrimaryKey(Order = 2)] attribute must be added in base class with new property.
        /// </summary>
        [DataMember]
        [JsonProperty("Version")]
        [AuditField(AuditFieldType.Version)]
        [Column("Version", SqlDbType.BigInt, NotNull = true)]
        [Index("IX_CacheEntry_Version")]
        public long Version { get; set; }

        [DataMember]
        [JsonProperty("CreatedTime")]
        [AuditField(AuditFieldType.CreatedTime)]
        [Column("CreatedTime", SqlDbType.Text, NotNull = true)]
        public DateTimeOffset CreatedTime { get; set; }

        [DataMember]
        [JsonProperty("LastWriteTime")]
        [AuditField(AuditFieldType.LastWriteTime)]
        [Column("LastWriteTime", SqlDbType.DateTime, NotNull = true)]
        [Index("IX_CacheEntry_LastWriteTime")]
        public DateTimeOffset LastWriteTime { get; set; }

        public virtual long EstimateEntitySize()
        {
            return MemorySizeEstimator.EstimateObjectSize(this);
        }
    }
}