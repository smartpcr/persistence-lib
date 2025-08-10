// -----------------------------------------------------------------------
// <copyright file="QueryOperationsTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.QueryOperations
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.QueryOperations;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Providers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using QueryTestEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.QueryOperations.QueryTestEntity;

    [TestClass]
    public class QueryOperationsTests : SQLiteTestBase
    {
        private string testDbPath;
        private string connectionString;
        private SQLitePersistenceProvider<QueryTestEntity, Guid> provider;
        private SQLitePersistenceProvider<QueryTestSoftDeleteEntity, Guid> softDeleteProvider;
        private CallerInfo callerInfo;

        [TestInitialize]
        public async Task Setup()
        {
            this.testDbPath = Path.Combine(Directory.GetCurrentDirectory(), $"test_{Guid.NewGuid()}.db");
            this.connectionString = $"Data Source={this.testDbPath};Version=3;";
            this.provider = new SQLitePersistenceProvider<QueryTestEntity, Guid>(this.connectionString);
            await this.provider.InitializeAsync();
            this.softDeleteProvider = new SQLitePersistenceProvider<QueryTestSoftDeleteEntity, Guid>(this.connectionString);
            await this.softDeleteProvider.InitializeAsync();

            this.callerInfo = new CallerInfo
            {
                CorrelationId = Guid.NewGuid().ToString()
            };

            // Seed test data
            await this.SeedTestData();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (this.provider != null)
            {
                await this.provider.DisposeAsync();
            }

            if (this.softDeleteProvider != null)
            {
                await this.softDeleteProvider.DisposeAsync();
            }

            this.SafeDeleteDatabase(this.testDbPath);
        }

        private async Task SeedTestData()
        {
            var entities = new[]
            {
                new QueryTestEntity { Id = Guid.NewGuid(), Name = "Alpha", Status = "Active", Amount = 100, Category = "A", DateCreated = DateTime.UtcNow.AddDays(-10) },
                new QueryTestEntity { Id = Guid.NewGuid(), Name = "Beta", Status = "Active", Amount = 200, Category = "B", DateCreated = DateTime.UtcNow.AddDays(-8) },
                new QueryTestEntity { Id = Guid.NewGuid(), Name = "Gamma", Status = "Inactive", Amount = 150, Category = "A", DateCreated = DateTime.UtcNow.AddDays(-6) },
                new QueryTestEntity { Id = Guid.NewGuid(), Name = "Delta", Status = "Active", Amount = 50, Category = "C", DateCreated = DateTime.UtcNow.AddDays(-4) },
                new QueryTestEntity { Id = Guid.NewGuid(), Name = "Epsilon", Status = "Pending", Amount = 300, Category = "B", DateCreated = DateTime.UtcNow.AddDays(-2) },
                new QueryTestEntity { Id = Guid.NewGuid(), Name = "TestAlpha", Status = "Active", Amount = 175, Category = "A", DateCreated = DateTime.UtcNow.AddDays(-1) },
                new QueryTestEntity { Id = Guid.NewGuid(), Name = "TestBeta", Status = "Inactive", Amount = 225, Category = "C", DateCreated = DateTime.UtcNow }
            };

            foreach (var entity in entities)
            {
                await this.provider.CreateAsync(entity, this.callerInfo);
            }

            // Seed soft delete entities
            var softDeleteEntities = new[]
            {
                new QueryTestSoftDeleteEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Alpha",
                    Status = "Active",
                    Amount = 100,
                    Category = "A",
                    DateCreated = DateTime.UtcNow.AddDays(-10)
                },
                new QueryTestSoftDeleteEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Beta",
                    Status = "Active",
                    Amount = 200,
                    Category = "B",
                    DateCreated = DateTime.UtcNow.AddDays(-8)
                },
                new QueryTestSoftDeleteEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Gamma",
                    Status = "Inactive",
                    Amount = 150,
                    Category = "A",
                    DateCreated = DateTime.UtcNow.AddDays(-6)
                },
                new QueryTestSoftDeleteEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Delta",
                    Status = "Active",
                    Amount = 50,
                    Category = "C",
                    DateCreated = DateTime.UtcNow.AddDays(-4)
                },
                new QueryTestSoftDeleteEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Epsilon",
                    Status = "Pending",
                    Amount = 300,
                    Category = "B",
                    DateCreated = DateTime.UtcNow.AddDays(-2)
                },
                new QueryTestSoftDeleteEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "TestAlpha",
                    Status = "Active",
                    Amount = 175,
                    Category = "A",
                    DateCreated = DateTime.UtcNow.AddDays(-1)
                },
                new QueryTestSoftDeleteEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "TestBeta",
                    Status = "Inactive",
                    Amount = 225,
                    Category = "C",
                    DateCreated = DateTime.UtcNow
                }
            };

            foreach (var entity in softDeleteEntities)
            {
                await this.softDeleteProvider.CreateAsync(entity, this.callerInfo);
            }
        }

        [TestMethod]
        [TestCategory("QueryOperations")]
        public async Task QueryAsync_SimplePredicate_FiltersCorrectly()
        {
            // Act
            var results = await this.provider.QueryAsync(
                e => e.Status == "Active",
                null,
                this.callerInfo);

            // Assert
            results.Should().NotBeNull();
            results.Count().Should().Be(4);
            results.Should().OnlyContain(e => e.Status == "Active");
        }

        [TestMethod]
        [TestCategory("QueryOperations")]
        public async Task QueryAsync_CompoundPredicate_AppliesAllConditions()
        {
            // Act
            var results = (await this.provider.QueryAsync(
                e => e.Status == "Active" && e.Amount > 100,
                null,
                this.callerInfo))?.ToList();

            // Assert
            results.Should().NotBeNull();
            results!.Count.Should().Be(2); // Beta (200) and TestAlpha (175)
            results.Should().OnlyContain(e => e.Status == "Active" && e.Amount > 100);
        }

        [TestMethod]
        [TestCategory("QueryOperations")]
        public async Task QueryAsync_StringOperations_TranslatesCorrectly()
        {
            // Act - StartsWith
            var startsWithResults = await this.provider.QueryAsync(
                e => e.Name.StartsWith("Test"),
                null,
                this.callerInfo);

            // Assert
            startsWithResults.Count().Should().Be(2);
            startsWithResults.Should().OnlyContain(e => e.Name.StartsWith("Test"));

            // Act - Contains
            var containsResults = await this.provider.QueryAsync(
                e => e.Name.Contains("et"),
                null,
                this.callerInfo);

            // Assert
            containsResults.Count().Should().Be(2); // Beta and TestBeta

            // Act - EndsWith
            var endsWithResults = await this.provider.QueryAsync(
                e => e.Name.EndsWith("a"),
                null,
                this.callerInfo);

            // Assert
            endsWithResults.Count().Should().BeGreaterThanOrEqualTo(2); // Alpha, Beta, Gamma, Delta, TestAlpha, TestBeta
        }

        [TestMethod]
        [TestCategory("QueryOperations")]
        public async Task QueryAsync_OrderBy_SortsResults()
        {
            // Act - Order by Amount ascending
            var ascResults = await this.provider.QueryAsync(
                null,
                q => q.OrderBy(e => e.Amount),
                this.callerInfo);

            // Assert
            ascResults.Should().NotBeNull();
            ascResults.Count().Should().Be(7);
            ascResults.First().Amount.Should().Be(50);
            ascResults.Last().Amount.Should().Be(300);

            // Act - Order by Name descending
            var descResults = await this.provider.QueryAsync(
                null,
                q => q.OrderByDescending(e => e.Name),
                this.callerInfo);

            // Assert
            descResults.First().Name.CompareTo(descResults.Last().Name).Should().BeGreaterThan(0);
        }

        [TestMethod]
        [TestCategory("QueryOperations")]
        public async Task QueryAsync_SkipTake_ImplementsPaging()
        {
            // Act - Skip 2, Take 3
            var pagedResults = await this.provider.QueryAsync(
                null,
                q => q.OrderBy(e => e.Name),
                this.callerInfo,
                skip: 2,
                take: 3);

            // Assert
            pagedResults.Should().NotBeNull();
            pagedResults.Count().Should().Be(3);

            // Verify we skipped the first 2 when ordered by name
            var allOrdered = await this.provider.QueryAsync(
                null,
                q => q.OrderBy(e => e.Name),
                this.callerInfo);

            var allOrderedArray = allOrdered.ToArray();
            var pagedResultsArray = pagedResults.ToArray();
            pagedResultsArray[0].Id.Should().Be(allOrderedArray[2].Id);
            pagedResultsArray[1].Id.Should().Be(allOrderedArray[3].Id);
            pagedResultsArray[2].Id.Should().Be(allOrderedArray[4].Id);
        }

        [TestMethod]
        [TestCategory("QueryOperations")]
        public async Task QueryPagedAsync_ReturnsPagedResult()
        {
            // Act, 3 items with category "A", page size 2, page number 1
            var pagedResult = await this.provider.QueryPagedAsync(
                e => e.Category == "A",
                pageSize: 2,
                pageNumber: 1);

            // Assert
            pagedResult.Should().NotBeNull();
            pagedResult.Items.Count().Should().Be(2);
            pagedResult.PageNumber.Should().Be(1);
            pagedResult.PageSize.Should().Be(2);
            pagedResult.TotalCount.Should().Be(3); // Alpha, Gamma, TestAlpha
            pagedResult.PageNumber.Should().BeLessThan(pagedResult.TotalPages); // HasNextPage
            pagedResult.PageNumber.Should().BeGreaterOrEqualTo(1);
        }

        [TestMethod]
        [TestCategory("QueryOperations")]
        public async Task QueryPagedAsync_CalculatesTotalPages()
        {
            // Act
            var pagedResult = await this.provider.QueryPagedAsync(
                null,
                3,
                2);

            // Assert
            pagedResult.Should().NotBeNull();
            pagedResult.TotalPages.Should().Be(3); // 7 items / 3 per page = 3 pages
            pagedResult.PageNumber.Should().Be(2);
            pagedResult.PageNumber.Should().BeGreaterThan(1); // HasPreviousPage
            pagedResult.PageNumber.Should().BeLessThan(pagedResult.TotalPages); // HasNextPage
        }

        [TestMethod]
        [TestCategory("QueryOperations")]
        public async Task CountAsync_WithPredicate_ReturnsCorrectCount()
        {
            // Act
            var activeCount = await this.provider.CountAsync(
                e => e.Status == "Active");

            // Assert
            activeCount.Should().Be(4);

            // Act
            var highAmountCount = await this.provider.CountAsync(
                e => e.Amount >= 200);

            // Assert
            highAmountCount.Should().Be(3); // Beta (200), Epsilon (300), TestBeta (225)
        }

        [TestMethod]
        [TestCategory("QueryOperations")]
        public async Task CountAsync_WithoutPredicate_ReturnsTotal()
        {
            // Act
            var totalCount = await this.provider.CountAsync();

            // Assert
            totalCount.Should().Be(7);
        }

        [TestMethod]
        [TestCategory("QueryOperations")]
        public async Task ExistsAsync_ExistingEntity_ReturnsTrue()
        {
            // Act
            var exists = await this.provider.ExistsAsync(
                e => e.Name == "Alpha" && e.Status == "Active");

            // Assert
            exists.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("QueryOperations")]
        public async Task ExistsAsync_NonExistentEntity_ReturnsFalse()
        {
            // Act
            var exists = await this.provider.ExistsAsync(
                e => e.Name == "NonExistent" || e.Amount > 1000);

            // Assert
            exists.Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("QueryOperations")]
        public async Task ExistsAsync_ExistingSoftDeleteEntity_ReturnsTrue()
        {
            // Act
            var exists = await this.softDeleteProvider.ExistsAsync(
                e => e.Name == "Alpha" && e.Status == "Active");

            // Assert
            exists.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("QueryOperations")]
        public async Task ExistsAsync_NonExistentSoftDeleteEntity_ReturnsFalse()
        {
            // Act
            var exists = await this.softDeleteProvider.ExistsAsync(
                e => e.Name == "NonExistent" || e.Amount > 1000);

            // Assert
            exists.Should().BeFalse();
        }
    }
}