// -----------------------------------------------------------------------
// <copyright file="InitializationTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.CorePersistence
{
    using System;
    using System.Data.SQLite;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.CorePersistence;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class InitializationTests
    {
        private string connectionString;
        private SQLitePersistenceProvider<CrudTestEntity, Guid> provider;

        [TestInitialize]
        public void Setup()
        {
            // Use in-memory database for tests
            this.connectionString = "Data Source=:memory:";
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
        [TestCategory("CorePersistence")]
        [TestCategory("Initialization")]
        public async Task InitializeAsync_CreatesRequiredTables()
        {
            // Arrange
            this.provider = new SQLitePersistenceProvider<CrudTestEntity, Guid>(this.connectionString);

            // Act
            await this.provider.InitializeAsync();

            // Assert
            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync();
            
            // Check if main table exists
            using var command = new SQLiteCommand(
                "SELECT name FROM sqlite_master WHERE type='table' AND name='TestEntity'", 
                connection);
            var tableName = await command.ExecuteScalarAsync();
            
            Assert.IsNotNull(tableName, "TestEntity table should be created");
            Assert.AreEqual("TestEntity", tableName.ToString());
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("Initialization")]
        public async Task InitializeAsync_AppliesPragmaSettings()
        {
            // Arrange
            var configuration = new SqliteConfiguration
            {
                JournalMode = "WAL",
                Synchronous = "NORMAL",
                CacheSize = 10000
            };
            
            this.provider = new SQLitePersistenceProvider<CrudTestEntity, Guid>(this.connectionString, configuration);

            // Act
            await this.provider.InitializeAsync();

            // Assert
            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync();
            
            // Check journal mode
            using var journalCommand = new SQLiteCommand("PRAGMA journal_mode", connection);
            var journalMode = await journalCommand.ExecuteScalarAsync();
            Assert.AreEqual("wal", journalMode?.ToString()?.ToLower());
            
            // Check synchronous mode
            using var syncCommand = new SQLiteCommand("PRAGMA synchronous", connection);
            var syncMode = await syncCommand.ExecuteScalarAsync();
            Assert.AreEqual(1, Convert.ToInt32(syncMode)); // NORMAL = 1
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("Initialization")]
        public async Task InitializeAsync_CreatesAuditTable()
        {
            // Arrange
            var configuration = new SqliteConfiguration
            {
                EnableAuditTrail = true
            };
            
            this.provider = new SQLitePersistenceProvider<CrudTestEntity, Guid>(this.connectionString, configuration);

            // Act
            await this.provider.InitializeAsync();

            // Assert
            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync();
            
            using var command = new SQLiteCommand(
                "SELECT name FROM sqlite_master WHERE type='table' AND name='AuditRecord'", 
                connection);
            var tableName = await command.ExecuteScalarAsync();
            
            Assert.IsNotNull(tableName, "AuditRecord table should be created when audit trail is enabled");
            Assert.AreEqual("AuditRecord", tableName.ToString());
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("Initialization")]
        public async Task InitializeAsync_IdempotentOperation()
        {
            // Arrange
            this.provider = new SQLitePersistenceProvider<CrudTestEntity, Guid>(this.connectionString);

            // Act - Initialize multiple times
            await this.provider.InitializeAsync();
            await this.provider.InitializeAsync();
            await this.provider.InitializeAsync();

            // Assert - Should not throw and table should exist only once
            using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync();
            
            using var command = new SQLiteCommand(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='TestEntity'", 
                connection);
            var tableCount = Convert.ToInt32(await command.ExecuteScalarAsync());
            
            Assert.AreEqual(1, tableCount, "Table should be created only once despite multiple initializations");
        }
    }
}