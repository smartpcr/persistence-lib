// -----------------------------------------------------------------------
// <copyright file="BaseEntityMapperSoftDeletePredicateTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Mappings
{
    using System;
    using System.Linq.Expressions;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BaseEntityMapperSoftDeletePredicateTests
    {
        private BaseEntityMapper<SoftDeleteEntity, string> mapper;

        [TestInitialize]
        public void Setup()
        {
            this.mapper = new BaseEntityMapper<SoftDeleteEntity, string>();
        }

        [TestMethod]
        [TestCategory("Mappings")]
        public void GenerateSelectSql_WithSoftDeleteAndSimplePredicate_GeneratesCorrectSql()
        {
            // Arrange
            Expression<Func<SoftDeleteEntity, bool>> predicate = e => e.Description == "Test";

            // Act
            var (sql, parameters) = this.mapper.GenerateSelectSql(predicate);

            // Assert
            sql.Should().NotBeNullOrEmpty();
            sql.Should().Contain("WHERE");
            sql.Should().Contain("t.Description");
            sql.Should().NotContain("t.(Description", "The WHERE clause should not have double table alias prefixing");
            sql.Should().NotContain("t.((", "The WHERE clause should not have double prefixing with parentheses");
            sql.Should().Contain("t.IsDeleted = 0", "Should include soft delete filter");
            sql.Should().Contain("INNER JOIN", "Should use JOIN for latest version");
            parameters.Should().ContainKey("@p0");
            parameters["@p0"].Should().Be("Test");
        }

        [TestMethod]
        [TestCategory("Mappings")]
        public void GenerateSelectSql_WithSoftDeleteAndCompoundPredicate_GeneratesCorrectSql()
        {
            // Arrange
            Expression<Func<SoftDeleteEntity, bool>> predicate = 
                e => e.Description == "Test" && e.Id == "123";

            // Act
            var (sql, parameters) = this.mapper.GenerateSelectSql(predicate);

            // Assert
            sql.Should().NotBeNullOrEmpty();
            sql.Should().Contain("WHERE");
            // When querying by ID (single entity), it doesn't use table aliases
            sql.Should().Contain("Description");
            sql.Should().Contain("CacheKey"); // Note: The entity uses CacheKey as Id
            sql.Should().NotContain("t.((Description", "The WHERE clause should not have double table alias prefixing");
            sql.Should().NotContain("t.(Description", "The WHERE clause should not have double table alias prefixing for Description");
            sql.Should().NotContain("t.(CacheKey", "The WHERE clause should not have double table alias prefixing for Id");
            sql.Should().Contain("AND");
            sql.Should().Contain("IsDeleted = 0");
            parameters.Should().ContainKey("@p0");
            parameters.Should().ContainKey("@p1");
        }

        [TestMethod]
        [TestCategory("Mappings")]
        public void GenerateSelectSql_WithSoftDeleteAndOrPredicate_GeneratesCorrectSql()
        {
            // Arrange
            Expression<Func<SoftDeleteEntity, bool>> predicate = 
                e => e.Description == "Test1" || e.Description == "Test2";

            // Act
            var (sql, parameters) = this.mapper.GenerateSelectSql(predicate);

            // Assert
            sql.Should().NotBeNullOrEmpty();
            sql.Should().Contain("WHERE");
            
            // The predicate should be properly wrapped with table aliases
            sql.Should().Match("*t.Description*");
            sql.Should().NotContain("t.((", "Should not have double parentheses with table alias");
            sql.Should().NotContain("t.(Description", "Should not have table alias directly before parentheses");
            
            // Should contain OR for the predicate
            sql.Should().Contain("OR");
            
            // Should still have soft delete filter
            sql.Should().Contain("t.IsDeleted = 0");
            
            parameters.Should().ContainKey("@p0");
            parameters.Should().ContainKey("@p1");
            parameters["@p0"].Should().Be("Test1");
            parameters["@p1"].Should().Be("Test2");
        }

        [TestMethod]
        [TestCategory("Mappings")]
        public void GenerateSelectSql_WithSoftDeleteAndComplexPredicate_GeneratesCorrectSql()
        {
            // Arrange
            Expression<Func<SoftDeleteEntity, bool>> predicate = 
                e => (e.Description == "Test" && e.Id == "123") || (e.Description == "Other" && e.Id == "456");

            // Act
            var (sql, parameters) = this.mapper.GenerateSelectSql(predicate);

            // Assert
            sql.Should().NotBeNullOrEmpty();
            sql.Should().Contain("WHERE");
            
            // When querying with ID (single entity query), no table aliases are used
            sql.Should().Contain("Description");
            sql.Should().Contain("CacheKey"); // The entity maps Id to CacheKey column
            
            // Should not have malformed double prefixing (table alias directly before parentheses)
            sql.Should().NotContain("t.((");
            sql.Should().NotContain("t.(Description");
            sql.Should().NotContain("t.(CacheKey");
            // Note: (( is acceptable for nested logical grouping in complex predicates
            
            // Should contain the logical operators
            sql.Should().Contain("AND");
            sql.Should().Contain("OR");
            
            // Should still include soft delete filter
            sql.Should().Contain("IsDeleted = 0");
            
            // Should have all parameters
            parameters.Should().HaveCount(4);
            parameters.Should().ContainKey("@p0");
            parameters.Should().ContainKey("@p1");
            parameters.Should().ContainKey("@p2");
            parameters.Should().ContainKey("@p3");
        }

        [TestMethod]
        [TestCategory("Mappings")]
        public void GenerateSelectSql_WithSoftDeleteAndSelectOptions_CombinesCorrectly()
        {
            // Arrange
            Expression<Func<SoftDeleteEntity, bool>> predicate = e => e.Description == "Test";
            var options = new SelectOptions
            {
                Limit = 10,
                Offset = 5,
                OrderBy = "Description DESC"
            };

            // Act
            var (sql, parameters) = this.mapper.GenerateSelectSql(predicate, options);

            // Assert
            sql.Should().NotBeNullOrEmpty();
            sql.Should().Contain("WHERE");
            sql.Should().Contain("t.Description");
            sql.Should().NotContain("t.((");
            sql.Should().NotContain("t.(Description");
            sql.Should().Contain("t.IsDeleted = 0");
            sql.Should().Contain("LIMIT 10");
            sql.Should().Contain("OFFSET 5");
            sql.Should().Contain("ORDER BY Description DESC");
            parameters.Should().ContainKey("@p0");
            parameters["@p0"].Should().Be("Test");
        }
    }
}