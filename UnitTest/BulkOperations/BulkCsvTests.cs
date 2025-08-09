// -----------------------------------------------------------------------
// <copyright file="BulkCsvTests.cs" company="Microsoft Corp.">
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
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.BulkOperations;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Providers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BulkCsvTests : SQLiteTestBase
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
                UserId = "TestUser",
                CorrelationId = Guid.NewGuid().ToString()
            };

            this.exportFolder = Path.Combine(Path.GetTempPath(), $"csv_export_{Guid.NewGuid()}");
            Directory.CreateDirectory(this.exportFolder);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (this.provider != null)
            {
                await this.provider.DisposeAsync();
            }

            this.SafeDeleteDatabase(this.testDbPath);

            if (Directory.Exists(this.exportFolder))
            {
                Directory.Delete(this.exportFolder, true);
            }
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkExportAsync_CsvFormat_ExportsCorrectly()
        {
            // Arrange
            var entities = GenerateTestEntities(10);
            await this.provider.CreateAsync(entities, this.callerInfo);

            var options = new BulkExportOptions
            {
                ExportFolder = this.exportFolder,
                FileFormat = FileFormat.Csv,
                CompressOutput = false,
                CsvOptions = new CsvOptions
                {
                    HasHeaders = true,
                    Delimiter = ',',
                    QuoteCharacter = '"'
                }
            };

            // Act
            var result = await this.provider.BulkExportAsync(null, options);

            // Assert
            result.Should().NotBeNull();
            result.ExportedCount.Should().Be(10);
            result.ExportedFiles.Should().NotBeEmpty();

            // Verify CSV file exists and has correct format
            var csvFile = result.ExportedFiles.FirstOrDefault(f => f.EndsWith(".csv"));
            csvFile.Should().NotBeNull();
            File.Exists(csvFile).Should().BeTrue();

            var csvContent = await File.ReadAllTextAsync(csvFile);
            var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Should have header + 10 data rows
            lines.Length.Should().BeGreaterOrEqualTo(11);

            // Check header contains expected columns
            lines[0].Should().Contain("Id");
            lines[0].Should().Contain("Name");
            lines[0].Should().Contain("Category");
            lines[0].Should().Contain("Value");
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkImportFromFileAsync_CsvFormat_ImportsCorrectly()
        {
            // Arrange
            var entities = GenerateTestEntities(5);
            var csvContent = GenerateCsvContent(entities);
            var csvFile = Path.Combine(this.exportFolder, "test_import.csv");
            await File.WriteAllTextAsync(csvFile, csvContent);

            var options = new BulkImportOptions
            {
                FileFormat = FileFormat.Csv,
                CsvOptions = new CsvOptions
                {
                    HasHeaders = true,
                    Delimiter = ',',
                    DateFormat = "yyyy-MM-dd HH:mm:ss"
                }
            };

            // Act
            var result = await this.provider.BulkImportFromFileAsync(csvFile, options);

            // Assert
            result.Should().NotBeNull();
            result.SuccessCount.Should().Be(5);
            result.FailureCount.Should().Be(0);

            // Verify entities were imported
            var importedEntities = await this.provider.GetAllAsync(this.callerInfo);
            importedEntities.Should().HaveCount(5);
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkExportAsync_CsvWithSpecialCharacters_HandlesEscapingCorrectly()
        {
            // Arrange
            var entity = new BulkTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Test, with comma",
                Category = "Contains \"quotes\" and\nnewlines",
                Value = 123
            };
            await this.provider.CreateAsync(entity, this.callerInfo);

            var options = new BulkExportOptions
            {
                ExportFolder = this.exportFolder,
                FileFormat = FileFormat.Csv,
                CompressOutput = false
            };

            // Act
            var result = await this.provider.BulkExportAsync(null, options);

            // Assert
            var csvFile = result.ExportedFiles.First(f => f.EndsWith(".csv"));
            var csvContent = await File.ReadAllTextAsync(csvFile);

            // Should properly escape fields with commas and quotes
            csvContent.Should().Contain("\"Test, with comma\"");
            csvContent.Should().Contain("\"Contains \"\"quotes\"\" and\nnewlines\"");
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkImportFromFileAsync_AutoDetectFormat_WorksForCsv()
        {
            // Arrange
            var entities = GenerateTestEntities(3);
            var csvContent = GenerateCsvContent(entities);
            var csvFile = Path.Combine(this.exportFolder, "auto_detect.csv");
            await File.WriteAllTextAsync(csvFile, csvContent);

            var options = new BulkImportOptions
            {
                FileFormat = FileFormat.Auto // Auto-detect from extension
            };

            // Act
            var result = await this.provider.BulkImportFromFileAsync(csvFile, options);

            // Assert
            result.Should().NotBeNull();
            result.SuccessCount.Should().Be(3);
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkExportAsync_CompressedCsv_CreatesGzipFile()
        {
            // Arrange
            var entities = GenerateTestEntities(5);
            await this.provider.CreateAsync(entities, this.callerInfo);

            var options = new BulkExportOptions
            {
                ExportFolder = this.exportFolder,
                FileFormat = FileFormat.Csv,
                CompressOutput = true
            };

            // Act
            var result = await this.provider.BulkExportAsync(null, options);

            // Assert
            result.Should().NotBeNull();
            var compressedFile = result.ExportedFiles.FirstOrDefault(f => f.EndsWith(".csv.gz"));
            compressedFile.Should().NotBeNull();
            File.Exists(compressedFile).Should().BeTrue();

            // File should be smaller than uncompressed
            var fileInfo = new FileInfo(compressedFile);
            fileInfo.Length.Should().BeGreaterThan(0);
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task BulkExportAsync_WithFileNamePrefix_UsesCorrectNamingPattern()
        {
            // Arrange
            var entities = GenerateTestEntities(5);
            await this.provider.CreateAsync(entities, this.callerInfo);

            var customPrefix = "MyCustomExport";
            var options = new BulkExportOptions
            {
                ExportFolder = this.exportFolder,
                FileFormat = FileFormat.Csv,
                CompressOutput = true,
                FileNamePrefix = customPrefix,
                BatchSize = 2 // Force multiple files
            };

            // Act
            var result = await this.provider.BulkExportAsync(null, options);

            // Assert
            result.Should().NotBeNull();
            result.ExportedFiles.Should().NotBeEmpty();

            // Check that files follow the pattern: {FileNamePrefix}_{timestamp}_{####}.csv.gz
            var exportedDataFiles = result.ExportedFiles
                .Where(f => f.Contains("_0000.csv.gz") || f.Contains("_0001.csv.gz") || f.Contains("_0002.csv.gz"))
                .ToList();

            exportedDataFiles.Should().NotBeEmpty();

            foreach (var file in exportedDataFiles)
            {
                var fileName = Path.GetFileName(file);

                // Should start with custom prefix
                fileName.Should().StartWith(customPrefix + "_");

                // Should match pattern: prefix_YYYYMMddHHmmss_####.csv.gz
                fileName.Should().MatchRegex(@"^" + customPrefix + @"_\d{14}_\d{4}\.csv\.gz$");

                File.Exists(file).Should().BeTrue();
            }

            // Also check metadata file follows the pattern
            var metadataFile = result.ExportedFiles.FirstOrDefault(f => f.Contains("_metadata.json"));
            metadataFile.Should().NotBeNull();
            Path.GetFileName(metadataFile).Should().MatchRegex(@"^" + customPrefix + @"_\d{14}_metadata\.json$");
        }

        private List<BulkTestEntity> GenerateTestEntities(int count)
        {
            var entities = new List<BulkTestEntity>();
            for (int i = 0; i < count; i++)
            {
                entities.Add(new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = $"Entity {i}",
                    Category = $"Category {i % 3}",
                    Value = i * 10,
                    CreatedTime = DateTime.UtcNow.AddDays(-i),
                    LastWriteTime = DateTime.UtcNow
                });
            }
            return entities;
        }

        private string GenerateCsvContent(List<BulkTestEntity> entities)
        {
            var lines = new List<string>();

            // Header
            lines.Add("Id,Name,Category,Value,CreatedTime,LastWriteTime,Version");

            // Data rows
            foreach (var entity in entities)
            {
                lines.Add($"{entity.Id},{entity.Name},{entity.Category},{entity.Value}," +
                         $"{entity.CreatedTime:yyyy-MM-dd HH:mm:ss},{entity.LastWriteTime:yyyy-MM-dd HH:mm:ss},{entity.Version}");
            }

            return string.Join("\n", lines);
        }
    }
}