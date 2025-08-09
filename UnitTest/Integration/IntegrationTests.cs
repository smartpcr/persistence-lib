// -----------------------------------------------------------------------
// <copyright file="IntegrationTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Integration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.Integration;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Providers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Integration")]
    public class IntegrationTests : SQLiteTestBase
    {
        private string testDbPath;

        private string connectionString;
        private SQLitePersistenceProvider<Order, Guid> orderProvider;
        private SQLitePersistenceProvider<OrderItem, Guid> orderItemProvider;
        private SQLitePersistenceProvider<Product, Guid> productProvider;
        private CallerInfo callerInfo;

        [TestInitialize]
        public async Task Setup()
        {
            this.testDbPath = Path.Combine(Directory.GetCurrentDirectory(), $"test_{Guid.NewGuid()}.db");
            this.connectionString = $"Data Source={this.testDbPath};Version=3;";
            var config = new SqliteConfiguration
            {
                JournalMode = JournalMode.WAL,
                CacheSize = 10000,
                EnableForeignKeys = false // Disabled due to SQLite limitation: each provider has its own connection,
                                          // so foreign key constraints fail when inserting related entities across providers.
                                          // To enable foreign keys, all related entities must use the same provider/connection.
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

            this.SafeDeleteDatabase(this.testDbPath);
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
            // Step 1: Create order first (must be committed before creating items due to foreign key)
            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = "ORD-2024-001",
                CustomerName = "John Doe",
                Status = "Pending",
                OrderDate = DateTime.UtcNow,
                TotalAmount = 0 // Will update later
            };

            order = await this.orderProvider.CreateAsync(order, this.callerInfo);

            // Get products for order
            var laptop = (await this.productProvider.QueryAsync(p => p.Name == "Laptop", null, this.callerInfo)).First();
            var mouse = (await this.productProvider.QueryAsync(p => p.Name == "Mouse", null, this.callerInfo)).First();

            // Step 2: Add items (after order is committed)
            var orderItems = new List<OrderItem>
            {
                new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, ProductName = laptop.Name, Quantity = 2, UnitPrice = laptop.Price },
                new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, ProductName = mouse.Name, Quantity = 4, UnitPrice = mouse.Price }
            };

            await this.orderItemProvider.CreateAsync(orderItems, this.callerInfo);

            // Calculate and update total
            order.TotalAmount = orderItems.Sum(i => i.Quantity * i.UnitPrice);
            order = await this.orderProvider.UpdateAsync(order, this.callerInfo);

            // Step 3: Update status (optimistic lock)
            order.Status = "Processing";
            order = await this.orderProvider.UpdateAsync(order, this.callerInfo);
            order.Version.Should().BeGreaterThan(0); // Version should increment after update

            // Step 4: Query orders (pagination)
            var pendingOrders = await this.orderProvider.QueryPagedAsync(
                o => o.Status == "Processing",
                10,
                1);

            pendingOrders.TotalCount.Should().Be(1);
            pendingOrders.Items.First().Id.Should().Be(order.Id);

            // Step 5: Archive old orders (bulk export)
            var exportDir = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid()}");
            Directory.CreateDirectory(exportDir);
            try
            {
                var exportResult = await this.orderProvider.BulkExportAsync(
                    options: new BulkExportOptions
                    {
                        ExportFolder = exportDir,
                        FileFormat = FileFormat.Json,
                        CompressOutput = false
                    });

                exportResult.ExportedCount.Should().Be(1);
            }
            finally
            {
                if (Directory.Exists(exportDir))
                {
                    Directory.Delete(exportDir, true);
                }
            }

            // Step 6: Purge archived (retention)
            order.Status = "Archived";
            order = await this.orderProvider.UpdateAsync(order, this.callerInfo);

            // First do a preview purge
            var previewResult = await this.orderProvider.PurgeAsync(
                o => o.Status == "Archived",
                new PurgeOptions { SafeMode = true }); // Preview mode

            previewResult.IsPreview.Should().BeTrue();
            previewResult.Preview.AffectedEntityCount.Should().Be(1);

            // Then do actual purge
            var purgeResult = await this.orderProvider.PurgeAsync(
                o => o.Status == "Archived",
                new PurgeOptions { SafeMode = false }); // Actually purge

            purgeResult.EntitiesPurged.Should().Be(1);

            // Note: Audit trail querying would need to be implemented separately
            // var auditTrail = await this.orderProvider.QueryAuditTrailAsync(
            //     entityId: order.Id.ToString(),
            //     callerInfo: this.callerInfo);
            // auditTrail.Count(.Should().BeTrue() >= 3); // CREATE, UPDATE (total), UPDATE (status)
        }

        [TestMethod]
        [TestCategory("Integration")]
        public async Task EndToEnd_OrderProcessingTransaction()
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = "ORD-2024-001",
                CustomerName = "John Doe",
                Status = "Pending",
                OrderDate = DateTime.UtcNow,
                Version = 1,
                TotalAmount = 0 // Will update later
            };

            // Get products for order
            var laptop = (await this.productProvider.QueryAsync(p => p.Name == "Laptop", null, this.callerInfo)).First();
            var mouse = (await this.productProvider.QueryAsync(p => p.Name == "Mouse", null, this.callerInfo)).First();

            // transaction cmd does not support concurrency check
            await using (var transactionScope = new TransactionScope(this.connectionString))
            {
                // Step 1: Create order first (must be committed before creating items due to foreign key)
                transactionScope.AddOperation<Order, Guid>(TransactionalOperation<Order, Guid>.Create(
                    new SQLiteEntityMapper<Order, Guid>(),
                    DbOperationType.Insert,
                    order));

                // Step 2: Add items (after order is committed)
                var orderItems = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        ProductName = laptop.Name,
                        Quantity = 2,
                        UnitPrice = laptop.Price
                    },
                    new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        ProductName = mouse.Name,
                        Quantity = 4,
                        UnitPrice = mouse.Price
                    }
                };
                foreach (var orderItem in orderItems)
                {
                    transactionScope.AddOperation<OrderItem, Guid>(TransactionalOperation<OrderItem, Guid>.Create(
                        new SQLiteEntityMapper<OrderItem, Guid>(),
                        DbOperationType.Insert,
                        orderItem));
                }

                // Calculate and update total
                order.TotalAmount = orderItems.Sum(i => i.Quantity * i.UnitPrice);
                order.Version += 1;
                order.LastWriteTime = DateTimeOffset.UtcNow;
                transactionScope.AddOperation<Order, Guid>(TransactionalOperation<Order, Guid>.Create(
                    new SQLiteEntityMapper<Order, Guid>(),
                    DbOperationType.Update,
                    order,
                    order));

                // Step 3: Update status (optimistic lock)
                order.Status = "Processing";
                order.Version += 1;
                order.LastWriteTime = DateTimeOffset.UtcNow;
                transactionScope.AddOperation<Order, Guid>(TransactionalOperation<Order, Guid>.Create(
                    new SQLiteEntityMapper<Order, Guid>(),
                    DbOperationType.Update,
                    order,
                    order));

                transactionScope.Commit();
            }

            order = await this.orderProvider.GetAsync(order.Id, this.callerInfo);
            order.Should().NotBeNull();
            order.Version.Should().Be(3); // starting at value 1, updated twice, once for total, once for status

            // Step 4: Query orders (pagination)
            var pendingOrders = await this.orderProvider.QueryPagedAsync(
                o => o.Status == "Processing",
                10,
                1);

            pendingOrders.TotalCount.Should().Be(1);
            pendingOrders.Items.First().Id.Should().Be(order.Id);
        }

        [TestMethod]
        [TestCategory("Integration")]
        public async Task EndToEnd_DataMigration()
        {
            // Step 1: Export existing data
            var exportResult = await this.productProvider.BulkExportAsync();
            var exportData = exportResult.ExportedEntities.ToList();

            // Step 2: Clear existing data
            var allProducts = await this.productProvider.GetAllAsync(this.callerInfo);
            await this.productProvider.DeleteAsync(allProducts.Select(p => p.Id).ToList(), this.callerInfo);

            // Verify cleared
            var remaining = await this.productProvider.CountAsync();
            remaining.Should().Be(0);

            // Step 3: Import data back
            var importResult = await this.productProvider.BulkImportAsync(
                exportData,
                new BulkImportOptions { ConflictResolution = ConflictResolution.UseSource });

            importResult.SuccessCount.Should().Be(exportData.Count);

            // Step 4: Verify migration
            var migratedProducts = await this.productProvider.GetAllAsync(this.callerInfo);
            migratedProducts.Count().Should().Be(exportData.Count);

            foreach (var original in exportData)
            {
                var migrated = migratedProducts.FirstOrDefault(p => p.Id == original.Id);
                migrated.Should().NotBeNull();
                migrated.Name.Should().Be(original.Name);
                migrated.Price.Should().Be(original.Price);
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
            importResult.Count().Should().Be(sqliteData.Count());

            var sqlServerData = await sqlServerProvider.GetAllAsync(this.callerInfo);
            sqlServerData.Count().Should().Be(sqliteData.Count());

            // Verify each product
            foreach (var sqliteProduct in sqliteData)
            {
                var sqlServerProduct = sqlServerData.FirstOrDefault(p => p.Id == sqliteProduct.Id);
                sqlServerProduct.Should().NotBeNull();
                sqlServerProduct.Name.Should().Be(sqliteProduct.Name);
                sqlServerProduct.Price.Should().Be(sqliteProduct.Price);
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
                                null,
                                this.callerInfo,
                                take: 10);
                            break;

                        case 2: // Update
                            var toUpdate = (await this.orderProvider.QueryAsync(
                                o => o.Status == "New",
                                null,
                                this.callerInfo,
                                take: 1)).FirstOrDefault();

                            if (toUpdate != null)
                            {
                                toUpdate.Status = "Processing";
                                await this.orderProvider.UpdateAsync(toUpdate, this.callerInfo);
                            }
                            break;

                        case 3: // Delete
                            var toDelete = (await this.orderProvider.QueryAsync(
                                o => o.Status == "Processing",
                                null,
                                this.callerInfo,
                                take: 1)).FirstOrDefault();

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
            operationCount.Should().BeGreaterThan(100, $"Should complete at least 100 operations in {duration.TotalSeconds} seconds, completed {operationCount}");
            errors.Count.Should().Be(0, $"No errors expected during sustained load, but got {errors.Count}");

            // Calculate throughput
            var throughput = operationCount / duration.TotalSeconds;
            Console.WriteLine($"Sustained throughput: {throughput:F2} operations/second");
        }
    }
}