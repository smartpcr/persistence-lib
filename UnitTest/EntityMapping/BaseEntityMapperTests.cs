// -----------------------------------------------------------------------
// <copyright file="BaseEntityMapperTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.EntityMapping
{
    using System;
    using System.Linq;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Parser;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.EntityMapping;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Parser;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BaseEntityMapperTests
    {
        private TestEntityMapper mapper;

        [TestInitialize]
        public void Setup()
        {
            this.mapper = new TestEntityMapper();
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void DiscoverProperties_ExcludesNotMapped()
        {
            // Arrange & Act
            var properties = this.mapper.GetProperties();

            // Assert
            properties.Should().NotBeNull();
            properties.Any(p => p.Name == "Ignored").Should().BeFalse(
                "Properties marked with [NotMapped] should be excluded");
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void DiscoverProperties_IncludesAllPublicProperties()
        {
            // Arrange
            var expectedProperties = new[] { "Id", "Name", "Count", "CreatedDate", "Amount", "Version", "CreatedTime", "LastWriteTime" };

            // Act
            var properties = this.mapper.GetProperties();
            var propertyNames = properties.Select(p => p.Name).ToArray();

            // Assert
            foreach (var expected in expectedProperties)
            {
                propertyNames.Should().Contain(expected,
                    $"Property {expected} should be included in discovered properties");
            }
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void GetSqlType_MapsAllSupportedTypes()
        {
            this.mapper.TestGetSqlType(typeof(Guid)).Should().Be("UNIQUEIDENTIFIER");
            this.mapper.TestGetSqlType(typeof(string)).Should().Be("NVARCHAR(255)");
            this.mapper.TestGetSqlType(typeof(int)).Should().Be("INT");
            this.mapper.TestGetSqlType(typeof(DateTime)).Should().Be("DATETIME2");
            this.mapper.TestGetSqlType(typeof(decimal)).Should().Be("DECIMAL(18,2)");
            this.mapper.TestGetSqlType(typeof(bool)).Should().Be("BIT");
            this.mapper.TestGetSqlType(typeof(long)).Should().Be("BIGINT");
            this.mapper.TestGetSqlType(typeof(float)).Should().Be("REAL");
            this.mapper.TestGetSqlType(typeof(double)).Should().Be("FLOAT");
            this.mapper.TestGetSqlType(typeof(byte[])).Should().Be("VARBINARY(MAX)");
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void GetSqlType_HandlesNullableTypes()
        {
            // Arrange & Act & Assert
            this.mapper.TestGetSqlType(typeof(int?)).Should().Be("INT");
            this.mapper.TestGetSqlType(typeof(decimal?)).Should().Be("DECIMAL(18,2)");
            this.mapper.TestGetSqlType(typeof(DateTime?)).Should().Be("DATETIME2");
            this.mapper.TestGetSqlType(typeof(bool?)).Should().Be("BIT");
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void GenerateColumnName_RetainPascalCase()
        {
            // Arrange & Act & Assert
            this.mapper.TestGenerateColumnName("Id").Should().Be("Id");
            this.mapper.TestGenerateColumnName("Name").Should().Be("Name");
            this.mapper.TestGenerateColumnName("CreatedDate").Should().Be("CreatedDate");
            this.mapper.TestGenerateColumnName("LastWriteTime").Should().Be("LastWriteTime");
            this.mapper.TestGenerateColumnName("MyLongPropertyName").Should().BeNull();
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void GenerateCreateTableSql_WithSoftDelete()
        {
            // Arrange
            var mapperWithSoftDelete = new TestEntityMapperWithSoftDelete();

            // Act
            var sql = mapperWithSoftDelete.TestGenerateCreateTableSql();
            var node = DebugParser.ParseSqlStatement(sql);

            node.Should().NotBeNull();
            node.Should().BeOfType<CreateTableStatement>("Should parse as CREATE TABLE statement");
            var createTable = (CreateTableStatement)node;
            createTable.TableName.Should().Be("TestEntity", "Should reference correct table name");
            createTable.Columns.Should().Contain(c => c.Name == "Version", "Should include Version column for soft delete");
            createTable.Columns.Should().Contain(c => c.Name == "IsDeleted", "Should include IsDeleted column for soft delete");
            createTable.Constraints.Any(c =>
                    c.Type == ConstraintType.PrimaryKey &&
                    c.Columns.Count == 2 &&
                    c.Columns.Contains("Id") &&
                    c.Columns.Contains("Version"))
                .Should().BeTrue("Should define primary key on both Id and Version column");

            sql.Should().NotBeNull();
            sql.Should().Contain("CREATE TABLE IF NOT EXISTS", "Should have CREATE TABLE statement");
            sql.Should().Contain("TestEntity", "Should reference correct table name");
            sql.Should().Contain("Version", "Should include Version column for soft delete");
            sql.Should().Contain("IsDeleted", "Should include IsDeleted column for soft delete");
            sql.Should().Contain("PRIMARY KEY", "Should define primary key");
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void GenerateCreateTableSql_WithExpiry()
        {
            // Arrange
            var mapperWithExpiry = new TestEntityMapperWithExpiry();

            // Act
            var sql = mapperWithExpiry.TestGenerateCreateTableSql();

            // Assert
            sql.Should().NotBeNull();
            sql.Should().Contain("AbsoluteExpiration", "Should include AbsoluteExpiration column");
            sql.Should().Contain("DATETIME", "AbsoluteExpiration should be DATETIME type");
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void GenerateCreateIndexSql_CreatesRequiredIndexes()
        {
            // Arrange & Act
            var indexSql = this.mapper.TestGenerateCreateIndexSql();

            // Assert
            indexSql.Should().NotBeNull();
            indexSql.Should().NotBeEmpty("Should generate at least one index SQL statement");
            indexSql.Any(i => i.Contains("CREATE INDEX")).Should().BeTrue("Should contain CREATE INDEX statement");
            indexSql.Any(i => i.Contains("IX_TestEntity_Version")).Should().BeTrue("Should create index on Version column");
            indexSql.Any(i => i.Contains("ON TestEntity (Version)")).Should().BeTrue("Should create index on Version column");
            indexSql.Any(i => i.Contains("IX_TestEntity_LastWriteTime")).Should().BeTrue("Should create index on LastWriteTime column");
            indexSql.Any(i => i.Contains("ON TestEntity (LastWriteTime)")).Should().BeTrue("Should create index on LastWriteTime column");
        }
    }
}