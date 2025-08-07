// -----------------------------------------------------------------------
// <copyright file="CrudOperationsTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.CorePersistence
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Errors;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using CrudTestEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.CorePersistence.CrudTestEntity;
    using CrudSoftDeleteEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.CorePersistence.CrudSoftDeleteEntity;
    using CrudExpiryEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.CorePersistence.CrudExpiryEntity;

    [TestClass]
    public class CrudOperationsTests
    {
        private string connectionString;
        private SQLitePersistenceProvider<CrudTestEntity, Guid> provider;
        private CallerInfo callerInfo;

        [TestInitialize]
        public async Task Setup()
        {
            this.connectionString = "Data Source=:memory:";
            this.provider = new SQLitePersistenceProvider<CrudTestEntity, Guid>(this.connectionString);
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
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        public async Task CreateAsync_ValidEntity_Success()
        {
            // Arrange
            var entity = new CrudTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Test Entity",
                Status = "Active"
            };

            // Act
            var result = await this.provider.CreateAsync(entity, this.callerInfo);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(entity.Id, result.Id);
            Assert.AreEqual(entity.Name, result.Name);
            Assert.AreEqual(1, result.Version);
            Assert.IsTrue(result.CreatedTime > DateTime.MinValue);
            Assert.AreEqual(result.CreatedTime, result.LastWriteTime);
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        [ExpectedException(typeof(EntityAlreadyExistsException))]
        public async Task CreateAsync_DuplicateKey_ThrowsException()
        {
            // Arrange
            var id = Guid.NewGuid();
            var entity1 = new CrudTestEntity { Id = id, Name = "Entity 1" };
            var entity2 = new CrudTestEntity { Id = id, Name = "Entity 2" };

            // Act
            await this.provider.CreateAsync(entity1, this.callerInfo);
            await this.provider.CreateAsync(entity2, this.callerInfo); // Should throw
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task CreateAsync_NullEntity_ThrowsException()
        {
            // Act
            await this.provider.CreateAsync((CrudTestEntity)null, this.callerInfo);
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        public async Task CreateAsync_SetsTrackingFields()
        {
            // Arrange
            var entity = new CrudTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Test"
            };

            // Act
            var result = await this.provider.CreateAsync(entity, this.callerInfo);

            // Assert
            Assert.AreEqual(1, result.Version, "Version should be set to 1");
            Assert.IsTrue(result.CreatedTime <= DateTime.UtcNow, "CreatedTime should be set");
            Assert.IsTrue(result.LastWriteTime <= DateTime.UtcNow, "LastWriteTime should be set");
            Assert.AreEqual(result.CreatedTime, result.LastWriteTime, 
                "CreatedTime and LastWriteTime should be equal on creation");
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        public async Task CreateAsync_WithSoftDelete_CreatesVersion()
        {
            // Arrange
            var provider = new SQLitePersistenceProvider<CrudSoftDeleteEntity, Guid>(this.connectionString);
            await provider.InitializeAsync();
            
            var entity = new CrudSoftDeleteEntity
            {
                Id = Guid.NewGuid(),
                Name = "Soft Delete Test"
            };

            // Act
            var result = await provider.CreateAsync(entity, this.callerInfo);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Version);
            Assert.IsFalse(result.IsDeleted);
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        public async Task CreateAsync_WithExpiry_SetsExpirationTime()
        {
            // Arrange
            var provider = new SQLitePersistenceProvider<CrudExpiryEntity, Guid>(this.connectionString);
            await provider.InitializeAsync();
            
            var entity = new CrudExpiryEntity
            {
                Id = Guid.NewGuid(),
                Name = "Expiry Test"
            };

            // Act
            var result = await provider.CreateAsync(entity, this.callerInfo);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.AbsoluteExpiration);
            Assert.IsTrue(result.AbsoluteExpiration > DateTime.UtcNow, 
                "AbsoluteExpiration should be set in the future");
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        public async Task GetAsync_ExistingEntity_ReturnsEntity()
        {
            // Arrange
            var entity = new CrudTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Test Entity",
                Status = "Active"
            };
            await this.provider.CreateAsync(entity, this.callerInfo);

            // Act
            var result = await this.provider.GetAsync(entity.Id, this.callerInfo);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(entity.Id, result.Id);
            Assert.AreEqual(entity.Name, result.Name);
            Assert.AreEqual(entity.Status, result.Status);
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        public async Task GetAsync_NonExistentEntity_ReturnsNull()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            // Act
            var result = await this.provider.GetAsync(nonExistentId, this.callerInfo);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        public async Task GetAsync_SoftDeletedEntity_ReturnsNull()
        {
            // Arrange
            var provider = new SQLitePersistenceProvider<CrudSoftDeleteEntity, Guid>(this.connectionString);
            await provider.InitializeAsync();
            
            var entity = new CrudSoftDeleteEntity
            {
                Id = Guid.NewGuid(),
                Name = "To Be Deleted"
            };
            var created = await provider.CreateAsync(entity, this.callerInfo);
            await provider.DeleteAsync(created.Id, this.callerInfo);

            // Act
            var result = await provider.GetAsync(entity.Id, this.callerInfo);

            // Assert
            Assert.IsNull(result, "Soft-deleted entity should not be returned by GetAsync");
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        public async Task GetAsync_ExpiredEntity_ReturnsNull()
        {
            // Arrange
            var provider = new SQLitePersistenceProvider<CrudExpiryEntity, Guid>(this.connectionString);
            await provider.InitializeAsync();
            
            var entity = new CrudExpiryEntity
            {
                Id = Guid.NewGuid(),
                Name = "Will Expire"
            };
            var created = await provider.CreateAsync(entity, this.callerInfo);
            
            // Wait for expiration
            await Task.Delay(1500); // Wait 1.5 seconds (expiry is 1 second)

            // Act
            var result = await provider.GetAsync(entity.Id, this.callerInfo);

            // Assert
            Assert.IsNull(result, "Expired entity should not be returned by GetAsync");
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        public async Task GetByKeyAsync_IncludeAllVersions_ReturnsHistory()
        {
            // Arrange
            var provider = new SQLitePersistenceProvider<CrudSoftDeleteEntity, Guid>(this.connectionString);
            await provider.InitializeAsync();
            
            var entity = new CrudSoftDeleteEntity
            {
                Id = Guid.NewGuid(),
                Name = "Version 1"
            };
            
            var v1 = await provider.CreateAsync(entity, this.callerInfo);
            v1.Name = "Version 2";
            var v2 = await provider.UpdateAsync(v1, this.callerInfo);

            // Act
            var results = await provider.GetByKeyAsync(
                entity.Id, 
                includeAllVersions: true, 
                includeDeleted: false, 
                includeExpired: false,
                callerInfo: this.callerInfo);

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(2, results.Count(), "Should return all versions");
            Assert.IsTrue(results.Any(e => e.Version == 1));
            Assert.IsTrue(results.Any(e => e.Version == 2));
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        public async Task GetByKeyAsync_IncludeDeleted_ReturnsSoftDeleted()
        {
            // Arrange
            var provider = new SQLitePersistenceProvider<CrudSoftDeleteEntity, Guid>(this.connectionString);
            await provider.InitializeAsync();
            
            var entity = new CrudSoftDeleteEntity
            {
                Id = Guid.NewGuid(),
                Name = "To Be Deleted"
            };
            
            var created = await provider.CreateAsync(entity, this.callerInfo);
            await provider.DeleteAsync(created.Id, this.callerInfo);

            // Act
            var results = await provider.GetByKeyAsync(
                entity.Id,
                includeAllVersions: false,
                includeDeleted: true,
                includeExpired: false,
                callerInfo: this.callerInfo);

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count());
            Assert.IsTrue(results[0].IsDeleted);
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        public async Task UpdateAsync_ValidEntity_Success()
        {
            // Arrange
            var entity = new CrudTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Original Name",
                Status = "Active"
            };
            var created = await this.provider.CreateAsync(entity, this.callerInfo);
            
            created.Name = "Updated Name";
            created.Status = "Inactive";

            // Act
            var updated = await this.provider.UpdateAsync(created, this.callerInfo);

            // Assert
            Assert.IsNotNull(updated);
            Assert.AreEqual("Updated Name", updated.Name);
            Assert.AreEqual("Inactive", updated.Status);
            Assert.AreEqual(2, updated.Version);
            Assert.IsTrue(updated.LastWriteTime > created.LastWriteTime);
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        [ExpectedException(typeof(ConcurrencyConflictException))]
        public async Task UpdateAsync_ConcurrencyConflict_ThrowsException()
        {
            // Arrange
            var entity = new CrudTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Original"
            };
            var created = await this.provider.CreateAsync(entity, this.callerInfo);
            
            // Simulate concurrent update
            created.Name = "Update 1";
            await this.provider.UpdateAsync(created, this.callerInfo);
            
            // Try to update with old version
            created.Version = 1; // Reset to old version
            created.Name = "Update 2";

            // Act
            await this.provider.UpdateAsync(created, this.callerInfo); // Should throw
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        [ExpectedException(typeof(EntityNotFoundException))]
        public async Task UpdateAsync_NonExistentEntity_ThrowsException()
        {
            // Arrange
            var entity = new CrudTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Non-existent",
                Version = 1
            };

            // Act
            await this.provider.UpdateAsync(entity, this.callerInfo);
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        public async Task UpdateAsync_IncrementsVersion()
        {
            // Arrange
            var entity = new CrudTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Test"
            };
            var created = await this.provider.CreateAsync(entity, this.callerInfo);

            // Act
            created.Name = "Updated";
            var updated = await this.provider.UpdateAsync(created, this.callerInfo);

            // Assert
            Assert.AreEqual(2, updated.Version);
            
            // Update again
            updated.Name = "Updated Again";
            var updated2 = await this.provider.UpdateAsync(updated, this.callerInfo);
            Assert.AreEqual(3, updated2.Version);
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        public async Task UpdateAsync_WithSoftDelete_CreatesNewVersion()
        {
            // Arrange
            var provider = new SQLitePersistenceProvider<CrudSoftDeleteEntity, Guid>(this.connectionString);
            await provider.InitializeAsync();
            
            var entity = new CrudSoftDeleteEntity
            {
                Id = Guid.NewGuid(),
                Name = "Version 1"
            };
            var created = await provider.CreateAsync(entity, this.callerInfo);

            // Act
            created.Name = "Version 2";
            var updated = await provider.UpdateAsync(created, this.callerInfo);

            // Assert
            Assert.AreEqual(2, updated.Version);
            
            // Verify both versions exist
            var allVersions = await provider.GetByKeyAsync(
                entity.Id,
                includeAllVersions: true,
                includeDeleted: false,
                includeExpired: false,
                this.callerInfo);
            
            Assert.AreEqual(2, allVersions.Count());
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        public async Task DeleteAsync_ExistingEntity_Success()
        {
            // Arrange
            var entity = new CrudTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "To Delete"
            };
            await this.provider.CreateAsync(entity, this.callerInfo);

            // Act
            var result = await this.provider.DeleteAsync(entity.Id, this.callerInfo);

            // Assert
            Assert.IsTrue(result);
            
            // Verify entity is deleted
            var deleted = await this.provider.GetAsync(entity.Id, this.callerInfo);
            Assert.IsNull(deleted);
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        public async Task DeleteAsync_NonExistentEntity_Idempotent()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            // Act
            var result = await this.provider.DeleteAsync(nonExistentId, this.callerInfo);

            // Assert
            Assert.IsTrue(result, "Delete should be idempotent and return true even for non-existent entities");
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        public async Task DeleteAsync_SoftDelete_CreatesDeletedVersion()
        {
            // Arrange
            var provider = new SQLitePersistenceProvider<CrudSoftDeleteEntity, Guid>(this.connectionString);
            await provider.InitializeAsync();
            
            var entity = new CrudSoftDeleteEntity
            {
                Id = Guid.NewGuid(),
                Name = "To Soft Delete"
            };
            var created = await provider.CreateAsync(entity, this.callerInfo);

            // Act
            var result = await provider.DeleteAsync(created.Id, this.callerInfo);

            // Assert
            Assert.IsTrue(result);
            
            // Verify soft delete created a new version
            var allVersions = await provider.GetByKeyAsync(
                entity.Id,
                includeAllVersions: true,
                includeDeleted: true,
                includeExpired: false,
                this.callerInfo);
            
            Assert.IsTrue(allVersions.Count() >= 2, "Should have at least 2 versions after soft delete");
            Assert.IsTrue(allVersions.Any(v => v.IsDeleted), "Should have a deleted version");
        }

        [TestMethod]
        [TestCategory("CorePersistence")]
        [TestCategory("CRUD")]
        public async Task DeleteAsync_HardDelete_RemovesPhysically()
        {
            // Arrange
            var entity = new CrudTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "To Hard Delete"
            };
            await this.provider.CreateAsync(entity, this.callerInfo);

            // Act
            var result = await this.provider.DeleteAsync(entity.Id, this.callerInfo);

            // Assert
            Assert.IsTrue(result);
            
            // Verify entity is physically removed
            var deleted = await this.provider.GetAsync(entity.Id, this.callerInfo);
            Assert.IsNull(deleted);
            
            // Even with include deleted, should not find it (hard delete)
            var allResults = await this.provider.GetByKeyAsync(
                entity.Id,
                includeAllVersions: true,
                includeDeleted: true,
                includeExpired: true,
                this.callerInfo);
            
            Assert.AreEqual(0, allResults.Count(), "Hard delete should physically remove the entity");
        }
    }
}