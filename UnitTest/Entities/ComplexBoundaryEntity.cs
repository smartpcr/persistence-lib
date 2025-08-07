//-------------------------------------------------------------------------------
// <copyright file="ComplexBoundaryEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System;
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("BoundaryTestEntity")]
    public class ComplexBoundaryEntity : BaseEntity<string>
    {
        [Column("BigNumber", SqlDbType.BigInt)]
        public long BigNumber { get; set; }

        [Column("MaxDecimal", SqlDbType.Decimal, Precision = 38, Scale = 10)]
        public decimal MaxDecimal { get; set; }

        [Column("MinDateTime", SqlDbType.DateTime)]
        public DateTime MinDateTime { get; set; }

        [Column("MaxDateTime", SqlDbType.DateTime)]
        public DateTime MaxDateTime { get; set; }
    }
}