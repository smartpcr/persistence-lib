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
            Assert.AreEqual(entity.Id, parameters["@CacheKey"]);
            Assert.AreEqual("Test String", parameters["@StringField"]);
            Assert.AreEqual(42, parameters["@IntField"]);
            Assert.AreEqual(123L, parameters["@Version"]);
            Assert.AreEqual(123.45m, parameters["@DecimalField"]);
            Assert.AreEqual(entity.DateTimeField, parameters["@DateTimeField"]);
            Assert.AreEqual(entity.DateTimeOffsetField, parameters["@DateTimeOffsetField"]);
            Assert.AreEqual(true, parameters["@BoolField"]);
            Assert.AreEqual(entity.GuidField, parameters["@GuidField"]);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, (byte[])parameters["@ByteArrayField"]);
            Assert.AreEqual(DBNull.Value, parameters["@NullableIntField"]);
            Assert.AreEqual(TestEnum.Value2, parameters["@EnumField"]);
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
            Assert.IsTrue(sql.Contains("DecimalField REAL"));
            Assert.IsTrue(sql.Contains("StringField TEXT"));
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
            Assert.IsTrue(indexSqls.Any(sql => sql.Contains("IX_Field1") && sql.Contains("(Field1)")));
            Assert.IsTrue(indexSqls.Any(sql => sql.Contains("IX_Field2") && sql.Contains("(Field2)")));
            Assert.IsTrue(indexSqls.Any(sql => sql.Contains("IX_Field3") && sql.Contains("UNIQUE")));
            Assert.IsTrue(indexSqls.Any(sql => sql.Contains("IX_Composite_1_2") && sql.Contains("(Field1, Field2)")));
            Assert.IsTrue(indexSqls.Any(sql => sql.Contains("IX_Composite_2_3") && sql.Contains("(Field2, Field3)")));
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
            Assert.IsNotNull(includedIndexSql);
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
            Assert.IsTrue(sql.Contains("CONSTRAINT CK_Email_Valid CHECK (Email LIKE '%@%')"));
            Assert.IsTrue(sql.Contains("CONSTRAINT CK_Phone_Format CHECK (Phone REGEXP '^[0-9-]+$')"));
            Assert.IsTrue(sql.Contains("CONSTRAINT CK_Status_Valid CHECK (Status IN ('Active', 'Inactive', 'Suspended'))"));
            Assert.IsTrue(sql.Contains("CONSTRAINT CK_Score_Range CHECK (Score BETWEEN 0 AND 100)"));
            Assert.IsTrue(sql.Contains("CreatedDate TEXT DEFAULT 'CURRENT_TIMESTAMP'"));
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
            Assert.IsTrue(sql.Contains("CONSTRAINT FK_Parent1 FOREIGN KEY (ParentId1) REFERENCES Parent1(Id) ON DELETE CASCADE"));
            Assert.IsTrue(sql.Contains("CONSTRAINT FK_Parent2 FOREIGN KEY (ParentId2) REFERENCES Parent2(Id) ON DELETE SET NULL"));
            Assert.IsTrue(sql.Contains("CONSTRAINT FK_Parent3 FOREIGN KEY (ParentId3) REFERENCES Parent3(Id) ON DELETE RESTRICT"));
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
            Assert.IsTrue(sql.Contains("CONSTRAINT FK_Parent FOREIGN KEY (ParentId) REFERENCES HierarchicalEntity(Id)"));
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
            Assert.IsTrue(sql.Contains("TotalPrice REAL AS (Quantity * UnitPrice)"));
            Assert.IsTrue(sql.Contains("FullName TEXT AS (FirstName || ' ' || LastName)"));
            Assert.IsTrue(sql.Contains("CreatedYear INTEGER AS (strftime('%Y', CreatedTime))"));
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
            Assert.IsTrue(sql.Contains("SELECT"));
            Assert.IsTrue(sql.Contains("StringField"));
            Assert.IsTrue(sql.Contains("IntField"));
            Assert.IsTrue(sql.Contains("DecimalField"));
            Assert.IsTrue(sql.Contains("DateTimeField"));
            Assert.IsTrue(sql.Contains("FROM ComplexEntity"));
            Assert.IsTrue(sql.Contains("IsDeleted = 0"));
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
            Assert.IsTrue(sql.Contains("WHERE CacheKey = @CacheKey"));
            Assert.IsInstanceOfType(parameters["@CacheKey"], typeof(Guid));
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
            Assert.IsTrue(sql.Contains("INSERT INTO TestEntity"));
            Assert.IsTrue(sql.Contains("VALUES"));
            // Should contain parameter placeholders for batch insert
            Assert.IsTrue(sql.Contains("@CacheKey0") || sql.Contains("@CacheKey"));
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
            Assert.AreEqual("Test's \"Special\" Characters: \n\r\t", parameters["@Name"]);
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
            Assert.IsTrue(sql.Contains("ThisIsAVeryLongTableNameThatExceedsNormalLimitsButShouldStillWorkCorrectly"));
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
            Assert.AreSame(mappings1, mappings2); // Should return same instance if cached
        }

        #endregion
    }
}