// -----------------------------------------------------------------------
// <copyright file="BulkOperationsTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.BulkOperations
{
    using System;
    using System.Collections.Generic;
    using System.Data.SQLite;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Providers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using BulkTestEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.BulkOperations.BulkTestEntity;

    [TestClass]
    public class BulkOperationsTests : SQLiteTestBase
    {
        private string connectionString;
        private string testDbPath;
        private SQLitePersistenceProvider<BulkTestEntity, Guid> provider;
        private CallerInfo callerInfo;
        private string exportDir;

        [TestInitialize]
        public async Task Setup()
        {
            this.exportDir = Path.Combine(Directory.GetCurrentDirectory(), "export");
            if (Directory.Exists(this.exportDir))
            {
                Directory.Delete(this.exportDir, true);
            }

            this.testDbPath = Path.Combine(Directory.GetCurrentDirectory(), $"test_{Guid.NewGuid()}.db");
            this.connectionString = $"Data Source={this.testDbPath};Version=3;";
            this.provider = new SQLitePersistenceProvider<BulkTestEntity, Guid>(this.connectionString);
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

            if (Directory.Exists(this.exportDir))
            {
                Directory.Delete(this.exportDir, true);
            }

            SQLiteProviderSharedState.ClearState();

            this.SafeDeleteDatabase(this.testDbPath);
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkImportAsync_LargeDataset_Success()
        {
            // Arrange
            var entities = new List<BulkTestEntity>();
            for (var i = 0; i < 10000; i++)
            {
                entities.Add(new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Bulk Entity {i}",
                    Category = $"Category {i % 10}",
                    Value = i
                });
            }

            // Act
            var result = await this.provider.BulkImportAsync(entities);

            // Assert
            result.SuccessCount.Should().Be(10000);
            result.FailureCount.Should().Be(0);

            // Verify import
            var count = await this.provider.CountAsync();
            count.Should().Be(10000);
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkImportAsync_ConflictResolution_Skip()
        {
            // Arrange
            var existingId = Guid.NewGuid();
            await this.provider.CreateAsync(
                new BulkTestEntity { Id = existingId, Name = "Existing", Value = 100 },
                this.callerInfo);

            var importEntities = new List<BulkTestEntity>
            {
                new BulkTestEntity { Id = existingId, Name = "Should Skip", Value = 200 },
                new BulkTestEntity { Id = Guid.NewGuid(), Name = "Should Import", Value = 300 }
            };

            // Act
            var result = await this.provider.BulkImportAsync(
                importEntities,
                new BulkImportOptions { ConflictResolution = ConflictResolution.UseTarget });

            // Assert
            result.SuccessCount.Should().Be(1);
            result.Statistics.EntitiesSkipped.Should().Be(1);

            // Verify existing entity wasn't overwritten
            var existing = await this.provider.GetAsync(existingId, this.callerInfo);
            existing.Name.Should().Be("Existing");
            existing.Value.Should().Be(100);
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkImportAsync_ConflictResolution_UseSource()
        {
            // Arrange
            var existingId = Guid.NewGuid();
            await this.provider.CreateAsync(
                new BulkTestEntity { Id = existingId, Name = "Original", Value = 100 },
                this.callerInfo);

            var importEntities = new List<BulkTestEntity>
            {
                new BulkTestEntity { Id = existingId, Name = "Overwritten", Value = 200 },
                new BulkTestEntity { Id = Guid.NewGuid(), Name = "New", Value = 300 }
            };

            // Act
            var result = await this.provider.BulkImportAsync(
                importEntities,
                new BulkImportOptions { ConflictResolution = ConflictResolution.UseSource });

            // Assert
            result.SuccessCount.Should().Be(2);

            // Verify existing entity was overwritten
            var overwritten = await this.provider.GetAsync(existingId, this.callerInfo);
            overwritten.Name.Should().Be("Overwritten");
            overwritten.Value.Should().Be(200);
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkImportAsync_ProgressReporting_UpdatesProgress()
        {
            // Arrange
            var entities = new List<BulkTestEntity>();
            for (var i = 0; i < 1000; i++)
            {
                entities.Add(new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Entity {i}",
                    Value = i
                });
            }

            var progressUpdates = new List<BulkOperationProgress>();
            var progress = new Progress<BulkOperationProgress>(p => progressUpdates.Add(p));

            // Act
            var result = await this.provider.BulkImportAsync(
                entities,
                new BulkImportOptions(),
                progress);

            // Assert
            progressUpdates.Count.Should().BeGreaterThan(0, "Progress should be reported");
            progressUpdates.Any(p => p.PercentComplete > 0 && p.PercentComplete < 100).Should().BeTrue();
            progressUpdates.Last().PercentComplete.Should().Be(100);
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkImportFromFileAsync_JsonFormat_Success()
        {
            // Arrange
            var entities = new List<BulkTestEntity>
            {
                new BulkTestEntity { Id = Guid.NewGuid(), Name = "JSON Entity 1", Value = 100 },
                new BulkTestEntity { Id = Guid.NewGuid(), Name = "JSON Entity 2", Value = 200 }
            };

            var tempFile = Path.GetTempFileName();
            var json = JsonConvert.SerializeObject(entities);
            Console.WriteLine($"Serialized JSON: {json}");
            await File.WriteAllTextAsync(tempFile, json);

            try
            {
                // Act
                var result = await this.provider.BulkImportFromFileAsync(
                    tempFile);

                // Assert
                result.SuccessCount.Should().Be(2);

                var imported = await this.provider.GetAllAsync(this.callerInfo);
                imported.Count().Should().Be(2);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkImportFromFileAsync_CsvFormat_Success()
        {
            // Arrange
            var csvContent = "Id,Name,Value,Category\n" +
                            $"{Guid.NewGuid()},CSV Entity 1,100,Cat A\n" +
                            $"{Guid.NewGuid()},CSV Entity 2,200,Cat B\n";

            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, csvContent);

            try
            {
                // Act
                var result = await this.provider.BulkImportFromFileAsync(
                    tempFile);

                // Assert
                result.SuccessCount.Should().Be(2);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkExportAsync_StreamsData_MemoryEfficient()
        {
            // Arrange
            for (int i = 0; i < 100; i++)
            {
                await this.provider.CreateAsync(
                    new BulkTestEntity { Id = Guid.NewGuid(), Name = $"Export {i}", Value = i },
                    this.callerInfo);
            }

            using var stream = new MemoryStream();

            // Act
            var result = await this.provider.BulkExportAsync();

            // Assert
            result.ExportedCount.Should().Be(100);

            // Verify exported data
            var exported = result.ExportedEntities.ToList();
            exported.Count.Should().Be(100);
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkExportAsync_MarkAsExported_AddsColumn()
        {
            // Arrange
            var entity = new BulkTestEntity { Id = Guid.NewGuid(), Name = "ToExport", Value = 1 };
            await this.provider.CreateAsync(entity, this.callerInfo);

            (await this.ColumnExistsAsync("ExportedDate")).Should().BeFalse();

            // Act
            var result = await this.provider.BulkExportAsync(
                options: new BulkExportOptions { Mode = ExportMode.Archive, MarkAsExported = true });

            // Assert
            result.ExportedCount.Should().Be(1);
            (await this.ColumnExistsAsync("ExportedDate")).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkExportAsync_ChunkedFiles_CreatesMultiple()
        {
            // Arrange
            var entities = new List<BulkTestEntity>();
            for (var i = 0; i < 250; i++)
            {
                entities.Add(new BulkTestEntity { Id = Guid.NewGuid(), Name = $"Chunked {i}", Value = i });
            }
            await this.provider.CreateAsync(
                entities,
                this.callerInfo);

            var result = await this.provider.BulkExportAsync(
                null,
                new  BulkExportOptions
                {
                    Mode = ExportMode.Archive,
                    MarkAsExported = true,
                    BatchSize = 100,
                    FileFormat = FileFormat.Csv,
                    CompressOutput = true,
                    ExportFolder = this.exportDir,
                    CsvOptions = new CsvOptions()
                    {
                        HasHeaders = true
                    }
                },
                new Progress<BulkOperationProgress>(progress =>
                {
                    Console.WriteLine($"Export progress: {progress.PercentComplete}%");
                }),
                CancellationToken.None);

            // Assert - commented out since method is not implemented
            result.Should().NotBeNull();
            result.ExportedCount.Should().Be(250);

            var files = Directory.GetFiles(this.exportDir, $"{nameof(BulkTestEntity)}*.csv.gz");
            files.Length.Should().Be(3); // 250 items / 100 per chunk = 3 files
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkExportAsync_Compression_ReducesSize()
        {
            // Arrange
            var entities = new List<BulkTestEntity>();
            for (var i = 0; i < 100; i++)
            {
                entities.Add(new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"This is a long name for entity number {i} to ensure compression is effective",
                    Value = i
                });
            }
            await this.provider.CreateAsync(
                entities,
                this.callerInfo);

            var unCompressedResult = await this.provider.BulkExportAsync(
                null,
                new  BulkExportOptions
                {
                    Mode = ExportMode.Archive,
                    MarkAsExported = true,
                    FileFormat = FileFormat.Csv,
                    CompressOutput = false,
                    ExportFolder = this.exportDir,
                    CsvOptions = new CsvOptions()
                    {
                        HasHeaders = true
                    },
                    FileNamePrefix = $"{nameof(BulkTestEntity)}_Uncompressed"
                },
                new Progress<BulkOperationProgress>(progress =>
                {
                    Console.WriteLine($"Export progress: {progress.PercentComplete}%");
                }),
                CancellationToken.None);
            unCompressedResult.ExportedCount.Should().Be(100);

            var uncompressedFiles = Directory.GetFiles(this.exportDir, $"{nameof(BulkTestEntity)}_Uncompressed*.csv");
            uncompressedFiles.Length.Should().Be(1);
            var uncompressedFileSize = new FileInfo(uncompressedFiles[0]).Length;

            var compressedResult = await this.provider.BulkExportAsync(
                null,
                new  BulkExportOptions
                {
                    Mode = ExportMode.Archive,
                    MarkAsExported = true,
                    FileFormat = FileFormat.Csv,
                    CompressOutput = true,
                    ExportFolder = this.exportDir,
                    CsvOptions = new CsvOptions()
                    {
                        HasHeaders = true
                    },
                    FileNamePrefix = $"{nameof(BulkTestEntity)}_Compressed"
                },
                new Progress<BulkOperationProgress>(progress =>
                {
                    Console.WriteLine($"Export progress: {progress.PercentComplete}%");
                }),
                CancellationToken.None);
            compressedResult.ExportedCount.Should().Be(100);
            var compressedFiles = Directory.GetFiles(this.exportDir, $"{nameof(BulkTestEntity)}_Compressed*.csv.gz");
            compressedFiles.Length.Should().Be(1);
            var compressedFileSize = new FileInfo(compressedFiles[0]).Length;

            compressedFileSize.Should().BeLessThan(uncompressedFileSize, "Compressed file should be smaller than uncompressed file");
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task PurgeAsync_AgeBasedRetention_RemovesOld()
        {
            // Arrange
            var oldDate = DateTimeOffset.UtcNow.AddDays(-100);
            var recentDate = DateTimeOffset.UtcNow.AddDays(-10);
            var entities = new List<BulkTestEntity>();

            // Create old entities (100 days old, which is older than 90 days threshold)
            for (var i = 0; i < 5; i++)
            {
                entities.Add(new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Old {i}",
                    Value = i,
                    CreatedTime = oldDate,
                    LastWriteTime = oldDate
                });
            }

            // Create recent entities (10 days old, which is newer than 90 days threshold)
            for (var i = 0; i < 3; i++)
            {
                entities.Add(new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Recent {i}",
                    Value = i,
                    CreatedTime = recentDate,
                    LastWriteTime = recentDate
                });
            }

            await this.provider.CreateAsync(entities, this.callerInfo);

            // Act - Purge entities older than 90 days
            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-90);
            var result = await this.provider.PurgeAsync(
                e => e.CreatedTime < cutoffDate);

            // Assert
            result.IsPreview.Should().BeTrue();
            result.Preview.AffectedEntityCount.Should().Be(5, "5 entities are older than 90 days");
            result.EntitiesPurged.Should().Be(0, "Preview mode should not actually purge");

            var remaining = (await this.provider.GetAllAsync(this.callerInfo))?.ToList();
            remaining.Should().NotBeNull();
            remaining!.Count.Should().Be(8, "All 8 entities should still exist in preview mode");

            // Now do actual purge
            var actualResult = await this.provider.PurgeAsync(
                e => e.CreatedTime < cutoffDate,
                new PurgeOptions { SafeMode = false });

            actualResult.EntitiesPurged.Should().Be(5, "Should have purged 5 old entities");

            // Verify remaining entities
            var afterPurge = (await this.provider.GetAllAsync(this.callerInfo))?.ToList();
            afterPurge.Should().NotBeNull();
            afterPurge!.Count.Should().Be(3, "Only 3 recent entities should remain");
            afterPurge.All(e => e.Name.StartsWith("Recent")).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task PurgeAsync_PreviewMode_NoChanges()
        {
            // Arrange
            for (int i = 0; i < 10; i++)
            {
                await this.provider.CreateAsync(
                    new BulkTestEntity { Id = Guid.NewGuid(), Name = $"Entity {i}", Value = i },
                    this.callerInfo);
            }

            // Act
            var result = await this.provider.PurgeAsync(
                e => e.Value < 5,
                new PurgeOptions { SafeMode = true });

            // Assert
            result.IsPreview.Should().BeTrue();
            result.Preview.AffectedEntityCount.Should().Be(5);
            result.EntitiesPurged.Should().Be(0);

            // Verify nothing was actually deleted
            var allEntities = await this.provider.GetAllAsync(this.callerInfo);
            allEntities.Count().Should().Be(10);
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task PurgeAsync_VacuumAfter_ReclaimsSpace()
        {
            // Arrange
            var entities = new List<BulkTestEntity>();
            for (var i = 0; i < 1000; i++)
            {
                entities.Add(new BulkTestEntity { Id = Guid.NewGuid(), Name = $"ToDelete {i}", Value = i });
            }
            await this.provider.CreateAsync(
                entities,
                this.callerInfo);

            // Act
            var result = await this.provider.PurgeAsync(
                e => e.Value < 500,
                new PurgeOptions
                {
                    SafeMode = false,
                    OptimizeStorage = true
                });

            // Assert
            result.EntitiesPurged.Should().Be(500);
            result.SpaceReclaimedBytes.Should().BeGreaterThan(0, "VACUUM should reclaim space");

            var remaining = await this.provider.CountAsync();
            remaining.Should().Be(500);
        }

        private async Task<bool> ColumnExistsAsync(string columnName)
        {
            var method = typeof(SQLitePersistenceProvider<BulkTestEntity, Guid>).GetMethod(
                "CreateAndOpenConnectionAsync",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var task = (Task<SQLiteConnection>)method.Invoke(this.provider, new object[] { CancellationToken.None });
            using var connection = await task;
            using var cmd = new SQLiteCommand($"PRAGMA table_info('{this.provider.Mapper.TableName}')", connection);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (reader["name"].ToString() == columnName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}