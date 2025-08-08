// -----------------------------------------------------------------------
// <copyright file="BaseEntityMapperTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.EntityMapping
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Reflection;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
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

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void GetColumnMappings_RespectsColumnAttributes()
        {
            // Arrange
            var mapper = new ColumnDefinitionTestEntityMapper();

            // Act
            var mappings = mapper.GetColumnMappings();

            // Assert - Verify column mappings exist
            mappings.Should().NotBeNull();
            mappings.Should().NotBeEmpty();

            // Verify NotMapped property is excluded
            mappings.Keys.Any(p => p.Name == "IgnoredProperty").Should().BeFalse(
                "Properties marked with [NotMapped] should be excluded");

            // Find specific column mappings
            var requiredStringMapping = mappings.FirstOrDefault(m => m.Key.Name == "RequiredString").Value;
            var optionalStringMapping = mappings.FirstOrDefault(m => m.Key.Name == "OptionalString").Value;
            var decimalMapping = mappings.FirstOrDefault(m => m.Key.Name == "DecimalColumn").Value;
            var bigDecimalMapping = mappings.FirstOrDefault(m => m.Key.Name == "BigDecimal").Value;
            var binaryMapping = mappings.FirstOrDefault(m => m.Key.Name == "BinaryColumn").Value;
            var maxBinaryMapping = mappings.FirstOrDefault(m => m.Key.Name == "MaxBinaryColumn").Value;

            // Assert - Verify column names
            requiredStringMapping?.ColumnName.Should().Be("RequiredString");
            optionalStringMapping?.ColumnName.Should().Be("OptionalString");
            decimalMapping?.ColumnName.Should().Be("DecimalColumn");

            // Assert - Verify SqlDbType
            requiredStringMapping?.SqlType.Should().Be(SqlDbType.NVarChar);
            decimalMapping?.SqlType.Should().Be(SqlDbType.Decimal);
            binaryMapping?.SqlType.Should().Be(SqlDbType.VarBinary);

            // Assert - Verify Size
            requiredStringMapping?.Size.Should().Be(255);
            optionalStringMapping?.Size.Should().Be(100);
            binaryMapping?.Size.Should().Be(1024);
            maxBinaryMapping?.Size.Should().Be(-1, "Size -1 indicates MAX");

            // Assert - Verify NotNull
            requiredStringMapping?.IsNotNull.Should().BeTrue("RequiredString should be NOT NULL");
            optionalStringMapping?.IsNotNull.Should().BeFalse("OptionalString should be nullable");
            decimalMapping?.IsNotNull.Should().BeTrue("DecimalColumn should be NOT NULL");

            // Assert - Verify Precision and Scale
            decimalMapping?.Precision.Should().Be(10);
            decimalMapping?.Scale.Should().Be(3);
            bigDecimalMapping?.Precision.Should().Be(28);
            bigDecimalMapping?.Scale.Should().Be(8);
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void GenerateCreateTableSql_IncludesAllColumnDefinitions()
        {
            // Arrange
            var mapper = new ColumnDefinitionTestEntityMapper();

            // Act
            var sql = mapper.TestGenerateCreateTableSql();

            // Assert - Basic structure
            sql.Should().NotBeNull();
            sql.Should().Contain("CREATE TABLE IF NOT EXISTS");
            sql.Should().Contain("ColumnDefinitionTest");

            // Assert - Primary key
            sql.Should().Contain("Id UNIQUEIDENTIFIER NOT NULL");
            sql.Should().Contain("PRIMARY KEY (Id)");

            // Assert - String columns with size
            sql.Should().Contain("RequiredString NVARCHAR(255) NOT NULL");
            sql.Should().Contain("OptionalString NVARCHAR(100)");
            sql.Should().MatchRegex(@"TextColumn\s+(TEXT|NVARCHAR\(MAX\))");

            // Assert - Numeric columns
            sql.Should().Contain("IntColumn INT NOT NULL");
            sql.Should().MatchRegex(@"NullableInt\s+INT(?!\s+NOT\s+NULL)");
            sql.Should().Contain("DecimalColumn DECIMAL(10,3) NOT NULL");
            sql.Should().MatchRegex(@"BigDecimal\s+DECIMAL\(28,8\)");
            sql.Should().Contain("BigIntColumn BIGINT NOT NULL");
            sql.Should().Contain("SmallIntColumn SMALLINT");
            sql.Should().Contain("TinyIntColumn TINYINT");

            // Assert - Binary columns
            sql.Should().MatchRegex(@"BinaryColumn\s+VARBINARY\(1024\)");
            sql.Should().MatchRegex(@"MaxBinaryColumn\s+VARBINARY\(MAX\)");

            // Assert - Float/Real columns
            sql.Should().Contain("FloatColumn FLOAT");
            sql.Should().Contain("RealColumn REAL");

            // Assert - DateTime columns
            sql.Should().Contain("DateTimeColumn DATETIME NOT NULL");
            sql.Should().MatchRegex(@"DateTime2Column\s+DATETIME2");

            // Assert - Bit column
            sql.Should().Contain("BitColumn BIT NOT NULL");

            // Assert - Unique constraint
            sql.Should().MatchRegex(@"UniqueIdColumn\s+UNIQUEIDENTIFIER");

            // Assert - Should not contain ignored property
            sql.Should().NotContain("IgnoredProperty");
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void GenerateCreateTableSql_HandlesSpecialAttributes()
        {
            // Arrange
            var mapper = new ColumnDefinitionTestEntityMapper();

            // Act
            var sql = mapper.TestGenerateCreateTableSql();
            var indexSql = mapper.TestGenerateCreateIndexSql();

            // Assert - Index creation
            indexSql.Should().Contain(i => i.Contains("IX_IndexedColumn"));
            indexSql.Should().Contain(i => i.Contains("CREATE INDEX") && i.Contains("IndexedColumn"));

            // Assert - Foreign key should be in column definition
            sql.Should().Contain("ForeignKeyColumn");

            // Assert - Computed column handling
            sql.Should().Contain("ComputedColumn");

            // Assert - Unique constraint
            sql.Should().Contain("UniqueIdColumn");
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void GetColumnMappings_HandlesAllSqlDbTypes()
        {
            // Arrange
            var mapper = new ColumnDefinitionTestEntityMapper();

            // Act
            var mappings = mapper.GetColumnMappings();

            // Assert - Verify all SqlDbType values are handled
            var columnTypes = new Dictionary<string, SqlDbType>
            {
                { "Id", SqlDbType.UniqueIdentifier },
                { "RequiredString", SqlDbType.NVarChar },
                { "TextColumn", SqlDbType.Text },
                { "IntColumn", SqlDbType.Int },
                { "DecimalColumn", SqlDbType.Decimal },
                { "BitColumn", SqlDbType.Bit },
                { "DateTimeColumn", SqlDbType.DateTime },
                { "DateTime2Column", SqlDbType.DateTime2 },
                { "BinaryColumn", SqlDbType.VarBinary },
                { "FloatColumn", SqlDbType.Float },
                { "RealColumn", SqlDbType.Real },
                { "BigIntColumn", SqlDbType.BigInt },
                { "SmallIntColumn", SqlDbType.SmallInt },
                { "TinyIntColumn", SqlDbType.TinyInt },
                { "UniqueIdColumn", SqlDbType.UniqueIdentifier }
            };

            foreach (var kvp in columnTypes)
            {
                var mapping = mappings.FirstOrDefault(m => m.Key.Name == kvp.Key).Value;
                mapping.Should().NotBeNull($"Mapping for {kvp.Key} should exist");
                mapping.SqlType.Should().Be(kvp.Value, $"{kvp.Key} should have SqlDbType.{kvp.Value}");
            }
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void GetColumnMappings_ValidatesPrimaryKeyConfiguration()
        {
            // Arrange
            var mapper = new ColumnDefinitionTestEntityMapper();

            // Act
            var mappings = mapper.GetColumnMappings();
            var idMapping = mappings.FirstOrDefault(m => m.Key.Name == "Id");

            // Assert
            idMapping.Key.Should().NotBeNull("Id property should exist");
            var primaryKeyAttr = idMapping.Key.GetCustomAttribute<PrimaryKeyAttribute>();
            primaryKeyAttr.Should().NotBeNull("Id should have PrimaryKey attribute");
            primaryKeyAttr.Order.Should().Be(1, "Primary key order should be 1");

            idMapping.Value.Should().NotBeNull("Id should have column mapping");
            idMapping.Value.IsNotNull.Should().BeTrue("Primary key column should be NOT NULL");
            idMapping.Value.SqlType.Should().Be(SqlDbType.UniqueIdentifier);
        }
    }
}