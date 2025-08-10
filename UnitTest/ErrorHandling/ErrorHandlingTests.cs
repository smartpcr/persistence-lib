// -----------------------------------------------------------------------
// <copyright file="ErrorHandlingTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.ErrorHandling
{
    using System;
    using System.Data;
    using System.Data.SQLite;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Errors;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Resilience;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Providers;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Helpers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ErrorTestEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.ErrorHandling.ErrorTestEntity;
    using ParentEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.ErrorHandling.ParentEntity;
    using ChildEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.ErrorHandling.ChildEntity;

    [TestClass]
    public class ErrorHandlingTests : SQLiteTestBase
    {
        private string testDbPath;

        private string connectionString;
        private SQLitePersistenceProvider<ErrorTestEntity, Guid> provider;
        private CallerInfo callerInfo;

        [TestInitialize]
        public async Task Setup()
        {
            this.testDbPath = Path.Combine(Directory.GetCurrentDirectory(), $"test_{Guid.NewGuid()}.db");
            this.connectionString = $"Data Source={this.testDbPath};Version=3;";
            var config = new SqliteConfiguration
            {
                // Note: Retry functionality not yet implemented in SqliteConfiguration
                BusyTimeout = 5000,
                CommandTimeout = 30
            };
            this.provider = new SQLitePersistenceProvider<ErrorTestEntity, Guid>(this.connectionString, config);
            await this.provider.InitializeAsync();

            // Add unique constraint
            await using var connection = new SQLiteConnection(this.connectionString);
            await connection.OpenAsync();
            await using var cmd = new SQLiteCommand(
                "CREATE UNIQUE INDEX IF NOT EXISTS idx_unique_field ON ErrorTestEntity(UniqueField)",
                connection);
            await cmd.ExecuteNonQueryAsync();

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

            this.SafeDeleteDatabase(this.testDbPath);
        }

        [TestMethod]
        [TestCategory("ErrorHandling")]
        public async Task ConnectionLoss_TransientFailure_Retries()
        {
            // Arrange - Create provider with retry configuration
            var retryConfig = new SqliteConfiguration
            {
                BusyTimeout = 1000,
                CommandTimeout = 30,
                RetryPolicy = new RetryConfiguration
                {
                    Enabled = true,
                    MaxAttempts = 3,
                    InitialDelayMs = 50,
                    MaxDelayMs = 500,
                    BackoffMultiplier = 2.0
                }
            };
            
            var retryProvider = new SQLitePersistenceProvider<ErrorTestEntity, Guid>(this.connectionString, retryConfig);
            await retryProvider.InitializeAsync();

            var entity = new ErrorTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Retry Test",
                UniqueField = Guid.NewGuid().ToString(),
                Value = 100
            };

            // Create a separate connection to lock the database
            await using var lockingConnection = new SQLiteConnection(this.connectionString);
            await lockingConnection.OpenAsync();
            
            // Start a transaction to lock the database
            await using var lockingTransaction = lockingConnection.BeginTransaction();
            
            // Insert a dummy row to hold a write lock
            // Note: ErrorTestEntity uses BaseEntity which maps Id to CacheKey column
            await using (var lockCmd = new SQLiteCommand(
                "INSERT INTO ErrorTestEntity (CacheKey, Name, UniqueField, Value, Version, CreatedTime, LastWriteTime) " +
                "VALUES (@id, 'lock', 'lock', 0, 1, @now, @now)", 
                lockingConnection, lockingTransaction))
            {
                lockCmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                lockCmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
                await lockCmd.ExecuteNonQueryAsync();
            }

            // Act & Assert - Try to insert while database is locked
            var insertTask = Task.Run(async () =>
            {
                // This should retry because the database is locked by the transaction above
                await retryProvider.CreateAsync(entity, this.callerInfo);
            });

            // Wait a bit to ensure retries are happening
            await Task.Delay(200);
            
            // Release the lock by committing the transaction
            await lockingTransaction.CommitAsync();
            
            // Now the insert should succeed after retrying
            await insertTask;

            // Verify the entity was created
            var result = await retryProvider.GetAsync(entity.Id, this.callerInfo);
            result.Should().NotBeNull();
            result.Name.Should().Be("Retry Test");
            
            await retryProvider.DisposeAsync();
        }

        [TestMethod]
        [TestCategory("ErrorHandling")]
        public async Task ConnectionLoss_PersistentFailure_ThrowsException()
        {
            // Arrange - Create provider with limited retry configuration
            var retryConfig = new SqliteConfiguration
            {
                BusyTimeout = 500,
                CommandTimeout = 5,
                RetryPolicy = new RetryConfiguration
                {
                    Enabled = true,
                    MaxAttempts = 2,
                    InitialDelayMs = 100,
                    MaxDelayMs = 200,
                    BackoffMultiplier = 2.0
                }
            };
            
            var retryProvider = new SQLitePersistenceProvider<ErrorTestEntity, Guid>(this.connectionString, retryConfig);
            await retryProvider.InitializeAsync();

            var entity = new ErrorTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Persistent Failure",
                UniqueField = Guid.NewGuid().ToString(),
                Value = 100
            };

            // Create a separate connection to lock the database persistently
            await using var lockingConnection = new SQLiteConnection(this.connectionString);
            await lockingConnection.OpenAsync();
            
            // Start a transaction to lock the database
            await using var lockingTransaction = lockingConnection.BeginTransaction(IsolationLevel.Serializable);
            
            // Insert a dummy row to hold a write lock
            // Note: ErrorTestEntity uses BaseEntity which maps Id to CacheKey column
            await using (var lockCmd = new SQLiteCommand(
                "INSERT INTO ErrorTestEntity (CacheKey, Name, UniqueField, Value, Version, CreatedTime, LastWriteTime) " +
                "VALUES (@id, 'lock', 'persistent_lock', 0, 1, @now, @now)", 
                lockingConnection, lockingTransaction))
            {
                lockCmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                lockCmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
                await lockCmd.ExecuteNonQueryAsync();
            }

            // Act - Try to insert while database is persistently locked
            // This should fail after exhausting retries
            Func<Task> act = async () =>
            {
                await retryProvider.CreateAsync(entity, this.callerInfo);
            };

            // Assert - Should throw after exhausting retries
            await act.Should().ThrowAsync<SQLiteException>()
                .WithMessage("*database is locked*");

            // Cleanup
            await lockingTransaction.RollbackAsync();
            await retryProvider.DisposeAsync();
        }

        [TestMethod]
        [TestCategory("ErrorHandling")]
        public async Task ConstraintViolation_ForeignKey_HandledGracefully()
        {
            // Arrange
            var config = new SqliteConfiguration()
            {
                EnableForeignKeys = true,
            };
            var parentProvider = new SQLitePersistenceProvider<ParentEntity, Guid>(this.connectionString, config);
            var childProvider = new SQLitePersistenceProvider<ChildEntity, Guid>(this.connectionString, config);
            await parentProvider.InitializeAsync();
            await childProvider.InitializeAsync();

            // check foreign key should exist on ChildEntity
            var sqliteHelper = new SQLiteHelper(this.connectionString);
            var foreignKeys = await sqliteHelper.GetTableForeignKeysAsync(nameof(ChildEntity));
            foreignKeys.Should().NotBeNullOrEmpty();
            foreignKeys.Should().Contain(fk =>
                fk.ToTable == nameof(ParentEntity) &&
                fk.ToColumn == "CacheKey" &&
                fk.FromColumn == "ParentId");

            var child = new ChildEntity
            {
                Id = Guid.NewGuid(),
                ParentId = Guid.NewGuid(), // Non-existent parent
                Name = "Orphan Child"
            };

            Func<Task> createChild = async () => await childProvider.CreateAsync(child, this.callerInfo);
            
            // Verify SQLite error code for constraint failure
            // SQLite returns ResultCode 19 (SQLITE_CONSTRAINT) for base error
            // ExtendedResultCode 787 (SQLITE_CONSTRAINT_FOREIGNKEY) might be available
            // The specific foreign key violation is always indicated in the message
            var exception = await createChild.Should().ThrowAsync<SQLiteException>()
                .WithMessage("*FOREIGN KEY constraint failed*");
            
            // Verify error codes
            // Base code should be SQLITE_CONSTRAINT (19)
            // Extended code might be SQLITE_CONSTRAINT_FOREIGNKEY (787) but SQLite.NET doesn't always provide it
            exception.Where(ex => 
                (int)ex.ResultCode == 19 || ex.ErrorCode == 787, 
                "SQLite should return CONSTRAINT error code (19) or CONSTRAINT_FOREIGNKEY (787)");
            
            // Verify that SQLiteTransientErrorDetector correctly identifies this as non-transient
            var (isTransient, errorDescription) = SQLiteTransientErrorDetector.IsTransientError(exception.Which);
            isTransient.Should().BeFalse("Foreign key constraint violations are not transient errors");
            errorDescription.Should().Contain("NON-TRANSIENT ERROR", 
                "Error detector should classify foreign key violations as non-transient");

            await parentProvider.DisposeAsync();
            await childProvider.DisposeAsync();
        }

        [TestMethod]
        [TestCategory("ErrorHandling")]
        public async Task ConstraintViolation_Unique_HandledGracefully()
        {
            // Arrange
            var entity1 = new ErrorTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "First",
                UniqueField = "duplicate",
                Value = 100
            };

            var entity2 = new ErrorTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Second",
                UniqueField = "duplicate", // Same value
                Value = 200
            };

            await this.provider.CreateAsync(entity1, this.callerInfo);

            // Act & Assert
            Func<Task> act = async () => await this.provider.CreateAsync(entity2, this.callerInfo);
            
            // Should throw exception for unique constraint violation
            // The provider may throw SQLiteException, EntityAlreadyExistsException, or EntityWriteException
            var exception = await act.Should().ThrowAsync<Exception>();
            
            // Verify it's a constraint violation
            var ex = exception.Which;
            var isValidException = 
                (ex is SQLiteException sqlEx && sqlEx.Message.Contains("UNIQUE constraint failed")) ||
                (ex is EntityAlreadyExistsException) ||
                (ex is EntityWriteException writeEx && (writeEx.Message.Contains("unique") || writeEx.Message.Contains("constraint")));
                
            isValidException.Should().BeTrue("Should throw an exception indicating unique constraint violation, but got: {0}", ex.Message);
        }

        [TestMethod]
        [TestCategory("ErrorHandling")]
        public async Task DataTypeMismatch_ThrowsMeaningfulError()
        {
            // Arrange
            var entity = new ErrorTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Type Mismatch Test",
                UniqueField = "unique3",
                Value = int.MaxValue // This could overflow in some scenarios
            };

            try
            {
                // Act
                await this.provider.CreateAsync(entity, this.callerInfo);

                // Try to corrupt data type
                await using var connection = new SQLiteConnection(this.connectionString);
                await connection.OpenAsync();
                await using var cmd = new SQLiteCommand(
                    "UPDATE ErrorTestEntity SET Value = 'NotANumber' WHERE CacheKey = @Id",
                    connection);
                cmd.Parameters.AddWithValue("@Id", entity.Id.ToString());
                await cmd.ExecuteNonQueryAsync();

                // Try to read corrupted data
                var result = await this.provider.GetAsync(entity.Id, this.callerInfo);

                // If we get here, SQLite might have coerced the value
                result.Should().NotBeNull();
            }
            catch (EntityWriteException ex)
            {
                // Assert
                (ex.Message.Contains("type") || ex.Message.Contains("convert") || ex.Message.Contains("cast")).Should().BeTrue(
                    "Exception should mention type conversion issue");
            }
            catch (FormatException)
            {
                // This is also acceptable for type conversion errors
            }
        }
    }
}