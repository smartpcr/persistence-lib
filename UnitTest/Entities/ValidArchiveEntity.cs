//-------------------------------------------------------------------------------
// <copyright file="ValidArchiveEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System;
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("ValidArchiveEntity", ExpirySpanString = "30.00:00:00", EnableArchive = true)]
    public class ValidArchiveEntity : BaseEntity<string>, IExpirableEntity<string>
    {
        [Column("Data", SqlDbType.Text)]
        public string Data { get; set; }

        [Column("CreationTime", SqlDbType.DateTimeOffset)]
        public DateTimeOffset CreationTime { get; set; }

        [Column("AbsoluteExpiration", SqlDbType.DateTimeOffset)]
        public DateTimeOffset? AbsoluteExpiration { get; set; }

        [Column("IsArchived", SqlDbType.Bit)]
        public bool IsArchived { get; set; }
    }
}