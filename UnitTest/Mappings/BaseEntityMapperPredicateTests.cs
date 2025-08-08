// -----------------------------------------------------------------------
// <copyright file="BaseEntityMapperPredicateTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Mappings
{
    using System;
    using System.Linq.Expressions;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.CorePersistence;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BaseEntityMapperPredicateTests
    {
        private BaseEntityMapper<CrudTestEntity, Guid> mapper;

        [TestInitialize]
        public void Setup()
        {
            this.mapper = new BaseEntityMapper<CrudTestEntity, Guid>();
        }

        [TestMethod]
        [TestCategory("Mappings")]
        public void GenerateSelectSql_WithSimplePredicate_GeneratesCorrectWhereClause()
        {
            // Arrange
            Expression<Func<CrudTestEntity, bool>> predicate = e => e.Name == "Test";

            // Act
            var (sql, parameters) = this.mapper.GenerateSelectSql(predicate);

            // Assert
            sql.Should().NotBeNullOrEmpty();
            sql.Should().Contain("WHERE");
            sql.Should().Contain("Name");
            parameters.Should().ContainKey("@p0");
            parameters["@p0"].Should().Be("Test");
        }

        [TestMethod]
        [TestCategory("Mappings")]
        public void GenerateSelectSql_WithCompoundPredicate_GeneratesCorrectWhereClause()
        {
            // Arrange
            Expression<Func<CrudTestEntity, bool>> predicate = 
                e => e.Name == "Test" && e.Status == "Active";

            // Act
            var (sql, parameters) = this.mapper.GenerateSelectSql(predicate);

            // Assert
            sql.Should().NotBeNullOrEmpty();
            sql.Should().Contain("WHERE");
            sql.Should().Contain("Name");
            sql.Should().Contain("Status");
            sql.Should().Contain("AND");
            parameters.Should().ContainKey("@p0");
            parameters.Should().ContainKey("@p1");
            parameters["@p0"].Should().Be("Test");
            parameters["@p1"].Should().Be("Active");
        }

        [TestMethod]
        [TestCategory("Mappings")]
        public void GenerateSelectSql_WithOrPredicate_GeneratesCorrectWhereClause()
        {
            // Arrange
            Expression<Func<CrudTestEntity, bool>> predicate = 
                e => e.Name == "Test1" || e.Name == "Test2";

            // Act
            var (sql, parameters) = this.mapper.GenerateSelectSql(predicate);

            // Assert
            sql.Should().NotBeNullOrEmpty();
            sql.Should().Contain("WHERE");
            sql.Should().Contain("Name");
            sql.Should().Contain("OR");
            parameters.Should().ContainKey("@p0");
            parameters.Should().ContainKey("@p1");
            parameters["@p0"].Should().Be("Test1");
            parameters["@p1"].Should().Be("Test2");
        }

        [TestMethod]
        [TestCategory("Mappings")]
        public void GenerateSelectSql_WithNullPredicate_GeneratesSimpleSelect()
        {
            // Act
            var (sql, parameters) = this.mapper.GenerateSelectSql(null);

            // Assert
            sql.Should().NotBeNullOrEmpty();
            sql.Should().StartWith("SELECT");
            sql.Should().Contain("FROM");
            sql.Should().NotContain("WHERE");
            parameters.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory("Mappings")]
        public void GenerateSelectSql_WithPredicateAndOptions_AppliesBoth()
        {
            // Arrange
            Expression<Func<CrudTestEntity, bool>> predicate = e => e.Name == "Test";
            var options = new SelectOptions
            {
                OrderBy = "CreatedTime DESC",
                Limit = 10,
                Offset = 5
            };

            // Act
            var (sql, parameters) = this.mapper.GenerateSelectSql(predicate, options);

            // Assert
            sql.Should().NotBeNullOrEmpty();
            sql.Should().Contain("WHERE");
            sql.Should().Contain("Name");
            sql.Should().Contain("ORDER BY");
            sql.Should().Contain("CreatedTime DESC");
            sql.Should().Contain("LIMIT 10");
            sql.Should().Contain("OFFSET 5");
            parameters.Should().ContainKey("@p0");
            parameters["@p0"].Should().Be("Test");
        }

        [TestMethod]
        [TestCategory("Mappings")]
        public void GenerateSelectSql_WithDateTimePredicate_HandlesCorrectly()
        {
            // Arrange
            var testDate = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            Expression<Func<CrudTestEntity, bool>> predicate = 
                e => e.CreatedTime > testDate;

            // Act
            var (sql, parameters) = this.mapper.GenerateSelectSql(predicate);

            // Assert
            sql.Should().NotBeNullOrEmpty();
            sql.Should().Contain("WHERE");
            sql.Should().Contain("CreatedTime");
            sql.Should().Contain(">");
            parameters.Should().ContainKey("@p0");
            parameters["@p0"].Should().Be(testDate);
        }

        [TestMethod]
        [TestCategory("Mappings")]
        public void GenerateSelectSql_WithContainsPredicate_GeneratesLikeClause()
        {
            // Arrange
            Expression<Func<CrudTestEntity, bool>> predicate = 
                e => e.Name.Contains("Test");

            // Act
            var (sql, parameters) = this.mapper.GenerateSelectSql(predicate);

            // Assert
            sql.Should().NotBeNullOrEmpty();
            sql.Should().Contain("WHERE");
            sql.Should().Contain("Name");
            sql.Should().Contain("LIKE");
            parameters.Should().ContainKey("@p0");
            parameters["@p0"].Should().Be("%Test%");
        }

        [TestMethod]
        [TestCategory("Mappings")]
        public void GenerateSelectSql_WithStartsWithPredicate_GeneratesLikeClause()
        {
            // Arrange
            Expression<Func<CrudTestEntity, bool>> predicate = 
                e => e.Name.StartsWith("Test");

            // Act
            var (sql, parameters) = this.mapper.GenerateSelectSql(predicate);

            // Assert
            sql.Should().NotBeNullOrEmpty();
            sql.Should().Contain("WHERE");
            sql.Should().Contain("Name");
            sql.Should().Contain("LIKE");
            parameters.Should().ContainKey("@p0");
            parameters["@p0"].Should().Be("Test%");
        }

        [TestMethod]
        [TestCategory("Mappings")]
        public void GenerateSelectSql_WithEndsWithPredicate_GeneratesLikeClause()
        {
            // Arrange
            Expression<Func<CrudTestEntity, bool>> predicate = 
                e => e.Name.EndsWith("Test");

            // Act
            var (sql, parameters) = this.mapper.GenerateSelectSql(predicate);

            // Assert
            sql.Should().NotBeNullOrEmpty();
            sql.Should().Contain("WHERE");
            sql.Should().Contain("Name");
            sql.Should().Contain("LIKE");
            parameters.Should().ContainKey("@p0");
            parameters["@p0"].Should().Be("%Test");
        }

        [TestMethod]
        [TestCategory("Mappings")]
        public void GenerateSelectSql_WithComplexNestedPredicate_HandlesCorrectly()
        {
            // Arrange
            Expression<Func<CrudTestEntity, bool>> predicate = 
                e => (e.Name == "Test1" || e.Name == "Test2") && e.Status == "Active";

            // Act
            var (sql, parameters) = this.mapper.GenerateSelectSql(predicate);

            // Assert
            sql.Should().NotBeNullOrEmpty();
            sql.Should().Contain("WHERE");
            sql.Should().Contain("Name");
            sql.Should().Contain("Status");
            sql.Should().Contain("AND");
            sql.Should().Contain("OR");
            parameters.Should().HaveCount(3);
            parameters["@p0"].Should().Be("Test1");
            parameters["@p1"].Should().Be("Test2");
            parameters["@p2"].Should().Be("Active");
        }
    }
}