//-------------------------------------------------------------------------------
// <copyright file="SQLitePersistenceProviderIntegrationTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Integration tests for <see cref="SQLitePersistenceProvider{T,TKey}"/> covering real-world scenarios.
    /// </summary>
    [TestClass]
    public class SQLitePersistenceProviderIntegrationTests : SQLiteTestBase
    {
        private string testDbPath;

        #region Complex Test Entities

        [Table("UpdateEntity")]
        public class UpdateEntity : BaseEntity<string>
        {
            [Column("Name", SqlDbType.NVarChar, Size = 200)]
            public string Name { get; set; }

            public string PackageVersion { get; set; }

            public string Description { get; set; }

            public long PackageSize { get; set; }

            public DateTimeOffset ReleaseDate { get; set; }

            public string Prerequisites { get; set; }

            public DateTimeOffset? AbsoluteExpiration { get; set; }
        }

        [Table("UpdateRun", ExpirySpanString = "00:01:00", EnableArchive = true)]
        public class UpdateRunEntity : BaseEntity<string>, IExpirableEntity<string>
        {
            [Column("UpdateId", SqlDbType.NVarChar, Size = 50)]
            [ForeignKey("UpdateEntity", "CacheKey")]
            public string UpdateId { get; set; }

            [Column("Status", SqlDbType.NVarChar, Size = 50)]
            public string Status { get; set; }

            [Column("StartTime", SqlDbType.DateTimeOffset)]
            public DateTimeOffset StartTime { get; set; }

            [Column("EndTime", SqlDbType.DateTimeOffset)]
            public DateTimeOffset? EndTime { get; set; }

            [Column("Progress", SqlDbType.Int)]
            public int Progress { get; set; }

            [Column("ErrorMessage", SqlDbType.Text)]
            public string ErrorMessage { get; set; }

            [Column("AbsoluteExpiration", SqlDbType.DateTimeOffset)]
            public DateTimeOffset? AbsoluteExpiration { get; set; }

            [Column("IsArchived", SqlDbType.Bit)]
            public bool IsArchived { get; set; }
        }

        [Table("CacheMetadata")]
        public class CacheMetadataEntity : BaseEntity<string>
        {
            [Column("EntityType", SqlDbType.NVarChar, Size = 100)]
            [Index("IX_EntityType")]
            public string EntityType { get; set; }

            [Column("EntityId", SqlDbType.NVarChar, Size = 50)]
            [Index("IX_EntityId")]
            public string EntityId { get; set; }

            [Column("MetadataKey", SqlDbType.NVarChar, Size = 100)]
            public string MetadataKey { get; set; }

            [Column("MetadataValue", SqlDbType.Text)]
            public string MetadataValue { get; set; }

            [Column("ExtractionTime", SqlDbType.DateTimeOffset)]
            public DateTimeOffset ExtractionTime { get; set; }
        }

        #endregion

        #region Test Setup and Cleanup

        [TestInitialize]
        public void TestInitialize()
        {
            this.testDbPath = Path.Combine(Path.GetTempPath(), $"test_integration_{Guid.NewGuid()}.db");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (File.Exists(this.testDbPath))
            {
                this.SafeDeleteDatabase(this.testDbPath);
            }
        }

        #endregion

        #region Real-World Scenario Tests

        [TestMethod]
        [TestCategory("IntegrationTest")]
        public async Task UpdateScenario_CompleteUpdateLifecycle()
        {
            // Arrange
            var connectionString = $"Data Source={this.testDbPath};Version=3;Foreign Keys=true;";
            var updateProvider = new SQLitePersistenceProvider<UpdateEntity, string>(connectionString);
            var runProvider = new SQLitePersistenceProvider<UpdateRunEntity, string>(connectionString);
            
            await updateProvider.InitializeAsync();
            await runProvider.InitializeAsync();

            // Create an update
            var update = new UpdateEntity
            {
                Id = "update-2024.1",
                Name = "Security Update 2024.1",
                PackageVersion = "2024.1.0.0",
                Description = "Critical security update",
                PackageSize = 1024 * 1024 * 500, // 500MB
                ReleaseDate = DateTimeOffset.UtcNow.AddDays(-7),
                Prerequisites = "2023.12",
                CreatedTime = DateTimeOffset.UtcNow,
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(90) // 90 day retention
            };

            var createdUpdate = await updateProvider.CreateAsync(update, new CallerInfo());

            // Start update run
            var run = new UpdateRunEntity
            {
                Id = $"run-{Guid.NewGuid()}",
                UpdateId = createdUpdate.Id,
                Status = "InProgress",
                StartTime = DateTimeOffset.UtcNow,
                Progress = 0,
                CreatedTime = DateTimeOffset.UtcNow,
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(30), // 30 day retention
                IsArchived = false
            };

            var createdRun = await runProvider.CreateAsync(run, new CallerInfo());
            createdRun.Should().NotBeNull();
            createdRun.Version.Should().Be(1);

            // Simulate update progress
            for (var progress = 10; progress <= 100; progress += 10)
            {
                createdRun.Progress = progress;
                createdRun = await runProvider.UpdateAsync(createdRun, new CallerInfo());
                await Task.Delay(10); // Simulate work
            }

            // Complete update
            createdRun.Status = "Completed";
            createdRun.EndTime = DateTimeOffset.UtcNow;
            createdRun = await runProvider.UpdateAsync(createdRun, new CallerInfo());
            createdRun.Version.Should().Be(12);

            // Assert
            var finalRun = await runProvider.GetAsync(createdRun.Id, new CallerInfo());
            Assert.IsNotNull(finalRun);
            Assert.AreEqual("Completed", finalRun.Status);
            Assert.AreEqual(100, finalRun.Progress);
            Assert.IsNotNull(finalRun.EndTime);
            finalRun.Version.Should().Be(12);
        }

        [TestMethod]
        [TestCategory("IntegrationTest")]
        public async Task CacheScenario_MetadataExtractionAndQuerying()
        {
            // Arrange
            var connectionString = $"Data Source={this.testDbPath};Version=3;";
            var updateProvider = new SQLitePersistenceProvider<UpdateEntity, string>(connectionString);
            var metadataProvider = new SQLitePersistenceProvider<CacheMetadataEntity, string>(connectionString);
            
            await updateProvider.InitializeAsync();
            await metadataProvider.InitializeAsync();

            // Create updates with different versions
            var updates = new[]
            {
                new UpdateEntity
                {
                    Id = "update-2024.1",
                    Name = "Q1 2024 Update",
                    PackageVersion = "2024.1.0.0",
                    ReleaseDate = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero),
                    PackageSize = 500 * 1024 * 1024,
                    CreatedTime = DateTimeOffset.UtcNow
                },
                new UpdateEntity
                {
                    Id = "update-2024.2",
                    Name = "Q2 2024 Update",
                    PackageVersion = "2024.2.0.0",
                    ReleaseDate = new DateTimeOffset(2024, 4, 15, 0, 0, 0, TimeSpan.Zero),
                    PackageSize = 600 * 1024 * 1024,
                    CreatedTime = DateTimeOffset.UtcNow
                },
                new UpdateEntity
                {
                    Id = "update-2024.3",
                    Name = "Q3 2024 Update",
                    PackageVersion = "2024.3.0.0",
                    ReleaseDate = new DateTimeOffset(2024, 7, 15, 0, 0, 0, TimeSpan.Zero),
                    PackageSize = 550 * 1024 * 1024,
                    CreatedTime = DateTimeOffset.UtcNow
                }
            };

            foreach (var update in updates)
            {
                await updateProvider.CreateAsync(update, new CallerInfo());
                
                // Extract metadata
                var metadata = new[]
                {
                    new CacheMetadataEntity
                    {
                        Id = $"meta-{Guid.NewGuid()}",
                        EntityType = "UpdateEntity",
                        EntityId = update.Id,
                        MetadataKey = "Version",
                        MetadataValue = update.PackageVersion,
                        ExtractionTime = DateTimeOffset.UtcNow
                    },
                    new CacheMetadataEntity
                    {
                        Id = $"meta-{Guid.NewGuid()}",
                        EntityType = "UpdateEntity",
                        EntityId = update.Id,
                        MetadataKey = "Quarter",
                        MetadataValue = $"Q{(update.ReleaseDate.Month - 1) / 3 + 1}",
                        ExtractionTime = DateTimeOffset.UtcNow
                    },
                    new CacheMetadataEntity
                    {
                        Id = $"meta-{Guid.NewGuid()}",
                        EntityType = "UpdateEntity",
                        EntityId = update.Id,
                        MetadataKey = "Year",
                        MetadataValue = update.ReleaseDate.Year.ToString(),
                        ExtractionTime = DateTimeOffset.UtcNow
                    }
                };

                await metadataProvider.CreateAsync(metadata.ToList(), new CallerInfo());
            }

            // Query metadata
            var q2Metadata = await metadataProvider.QueryAsync(
                m => m.MetadataKey == "Quarter" && m.MetadataValue == "Q2", null, new CallerInfo());

            var year2024Metadata = await metadataProvider.QueryAsync(
                m => m.MetadataKey == "Year" && m.MetadataValue == "2024", null, new CallerInfo());

            // Assert
            Assert.AreEqual(1, q2Metadata.Count());
            Assert.AreEqual("update-2024.2", q2Metadata.First().EntityId);

            Assert.AreEqual(3, year2024Metadata.Count());
            Assert.IsTrue(year2024Metadata.All(m => m.MetadataValue == "2024"));
        }

        [TestMethod]
        [TestCategory("IntegrationTest")]
        public async Task ExpirationAndArchive_CompleteLifecycle()
        {
            // Arrange
            var connectionString = $"Data Source={this.testDbPath};Version=3;";
            var updateProvider = new SQLitePersistenceProvider<UpdateEntity, string>(connectionString);
            var runProvider = new SQLitePersistenceProvider<UpdateRunEntity, string>(connectionString);
            await updateProvider.InitializeAsync();
            await runProvider.InitializeAsync();

            // Create updates with different versions
            var updates = new[]
            {
                new UpdateEntity
                {
                    Id = "update-2024.1",
                    Name = "Q1 2024 Update",
                    PackageVersion = "2024.1.0.0",
                    ReleaseDate = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero),
                    PackageSize = 500 * 1024 * 1024,
                    CreatedTime = DateTimeOffset.UtcNow
                },
                new UpdateEntity
                {
                    Id = "update-2024.2",
                    Name = "Q2 2024 Update",
                    PackageVersion = "2024.2.0.0",
                    ReleaseDate = new DateTimeOffset(2024, 4, 15, 0, 0, 0, TimeSpan.Zero),
                    PackageSize = 600 * 1024 * 1024,
                    CreatedTime = DateTimeOffset.UtcNow
                },
                new UpdateEntity
                {
                    Id = "update-2024.3",
                    Name = "Q3 2024 Update",
                    PackageVersion = "2024.3.0.0",
                    ReleaseDate = new DateTimeOffset(2024, 7, 15, 0, 0, 0, TimeSpan.Zero),
                    PackageSize = 550 * 1024 * 1024,
                    CreatedTime = DateTimeOffset.UtcNow
                }
            };

            foreach (var update in updates)
            {
                await updateProvider.CreateAsync(update, new CallerInfo());
            }

            // Create runs with different ages
            var runs = new[]
            {
                new UpdateRunEntity
                {
                    Id = "run-active",
                    UpdateId = updates[0].Id,
                    Status = "Completed",
                    StartTime = DateTimeOffset.UtcNow,
                    EndTime = DateTimeOffset.UtcNow,
                    Progress = 100,
                    CreatedTime = DateTimeOffset.UtcNow,
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(10), // Not expired
                    IsArchived = false
                },
                new UpdateRunEntity
                {
                    Id = "run-expired-1",
                    UpdateId = updates[1].Id,
                    Status = "Completed",
                    StartTime = DateTimeOffset.UtcNow.AddSeconds(-5),
                    EndTime = DateTimeOffset.UtcNow.AddSeconds(-4),
                    Progress = 100,
                    CreatedTime = DateTimeOffset.UtcNow.AddSeconds(-5),
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(-2), // Already expired
                    IsArchived = false
                },
                new UpdateRunEntity
                {
                    Id = "run-expired-2",
                    UpdateId = updates[2].Id,
                    Status = "Failed",
                    StartTime = DateTimeOffset.UtcNow.AddSeconds(-10),
                    EndTime = DateTimeOffset.UtcNow.AddSeconds(-8),
                    Progress = 50,
                    ErrorMessage = "Update failed",
                    CreatedTime = DateTimeOffset.UtcNow.AddSeconds(-10),
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(-5), // Already expired
                    IsArchived = false
                }
            };

            foreach (var run in runs)
            {
                await runProvider.CreateAsync(run, new CallerInfo());
            }

            // Act - Archive expired
            var purgeResult = await runProvider.PurgeAsync(
                null,
                new PurgeOptions()
                {
                    SafeMode = false,
                    BackupBeforePurge = false,
                    Strategy = PurgeStrategy.PurgeExpired
                });

            // Assert
            Assert.AreEqual(2, purgeResult.EntitiesPurged);

            // Verify active run is still active
            var activeRun = await runProvider.GetAsync("run-active", new CallerInfo());
            Assert.IsNotNull(activeRun);
            Assert.IsFalse(activeRun.IsArchived);
        }

        #endregion

        #region Edge Cases and Error Scenarios

        [TestMethod]
        [TestCategory("IntegrationTest")]
        public async Task EdgeCase_EmptyStringValues_HandledCorrectly()
        {
            // Arrange
            var connectionString = $"Data Source={this.testDbPath};Version=3;";
            var provider = new SQLitePersistenceProvider<UpdateEntity, string>(connectionString);
            await provider.InitializeAsync();

            var entity = new UpdateEntity
            {
                Id = "edge-empty",
                Name = "", // Empty string
                PackageVersion = "1.0",
                Description = null, // Null value
                Prerequisites = "", // Empty string
                PackageSize = 0,
                ReleaseDate = DateTimeOffset.UtcNow,
                CreatedTime = DateTimeOffset.UtcNow
            };

            // Act
            var created = await provider.CreateAsync(entity, new CallerInfo());
            var retrieved = await provider.GetAsync("edge-empty", new CallerInfo());

            // Assert
            Assert.IsNotNull(retrieved);
            Assert.AreEqual("", retrieved.Name);
            Assert.IsNull(retrieved.Description);
            Assert.AreEqual("", retrieved.Prerequisites);
        }

        [TestMethod]
        [TestCategory("IntegrationTest")]
        public async Task EdgeCase_SpecialCharactersInId_HandledCorrectly()
        {
            // Arrange
            var connectionString = $"Data Source={this.testDbPath};Version=3;";
            var provider = new SQLitePersistenceProvider<UpdateEntity, string>(connectionString);
            await provider.InitializeAsync();

            var specialIds = new[]
            {
                "id-with-spaces and tabs",
                "id/with/slashes",
                "id\\with\\backslashes",
                "id'with'quotes",
                "id\"with\"double-quotes",
                "id-with-unicode-文字"
            };

            // Act & Assert
            foreach (var id in specialIds)
            {
                var entity = new UpdateEntity
                {
                    Id = id,
                    Name = $"Entity {id}",
                    PackageVersion = "1.0",
                    ReleaseDate = DateTimeOffset.UtcNow,
                    PackageSize = 1000,
                    CreatedTime = DateTimeOffset.UtcNow
                };

                var created = await provider.CreateAsync(entity, new CallerInfo());
                var retrieved = await provider.GetAsync(id, new CallerInfo());

                Assert.IsNotNull(retrieved);
                Assert.AreEqual(id, retrieved.Id);
            }
        }

        [TestMethod]
        [TestCategory("IntegrationTest")]
        public async Task EdgeCase_MaxConcurrentOperations_HandlesGracefully()
        {
            // Arrange
            var connectionString = $"Data Source={this.testDbPath};Version=3;";
            var provider = new SQLitePersistenceProvider<UpdateEntity, string>(connectionString);
            await provider.InitializeAsync();

            var tasks = new List<Task<UpdateEntity>>();

            // Act - Create many concurrent operations
            for (int i = 0; i < 100; i++)
            {
                var entity = new UpdateEntity
                {
                    Id = $"concurrent-{i}",
                    Name = $"Concurrent Entity {i}",
                    PackageVersion = "1.0",
                    ReleaseDate = DateTimeOffset.UtcNow,
                    PackageSize = i * 1000,
                    CreatedTime = DateTimeOffset.UtcNow
                };

                tasks.Add(Task.Run(() => provider.CreateAsync(entity, new CallerInfo())));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.AreEqual(100, results.Length);
            Assert.IsTrue(results.All(r => r != null));

            var count = await provider.CountAsync();
            Assert.AreEqual(100, count);
        }

        #endregion

        #region Data Migration Scenarios

        [TestMethod]
        [TestCategory("IntegrationTest")]
        public async Task DataMigration_FromV1ToV2Schema_Success()
        {
            // This test simulates migrating data when schema changes
            // For example, adding new required columns

            // Arrange
            var connectionString = $"Data Source={this.testDbPath};Version=3;";
            var provider = new SQLitePersistenceProvider<UpdateEntity, string>(connectionString);
            await provider.InitializeAsync();

            // Create "old" entities (simulating v1 schema)
            var oldEntities = new[]
            {
                new UpdateEntity
                {
                    Id = "old-1",
                    Name = "Old Update 1",
                    PackageVersion = "1.0",
                    Description = "Legacy update",
                    PackageSize = 1000,
                    ReleaseDate = DateTimeOffset.UtcNow.AddDays(-365),
                    CreatedTime = DateTimeOffset.UtcNow.AddDays(-365),
                    // AbsoluteExpiration would be null in old schema
                    AbsoluteExpiration = null
                },
                new UpdateEntity
                {
                    Id = "old-2",
                    Name = "Old Update 2",
                    PackageVersion = "2.0",
                    Description = "Another legacy update",
                    PackageSize = 2000,
                    ReleaseDate = DateTimeOffset.UtcNow.AddDays(-180),
                    CreatedTime = DateTimeOffset.UtcNow.AddDays(-180),
                    AbsoluteExpiration = null
                }
            };

            foreach (var entity in oldEntities)
            {
                await provider.CreateAsync(entity, new CallerInfo());
            }

            // Act - Migrate by updating with new schema requirements
            var migrated = new List<UpdateEntity>();
            foreach (var oldEntity in oldEntities)
            {
                var entity = await provider.GetAsync(oldEntity.Id, new CallerInfo());
                if (entity != null && !entity.AbsoluteExpiration.HasValue)
                {
                    // Set expiration based on age
                    var age = DateTimeOffset.UtcNow - entity.CreatedTime;
                    entity.AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(90); // Default 90 days from now

                    var updated = await provider.UpdateAsync(entity, new CallerInfo());
                    migrated.Add(updated);
                }
            }

            // Assert
            Assert.AreEqual(2, migrated.Count);
            Assert.IsTrue(migrated.All(m => m.AbsoluteExpiration.HasValue));
            Assert.IsTrue(migrated.All(m => m.AbsoluteExpiration > DateTimeOffset.UtcNow));
        }

        #endregion
    }
}