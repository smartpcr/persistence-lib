//-------------------------------------------------------------------------------
// <copyright file="IntegrationEntities.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.Integration
{
    using System;
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("Order", SoftDeleteEnabled = true, EnableAuditTrail = true)]
    public class Order : BaseEntity<Guid>, IVersionedEntity<Guid>
    {
        [Column("OrderNumber", SqlDbType.NVarChar, Size = 100)]
        public string OrderNumber { get; set; }

        [Column("CustomerName", SqlDbType.NVarChar, Size = 100)]
        public string CustomerName { get; set; }

        [Column("Status", SqlDbType.NVarChar, Size = 50)]
        public string Status { get; set; }

        [Column("TotalAmount", SqlDbType.Decimal, Precision = 18, Scale = 2)]
        public decimal TotalAmount { get; set; }

        [Column("OrderDate", SqlDbType.DateTime)]
        public DateTime OrderDate { get; set; }

        public bool IsDeleted { get; set; }
    }

    [Table("OrderItem")]
    public class OrderItem : BaseEntity<Guid>
    {
        [Column("OrderId", SqlDbType.UniqueIdentifier)]
        public Guid OrderId { get; set; }

        [Column("ProductName", SqlDbType.NVarChar, Size = 100)]
        public string ProductName { get; set; }

        [Column("Quantity", SqlDbType.Int)]
        public int Quantity { get; set; }

        [Column("UnitPrice", SqlDbType.Decimal, Precision = 18, Scale = 2)]
        public decimal UnitPrice { get; set; }
    }

    [Table("Product")]
    public class Product : BaseEntity<Guid>
    {
        [Column("Name", SqlDbType.NVarChar, Size = 100)]
        public string Name { get; set; }

        [Column("Category", SqlDbType.NVarChar, Size = 50)]
        public string Category { get; set; }

        [Column("Price", SqlDbType.Decimal, Precision = 18, Scale = 2)]
        public decimal Price { get; set; }

        [Column("StockQuantity", SqlDbType.Int)]
        public int StockQuantity { get; set; }
    }
}
