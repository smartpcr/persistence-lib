// -----------------------------------------------------------------------
// <copyright file="SQLiteHelperTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Helpers
{
    using System;
    using System.Data.SQLite;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Helpers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SQLiteHelperTests
    {
        private string testDbPath;
        private string connectionString;

        [TestInitialize]
        public void Setup()
        {
            this.testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
            this.connectionString = $"Data Source={this.testDbPath};Version=3;";
            
            // Create test database with sample schema
            this.CreateTestDatabase();
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(this.testDbPath))
            {
                // Ensure all connections are closed
                SQLiteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                
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
        [TestCategory("SQLiteHelper")]
        public async Task GetDatabaseInfoAsync_ReturnsCompleteInfo()
        {
            // Arrange
            await using var helper = new SQLiteHelper(this.connectionString);

            // Act
            var dbInfo = await helper.GetDatabaseInfoAsync();

            // Assert
            dbInfo.Should().NotBeNull();
            dbInfo.FilePath.Should().Be(this.testDbPath);
            dbInfo.SqliteVersion.Should().NotBeNullOrEmpty();
            dbInfo.JournalMode.Should().NotBeNullOrEmpty();
            dbInfo.PageSize.Should().BeGreaterThan(0);
            dbInfo.Stats.Should().NotBeNull();
            dbInfo.Tables.Should().NotBeEmpty();
        }

        [TestMethod]
        [TestCategory("SQLiteHelper")]
        public async Task GetDatabaseStatsAsync_ReturnsAccurateStats()
        {
            // Arrange
            await using var helper = new SQLiteHelper(this.connectionString);

            // Act
            var stats = await helper.GetDatabaseStatsAsync();

            // Assert
            stats.Should().NotBeNull();
            stats.FileSizeBytes.Should().BeGreaterThan(0);
            stats.FormattedFileSize.Should().NotBeNullOrEmpty();
            stats.PageCount.Should().BeGreaterThan(0);
            stats.Encoding.Should().Be("UTF-8");
            stats.TableCount.Should().BeGreaterThanOrEqualTo(3); // Users, Products, Orders
            stats.IndexCount.Should().BeGreaterThanOrEqualTo(2); // At least the indexes we created
            stats.LastModified.Should().NotBeNull();
        }

        [TestMethod]
        [TestCategory("SQLiteHelper")]
        public async Task GetTablesAsync_ReturnsAllTables()
        {
            // Arrange
            await using var helper = new SQLiteHelper(this.connectionString);

            // Act
            var tables = await helper.GetTablesAsync();

            // Assert
            tables.Should().NotBeNull();
            tables.Should().HaveCountGreaterThanOrEqualTo(3);
            
            var usersTable = tables.FirstOrDefault(t => t.TableName == "Users");
            usersTable.Should().NotBeNull();
            usersTable.Columns.Should().NotBeEmpty();
            usersTable.HasPrimaryKey.Should().BeTrue();
            usersTable.RowCount.Should().BeGreaterThanOrEqualTo(0);
        }

        [TestMethod]
        [TestCategory("SQLiteHelper")]
        public async Task GetTableColumnsAsync_ReturnsCorrectColumns()
        {
            // Arrange
            await using var helper = new SQLiteHelper(this.connectionString);

            // Act
            var columns = await helper.GetTableColumnsAsync("Users");

            // Assert
            columns.Should().NotBeNull();
            columns.Should().HaveCount(4); // Id, Name, Email, CreatedAt
            
            var idColumn = columns.FirstOrDefault(c => c.ColumnName == "Id");
            idColumn.Should().NotBeNull();
            idColumn.IsPrimaryKey.Should().BeTrue();
            idColumn.IsAutoIncrement.Should().BeTrue();
            idColumn.DataType.Should().Be("INTEGER");
            
            var emailColumn = columns.FirstOrDefault(c => c.ColumnName == "Email");
            emailColumn.Should().NotBeNull();
            emailColumn.IsUnique.Should().BeFalse(); // We set unique via index, not column constraint
            emailColumn.IsNullable.Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("SQLiteHelper")]
        public async Task GetAllIndexesAsync_ReturnsAllIndexes()
        {
            // Arrange
            await using var helper = new SQLiteHelper(this.connectionString);

            // Act
            var indexes = await helper.GetAllIndexesAsync();

            // Assert
            indexes.Should().NotBeNull();
            indexes.Should().NotBeEmpty();
            
            var emailIndex = indexes.FirstOrDefault(i => i.IndexName == "IX_Users_Email");
            emailIndex.Should().NotBeNull();
            emailIndex.TableName.Should().Be("Users");
            emailIndex.IsUnique.Should().BeTrue();
            emailIndex.Columns.Should().NotBeEmpty();
        }

        [TestMethod]
        [TestCategory("SQLiteHelper")]
        public async Task GetTableIndexesAsync_ReturnsTableSpecificIndexes()
        {
            // Arrange
            await using var helper = new SQLiteHelper(this.connectionString);

            // Act
            var indexes = await helper.GetTableIndexesAsync("Products");

            // Assert
            indexes.Should().NotBeNull();
            var nameIndex = indexes.FirstOrDefault(i => i.IndexName == "IX_Products_Name");
            nameIndex.Should().NotBeNull();
            nameIndex.IsUnique.Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("SQLiteHelper")]
        public async Task GetAllForeignKeysAsync_ReturnsForeignKeys()
        {
            // Arrange
            await using var helper = new SQLiteHelper(this.connectionString);

            // Act
            var foreignKeys = await helper.GetAllForeignKeysAsync();

            // Assert
            foreignKeys.Should().NotBeNull();
            foreignKeys.Should().NotBeEmpty();
            
            var userFk = foreignKeys.FirstOrDefault(fk => fk.FromTable == "Orders" && fk.FromColumn == "UserId");
            userFk.Should().NotBeNull();
            userFk.ToTable.Should().Be("Users");
            userFk.ToColumn.Should().Be("Id");
            userFk.OnDelete.Should().Be("CASCADE");
        }

        [TestMethod]
        [TestCategory("SQLiteHelper")]
        public async Task GetTableCheckConstraintsAsync_ReturnsCheckConstraints()
        {
            // Arrange
            await using var helper = new SQLiteHelper(this.connectionString);

            // Act
            var constraints = await helper.GetTableCheckConstraintsAsync("Products");

            // Assert
            constraints.Should().NotBeNull();
            constraints.Should().HaveCount(1);
            constraints[0].CheckExpression.Should().Contain("Price > 0");
        }

        [TestMethod]
        [TestCategory("SQLiteHelper")]
        public async Task GetTableTriggersAsync_ReturnsTriggers()
        {
            // Arrange
            using (var connection = new SQLiteConnection(this.connectionString))
            {
                await connection.OpenAsync();
                // Create a trigger
                using (var cmd = new SQLiteCommand(@"
                    CREATE TRIGGER UpdateProductTimestamp 
                    AFTER UPDATE ON Products 
                    BEGIN 
                        UPDATE Products SET UpdatedAt = datetime('now') WHERE Id = NEW.Id; 
                    END", connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await using var helper = new SQLiteHelper(this.connectionString);

            // Act
            var triggers = await helper.GetTableTriggersAsync("Products");

            // Assert
            triggers.Should().NotBeNull();
            triggers.Should().HaveCount(1);
            triggers[0].TriggerName.Should().Be("UpdateProductTimestamp");
            triggers[0].TriggerTiming.Should().Be("AFTER");
            triggers[0].TriggerEvent.Should().Be("UPDATE");
        }

        [TestMethod]
        [TestCategory("SQLiteHelper")]
        public async Task GetViewsAsync_ReturnsViews()
        {
            // Arrange
            using (var connection = new SQLiteConnection(this.connectionString))
            {
                await connection.OpenAsync();
                // Create a view
                using (var cmd = new SQLiteCommand(@"
                    CREATE VIEW ActiveUsers AS 
                    SELECT * FROM Users WHERE CreatedAt > date('now', '-30 days')", connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await using var helper = new SQLiteHelper(this.connectionString);

            // Act
            var views = await helper.GetViewsAsync();

            // Assert
            views.Should().NotBeNull();
            views.Should().HaveCount(1);
            views[0].ViewName.Should().Be("ActiveUsers");
            views[0].CreateSql.Should().Contain("SELECT * FROM Users");
        }

        [TestMethod]
        [TestCategory("SQLiteHelper")]
        public async Task GenerateDatabaseReport_CreatesReadableReport()
        {
            // Arrange
            await using var helper = new SQLiteHelper(this.connectionString);
            var dbInfo = await helper.GetDatabaseInfoAsync();

            // Act
            var report = helper.GenerateDatabaseReport(dbInfo);

            // Assert
            report.Should().NotBeNullOrEmpty();
            report.Should().Contain("DATABASE REPORT");
            report.Should().Contain("DATABASE SETTINGS");
            report.Should().Contain("OBJECT SUMMARY");
            report.Should().Contain("TABLES");
            report.Should().Contain("Users");
            report.Should().Contain("Products");
            report.Should().Contain("Orders");
        }

        [TestMethod]
        [TestCategory("SQLiteHelper")]
        public async Task GetDatabaseInfoAsync_HandlesWithoutRowIdTable()
        {
            // Arrange
            using (var connection = new SQLiteConnection(this.connectionString))
            {
                await connection.OpenAsync();
                using (var cmd = new SQLiteCommand(@"
                    CREATE TABLE TestWithoutRowId (
                        Id INTEGER PRIMARY KEY,
                        Data TEXT
                    ) WITHOUT ROWID", connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await using var helper = new SQLiteHelper(this.connectionString);

            // Act
            var dbInfo = await helper.GetDatabaseInfoAsync();

            // Assert
            var table = dbInfo.Tables.FirstOrDefault(t => t.TableName == "TestWithoutRowId");
            table.Should().NotBeNull();
            table.IsWithoutRowId.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("SQLiteHelper")]
        public async Task GetDatabaseInfoAsync_HandlesStrictTable()
        {
            // Skip if SQLite version doesn't support STRICT tables
            using (var connection = new SQLiteConnection(this.connectionString))
            {
                await connection.OpenAsync();
                try
                {
                    using (var cmd = new SQLiteCommand(@"
                        CREATE TABLE TestStrict (
                            Id INTEGER PRIMARY KEY,
                            Amount REAL
                        ) STRICT", connection))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                catch (SQLiteException)
                {
                    // STRICT tables not supported in this SQLite version
                    return;
                }
            }

            await using var helper = new SQLiteHelper(this.connectionString);

            // Act
            var dbInfo = await helper.GetDatabaseInfoAsync();

            // Assert
            var table = dbInfo.Tables.FirstOrDefault(t => t.TableName == "TestStrict");
            table?.IsStrict.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("SQLiteHelper")]
        public async Task GetIndexColumnsAsync_HandlesPartialIndex()
        {
            // Arrange
            using (var connection = new SQLiteConnection(this.connectionString))
            {
                await connection.OpenAsync();
                using (var cmd = new SQLiteCommand(@"
                    CREATE INDEX IX_Products_Active 
                    ON Products(Name) 
                    WHERE Price > 10", connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await using var helper = new SQLiteHelper(this.connectionString);

            // Act
            var dbInfo = await helper.GetDatabaseInfoAsync();

            // Assert
            var index = dbInfo.Indexes.FirstOrDefault(i => i.IndexName == "IX_Products_Active");
            index.Should().NotBeNull();
            index.IsPartial.Should().BeTrue();
            index.WhereClause.Should().Contain("Price > 10");
        }

        private void CreateTestDatabase()
        {
            using var connection = new SQLiteConnection(this.connectionString);
            connection.Open();

            // Enable foreign keys
            using (var cmd = new SQLiteCommand("PRAGMA foreign_keys = ON", connection))
            {
                cmd.ExecuteNonQuery();
            }

            // Create Users table
            using (var cmd = new SQLiteCommand(@"
                CREATE TABLE Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Email TEXT NOT NULL,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                )", connection))
            {
                cmd.ExecuteNonQuery();
            }

            // Create unique index on Email
            using (var cmd = new SQLiteCommand(@"
                CREATE UNIQUE INDEX IX_Users_Email ON Users(Email)", connection))
            {
                cmd.ExecuteNonQuery();
            }

            // Create Products table with check constraint
            using (var cmd = new SQLiteCommand(@"
                CREATE TABLE Products (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Price REAL NOT NULL CHECK(Price > 0),
                    Stock INTEGER DEFAULT 0,
                    UpdatedAt DATETIME
                )", connection))
            {
                cmd.ExecuteNonQuery();
            }

            // Create index on Product Name
            using (var cmd = new SQLiteCommand(@"
                CREATE INDEX IX_Products_Name ON Products(Name)", connection))
            {
                cmd.ExecuteNonQuery();
            }

            // Create Orders table with foreign keys
            using (var cmd = new SQLiteCommand(@"
                CREATE TABLE Orders (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    ProductId INTEGER NOT NULL,
                    Quantity INTEGER NOT NULL,
                    OrderDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
                    FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE RESTRICT
                )", connection))
            {
                cmd.ExecuteNonQuery();
            }

            // Insert sample data
            using (var cmd = new SQLiteCommand(@"
                INSERT INTO Users (Name, Email) VALUES 
                ('John Doe', 'john@example.com'),
                ('Jane Smith', 'jane@example.com')", connection))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new SQLiteCommand(@"
                INSERT INTO Products (Name, Price, Stock) VALUES 
                ('Widget', 19.99, 100),
                ('Gadget', 29.99, 50)", connection))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new SQLiteCommand(@"
                INSERT INTO Orders (UserId, ProductId, Quantity) VALUES 
                (1, 1, 2),
                (2, 2, 1)", connection))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }
}