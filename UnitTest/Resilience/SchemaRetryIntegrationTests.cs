// -----------------------------------------------------------------------
// <copyright file="SchemaRetryIntegrationTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Resilience
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SchemaRetryIntegrationTests
    {
        private string testDbPath;

        [TestInitialize]
        public void Setup()
        {
            this.testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(this.testDbPath))
            {
                try
                {
                    File.Delete(this.testDbPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [TestMethod]
        [TestCategory("Resilience")]
        [TestCategory("Integration")]
        public async Task InitializeAsync_WithRetryPolicy_CreatesSchemaSuccessfully()
        {
            // Arrange
            var config = new SqliteConfiguration
            {
                DbFile = this.testDbPath,
                RetryPolicy = new RetryConfiguration
                {
                    Enabled = true,
                    MaxAttempts = 3,
                    InitialDelayMs = 10,
                    MaxDelayMs = 100
                }
            };

            var connectionString = $"Data Source={this.testDbPath}";
            var provider = new SQLitePersistenceProvider<TestEntity, string>(connectionString, config);

            // Act
            await provider.InitializeAsync();

            // Assert
            File.Exists(this.testDbPath).Should().BeTrue();
            
            // Verify we can perform operations
            var entity = new TestEntity
            {
                Id = "test-1",
                Name = "Test Entity",
                Value = 42,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow,
                Version = 1
            };

            var created = await provider.CreateAsync(entity, new CallerInfo());
            created.Should().NotBeNull();
            created.Id.Should().Be("test-1");
        }

        [TestMethod]
        [TestCategory("Resilience")]
        [TestCategory("Integration")]
        public async Task InitializeAsync_WithNoRetryPolicy_StillWorks()
        {
            // Arrange
            var config = new SqliteConfiguration
            {
                DbFile = this.testDbPath,
                RetryPolicy = RetryConfiguration.NoRetry
            };

            var connectionString = $"Data Source={this.testDbPath}";
            var provider = new SQLitePersistenceProvider<TestEntity, string>(connectionString, config);

            // Act
            await provider.InitializeAsync();

            // Assert
            File.Exists(this.testDbPath).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Resilience")]
        [TestCategory("Integration")]
        public async Task SchemaOperations_WithNetworkStorageConfig_AppliesLongerRetries()
        {
            // Arrange
            var config = new SqliteConfiguration
            {
                DbFile = this.testDbPath,
                RetryPolicy = RetryConfiguration.ForNetworkStorage
            };

            var connectionString = $"Data Source={this.testDbPath}";
            var provider = new SQLitePersistenceProvider<TestEntity, string>(connectionString, config);

            // Act
            await provider.InitializeAsync();

            // Assert
            config.RetryPolicy.MaxAttempts.Should().Be(5);
            config.RetryPolicy.InitialDelayMs.Should().Be(500);
            File.Exists(this.testDbPath).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Resilience")]
        [TestCategory("Integration")]
        public async Task MultipleProviders_WithSharedDatabase_HandleConcurrentSchema()
        {
            // Arrange
            var config = new SqliteConfiguration
            {
                DbFile = this.testDbPath,
                RetryPolicy = RetryConfiguration.ForHighContention,
                BusyTimeout = 5000 // 5 seconds busy timeout
            };

            var connectionString = $"Data Source={this.testDbPath}";
            
            // Create multiple providers for different entity types
            var provider1 = new SQLitePersistenceProvider<TestEntity, string>(connectionString, config);
            var provider2 = new SQLitePersistenceProvider<SimpleEntity, string>(connectionString, config);
            
            // Act - Initialize both providers concurrently
            var task1 = provider1.InitializeAsync();
            var task2 = provider2.InitializeAsync();
            
            await Task.WhenAll(task1, task2);

            // Assert
            File.Exists(this.testDbPath).Should().BeTrue();
            
            // Both providers should be able to work
            var entity1 = new TestEntity
            {
                Id = "test-1",
                Name = "Test",
                Version = 1
            };
            
            var entity2 = new SimpleEntity
            {
                Id = "simple-1",
                Name = "Simple",
                Age = 25,
                Version = 1
            };
            
            var created1 = await provider1.CreateAsync(entity1, new CallerInfo());
            var created2 = await provider2.CreateAsync(entity2, new CallerInfo());
            
            created1.Should().NotBeNull();
            created2.Should().NotBeNull();
        }
    }
}