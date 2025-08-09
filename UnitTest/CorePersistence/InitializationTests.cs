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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.CorePersistence;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.AuditTrail;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Providers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class InitializationTests : SQLiteTestBase
    {
        private string testDbPath;
        private string connectionString;
        private SQLitePersistenceProvider<CrudTestEntity, Guid> provider;
        private SQLitePersistenceProvider<AuditTestEntity, Guid> auditProvider;

        [TestInitialize]
        public void Setup()
        {
            this.testDbPath = Path.Combine(Directory.GetCurrentDirectory(), $"test_{Guid.NewGuid()}.db");
            this.connectionString = $"Data Source={this.testDbPath};Version=3;";
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (this.provider != null)
            {
                await this.provider.DisposeAsync();
            }

            if (this.auditProvider != null)
            {
                await this.auditProvider.DisposeAsync();
            }

            this.SafeDeleteDatabase(this.testDbPath);
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
            await using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync();

            // Check if main table exists
            await using var command = new SQLiteCommand(
                "SELECT name FROM sqlite_master WHERE type='table' AND name='TestEntity'",
                connection);
            var tableName = await command.ExecuteScalarAsync();

            tableName.Should().NotBeNull("TestEntity table should be created");
            tableName!.ToString().Should().Be("TestEntity");
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("Initialization")]
        public async Task InitializeAsync_AppliesPragmaSettings()
        {
            // Arrange
            var configuration = new SqliteConfiguration
            {
                JournalMode = JournalMode.WAL,
                SynchronousMode = SynchronousMode.Normal,
                CacheSize = 10000
            };

            this.provider = new SQLitePersistenceProvider<CrudTestEntity, Guid>(this.connectionString, configuration);

            // Act
            await this.provider.InitializeAsync();

            // Assert
            await using var connection = await this.provider.CreateAndOpenConnectionAsync(CancellationToken.None);

            // Check journal mode
            await using var journalCommand = new SQLiteCommand("PRAGMA journal_mode", connection);
            var journalMode = await journalCommand.ExecuteScalarAsync();
            journalMode?.ToString()?.ToLower().Should().Be("wal");

            // Check synchronous mode
            await using var syncCommand = new SQLiteCommand("PRAGMA synchronous", connection);
            var syncMode = await syncCommand.ExecuteScalarAsync();
            Convert.ToInt32(syncMode).Should().Be(1); // NORMAL = 1
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("Initialization")]
        public async Task InitializeAsync_CreatesAuditTable()
        {
            // Arrange
            var configuration = new SqliteConfiguration
            {
                // Note: EnableAuditTrail is not yet implemented in SqliteConfiguration
                BusyTimeout = 5000
            };

            this.auditProvider = new SQLitePersistenceProvider<AuditTestEntity, Guid>(this.connectionString, configuration);

            // Act
            await this.auditProvider.InitializeAsync();

            // Assert
            await using var connection = await this.auditProvider.CreateAndOpenConnectionAsync(CancellationToken.None);

            await using var command = new SQLiteCommand(
                "SELECT name FROM sqlite_master WHERE type='table' AND name='Audit'",
                connection);
            var tableName = await command.ExecuteScalarAsync();

            tableName.Should().NotBeNull("Audit table should be created when audit trail is enabled");
            tableName!.ToString().Should().Be("Audit");
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

            tableCount.Should().Be(1, "Table should be created only once despite multiple initializations");
        }
    }
}