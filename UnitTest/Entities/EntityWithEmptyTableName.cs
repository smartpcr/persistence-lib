// -----------------------------------------------------------------------
// <copyright file="EntityWithEmptyTableName.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("", SoftDeleteEnabled = true)]
    public class EntityWithEmptyTableName : BaseEntity<string>, IVersionedEntity<string>
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

        [Column("Name", SqlDbType.Text)]
        public string Name { get; set; }

        public bool IsDeleted { get; set; }
    }
}
