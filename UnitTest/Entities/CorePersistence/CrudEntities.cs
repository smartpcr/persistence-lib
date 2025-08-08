//-------------------------------------------------------------------------------
// <copyright file="CrudEntities.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.CorePersistence
{
    using System;
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("TestEntity")]
    public class CrudTestEntity : BaseEntity<Guid>
    {
        [PrimaryKey]
        [Column("Id", SqlDbType.UniqueIdentifier, NotNull = true)]
        public new Guid Id { get; set; }

        [Column("Name", SqlDbType.NVarChar, Size = 100)]
        public string Name { get; set; }

        [Column("Status", SqlDbType.NVarChar, Size = 50)]
        public string Status { get; set; }
    }

    [Table("SoftDeleteEntity", SoftDeleteEnabled = true)]
    public class CrudSoftDeleteEntity : BaseEntity<Guid>, IVersionedEntity<Guid>
    {
        [Column("Name", SqlDbType.NVarChar, Size = 100)]
        public string Name { get; set; }

        public bool IsDeleted { get; set; }
    }

    [Table("ExpiryEntity", ExpirySpanString = "00:00:01")]
    public class CrudExpiryEntity : BaseEntity<Guid>, IExpirableEntity<Guid>
    {
        [Column("Name", SqlDbType.NVarChar, Size = 100)]
        public string Name { get; set; }

        [Column("AbsoluteExpiration", SqlDbType.DateTimeOffset)]
        public DateTimeOffset? AbsoluteExpiration { get; set; }
    }
}
