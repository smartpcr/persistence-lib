//-------------------------------------------------------------------------------
// <copyright file="BaseEntityMapperTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Mappings
{
    using System;
    using System.Data;
    using System.Linq;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="BaseEntityMapper{T,TKey}"/>.
    /// </summary>
    [TestClass]
    public class BaseEntityMapperTests
    {
        // Test entities have been moved to the Entities folder

        #region Table Information Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void Constructor_WithTableAttribute_ExtractsTableName()
        {
            var mapper = new BaseEntityMapper<SimpleEntity, string>();

            mapper.GetTableName().Should().Be("TestEntity");
            mapper.GetFullTableName().Should().Be("TestEntity");
            mapper.TableName.Should().Be("TestEntity");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void Constructor_WithoutTableAttribute_ThrowInvalidOperationException()
        {
            Action act = () => new BaseEntityMapper<EntityWithoutTableAttribute, string>();

            act.Should().Throw<InvalidOperationException>()
                .WithMessage($"Entity type EntityWithoutTableAttribute must have [Table] attribute at class declaration");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void Constructor_WithSoftDeleteEnabled_SetsPropertyCorrectly()
        {
            var mapper = new BaseEntityMapper<SoftDeleteEntity, string>();
            mapper.EnableSoftDelete.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void Constructor_WithSoftDeleteDisabled_SetsPropertyCorrectly()
        {
            var mapper = new BaseEntityMapper<NoSoftDeleteEntity, string>();
            mapper.EnableSoftDelete.Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GetFullTableName_WithSchema_ReturnsSchemaQualifiedName()
        {
            var mapper = new BaseEntityMapper<SimpleEntity, string>();
            mapper.SchemaName = "dbo";
            var fullName = mapper.GetFullTableName();
            fullName.Should().Be("dbo.TestEntity");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GetFullTableName_WithoutSchema_ReturnsTableNameOnly()
        {
            var mapper = new BaseEntityMapper<SimpleEntity, string>();

            var fullName = mapper.GetFullTableName();
            fullName.Should().Be("TestEntity");
        }

        #endregion

        #region Property Mapping Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GetPropertyMappings_ReturnsAllMappedProperties()
        {
            var mapper = new BaseEntityMapper<SimpleEntity, string>();

            var mappings = mapper.GetPropertyMappings();

            mappings.Count.Should().BeGreaterThan(0);
            mappings.Any(m => m.Value.PropertyName == "Name").Should().BeTrue();
            mappings.Any(m => m.Value.PropertyName == "Age").Should().BeTrue();
            mappings.Any(m => m.Value.PropertyName == "Id").Should().BeTrue();
            mappings.Any(m => m.Value.PropertyName == "Version").Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GetPropertyMappings_ExcludesNotMappedProperties()
        {
            var mapper = new BaseEntityMapper<NotMappedEntity, string>();

            var mappings = mapper.GetPropertyMappings();

            mappings.Any(m => m.Value.PropertyName == "MappedProperty").Should().BeTrue();
            mappings.Any(m => m.Value.PropertyName == "NotMappedProperty").Should().BeFalse();
            mappings.Any(m => m.Value.PropertyName == "ComplexProperty").Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GetPrimaryKeyColumns_WithSingleKey_ReturnsCorrectColumns()
        {
            // Arrange
            var mapper = new BaseEntityMapper<SimpleEntity, string>();

            // Act
            var pkColumns = mapper.GetPrimaryKeyColumns();

            // Assert
            pkColumns.Count.Should().Be(1);
            pkColumns[0].Should().Be("CacheKey");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GetPrimaryKeyColumns_WithSoftDelete_ReturnsCompositeKey()
        {
            // Arrange
            var mapper = new BaseEntityMapper<SoftDeleteEntity, string>();

            // Act
            var pkColumns = mapper.GetPrimaryKeyColumns();

            // Assert
            pkColumns.Count.Should().Be(2);
            pkColumns.Should().Contain("CacheKey");
            pkColumns.Should().Contain("Version");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GetPrimaryKeyColumns_WithCompositeKey_ReturnsAllKeyColumns()
        {
            // Arrange
            var mapper = new BaseEntityMapper<CompositeKeyEntity, string>();

            // Act
            var pkColumns = mapper.GetPrimaryKeyColumns();

            // Assert
            pkColumns.Count.Should().Be(2);
            pkColumns.Should().Contain("Key1");
            pkColumns.Should().Contain("Key2");
        }

        #endregion

        #region SQL Generation Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateCreateTableSql_SimpleEntity_GeneratesCorrectSql()
        {
            // Arrange
            var mapper = new BaseEntityMapper<SimpleEntity, string>();

            // Act
            var sql = mapper.GenerateCreateTableSql();

            // Assert
            sql.Should().Contain("CREATE TABLE IF NOT EXISTS TestEntity");
            sql.Should().Contain("CacheKey TEXT NOT NULL");
            sql.Should().Contain("Name TEXT");
            sql.Should().Contain("Age INTEGER");
            sql.Should().Contain("Version INTEGER NOT NULL");
            sql.Should().Contain("PRIMARY KEY (CacheKey)");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateCreateTableSql_WithSoftDelete_IncludesCompositeKey()
        {
            // Arrange
            var mapper = new BaseEntityMapper<SoftDeleteEntity, string>();

            // Act
            var sql = mapper.GenerateCreateTableSql();

            // Assert
            sql.Should().Contain("PRIMARY KEY (CacheKey, Version)");
            sql.Should().Contain("IsDeleted INTEGER NOT NULL DEFAULT 0");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateCreateTableSql_WithIndexes_GeneratesIndexStatements()
        {
            // Arrange
            var mapper = new BaseEntityMapper<IndexedEntity, string>();

            // Act
            var sql = mapper.GenerateCreateTableSql();
            var indexSql = mapper.GenerateCreateIndexSql().ToList();

            // Assert
            sql.Should().Contain("CREATE TABLE IF NOT EXISTS TestEntityWithIndexes");
            sql.Should().Contain("Email TEXT");
            sql.Should().Contain("Category TEXT");
            sql.Should().Contain("DateCreated TEXT");
            sql.Should().Contain("PRIMARY KEY (CacheKey)");

            indexSql.Should().HaveCountGreaterThanOrEqualTo(2);
            indexSql.Any(i => i.Contains("CREATE UNIQUE INDEX IF NOT EXISTS IX_Email")).Should().BeTrue();
            indexSql.Any(i => i.Contains("CREATE INDEX IF NOT EXISTS IX_CategoryDate")).Should().BeTrue();
            indexSql.Any(i => i.Contains("(Category, DateCreated)")).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateCreateTableSql_WithForeignKey_GeneratesForeignKeyConstraint()
        {
            // Arrange
            var mapper = new BaseEntityMapper<ForeignKeyEntity, string>();

            // Act
            var sql = mapper.GenerateCreateTableSql();

            // Assert
            sql.Should().Contain("FOREIGN KEY (ParentId) REFERENCES TestEntity(Id)");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateCreateTableSql_WithConstraints_GeneratesConstraints()
        {
            // Arrange
            var mapper = new BaseEntityMapper<ConstraintEntity, string>();

            // Act
            var sql = mapper.GenerateCreateTableSql();

            // Assert
            sql.Should().Contain("CONSTRAINT CK_Age_Range CHECK (Age >= 0 AND Age <= 150)");
            sql.Should().Contain("Email TEXT UNIQUE");
            sql.Should().Contain("Status TEXT DEFAULT 'Active'");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateCreateTableSql_WithComputedColumn_GeneratesComputedExpression()
        {
            // Arrange
            var mapper = new BaseEntityMapper<ComputedColumnEntity, string>();

            // Act
            var sql = mapper.GenerateCreateTableSql();

            // Assert
            sql.Should().Contain("FullName TEXT AS (FirstName || ' ' || LastName)");
        }

        #endregion

        #region CRUD SQL Generation Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateInsertSql_SimpleEntity_GeneratesCorrectSql()
        {
            // Arrange
            var mapper = new BaseEntityMapper<SimpleEntity, string>();

            // Act
            var sql = mapper.GenerateInsertSql();

            // Assert
            sql.Should().StartWith("INSERT INTO TestEntity");

            // Verify all expected columns are present in any order
            var expectedColumns = new[] { "CacheKey", "Version", "Name", "Age", "CreatedTime", "LastWriteTime" };

            // Extract the columns part and values part more reliably
            var columnsStartIndex = sql.IndexOf('(');
            var columnsEndIndex = sql.IndexOf(") VALUES", StringComparison.Ordinal);
            var valuesStartIndex = sql.IndexOf("VALUES (", StringComparison.Ordinal) + "VALUES (".Length;
            var valuesEndIndex = sql.LastIndexOf(')');

            columnsStartIndex.Should().BeGreaterOrEqualTo(0, "SQL should contain opening parenthesis for columns");
            columnsEndIndex.Should().BeGreaterThan(columnsStartIndex, "SQL should contain ') VALUES'");
            valuesStartIndex.Should().BeGreaterThan(columnsEndIndex, "SQL should contain 'VALUES ('");
            valuesEndIndex.Should().BeGreaterThan(valuesStartIndex, "SQL should contain closing parenthesis for values");

            var columnsPart = sql.Substring(columnsStartIndex + 1, columnsEndIndex - columnsStartIndex - 1);
            var valuesPart = sql.Substring(valuesStartIndex, valuesEndIndex - valuesStartIndex);

            // Parse actual columns and parameters
            var actualColumns = columnsPart.Split(',').Select(c => c.Trim()).ToArray();
            var actualParameters = valuesPart.Split(',').Select(p => p.Trim().TrimStart('@')).ToArray();

            // Verify each expected column and its corresponding parameter
            foreach (var expectedColumn in expectedColumns)
            {
                actualColumns.Should().Contain(expectedColumn,
                    $"Column '{expectedColumn}' should be present in the columns list");
                actualParameters.Should().Contain(expectedColumn,
                    $"Parameter '{expectedColumn}' should be present in the parameters list");
            }

            // Verify counts match
            actualColumns.Length.Should().Be(expectedColumns.Length,
                "Number of columns should match expected count");
            actualParameters.Length.Should().Be(expectedColumns.Length,
                "Number of parameters should match expected count");

            // Verify column and parameter order match (columns and parameters should be in same order)
            for (var i = 0; i < actualColumns.Length; i++)
            {
                actualParameters[i].Should().Be(actualColumns[i],
                    $"Parameter at position {i} should match column at same position");
            }

            // Verify the overall structure
            sql.Should().MatchRegex(@"INSERT INTO TestEntity \([^)]+\) VALUES \([^)]+\)");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateUpdateSql_WithoutSoftDelete_GeneratesUpdateStatement()
        {
            // Arrange
            var mapper = new BaseEntityMapper<NoSoftDeleteEntity, string>();

            // Act
            var sql = mapper.GenerateUpdateSql();

            // Assert
            sql.Should().StartWith("UPDATE TestEntityWithoutSoftDelete SET");
            sql.Should().Contain("Value = @Value");
            sql.Should().Contain("LastWriteTime = @LastWriteTime");
            sql.Should().Contain("Version = @Version + 1");
            sql.Should().Contain("WHERE CacheKey = @CacheKey");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateUpdateSql_WithSoftDelete_GeneratesInsertStatement()
        {
            // Arrange
            var mapper = new BaseEntityMapper<SoftDeleteEntity, string>();

            // Act
            var sql = mapper.GenerateUpdateSql();

            // Assert
            sql.Should().StartWith("INSERT INTO TestEntityWithSoftDelete");
            sql.Should().NotContain("UPDATE");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateDeleteSql_WithoutSoftDelete_GeneratesDeleteStatement()
        {
            // Arrange
            var mapper = new BaseEntityMapper<NoSoftDeleteEntity, string>();

            // Act
            var sql = mapper.GenerateDeleteSql();

            // Assert
            sql.Should().Be("DELETE FROM TestEntityWithoutSoftDelete WHERE CacheKey = @CacheKey");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateDeleteSql_WithSoftDelete_GeneratesUpdateStatement()
        {
            // Arrange
            var mapper = new BaseEntityMapper<SoftDeleteEntity, string>();

            // Act
            var sql = mapper.GenerateDeleteSql();

            // Assert
            sql.Should().StartWith("UPDATE TestEntityWithSoftDelete SET IsDeleted = 1, Version = @NextVersion");
            sql.Should().Contain("WHERE CacheKey = @CacheKey AND Version = @Version");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateSelectByIdSql_SimpleEntity_GeneratesCorrectSql()
        {
            // Arrange
            var mapper = new BaseEntityMapper<SoftDeleteEntity, string>();

            // Act
            var sql = mapper.GenerateSelectByIdSql();

            // Assert
            sql.Should().StartWith("SELECT");
            sql.Should().Contain("FROM TestEntity");
            sql.Should().Contain("WHERE CacheKey = @CacheKey");
            sql.Should().Contain("IsDeleted = 0");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateSelectByIdSql_WithSoftDelete_OrdersByVersionDesc()
        {
            // Arrange
            var mapper = new BaseEntityMapper<SoftDeleteEntity, string>();

            // Act
            var sql = mapper.GenerateSelectByIdSql();

            // Assert
            sql.Should().Contain("SELECT Version, Description, IsDeleted, CacheKey, CreatedTime, LastWriteTime FROM TestEntityWithSoftDelete");
            sql.Should().Contain("SELECT MAX(Version) FROM TestEntityWithSoftDelete WHERE CacheKey = @CacheKey");
            sql.Should().Contain("WHERE CacheKey = @CacheKey AND IsDeleted = 0");
        }

        #endregion

        #region Entity to Parameter Mapping Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void MapEntityToParameters_MapsAllProperties()
        {
            // Arrange
            var mapper = new BaseEntityMapper<SimpleEntity, string>();
            var entity = new SimpleEntity
            {
                Id = "123",
                Name = "Test",
                Age = 25,
                Version = 1,
                CreatedTime = DateTimeOffset.Now,
                LastWriteTime = DateTimeOffset.Now
            };

            // Act
            var parameters = mapper.MapEntityToParameters(entity);

            // Assert
            parameters["@CacheKey"].Should().Be("123");
            parameters["@Name"].Should().Be("Test");
            parameters["@Age"].Should().Be(25);
            parameters["@Version"].Should().Be(1L);
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void MapEntityToParameters_HandlesNullValues()
        {
            // Arrange
            var mapper = new BaseEntityMapper<SimpleEntity, string>();
            var entity = new SimpleEntity
            {
                Id = "123",
                Name = null,
                Age = 25
            };

            // Act
            var parameters = mapper.MapEntityToParameters(entity);

            // Assert
            parameters["@Name"].Should().Be(DBNull.Value);
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void MapEntityToParameters_IgnoresNotMappedProperties()
        {
            // Arrange
            var mapper = new BaseEntityMapper<NotMappedEntity, string>();
            var entity = new NotMappedEntity
            {
                Id = "123",
                MappedProperty = "Mapped",
                NotMappedProperty = "NotMapped",
                ComplexProperty = new ComplexObject { SubProperty = "Complex" }
            };

            // Act
            var parameters = mapper.MapEntityToParameters(entity);

            // Assert
            parameters.Should().ContainKey("@MappedProperty");
            parameters.Should().NotContainKey("@NotMappedProperty");
            parameters.Should().NotContainKey("@ComplexProperty");
        }

        #endregion

        #region Type Inference Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void PropertyMapping_InfersSqlTypes_ForCommonTypes()
        {
            // Arrange
            var mapper = new BaseEntityMapper<SimpleEntity, string>();
            var mappings = mapper.GetPropertyMappings();

            // Act & Assert
            var nameMapping = mappings.First(m => m.Value.PropertyName == "Name").Value;
            nameMapping.SqlType.Should().Be(SqlDbType.Text);

            var ageMapping = mappings.First(m => m.Value.PropertyName == "Age").Value;
            ageMapping.SqlType.Should().Be(SqlDbType.Int);
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateIndexSql_WithUniqueIndex_GeneratesUniqueConstraint()
        {
            // Arrange
            var mapper = new BaseEntityMapper<IndexedEntity, string>();

            // Act
            var sqls = mapper.GenerateCreateIndexSql();

            // Assert
            var uniqueIndexSql = sqls.FirstOrDefault(sql => sql.Contains("IX_Email"));
            uniqueIndexSql.Should().NotBeNull();
            uniqueIndexSql.Should().Contain("CREATE UNIQUE INDEX");
        }

        #endregion

        #region Expiry Configuration Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void Constructor_WithExpirySpan_SetsExpiryProperties()
        {
            // Arrange & Act
            var mapper = new BaseEntityMapper<ExpiryEntity, string>();

            // Assert
            mapper.EnableExpiry.Should().BeTrue();
            mapper.ExpirySpan.Should().Be(TimeSpan.FromDays(7));
        }

        #endregion

        #region Exception Handling Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void Constructor_WithoutPrimaryKey_ThrowsException()
        {
            var entityType = typeof(EntityWithoutPrimaryKey);
            var act = () =>
            {
                var mapperType = typeof(BaseEntityMapper<,>).MakeGenericType(entityType, typeof(string));
                Activator.CreateInstance(mapperType);
            };

            act.Should().Throw<System.Reflection.TargetInvocationException>()
                .WithInnerException<InvalidOperationException>()
                .WithMessage("Entity type EntityWithoutPrimaryKey must have at least one property marked with [PrimaryKey]*"); // Optional: validate the inner exception message contains "primary key"
            
        }


        #endregion
    }
}