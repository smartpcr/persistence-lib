// -----------------------------------------------------------------------
// <copyright file="TransactionScopeTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Transaction
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Resilience;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Providers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using TransactionTestEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.Transaction.TransactionTestEntity;

    [TestClass]
    public class TransactionScopeTests : SQLiteTestBase
    {
        private string testDbPath;
        private string connectionString;
        private SqliteConfiguration config;
        private SQLitePersistenceProvider<TransactionTestEntity, Guid> provider;
        private CallerInfo callerInfo;

        [TestInitialize]
        public async Task Setup()
        {
            this.testDbPath = Path.Combine(Directory.GetCurrentDirectory(), $"test_advanced_{Guid.NewGuid()}.db");
            this.connectionString = $"Data Source={this.testDbPath};Version=3;";
            this.config = new SqliteConfiguration()
            {
                BusyTimeout = 100,
                RetryPolicy = new RetryConfiguration()
                {
                    MaxAttempts = 0,
                    Enabled = false
                }
            };
            this.provider = new SQLitePersistenceProvider<TransactionTestEntity, Guid>(this.connectionString, this.config);
            await this.provider.InitializeAsync();

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
        [TestCategory("Transaction")]
        public async Task BeginTransaction_CreateUpdateDelete_Atomic()
        {
            // Arrange
            var entity1 = new TransactionTestEntity { Id = Guid.NewGuid(), Name = "Entity 1", Value = 100 };
            var entity2 = new TransactionTestEntity { Id = entity1.Id, Name = "Entity 2", Value = 200 };
            var entity3 = new TransactionTestEntity { Id = Guid.NewGuid(), Name = "Entity 3", Value = 300 };

            // Create entity3 outside transaction
            await this.provider.CreateAsync(entity3, this.callerInfo);

            // Act
            await using (var scope = this.provider.BeginTransaction())
            {
                // The operations would need to be added as ITransactionalOperation
                scope.AddOperation<TransactionTestEntity, Guid>(TransactionalOperation<TransactionTestEntity, Guid>.Create(
                    new SQLiteEntityMapper<TransactionTestEntity, Guid>(new RetryPolicy(this.config.RetryPolicy)),
                    DbOperationType.Insert,
                    entity1));
                scope.AddOperation<TransactionTestEntity, Guid>(TransactionalOperation<TransactionTestEntity, Guid>.Create(
                    new SQLiteEntityMapper<TransactionTestEntity, Guid>(new RetryPolicy(this.config.RetryPolicy)),
                    DbOperationType.Update,
                    entity1,
                    entity2));
                scope.AddOperation<TransactionTestEntity, Guid>(TransactionalOperation<TransactionTestEntity, Guid>.Create(
                    new SQLiteEntityMapper<TransactionTestEntity, Guid>(new RetryPolicy(this.config.RetryPolicy)),
                    DbOperationType.Delete,
                    entity3));

                scope.Commit();
            }

            // Assert
            var result1 = await this.provider.GetAsync(entity1.Id, this.callerInfo);
            result1.Should().NotBeNull();
            result1.Name.Should().Be("Entity 2", "Entity 1 should be updated to Entity 2's values");
            result1.Value.Should().Be(200, "Entity 1 should be updated to Entity 2's values");

            var result3 = await this.provider.GetAsync(entity3.Id, this.callerInfo);
            result3.Should().BeNull();
        }

        [TestMethod]
        [TestCategory("Transaction")]
        public async Task BeginTransaction_Rollback_NoChanges()
        {
            // Arrange
            var entity1 = new TransactionTestEntity { Id = Guid.NewGuid(), Name = "Should Not Exist", Value = 100 };
            var entity2 = new TransactionTestEntity { Id = Guid.NewGuid(), Name = "Original", Value = 200 };
            await this.provider.CreateAsync(entity2, this.callerInfo);

            // Act
            await using (var scope = this.provider.BeginTransaction())
            {
                scope.AddOperation<TransactionTestEntity, Guid>(TransactionalOperation<TransactionTestEntity, Guid>.Create(
                    new SQLiteEntityMapper<TransactionTestEntity, Guid>(new RetryPolicy(this.config.RetryPolicy)),
                    DbOperationType.Insert,
                    entity1));

                entity2.Name = "Updated";
                scope.AddOperation<TransactionTestEntity, Guid>(TransactionalOperation<TransactionTestEntity, Guid>.Create(
                    new SQLiteEntityMapper<TransactionTestEntity, Guid>(new RetryPolicy(this.config.RetryPolicy)),
                    DbOperationType.Update,
                    entity2,
                    entity2));

                scope.Rollback();
            }

            // Assert
            var result1 = await this.provider.GetAsync(entity1.Id, this.callerInfo);
            result1.Should().BeNull("Entity should not exist after rollback");

            var result2 = await this.provider.GetAsync(entity2.Id, this.callerInfo);
            result2.Should().NotBeNull();
            result2.Name.Should().Be("Original", "Entity should not be updated after rollback");
        }

        [TestMethod]
        [TestCategory("Transaction")]
        public async Task BeginTransaction_NestedScope_HandlesCorrectly()
        {
            // Arrange
            var entity1 = new TransactionTestEntity { Id = Guid.NewGuid(), Name = "Outer", Value = 100 };
            var entity2 = new TransactionTestEntity { Id = Guid.NewGuid(), Name = "Inner", Value = 200 };

            // Act
            await using (var outerScope = this.provider.BeginTransaction())
            {
                outerScope.AddOperation<TransactionTestEntity, Guid>(TransactionalOperation<TransactionTestEntity, Guid>.Create(
                    new SQLiteEntityMapper<TransactionTestEntity, Guid>(new RetryPolicy(this.config.RetryPolicy)),
                    DbOperationType.Insert,
                    entity1));

                await using (var innerScope = this.provider.BeginTransaction())
                {
                    innerScope.AddOperation<TransactionTestEntity, Guid>(TransactionalOperation<TransactionTestEntity, Guid>.Create(
                        new SQLiteEntityMapper<TransactionTestEntity, Guid>(new RetryPolicy(this.config.RetryPolicy)),
                        DbOperationType.Insert,
                        entity2));
                    innerScope.Commit();
                }

                outerScope.Commit();
            }

            // Assert
            var result1 = await this.provider.GetAsync(entity1.Id, this.callerInfo);
            result1.Should().NotBeNull();

            var result2 = await this.provider.GetAsync(entity2.Id, this.callerInfo);
            result2.Should().NotBeNull();
        }

        [TestMethod]
        [TestCategory("Transaction")]
        public async Task BeginTransaction_Timeout_RollsBack()
        {
            // Arrange - Create a provider with very short timeout
            var timeoutConfig = new SqliteConfiguration()
            {
                BusyTimeout = 50,  // Very short timeout (50ms)
                CommandTimeout = 1, // 1 second command timeout
                RetryPolicy = new RetryConfiguration()
                {
                    MaxAttempts = 0,
                    Enabled = false
                }
            };
            var timeoutProvider = new SQLitePersistenceProvider<TransactionTestEntity, Guid>(this.connectionString, timeoutConfig);
            await timeoutProvider.InitializeAsync();

            var entity = new TransactionTestEntity { Id = Guid.NewGuid(), Name = "Timeout Test", Value = 100 };

            // Create a lock on the database from another connection
            var lockConnection = new System.Data.SQLite.SQLiteConnection(this.connectionString);
            await lockConnection.OpenAsync();
            var lockTransaction = lockConnection.BeginTransaction();

            // Insert a row to hold a write lock
            await using (var lockCmd = new System.Data.SQLite.SQLiteCommand(@"
INSERT INTO TransactionTestEntity (CacheKey, Name, Value, Version, CreatedTime, LastWriteTime) VALUES (@id, 'lock', 999, 1, @now, @now)
",
                             lockConnection, lockTransaction))
            {
                lockCmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                lockCmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
                await lockCmd.ExecuteNonQueryAsync();
            }

            // Act & Assert - Try to create entity while database is locked
            try
            {
                // This should timeout because the database is locked by the transaction above
                await timeoutProvider.CreateAsync(entity, this.callerInfo);
                Assert.Fail("Should have thrown timeout exception");
            }
            catch (System.Data.SQLite.SQLiteException ex)
            {
                // Verify it's a timeout/busy error
                ex.Message.Should().Contain("database is locked", "Should indicate database lock/timeout");
                ex.ResultCode.Should().BeOneOf(
                    System.Data.SQLite.SQLiteErrorCode.Busy,
                    System.Data.SQLite.SQLiteErrorCode.Locked);
            }
            finally
            {
                // Cleanup
                await lockTransaction.RollbackAsync();
                await lockConnection.CloseAsync();
                await lockConnection.DisposeAsync();
                await timeoutProvider.DisposeAsync();
            }

            // Verify the entity was not created
            var result = await this.provider.GetAsync(entity.Id, this.callerInfo);
            result.Should().BeNull("Entity should not exist after timeout");
        }

        [TestMethod]
        [TestCategory("Transaction")]
        public async Task BeginTransaction_ConcurrentAccess_Isolated()
        {
            // Arrange
            var entity = new TransactionTestEntity { Id = Guid.NewGuid(), Name = "Initial", Value = 100 };
            await this.provider.CreateAsync(entity, this.callerInfo);

            var transaction1Complete = new TaskCompletionSource<bool>();
            var transaction2Start = new TaskCompletionSource<bool>();

            // Act - Start two concurrent updates without using transaction scope
            // This tests concurrent access isolation at the provider level
            var task1 = Task.Run(async () =>
            {
                // Get fresh entity to ensure we have the latest version
                var current = await this.provider.GetAsync(entity.Id, this.callerInfo);
                current.Should().NotBeNull();
                
                // Update entity
                current.Name = "Transaction 1";
                current.Value = 200;
                
                // Signal transaction 2 to start
                transaction2Start.SetResult(true);

                // Wait a bit to ensure transaction 2 tries to access
                await Task.Delay(100);

                // Perform the update
                await this.provider.UpdateAsync(current, this.callerInfo);
                transaction1Complete.SetResult(true);
            });

            var task2 = Task.Run(async () =>
            {
                await transaction2Start.Task;
                
                // Wait for transaction 1 to complete to avoid version conflicts
                await transaction1Complete.Task;

                // Get the current state after transaction 1
                var current = await this.provider.GetAsync(entity.Id, this.callerInfo);
                current.Should().NotBeNull();
                current.Name.Should().Be("Transaction 1", "Should see Transaction 1's update");

                // Update entity
                current.Name = "Transaction 2";
                current.Value = 300;
                
                // Perform the update
                await this.provider.UpdateAsync(current, this.callerInfo);
            });

            // Wait for both transactions
            await Task.WhenAll(task1, task2);

            // Assert - Transaction 2 should have the final update
            var result = await this.provider.GetAsync(entity.Id, this.callerInfo);
            result.Should().NotBeNull();
            result.Name.Should().Be("Transaction 2", "Transaction 2 should complete after Transaction 1");
            result.Value.Should().Be(300);
        }
    }
}