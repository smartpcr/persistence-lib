// -----------------------------------------------------------------------
// <copyright file="TypeExtensionsTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Extensions
{
    using System;
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Extensions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TypeExtensionsTests
    {
        public enum TestEnum
        {
            Value1 = 1,
            Value2 = 2,
            Value3 = 3
        }

        public class ComplexType
        {
            public string Name { get; set; }
            public int Value { get; set; }
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_StringType_ReturnsNVarChar()
        {
            // Act
            var result = typeof(string).ToSqlDbType();

            // Assert
            Assert.AreEqual(SqlDbType.NVarChar, result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_IntegerTypes_ReturnsCorrectSqlTypes()
        {
            // Act & Assert
            Assert.AreEqual(SqlDbType.Int, typeof(int).ToSqlDbType());
            Assert.AreEqual(SqlDbType.BigInt, typeof(long).ToSqlDbType());
            Assert.AreEqual(SqlDbType.SmallInt, typeof(short).ToSqlDbType());
            Assert.AreEqual(SqlDbType.TinyInt, typeof(byte).ToSqlDbType());
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_BooleanType_ReturnsBit()
        {
            // Act
            var result = typeof(bool).ToSqlDbType();

            // Assert
            Assert.AreEqual(SqlDbType.Bit, result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_DecimalTypes_ReturnsCorrectSqlTypes()
        {
            // Act & Assert
            Assert.AreEqual(SqlDbType.Decimal, typeof(decimal).ToSqlDbType());
            Assert.AreEqual(SqlDbType.Float, typeof(double).ToSqlDbType());
            Assert.AreEqual(SqlDbType.Real, typeof(float).ToSqlDbType());
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_DateTimeTypes_ReturnsCorrectSqlTypes()
        {
            // Act & Assert
            Assert.AreEqual(SqlDbType.DateTime2, typeof(DateTime).ToSqlDbType());
            Assert.AreEqual(SqlDbType.DateTimeOffset, typeof(DateTimeOffset).ToSqlDbType());
            Assert.AreEqual(SqlDbType.Time, typeof(TimeSpan).ToSqlDbType());
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_BinaryType_ReturnsVarBinary()
        {
            // Act
            var result = typeof(byte[]).ToSqlDbType();

            // Assert
            Assert.AreEqual(SqlDbType.VarBinary, result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_GuidType_ReturnsUniqueIdentifier()
        {
            // Act
            var result = typeof(Guid).ToSqlDbType();

            // Assert
            Assert.AreEqual(SqlDbType.UniqueIdentifier, result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_EnumType_ReturnsInt()
        {
            // Act
            var result = typeof(TestEnum).ToSqlDbType();

            // Assert
            Assert.AreEqual(SqlDbType.Int, result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_NullableTypes_ReturnsCorrectSqlTypes()
        {
            // Act & Assert
            Assert.AreEqual(SqlDbType.Int, typeof(int?).ToSqlDbType());
            Assert.AreEqual(SqlDbType.DateTime2, typeof(DateTime?).ToSqlDbType());
            Assert.AreEqual(SqlDbType.Decimal, typeof(decimal?).ToSqlDbType());
            Assert.AreEqual(SqlDbType.Bit, typeof(bool?).ToSqlDbType());
            Assert.AreEqual(SqlDbType.UniqueIdentifier, typeof(Guid?).ToSqlDbType());
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_CharTypes_ReturnsCorrectSqlTypes()
        {
            // Act & Assert
            Assert.AreEqual(SqlDbType.NChar, typeof(char).ToSqlDbType());
            Assert.AreEqual(SqlDbType.NVarChar, typeof(char[]).ToSqlDbType());
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_ComplexType_ReturnsNVarChar()
        {
            // Act
            var result = typeof(ComplexType).ToSqlDbType();

            // Assert
            Assert.AreEqual(SqlDbType.NVarChar, result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_WithMetadata_StringType_ReturnsSizeInfo()
        {
            // Act
            var sqlType = typeof(string).ToSqlDbType(out var size, out var precision, out var scale);

            // Assert
            Assert.AreEqual(SqlDbType.NVarChar, sqlType);
            Assert.AreEqual(255, size);
            Assert.IsNull(precision);
            Assert.IsNull(scale);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_WithMetadata_DecimalType_ReturnsPrecisionScale()
        {
            // Act
            var sqlType = typeof(decimal).ToSqlDbType(out var size, out var precision, out var scale);

            // Assert
            Assert.AreEqual(SqlDbType.Decimal, sqlType);
            Assert.IsNull(size);
            Assert.AreEqual(18, precision);
            Assert.AreEqual(2, scale);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_WithMetadata_ByteArrayType_ReturnsMaxSize()
        {
            // Act
            var sqlType = typeof(byte[]).ToSqlDbType(out var size, out var precision, out var scale);

            // Assert
            Assert.AreEqual(SqlDbType.VarBinary, sqlType);
            Assert.AreEqual(-1, size); // MAX
            Assert.IsNull(precision);
            Assert.IsNull(scale);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlTypeString_BasicTypes_ReturnsCorrectSqlStrings()
        {
            // Act & Assert
            Assert.AreEqual("INT", typeof(int).ToSqlTypeString());
            Assert.AreEqual("BIGINT", typeof(long).ToSqlTypeString());
            Assert.AreEqual("BIT", typeof(bool).ToSqlTypeString());
            Assert.AreEqual("UNIQUEIDENTIFIER", typeof(Guid).ToSqlTypeString());
            Assert.AreEqual("DATETIME2", typeof(DateTime).ToSqlTypeString());
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlTypeString_StringType_ReturnsNVarCharWithSize()
        {
            // Act
            var result = typeof(string).ToSqlTypeString();

            // Assert
            Assert.AreEqual("NVARCHAR(255)", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlTypeString_DecimalType_ReturnsDecimalWithPrecisionScale()
        {
            // Act
            var result = typeof(decimal).ToSqlTypeString();

            // Assert
            Assert.AreEqual("DECIMAL(18,2)", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlTypeString_ByteArrayType_ReturnsVarBinaryMax()
        {
            // Act
            var result = typeof(byte[]).ToSqlTypeString();

            // Assert
            Assert.AreEqual("VARBINARY(MAX)", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlTypeString_ComplexType_ReturnsNVarCharMax()
        {
            // Act
            var result = typeof(ComplexType).ToSqlTypeString();

            // Assert
            Assert.AreEqual("NVARCHAR(MAX)", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void IsNullable_NullableTypes_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(typeof(int?).IsNullable());
            Assert.IsTrue(typeof(DateTime?).IsNullable());
            Assert.IsTrue(typeof(Guid?).IsNullable());
            Assert.IsTrue(typeof(string).IsNullable()); // Reference types are nullable
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void IsNullable_NonNullableValueTypes_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(typeof(int).IsNullable());
            Assert.IsFalse(typeof(DateTime).IsNullable());
            Assert.IsFalse(typeof(Guid).IsNullable());
            Assert.IsFalse(typeof(bool).IsNullable());
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void GetUnderlyingTypeOrSelf_NullableType_ReturnsUnderlyingType()
        {
            // Act
            var result = typeof(int?).GetUnderlyingTypeOrSelf();

            // Assert
            Assert.AreEqual(typeof(int), result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void GetUnderlyingTypeOrSelf_NonNullableType_ReturnsSameType()
        {
            // Act
            var result = typeof(string).GetUnderlyingTypeOrSelf();

            // Assert
            Assert.AreEqual(typeof(string), result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void RequiresSize_SizeRequiringTypes_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(SqlDbType.NVarChar.RequiresSize());
            Assert.IsTrue(SqlDbType.VarChar.RequiresSize());
            Assert.IsTrue(SqlDbType.VarBinary.RequiresSize());
            Assert.IsTrue(SqlDbType.Char.RequiresSize());
            Assert.IsTrue(SqlDbType.NChar.RequiresSize());
            Assert.IsTrue(SqlDbType.Binary.RequiresSize());
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void RequiresSize_NonSizeRequiringTypes_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(SqlDbType.Int.RequiresSize());
            Assert.IsFalse(SqlDbType.DateTime2.RequiresSize());
            Assert.IsFalse(SqlDbType.Bit.RequiresSize());
            Assert.IsFalse(SqlDbType.UniqueIdentifier.RequiresSize());
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void RequiresPrecisionScale_PrecisionScaleTypes_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(SqlDbType.Decimal.RequiresPrecisionScale());
            Assert.IsTrue(SqlDbType.Money.RequiresPrecisionScale());
            Assert.IsTrue(SqlDbType.SmallMoney.RequiresPrecisionScale());
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void RequiresPrecisionScale_NonPrecisionScaleTypes_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(SqlDbType.Int.RequiresPrecisionScale());
            Assert.IsFalse(SqlDbType.Float.RequiresPrecisionScale());
            Assert.IsFalse(SqlDbType.Real.RequiresPrecisionScale());
            Assert.IsFalse(SqlDbType.NVarChar.RequiresPrecisionScale());
        }

        [TestMethod]
        [TestCategory("Extensions")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ToSqlDbType_NullType_ThrowsArgumentNullException()
        {
            // Act
            Type nullType = null;
            nullType.ToSqlDbType();
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_SimpleColumn_ReturnsCorrectDefinition()
        {
            // Act
            var result = typeof(string).ToSqlColumnDefinition("Name");

            // Assert
            Assert.AreEqual("[Name] NVARCHAR(255) NULL", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_NotNullColumn_ReturnsNotNull()
        {
            // Act
            var result = typeof(int).ToSqlColumnDefinition("Id", isNullable: false);

            // Assert
            Assert.AreEqual("[Id] INT NOT NULL", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_PrimaryKeyColumn_ReturnsPrimaryKey()
        {
            // Act
            var result = typeof(Guid).ToSqlColumnDefinition("Id", isPrimaryKey: true);

            // Assert
            Assert.AreEqual("[Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_IdentityColumn_ReturnsIdentity()
        {
            // Act
            var result = typeof(int).ToSqlColumnDefinition("Id", isIdentity: true, isPrimaryKey: true);

            // Assert
            Assert.AreEqual("[Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_WithDefaultValue_ReturnsDefault()
        {
            // Act
            var result = typeof(bool).ToSqlColumnDefinition("IsActive", defaultValue: true);

            // Assert
            Assert.AreEqual("[IsActive] BIT NOT NULL DEFAULT 1", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_WithStringDefault_ReturnsQuotedDefault()
        {
            // Act
            var result = typeof(string).ToSqlColumnDefinition("Status", defaultValue: "Active");

            // Assert
            Assert.AreEqual("[Status] NVARCHAR(255) NULL DEFAULT N'Active'", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_WithDateTimeDefault_ReturnsFormattedDefault()
        {
            // Act
            var testDate = new DateTime(2024, 1, 15, 10, 30, 45, 123);
            var result = typeof(DateTime).ToSqlColumnDefinition("CreatedDate", defaultValue: testDate);

            // Assert
            Assert.AreEqual("[CreatedDate] DATETIME2 NOT NULL DEFAULT '2024-01-15 10:30:45.123'", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_WithGetDateDefault_ReturnsGetDate()
        {
            // Act
            var result = typeof(DateTime).ToSqlColumnDefinition("CreatedTime", defaultValue: "GETDATE()");

            // Assert
            Assert.AreEqual("[CreatedTime] DATETIME2 NOT NULL DEFAULT GETDATE()", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_WithCheckConstraint_ReturnsCheck()
        {
            // Act
            var result = typeof(int).ToSqlColumnDefinition("Age", checkConstraint: "Age >= 0 AND Age <= 150");

            // Assert
            Assert.AreEqual("[Age] INT NOT NULL CHECK (Age >= 0 AND Age <= 150)", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_DecimalColumn_ReturnsWithPrecisionScale()
        {
            // Act
            var result = typeof(decimal).ToSqlColumnDefinition("Price", isNullable: false);

            // Assert
            Assert.AreEqual("[Price] DECIMAL(18,2) NOT NULL", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_NullableType_ReturnsNullable()
        {
            // Act
            var result = typeof(int?).ToSqlColumnDefinition("OptionalValue");

            // Assert
            Assert.AreEqual("[OptionalValue] INT NULL", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_CompleteColumn_ReturnsFullDefinition()
        {
            // Act
            var result = typeof(string).ToSqlColumnDefinition(
                "Email",
                isNullable: false,
                defaultValue: "",
                checkConstraint: "Email LIKE '%@%'");

            // Assert
            Assert.AreEqual("[Email] NVARCHAR(255) NOT NULL DEFAULT N'' CHECK (Email LIKE '%@%')", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_GuidWithNewIdDefault_ReturnsNewId()
        {
            // Act
            var result = typeof(Guid).ToSqlColumnDefinition("Id", defaultValue: "NEWID()");

            // Assert
            Assert.AreEqual("[Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_ByteArrayColumn_ReturnsVarBinaryMax()
        {
            // Act
            var result = typeof(byte[]).ToSqlColumnDefinition("Data");

            // Assert
            Assert.AreEqual("[Data] VARBINARY(MAX) NULL", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_EnumColumn_ReturnsInt()
        {
            // Act
            var result = typeof(TestEnum).ToSqlColumnDefinition("Status", defaultValue: TestEnum.Value1);

            // Assert
            Assert.AreEqual("[Status] INT NOT NULL DEFAULT 1", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        [ExpectedException(typeof(ArgumentException))]
        public void ToSqlColumnDefinition_EmptyColumnName_ThrowsArgumentException()
        {
            // Act
            typeof(int).ToSqlColumnDefinition("");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        [ExpectedException(typeof(ArgumentException))]
        public void ToSqlColumnDefinition_NullColumnName_ThrowsArgumentException()
        {
            // Act
            typeof(int).ToSqlColumnDefinition(null);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_StringWithApostrophe_EscapesCorrectly()
        {
            // Act
            var result = typeof(string).ToSqlColumnDefinition("Description", defaultValue: "It's a test");

            // Assert
            Assert.AreEqual("[Description] NVARCHAR(255) NULL DEFAULT N'It''s a test'", result);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_DateTimeOffsetColumn_ReturnsDateTimeOffset()
        {
            // Act
            var result = typeof(DateTimeOffset).ToSqlColumnDefinition("EventTime");

            // Assert
            Assert.AreEqual("[EventTime] DATETIMEOFFSET NOT NULL", result);
        }
    }
}