//-------------------------------------------------------------------------------
// <copyright file="BatchEntities.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.BatchOperations
{
    using System;
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("BatchTestEntity")]
    public class BatchTestEntity : BaseEntity<Guid>
    {
        [Column("Name", SqlDbType.NVarChar, Size = 100)]
        public string Name { get; set; }

        [Column("Status", SqlDbType.NVarChar, Size = 50)]
        public string Status { get; set; }

        [Column("Value", SqlDbType.Int)]
        public int Value { get; set; }
    }

    [Table("SoftDeleteBatchEntity", SoftDeleteEnabled = true)]
    public class SoftDeleteBatchEntity : BaseEntity<Guid>, IVersionedEntity<Guid>
    {
        [Column("Name", SqlDbType.NVarChar, Size = 100)]
        public string Name { get; set; }

        public bool IsDeleted { get; set; }
    }

    [Table("ExpiryBatchEntity", ExpirySpanString = "00:00:01")]
    public class ExpiryBatchEntity : BaseEntity<Guid>, IExpirableEntity<Guid>
    {
        [Column("Name", SqlDbType.NVarChar, Size = 100)]
        public string Name { get; set; }

        [Column("AbsoluteExpiration", SqlDbType.DateTimeOffset)]
        public DateTimeOffset? AbsoluteExpiration { get; set; }
    }
}
