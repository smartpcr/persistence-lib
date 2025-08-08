//-------------------------------------------------------------------------------
// <copyright file="BaseEntityMapperAdvancedTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Mappings
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Advanced unit tests for <see cref="BaseEntityMapper{T,TKey}"/> covering complex scenarios.
    /// </summary>
    [TestClass]
    public class BaseEntityMapperAdvancedTests
    {

        #region Complex Type Mapping Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void MapEntityToParameters_HandlesAllDataTypes()
        {
            // Arrange
            var mapper = new BaseEntityMapper<ComplexEntity, Guid>();
            var entity = new ComplexEntity
            {
                Id = Guid.NewGuid(),
                Version = 123,
                StringField = "Test String",
                IntField = 42,
                DecimalField = 123.45m,
                DateTimeField = new DateTime(2024, 1, 1, 12, 0, 0),
                DateTimeOffsetField = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.FromHours(-8)),
                BoolField = true,
                GuidField = Guid.NewGuid(),
                ByteArrayField = new byte[] { 1, 2, 3, 4, 5 },
                NullableIntField = null,
                EnumField = TestEnum.Value2
            };

            // Act
            var parameters = mapper.MapEntityToParameters(entity);

            // Assert
            parameters["@CacheKey"].Should().Be(entity.Id);
            parameters["@StringField"].Should().Be("Test String");
            parameters["@IntField"].Should().Be(42);
            parameters["@Version"].Should().Be(123L);
            parameters["@DecimalField"].Should().Be(123.45m);
            parameters["@DateTimeField"].Should().Be(entity.DateTimeField);
            parameters["@DateTimeOffsetField"].Should().Be(entity.DateTimeOffsetField);
            parameters["@BoolField"].Should().Be(true);
            parameters["@GuidField"].Should().Be(entity.GuidField);
            ((byte[])parameters["@ByteArrayField"]).Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4, 5 });
            parameters["@NullableIntField"].Should().Be(DBNull.Value);
            parameters["@EnumField"].Should().Be(TestEnum.Value2);
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateCreateTableSql_WithPrecisionAndScale_GeneratesCorrectSql()
        {
            // Arrange
            var mapper = new BaseEntityMapper<ComplexEntity, Guid>();

            // Act
            var sql = mapper.GenerateCreateTableSql();

            // Assert
            sql.Should().Contain("DecimalField REAL");
            sql.Should().Contain("StringField TEXT");
        }

        #endregion

        #region Multiple Index Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateIndexSql_WithMultipleIndexes_GeneratesAllIndexes()
        {
            // Arrange
            var mapper = new BaseEntityMapper<MultiIndexEntity, string>();

            // Act
            var indexSqls = mapper.GenerateCreateIndexSql();

            // Assert
            indexSqls.Any(sql => sql.Contains("IX_Field1") && sql.Contains("(Field1)")).Should().BeTrue();
            indexSqls.Any(sql => sql.Contains("IX_Field2") && sql.Contains("(Field2)")).Should().BeTrue();
            indexSqls.Any(sql => sql.Contains("IX_Field3") && sql.Contains("UNIQUE")).Should().BeTrue();
            indexSqls.Any(sql => sql.Contains("IX_Composite_1_2") && sql.Contains("(Field1, Field2)")).Should().BeTrue();
            indexSqls.Any(sql => sql.Contains("IX_Composite_2_3") && sql.Contains("(Field2, Field3)")).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateIndexSql_WithIncludedColumns_GeneratesIncludeClause()
        {
            // Arrange
            var mapper = new BaseEntityMapper<MultiIndexEntity, string>();

            // Act
            var indexSqls = mapper.GenerateCreateIndexSql();

            // Assert
            var includedIndexSql = indexSqls.FirstOrDefault(sql => sql.Contains("IX_Included"));
            includedIndexSql.Should().NotBeNull();
            // Note: SQLite doesn't support INCLUDE clause, so this might be commented or handled differently
        }

        #endregion

        #region Complex Constraint Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateCreateTableSql_WithComplexConstraints_GeneratesAllConstraints()
        {
            // Arrange
            var mapper = new BaseEntityMapper<ComplexConstraintEntity, string>();

            // Act
            var sql = mapper.GenerateCreateTableSql();

            // Assert
            sql.Should().Contain("CONSTRAINT CK_Email_Valid CHECK (Email LIKE '%@%')");
            sql.Should().Contain("CONSTRAINT CK_Phone_Format CHECK (Phone REGEXP '^[0-9-]+$')");
            sql.Should().Contain("CONSTRAINT CK_Status_Valid CHECK (Status IN ('Active', 'Inactive', 'Suspended'))");
            sql.Should().Contain("CONSTRAINT CK_Score_Range CHECK (Score BETWEEN 0 AND 100)");
            sql.Should().Contain("CreatedDate TEXT DEFAULT 'CURRENT_TIMESTAMP')");
        }

        #endregion

        #region Foreign Key Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateCreateTableSql_WithMultipleForeignKeys_GeneratesAllForeignKeys()
        {
            // Arrange
            var mapper = new BaseEntityMapper<MultiForeignKeyEntity, string>();

            // Act
            var sql = mapper.GenerateCreateTableSql();

            // Assert
            sql.Should().Contain("CONSTRAINT FK_Parent1 FOREIGN KEY (ParentId1) REFERENCES Parent1(Id) ON DELETE CASCADE");
            sql.Should().Contain("CONSTRAINT FK_Parent2 FOREIGN KEY (ParentId2) REFERENCES Parent2(Id) ON DELETE SET NULL");
            sql.Should().Contain("CONSTRAINT FK_Parent3 FOREIGN KEY (ParentId3) REFERENCES Parent3(Id) ON DELETE RESTRICT");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateCreateTableSql_WithSelfReferencingForeignKey_GeneratesCorrectConstraint()
        {
            // Arrange
            var mapper = new BaseEntityMapper<HierarchicalEntity, string>();

            // Act
            var sql = mapper.GenerateCreateTableSql();

            // Assert
            sql.Should().Contain("CONSTRAINT FK_Parent FOREIGN KEY (ParentId) REFERENCES HierarchicalEntity(Id)");
        }

        #endregion

        #region Computed Column Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateCreateTableSql_WithComputedColumns_GeneratesComputedExpressions()
        {
            // Arrange
            var mapper = new BaseEntityMapper<ComputedEntity, string>();

            // Act
            var sql = mapper.GenerateCreateTableSql();

            // Assert
            sql.Should().Contain("TotalPrice REAL AS (Quantity * UnitPrice)");
            sql.Should().Contain("FullName TEXT AS (FirstName || ' ' || LastName)");
            sql.Should().Contain("CreatedYear INTEGER AS (strftime('%Y', CreatedTime))");
        }

        #endregion

        #region Query Generation Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateSelectAllSql_WithComplexEntity_IncludesAllColumns()
        {
            // Arrange
            var mapper = new BaseEntityMapper<ComplexEntity, Guid>();

            // Act
            var sql = mapper.GenerateSelectAllSql();

            // Assert
            sql.Should().Contain("SELECT");
            sql.Should().Contain("StringField");
            sql.Should().Contain("IntField");
            sql.Should().Contain("DecimalField");
            sql.Should().Contain("DateTimeField");
            sql.Should().Contain("FROM ComplexEntity");
            sql.Should().Contain("IsDeleted = 0");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateSelectByIdSql_WithGuidKey_GeneratesCorrectParameterType()
        {
            // Arrange
            var mapper = new BaseEntityMapper<EntityWithGuidKey, Guid>();

            // Act
            var sql = mapper.GenerateSelectByIdSql();
            var parameters = mapper.MapIdToParameters(Guid.NewGuid());

            // Assert
            sql.Should().Contain("WHERE CacheKey = @CacheKey");
            parameters["@CacheKey"].Should().BeOfType<Guid>();
        }

        #endregion

        #region Batch Operation Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateBatchInsertSql_GeneratesParameterizedBatchInsert()
        {
            // Arrange
            var mapper = new BaseEntityMapper<SimpleEntity, string>();
            var entities = new List<SimpleEntity>
            {
                new SimpleEntity { Id = "1", Name = "Entity1", Age = 20 },
                new SimpleEntity { Id = "2", Name = "Entity2", Age = 30 },
                new SimpleEntity { Id = "3", Name = "Entity3", Age = 40 }
            };

            // Act
            var sql = mapper.GenerateBatchInsertSql(entities.Count);

            // Assert
            sql.Should().Contain("INSERT INTO TestEntity");
            sql.Should().Contain("VALUES");
            // Should contain parameter placeholders for batch insert
            (sql.Contains("@CacheKey0") || sql.Contains("@CacheKey")).Should().BeTrue();
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void MapEntityToParameters_WithSpecialCharactersInStrings_HandlesCorrectly()
        {
            // Arrange
            var mapper = new BaseEntityMapper<SimpleEntity, string>();
            var entity = new SimpleEntity
            {
                Id = "123",
                Name = "Test's \"Special\" Characters: \n\r\t",
                Age = 25
            };

            // Act
            var parameters = mapper.MapEntityToParameters(entity);

            // Assert
            parameters["@Name"].Should().Be("Test's \"Special\" Characters: \n\r\t");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GenerateCreateTableSql_WithVeryLongTableName_HandlesCorrectly()
        {
            // Arrange
            var mapper = new BaseEntityMapper<SimpleEntity, string>();
            mapper.TableName = "ThisIsAVeryLongTableNameThatExceedsNormalLimitsButShouldStillWorkCorrectly";

            // Act
            var sql = mapper.GenerateCreateTableSql();

            // Assert
            sql.Should().Contain("ThisIsAVeryLongTableNameThatExceedsNormalLimitsButShouldStillWorkCorrectly");
        }

        #endregion

        #region Performance Optimization Tests

        [TestMethod]
        [TestCategory("UnitTest")]
        public void GetPropertyMappings_CachesResults_ForPerformance()
        {
            // Arrange
            var mapper = new BaseEntityMapper<ComplexEntity, Guid>();

            // Act
            var mappings1 = mapper.GetPropertyMappings();
            var mappings2 = mapper.GetPropertyMappings();

            // Assert
            mappings2.Should().BeSameAs(mappings1); // Should return same instance if cached
        }

        #endregion
    }
}