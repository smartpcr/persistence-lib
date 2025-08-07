//-------------------------------------------------------------------------------
// <copyright file="BulkTestEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.BulkOperations
{
    using System;
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("BulkTestEntity")]
    public class BulkTestEntity : BaseEntity<Guid>
    {
        [Column("Name", SqlDbType.NVarChar, Size = 100)]
        public string Name { get; set; }

        [Column("Category", SqlDbType.NVarChar, Size = 50)]
        public string Category { get; set; }

        [Column("Value", SqlDbType.Int)]
        public int Value { get; set; }
    }
}
