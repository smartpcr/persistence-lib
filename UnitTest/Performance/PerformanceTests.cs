// -----------------------------------------------------------------------
// <copyright file="PerformanceTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Performance
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using PerfTestEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.Performance.PerfTestEntity;

    [TestClass]
    [TestCategory("Performance")]
    public class PerformanceTests
    {
        private string connectionString;
        private SQLitePersistenceProvider<PerfTestEntity, Guid> provider;
        private CallerInfo callerInfo;

        [TestInitialize]
        public async Task Setup()
        {
            this.connectionString = "Data Source=:memory:";
            var config = new SqliteConfiguration
            {
                JournalMode = JournalMode.WAL,
                SynchronousMode = SynchronousMode.Normal,
                CacheSize = 10000
            };
            this.provider = new SQLitePersistenceProvider<PerfTestEntity, Guid>(this.connectionString, config);
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
        [TestCategory("Performance")]
        public async Task Create_SingleEntity_MeetsTarget()
        {
            // Arrange
            var entity = new PerfTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Performance Test",
                Data = new string('x', 1000), // 1KB of data
                Value = 100
            };
            
            // Warm up
            await this.provider.CreateAsync(
                new PerfTestEntity { Id = Guid.NewGuid(), Name = "Warmup" },
                this.callerInfo);

            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = await this.provider.CreateAsync(entity, this.callerInfo);
            stopwatch.Stop();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 50, 
                $"Single create took {stopwatch.ElapsedMilliseconds}ms, target is < 50ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public async Task Read_SingleEntity_MeetsTarget()
        {
            // Arrange
            var entity = new PerfTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Read Test",
                Data = new string('x', 1000),
                Value = 100
            };
            await this.provider.CreateAsync(entity, this.callerInfo);

            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = await this.provider.GetAsync(entity.Id, this.callerInfo);
            stopwatch.Stop();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 20,
                $"Single read took {stopwatch.ElapsedMilliseconds}ms, target is < 20ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public async Task BatchCreate_1000Entities_MeetsTarget()
        {
            // Arrange
            var entities = new List<PerfTestEntity>();
            for (int i = 0; i < 1000; i++)
            {
                entities.Add(new PerfTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Batch Entity {i}",
                    Data = $"Data {i}",
                    Value = i
                });
            }

            // Act
            var stopwatch = Stopwatch.StartNew();
            var results = await this.provider.CreateAsync(entities, this.callerInfo);
            stopwatch.Stop();

            // Assert
            Assert.AreEqual(1000, results.Count());
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 2000,
                $"Batch create of 1000 entities took {stopwatch.ElapsedMilliseconds}ms, target is < 2000ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public async Task Query_1000Results_MeetsTarget()
        {
            // Arrange - Create 1000 entities
            var entities = new List<PerfTestEntity>();
            for (int i = 0; i < 1000; i++)
            {
                entities.Add(new PerfTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Query Entity {i}",
                    Data = $"Data {i}",
                    Value = i % 100
                });
            }
            await this.provider.CreateAsync(entities, this.callerInfo);

            // Act
            var stopwatch = Stopwatch.StartNew();
            var results = await this.provider.QueryAsync(
                e => e.Value < 50,
                null,
                this.callerInfo);
            stopwatch.Stop();

            // Assert
            Assert.AreEqual(500, results.Count()); // 50% should match
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 500,
                $"Query returning 1000 results took {stopwatch.ElapsedMilliseconds}ms, target is < 500ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public async Task BulkImport_10000Entities_MeetsTarget()
        {
            // Arrange
            var entities = new List<PerfTestEntity>();
            for (int i = 0; i < 10000; i++)
            {
                entities.Add(new PerfTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Bulk {i}",
                    Data = $"D{i}",
                    Value = i
                });
            }

            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = await this.provider.BulkImportAsync(entities);
            stopwatch.Stop();

            // Assert
            Assert.AreEqual(10000, result.SuccessCount);
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 30000,
                $"Bulk import of 10000 entities took {stopwatch.ElapsedMilliseconds}ms, target is < 30s");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public async Task ConcurrentOperations_100Threads_NoDeadlock()
        {
            // Arrange
            var tasks = new List<Task>();
            var errors = new List<Exception>();
            var successCount = 0;
            var lockObject = new object();

            // Act - Launch 100 concurrent operations
            for (int i = 0; i < 100; i++)
            {
                var index = i;
                var task = Task.Run(async () =>
                {
                    try
                    {
                        var entity = new PerfTestEntity
                        {
                            Id = Guid.NewGuid(),
                            Name = $"Concurrent {index}",
                            Value = index
                        };
                        
                        // Mix of operations
                        if (index % 3 == 0)
                        {
                            // Create
                            await this.provider.CreateAsync(entity, this.callerInfo);
                        }
                        else if (index % 3 == 1)
                        {
                            // Create then update
                            var created = await this.provider.CreateAsync(entity, this.callerInfo);
                            created.Name = $"Updated {index}";
                            await this.provider.UpdateAsync(created, this.callerInfo);
                        }
                        else
                        {
                            // Create then delete
                            await this.provider.CreateAsync(entity, this.callerInfo);
                            await this.provider.DeleteAsync(entity.Id, this.callerInfo);
                        }
                        
                        lock (lockObject)
                        {
                            successCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (lockObject)
                        {
                            errors.Add(ex);
                        }
                    }
                });
                
                tasks.Add(task);
            }
            
            // Wait for all tasks with timeout
            var allCompleted = Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(30));

            // Assert
            Assert.IsTrue(allCompleted, "All tasks should complete within 30 seconds");
            Assert.AreEqual(0, errors.Count, $"No errors expected, but got {errors.Count}");
            Assert.AreEqual(100, successCount, "All 100 operations should succeed");
        }
    }
}