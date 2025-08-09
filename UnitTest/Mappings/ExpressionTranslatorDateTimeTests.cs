// -----------------------------------------------------------------------
// <copyright file="ExpressionTranslatorDateTimeTests.cs" company="Microsoft Corp.">
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
    public class ExpressionTranslatorDateTimeTests
    {
        private ExpressionTranslator<TestEntity> translator;

        [TestInitialize]
        public void Setup()
        {
            this.translator = new ExpressionTranslator<TestEntity>();
        }

        [TestMethod]
        [TestCategory("ExpressionTranslator")]
        public void Translate_DateTimeAddDays_CreatesCorrectParameter()
        {
            // Arrange
            Expression<Func<TestEntity, bool>> expression = e => e.CreatedTime < DateTime.UtcNow.AddDays(-90);

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            result.Sql.Should().Contain("(CreatedTime < @p0)");
            result.Parameters.Should().ContainKey("@p0");
            
            // The parameter should be a DateTime that is approximately 90 days ago
            var paramValue = result.Parameters["@p0"];
            paramValue.Should().BeOfType<DateTime>();
            var dateValue = (DateTime)paramValue;
            var expectedDate = DateTime.UtcNow.AddDays(-90);
            dateValue.Should().BeCloseTo(expectedDate, TimeSpan.FromSeconds(1));
        }

        [TestMethod]
        [TestCategory("ExpressionTranslator")]
        public void Translate_DateTimeAddMonths_CreatesCorrectParameter()
        {
            // Arrange
            Expression<Func<TestEntity, bool>> expression = e => e.LastWriteTime > DateTime.UtcNow.AddMonths(-3);

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            result.Sql.Should().Contain("(LastWriteTime > @p0)");
            result.Parameters.Should().ContainKey("@p0");
            
            var paramValue = result.Parameters["@p0"];
            paramValue.Should().BeOfType<DateTime>();
            var dateValue = (DateTime)paramValue;
            var expectedDate = DateTime.UtcNow.AddMonths(-3);
            dateValue.Should().BeCloseTo(expectedDate, TimeSpan.FromSeconds(1));
        }

        [TestMethod]
        [TestCategory("ExpressionTranslator")]
        public void Translate_DateTimeAddYears_CreatesCorrectParameter()
        {
            // Arrange
            Expression<Func<TestEntity, bool>> expression = e => e.CreatedTime >= DateTime.UtcNow.AddYears(-1);

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            result.Sql.Should().Contain("(CreatedTime >= @p0)");
            result.Parameters.Should().ContainKey("@p0");
            
            var paramValue = result.Parameters["@p0"];
            paramValue.Should().BeOfType<DateTime>();
            var dateValue = (DateTime)paramValue;
            var expectedDate = DateTime.UtcNow.AddYears(-1);
            dateValue.Should().BeCloseTo(expectedDate, TimeSpan.FromSeconds(1));
        }

        [TestMethod]
        [TestCategory("ExpressionTranslator")]
        public void Translate_DateTimeAddHours_CreatesCorrectParameter()
        {
            // Arrange
            Expression<Func<TestEntity, bool>> expression = e => e.LastWriteTime <= DateTime.UtcNow.AddHours(24);

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            result.Sql.Should().Contain("(LastWriteTime <= @p0)");
            result.Parameters.Should().ContainKey("@p0");
            
            var paramValue = result.Parameters["@p0"];
            paramValue.Should().BeOfType<DateTime>();
            var dateValue = (DateTime)paramValue;
            var expectedDate = DateTime.UtcNow.AddHours(24);
            dateValue.Should().BeCloseTo(expectedDate, TimeSpan.FromSeconds(1));
        }

        [TestMethod]
        [TestCategory("ExpressionTranslator")]
        public void Translate_DateTimeAddMinutes_CreatesCorrectParameter()
        {
            // Arrange
            Expression<Func<TestEntity, bool>> expression = e => e.CreatedTime > DateTime.UtcNow.AddMinutes(-30);

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            result.Sql.Should().Contain("(CreatedTime > @p0)");
            result.Parameters.Should().ContainKey("@p0");
            
            var paramValue = result.Parameters["@p0"];
            paramValue.Should().BeOfType<DateTime>();
            var dateValue = (DateTime)paramValue;
            var expectedDate = DateTime.UtcNow.AddMinutes(-30);
            dateValue.Should().BeCloseTo(expectedDate, TimeSpan.FromSeconds(1));
        }

        [TestMethod]
        [TestCategory("ExpressionTranslator")]
        public void Translate_ComplexDateTimeExpression_CreatesCorrectParameters()
        {
            // Arrange
            Expression<Func<TestEntity, bool>> expression = e => 
                e.CreatedTime > DateTime.UtcNow.AddDays(-30) && 
                e.LastWriteTime < DateTime.UtcNow.AddHours(-1);

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            result.Sql.Should().Contain("CreatedTime > @p0");
            result.Sql.Should().Contain("LastWriteTime < @p1");
            result.Sql.Should().Contain(" AND ");
            result.Parameters.Should().HaveCount(2);
            
            // Verify both date parameters
            var param0 = (DateTime)result.Parameters["@p0"];
            param0.Should().BeCloseTo(DateTime.UtcNow.AddDays(-30), TimeSpan.FromSeconds(1));
            
            var param1 = (DateTime)result.Parameters["@p1"];
            param1.Should().BeCloseTo(DateTime.UtcNow.AddHours(-1), TimeSpan.FromSeconds(1));
        }

        [TestMethod]
        [TestCategory("ExpressionTranslator")]
        public void Translate_DateTimeWithSpecificDate_CreatesCorrectParameter()
        {
            // Arrange
            var specificDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Expression<Func<TestEntity, bool>> expression = e => e.CreatedTime > specificDate.AddDays(30);

            // Act
            var result = this.translator.Translate(expression);

            // Assert
            result.Should().NotBeNull();
            result.Sql.Should().Contain("(CreatedTime > @p0)");
            result.Parameters.Should().ContainKey("@p0");
            
            var paramValue = result.Parameters["@p0"];
            paramValue.Should().BeOfType<DateTime>();
            var dateValue = (DateTime)paramValue;
            var expectedDate = new DateTime(2024, 1, 31, 0, 0, 0, DateTimeKind.Utc);
            dateValue.Should().Be(expectedDate);
        }
    }
}