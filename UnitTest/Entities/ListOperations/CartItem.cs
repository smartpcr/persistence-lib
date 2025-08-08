//-------------------------------------------------------------------------------
// <copyright file="CartItem.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.ListOperations
{
    using System;
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("CartItem", SyncWithList = true, SoftDeleteEnabled = false)]
    public class CartItem : BaseEntity<Guid>
    {
        [Column("ProductName", SqlDbType.NVarChar, Size = 100)]
        public string ProductName { get; set; }

        [Column("Quantity", SqlDbType.Int)]
        public int Quantity { get; set; }

        [Column("Price", SqlDbType.Decimal, Precision = 18, Scale = 2)]
        public decimal Price { get; set; }
    }
}
