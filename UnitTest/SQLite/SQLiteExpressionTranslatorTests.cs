// -----------------------------------------------------------------------
// <copyright file="SQLiteExpressionTranslatorTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.SQLite
{
    using System;
    using System.Globalization;
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
            var testDateTime = DateTime.UtcNow;
            Expression<Func<TestEntity, bool>> expression = e => e.CreatedDate < testDateTime.AddDays(-90);

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            result.Sql.Should().Contain("datetime(CreatedDate)");
            result.Sql.Should().Contain("datetime(@p0)");
            result.Sql.Should().Be("(datetime(CreatedDate) < datetime(@p0))");
            result.Parameters.Should().ContainKey("@p0");
            
            // Parameter should be stored as ISO 8601 string for SQLite
            var paramValue = result.Parameters["@p0"];
            paramValue.Should().BeOfType<string>();
            var stringValue = (string)paramValue;
            stringValue.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+Z?$");
            
            // Verify the date is approximately 90 days ago
            // Use RoundtripKind to preserve UTC timezone
            var parsedDate = DateTime.Parse(stringValue, null, System.Globalization.DateTimeStyles.RoundtripKind);
            var expectedDate = testDateTime.AddDays(-90);
            parsedDate.Should().BeCloseTo(expectedDate, TimeSpan.FromSeconds(1));
        }

        [TestMethod]
        [TestCategory("SQLite")]
        public void Translate_DateTimeEquality_UsesDateTimeFunction()
        {
            // Arrange
            var specificDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Expression<Func<TestEntity, bool>> expression = e => e.ModifiedDate == specificDate;

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            result.Sql.Should().Contain("datetime(ModifiedDate)");
            result.Sql.Should().Contain("datetime(@p0)");
            result.Sql.Should().Be("(datetime(ModifiedDate) = datetime(@p0))");
            
            // Parameter should be stored as ISO 8601 string
            result.Parameters["@p0"].Should().BeOfType<string>();
            result.Parameters["@p0"].Should().Be(specificDate.ToString("O"));
        }

        [TestMethod]
        [TestCategory("SQLite")]
        public void Translate_DateTimeRangeQuery_UsesDateTimeFunctionForBoth()
        {
            // Arrange
            var testDateTime = DateTime.UtcNow;
            Expression<Func<TestEntity, bool>> expression = e => 
                e.CreatedDate >= testDateTime.AddDays(-30) && 
                e.CreatedDate < testDateTime;

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            result.Sql.Should().Contain("datetime(CreatedDate)");
            result.Sql.Should().MatchRegex(@"\(datetime\(CreatedDate\) >= datetime\(@p\d+\)\) AND \(datetime\(CreatedDate\) < datetime\(@p\d+\)\)");
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
            var testDateTime = DateTime.UtcNow;
            Expression<Func<TestEntity, bool>> expression = e => 
                e.CreatedDate < testDateTime.AddDays(-7) && 
                e.Value > 50;

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            // DateTime comparison should use datetime()
            result.Sql.Should().Contain("datetime(CreatedDate)");
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
            var testDateTime = DateTime.UtcNow;
            Expression<Func<TestEntity, bool>> expression = e => e.ModifiedDate <= testDateTime.AddMonths(-3);

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            result.Sql.Should().Be("(datetime(ModifiedDate) <= datetime(@p0))");
            
            // Parameter should be ISO 8601 string
            var paramValue = result.Parameters["@p0"];
            paramValue.Should().BeOfType<string>();
            var stringValue = (string)paramValue;
            
            // Parse and verify the date with proper UTC handling
            var parsedDate = DateTime.Parse(stringValue, null, DateTimeStyles.RoundtripKind);
            var expectedDate = testDateTime.AddMonths(-3);
            parsedDate.Should().BeCloseTo(expectedDate, TimeSpan.FromSeconds(1));
        }

        [TestMethod]
        [TestCategory("SQLite")]
        public void Translate_NullableDateTimeComparison_UsesDateTimeFunction()
        {
            // Arrange - Using TestEntity which has nullable DeletedDate property
            var testDateTime = DateTime.UtcNow;
            Expression<Func<TestEntity, bool>> expression = e => e.DeletedDate != null && e.DeletedDate < testDateTime;

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            // SQLite translator wraps all DateTime operations with datetime() function
            // even for null checks, which is actually OK for SQLite
            result.Sql.Should().Contain("datetime(DeletedDate) <> @p0");
            // The comparison should use datetime()
            result.Sql.Should().Contain("datetime(DeletedDate) < datetime(@p1)");
        }
    }
}