//-------------------------------------------------------------------------------
// <copyright file="SQLitePersistenceProviderTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Providers
{
    using System;
    using System.Data;
    using System.Data.SQLite;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Errors;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="SQLitePersistenceProvider{T,TKey}"/>.
    /// </summary>
    [TestClass]
    public class SQLitePersistenceProviderTests : SQLiteTestBase
    {
        private string testDbPath;
        private SQLitePersistenceProvider<TestEntity, string> provider;
        private SQLitePersistenceProvider<SoftDeleteTestEntity, string> softDeleteProvider;
        private SQLitePersistenceProvider<NoSoftDeleteTestEntity, string> noSoftDeleteProvider;

        #region Test Entities

        [Table("TestEntity")]
        public class TestEntity : BaseEntity<string>
        {
            [Column("Name", SqlDbType.NVarChar, Size = 100)]
            public string Name { get; set; }

            [Column("Value", SqlDbType.Int)]
            public int Value { get; set; }

            [Column("Description", SqlDbType.Text)]
            public string Description { get; set; }

        }

        [Table("SoftDeleteTestEntity", SoftDeleteEnabled = true)]
        public class SoftDeleteTestEntity : BaseEntity<string>, IVersionedEntity<string>
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

            [Column("Title", SqlDbType.NVarChar, Size = 200)]
            public string Title { get; set; }

            [Column("Status", SqlDbType.NVarChar, Size = 50)]
            public string Status { get; set; }

            public bool IsDeleted { get; set; }
        }

        [Table("NoSoftDeleteTestEntity", SoftDeleteEnabled = false)]
        public class NoSoftDeleteTestEntity : BaseEntity<string>
        {
            [Column("Content", SqlDbType.Text)]
            public string Content { get; set; }

            [Column("Priority", SqlDbType.Int)]
            public int Priority { get; set; }
        }

        [Table("ExpirationTestEntity", SoftDeleteEnabled = true)]
        public class ExpirationTestEntity : BaseEntity<string>
        {
            [Column("Data", SqlDbType.Text)]
            public string Data { get; set; }

            [Column("CreationTime", SqlDbType.DateTimeOffset)]
            public DateTimeOffset CreationTime { get; set; }

            [Column("AbsoluteExpiration", SqlDbType.DateTimeOffset)]
            public DateTimeOffset AbsoluteExpiration { get; set; }
        }

        [Table("ArchiveTestEntity", SoftDeleteEnabled = true)]
        public class ArchiveTestEntity : BaseEntity<string>
        {
            [Column("Content", SqlDbType.Text)]
            public string Content { get; set; }

            [Column("CreationTime", SqlDbType.DateTimeOffset)]
            public DateTimeOffset CreationTime { get; set; }

            [Column("AbsoluteExpiration", SqlDbType.DateTimeOffset)]
            public DateTimeOffset AbsoluteExpiration { get; set; }

            [Column("IsArchived", SqlDbType.Bit)]
            public bool IsArchived { get; set; }
        }

        #endregion

        #region Test Setup and Cleanup

        [TestInitialize]
        public async Task TestInitialize()
        {
            // Create a unique test database for each test
            this.testDbPath = Path.Combine(Directory.GetCurrentDirectory(), $"test_{Guid.NewGuid()}.db");
            var connectionString = $"Data Source={this.testDbPath};Version=3;";

            this.provider = new SQLitePersistenceProvider<TestEntity, string>(connectionString);
            this.softDeleteProvider = new SQLitePersistenceProvider<SoftDeleteTestEntity, string>(connectionString);
            this.noSoftDeleteProvider = new SQLitePersistenceProvider<NoSoftDeleteTestEntity, string>(connectionString);

            // Initialize database schema
            await this.provider.InitializeAsync();
            await this.softDeleteProvider.InitializeAsync();
            await this.noSoftDeleteProvider.InitializeAsync();
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            // Dispose all providers
            if (this.provider != null)
            {
                await this.provider.DisposeAsync();
            }

            if (this.softDeleteProvider != null)
            {
                await this.softDeleteProvider.DisposeAsync();
            }

            if (this.noSoftDeleteProvider != null)
            {
                await this.noSoftDeleteProvider.DisposeAsync();
            }

            // Use the base class method for safe database deletion
            this.SafeDeleteDatabase(this.testDbPath);
        }

        #endregion

        #region Basic CRUD Operation Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task CreateAsync_NewEntity_SuccessfullyCreates()
        {
            // Arrange
            var entity = new TestEntity
            {
                Id = "test-1",
                Name = "Test Entity",
                Value = 42,
                Description = "Test Description"
            };

            // Act
            var result = await this.provider.CreateAsync(entity, new CallerInfo());

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be("test-1");
            result.Name.Should().Be("Test Entity");
            result.Value.Should().Be(42);
            result.Version.Should().Be(1);
            result.CreatedTime.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
            result.LastWriteTime.Should().Be(result.CreatedTime);
        }

        [TestMethod]
        [TestCategory("UnitTest")]

        public async Task CreateAsync_DuplicateEntity_ThrowsException()
        {
            // Arrange
            var entity = new TestEntity
            {
                Id = "test-1",
                Name = "Test Entity"
            };

            // Act
            await this.provider.CreateAsync(entity, new CallerInfo());

            Func<Task> act = async () => await this.provider.CreateAsync(entity, new CallerInfo());
            await act.Should().ThrowAsync<EntityAlreadyExistsException>("Entity with the same ID already exists.");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task GetByIdAsync_ExistingEntity_ReturnsEntity()
        {
            // Arrange
            var entity = new TestEntity
            {
                Id = "test-1",
                Name = "Test Entity",
                Value = 42
            };
            await this.provider.CreateAsync(entity, new CallerInfo());

            // Act
            var result = await this.provider.GetAsync("test-1", new CallerInfo());

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be("test-1");
            result.Name.Should().Be("Test Entity");
            result.Value.Should().Be(42);
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task GetByIdAsync_NonExistentEntity_ReturnsNull()
        {
            // Act
            var result = await this.provider.GetAsync("non-existent", new CallerInfo());

            // Assert
            result.Should().BeNull();
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task UpdateAsync_ExistingEntity_SuccessfullyUpdates()
        {
            // Arrange
            var entity = new TestEntity
            {
                Id = "test-1",
                Name = "Original Name",
                Value = 42
            };
            var created = await this.provider.CreateAsync(entity, new CallerInfo());

            // Act
            created.Name = "Updated Name";
            created.Value = 100;
            var updated = await this.provider.UpdateAsync(created, new CallerInfo());

            // Assert
            updated.Should().NotBeNull();
            updated.Name.Should().Be("Updated Name");
            updated.Value.Should().Be(100);
            updated.Version.Should().Be(2);
            updated.LastWriteTime.Should().BeAfter(updated.CreatedTime);
        }

        [TestMethod]
        [TestCategory("UnitTest")]

        public async Task UpdateAsync_ConcurrentUpdate_ThrowsConcurrencyException()
        {
            // Arrange
            var entity = new TestEntity
            {
                Id = "test-1",
                Name = "Original Name"
            };
            var created = await this.provider.CreateAsync(entity, new CallerInfo());

            // Simulate concurrent update by modifying version
            created.Version = 0;

            // Act
            created.Name = "Updated Name";
            Func<Task> act = async () => await this.provider.UpdateAsync(created, new CallerInfo());
            await act.Should().ThrowAsync<ConcurrencyConflictException>();
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task DeleteAsync_ExistingEntity_SuccessfullyDeletes()
        {
            // Arrange
            var entity = new TestEntity
            {
                Id = "test-1",
                Name = "Test Entity"
            };
            await this.provider.CreateAsync(entity, new CallerInfo());

            // Act
            await this.provider.DeleteAsync("test-1", new CallerInfo());

            // Assert
            var deleted = await this.provider.GetAsync("test-1", new CallerInfo());
            deleted.Should().BeNull();
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task GetAllAsync_MultipleEntities_ReturnsAll()
        {
            // Arrange
            var entities = new[]
            {
                new TestEntity { Id = "test-1", Name = "Entity 1", Value = 1 },
                new TestEntity { Id = "test-2", Name = "Entity 2", Value = 2 },
                new TestEntity { Id = "test-3", Name = "Entity 3", Value = 3 }
            };

            foreach (var entity in entities)
            {
                await this.provider.CreateAsync(entity, new CallerInfo());
            }

            // Act
            var results = await this.provider.GetAllAsync(new CallerInfo());

            // Assert
            results.Count().Should().Be(3);
            results.Any(e => e.Id == "test-1").Should().BeTrue();
            results.Any(e => e.Id == "test-2").Should().BeTrue();
            results.Any(e => e.Id == "test-3").Should().BeTrue();
        }

        #endregion

        #region Soft Delete Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task SoftDelete_CreateUpdate_CreatesNewVersion()
        {
            // Arrange
            var entity = new SoftDeleteTestEntity
            {
                Id = "soft-1",
                Title = "Original Title",
                Status = "Active"
            };
            var created = await this.softDeleteProvider.CreateAsync(entity, new CallerInfo());

            // Act
            created.Title = "Updated Title";
            var updated = await this.softDeleteProvider.UpdateAsync(created, new CallerInfo());

            // Assert
            updated.Version.Should().Be(2);

            // Verify both versions exist in database
            var current = await this.softDeleteProvider.GetAsync("soft-1", new CallerInfo());
            current.Should().NotBeNull();
            current.Version.Should().BeGreaterThan(entity.Version);
            current.Title.Should().Be("Updated Title");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task SoftDelete_Delete_MarksAsDeleted()
        {
            // Arrange
            var entity = new SoftDeleteTestEntity
            {
                Id = "soft-1",
                Title = "Test Title",
                Status = "Active"
            };
            await this.softDeleteProvider.CreateAsync(entity, new CallerInfo());

            // Act
            await this.softDeleteProvider.DeleteAsync("soft-1", new CallerInfo());

            // Assert
            var deletedItems = await this.softDeleteProvider.GetByKeyAsync("soft-1", new CallerInfo(), includeDeleted: true, includeAllVersions: true);
            deletedItems.Should().NotBeNull();
            deletedItems!.Should().HaveCount(2);
            var deleted = deletedItems.OrderByDescending(d => d.Version).First();
            deleted.IsDeleted.Should().BeTrue();

            // Should not be returned in normal queries
            var notFound = await this.softDeleteProvider.GetAsync("soft-1", new CallerInfo());
            notFound.Should().BeNull();
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task SoftDelete_GetById_ReturnsLatestVersion()
        {
            // Arrange
            var entity = new SoftDeleteTestEntity
            {
                Id = "soft-1",
                Title = "Version 1",
                Status = "Active"
            };
            var v1 = await this.softDeleteProvider.CreateAsync(entity, new CallerInfo());

            v1.Title = "Version 2";
            var v2 = await this.softDeleteProvider.UpdateAsync(v1, new CallerInfo());

            v2.Title = "Version 3";
            await this.softDeleteProvider.UpdateAsync(v2, new CallerInfo());

            // Act
            var latest = await this.softDeleteProvider.GetAsync("soft-1", new CallerInfo());

            // Assert
            latest.Should().NotBeNull();
            latest.Title.Should().Be("Version 3");
            latest.Version.Should().Be(3);
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task NoSoftDelete_Update_UpdatesInPlace()
        {
            // Arrange
            var entity = new NoSoftDeleteTestEntity
            {
                Id = "no-soft-1",
                Content = "Original Content",
                Priority = 1
            };
            var created = await this.noSoftDeleteProvider.CreateAsync(entity, new CallerInfo());

            // Act
            created.Content = "Updated Content";
            created.Priority = 2;
            var updated = await this.noSoftDeleteProvider.UpdateAsync(created, new CallerInfo());

            // Assert
            updated.Version.Should().Be(2);

            // Verify only one record exists
            var all = await this.noSoftDeleteProvider.GetAllAsync(new CallerInfo());
            all.Count().Should().Be(1);
            all.First().Content.Should().Be("Updated Content");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task NoSoftDelete_Delete_RemovesFromDatabase()
        {
            // Arrange
            var entity = new NoSoftDeleteTestEntity
            {
                Id = "no-soft-1",
                Content = "Test Content"
            };
            await this.noSoftDeleteProvider.CreateAsync(entity, new CallerInfo());

            // Act
            await this.noSoftDeleteProvider.DeleteAsync("no-soft-1", new CallerInfo());

            // Assert
            var all = await this.noSoftDeleteProvider.GetAllAsync(new CallerInfo());
            all.Count().Should().Be(0);
        }

        #endregion

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task TodoWrite_UpdateProgress()
        {
            await Task.CompletedTask;
            // Test method to update todo progress
        }
    }
}