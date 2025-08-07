// -----------------------------------------------------------------------
// <copyright file="BulkOperationsTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.BulkOperations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using BulkTestEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.BulkOperations.BulkTestEntity;

    [TestClass]
    public class BulkOperationsTests
    {
        private string connectionString;
        private SQLitePersistenceProvider<BulkTestEntity, Guid> provider;
        private CallerInfo callerInfo;

        [TestInitialize]
        public async Task Setup()
        {
            this.connectionString = "Data Source=:memory:";
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
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkImportAsync_LargeDataset_Success()
        {
            // Arrange
            var entities = new List<BulkTestEntity>();
            for (int i = 0; i < 10000; i++)
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
            var result = await this.provider.BulkImportAsync(entities, this.callerInfo);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(10000, result.ImportedCount);
            Assert.AreEqual(0, result.FailedCount);
            
            // Verify import
            var count = await this.provider.CountAsync(null, this.callerInfo);
            Assert.AreEqual(10000, count);
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
                new BulkImportOptions { ConflictResolution = ConflictResolution.Skip },
                this.callerInfo);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.ImportedCount);
            Assert.AreEqual(1, result.SkippedCount);
            
            // Verify existing entity wasn't overwritten
            var existing = await this.provider.GetAsync(existingId, this.callerInfo);
            Assert.AreEqual("Existing", existing.Name);
            Assert.AreEqual(100, existing.Value);
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkImportAsync_ConflictResolution_Overwrite()
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
                new BulkImportOptions { ConflictResolution = ConflictResolution.Overwrite },
                this.callerInfo);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.ImportedCount);
            
            // Verify existing entity was overwritten
            var overwritten = await this.provider.GetAsync(existingId, this.callerInfo);
            Assert.AreEqual("Overwritten", overwritten.Name);
            Assert.AreEqual(200, overwritten.Value);
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkImportAsync_ProgressReporting_UpdatesProgress()
        {
            // Arrange
            var entities = new List<BulkTestEntity>();
            for (int i = 0; i < 1000; i++)
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
                new BulkImportOptions { Progress = progress },
                this.callerInfo);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsTrue(progressUpdates.Count > 0, "Progress should be reported");
            Assert.IsTrue(progressUpdates.Any(p => p.PercentComplete > 0 && p.PercentComplete < 100));
            Assert.IsTrue(progressUpdates.Last().PercentComplete == 100);
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
            File.WriteAllText(tempFile, JsonConvert.SerializeObject(entities));

            try
            {
                // Act
                var result = await this.provider.BulkImportFromFileAsync(
                    tempFile,
                    ImportFormat.Json,
                    this.callerInfo);

                // Assert
                Assert.IsTrue(result.Success);
                Assert.AreEqual(2, result.ImportedCount);
                
                var imported = await this.provider.GetAllAsync(this.callerInfo);
                Assert.AreEqual(2, imported.Count);
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
            File.WriteAllText(tempFile, csvContent);

            try
            {
                // Act
                var result = await this.provider.BulkImportFromFileAsync(
                    tempFile,
                    ImportFormat.Csv,
                    this.callerInfo);

                // Assert
                Assert.IsTrue(result.Success);
                Assert.AreEqual(2, result.ImportedCount);
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
            var result = await this.provider.BulkExportAsync(
                stream,
                ExportFormat.Json,
                this.callerInfo);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(100, result.ExportedCount);
            Assert.IsTrue(stream.Length > 0);
            
            // Verify exported data
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var exported = JsonConvert.DeserializeObject<List<BulkTestEntity>>(json);
            Assert.AreEqual(100, exported.Count);
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkExportAsync_ChunkedFiles_CreatesMultiple()
        {
            // Arrange
            for (int i = 0; i < 250; i++)
            {
                await this.provider.CreateAsync(
                    new BulkTestEntity { Id = Guid.NewGuid(), Name = $"Chunked {i}", Value = i },
                    this.callerInfo);
            }
            
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Act
                var result = await this.provider.BulkExportToFilesAsync(
                    tempDir,
                    ExportFormat.Json,
                    new BulkExportOptions { ChunkSize = 100 },
                    this.callerInfo);

                // Assert
                Assert.IsTrue(result.Success);
                Assert.AreEqual(250, result.ExportedCount);
                
                var files = Directory.GetFiles(tempDir, "*.json");
                Assert.AreEqual(3, files.Length); // 250 items / 100 per chunk = 3 files
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkExportAsync_Compression_ReducesSize()
        {
            // Arrange
            for (int i = 0; i < 100; i++)
            {
                await this.provider.CreateAsync(
                    new BulkTestEntity 
                    { 
                        Id = Guid.NewGuid(), 
                        Name = $"This is a long name for entity number {i} to ensure compression is effective", 
                        Value = i 
                    },
                    this.callerInfo);
            }
            
            using var uncompressedStream = new MemoryStream();
            using var compressedStream = new MemoryStream();

            // Act
            await this.provider.BulkExportAsync(
                uncompressedStream,
                ExportFormat.Json,
                this.callerInfo);
            
            await this.provider.BulkExportAsync(
                compressedStream,
                ExportFormat.JsonCompressed,
                this.callerInfo);

            // Assert
            Assert.IsTrue(compressedStream.Length < uncompressedStream.Length,
                "Compressed export should be smaller than uncompressed");
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task PurgeAsync_AgeBasedRetention_RemovesOld()
        {
            // Arrange
            var oldDate = DateTime.UtcNow.AddDays(-100);
            var recentDate = DateTime.UtcNow.AddDays(-10);
            
            // Create old entities
            for (int i = 0; i < 5; i++)
            {
                var entity = new BulkTestEntity 
                { 
                    Id = Guid.NewGuid(), 
                    Name = $"Old {i}",
                    Value = i,
                    CreatedTime = oldDate
                };
                await this.provider.CreateAsync(entity, this.callerInfo);
            }
            
            // Create recent entities
            for (int i = 0; i < 3; i++)
            {
                var entity = new BulkTestEntity 
                { 
                    Id = Guid.NewGuid(), 
                    Name = $"Recent {i}",
                    Value = i,
                    CreatedTime = recentDate
                };
                await this.provider.CreateAsync(entity, this.callerInfo);
            }

            // Act
            var result = await this.provider.PurgeAsync(
                e => e.CreatedTime < DateTime.UtcNow.AddDays(-90),
                this.callerInfo);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(5, result.PurgedCount);
            
            var remaining = await this.provider.GetAllAsync(this.callerInfo);
            Assert.AreEqual(3, remaining.Count);
            Assert.IsTrue(remaining.All(e => e.Name.StartsWith("Recent")));
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
                new PurgeOptions { PreviewOnly = true },
                this.callerInfo);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(5, result.PreviewCount);
            Assert.AreEqual(0, result.PurgedCount);
            
            // Verify nothing was actually deleted
            var allEntities = await this.provider.GetAllAsync(this.callerInfo);
            Assert.AreEqual(10, allEntities.Count);
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task PurgeAsync_VacuumAfter_ReclaimsSpace()
        {
            // Arrange
            for (int i = 0; i < 1000; i++)
            {
                await this.provider.CreateAsync(
                    new BulkTestEntity { Id = Guid.NewGuid(), Name = $"ToDelete {i}", Value = i },
                    this.callerInfo);
            }

            // Act
            var result = await this.provider.PurgeAsync(
                e => e.Value < 500,
                new PurgeOptions { VacuumAfterPurge = true },
                this.callerInfo);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(500, result.PurgedCount);
            Assert.IsTrue(result.SpaceReclaimed > 0, "VACUUM should reclaim space");
            
            var remaining = await this.provider.CountAsync(null, this.callerInfo);
            Assert.AreEqual(500, remaining);
        }
    }
}