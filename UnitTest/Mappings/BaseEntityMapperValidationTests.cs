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
            Assert.IsTrue(mapper.EnableSoftDelete);
            var pkColumns = mapper.GetPrimaryKeyColumns();
            Assert.AreEqual(2, pkColumns.Count);
            Assert.IsTrue(pkColumns.Contains("CacheKey"));
            Assert.IsTrue(pkColumns.Contains("Version"));
        }

        #endregion

        #region Expiry Configuration Validation Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void ExpiryEntity_WithRequiredProperties_MapsCorrectly()
        {
            var mapper = new BaseEntityMapper<ValidExpiryEntity, string>();

            Assert.IsTrue(mapper.EnableExpiry);
            Assert.AreEqual(TimeSpan.FromDays(30), mapper.ExpirySpan);
            
            var mappings = mapper.GetPropertyMappings();
            Assert.IsTrue(mappings.Any(m => m.Value.PropertyName == "CreationTime"));
            Assert.IsTrue(mappings.Any(m => m.Value.PropertyName == "AbsoluteExpiration"));
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void ArchiveEntity_WithIsArchivedProperty_MapsCorrectly()
        {
            var mapper = new BaseEntityMapper<ValidArchiveEntity, string>();

            // Assert
            Assert.IsTrue(mapper.EnableArchive);
            var mappings = mapper.GetPropertyMappings();
            Assert.IsTrue(mappings.Any(m => m.Value.PropertyName == "IsArchived"));
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
            Assert.IsTrue(sql.Contains("\"Select\"") || sql.Contains("[Select]"));
            Assert.IsTrue(sql.Contains("\"From\"") || sql.Contains("[From]"));
            Assert.IsTrue(sql.Contains("\"Where\"") || sql.Contains("[Where]"));
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
            Assert.IsTrue(sql.Contains("\"Select\"") || sql.Contains("[Select]"));
            Assert.IsTrue(sql.Contains("@Select"));
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
            Assert.ThrowsException<ArgumentNullException>(() => mapper.MapEntityToParameters(null));
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
            Assert.AreEqual(DBNull.Value, parameters["@CacheKey"]);
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
            Assert.AreEqual(new string('X', 255), parameters["@CacheKey"]);
            Assert.AreEqual(long.MaxValue, parameters["@BigNumber"]);
            Assert.AreEqual(decimal.MaxValue, parameters["@MaxDecimal"]);
            Assert.AreEqual(DateTime.MinValue, parameters["@MinDateTime"]);
            Assert.AreEqual(DateTime.MaxValue, parameters["@MaxDateTime"]);
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
            Assert.AreEqual("Name", columnName);
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
            Assert.IsNull(columnName);
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
            Assert.AreEqual("1'; DROP TABLE Users; --", parameters["@CacheKey"]);
            Assert.AreEqual("Robert'); DROP TABLE Students;--", parameters["@Name"]);
            Assert.AreEqual("\" OR \"1\"=\"1", parameters["@Description"]);
        }


        #endregion
    }
}