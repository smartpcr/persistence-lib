// -----------------------------------------------------------------------
// <copyright file="BaseEntityMapperTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.EntityMapping
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Extensions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BaseEntityMapperTests
    {
        [Table("TestEntity")]
        public class TestEntity : BaseEntity<Guid>
        {
            [PrimaryKey(Order = 1)]
            public new Guid Id { get; set; }
            public string Name { get; set; }
            public int Count { get; set; }
            public DateTime CreatedDate { get; set; }
            public decimal? Amount { get; set; }
            [NotMapped] public string Ignored { get; set; }
        }

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
            Assert.IsNotNull(properties);
            Assert.IsFalse(properties.Any(p => p.Name == "Ignored"), 
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
                Assert.IsTrue(propertyNames.Contains(expected), 
                    $"Property {expected} should be included in discovered properties");
            }
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void GetSqlType_MapsAllSupportedTypes()
        {
            // Arrange & Act & Assert
            Assert.AreEqual("VARCHAR(36)", this.mapper.TestGetSqlType(typeof(Guid)));
            Assert.AreEqual("NVARCHAR(MAX)", this.mapper.TestGetSqlType(typeof(string)));
            Assert.AreEqual("INTEGER", this.mapper.TestGetSqlType(typeof(int)));
            Assert.AreEqual("DATETIME", this.mapper.TestGetSqlType(typeof(DateTime)));
            Assert.AreEqual("DECIMAL(18,6)", this.mapper.TestGetSqlType(typeof(decimal)));
            Assert.AreEqual("BIT", this.mapper.TestGetSqlType(typeof(bool)));
            Assert.AreEqual("BIGINT", this.mapper.TestGetSqlType(typeof(long)));
            Assert.AreEqual("REAL", this.mapper.TestGetSqlType(typeof(float)));
            Assert.AreEqual("FLOAT", this.mapper.TestGetSqlType(typeof(double)));
            Assert.AreEqual("VARBINARY(MAX)", this.mapper.TestGetSqlType(typeof(byte[])));
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void GetSqlType_HandlesNullableTypes()
        {
            // Arrange & Act & Assert
            Assert.AreEqual("INTEGER", this.mapper.TestGetSqlType(typeof(int?)));
            Assert.AreEqual("DECIMAL(18,6)", this.mapper.TestGetSqlType(typeof(decimal?)));
            Assert.AreEqual("DATETIME", this.mapper.TestGetSqlType(typeof(DateTime?)));
            Assert.AreEqual("BIT", this.mapper.TestGetSqlType(typeof(bool?)));
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void GenerateColumnName_ConvertsPascalToSnakeCase()
        {
            // Arrange & Act & Assert
            Assert.AreEqual("id", this.mapper.TestGenerateColumnName("Id"));
            Assert.AreEqual("name", this.mapper.TestGenerateColumnName("Name"));
            Assert.AreEqual("created_date", this.mapper.TestGenerateColumnName("CreatedDate"));
            Assert.AreEqual("last_write_time", this.mapper.TestGenerateColumnName("LastWriteTime"));
            Assert.AreEqual("my_long_property_name", this.mapper.TestGenerateColumnName("MyLongPropertyName"));
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void GenerateCreateTableSql_WithSoftDelete()
        {
            // Arrange
            var mapperWithSoftDelete = new TestEntityMapperWithSoftDelete();

            // Act
            var sql = mapperWithSoftDelete.TestGenerateCreateTableSql();

            // Assert
            Assert.IsNotNull(sql);
            Assert.IsTrue(sql.Contains("CREATE TABLE IF NOT EXISTS"), "Should have CREATE TABLE statement");
            Assert.IsTrue(sql.Contains("TestEntity"), "Should reference correct table name");
            Assert.IsTrue(sql.Contains("Version"), "Should include Version column for soft delete");
            Assert.IsTrue(sql.Contains("IsDeleted"), "Should include IsDeleted column for soft delete");
            Assert.IsTrue(sql.Contains("PRIMARY KEY"), "Should define primary key");
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
            Assert.IsNotNull(sql);
            Assert.IsTrue(sql.Contains("AbsoluteExpiration"), "Should include AbsoluteExpiration column");
            Assert.IsTrue(sql.Contains("DATETIME"), "AbsoluteExpiration should be DATETIME type");
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void GenerateCreateIndexSql_CreatesRequiredIndexes()
        {
            // Arrange & Act
            var indexSql = this.mapper.TestGenerateCreateIndexSql();

            // Assert
            Assert.IsNotNull(indexSql);
            Assert.IsTrue(indexSql.Count > 0, "Should generate at least one index");
            
            // Check for primary key index
            Assert.IsTrue(indexSql.Any(sql => sql.Contains("idx_TestEntity_Id")), 
                "Should create index on primary key");
        }

        // Test helper mapper class for accessing protected methods
        private class TestEntityMapper : BaseEntityMapper<TestEntity, Guid>
        {
            public PropertyInfo[] GetProperties() => this.GetPropertyMappings().Keys.ToArray();
            
            public string TestGetSqlType(Type type) => type.ToSqlTypeString();
            
            public string TestGenerateColumnName(string propertyName) => this.GetPropertyMappings().First(p => p.Key.Name ==propertyName).Value.ColumnName;
            
            public string TestGenerateCreateTableSql() => this.GenerateCreateTableSql();
            
            public List<string> TestGenerateCreateIndexSql() => this.GenerateCreateIndexSql().ToList();
        }

        [Table("TestEntity", SoftDeleteEnabled = true)]
        private class TestEntityWithSoftDelete : TestEntity, IVersionedEntity<Guid>
        {
            public bool IsDeleted { get; set; }
        }

        private class TestEntityMapperWithSoftDelete : BaseEntityMapper<TestEntityWithSoftDelete, Guid>
        {
            public string TestGenerateCreateTableSql() => this.GenerateCreateTableSql();
        }

        [Table("TestEntity", ExpirySpanString = "01:00:00")]
        private class TestEntityWithExpiry : TestEntity, IExpirableEntity<Guid>
        {
            public DateTimeOffset? AbsoluteExpiration { get; set; }
        }

        private class TestEntityMapperWithExpiry : BaseEntityMapper<TestEntityWithExpiry, Guid>
        {
            public string TestGenerateCreateTableSql() => this.GenerateCreateTableSql();
        }
    }
}