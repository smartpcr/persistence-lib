//-------------------------------------------------------------------------------
// <copyright file="QueryTestEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.QueryOperations
{
    using System;
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("QueryTestEntity")]
    public class QueryTestEntity : BaseEntity<Guid>
    {
        [Column("Name", SqlDbType.NVarChar, Size = 100)]
        public string Name { get; set; }

        [Column("Status", SqlDbType.NVarChar, Size = 50)]
        public string Status { get; set; }

        [Column("Amount", SqlDbType.Int)]
        public int Amount { get; set; }

        [Column("Category", SqlDbType.NVarChar, Size = 50)]
        public string Category { get; set; }

        [Column("DateCreated", SqlDbType.DateTime)]
        public DateTime DateCreated { get; set; }
    }
}
