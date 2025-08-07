//-------------------------------------------------------------------------------
// <copyright file="BaseEntityMapperPropertyHidingTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Mappings
{
    using System;
    using System.Linq;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for property hiding scenarios in BaseEntityMapper.
    /// </summary>
    [TestClass]
    public class BaseEntityMapperPropertyHidingTests
    {
        [TestMethod]
        public void ChangePrimaryKeyDataType()
        {
            var mapper = new BaseEntityMapper<AuditRecord, long>();
            mapper.GetPrimaryKeyColumns().Count.Should().Be(1, 
                "AuditRecord should have a single primary key column");
            var mappings = mapper.GetPropertyMappings();
            var primaryKeyMapping = mappings.FirstOrDefault(m => m.Value.IsPrimaryKey);
            primaryKeyMapping.Should().NotBeNull("Primary key mapping should exist");
            primaryKeyMapping.Value.SqlType.Should().Be(System.Data.SqlDbType.BigInt, 
                "Primary key should use BigInt as SQL type");
            primaryKeyMapping.Value.PropertyName.Should().Be("Id", "primary key should be named 'Id'");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void PropertyHiding_WithNotMapped_ExcludesPropertyFromMapping()
        {
            // Arrange
            var mapper = new BaseEntityMapper<PropertyHidingEntity, string>();

            // Act
            var mappings = mapper.GetPropertyMappings();
            var hasIdMapping = mappings.Any(m => m.Value.PropertyName == "Id");
            var hasTestIdMapping = mappings.Any(m => m.Value.PropertyName == "TestId");

            // Assert
            hasIdMapping.Should().BeFalse("Id property should not be mapped when marked with [NotMapped]");
            hasTestIdMapping.Should().BeTrue("TestId property should be mapped");
            mapper.GetPrimaryKeyColumn().Should().Be("TestId");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void Entity_WithIdFieldNotMapped_DoesNotUseDerivedId()
        {
            var mapper = new BaseEntityMapper<EntityWithoutIdField, string>();

            // If we get here without exception, check that Id is not mapped
            var mappings = mapper.GetPropertyMappings();
            var hasIdMapping = mappings.Any(m => m.Value.PropertyName == "Id");

            hasIdMapping.Should().BeFalse("Id property should not be mapped when marked with [NotMapped]");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void PropertyHiding_ChangingPropertyType_UsesDerivedProperty()
        {
            // Arrange
            var mapper = new BaseEntityMapper<PropertyTypeChangeEntity, string>();

            // Act
            var mappings = mapper.GetPropertyMappings();
            var versionMapping = mappings.FirstOrDefault(m => m.Value.PropertyName == "Version");

            // Assert
            versionMapping.Value.Should().NotBeNull("Version property should be mapped");
            versionMapping.Value.SqlType.Should().Be(System.Data.SqlDbType.Text, 
                "Should use the derived property's SQL type");
            versionMapping.Value.ColumnName.Should().Be("Data", 
                "Should use the derived property's column name");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateCreateTableSql_WithPropertyHiding_UsesCorrectProperties()
        {
            // Arrange
            var mapper = new BaseEntityMapper<PropertyHidingEntity, string>();

            // Act
            var sql = mapper.GenerateCreateTableSql();

            // Assert
            sql.Should().NotContain("CacheKey TEXT", "Should not include the hidden Id column");
            sql.Should().Contain("TestId TEXT", "Should include the TestId column");
            sql.Should().Contain("PRIMARY KEY (TestId)", "Should use TestId as primary key");
        }
    }
}