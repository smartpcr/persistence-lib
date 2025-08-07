//-------------------------------------------------------------------------------
// <copyright file="SQLiteExpressionTranslatorOrderByTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Query
{
    using System;
    using System.Linq;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SQLiteExpressionTranslatorOrderByTests
    {
        private ExpressionTranslator<TestEntity> translator;

        [TestInitialize]
        public void TestInitialize()
        {
            this.translator = new ExpressionTranslator<TestEntity>();
        }

        [TestMethod]
        public void TranslateOrderBy_WithSingleOrderBy_ShouldGenerateCorrectSql()
        {
            // Arrange
            Func<IQueryable<TestEntity>, IOrderedQueryable<TestEntity>> orderBy = 
                q => q.OrderBy(e => e.Name);

            // Act
            var sql = this.translator.TranslateOrderBy(orderBy);

            // Assert
            sql.Should().Be("ORDER BY Name ASC");
        }

        [TestMethod]
        public void TranslateOrderBy_WithOrderByDescending_ShouldGenerateCorrectSql()
        {
            // Arrange
            Func<IQueryable<TestEntity>, IOrderedQueryable<TestEntity>> orderBy = 
                q => q.OrderByDescending(e => e.Value);

            // Act
            var sql = this.translator.TranslateOrderBy(orderBy);

            // Assert
            sql.Should().Be("ORDER BY Value DESC");
        }

        [TestMethod]
        public void TranslateOrderBy_WithOrderByThenBy_ShouldGenerateCorrectSql()
        {
            // Arrange
            Func<IQueryable<TestEntity>, IOrderedQueryable<TestEntity>> orderBy = 
                q => q.OrderBy(e => e.Name).ThenBy(e => e.Value);

            // Act
            var sql = this.translator.TranslateOrderBy(orderBy);

            // Assert
            sql.Should().Be("ORDER BY Name ASC, Value ASC");
        }

        [TestMethod]
        public void TranslateOrderBy_WithOrderByDescendingThenByDescending_ShouldGenerateCorrectSql()
        {
            // Arrange
            Func<IQueryable<TestEntity>, IOrderedQueryable<TestEntity>> orderBy = 
                q => q.OrderByDescending(e => e.Value).ThenByDescending(e => e.Name);

            // Act
            var sql = this.translator.TranslateOrderBy(orderBy);

            // Assert
            sql.Should().Be("ORDER BY Value DESC, Name DESC");
        }

        [TestMethod]
        public void TranslateOrderBy_WithMultipleThenBy_ShouldGenerateCorrectSql()
        {
            // Arrange
            Func<IQueryable<TestEntity>, IOrderedQueryable<TestEntity>> orderBy = 
                q => q.OrderBy(e => e.Name)
                      .ThenBy(e => e.Value)
                      .ThenByDescending(e => e.IsActive);

            // Act
            var sql = this.translator.TranslateOrderBy(orderBy);

            // Assert
            sql.Should().Be("ORDER BY Name ASC, Value ASC, IsActive DESC");
        }

        [TestMethod]
        public void TranslateOrderBy_WithNullOrderBy_ShouldReturnEmptyString()
        {
            // Act
            var sql = this.translator.TranslateOrderBy(null);

            // Assert
            sql.Should().BeEmpty();
        }

        [TestMethod]
        public void TranslateOrderBy_WithComplexChaining_ShouldGenerateCorrectSql()
        {
            // Arrange
            Func<IQueryable<TestEntity>, IOrderedQueryable<TestEntity>> orderBy = 
                q => q.OrderByDescending(e => e.CreatedDate)
                      .ThenBy(e => e.Name)
                      .ThenByDescending(e => e.Value)
                      .ThenBy(e => e.IsActive);

            // Act
            var sql = this.translator.TranslateOrderBy(orderBy);

            // Assert
            sql.Should().Be("ORDER BY CreatedDate DESC, Name ASC, Value DESC, IsActive ASC");
        }

        [TestMethod]
        public void TranslateOrderBy_WithIdProperty_ShouldUseCorrectColumn()
        {
            // Arrange
            Func<IQueryable<TestEntity>, IOrderedQueryable<TestEntity>> orderBy = 
                q => q.OrderBy(e => e.Id);

            // Act
            var sql = this.translator.TranslateOrderBy(orderBy);

            // Assert
            sql.Should().Be("ORDER BY Id ASC");
        }

        private class TestEntity
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public int Value { get; set; }
            public bool IsActive { get; set; }
            public DateTime CreatedDate { get; set; }
        }
    }
}