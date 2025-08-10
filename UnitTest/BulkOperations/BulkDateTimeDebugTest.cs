// -----------------------------------------------------------------------
// <copyright file="BulkDateTimeDebugTest.cs" company="Microsoft Corp.">
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
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.BulkOperations;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Providers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BulkDateTimeDebugTest : SQLiteTestBase
    {
        private string testDbPath;
        private string connectionString;
        private SQLitePersistenceProvider<BulkTestEntity, Guid> provider;
        private CallerInfo callerInfo;

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
        }

        [TestMethod]
        [TestCategory("BulkOperations")]
        public async Task Debug_DateTimeTranslation_WorksCorrectly()
        {
            // Arrange
            var testDate = DateTime.UtcNow.AddDays(-100);
            var entities = new List<BulkTestEntity>
            {
                new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Old Entity",
                    Category = "Archive",
                    Value = 100,
                    CreatedTime = testDate,
                    LastWriteTime = testDate
                },
                new BulkTestEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Recent Entity",
                    Category = "Active",
                    Value = 200,
                    CreatedTime = DateTime.UtcNow.AddDays(-10),
                    LastWriteTime = DateTime.UtcNow.AddDays(-10)
                }
            };

            await this.provider.CreateAsync(entities, this.callerInfo);

            // Test the translation directly
            Expression<Func<BulkTestEntity, bool>> predicate = e => e.CreatedTime < DateTime.UtcNow.AddDays(-90);
            var translator = new ExpressionTranslator<BulkTestEntity>();
            var result = translator.Translate(predicate);

            Console.WriteLine($"SQL: {result.Sql}");
            foreach (var param in result.Parameters)
            {
                Console.WriteLine($"Parameter {param.Key}: {param.Value} (Type: {param.Value?.GetType()})");
                if (param.Value is DateTime dt)
                {
                    Console.WriteLine($"  DateTime value: {dt:O}");
                }
            }

            // Test with QueryAsync first
            var queryResult = await this.provider.QueryAsync(predicate, null, this.callerInfo);
            Console.WriteLine($"QueryAsync returned {queryResult.Count()} entities");
            
            foreach (var entity in queryResult)
            {
                Console.WriteLine($"  - {entity.Name}: CreatedTime = {entity.CreatedTime:O}");
            }

            // Test with BulkExportAsync
            var exportOptions = new BulkExportOptions
            {
                FileFormat = FileFormat.Json,
                CompressOutput = false
            };

            var exportResult = await this.provider.BulkExportAsync(predicate, exportOptions);
            Console.WriteLine($"BulkExportAsync exported {exportResult.ExportedCount} entities");
            
            // Verify
            queryResult.Should().HaveCount(1);
            queryResult.First().Name.Should().Be("Old Entity");
            
            exportResult.ExportedCount.Should().Be(1);
            exportResult.ExportedEntities.Should().HaveCount(1);
            exportResult.ExportedEntities.First().Name.Should().Be("Old Entity");
        }
    }
}