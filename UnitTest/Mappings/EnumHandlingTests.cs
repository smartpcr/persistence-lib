// -----------------------------------------------------------------------
// <copyright file="EnumHandlingTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Mappings
{
    using System;
    using System.Data;
    using System.Data.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using FluentAssertions;

    [TestClass]
    public class EnumHandlingTests
    {
        private BaseEntityMapper<ComplexEntity, Guid> mapper;

        [TestInitialize]
        public void Setup()
        {
            this.mapper = new BaseEntityMapper<ComplexEntity, Guid>();
        }

        [TestMethod]
        [TestCategory("EnumHandling")]
        public void ConvertParameterValue_WithEnum_ReturnsString()
        {
            // Arrange
            var entity = new ComplexEntity
            {
                Id = Guid.NewGuid(),
                EnumField = TestEnum.Value2
            };

            // Act - using reflection to test protected method
            var convertMethod = typeof(BaseEntityMapper<ComplexEntity, Guid>)
                .GetMethod("ConvertParameterValue", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = convertMethod.Invoke(this.mapper, new object[] { TestEnum.Value2 });

            // Assert
            result.Should().BeOfType<string>();
            result.Should().Be("Value2");
        }

        [TestMethod]
        [TestCategory("EnumHandling")]
        public void MapFromReader_WithStringEnum_ConvertsToEnum()
        {
            // Arrange
            var dataTable = new DataTable();
            dataTable.Columns.Add("Id", typeof(string));
            dataTable.Columns.Add("Version", typeof(long));
            dataTable.Columns.Add("StringField", typeof(string));
            dataTable.Columns.Add("IntField", typeof(int));
            dataTable.Columns.Add("DecimalField", typeof(decimal));
            dataTable.Columns.Add("DateTimeField", typeof(DateTime));
            dataTable.Columns.Add("DateTimeOffsetField", typeof(DateTimeOffset));
            dataTable.Columns.Add("BoolField", typeof(bool));
            dataTable.Columns.Add("GuidField", typeof(string));
            dataTable.Columns.Add("ByteArrayField", typeof(byte[]));
            dataTable.Columns.Add("NullableIntField", typeof(int));
            dataTable.Columns.Add("EnumField", typeof(string)); // Enum stored as string
            dataTable.Columns.Add("IsDeleted", typeof(bool));

            var row = dataTable.NewRow();
            var testGuid = Guid.NewGuid();
            row["Id"] = testGuid.ToString();
            row["Version"] = 1L;
            row["StringField"] = "Test";
            row["IntField"] = 42;
            row["DecimalField"] = 123.45M;
            row["DateTimeField"] = DateTime.Now;
            row["DateTimeOffsetField"] = DateTimeOffset.Now;
            row["BoolField"] = true;
            row["GuidField"] = Guid.NewGuid().ToString();
            row["ByteArrayField"] = new byte[] { 1, 2, 3 };
            row["NullableIntField"] = DBNull.Value;
            row["EnumField"] = "Value3"; // String value of enum
            row["IsDeleted"] = false;
            dataTable.Rows.Add(row);

            using (var reader = dataTable.CreateDataReader())
            {
                reader.Read();

                // Act
                var entity = this.mapper.MapFromReader(reader);

                // Assert
                entity.Should().NotBeNull();
                entity.EnumField.Should().Be(TestEnum.Value3);
            }
        }

        [TestMethod]
        [TestCategory("EnumHandling")]
        public void MapFromReader_WithIntegerEnum_ConvertsToEnum()
        {
            // Arrange - Test backward compatibility with integer values
            var dataTable = new DataTable();
            dataTable.Columns.Add("Id", typeof(string));
            dataTable.Columns.Add("Version", typeof(long));
            dataTable.Columns.Add("StringField", typeof(string));
            dataTable.Columns.Add("IntField", typeof(int));
            dataTable.Columns.Add("DecimalField", typeof(decimal));
            dataTable.Columns.Add("DateTimeField", typeof(DateTime));
            dataTable.Columns.Add("DateTimeOffsetField", typeof(DateTimeOffset));
            dataTable.Columns.Add("BoolField", typeof(bool));
            dataTable.Columns.Add("GuidField", typeof(string));
            dataTable.Columns.Add("ByteArrayField", typeof(byte[]));
            dataTable.Columns.Add("NullableIntField", typeof(int));
            dataTable.Columns.Add("EnumField", typeof(int)); // Enum stored as integer (backward compatibility)
            dataTable.Columns.Add("IsDeleted", typeof(bool));

            var row = dataTable.NewRow();
            var testGuid = Guid.NewGuid();
            row["Id"] = testGuid.ToString();
            row["Version"] = 1L;
            row["StringField"] = "Test";
            row["IntField"] = 42;
            row["DecimalField"] = 123.45M;
            row["DateTimeField"] = DateTime.Now;
            row["DateTimeOffsetField"] = DateTimeOffset.Now;
            row["BoolField"] = true;
            row["GuidField"] = Guid.NewGuid().ToString();
            row["ByteArrayField"] = new byte[] { 1, 2, 3 };
            row["NullableIntField"] = DBNull.Value;
            row["EnumField"] = 2; // Integer value of TestEnum.Value2
            row["IsDeleted"] = false;
            dataTable.Rows.Add(row);

            using (var reader = dataTable.CreateDataReader())
            {
                reader.Read();

                // Act
                var entity = this.mapper.MapFromReader(reader);

                // Assert
                entity.Should().NotBeNull();
                entity.EnumField.Should().Be(TestEnum.Value2);
            }
        }

        [TestMethod]
        [TestCategory("EnumHandling")]
        public void FormatDefaultValue_WithEnum_ReturnsQuotedString()
        {
            // Arrange - Using reflection to test protected method
            var formatMethod = typeof(BaseEntityMapper<ComplexEntity, Guid>)
                .GetMethod("FormatDefaultValue", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = formatMethod.Invoke(this.mapper, new object[] { TestEnum.Value1, SqlDbType.Text });

            // Assert
            result.Should().Be("'Value1'");
        }
    }
}