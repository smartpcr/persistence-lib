// -----------------------------------------------------------------------
// <copyright file="IntegrationTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Integration
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Integration")]
    public class IntegrationTests
    {
        [Table("Order", SoftDeleteEnabled = true, EnableAuditTrail = true)]
        public class Order : IEntity<Guid>
        {
            public Guid Id { get; set; }
            public string OrderNumber { get; set; }
            public string CustomerName { get; set; }
            public string Status { get; set; }
            public decimal TotalAmount { get; set; }
            public DateTime OrderDate { get; set; }
            public int Version { get; set; }
            public bool IsDeleted { get; set; }
            public DateTime CreatedTime { get; set; }
            public DateTime LastWriteTime { get; set; }
        }

        [Table("OrderItem")]
        public class OrderItem : IEntity<Guid>
        {
            public Guid Id { get; set; }
            public Guid OrderId { get; set; }
            public string ProductName { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public int Version { get; set; }
            public DateTime CreatedTime { get; set; }
            public DateTime LastWriteTime { get; set; }
        }

        [Table("Product")]
        public class Product : IEntity<Guid>
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string Category { get; set; }
            public decimal Price { get; set; }
            public int StockQuantity { get; set; }
            public int Version { get; set; }
            public DateTime CreatedTime { get; set; }
            public DateTime LastWriteTime { get; set; }
        }

        private string connectionString;
        private SQLitePersistenceProvider<Order, Guid> orderProvider;
        private SQLitePersistenceProvider<OrderItem, Guid> orderItemProvider;
        private SQLitePersistenceProvider<Product, Guid> productProvider;
        private CallerInfo callerInfo;

        [TestInitialize]
        public async Task Setup()
        {
            this.connectionString = "Data Source=:memory:";
            var config = new SqliteConfiguration
            {
                EnableAuditTrail = true,
                JournalMode = "WAL",
                CacheSize = 10000
            };
            
            this.orderProvider = new SQLitePersistenceProvider<Order, Guid>(this.connectionString, config);
            this.orderItemProvider = new SQLitePersistenceProvider<OrderItem, Guid>(this.connectionString, config);
            this.productProvider = new SQLitePersistenceProvider<Product, Guid>(this.connectionString, config);
            
            await this.orderProvider.InitializeAsync();
            await this.orderItemProvider.InitializeAsync();
            await this.productProvider.InitializeAsync();
            
            this.callerInfo = new CallerInfo
            {
                UserId = "IntegrationTest",
                CorrelationId = Guid.NewGuid().ToString()
            };
            
            await this.SeedProducts();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (this.orderProvider != null) await this.orderProvider.DisposeAsync();
            if (this.orderItemProvider != null) await this.orderItemProvider.DisposeAsync();
            if (this.productProvider != null) await this.productProvider.DisposeAsync();
        }

        private async Task SeedProducts()
        {
            var products = new[]
            {
                new Product { Id = Guid.NewGuid(), Name = "Laptop", Category = "Electronics", Price = 999.99m, StockQuantity = 50 },
                new Product { Id = Guid.NewGuid(), Name = "Mouse", Category = "Electronics", Price = 29.99m, StockQuantity = 200 },
                new Product { Id = Guid.NewGuid(), Name = "Keyboard", Category = "Electronics", Price = 79.99m, StockQuantity = 150 },
                new Product { Id = Guid.NewGuid(), Name = "Monitor", Category = "Electronics", Price = 299.99m, StockQuantity = 75 },
                new Product { Id = Guid.NewGuid(), Name = "Desk", Category = "Furniture", Price = 499.99m, StockQuantity = 30 },
                new Product { Id = Guid.NewGuid(), Name = "Chair", Category = "Furniture", Price = 199.99m, StockQuantity = 100 }
            };
            
            await this.productProvider.CreateAsync(products.ToList(), this.callerInfo);
        }

        [TestMethod]
        [TestCategory("Integration")]
        public async Task EndToEnd_OrderProcessingWorkflow()
        {
            // Step 1: Create order (transaction)
            Order order;
            List<OrderItem> orderItems;
            
            using (var transaction = await this.orderProvider.BeginTransactionAsync())
            {
                order = new Order
                {
                    Id = Guid.NewGuid(),
                    OrderNumber = "ORD-2024-001",
                    CustomerName = "John Doe",
                    Status = "Pending",
                    OrderDate = DateTime.UtcNow
                };
                
                order = await transaction.CreateAsync(order, this.callerInfo);
                
                // Get products for order
                var laptop = (await this.productProvider.QueryAsync(p => p.Name == "Laptop", this.callerInfo)).First();
                var mouse = (await this.productProvider.QueryAsync(p => p.Name == "Mouse", this.callerInfo)).First();
                
                // Step 2: Add items (list operation)
                orderItems = new List<OrderItem>
                {
                    new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, ProductName = laptop.Name, Quantity = 2, UnitPrice = laptop.Price },
                    new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, ProductName = mouse.Name, Quantity = 4, UnitPrice = mouse.Price }
                };
                
                await this.orderItemProvider.CreateListAsync($"order:{order.Id}:items", orderItems, this.callerInfo);
                
                // Calculate total
                order.TotalAmount = orderItems.Sum(i => i.Quantity * i.UnitPrice);
                await transaction.UpdateAsync(order, this.callerInfo);
                
                await transaction.CommitAsync();
            }
            
            // Step 3: Update status (optimistic lock)
            order.Status = "Processing";
            order = await this.orderProvider.UpdateAsync(order, this.callerInfo);
            Assert.AreEqual(2, order.Version);
            
            // Step 4: Query orders (pagination)
            var pendingOrders = await this.orderProvider.QueryPagedAsync(
                o => o.Status == "Processing",
                pageSize: 10,
                pageNumber: 1,
                callerInfo: this.callerInfo);
            
            Assert.AreEqual(1, pendingOrders.TotalCount);
            Assert.AreEqual(order.Id, pendingOrders.Items.First().Id);
            
            // Step 5: Archive old orders (bulk export)
            var exportPath = Path.GetTempFileName();
            try
            {
                using var stream = File.OpenWrite(exportPath);
                var exportResult = await this.orderProvider.BulkExportAsync(
                    stream,
                    ExportFormat.Json,
                    this.callerInfo);
                
                Assert.IsTrue(exportResult.Success);
                Assert.AreEqual(1, exportResult.ExportedCount);
            }
            finally
            {
                File.Delete(exportPath);
            }
            
            // Step 6: Purge archived (retention)
            order.Status = "Archived";
            order.CreatedTime = DateTime.UtcNow.AddDays(-100); // Simulate old order
            await this.orderProvider.UpdateAsync(order, this.callerInfo);
            
            var purgeResult = await this.orderProvider.PurgeAsync(
                o => o.Status == "Archived" && o.CreatedTime < DateTime.UtcNow.AddDays(-90),
                this.callerInfo);
            
            Assert.IsTrue(purgeResult.Success);
            Assert.AreEqual(1, purgeResult.PurgedCount);
            
            // Verify audit trail
            var auditTrail = await this.orderProvider.QueryAuditTrailAsync(
                entityId: order.Id.ToString(),
                callerInfo: this.callerInfo);
            
            Assert.IsTrue(auditTrail.Count >= 3); // CREATE, UPDATE (total), UPDATE (status)
        }

        [TestMethod]
        [TestCategory("Integration")]
        public async Task EndToEnd_DataMigration()
        {
            // Step 1: Export existing data
            var exportData = new List<Product>();
            using (var exportStream = new MemoryStream())
            {
                var exportResult = await this.productProvider.BulkExportAsync(
                    exportStream,
                    ExportFormat.Json,
                    this.callerInfo);
                
                Assert.IsTrue(exportResult.Success);
                
                exportStream.Position = 0;
                using var reader = new StreamReader(exportStream);
                var json = await reader.ReadToEndAsync();
                exportData = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Product>>(json);
            }
            
            // Step 2: Clear existing data
            var allProducts = await this.productProvider.GetAllAsync(this.callerInfo);
            await this.productProvider.DeleteAsync(allProducts.Select(p => p.Id).ToList(), this.callerInfo);
            
            // Verify cleared
            var remaining = await this.productProvider.CountAsync(null, this.callerInfo);
            Assert.AreEqual(0, remaining);
            
            // Step 3: Import data back
            var importResult = await this.productProvider.BulkImportAsync(
                exportData,
                new BulkImportOptions { ConflictResolution = ConflictResolution.Overwrite },
                this.callerInfo);
            
            Assert.IsTrue(importResult.Success);
            Assert.AreEqual(exportData.Count, importResult.ImportedCount);
            
            // Step 4: Verify migration
            var migratedProducts = await this.productProvider.GetAllAsync(this.callerInfo);
            Assert.AreEqual(exportData.Count, migratedProducts.Count);
            
            foreach (var original in exportData)
            {
                var migrated = migratedProducts.FirstOrDefault(p => p.Id == original.Id);
                Assert.IsNotNull(migrated);
                Assert.AreEqual(original.Name, migrated.Name);
                Assert.AreEqual(original.Price, migrated.Price);
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        public async Task ProviderSwitch_SQLiteToSqlServer()
        {
            // Note: This test simulates provider switching
            // In real scenario, SQL Server provider would be used
            
            // Step 1: Export from SQLite
            var sqliteData = await this.productProvider.GetAllAsync(this.callerInfo);
            
            // Step 2: Simulate SQL Server provider (using another SQLite instance)
            var sqlServerConnectionString = "Data Source=:memory:";
            var sqlServerProvider = new SQLitePersistenceProvider<Product, Guid>(sqlServerConnectionString);
            await sqlServerProvider.InitializeAsync();
            
            // Step 3: Import to "SQL Server"
            var importResult = await sqlServerProvider.CreateAsync(sqliteData, this.callerInfo);
            
            // Step 4: Verify data integrity
            Assert.AreEqual(sqliteData.Count, importResult.Count);
            
            var sqlServerData = await sqlServerProvider.GetAllAsync(this.callerInfo);
            Assert.AreEqual(sqliteData.Count, sqlServerData.Count);
            
            // Verify each product
            foreach (var sqliteProduct in sqliteData)
            {
                var sqlServerProduct = sqlServerData.FirstOrDefault(p => p.Id == sqliteProduct.Id);
                Assert.IsNotNull(sqlServerProduct);
                Assert.AreEqual(sqliteProduct.Name, sqlServerProduct.Name);
                Assert.AreEqual(sqliteProduct.Price, sqlServerProduct.Price);
            }
            
            await sqlServerProvider.DisposeAsync();
        }

        [TestMethod]
        [TestCategory("Integration")]
        public async Task HighLoad_SustainedThroughput()
        {
            // Simulate sustained high load
            var duration = TimeSpan.FromSeconds(5);
            var startTime = DateTime.UtcNow;
            var operationCount = 0;
            var errors = new List<Exception>();
            
            // Run operations for specified duration
            while (DateTime.UtcNow - startTime < duration)
            {
                try
                {
                    // Mix of operations
                    var operation = operationCount % 4;
                    
                    switch (operation)
                    {
                        case 0: // Create
                            var newOrder = new Order
                            {
                                Id = Guid.NewGuid(),
                                OrderNumber = $"LOAD-{operationCount}",
                                CustomerName = $"Customer {operationCount}",
                                Status = "New",
                                TotalAmount = operationCount * 10m,
                                OrderDate = DateTime.UtcNow
                            };
                            await this.orderProvider.CreateAsync(newOrder, this.callerInfo);
                            break;
                            
                        case 1: // Read
                            var orders = await this.orderProvider.QueryAsync(
                                o => o.Status == "New",
                                take: 10,
                                callerInfo: this.callerInfo);
                            break;
                            
                        case 2: // Update
                            var toUpdate = (await this.orderProvider.QueryAsync(
                                o => o.Status == "New",
                                take: 1,
                                callerInfo: this.callerInfo)).FirstOrDefault();
                            
                            if (toUpdate != null)
                            {
                                toUpdate.Status = "Processing";
                                await this.orderProvider.UpdateAsync(toUpdate, this.callerInfo);
                            }
                            break;
                            
                        case 3: // Delete
                            var toDelete = (await this.orderProvider.QueryAsync(
                                o => o.Status == "Processing",
                                take: 1,
                                callerInfo: this.callerInfo)).FirstOrDefault();
                            
                            if (toDelete != null)
                            {
                                await this.orderProvider.DeleteAsync(toDelete.Id, this.callerInfo);
                            }
                            break;
                    }
                    
                    operationCount++;
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }
            
            // Assert
            Assert.IsTrue(operationCount > 100, $"Should complete at least 100 operations in {duration.TotalSeconds} seconds, completed {operationCount}");
            Assert.AreEqual(0, errors.Count, $"No errors expected during sustained load, but got {errors.Count}");
            
            // Calculate throughput
            var throughput = operationCount / duration.TotalSeconds;
            Console.WriteLine($"Sustained throughput: {throughput:F2} operations/second");
        }
    }
}