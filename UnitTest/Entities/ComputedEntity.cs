//-------------------------------------------------------------------------------
// <copyright file="ComputedEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    /// <summary>
    /// Entity with computed columns for testing.
    /// </summary>
    [Table("EntityWithComputedColumns")]
    public class ComputedEntity : BaseEntity<string>
    {
        [Column("Quantity", SqlDbType.Int)]
        public int Quantity { get; set; }

        [Column("UnitPrice", SqlDbType.Decimal, Precision = 10, Scale = 2)]
        public decimal UnitPrice { get; set; }

        [Column("TotalPrice", SqlDbType.Decimal)]
        [Computed("Quantity * UnitPrice", IsPersisted = true)]
        public decimal TotalPrice { get; set; }

        [Column("FirstName", SqlDbType.NVarChar, Size = 50)]
        public string FirstName { get; set; }

        [Column("LastName", SqlDbType.NVarChar, Size = 50)]
        public string LastName { get; set; }

        [Column("FullName", SqlDbType.NVarChar)]
        [Computed("FirstName || ' ' || LastName", IsPersisted = false)]
        public string FullName { get; set; }

        [Column("CreatedYear", SqlDbType.Int)]
        [Computed("strftime('%Y', CreatedTime)")]
        public int CreatedYear { get; set; }
    }
}