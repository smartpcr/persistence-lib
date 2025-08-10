// -----------------------------------------------------------------------
// <copyright file="EnumCheckConstraintIntegrationTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Integration
{
    using System;
    using System.Data;
    using System.Data.SQLite;
    using System.IO;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Resilience;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EnumCheckConstraintIntegrationTests
    {
        private string dbPath;
        private string connectionString;

        public enum ProductStatus
        {
            Draft,
            Active,
            Discontinued,
            OutOfStock
        }

        [Table("Product")]
        private class Product : IEntity<Guid>
        {
            [PrimaryKey]
            [Column("Id", SqlDbType.UniqueIdentifier)]
            public Guid Id { get; set; }

            [Column("Name", SqlDbType.NVarChar)]
            public string Name { get; set; }

            [Column("Status", SqlDbType.NVarChar)]
            public ProductStatus Status { get; set; }

            [Column("Version", SqlDbType.BigInt)]
            public long Version { get; set; }

            public DateTimeOffset CreatedTime { get; set; }
            public DateTimeOffset LastWriteTime { get; set; }

            public long EstimateEntitySize() => 100;
        }

        [TestInitialize]
        public void Setup()
        {
            this.dbPath = Path.Combine(Path.GetTempPath(), $"enum_constraint_test_{Guid.NewGuid():N}.db");
            this.connectionString = $"Data Source={this.dbPath};Version=3;";
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(this.dbPath))
            {
                File.Delete(this.dbPath);
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("EnumHandling")]
        public async Task CreateTable_WithEnum_CreatesCheckConstraint()
        {
            // Arrange
            var mapper = new SQLiteEntityMapper<Product, Guid>(new RetryPolicy(0));
            
            // Act - Create table
            using (var connection = new SQLiteConnection(this.connectionString))
            {
                await connection.OpenAsync();
                var createTableSql = mapper.GenerateCreateTableSql();
                using (var command = new SQLiteCommand(createTableSql, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
            }

            // Assert - Verify constraint exists in schema
            using (var connection = new SQLiteConnection(this.connectionString))
            {
                await connection.OpenAsync();
                
                // Query SQLite's schema to verify the check constraint
                var schemaSql = "SELECT sql FROM sqlite_master WHERE type='table' AND name='Product'";
                using (var command = new SQLiteCommand(schemaSql, connection))
                {
                    var tableDefinition = (string)await command.ExecuteScalarAsync();
                    
                    tableDefinition.Should().NotBeNull();
                    tableDefinition.Should().Contain("CHECK");
                    tableDefinition.Should().Contain("Status");
                    tableDefinition.Should().Contain("'Draft'");
                    tableDefinition.Should().Contain("'Active'");
                    tableDefinition.Should().Contain("'Discontinued'");
                    tableDefinition.Should().Contain("'OutOfStock'");
                }
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("EnumHandling")]
        public async Task Insert_WithValidEnumValue_Succeeds()
        {
            // Arrange
            await CreateTableWithConstraints();
            
            // Act & Assert - Should succeed with valid enum value
            using (var connection = new SQLiteConnection(this.connectionString))
            {
                await connection.OpenAsync();
                
                var insertSql = @"
                    INSERT INTO Product (Id, Name, Status, Version, CreatedTime, LastWriteTime) 
                    VALUES (@Id, @Name, @Status, @Version, @CreatedTime, @LastWriteTime)";
                
                using (var command = new SQLiteCommand(insertSql, connection))
                {
                    command.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
                    command.Parameters.AddWithValue("@Name", "Test Product");
                    command.Parameters.AddWithValue("@Status", "Active"); // Valid enum value
                    command.Parameters.AddWithValue("@Version", 1);
                    command.Parameters.AddWithValue("@CreatedTime", DateTimeOffset.UtcNow.ToString("O"));
                    command.Parameters.AddWithValue("@LastWriteTime", DateTimeOffset.UtcNow.ToString("O"));
                    
                    var result = await command.ExecuteNonQueryAsync();
                    result.Should().Be(1); // One row inserted
                }
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("EnumHandling")]
        public async Task Insert_WithInvalidEnumValue_ThrowsConstraintException()
        {
            // Arrange
            await CreateTableWithConstraints();
            
            // Act & Assert - Should fail with invalid enum value
            using (var connection = new SQLiteConnection(this.connectionString))
            {
                await connection.OpenAsync();
                
                var insertSql = @"
                    INSERT INTO Product (Id, Name, Status, Version, CreatedTime, LastWriteTime) 
                    VALUES (@Id, @Name, @Status, @Version, @CreatedTime, @LastWriteTime)";
                
                using (var command = new SQLiteCommand(insertSql, connection))
                {
                    command.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
                    command.Parameters.AddWithValue("@Name", "Test Product");
                    command.Parameters.AddWithValue("@Status", "InvalidStatus"); // Invalid enum value
                    command.Parameters.AddWithValue("@Version", 1);
                    command.Parameters.AddWithValue("@CreatedTime", DateTimeOffset.UtcNow.ToString("O"));
                    command.Parameters.AddWithValue("@LastWriteTime", DateTimeOffset.UtcNow.ToString("O"));
                    
                    Func<Task> action = async () => await command.ExecuteNonQueryAsync();
                    
                    // Should throw constraint violation
                    await action.Should().ThrowAsync<SQLiteException>()
                        .Where(ex => ex.Message.Contains("constraint", StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("EnumHandling")]
        public async Task Update_WithValidEnumValue_Succeeds()
        {
            // Arrange
            await CreateTableWithConstraints();
            var productId = await InsertTestProduct();
            
            // Act & Assert - Update with valid enum value should succeed
            using (var connection = new SQLiteConnection(this.connectionString))
            {
                await connection.OpenAsync();
                
                var updateSql = "UPDATE Product SET Status = @Status WHERE Id = @Id";
                
                using (var command = new SQLiteCommand(updateSql, connection))
                {
                    command.Parameters.AddWithValue("@Status", "Discontinued"); // Valid enum value
                    command.Parameters.AddWithValue("@Id", productId.ToString());
                    
                    var result = await command.ExecuteNonQueryAsync();
                    result.Should().Be(1); // One row updated
                }
                
                // Verify the update
                var selectSql = "SELECT Status FROM Product WHERE Id = @Id";
                using (var command = new SQLiteCommand(selectSql, connection))
                {
                    command.Parameters.AddWithValue("@Id", productId.ToString());
                    var status = (string)await command.ExecuteScalarAsync();
                    status.Should().Be("Discontinued");
                }
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("EnumHandling")]
        public async Task Update_WithInvalidEnumValue_ThrowsConstraintException()
        {
            // Arrange
            await CreateTableWithConstraints();
            var productId = await InsertTestProduct();
            
            // Act & Assert - Update with invalid enum value should fail
            using (var connection = new SQLiteConnection(this.connectionString))
            {
                await connection.OpenAsync();
                
                var updateSql = "UPDATE Product SET Status = @Status WHERE Id = @Id";
                
                using (var command = new SQLiteCommand(updateSql, connection))
                {
                    command.Parameters.AddWithValue("@Status", "UnknownStatus"); // Invalid enum value
                    command.Parameters.AddWithValue("@Id", productId.ToString());
                    
                    Func<Task> action = async () => await command.ExecuteNonQueryAsync();
                    
                    // Should throw constraint violation
                    await action.Should().ThrowAsync<SQLiteException>()
                        .Where(ex => ex.Message.Contains("constraint", StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        private async Task CreateTableWithConstraints()
        {
            var mapper = new SQLiteEntityMapper<Product, Guid>(new RetryPolicy(0));
            
            using (var connection = new SQLiteConnection(this.connectionString))
            {
                await connection.OpenAsync();
                var createTableSql = mapper.GenerateCreateTableSql();
                using (var command = new SQLiteCommand(createTableSql, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<Guid> InsertTestProduct()
        {
            var productId = Guid.NewGuid();
            
            using (var connection = new SQLiteConnection(this.connectionString))
            {
                await connection.OpenAsync();
                
                var insertSql = @"
                    INSERT INTO Product (Id, Name, Status, Version, CreatedTime, LastWriteTime) 
                    VALUES (@Id, @Name, @Status, @Version, @CreatedTime, @LastWriteTime)";
                
                using (var command = new SQLiteCommand(insertSql, connection))
                {
                    command.Parameters.AddWithValue("@Id", productId.ToString());
                    command.Parameters.AddWithValue("@Name", "Test Product");
                    command.Parameters.AddWithValue("@Status", "Active");
                    command.Parameters.AddWithValue("@Version", 1);
                    command.Parameters.AddWithValue("@CreatedTime", DateTimeOffset.UtcNow.ToString("O"));
                    command.Parameters.AddWithValue("@LastWriteTime", DateTimeOffset.UtcNow.ToString("O"));
                    
                    await command.ExecuteNonQueryAsync();
                }
            }
            
            return productId;
        }
    }
}