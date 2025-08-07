//-------------------------------------------------------------------------------
// <copyright file="BaseMapperEntities.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.EntityMapping
{
    using System;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("TestEntity")]
    public class BaseMapperTestEntity : BaseEntity<Guid>
    {
        public new Guid Id { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
        public DateTime CreatedDate { get; set; }
        public decimal? Amount { get; set; }
        [NotMapped]
        public string Ignored { get; set; }
    }

    [Table("TestEntity", SoftDeleteEnabled = true)]
    public class BaseMapperSoftDeleteEntity : BaseMapperTestEntity, IVersionedEntity<Guid>
    {
        public bool IsDeleted { get; set; }
    }

    [Table("TestEntity", ExpirySpanString = "01:00:00")]
    public class BaseMapperExpiryEntity : BaseMapperTestEntity, IExpirableEntity<Guid>
    {
        public DateTimeOffset? AbsoluteExpiration { get; set; }
    }
}
