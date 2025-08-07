// -----------------------------------------------------------------------
// <copyright file="BatchOperationsTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.BatchOperations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Errors;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using BatchTestEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.BatchOperations.BatchTestEntity;
    using SoftDeleteBatchEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.BatchOperations.SoftDeleteBatchEntity;
    using ExpiryBatchEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.BatchOperations.ExpiryBatchEntity;

    [TestClass]
    public class BatchOperationsTests
    {
        private string connectionString;
        private SQLitePersistenceProvider<BatchTestEntity, Guid> provider;
        private CallerInfo callerInfo;

        [TestInitialize]
        public async Task Setup()
        {
            this.connectionString = "Data Source=:memory:";
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
        }

        [TestMethod]
        [TestCategory("BatchOperations")]
        public async Task CreateAsync_BatchInsert_Success()
        {
            // Arrange
            var entities = new List<BatchTestEntity>();
            for (int i = 0; i < 100; i++)
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
            var results = await this.provider.CreateAsync(entities, this.callerInfo);

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(100, results.Count());
            Assert.IsTrue(results.All(r => r.Version == 1));
            Assert.IsTrue(results.All(r => r.CreatedTime > DateTime.MinValue));
            
            // Verify all entities were created
            var allEntities = await this.provider.GetAllAsync(this.callerInfo);
            Assert.AreEqual(100, allEntities.Count());
        }

        [TestMethod]
        [TestCategory("BatchOperations")]
        [ExpectedException(typeof(AggregateException))]
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
            try
            {
                await this.provider.CreateAsync(entities, this.callerInfo);
            }
            catch (AggregateException)
            {
                // Verify rollback - no entities should exist
                var allEntities = await this.provider.GetAllAsync(this.callerInfo);
                Assert.AreEqual(0, allEntities.Count(), "Transaction should have rolled back");
                throw;
            }
        }

        [TestMethod]
        [TestCategory("BatchOperations")]
        [Ignore("BatchSize property not yet implemented in SqliteConfiguration")]
        public async Task CreateAsync_CustomBatchSize_ProcessesInBatches()
        {
            // Arrange
            var config = new SqliteConfiguration { /* BatchSize = 10 */ };
            var customProvider = new SQLitePersistenceProvider<BatchTestEntity, Guid>(this.connectionString, config);
            await customProvider.InitializeAsync();
            
            var entities = new List<BatchTestEntity>();
            for (int i = 0; i < 25; i++)
            {
                entities.Add(new BatchTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Entity {i}",
                    Value = i
                });
            }

            // Act
            var results = await customProvider.CreateAsync(entities, this.callerInfo);

            // Assert
            Assert.AreEqual(25, results.Count());
            // With batch size 10, this should process in 3 batches (10, 10, 5)
            var allEntities = await customProvider.GetAllAsync(this.callerInfo);
            Assert.AreEqual(25, allEntities.Count());
            
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
            Assert.IsNotNull(results);
            Assert.AreEqual(50, results.Count());
            Assert.AreEqual(25, results.Count(e => e.Status == "Active"));
            Assert.AreEqual(25, results.Count(e => e.Status == "Inactive"));
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
            Assert.AreEqual(7, results.Count(), "Should filter out soft-deleted entities");
            Assert.IsFalse(results.Any(e => e.Id == createdArray[0].Id));
            Assert.IsFalse(results.Any(e => e.Id == createdArray[2].Id));
            Assert.IsFalse(results.Any(e => e.Id == createdArray[4].Id));
            
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
            Assert.AreEqual(0, results.Count(), "All entities should be expired and filtered out");
            
            await provider.DisposeAsync();
        }

        [TestMethod]
        [TestCategory("BatchOperations")]
        [Ignore("Batch update with IEnumerable<T> not supported - use individual UpdateAsync calls")]
        public async Task UpdateAsync_BatchUpdate_Success()
        {
            // Arrange
            var entities = new List<BatchTestEntity>();
            for (int i = 0; i < 20; i++)
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
            var updated = await this.provider.UpdateAsync(created, entity => 
            {
                entity.Name = entity.Name.Replace("Original", "Updated");
                entity.Status = "Modified";
                entity.Value = entity.Value * 2;
                return entity;
            }, this.callerInfo);

            // Assert
            Assert.IsNotNull(updated);
            Assert.AreEqual(20, updated.Count());
            Assert.IsTrue(updated.All(e => e.Name.StartsWith("Updated")));
            Assert.IsTrue(updated.All(e => e.Status == "Modified"));
            Assert.IsTrue(updated.All(e => e.Version == 2));
        }

        [TestMethod]
        [TestCategory("BatchOperations")]
        public async Task UpdateAsync_AppliesUpdateFunction()
        {
            // Arrange
            var entities = new List<BatchTestEntity>();
            for (int i = 0; i < 10; i++)
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
            Assert.IsNotNull(updated);
            Assert.AreEqual(10, updated.Count());
            Assert.IsTrue(updated.All(e => e.Status == "Processed"));
            Assert.IsTrue(updated.All(e => e.Value % 10 == 0));
            Assert.IsTrue(updated.All(e => e.Version == 2));
        }

        [TestMethod]
        [TestCategory("BatchOperations")]
        [ExpectedException(typeof(AggregateException))]
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
            Assert.IsTrue(result > 0);
            
            // Verify correct entities were deleted
            var remaining = await this.provider.GetAllAsync(this.callerInfo);
            Assert.AreEqual(20, remaining.Count()); // 30 - 10 deleted
            Assert.IsFalse(remaining.Any(e => keysToDelete.Contains(e.Id)));
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
            Assert.IsTrue(result >= 0, "Delete should be idempotent and handle non-existent keys");
            
            var remaining = await this.provider.GetAllAsync(this.callerInfo);
            Assert.AreEqual(2, remaining.Count()); // Only entities 1 and 3 should remain
            Assert.IsTrue(remaining.Any(e => e.Id == created[1].Id));
            Assert.IsTrue(remaining.Any(e => e.Id == created[3].Id));
        }
    }
}