// -----------------------------------------------------------------------
// <copyright file="BatchOperationsTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.BatchOperations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using BatchTestEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.BatchOperations.BatchTestEntity;
    using SoftDeleteBatchEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.BatchOperations.SoftDeleteBatchEntity;
    using ExpiryBatchEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.BatchOperations.ExpiryBatchEntity;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Providers;

    [TestClass]
    public class BatchOperationsTests : SQLiteTestBase
    {
        private string testDbPath;

        private string connectionString;
        private SQLitePersistenceProvider<BatchTestEntity, Guid> provider;
        private CallerInfo callerInfo;

        [TestInitialize]
        public async Task Setup()
        {
            this.testDbPath = Path.Combine(Directory.GetCurrentDirectory(), $"test_{Guid.NewGuid()}.db");
            this.connectionString = $"Data Source={this.testDbPath};Version=3;";
            this.provider = new SQLitePersistenceProvider<BatchTestEntity, Guid>(this.connectionString);
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
            }

            this.SafeDeleteDatabase(this.testDbPath);
        }

        [TestMethod]
        [TestCategory("BatchOperations")]
        public async Task CreateAsync_BatchInsert_Success()
        {
            // Arrange
            var entities = new List<BatchTestEntity>();
            for (var i = 0; i < 100; i++)
            {
                entities.Add(new BatchTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Entity {i}",
                    Status = "Active",
                    Value = i
                });
            }

            // Act
            var results = (await this.provider.CreateAsync(entities, this.callerInfo))?.ToList();

            // Assert
            results.Should().NotBeNull();
            results!.Count.Should().Be(100);
            results.All(r => r.Version == 1).Should().BeTrue();
            results.All(r => r.CreatedTime > DateTime.MinValue).Should().BeTrue();

            // Verify all entities were created
            var allEntities = await this.provider.GetAllAsync(this.callerInfo);
            allEntities.Count().Should().Be(100);
        }

        [TestMethod]
        [TestCategory("BatchOperations")]
        public async Task CreateAsync_BatchWithFailure_RollsBack()
        {
            // Arrange
            var duplicateId = Guid.NewGuid();
            var entities = new List<BatchTestEntity>
            {
                new BatchTestEntity { Id = Guid.NewGuid(), Name = "Entity 1" },
                new BatchTestEntity { Id = duplicateId, Name = "Entity 2" },
                new BatchTestEntity { Id = duplicateId, Name = "Duplicate" }, // This will fail
                new BatchTestEntity { Id = Guid.NewGuid(), Name = "Entity 4" }
            };

            // Act
            Func<Task> act = async () => await this.provider.CreateAsync(entities, this.callerInfo);
            await act.Should().ThrowAsync<AggregateException>("Failed to create 2 entities in batch*");

            var allEntities = await this.provider.GetAllAsync(this.callerInfo);
            allEntities.Count().Should().Be(0, "Transaction should have rolled back");
        }

        [TestMethod]
        [TestCategory("BatchOperations")]
        public async Task CreateAsync_CustomBatchSize_ProcessesInBatches()
        {
            // Arrange
            var customProvider = new SQLitePersistenceProvider<BatchTestEntity, Guid>(this.connectionString);
            await customProvider.InitializeAsync();

            var entities = new List<BatchTestEntity>();
            for (var i = 0; i < 25; i++)
            {
                entities.Add(new BatchTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Entity {i}",
                    Value = i
                });
            }

            // Act
            var results = await customProvider.CreateAsync(entities, this.callerInfo, batchSize: 10);

            // Assert
            results.Count().Should().Be(25);
            // With batch size 10, this should process in 3 batches (10, 10, 5)
            var allEntities = await customProvider.GetAllAsync(this.callerInfo);
            allEntities.Count().Should().Be(25);

            await customProvider.DisposeAsync();
        }

        [TestMethod]
        [TestCategory("BatchOperations")]
        public async Task GetAllAsync_ReturnsAllEntities()
        {
            // Arrange
            var entities = new List<BatchTestEntity>();
            for (int i = 0; i < 50; i++)
            {
                entities.Add(new BatchTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Entity {i}",
                    Status = i % 2 == 0 ? "Active" : "Inactive",
                    Value = i
                });
            }
            await this.provider.CreateAsync(entities, this.callerInfo);

            // Act
            var results = await this.provider.GetAllAsync(this.callerInfo);

            // Assert
            results.Should().NotBeNull();
            results.Count().Should().Be(50);
            results.Count(e => e.Status == "Active").Should().Be(25);
            results.Count(e => e.Status == "Inactive").Should().Be(25);
        }

        [TestMethod]
        [TestCategory("BatchOperations")]
        public async Task GetAllAsync_FiltersSoftDeleted()
        {
            // Arrange
            var provider = new SQLitePersistenceProvider<SoftDeleteBatchEntity, Guid>(this.connectionString);
            await provider.InitializeAsync();

            var entities = new List<SoftDeleteBatchEntity>();
            for (int i = 0; i < 10; i++)
            {
                entities.Add(new SoftDeleteBatchEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Entity {i}"
                });
            }
            var created = await provider.CreateAsync(entities, this.callerInfo);
            var createdArray = created.ToArray();

            // Delete some entities
            await provider.DeleteAsync(createdArray[0].Id, this.callerInfo);
            await provider.DeleteAsync(createdArray[2].Id, this.callerInfo);
            await provider.DeleteAsync(createdArray[4].Id, this.callerInfo);

            // Act
            var results = await provider.GetAllAsync(this.callerInfo);

            // Assert
            results.Count().Should().Be(7, "Should filter out soft-deleted entities");
            results.Any(e => e.Id == createdArray[0].Id).Should().BeFalse();
            results.Any(e => e.Id == createdArray[2].Id).Should().BeFalse();
            results.Any(e => e.Id == createdArray[4].Id).Should().BeFalse();

            await provider.DisposeAsync();
        }

        [TestMethod]
        [TestCategory("BatchOperations")]
        public async Task GetAllAsync_FiltersExpired()
        {
            // Arrange
            var provider = new SQLitePersistenceProvider<ExpiryBatchEntity, Guid>(this.connectionString);
            await provider.InitializeAsync();

            var entities = new List<ExpiryBatchEntity>();
            for (int i = 0; i < 5; i++)
            {
                entities.Add(new ExpiryBatchEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Entity {i}"
                });
            }
            await provider.CreateAsync(entities, this.callerInfo);

            // Wait for expiration
            await Task.Delay(1500);

            // Act
            var results = await provider.GetAllAsync(this.callerInfo);

            // Assert
            results.Count().Should().Be(0, "All entities should be expired and filtered out");

            await provider.DisposeAsync();
        }

        [TestMethod]
        [TestCategory("BatchOperations")]
        public async Task UpdateAsync_BatchUpdate_Success()
        {
            // Arrange
            var entities = new List<BatchTestEntity>();
            for (var i = 0; i < 20; i++)
            {
                entities.Add(new BatchTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Original {i}",
                    Status = "Active",
                    Value = i
                });
            }
            var created = await this.provider.CreateAsync(entities, this.callerInfo);

            // Act - Use batch update with update function
            var updated = (await this.provider.UpdateAsync(created, entity =>
            {
                entity.Name = entity.Name.Replace("Original", "Updated");
                entity.Status = "Modified";
                entity.Value = entity.Value * 2;
                return entity;
            }, this.callerInfo))?.ToList();

            // Assert
            updated.Should().NotBeNull();
            updated!.Count.Should().Be(20);
            updated.All(e => e.Name.StartsWith("Updated")).Should().BeTrue();
            updated.All(e => e.Status == "Modified").Should().BeTrue();
            updated.All(e => e.Version == 2).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("BatchOperations")]
        public async Task UpdateAsync_AppliesUpdateFunction()
        {
            // Arrange
            var entities = new List<BatchTestEntity>();
            for (var i = 0; i < 10; i++)
            {
                entities.Add(new BatchTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Entity {i}",
                    Status = "Pending",
                    Value = i
                });
            }
            var created = await this.provider.CreateAsync(entities, this.callerInfo);

            // Act
            var updated = await this.provider.UpdateAsync(
                created,
                entity =>
                {
                    entity.Status = "Processed";
                    entity.Value = entity.Value * 10;
                    return entity;
                },
                this.callerInfo);

            // Assert
            updated.Should().NotBeNull();
            updated.Count().Should().Be(10);
            updated.All(e => e.Status == "Processed").Should().BeTrue();
            updated.All(e => e.Value % 10 == 0).Should().BeTrue();
            updated.All(e => e.Version == 2).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("BatchOperations")]

        public async Task UpdateAsync_BatchConcurrencyConflict_Fails()
        {
            // Arrange
            var entities = new List<BatchTestEntity>();
            for (int i = 0; i < 5; i++)
            {
                entities.Add(new BatchTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Entity {i}",
                    Value = i
                });
            }
            var created = (await this.provider.CreateAsync(entities, this.callerInfo)).ToArray();

            // Simulate concurrent update on one entity
            created[2].Name = "Concurrent Update";
            await this.provider.UpdateAsync(created[2], this.callerInfo);

            // Try to update all with old versions
            foreach (var entity in created)
            {
                entity.Status = "Batch Update";
            }

            // Act - Should fail due to version mismatch on entity[2]
            await this.provider.UpdateAsync(created, entity => entity, this.callerInfo);
        }

        [TestMethod]
        [TestCategory("BatchOperations")]
        public async Task DeleteAsync_BatchDelete_Success()
        {
            // Arrange
            var entities = new List<BatchTestEntity>();
            var keysToDelete = new List<Guid>();

            for (int i = 0; i < 30; i++)
            {
                var entity = new BatchTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Entity {i}",
                    Value = i
                };
                entities.Add(entity);

                if (i % 3 == 0) // Delete every third entity
                {
                    keysToDelete.Add(entity.Id);
                }
            }
            await this.provider.CreateAsync(entities, this.callerInfo);

            // Act
            var result = await this.provider.DeleteAsync(keysToDelete, this.callerInfo);

            // Assert
            result.Should().BeGreaterThan(0);

            // Verify correct entities were deleted
            var remaining = await this.provider.GetAllAsync(this.callerInfo);
            remaining.Count().Should().Be(20); // 30 - 10 deleted
            remaining.Any(e => keysToDelete.Contains(e.Id)).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("BatchOperations")]
        public async Task DeleteAsync_MixedExistence_HandlesGracefully()
        {
            // Arrange
            var entities = new List<BatchTestEntity>();
            for (int i = 0; i < 5; i++)
            {
                entities.Add(new BatchTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Entity {i}"
                });
            }
            var created = (await this.provider.CreateAsync(entities, this.callerInfo)).ToArray();

            var keysToDelete = new List<Guid>
            {
                created[0].Id,                // Exists
                Guid.NewGuid(),               // Doesn't exist
                created[2].Id,                // Exists
                Guid.NewGuid(),               // Doesn't exist
                created[4].Id                 // Exists
            };

            // Act
            var result = await this.provider.DeleteAsync(keysToDelete, this.callerInfo);

            // Assert
            result.Should().BeGreaterThanOrEqualTo(0, "Delete should be idempotent and handle non-existent keys");

            var remaining = await this.provider.GetAllAsync(this.callerInfo);
            remaining.Count().Should().Be(2); // Only entities 1 and 3 should remain
            remaining.Any(e => e.Id == created[1].Id).Should().BeTrue();
            remaining.Any(e => e.Id == created[3].Id).Should().BeTrue();
        }
    }
}