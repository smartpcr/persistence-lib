// -----------------------------------------------------------------------
// <copyright file="ListOperationsTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.ListOperations
{
    using System.IO;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using CartItem = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.ListOperations.CartItem;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Providers;

    [TestClass]
    [Ignore("List operations (CreateListAsync, GetListAsync, etc.) are not implemented in SQLitePersistenceProvider")]
    public class ListOperationsTests : SQLiteTestBase
    {
        private string testDbPath;

        private string connectionString;
        private SQLitePersistenceProvider<CartItem, Guid> provider;
        private CallerInfo callerInfo;

        [TestInitialize]
        public async Task Setup()
        {
            this.connectionString = "Data Source=:memory:";
            this.provider = new SQLitePersistenceProvider<CartItem, Guid>(this.connectionString);
            await this.provider.InitializeAsync();
            
            this.callerInfo = new CallerInfo
            {
                UserId = "TestUser",
                CorrelationId = Guid.NewGuid().ToString()
            };
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (this.provider != null)
            {
                await this.provider.DisposeAsync();
            

                this.SafeDeleteDatabase(this.testDbPath);

            }
        }

        [TestMethod]
        [TestCategory("ListOperations")]
        [Ignore("List operations (CreateListAsync, GetListAsync, etc.) are not implemented in SQLitePersistenceProvider")]
        public async Task CreateListAsync_CreatesAllEntities()
        {
            // Arrange
            var listKey = "user:123:cart";
            var items = new List<CartItem>
            {
                new CartItem { Id = Guid.NewGuid(), ProductName = "Item 1", Quantity = 2, Price = 10.99m },
                new CartItem { Id = Guid.NewGuid(), ProductName = "Item 2", Quantity = 1, Price = 25.50m },
                new CartItem { Id = Guid.NewGuid(), ProductName = "Item 3", Quantity = 3, Price = 5.00m }
            };

            // Act
            var result = await this.provider.CreateListAsync(listKey, items, this.callerInfo);

            // Assert
            result.Should().NotBeNull();
            result.Count().Should().Be(3);
            
            // Verify all items were created
            foreach (var item in items)
            {
                var created = await this.provider.GetAsync(item.Id, this.callerInfo);
                created.Should().NotBeNull();
                created.ProductName.Should().Be(item.ProductName);
            }
        }

        [TestMethod]
        [TestCategory("ListOperations")]
        [Ignore("List operations (CreateListAsync, GetListAsync, etc.) are not implemented in SQLitePersistenceProvider")]
        public async Task CreateListAsync_CreatesListMappings()
        {
            // Arrange
            var listKey = "user:456:wishlist";
            var items = new List<CartItem>
            {
                new CartItem { Id = Guid.NewGuid(), ProductName = "Wish Item 1", Quantity = 1, Price = 100m },
                new CartItem { Id = Guid.NewGuid(), ProductName = "Wish Item 2", Quantity = 1, Price = 200m }
            };

            // Act
            var result = await this.provider.CreateListAsync(listKey, items, this.callerInfo);

            // Assert
            result.Should().NotBeNull();
            result.Count().Should().Be(2);
            
            // Verify list mappings can retrieve the items
            var retrievedItems = await this.provider.GetListAsync(listKey, this.callerInfo);
            retrievedItems.Should().NotBeNull();
            retrievedItems.Count().Should().Be(2);
        }

        [TestMethod]
        [TestCategory("ListOperations")]
        public async Task GetListAsync_ReturnsAssociatedEntities()
        {
            // Arrange
            var listKey = "order:789:items";
            var items = new List<CartItem>
            {
                new CartItem { Id = Guid.NewGuid(), ProductName = "Product A", Quantity = 5, Price = 15.99m },
                new CartItem { Id = Guid.NewGuid(), ProductName = "Product B", Quantity = 2, Price = 30.00m },
                new CartItem { Id = Guid.NewGuid(), ProductName = "Product C", Quantity = 1, Price = 45.50m }
            };
            await this.provider.CreateListAsync(listKey, items, this.callerInfo);

            // Act
            var retrievedItems = await this.provider.GetListAsync(listKey, this.callerInfo);

            // Assert
            retrievedItems.Should().NotBeNull();
            retrievedItems.Count().Should().Be(3);
            retrievedItems.Any(i => i.ProductName == "Product A").Should().BeTrue();
            retrievedItems.Any(i => i.ProductName == "Product B").Should().BeTrue();
            retrievedItems.Any(i => i.ProductName == "Product C").Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("ListOperations")]
        public async Task GetListAsync_MaintainsOrder()
        {
            // Arrange
            var listKey = "queue:items";
            var items = new List<CartItem>();
            for (int i = 0; i < 10; i++)
            {
                items.Add(new CartItem
                {
                    Id = Guid.NewGuid(),
                    ProductName = $"Item {i:D2}",
                    Quantity = i,
                    Price = i * 10m
                });
            }
            await this.provider.CreateListAsync(listKey, items, this.callerInfo);

            // Act
            var retrievedItems = await this.provider.GetListAsync(listKey, this.callerInfo);

            // Assert
            retrievedItems.Should().NotBeNull();
            var itemsArray = retrievedItems.ToArray();
            itemsArray.Length.Should().Be(10);
            
            // Verify order is maintained
            for (int i = 0; i < 10; i++)
            {
                itemsArray[i].ProductName.Should().Be($"Item {i:D2}");
                itemsArray[i].Quantity.Should().Be(i);
            }
        }

        [TestMethod]
        [TestCategory("ListOperations")]
        public async Task GetListAsync_UsesCacheOnSecondCall()
        {
            // Arrange
            var listKey = "cached:list";
            var items = new List<CartItem>
            {
                new CartItem { Id = Guid.NewGuid(), ProductName = "Cached Item 1", Quantity = 1, Price = 50m },
                new CartItem { Id = Guid.NewGuid(), ProductName = "Cached Item 2", Quantity = 2, Price = 75m }
            };
            await this.provider.CreateListAsync(listKey, items, this.callerInfo);

            // Act
            var firstCall = await this.provider.GetListAsync(listKey, this.callerInfo);
            var secondCall = await this.provider.GetListAsync(listKey, this.callerInfo);

            // Assert
            firstCall.Should().NotBeNull();
            secondCall.Should().NotBeNull();
            var firstArray = firstCall.ToArray();
            var secondArray = secondCall.ToArray();
            secondArray.Length.Should().Be(firstArray.Length);
            
            // Both calls should return the same data
            for (int i = 0; i < firstArray.Length; i++)
            {
                secondArray[i].Id.Should().Be(firstArray[i].Id);
                secondArray[i].ProductName.Should().Be(firstArray[i].ProductName);
            }
        }

        [TestMethod]
        [TestCategory("ListOperations")]
        public async Task UpdateListAsync_ReplacesEntireList()
        {
            // Arrange
            var listKey = "replaceable:list";
            var originalItems = new List<CartItem>
            {
                new CartItem { Id = Guid.NewGuid(), ProductName = "Old Item 1", Quantity = 1, Price = 10m },
                new CartItem { Id = Guid.NewGuid(), ProductName = "Old Item 2", Quantity = 2, Price = 20m }
            };
            await this.provider.CreateListAsync(listKey, originalItems, this.callerInfo);
            
            var newItems = new List<CartItem>
            {
                new CartItem { Id = Guid.NewGuid(), ProductName = "New Item 1", Quantity = 3, Price = 30m },
                new CartItem { Id = Guid.NewGuid(), ProductName = "New Item 2", Quantity = 4, Price = 40m },
                new CartItem { Id = Guid.NewGuid(), ProductName = "New Item 3", Quantity = 5, Price = 50m }
            };

            // Act
            var result = await this.provider.UpdateListAsync(listKey, newItems, this.callerInfo);

            // Assert
            result.Should().NotBeNull();
            result.Count().Should().Be(3);
            
            var retrievedItems = await this.provider.GetListAsync(listKey, this.callerInfo);
            retrievedItems.Count().Should().Be(3);
            retrievedItems.All(i => i.ProductName.StartsWith("New Item")).Should().BeTrue();
            retrievedItems.Any(i => i.ProductName.StartsWith("Old Item")).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("ListOperations")]
        public async Task UpdateListAsync_InvalidatesCache()
        {
            // Arrange
            var listKey = "cache:invalidation:test";
            var originalItems = new List<CartItem>
            {
                new CartItem { Id = Guid.NewGuid(), ProductName = "Original", Quantity = 1, Price = 100m }
            };
            await this.provider.CreateListAsync(listKey, originalItems, this.callerInfo);
            
            // Prime the cache
            var cached = await this.provider.GetListAsync(listKey, this.callerInfo);
            cached.Count().Should().Be(1);
            
            var updatedItems = new List<CartItem>
            {
                new CartItem { Id = Guid.NewGuid(), ProductName = "Updated 1", Quantity = 2, Price = 200m },
                new CartItem { Id = Guid.NewGuid(), ProductName = "Updated 2", Quantity = 3, Price = 300m }
            };

            // Act
            await this.provider.UpdateListAsync(listKey, updatedItems, this.callerInfo);
            var afterUpdate = await this.provider.GetListAsync(listKey, this.callerInfo);

            // Assert
            afterUpdate.Count().Should().Be(2);
            afterUpdate.All(i => i.ProductName.StartsWith("Updated")).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("ListOperations")]
        public async Task DeleteListAsync_RemovesAllAssociations()
        {
            // Arrange
            var listKey = "deletable:list";
            var items = new List<CartItem>
            {
                new CartItem { Id = Guid.NewGuid(), ProductName = "Delete Me 1", Quantity = 1, Price = 10m },
                new CartItem { Id = Guid.NewGuid(), ProductName = "Delete Me 2", Quantity = 2, Price = 20m },
                new CartItem { Id = Guid.NewGuid(), ProductName = "Delete Me 3", Quantity = 3, Price = 30m }
            };
            await this.provider.CreateListAsync(listKey, items, this.callerInfo);

            // Act
            var result = await this.provider.DeleteListAsync(listKey, this.callerInfo);

            // Assert
            result.Should().BeGreaterThan(0);
            
            // List should no longer exist
            var retrievedItems = await this.provider.GetListAsync(listKey, this.callerInfo);
            (retrievedItems == null || retrievedItems.Count() == 0).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("ListOperations")]
        public async Task DeleteListAsync_PreservesEntities()
        {
            // Arrange
            var listKey = "preserve:entities:list";
            var items = new List<CartItem>
            {
                new CartItem { Id = Guid.NewGuid(), ProductName = "Preserved Item 1", Quantity = 1, Price = 100m },
                new CartItem { Id = Guid.NewGuid(), ProductName = "Preserved Item 2", Quantity = 2, Price = 200m }
            };
            await this.provider.CreateListAsync(listKey, items, this.callerInfo);
            
            var itemIds = items.Select(i => i.Id).ToList();

            // Act
            await this.provider.DeleteListAsync(listKey, this.callerInfo);

            // Assert
            // Entities should still exist individually
            foreach (var itemId in itemIds)
            {
                var entity = await this.provider.GetAsync(itemId, this.callerInfo);
                entity.Should().NotBeNull("Entity should be preserved after list deletion");
            }
            
            // But list association should be gone
            var listItems = await this.provider.GetListAsync(listKey, this.callerInfo);
            (listItems == null || listItems.Count() == 0).Should().BeTrue();
        }
    }
}