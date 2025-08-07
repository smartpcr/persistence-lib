//-------------------------------------------------------------------------------
// <copyright file="VersionEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Entities
{
    using System;
    using System.Data;
    using System.Runtime.Serialization;
    using Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a version entry in the global version sequence table.
    /// </summary>
    [DataContract]
    [Table("Version")]
    public class VersionEntity : BaseEntity<long>
    {
        /// <summary>
        /// Override Id to map to Version column as primary key.
        /// </summary>
        [DataMember]
        [JsonProperty("Version")]
        [PrimaryKey(IsAutoIncrement = true)]
        [Column("Version", SqlDbType.BigInt, NotNull = true)]
        public new long Id { get; set; }

        [NotMapped]
        public new long Version { get; set; }

        /// <summary>
        /// Override CreatedTime property from BaseEntity - not mapped in this table.
        /// </summary>
        [NotMapped]
        public new DateTimeOffset CreatedTime { get; set; }

        /// <summary>
        /// Override LastWriteTime property from BaseEntity - not mapped in this table.
        /// </summary>
        [NotMapped]
        public new DateTimeOffset LastWriteTime { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this version was created.
        /// SQLite doesn't allow non-deterministic functions like datetime('now') in generated columns.
        /// </summary>
        [DataMember]
        [JsonProperty("Timestamp")]
        [Column("Timestamp", SqlDbType.Text, NotNull = true, DefaultValue = "datetime('now')")]
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Creates a new version entry.
        /// </summary>
        /// <returns>A new Version instance.</returns>
        public static VersionEntity Create()
        {
            return new VersionEntity
            {
                Timestamp = DateTimeOffset.UtcNow
            };
        }

        /// <summary>
        /// Estimates the memory size of this entity.
        /// </summary>
        /// <returns>The estimated memory size in bytes.</returns>
        public override long EstimateEntitySize()
        {
            // Version entity is very small - just an ID and timestamp
            return 32; // Approximate size
        }
    }
}