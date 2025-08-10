// -----------------------------------------------------------------------
// <copyright file="BulkDateTimeFilterTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.BulkOperations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.BulkOperations;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Providers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BulkDateTimeFilterTests : SQLiteTestBase
    {
        private string testDbPath;
        private string connectionString;
        private SQLitePersistenceProvider<BulkTestEntity, Guid> provider;
        private CallerInfo callerInfo;
        private string exportFolder;

        [TestInitialize]
        public async Task Setup()
        {
            this.testDbPath = Path.Combine(Directory.GetCurrentDirectory(), $"test_{Guid.NewGuid()}.db");
            this.connectionString = $"Data Source={this.testDbPath};Version=3;";
            this.provider = new SQLitePersistenceProvider<BulkTestEntity, Guid>(this.connectionString);
            await this.provider.InitializeAsync();

            this.callerInfo = new CallerInfo
            {
                CorrelationId = Guid.NewGuid().ToString()
            };

            this.exportFolder = Path.Combine(Path.GetTempPath(), $"datetime_export_{Guid.NewGuid()}");
            Directory.CreateDirectory(this.exportFolder);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (this.provider != null)
            {
                await this.provider.DisposeAsync();
            }

            SQLiteProviderSharedState.ClearState();

            this.SafeDeleteDatabase(this.testDbPath);

            if (Directory.Exists(this.exportFolder))
            {
                Directory.Delete(this.exportFolder, true);
            }
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkExportAsync_WithDateTimeAddDaysFilter_ExportsCorrectEntities()
        {
            // Arrange
            var entities = new List<BulkTestEntity>
            {
                new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Old Entity 1",
                    Category = "Archive",
                    Value = 100,
                    CreatedTime = DateTimeOffset.UtcNow.AddDays(-100),
                    LastWriteTime = DateTimeOffset.UtcNow.AddDays(-100)
                },
                new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Old Entity 2",
                    Category = "Archive",
                    Value = 200,
                    CreatedTime = DateTimeOffset.UtcNow.AddDays(-95),
                    LastWriteTime = DateTimeOffset.UtcNow.AddDays(-95)
                },
                new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Recent Entity 1",
                    Category = "Active",
                    Value = 300,
                    CreatedTime = DateTimeOffset.UtcNow.AddDays(-30),
                    LastWriteTime = DateTimeOffset.UtcNow.AddDays(-30)
                },
                new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Recent Entity 2",
                    Category = "Active",
                    Value = 400,
                    CreatedTime = DateTimeOffset.UtcNow.AddDays(-10),
                    LastWriteTime = DateTimeOffset.UtcNow.AddDays(-10)
                },
                new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Very Recent Entity",
                    Category = "Active",
                    Value = 500,
                    CreatedTime = DateTimeOffset.UtcNow.AddDays(-1),
                    LastWriteTime = DateTimeOffset.UtcNow.AddDays(-1)
                }
            };

            await this.provider.CreateAsync(entities, this.callerInfo);

            var options = new BulkExportOptions
            {
                ExportFolder = this.exportFolder,
                FileFormat = FileFormat.Json,
                CompressOutput = false
            };

            // Define predicate to export only entities older than 90 days
            Expression<Func<BulkTestEntity, bool>> predicate = e => e.CreatedTime < DateTimeOffset.UtcNow.AddDays(-90);

            // Act
            var result = await this.provider.BulkExportAsync(predicate, options);

            // Assert
            result.Should().NotBeNull();
            result.ExportedCount.Should().Be(2); // Only the two entities older than 90 days
            result.ExportedEntities.Should().NotBeNull();
            result.ExportedEntities.Should().HaveCount(2);
            
            var exportedNames = result.ExportedEntities.Select(e => e.Name).ToList();
            exportedNames.Should().Contain("Old Entity 1");
            exportedNames.Should().Contain("Old Entity 2");
            exportedNames.Should().NotContain("Recent Entity 1");
            exportedNames.Should().NotContain("Recent Entity 2");
            exportedNames.Should().NotContain("Very Recent Entity");
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkExportAsync_WithDateTimeAddMonthsFilter_ExportsCorrectEntities()
        {
            // Arrange
            var entities = new List<BulkTestEntity>
            {
                new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "4 Months Old",
                    Category = "Archive",
                    Value = 100,
                    CreatedTime = DateTimeOffset.UtcNow.AddMonths(-4),
                    LastWriteTime = DateTimeOffset.UtcNow.AddMonths(-4)
                },
                new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "2 Months Old",
                    Category = "Active",
                    Value = 200,
                    CreatedTime = DateTimeOffset.UtcNow.AddMonths(-2),
                    LastWriteTime = DateTimeOffset.UtcNow.AddMonths(-2)
                },
                new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "1 Week Old",
                    Category = "Active",
                    Value = 300,
                    CreatedTime = DateTimeOffset.UtcNow.AddDays(-7),
                    LastWriteTime = DateTimeOffset.UtcNow.AddDays(-7)
                }
            };

            await this.provider.CreateAsync(entities, this.callerInfo);

            var options = new BulkExportOptions
            {
                ExportFolder = this.exportFolder,
                FileFormat = FileFormat.Csv,
                CompressOutput = false
            };

            // Export entities older than 3 months
            Expression<Func<BulkTestEntity, bool>> predicate = e => e.CreatedTime < DateTimeOffset.UtcNow.AddMonths(-3);

            // Act
            var result = await this.provider.BulkExportAsync(predicate, options);

            // Assert
            result.Should().NotBeNull();
            result.ExportedCount.Should().Be(1); // Only the entity older than 3 months
            result.ExportedEntities.Should().HaveCount(1);
            result.ExportedEntities.First().Name.Should().Be("4 Months Old");
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkExportAsync_WithComplexDateTimeFilter_ExportsCorrectEntities()
        {
            // Arrange
            var now = DateTimeOffset.UtcNow;
            var entities = new List<BulkTestEntity>();
            for (int i = 0; i < 10; i++)
            {
                entities.Add(new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Entity {i}",
                    Category = i % 2 == 0 ? "Even" : "Odd",
                    Value = i * 100,
                    // Add 1 hour to ensure we're clearly past the boundary
                    CreatedTime = now.AddDays(-i * 10).AddHours(-1),
                    LastWriteTime = now.AddDays(-i * 5)
                });
            }

            await this.provider.CreateAsync(entities, this.callerInfo);

            var options = new BulkExportOptions
            {
                ExportFolder = this.exportFolder,
                FileFormat = FileFormat.Json,
                CompressOutput = true
            };

            // Complex predicate: entities created between 30 and 60 days ago
            var cutoffNew = now.AddDays(-30);
            var cutoffOld = now.AddDays(-60);
            Expression<Func<BulkTestEntity, bool>> predicate = e => 
                e.CreatedTime < cutoffNew && 
                e.CreatedTime > cutoffOld;

            // Act
            var result = await this.provider.BulkExportAsync(predicate, options);

            // Assert
            result.Should().NotBeNull();
            // Entities 3, 4, and 5 should match (30+, 40+, and 50+ days old)
            result.ExportedCount.Should().Be(3);
            
            var exportedNames = result.ExportedEntities.Select(e => e.Name).OrderBy(n => n).ToList();
            exportedNames.Should().BeEquivalentTo(new[] { "Entity 3", "Entity 4", "Entity 5" });
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task GetAllAsync_WithDateTimeAddHoursFilter_ReturnsCorrectEntities()
        {
            // Arrange
            var entities = new List<BulkTestEntity>
            {
                new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "3 Hours Ago",
                    Category = "Recent",
                    Value = 100,
                    CreatedTime = DateTimeOffset.UtcNow.AddHours(-3),
                    LastWriteTime = DateTimeOffset.UtcNow.AddHours(-3)
                },
                new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "1 Hour Ago",
                    Category = "Recent",
                    Value = 200,
                    CreatedTime = DateTimeOffset.UtcNow.AddHours(-1),
                    LastWriteTime = DateTimeOffset.UtcNow.AddHours(-1)
                },
                new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Just Now",
                    Category = "Recent",
                    Value = 300,
                    CreatedTime = DateTimeOffset.UtcNow.AddMinutes(-5),
                    LastWriteTime = DateTimeOffset.UtcNow.AddMinutes(-5)
                }
            };

            await this.provider.CreateAsync(entities, this.callerInfo);

            // Get entities created more than 2 hours ago
            Expression<Func<BulkTestEntity, bool>> predicate = e => e.CreatedTime < DateTimeOffset.UtcNow.AddHours(-2);

            // Act
            var result = await this.provider.QueryAsync(predicate, null, this.callerInfo);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.First().Name.Should().Be("3 Hours Ago");
        }
    }
}