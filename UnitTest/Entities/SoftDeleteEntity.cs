//-------------------------------------------------------------------------------
// <copyright file="SoftDeleteEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("TestEntityWithSoftDelete", SoftDeleteEnabled = true)]
    public class SoftDeleteEntity : BaseEntity<string>, IVersionedEntity<string>
    {
        [PrimaryKey(Order = 2)]
        [AuditField(AuditFieldType.Version)]
        [Column("Version", SqlDbType.BigInt, NotNull = true)]
        [Index("IX_CacheEntry_Version")]
        public new long Version
        {
            get => base.Version;
            set => base.Version = value;
        }

        [Column("Description", SqlDbType.Text)]
        public string Description { get; set; }

        [Column(NotNull = true, DefaultValue = false)]
        public bool IsDeleted { get; set; }
    }
}