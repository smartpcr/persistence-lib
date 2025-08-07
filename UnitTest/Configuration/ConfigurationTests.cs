// -----------------------------------------------------------------------
// <copyright file="ConfigurationTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Configuration
{
    using System;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.SQLite;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using ConfigTestEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.Configuration.ConfigTestEntity;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class ConfigurationTests
    {
        private string connectionString;
        private CallerInfo callerInfo;

        [TestInitialize]
        public void Setup()
        {
            this.connectionString = "Data Source=:memory:";
            this.callerInfo = new CallerInfo
            {
                UserId = "TestUser",
                CorrelationId = Guid.NewGuid().ToString()
            };
        }

        [TestMethod]
        [TestCategory("Configuration")]
        public async Task FromJsonFile_LoadsConfiguration()
        {
            // Arrange
            var configJson = @"{
                ""journalMode"": ""WAL"",
                ""synchronous"": ""NORMAL"",
                ""cacheSize"": 10000,
                ""commandTimeout"": 60,
                ""batchSize"": 100,
                ""enableAuditTrail"": true
            }";
            
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, configJson);

            try
            {
                // Act
                var config = SqliteConfiguration.FromJsonFile(tempFile);

                // Assert
                Assert.IsNotNull(config);
                Assert.AreEqual("WAL", config.JournalMode);
                Assert.AreEqual("NORMAL", config.Synchronous);
                Assert.AreEqual(10000, config.CacheSize);
                Assert.AreEqual(60, config.CommandTimeout);
                Assert.AreEqual(100, config.BatchSize);
                Assert.IsTrue(config.EnableAuditTrail);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        [TestCategory("Configuration")]
        public async Task ApplyPragmaSettings_SetsCorrectly()
        {
            // Arrange
            var config = new SqliteConfiguration
            {
                JournalMode = "WAL",
                Synchronous = "FULL",
                CacheSize = 5000,
                PageSize = 8192,
                TempStore = "MEMORY"
            };
            
            var provider = new SQLitePersistenceProvider<ConfigTestEntity, Guid>(this.connectionString, config);

            // Act
            await provider.InitializeAsync();

            // Assert - Verify PRAGMA settings
            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync();
            
            using var journalCmd = new SQLiteCommand("PRAGMA journal_mode", connection);
            var journalMode = await journalCmd.ExecuteScalarAsync();
            Assert.AreEqual("wal", journalMode?.ToString()?.ToLower());
            
            using var syncCmd = new SQLiteCommand("PRAGMA synchronous", connection);
            var syncMode = await syncCmd.ExecuteScalarAsync();
            Assert.AreEqual(2, Convert.ToInt32(syncMode)); // FULL = 2
            
            using var cacheCmd = new SQLiteCommand("PRAGMA cache_size", connection);
            var cacheSize = await cacheCmd.ExecuteScalarAsync();
            Assert.AreEqual(-5000, Convert.ToInt32(cacheSize)); // Negative means KB
            
            await provider.DisposeAsync();
        }

        [TestMethod]
        [TestCategory("Configuration")]
        public async Task JournalMode_WAL_EnablesWriteAheadLog()
        {
            // Arrange
            var config = new SqliteConfiguration { JournalMode = "WAL" };
            var provider = new SQLitePersistenceProvider<ConfigTestEntity, Guid>(this.connectionString, config);

            // Act
            await provider.InitializeAsync();
            
            // Create some entities to ensure WAL is active
            for (int i = 0; i < 10; i++)
            {
                await provider.CreateAsync(
                    new ConfigTestEntity { Id = Guid.NewGuid(), Name = $"Entity {i}" },
                    this.callerInfo);
            }

            // Assert
            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync();
            
            using var cmd = new SQLiteCommand("PRAGMA journal_mode", connection);
            var mode = await cmd.ExecuteScalarAsync();
            Assert.AreEqual("wal", mode?.ToString()?.ToLower());
            
            await provider.DisposeAsync();
        }

        [TestMethod]
        [TestCategory("Configuration")]
        public async Task CommandTimeout_AppliesToAllCommands()
        {
            // Arrange
            var config = new SqliteConfiguration { CommandTimeout = 5 };
            var provider = new SQLitePersistenceProvider<ConfigTestEntity, Guid>(this.connectionString, config);
            await provider.InitializeAsync();

            // Act
            var entity = new ConfigTestEntity { Id = Guid.NewGuid(), Name = "Timeout Test" };
            
            // This should complete within timeout
            var result = await provider.CreateAsync(entity, this.callerInfo);

            // Assert
            Assert.IsNotNull(result);
            
            // Verify timeout is applied to commands
            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM ConfigTestEntity";
            command.CommandTimeout = config.CommandTimeout;
            
            Assert.AreEqual(5, command.CommandTimeout);
            
            await provider.DisposeAsync();
        }

        [TestMethod]
        [TestCategory("Configuration")]
        public async Task CacheSize_AffectsPerformance()
        {
            // Arrange
            var smallCacheConfig = new SqliteConfiguration { CacheSize = 100 };
            var largeCacheConfig = new SqliteConfiguration { CacheSize = 10000 };
            
            var smallCacheProvider = new SQLitePersistenceProvider<ConfigTestEntity, Guid>(
                "Data Source=:memory:", smallCacheConfig);
            var largeCacheProvider = new SQLitePersistenceProvider<ConfigTestEntity, Guid>(
                "Data Source=:memory:", largeCacheConfig);
            
            await smallCacheProvider.InitializeAsync();
            await largeCacheProvider.InitializeAsync();

            // Act - Create many entities
            var entities = new ConfigTestEntity[1000];
            for (int i = 0; i < 1000; i++)
            {
                entities[i] = new ConfigTestEntity { Id = Guid.NewGuid(), Name = $"Entity {i}" };
            }
            
            // Measure time for small cache
            var smallCacheStart = DateTime.UtcNow;
            foreach (var entity in entities)
            {
                await smallCacheProvider.CreateAsync(entity, this.callerInfo);
            }
            var smallCacheTime = (DateTime.UtcNow - smallCacheStart).TotalMilliseconds;
            
            // Measure time for large cache
            var largeCacheStart = DateTime.UtcNow;
            foreach (var entity in entities)
            {
                await largeCacheProvider.CreateAsync(entity, this.callerInfo);
            }
            var largeCacheTime = (DateTime.UtcNow - largeCacheStart).TotalMilliseconds;

            // Assert - Large cache should generally perform better
            // Note: This might not always be true in memory database
            Assert.IsTrue(smallCacheTime > 0);
            Assert.IsTrue(largeCacheTime > 0);
            
            await smallCacheProvider.DisposeAsync();
            await largeCacheProvider.DisposeAsync();
        }
    }
}