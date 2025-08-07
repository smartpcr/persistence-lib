// -----------------------------------------------------------------------
// <copyright file="TransactionScopeTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Transaction
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using TransactionTestEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.Transaction.TransactionTestEntity;

    [TestClass]
    public class TransactionScopeTests
    {
        private string connectionString;
        private SQLitePersistenceProvider<TransactionTestEntity, Guid> provider;
        private CallerInfo callerInfo;

        [TestInitialize]
        public async Task Setup()
        {
            this.connectionString = "Data Source=:memory:";
            this.provider = new SQLitePersistenceProvider<TransactionTestEntity, Guid>(this.connectionString);
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
        }

        [TestMethod]
        [TestCategory("Transaction")]
        public async Task BeginTransaction_CreateUpdateDelete_Atomic()
        {
            // Arrange
            var entity1 = new TransactionTestEntity { Id = Guid.NewGuid(), Name = "Entity 1", Value = 100 };
            var entity2 = new TransactionTestEntity { Id = Guid.NewGuid(), Name = "Entity 2", Value = 200 };
            var entity3 = new TransactionTestEntity { Id = Guid.NewGuid(), Name = "Entity 3", Value = 300 };
            
            // Create entity3 outside transaction
            await this.provider.CreateAsync(entity3, this.callerInfo);

            // Act
            using (var scope = await this.provider.BeginTransactionAsync())
            {
                // Create
                await scope.CreateAsync(entity1, this.callerInfo);
                
                // Update
                entity2.Name = "Updated Entity 2";
                await scope.CreateAsync(entity2, this.callerInfo);
                await scope.UpdateAsync(entity2, this.callerInfo);
                
                // Delete
                await scope.DeleteAsync(entity3.Id, this.callerInfo);
                
                await scope.CommitAsync();
            }

            // Assert
            var result1 = await this.provider.GetAsync(entity1.Id, this.callerInfo);
            Assert.IsNotNull(result1);
            Assert.AreEqual("Entity 1", result1.Name);
            
            var result2 = await this.provider.GetAsync(entity2.Id, this.callerInfo);
            Assert.IsNotNull(result2);
            Assert.AreEqual("Updated Entity 2", result2.Name);
            
            var result3 = await this.provider.GetAsync(entity3.Id, this.callerInfo);
            Assert.IsNull(result3);
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
            using (var scope = await this.provider.BeginTransactionAsync())
            {
                await scope.CreateAsync(entity1, this.callerInfo);
                
                entity2.Name = "Should Not Be Updated";
                await scope.UpdateAsync(entity2, this.callerInfo);
                
                await scope.RollbackAsync();
            }

            // Assert
            var result1 = await this.provider.GetAsync(entity1.Id, this.callerInfo);
            Assert.IsNull(result1, "Entity should not exist after rollback");
            
            var result2 = await this.provider.GetAsync(entity2.Id, this.callerInfo);
            Assert.IsNotNull(result2);
            Assert.AreEqual("Original", result2.Name, "Entity should not be updated after rollback");
        }

        [TestMethod]
        [TestCategory("Transaction")]
        public async Task BeginTransaction_NestedScope_HandlesCorrectly()
        {
            // Arrange
            var entity1 = new TransactionTestEntity { Id = Guid.NewGuid(), Name = "Outer", Value = 100 };
            var entity2 = new TransactionTestEntity { Id = Guid.NewGuid(), Name = "Inner", Value = 200 };

            // Act
            using (var outerScope = await this.provider.BeginTransactionAsync())
            {
                await outerScope.CreateAsync(entity1, this.callerInfo);
                
                using (var innerScope = await this.provider.BeginTransactionAsync())
                {
                    await innerScope.CreateAsync(entity2, this.callerInfo);
                    await innerScope.CommitAsync();
                }
                
                await outerScope.CommitAsync();
            }

            // Assert
            var result1 = await this.provider.GetAsync(entity1.Id, this.callerInfo);
            Assert.IsNotNull(result1);
            
            var result2 = await this.provider.GetAsync(entity2.Id, this.callerInfo);
            Assert.IsNotNull(result2);
        }

        [TestMethod]
        [TestCategory("Transaction")]
        [ExpectedException(typeof(TimeoutException))]
        public async Task BeginTransaction_Timeout_RollsBack()
        {
            // Arrange
            var entity = new TransactionTestEntity { Id = Guid.NewGuid(), Name = "Timeout Test", Value = 100 };
            
            // Act
            using (var scope = await this.provider.BeginTransactionAsync(TimeSpan.FromMilliseconds(100)))
            {
                await scope.CreateAsync(entity, this.callerInfo);
                
                // Simulate long-running operation
                await Task.Delay(200);
                
                await scope.CommitAsync(); // Should timeout and throw
            }
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

            // Act - Start two concurrent transactions
            var task1 = Task.Run(async () =>
            {
                using (var scope = await this.provider.BeginTransactionAsync())
                {
                    entity.Name = "Transaction 1";
                    entity.Value = 200;
                    await scope.UpdateAsync(entity, this.callerInfo);
                    
                    // Signal transaction 2 to start
                    transaction2Start.SetResult(true);
                    
                    // Wait a bit to ensure transaction 2 tries to access
                    await Task.Delay(100);
                    
                    await scope.CommitAsync();
                    transaction1Complete.SetResult(true);
                }
            });
            
            var task2 = Task.Run(async () =>
            {
                await transaction2Start.Task;
                
                using (var scope = await this.provider.BeginTransactionAsync())
                {
                    // This should wait for transaction 1 to complete
                    var current = await scope.GetAsync(entity.Id, this.callerInfo);
                    Assert.IsNotNull(current);
                    
                    current.Name = "Transaction 2";
                    current.Value = 300;
                    await scope.UpdateAsync(current, this.callerInfo);
                    await scope.CommitAsync();
                }
            });

            // Wait for both transactions
            await Task.WhenAll(task1, task2);

            // Assert - Transaction 2 should have the final update
            var result = await this.provider.GetAsync(entity.Id, this.callerInfo);
            Assert.IsNotNull(result);
            Assert.AreEqual("Transaction 2", result.Name);
            Assert.AreEqual(300, result.Value);
        }
    }
}