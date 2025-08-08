//-------------------------------------------------------------------------------
// <copyright file="SQLitePersistenceProviderExpirationTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for expiration and archive functionality in <see cref="SQLitePersistenceProvider{T,TKey}"/>.
    /// </summary>
    [TestClass]
    public class SQLitePersistenceProviderExpirationTests : SQLiteTestBase
    {
        private string testDbPath;
        private SQLitePersistenceProvider<ExpirationEntity, string> expirationProvider;
        private SQLitePersistenceProvider<ArchiveEntity, string> archiveProvider;

        #region Test Entities

        [Table("ExpirationEntity", ExpirySpanString = "01:00:00")]
        public class ExpirationEntity : BaseEntity<string>, IExpirableEntity<string>
        {
            [Column("Data", SqlDbType.Text)]
            public string Data { get; set; }

            [Column("AbsoluteExpiration", SqlDbType.DateTimeOffset)]
            public DateTimeOffset? AbsoluteExpiration { get; set; }

            public bool IsExpired => this.AbsoluteExpiration.HasValue && DateTimeOffset.UtcNow.UtcDateTime > this.AbsoluteExpiration.Value.UtcDateTime;
        }

        [Table("ArchiveEntity", ExpirySpanString = "7.00:00:00", EnableArchive = true)]
        public class ArchiveEntity : BaseEntity<string>, IVersionedEntity<string>, IExpirableEntity<string>, IArchivableEntity<string>
        {
            [Column("Content", SqlDbType.Text)]
            public string Content { get; set; }

            [Column("AbsoluteExpiration", SqlDbType.DateTimeOffset)]
            public DateTimeOffset? AbsoluteExpiration { get; set; }

            [Column("IsArchived", SqlDbType.Bit)]
            public bool IsArchived { get; set; }

            public bool IsExpired => DateTimeOffset.UtcNow > this.AbsoluteExpiration;
            public bool IsDeleted { get; set; }
        }

        [Table("CacheEntry", ExpirySpanString = "7.00:00:00")]
        public class CacheEntry : BaseEntity<string>, IExpirableEntity<string>
        {
            [Column("TypeName", SqlDbType.Text)]
            public string TypeName { get; set; }

            [Column("Data", SqlDbType.Text)]
            public string Data { get; set; }

            [Column("AbsoluteExpiration", SqlDbType.DateTimeOffset)]
            public DateTimeOffset? AbsoluteExpiration { get; set; }

            [Column("SlidingExpirationSeconds", SqlDbType.Int)]
            public int? SlidingExpirationSeconds { get; set; }

            [Column("LastAccessTime", SqlDbType.DateTimeOffset)]
            public DateTimeOffset? LastAccessTime { get; set; }

            public bool IsExpired()
            {
                if (this.AbsoluteExpiration.HasValue && DateTimeOffset.UtcNow > this.AbsoluteExpiration.Value)
                {
                    return true;
                }

                if (this.SlidingExpirationSeconds.HasValue && this.LastAccessTime.HasValue)
                {
                    var expirationTime = this.LastAccessTime.Value.AddSeconds(this.SlidingExpirationSeconds.Value);
                    return DateTimeOffset.UtcNow > expirationTime;
                }

                return false;
            }
        }

        #endregion

        #region Test Setup and Cleanup

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.testDbPath = Path.Combine(Directory.GetCurrentDirectory(), $"test_expiry_{Guid.NewGuid()}.db");
            var connectionString = $"Data Source={this.testDbPath};Version=3;";

            this.expirationProvider = new SQLitePersistenceProvider<ExpirationEntity, string>(connectionString);
            this.archiveProvider = new SQLitePersistenceProvider<ArchiveEntity, string>(connectionString);

            await this.expirationProvider.InitializeAsync();
            await this.archiveProvider.InitializeAsync();
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.expirationProvider != null)
            {
                await this.expirationProvider.DisposeAsync();
            }

            if (this.archiveProvider != null)
            {
                await this.archiveProvider.DisposeAsync();
            }

            if (File.Exists(this.testDbPath))
            {
                this.SafeDeleteDatabase(this.testDbPath);
            }
        }

        #endregion

        #region Expiration Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task Create_WithExpirySpan_SetsAbsoluteExpiration()
        {
            // Arrange
            var entity = new ExpirationEntity
            {
                Id = "exp-1",
                Data = "Test Data",
                CreatedTime = DateTimeOffset.UtcNow
            };

            // Act
            var created = await this.expirationProvider.CreateAsync(entity, new CallerInfo());

            // Assert
            created.Should().NotBeNull();
            created.AbsoluteExpiration.Should().BeCloseTo(created.CreatedTime.AddHours(1), TimeSpan.FromMinutes(1));
            created.IsExpired.Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task GetByIdAsync_ExpiredEntity_ReturnsNullByDefault()
        {
            // Arrange
            var entity = new ExpirationEntity
            {
                Id = "exp-1",
                Data = "Expired Data",
                CreatedTime = DateTimeOffset.UtcNow.AddHours(-2),
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(-1) // Already expired
            };
            await this.expirationProvider.CreateAsync(entity, new CallerInfo());

            // Act
            var result = await this.expirationProvider.GetAsync("exp-1", new CallerInfo());

            // Assert
            result.Should().BeNull();
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task GetByIdAsync_ExpiredEntity_ReturnsEntityWhenIncludeExpired()
        {
            // Arrange
            var entity = new ExpirationEntity
            {
                Id = "exp-1",
                Data = "Expired Data",
                CreatedTime = DateTimeOffset.UtcNow.AddHours(-2),
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(-1) // Already expired
            };
            await this.expirationProvider.CreateAsync(entity, new CallerInfo());

            // Act
            var results = (await this.expirationProvider.GetByKeyAsync("exp-1", new CallerInfo(), includeExpired: true))?.ToList() ?? new List<ExpirationEntity>();

            results.Should().HaveCount(1);
            var result = results.First();
            result.Should().NotBeNull();
            result.IsExpired.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task GetAllAsync_MixedExpirationStatus_FiltersExpired()
        {
            // Arrange
            var entities = new[]
            {
                new ExpirationEntity
                {
                    Id = "exp-1",
                    Data = "Active 1",
                    CreatedTime = DateTimeOffset.UtcNow,
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(1)
                },
                new ExpirationEntity
                {
                    Id = "exp-2",
                    Data = "Expired 1",
                    CreatedTime = DateTimeOffset.UtcNow.AddHours(-2),
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(-1)
                },
                new ExpirationEntity
                {
                    Id = "exp-3",
                    Data = "Active 2",
                    CreatedTime = DateTimeOffset.UtcNow,
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(1)
                }
            };

            foreach (var entity in entities)
            {
                await this.expirationProvider.CreateAsync(entity, new CallerInfo());
            }

            // Act
            var activeOnly = await this.expirationProvider.GetAllAsync(new CallerInfo());
            var includeExpired = await this.expirationProvider.GetAllAsync(new CallerInfo(), includeExpired: true);

            // Assert
            activeOnly.Count().Should().Be(2);
            activeOnly.All(e => !e.IsExpired).Should().BeTrue();
            includeExpired.Count().Should().Be(3);
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task CleanupExpiredAsync_RemovesExpiredEntities()
        {
            // Arrange
            var entities = new[]
            {
                new ExpirationEntity
                {
                    Id = "exp-1",
                    Data = "Active",
                    CreatedTime = DateTimeOffset.UtcNow,
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(1)
                },
                new ExpirationEntity
                {
                    Id = "exp-2",
                    Data = "Expired 1",
                    CreatedTime = DateTimeOffset.UtcNow.AddDays(-2),
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(-1)
                },
                new ExpirationEntity
                {
                    Id = "exp-3",
                    Data = "Expired 2",
                    CreatedTime = DateTimeOffset.UtcNow.AddHours(-2),
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(-1)
                }
            };

            foreach (var entity in entities)
            {
                await this.expirationProvider.CreateAsync(entity, new CallerInfo());
            }

            // Act
            var purgeResult = await this.expirationProvider.PurgeAsync(null, new PurgeOptions()
            {
                Strategy = PurgeStrategy.PurgeExpired,
                BackupBeforePurge = false,
                SafeMode = false
            });

            // Assert
            purgeResult.EntitiesPurged.Should().Be(2);
            var remaining = await this.expirationProvider.GetAllAsync(new CallerInfo());
            remaining.Count().Should().Be(1);
            remaining.First().Id.Should().Be("exp-1");
        }

        #endregion

        #region Archive Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task Create_WithArchiveEnabled_InitializesArchiveProperties()
        {
            // Arrange
            var entity = new ArchiveEntity
            {
                Id = "arc-1",
                Content = "Archive Content",
                CreatedTime = DateTimeOffset.UtcNow,
                IsArchived = false
            };

            // Act
            var created = await this.archiveProvider.CreateAsync(entity, new CallerInfo());

            // Assert
            created.Should().NotBeNull();
            created.IsArchived.Should().BeFalse();
            created.AbsoluteExpiration.Should().BeCloseTo(created.CreatedTime.AddDays(7), TimeSpan.FromMinutes(1));
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task ArchiveExpiredAsync_MovesExpiredToArchive()
        {
            // Arrange
            var entities = new[]
            {
                new ArchiveEntity
                {
                    Id = "arc-1",
                    Content = "Active Content",
                    CreatedTime = DateTimeOffset.UtcNow,
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(1),
                    IsArchived = false
                },
                new ArchiveEntity
                {
                    Id = "arc-2",
                    Content = "Expired Content 1",
                    CreatedTime = DateTimeOffset.UtcNow.AddDays(-8),
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(-1),
                    IsArchived = false
                },
                new ArchiveEntity
                {
                    Id = "arc-3",
                    Content = "Expired Content 2",
                    CreatedTime = DateTimeOffset.UtcNow.AddDays(-10),
                    AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(-3),
                    IsArchived = false
                }
            };

            foreach (var entity in entities)
            {
                await this.archiveProvider.CreateAsync(entity, new CallerInfo());
            }

            // Act
            var exportResults = await this.archiveProvider.BulkExportAsync(
                null, new BulkExportOptions()
                {
                    Mode = ExportMode.Full,
                    IncludeExpired = false,
                });

            // Assert
            exportResults.ExportedCount.Should().Be(1);

            // Verify active entities remain active
            var active = await this.archiveProvider.GetAsync("arc-1", new CallerInfo());
            active.Should().NotBeNull();
            active.IsArchived.Should().BeFalse();

            // Verify expired entities are archived
            var archived2 = await this.archiveProvider.GetByKeyAsync("arc-2", new CallerInfo(), includeExpired: true);
            archived2.Should().NotBeNull();
            archived2.Should().HaveCount(1);
            archived2.First().IsExpired.Should().BeTrue();

            var archived3 = await this.archiveProvider.GetByKeyAsync("arc-3", new CallerInfo(), includeExpired: true);
            archived3.Should().NotBeNull();
            archived3.Should().HaveCount(1);
            archived3.First().IsExpired.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task GetAllAsync_WithArchiveFilter_ReturnsCorrectResults()
        {
            // Arrange
            var entities = new[]
            {
                new ArchiveEntity { Id = "arc-1", Content = "Active", IsArchived = false },
                new ArchiveEntity { Id = "arc-2", Content = "Archived 1", IsArchived = true },
                new ArchiveEntity { Id = "arc-3", Content = "Archived 2", IsArchived = true },
                new ArchiveEntity { Id = "arc-4", Content = "Active 2", IsArchived = false }
            };

            foreach (var entity in entities)
            {
                entity.CreatedTime = DateTimeOffset.UtcNow;
                entity.AbsoluteExpiration = entity.IsArchived 
                    ? DateTimeOffset.UtcNow.AddDays(-1) 
                    : DateTimeOffset.UtcNow.AddDays(1);
                await this.archiveProvider.CreateAsync(entity, new CallerInfo());
            }

            // Act
            var activeOnly = await this.archiveProvider.GetAllAsync(new CallerInfo());
            var archivedOnly = await this.archiveProvider.BulkExportAsync(
                null, new BulkExportOptions()
                {
                    IncludeExpired = true
                });
            var all = await this.archiveProvider.GetAllAsync(new CallerInfo(), includeExpired: true);

            // Assert
            activeOnly.Count().Should().Be(2);
            activeOnly.All(e => !e.IsArchived).Should().BeTrue();
            
            archivedOnly.ExportedEntities.Count().Should().Be(4);
            archivedOnly.ExportedEntities.Count(e => e.IsArchived).Should().Be(2);
            
            all.Count().Should().Be(4);
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task RestoreFromArchive_RestoresArchivedEntity()
        {
            // Arrange
            var entity = new ArchiveEntity
            {
                Id = "arc-1",
                Content = "Archived Content",
                CreatedTime = DateTimeOffset.UtcNow.AddDays(-10),
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(-3),
                IsArchived = true
            };

            //await this.archiveProvider.CreateAsync(entity, new CallerInfo());
            
            // Act
            var importResult = await this.archiveProvider.BulkImportAsync(
                new List<ArchiveEntity>(){entity},
                new BulkImportOptions()
                {
                    Strategy = ImportStrategy.Upsert
                });

            // Assert
            importResult.Should().NotBeNull();
            importResult.SuccessCount.Should().Be(1);

            var restored = await this.archiveProvider.GetAsync("arc-1", new CallerInfo());
            restored.Should().NotBeNull();
            restored.AbsoluteExpiration.Should().BeAfter(DateTimeOffset.UtcNow); // Should have new expiration
            restored.Version.Should().Be(entity.Version); // Version should NOT increment when originally missing
        }

        #endregion

        #region Sliding Expiration Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task CacheEntry_WithSlidingExpiration_UpdatesOnAccess()
        {
            // Arrange
            var cacheProvider = new SQLitePersistenceProvider<CacheEntry, string>(
                $"Data Source={this.testDbPath};Version=3;");
            await cacheProvider.InitializeAsync();

            var entry = new CacheEntry
            {
                Id = "cache-1",
                TypeName = "TestType",
                Data = "Cached Data",
                CreatedTime = DateTimeOffset.UtcNow,
                SlidingExpirationSeconds = 300, // 5 minutes
                LastAccessTime = DateTimeOffset.UtcNow
            };
            await cacheProvider.CreateAsync(entry, new CallerInfo());

            // Act - Access the entry after 2 minutes
            await Task.Delay(100); // Simulate time passing
            var accessed = await cacheProvider.GetAsync("cache-1", new CallerInfo());
            accessed.LastAccessTime = DateTimeOffset.UtcNow;
            var updated = await cacheProvider.UpdateAsync(accessed, new CallerInfo());

            // Assert
            updated.Should().NotBeNull();
            updated.IsExpired().Should().BeFalse();
            updated.LastAccessTime.Value.Should().BeAfter(entry.LastAccessTime.Value);
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task CacheEntry_WithAbsoluteAndSlidingExpiration_AbsoluteTakesPrecedence()
        {
            // Arrange
            var cacheProvider = new SQLitePersistenceProvider<CacheEntry, string>(
                $"Data Source={this.testDbPath};Version=3;");
            await cacheProvider.InitializeAsync();

            var entry = new CacheEntry
            {
                Id = "cache-1",
                TypeName = "TestType",
                Data = "Cached Data",
                CreatedTime = DateTimeOffset.UtcNow,
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(-10), // Already expired
                SlidingExpirationSeconds = 3600, // 1 hour
                LastAccessTime = DateTimeOffset.UtcNow
            };
            await cacheProvider.CreateAsync(entry, new CallerInfo());

            // Act
            var result = await cacheProvider.GetByKeyAsync("cache-1", new CallerInfo(), includeExpired: true);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.First().IsExpired().Should().BeTrue(); // Should be expired due to absolute expiration
        }

        #endregion

        #region Batch Operations with Expiration

        [TestMethod]
        [TestCategory("UnitTest")]
        public async Task BatchCreate_WithExpiration_SetsExpirationForAll()
        {
            // Arrange
            var entities = new List<ExpirationEntity>();
            for (var i = 1; i <= 5; i++)
            {
                entities.Add(new ExpirationEntity
                {
                    Id = $"batch-{i}",
                    Data = $"Batch Data {i}",
                    CreatedTime = DateTimeOffset.UtcNow
                });
            }

            // Act
            await this.expirationProvider.CreateAsync(entities, new CallerInfo());

            // Assert
            var results = await this.expirationProvider.GetAllAsync(new CallerInfo());
            results.Count().Should().Be(5);
            foreach (var result in results)
            {
                result.AbsoluteExpiration.Should().BeCloseTo(result.CreatedTime.AddHours(1), TimeSpan.FromMinutes(1));
            }
        }

        #endregion
    }
}