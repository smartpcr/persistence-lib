//-------------------------------------------------------------------------------
// <copyright file="BaseEntityMapperValidationTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Mappings
{
    using System;
    using System.Data;
    using System.Linq;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Validation-focused unit tests for <see cref="BaseEntityMapper{T,TKey}"/>.
    /// </summary>
    [TestClass]
    public class BaseEntityMapperValidationTests
    {

        #region Soft Delete Validation Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void ValidSoftDeleteEntity_WithVersion_MapsCorrectly()
        {
            // Arrange & Act
            var mapper = new BaseEntityMapper<ValidSoftDeleteEntity, string>();

            // Assert
            mapper.EnableSoftDelete.Should().BeTrue();
            var pkColumns = mapper.GetPrimaryKeyColumns();
            pkColumns.Count.Should().Be(2);
            pkColumns.Should().Contain("CacheKey");
            pkColumns.Should().Contain("Version");
        }

        #endregion

        #region Expiry Configuration Validation Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void ExpiryEntity_WithRequiredProperties_MapsCorrectly()
        {
            var mapper = new BaseEntityMapper<ValidExpiryEntity, string>();

            mapper.EnableExpiry.Should().BeTrue();
            mapper.ExpirySpan.Should().Be(TimeSpan.FromDays(30));

            var mappings = mapper.GetPropertyMappings();
            mappings.Any(m => m.Value.PropertyName == "CreationTime").Should().BeTrue();
            mappings.Any(m => m.Value.PropertyName == "AbsoluteExpiration").Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void ArchiveEntity_WithIsArchivedProperty_MapsCorrectly()
        {
            var mapper = new BaseEntityMapper<ValidArchiveEntity, string>();

            // Assert
            mapper.EnableArchive.Should().BeTrue();
            var mappings = mapper.GetPropertyMappings();
            mappings.Any(m => m.Value.PropertyName == "IsArchived").Should().BeTrue();
        }

        #endregion

        #region SQL Generation Validation Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateCreateTableSql_WithReservedKeywords_EscapesColumnNames()
        {
            // Arrange
            var mapper = new BaseEntityMapper<ReservedKeywordEntity, string>();

            // Act
            var sql = mapper.GenerateCreateTableSql();

            // Assert
            // Should escape reserved keywords with quotes or brackets
            (sql.Contains("\"Select\"") || sql.Contains("[Select]")).Should().BeTrue();
            (sql.Contains("\"From\"") || sql.Contains("[From]")).Should().BeTrue();
            (sql.Contains("\"Where\"") || sql.Contains("[Where]")).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateInsertSql_WithReservedKeywords_EscapesColumnNames()
        {
            // Arrange
            var mapper = new BaseEntityMapper<ReservedKeywordEntity, string>();

            // Act
            var sql = mapper.GenerateInsertSql();

            // Assert
            (sql.Contains("\"Select\"") || sql.Contains("[Select]")).Should().BeTrue();
            sql.Should().Contain("@Select");
        }

        #endregion

        #region Parameter Mapping Validation Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void MapEntityToParameters_WithNullEntity_ThrowsArgumentNullException()
        {
            // Arrange
            var mapper = new BaseEntityMapper<ValidSoftDeleteEntity, string>();

            // Act & Assert
            Action act = () => mapper.MapEntityToParameters(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void MapIdToParameters_WithNullId_HandlesCorrectly()
        {
            // Arrange
            var mapper = new BaseEntityMapper<ValidSoftDeleteEntity, string>();

            // Act
            var parameters = mapper.MapIdToParameters(null);

            // Assert
            parameters["@CacheKey"].Should().Be(DBNull.Value);
        }

        #endregion

        #region Boundary and Edge Case Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateCreateTableSql_WithEmptyTableName_TakesTypeName()
        {
            // Arrange
            var mapper = new BaseEntityMapper<EntityWithEmptyTableName, string>();

            // Act & Assert
            mapper.GetFullTableName().Should().Be("EntityWithEmptyTableName");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void MapEntityToParameters_WithMaxValues_HandlesCorrectly()
        {
            // Arrange
            var mapper = new BaseEntityMapper<ComplexBoundaryEntity, string>();
            var entity = new ComplexBoundaryEntity
            {
                Id = new string('X', 255), // Max length string
                BigNumber = long.MaxValue,
                MaxDecimal = decimal.MaxValue,
                MinDateTime = DateTime.MinValue,
                MaxDateTime = DateTime.MaxValue
            };

            // Act
            var parameters = mapper.MapEntityToParameters(entity);

            // Assert
            parameters["@CacheKey"].Should().Be(new string('X', 255));
            parameters["@BigNumber"].Should().Be(long.MaxValue);
            parameters["@MaxDecimal"].Should().Be(decimal.MaxValue);
            parameters["@MinDateTime"].Should().Be(DateTime.MinValue);
            parameters["@MaxDateTime"].Should().Be(DateTime.MaxValue);
        }


        #endregion

        #region Column Mapping Validation Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GetColumnName_ForMappedProperty_ReturnsColumnName()
        {
            // Arrange
            var mapper = new BaseEntityMapper<ValidSoftDeleteEntity, string>();

            // Act
            var columnName = mapper.GetColumnName("Name");

            // Assert
            columnName.Should().Be("Name");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GetColumnName_ForUnmappedProperty_ReturnsNull()
        {
            // Arrange
            var mapper = new BaseEntityMapper<NotMappedTestEntity, string>();

            // Act
            var columnName = mapper.GetColumnName("UnmappedProperty");

            // Assert
            columnName.Should().BeNull();
        }


        #endregion

        #region SQL Injection Prevention Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void MapEntityToParameters_WithSqlInjectionAttempt_SafelyParameterizes()
        {
            // Arrange
            var mapper = new BaseEntityMapper<SqlInjectionTestEntity, string>();
            var entity = new SqlInjectionTestEntity
            {
                Id = "1'; DROP TABLE Users; --",
                Name = "Robert'); DROP TABLE Students;--",
                Description = "\" OR \"1\"=\"1"
            };

            // Act
            var parameters = mapper.MapEntityToParameters(entity);

            // Assert
            // Values should be parameterized, not escaped
            parameters["@CacheKey"].Should().Be("1'; DROP TABLE Users; --");
            parameters["@Name"].Should().Be("Robert'); DROP TABLE Students;--");
            parameters["@Description"].Should().Be("\" OR \"1\"=\"1");
        }


        #endregion
    }
}