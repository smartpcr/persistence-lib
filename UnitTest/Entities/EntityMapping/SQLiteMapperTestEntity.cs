//-------------------------------------------------------------------------------
// <copyright file="SQLiteMapperTestEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.EntityMapping
{
    using System;
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("TestEntity")]
    public class SQLiteMapperTestEntity : BaseEntity<Guid>
    {
        [Column("Name", SqlDbType.NVarChar, Size = 100)]
        public string Name { get; set; }

        [Column("Count", SqlDbType.Int)]
        public int Count { get; set; }

        [Column("CreatedDate", SqlDbType.DateTime)]
        public DateTime CreatedDate { get; set; }

        [Column("Amount", SqlDbType.Decimal, Precision = 18, Scale = 6)]
        public decimal? Amount { get; set; }

        [Column("ComplexData", SqlDbType.Text)]
        public string ComplexData { get; set; }
    }
}
