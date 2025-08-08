//-------------------------------------------------------------------------------
// <copyright file="SQLitePersistenceProviderInitializeTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SQLite;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class SQLitePersistenceProviderInitializeTests : SQLiteTestBase
    {
        private string tempDbPath;
        private string tempConfigPath;

        [TestInitialize]
        public void TestInitialize()
        {
            this.tempDbPath = Path.Combine(Directory.GetCurrentDirectory(), $"test_{Guid.NewGuid()}.db");
            this.tempConfigPath = Path.Combine(Directory.GetCurrentDirectory(), $"config_{Guid.NewGuid()}.json");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (File.Exists(this.tempDbPath))
            {
                // Use the base class method for safe database deletion
                this.SafeDeleteDatabase(this.tempDbPath);
            }

            if (File.Exists(this.tempConfigPath))
            {
                File.Delete(this.tempConfigPath);
            }
        }

        [TestMethod]
        public async Task InitializeAsync_WithDefaultConfig_ShouldCreateDatabase()
        {
            // Arrange
            var connectionString = $"Data Source={this.tempDbPath}";
            var provider = new SQLitePersistenceProvider<TestEntity, string>(connectionString);

            // Act
            await provider.InitializeAsync();

            // Assert
            File.Exists(this.tempDbPath).Should().BeTrue();
            
            // Verify table was created
            using var connection = new SQLiteConnection(connectionString);
            await connection.OpenAsync();
            using var command = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='TestEntity'", connection);
            var tableName = await command.ExecuteScalarAsync();
            tableName.Should().NotBeNull();
        }

        [TestMethod]
        public async Task InitializeAsync_WithJsonConfig_ShouldApplySettings()
        {
            // Arrange
            var config = new
            {
                SqliteConfiguration = new
                {
                    DbFile = this.tempDbPath,
                    CacheSize = -4000,
                    PageSize = 8192,
                    JournalMode = "WAL",
                    SynchronousMode = "Full",
                    BusyTimeout = 15000,
                    EnableForeignKeys = true
                }
            };

            File.WriteAllText(this.tempConfigPath, JsonConvert.SerializeObject(config, Formatting.Indented));

            var connectionString = $"Data Source={this.tempDbPath}";
            var provider = SQLitePersistenceProvider<TestEntity, string>.CreateWithJsonConfig(connectionString, this.tempConfigPath);

            // Act
            await provider.InitializeAsync();

            // Assert
            File.Exists(this.tempDbPath).Should().BeTrue();

            // Verify pragma settings
            using var connection = await provider.CreateAndOpenConnectionAsync(CancellationToken.None);

            // Check cache_size
            using (var command = new SQLiteCommand("PRAGMA cache_size", connection))
            {
                var cacheSize = Convert.ToInt32(await command.ExecuteScalarAsync());
                cacheSize.Should().Be(-4000);
            }

            // Check journal_mode
            using (var command = new SQLiteCommand("PRAGMA journal_mode", connection))
            {
                var journalMode = await command.ExecuteScalarAsync() as string;
                journalMode.Should().Be("wal");
            }

            // Check synchronous
            using (var command = new SQLiteCommand("PRAGMA synchronous", connection))
            {
                var synchronous = Convert.ToInt32(await command.ExecuteScalarAsync());
                synchronous.Should().Be(2); // Full = 2
            }

            // Check busy_timeout
            using (var command = new SQLiteCommand("PRAGMA busy_timeout", connection))
            {
                var busyTimeout = Convert.ToInt32(await command.ExecuteScalarAsync());
                busyTimeout.Should().Be(15000);
            }

            // Check foreign_keys
            using (var command = new SQLiteCommand("PRAGMA foreign_keys", connection))
            {
                var foreignKeys = Convert.ToInt32(await command.ExecuteScalarAsync());
                foreignKeys.Should().Be(1); // ON = 1
            }
        }

        [TestMethod]
        public async Task InitializeAsync_WithInMemoryDatabase_ShouldNotCreateFile()
        {
            // Arrange
            var connectionString = $"Data Source={this.tempDbPath}";
            var provider = new SQLitePersistenceProvider<TestEntity, string>(connectionString);

            // Act
            await provider.InitializeAsync();

            // Assert
            // In-memory database should work without creating files
            var callerInfo = new CallerInfo();
            var entity = new TestEntity { Id = "test-1", Name = "Test" };
            
            // Should be able to perform operations
            var created = await provider.CreateAsync(entity, callerInfo);
            created.Should().NotBeNull();
        }

        [TestMethod]
        public async Task InitializeAsync_CalledMultipleTimes_ShouldOnlyInitializeOnce()
        {
            // Arrange
            var connectionString = $"Data Source={this.tempDbPath}";
            var provider = new SQLitePersistenceProvider<TestEntity, string>(connectionString);

            // Act
            await provider.InitializeAsync();
            await provider.InitializeAsync(); // Call again
            await provider.InitializeAsync(); // And again

            // Assert
            File.Exists(this.tempDbPath).Should().BeTrue();
            
            // Should still work correctly
            var callerInfo = new CallerInfo();
            var entity = new TestEntity { Id = "test-1", Name = "Test" };
            var created = await provider.CreateAsync(entity, callerInfo);
            created.Should().NotBeNull();
        }

        [TestMethod]
        public async Task InitializeAsync_WithVersionedEntity_ShouldCreateVersionTable()
        {
            // Arrange
            var connectionString = $"Data Source={this.tempDbPath}";
            var provider = new SQLitePersistenceProvider<VersionedTestEntity, string>(connectionString);

            // Act
            await provider.InitializeAsync();

            // Assert
            using var connection = new SQLiteConnection(connectionString);
            await connection.OpenAsync();
            
            // Check main table
            using (var command = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='VersionedTestEntity'", connection))
            {
                var tableName = await command.ExecuteScalarAsync();
                tableName.Should().NotBeNull();
            }

            // Check version table
            using (var command = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='Version'", connection))
            {
                var tableName = await command.ExecuteScalarAsync();
                tableName.Should().NotBeNull();
            }
        }

        [TestMethod]
        public void CreateWithJsonConfig_WithNonExistentConfigFile_ShouldThrow()
        {
            // Arrange
            var connectionString = $"Data Source={this.tempDbPath}";
            var nonExistentConfig = "non-existent-config.json";

            // Act & Assert
            Action act = () => SqliteConfiguration.FromJsonFileRequired(nonExistentConfig);
            act.Should().Throw<FileNotFoundException>();
        }

        [TestMethod]
        public async Task InitializeAsync_WithConfigAtRootLevel_ShouldApplySettings()
        {
            // Arrange - config at root level without SqliteConfiguration section
            var config = new
            {
                CacheSize = -3000,
                PageSize = 4096,
                JournalMode = "DELETE",
                SynchronousMode = "Off",
                BusyTimeout = 8000,
                EnableForeignKeys = false
            };

            File.WriteAllText(this.tempConfigPath, JsonConvert.SerializeObject(config, Formatting.Indented));

            var connectionString = $"Data Source={this.tempDbPath}";
            var provider = SQLitePersistenceProvider<TestEntity, string>.CreateWithJsonConfig(connectionString, this.tempConfigPath);

            // Act
            await provider.InitializeAsync();

            // Assert
            using var connection = await provider.CreateAndOpenConnectionAsync(CancellationToken.None);

            // Check settings were applied
            using (var command = new SQLiteCommand("PRAGMA cache_size", connection))
            {
                var cacheSize = Convert.ToInt32(await command.ExecuteScalarAsync());
                cacheSize.Should().Be(-3000);
            }

            using (var command = new SQLiteCommand("PRAGMA foreign_keys", connection))
            {
                var foreignKeys = Convert.ToInt32(await command.ExecuteScalarAsync());
                foreignKeys.Should().Be(0); // OFF = 0
            }
        }

        [Table("TestEntity")]
        private class TestEntity : BaseEntity<string>, IVersionedEntity<string>
        {
            [PrimaryKey(Order = 2)]
            [AuditField(AuditFieldType.Version)]
            [Column("Version", SqlDbType.BigInt, NotNull = true)]
            [Index("IX_CacheEntry_Version")]
            public new long Version
            {
                get => base.Version;
                set => base.Version = value;
            }

            public string Name { get; set; }
            public bool IsDeleted { get; set; }
        }

        [Table("VersionedTestEntity")]
        // ReSharper disable once ClassNeverInstantiated.Local
        private class VersionedTestEntity : BaseEntity<string>, IVersionedEntity<string>
        {
            public string EntityId => this.Id;
            public long EntityVersion => this.Version;
            public bool IsDeleted { get; set; }
        }
    }
}