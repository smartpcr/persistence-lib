// -----------------------------------------------------------------------
// <copyright file="SQLiteExpressionTranslatorTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.SQLite
{
    using System;
    using System.Linq.Expressions;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SQLiteExpressionTranslatorTests
    {
        private SQLiteExpressionTranslator<TestEntity> translator;

        [TestInitialize]
        public void Setup()
        {
            this.translator = new SQLiteExpressionTranslator<TestEntity>();
        }

        [TestMethod]
        [TestCategory("SQLite")]
        public void Translate_DateTimeComparison_UsesDateTimeFunction()
        {
            // Arrange
            Expression<Func<TestEntity, bool>> expression = e => e.CreatedTime < DateTime.UtcNow.AddDays(-90);

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            result.Sql.Should().Contain("datetime(CreatedTime)");
            result.Sql.Should().Contain("datetime(@p0)");
            result.Sql.Should().Be("(datetime(CreatedTime) < datetime(@p0))");
            result.Parameters.Should().ContainKey("@p0");
            
            // Parameter should be stored as ISO 8601 string for SQLite
            var paramValue = result.Parameters["@p0"];
            paramValue.Should().BeOfType<string>();
            var stringValue = (string)paramValue;
            stringValue.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+Z?$");
            
            // Verify the date is approximately 90 days ago
            var parsedDate = DateTime.Parse(stringValue);
            var expectedDate = DateTime.UtcNow.AddDays(-90);
            parsedDate.Should().BeCloseTo(expectedDate, TimeSpan.FromSeconds(1));
        }

        [TestMethod]
        [TestCategory("SQLite")]
        public void Translate_DateTimeEquality_UsesDateTimeFunction()
        {
            // Arrange
            var specificDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Expression<Func<TestEntity, bool>> expression = e => e.LastWriteTime == specificDate;

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            result.Sql.Should().Contain("datetime(LastWriteTime)");
            result.Sql.Should().Contain("datetime(@p0)");
            result.Sql.Should().Be("(datetime(LastWriteTime) = datetime(@p0))");
            
            // Parameter should be stored as ISO 8601 string
            result.Parameters["@p0"].Should().BeOfType<string>();
            result.Parameters["@p0"].Should().Be(specificDate.ToString("O"));
        }

        [TestMethod]
        [TestCategory("SQLite")]
        public void Translate_DateTimeRangeQuery_UsesDateTimeFunctionForBoth()
        {
            // Arrange
            Expression<Func<TestEntity, bool>> expression = e => 
                e.CreatedTime >= DateTime.UtcNow.AddDays(-30) && 
                e.CreatedTime < DateTime.UtcNow;

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            result.Sql.Should().Contain("datetime(CreatedTime)");
            result.Sql.Should().MatchRegex(@"\(datetime\(CreatedTime\) >= datetime\(@p\d+\)\) AND \(datetime\(CreatedTime\) < datetime\(@p\d+\)\)");
            result.Parameters.Should().HaveCount(2);
        }

        [TestMethod]
        [TestCategory("SQLite")]
        public void Translate_NonDateTimeComparison_DoesNotUseDateTimeFunction()
        {
            // Arrange
            Expression<Func<TestEntity, bool>> expression = e => e.Value > 100;

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            result.Sql.Should().NotContain("datetime");
            result.Sql.Should().Be("(Value > @p0)");
            result.Parameters["@p0"].Should().Be(100);
        }

        [TestMethod]
        [TestCategory("SQLite")]
        public void Translate_MixedDateTimeAndNonDateTime_OnlyWrapsDateTime()
        {
            // Arrange
            Expression<Func<TestEntity, bool>> expression = e => 
                e.CreatedTime < DateTime.UtcNow.AddDays(-7) && 
                e.Value > 50;

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            // DateTime comparison should use datetime()
            result.Sql.Should().Contain("datetime(CreatedTime)");
            result.Sql.Should().Contain("datetime(@p0)");
            // Non-DateTime comparison should not
            result.Sql.Should().Contain("Value > @p1");
            result.Sql.Should().NotContain("datetime(Value)");
        }

        [TestMethod]
        [TestCategory("SQLite")]
        public void Translate_DateTimeWithAddMonths_UsesDateTimeFunction()
        {
            // Arrange
            Expression<Func<TestEntity, bool>> expression = e => e.LastWriteTime <= DateTime.UtcNow.AddMonths(-3);

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            result.Sql.Should().Be("(datetime(LastWriteTime) <= datetime(@p0))");
            
            // Parameter should be ISO 8601 string
            var paramValue = result.Parameters["@p0"];
            paramValue.Should().BeOfType<string>();
            var stringValue = (string)paramValue;
            
            // Parse and verify the date
            var parsedDate = DateTime.Parse(stringValue);
            var expectedDate = DateTime.UtcNow.AddMonths(-3);
            parsedDate.Should().BeCloseTo(expectedDate, TimeSpan.FromSeconds(1));
        }

        [TestMethod]
        [TestCategory("SQLite")]
        public void Translate_NullableDateTimeComparison_UsesDateTimeFunction()
        {
            // Arrange - Using TestEntity which should have nullable DateTime properties
            Expression<Func<TestEntity, bool>> expression = e => e.CreatedTime != null && e.CreatedTime < DateTime.UtcNow;

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            // The null check should not use datetime()
            result.Sql.Should().Contain("CreatedTime <> @p0");
            // The comparison should use datetime()
            result.Sql.Should().Contain("datetime(CreatedTime)");
            result.Sql.Should().Contain("datetime(@p1)");
        }
    }
}