//-------------------------------------------------------------------------------
// <copyright file="BaseEntityMapperOrderByTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Mappings
{
    using System;
    using System.Linq;
    using Contracts.Mappings;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BaseEntityMapperOrderByTests
    {
        private BaseEntityMapper<TestEntity, string> mapper;

        [TestInitialize]
        public void TestInitialize()
        {
            this.mapper = new BaseEntityMapper<TestEntity, string>();
        }

        [TestMethod]
        public void GenerateOrderBySql_WithSingleOrderBy_ShouldGenerateCorrectSql()
        {
            // Arrange
            Func<IQueryable<TestEntity>, IOrderedQueryable<TestEntity>> orderBy = 
                q => q.OrderBy(e => e.Name);

            // Act
            var sql = this.mapper.GenerateOrderBySql(orderBy);

            // Assert
            sql.Should().Be(" ORDER BY Name ASC");
        }

        [TestMethod]
        public void GenerateOrderBySql_WithOrderByDescending_ShouldGenerateCorrectSql()
        {
            // Arrange
            Func<IQueryable<TestEntity>, IOrderedQueryable<TestEntity>> orderBy = 
                q => q.OrderByDescending(e => e.Value);

            // Act
            var sql = this.mapper.GenerateOrderBySql(orderBy);

            // Assert
            sql.Should().Be(" ORDER BY Value DESC");
        }

        [TestMethod]
        public void GenerateOrderBySql_WithOrderByThenBy_ShouldGenerateCorrectSql()
        {
            // Arrange
            Func<IQueryable<TestEntity>, IOrderedQueryable<TestEntity>> orderBy = 
                q => q.OrderBy(e => e.Name).ThenBy(e => e.Value);

            // Act
            var sql = this.mapper.GenerateOrderBySql(orderBy);

            // Assert
            sql.Should().Be(" ORDER BY Name ASC, Value ASC");
        }

        [TestMethod]
        public void GenerateOrderBySql_WithOrderByDescendingThenByDescending_ShouldGenerateCorrectSql()
        {
            // Arrange
            Func<IQueryable<TestEntity>, IOrderedQueryable<TestEntity>> orderBy = 
                q => q.OrderByDescending(e => e.Value).ThenByDescending(e => e.Name);

            // Act
            var sql = this.mapper.GenerateOrderBySql(orderBy);

            // Assert
            sql.Should().Be(" ORDER BY Value DESC, Name DESC");
        }

        [TestMethod]
        public void GenerateOrderBySql_WithMultipleThenBy_ShouldGenerateCorrectSql()
        {
            // Arrange
            Func<IQueryable<TestEntity>, IOrderedQueryable<TestEntity>> orderBy = 
                q => q.OrderBy(e => e.Name)
                      .ThenBy(e => e.Value)
                      .ThenByDescending(e => e.IsActive);

            // Act
            var sql = this.mapper.GenerateOrderBySql(orderBy);

            // Assert
            sql.Should().Be(" ORDER BY Name ASC, Value ASC, IsActive DESC");
        }

        [TestMethod]
        public void GenerateOrderBySql_WithNullOrderBy_ShouldReturnEmptyString()
        {
            // Act
            var sql = this.mapper.GenerateOrderBySql(null);

            // Assert
            sql.Should().BeEmpty();
        }

        [TestMethod]
        public void GenerateOrderBySql_WithColumnAttribute_ShouldUseColumnName()
        {
            // Arrange
            Func<IQueryable<TestEntity>, IOrderedQueryable<TestEntity>> orderBy = 
                q => q.OrderBy(e => e.Description);

            // Act
            var sql = this.mapper.GenerateOrderBySql(orderBy);

            // Assert
            sql.Should().Be(" ORDER BY Desc ASC");
        }
    }
}