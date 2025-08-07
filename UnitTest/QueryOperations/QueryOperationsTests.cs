// -----------------------------------------------------------------------
// <copyright file="QueryOperationsTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.QueryOperations
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using QueryTestEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.QueryOperations.QueryTestEntity;

    [TestClass]
    public class QueryOperationsTests
    {
        private string connectionString;
        private SQLitePersistenceProvider<QueryTestEntity, Guid> provider;
        private CallerInfo callerInfo;

        [TestInitialize]
        public async Task Setup()
        {
            this.connectionString = "Data Source=:memory:";
            this.provider = new SQLitePersistenceProvider<QueryTestEntity, Guid>(this.connectionString);
            await this.provider.InitializeAsync();
            
            this.callerInfo = new CallerInfo
            {
                UserId = "TestUser",
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
            Assert.IsNotNull(results);
            Assert.AreEqual(4, results.Count());
            Assert.IsTrue(results.All(e => e.Status == "Active"));
        }

        [TestMethod]
        [TestCategory("QueryOperations")]
        public async Task QueryAsync_CompoundPredicate_AppliesAllConditions()
        {
            // Act
            var results = await this.provider.QueryAsync(
                e => e.Status == "Active" && e.Amount > 100,
                null,
                this.callerInfo);

            // Assert
            Assert.IsNotNull(results);
            Assert.AreEqual(2, results.Count()); // Beta (200) and TestAlpha (175)
            Assert.IsTrue(results.All(e => e.Status == "Active" && e.Amount > 100));
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
            Assert.AreEqual(2, startsWithResults.Count());
            Assert.IsTrue(startsWithResults.All(e => e.Name.StartsWith("Test")));
            
            // Act - Contains
            var containsResults = await this.provider.QueryAsync(
                e => e.Name.Contains("et"),
                null,
                this.callerInfo);
            
            // Assert
            Assert.AreEqual(2, containsResults.Count()); // Beta and TestBeta
            
            // Act - EndsWith
            var endsWithResults = await this.provider.QueryAsync(
                e => e.Name.EndsWith("a"),
                null,
                this.callerInfo);
            
            // Assert
            Assert.IsTrue(endsWithResults.Count() >= 2); // Alpha, Beta, Gamma, Delta, TestAlpha, TestBeta
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
            Assert.IsNotNull(ascResults);
            Assert.AreEqual(7, ascResults.Count());
            Assert.AreEqual(50, ascResults.First().Amount);
            Assert.AreEqual(300, ascResults.Last().Amount);
            
            // Act - Order by Name descending
            var descResults = await this.provider.QueryAsync(
                null,
                q => q.OrderByDescending(e => e.Name),
                this.callerInfo);
            
            // Assert
            Assert.IsTrue(descResults.First().Name.CompareTo(descResults.Last().Name) > 0);
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
            Assert.IsNotNull(pagedResults);
            Assert.AreEqual(3, pagedResults.Count());
            
            // Verify we skipped the first 2 when ordered by name
            var allOrdered = await this.provider.QueryAsync(
                null,
                q => q.OrderBy(e => e.Name),
                this.callerInfo);
            
            var allOrderedArray = allOrdered.ToArray();
            var pagedResultsArray = pagedResults.ToArray();
            Assert.AreEqual(allOrderedArray[2].Id, pagedResultsArray[0].Id);
            Assert.AreEqual(allOrderedArray[3].Id, pagedResultsArray[1].Id);
            Assert.AreEqual(allOrderedArray[4].Id, pagedResultsArray[2].Id);
        }

        [TestMethod]
        [TestCategory("QueryOperations")]
        public async Task QueryPagedAsync_ReturnsPagedResult()
        {
            // Act
            var pagedResult = await this.provider.QueryPagedAsync(
                e => e.Category == "A",
                2,
                1);

            // Assert
            Assert.IsNotNull(pagedResult);
            Assert.AreEqual(2, pagedResult.Items.Count());
            Assert.AreEqual(1, pagedResult.PageNumber);
            Assert.AreEqual(2, pagedResult.PageSize);
            Assert.AreEqual(3, pagedResult.TotalCount); // Alpha, Gamma, TestAlpha
            Assert.IsTrue(pagedResult.PageNumber < pagedResult.TotalPages); // HasNextPage
            Assert.IsFalse(pagedResult.PageNumber > 1); // HasPreviousPage
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
            Assert.IsNotNull(pagedResult);
            Assert.AreEqual(3, pagedResult.TotalPages); // 7 items / 3 per page = 3 pages
            Assert.AreEqual(2, pagedResult.PageNumber);
            Assert.IsTrue(pagedResult.PageNumber > 1); // HasPreviousPage
            Assert.IsTrue(pagedResult.PageNumber < pagedResult.TotalPages); // HasNextPage
        }

        [TestMethod]
        [TestCategory("QueryOperations")]
        public async Task CountAsync_WithPredicate_ReturnsCorrectCount()
        {
            // Act
            var activeCount = await this.provider.CountAsync(
                e => e.Status == "Active",
                callerInfo: this.callerInfo);

            // Assert
            Assert.AreEqual(4, activeCount);
            
            // Act
            var highAmountCount = await this.provider.CountAsync(
                e => e.Amount >= 200,
                callerInfo: this.callerInfo);
            
            // Assert
            Assert.AreEqual(3, highAmountCount); // Beta (200), Epsilon (300), TestBeta (225)
        }

        [TestMethod]
        [TestCategory("QueryOperations")]
        public async Task CountAsync_WithoutPredicate_ReturnsTotal()
        {
            // Act
            var totalCount = await this.provider.CountAsync();

            // Assert
            Assert.AreEqual(7, totalCount);
        }

        [TestMethod]
        [TestCategory("QueryOperations")]
        public async Task ExistsAsync_ExistingEntity_ReturnsTrue()
        {
            // Act
            var exists = await this.provider.ExistsAsync(
                e => e.Name == "Alpha" && e.Status == "Active",
                callerInfo: this.callerInfo);

            // Assert
            Assert.IsTrue(exists);
        }

        [TestMethod]
        [TestCategory("QueryOperations")]
        public async Task ExistsAsync_NonExistentEntity_ReturnsFalse()
        {
            // Act
            var exists = await this.provider.ExistsAsync(
                e => e.Name == "NonExistent" || e.Amount > 1000,
                callerInfo: this.callerInfo);

            // Assert
            Assert.IsFalse(exists);
        }
    }
}