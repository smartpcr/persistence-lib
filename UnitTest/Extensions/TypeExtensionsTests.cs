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
    using FluentAssertions;
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
            result.Should().Be(SqlDbType.NVarChar);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_IntegerTypes_ReturnsCorrectSqlTypes()
        {
            // Act & Assert
            typeof(int).ToSqlDbType().Should().Be(SqlDbType.Int);
            typeof(long).ToSqlDbType().Should().Be(SqlDbType.BigInt);
            typeof(short).ToSqlDbType().Should().Be(SqlDbType.SmallInt);
            typeof(byte).ToSqlDbType().Should().Be(SqlDbType.TinyInt);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_BooleanType_ReturnsBit()
        {
            // Act
            var result = typeof(bool).ToSqlDbType();

            // Assert
            result.Should().Be(SqlDbType.Bit);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_DecimalTypes_ReturnsCorrectSqlTypes()
        {
            // Act & Assert
            typeof(decimal).ToSqlDbType().Should().Be(SqlDbType.Decimal);
            typeof(double).ToSqlDbType().Should().Be(SqlDbType.Float);
            typeof(float).ToSqlDbType().Should().Be(SqlDbType.Real);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_DateTimeTypes_ReturnsCorrectSqlTypes()
        {
            // Act & Assert
            typeof(DateTime).ToSqlDbType().Should().Be(SqlDbType.DateTime2);
            typeof(DateTimeOffset).ToSqlDbType().Should().Be(SqlDbType.DateTimeOffset);
            typeof(TimeSpan).ToSqlDbType().Should().Be(SqlDbType.Time);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_BinaryType_ReturnsVarBinary()
        {
            // Act
            var result = typeof(byte[]).ToSqlDbType();

            // Assert
            result.Should().Be(SqlDbType.VarBinary);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_GuidType_ReturnsUniqueIdentifier()
        {
            // Act
            var result = typeof(Guid).ToSqlDbType();

            // Assert
            result.Should().Be(SqlDbType.UniqueIdentifier);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_EnumType_ReturnsNVarChar()
        {
            // Act
            var result = typeof(TestEnum).ToSqlDbType();

            // Assert - Enums are now stored as strings
            result.Should().Be(SqlDbType.NVarChar);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_NullableTypes_ReturnsCorrectSqlTypes()
        {
            // Act & Assert
            typeof(int?).ToSqlDbType().Should().Be(SqlDbType.Int);
            typeof(DateTime?).ToSqlDbType().Should().Be(SqlDbType.DateTime2);
            typeof(decimal?).ToSqlDbType().Should().Be(SqlDbType.Decimal);
            typeof(bool?).ToSqlDbType().Should().Be(SqlDbType.Bit);
            typeof(Guid?).ToSqlDbType().Should().Be(SqlDbType.UniqueIdentifier);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_CharTypes_ReturnsCorrectSqlTypes()
        {
            // Act & Assert
            typeof(char).ToSqlDbType().Should().Be(SqlDbType.NChar);
            typeof(char[]).ToSqlDbType().Should().Be(SqlDbType.NVarChar);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_ComplexType_ReturnsNVarChar()
        {
            // Act
            var result = typeof(ComplexType).ToSqlDbType();

            // Assert
            result.Should().Be(SqlDbType.NVarChar);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_WithMetadata_StringType_ReturnsSizeInfo()
        {
            // Act
            var sqlType = typeof(string).ToSqlDbType(out var size, out var precision, out var scale);

            // Assert
            sqlType.Should().Be(SqlDbType.NVarChar);
            size.Should().Be(255);
            precision.Should().BeNull();
            scale.Should().BeNull();
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_WithMetadata_DecimalType_ReturnsPrecisionScale()
        {
            // Act
            var sqlType = typeof(decimal).ToSqlDbType(out var size, out var precision, out var scale);

            // Assert
            sqlType.Should().Be(SqlDbType.Decimal);
            size.Should().BeNull();
            precision.Should().Be(18);
            scale.Should().Be(2);
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_WithMetadata_EnumType_ReturnsStringWithSize()
        {
            // Act
            var sqlType = typeof(TestEnum).ToSqlDbType(out var size, out var precision, out var scale);

            // Assert
            sqlType.Should().Be(SqlDbType.NVarChar);
            size.Should().Be(50); // Reasonable size for enum string values
            precision.Should().BeNull();
            scale.Should().BeNull();
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_WithMetadata_ByteArrayType_ReturnsMaxSize()
        {
            // Act
            var sqlType = typeof(byte[]).ToSqlDbType(out var size, out var precision, out var scale);

            // Assert
            sqlType.Should().Be(SqlDbType.VarBinary);
            size.Should().Be(-1); // MAX
            precision.Should().BeNull();
            scale.Should().BeNull();
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlTypeString_BasicTypes_ReturnsCorrectSqlStrings()
        {
            // Act & Assert
            typeof(int).ToSqlTypeString().Should().Be("INT");
            typeof(long).ToSqlTypeString().Should().Be("BIGINT");
            typeof(bool).ToSqlTypeString().Should().Be("BIT");
            typeof(Guid).ToSqlTypeString().Should().Be("UNIQUEIDENTIFIER");
            typeof(DateTime).ToSqlTypeString().Should().Be("DATETIME2");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlTypeString_StringType_ReturnsNVarCharWithSize()
        {
            // Act
            var result = typeof(string).ToSqlTypeString();

            // Assert
            result.Should().Be("NVARCHAR(255)");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlTypeString_DecimalType_ReturnsDecimalWithPrecisionScale()
        {
            // Act
            var result = typeof(decimal).ToSqlTypeString();

            // Assert
            result.Should().Be("DECIMAL(18,2)");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlTypeString_ByteArrayType_ReturnsVarBinaryMax()
        {
            // Act
            var result = typeof(byte[]).ToSqlTypeString();

            // Assert
            result.Should().Be("VARBINARY(MAX)");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlTypeString_ComplexType_ReturnsNVarCharMax()
        {
            // Act
            var result = typeof(ComplexType).ToSqlTypeString();

            // Assert
            result.Should().Be("NVARCHAR(MAX)");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void IsNullable_NullableTypes_ReturnsTrue()
        {
            // Act & Assert
            typeof(int?).IsNullable().Should().BeTrue();
            typeof(DateTime?).IsNullable().Should().BeTrue();
            typeof(Guid?).IsNullable().Should().BeTrue();
            typeof(string).IsNullable().Should().BeTrue(); // Reference types are nullable
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void IsNullable_NonNullableValueTypes_ReturnsFalse()
        {
            // Act & Assert
            typeof(int).IsNullable().Should().BeFalse();
            typeof(DateTime).IsNullable().Should().BeFalse();
            typeof(Guid).IsNullable().Should().BeFalse();
            typeof(bool).IsNullable().Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void GetUnderlyingTypeOrSelf_NullableType_ReturnsUnderlyingType()
        {
            // Act
            var result = typeof(int?).GetUnderlyingTypeOrSelf();

            // Assert
            result.Should().Be(typeof(int));
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void GetUnderlyingTypeOrSelf_NonNullableType_ReturnsSameType()
        {
            // Act
            var result = typeof(string).GetUnderlyingTypeOrSelf();

            // Assert
            result.Should().Be(typeof(string));
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void RequiresSize_SizeRequiringTypes_ReturnsTrue()
        {
            // Act & Assert
            SqlDbType.NVarChar.RequiresSize().Should().BeTrue();
            SqlDbType.VarChar.RequiresSize().Should().BeTrue();
            SqlDbType.VarBinary.RequiresSize().Should().BeTrue();
            SqlDbType.Char.RequiresSize().Should().BeTrue();
            SqlDbType.NChar.RequiresSize().Should().BeTrue();
            SqlDbType.Binary.RequiresSize().Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void RequiresSize_NonSizeRequiringTypes_ReturnsFalse()
        {
            // Act & Assert
            SqlDbType.Int.RequiresSize().Should().BeFalse();
            SqlDbType.DateTime2.RequiresSize().Should().BeFalse();
            SqlDbType.Bit.RequiresSize().Should().BeFalse();
            SqlDbType.UniqueIdentifier.RequiresSize().Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void RequiresPrecisionScale_PrecisionScaleTypes_ReturnsTrue()
        {
            // Act & Assert
            SqlDbType.Decimal.RequiresPrecisionScale().Should().BeTrue();
            SqlDbType.Money.RequiresPrecisionScale().Should().BeTrue();
            SqlDbType.SmallMoney.RequiresPrecisionScale().Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void RequiresPrecisionScale_NonPrecisionScaleTypes_ReturnsFalse()
        {
            // Act & Assert
            SqlDbType.Int.RequiresPrecisionScale().Should().BeFalse();
            SqlDbType.Float.RequiresPrecisionScale().Should().BeFalse();
            SqlDbType.Real.RequiresPrecisionScale().Should().BeFalse();
            SqlDbType.NVarChar.RequiresPrecisionScale().Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlDbType_NullType_ThrowsArgumentNullException()
        {
            // Act
            Type nullType = null;
            var action = () => nullType.ToSqlDbType();

            // Assert
            action.Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_SimpleColumn_ReturnsCorrectDefinition()
        {
            // Act
            var result = typeof(string).ToSqlColumnDefinition("Name");

            // Assert
            result.Should().Be("[Name] NVARCHAR(255) NULL");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_NotNullColumn_ReturnsNotNull()
        {
            // Act
            var result = typeof(int).ToSqlColumnDefinition("Id", isNullable: false);

            // Assert
            result.Should().Be("[Id] INT NOT NULL");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_PrimaryKeyColumn_ReturnsPrimaryKey()
        {
            // Act
            var result = typeof(Guid).ToSqlColumnDefinition("Id", isPrimaryKey: true);

            // Assert
            result.Should().Be("[Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_IdentityColumn_ReturnsIdentity()
        {
            // Act
            var result = typeof(int).ToSqlColumnDefinition("Id", isIdentity: true, isPrimaryKey: true);

            // Assert
            result.Should().Be("[Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_WithDefaultValue_ReturnsDefault()
        {
            // Act
            var result = typeof(bool).ToSqlColumnDefinition("IsActive", defaultValue: true);

            // Assert
            result.Should().Be("[IsActive] BIT NOT NULL DEFAULT 1");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_WithStringDefault_ReturnsQuotedDefault()
        {
            // Act
            var result = typeof(string).ToSqlColumnDefinition("Status", defaultValue: "Active");

            // Assert
            result.Should().Be("[Status] NVARCHAR(255) NULL DEFAULT N'Active'");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_WithDateTimeDefault_ReturnsFormattedDefault()
        {
            // Act
            var testDate = new DateTime(2024, 1, 15, 10, 30, 45, 123);
            var result = typeof(DateTime).ToSqlColumnDefinition("CreatedDate", defaultValue: testDate);

            // Assert
            result.Should().Be("[CreatedDate] DATETIME2 NOT NULL DEFAULT '2024-01-15 10:30:45.123'");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_WithGetDateDefault_ReturnsGetDate()
        {
            // Act
            var result = typeof(DateTime).ToSqlColumnDefinition("CreatedTime", defaultValue: "GETDATE()");

            // Assert
            result.Should().Be("[CreatedTime] DATETIME2 NOT NULL DEFAULT GETDATE()");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_WithCheckConstraint_ReturnsCheck()
        {
            // Act
            var result = typeof(int).ToSqlColumnDefinition("Age", checkConstraint: "Age >= 0 AND Age <= 150");

            // Assert
            result.Should().Be("[Age] INT NOT NULL CHECK (Age >= 0 AND Age <= 150)");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_DecimalColumn_ReturnsWithPrecisionScale()
        {
            // Act
            var result = typeof(decimal).ToSqlColumnDefinition("Price", isNullable: false);

            // Assert
            result.Should().Be("[Price] DECIMAL(18,2) NOT NULL");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_NullableType_ReturnsNullable()
        {
            // Act
            var result = typeof(int?).ToSqlColumnDefinition("OptionalValue");

            // Assert
            result.Should().Be("[OptionalValue] INT NULL");
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
            result.Should().Be("[Email] NVARCHAR(255) NOT NULL DEFAULT N'' CHECK (Email LIKE '%@%')");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_GuidWithNewIdDefault_ReturnsNewId()
        {
            // Act
            var result = typeof(Guid).ToSqlColumnDefinition("Id", defaultValue: "NEWID()");

            // Assert
            result.Should().Be("[Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_ByteArrayColumn_ReturnsVarBinaryMax()
        {
            // Act
            var result = typeof(byte[]).ToSqlColumnDefinition("Data");

            // Assert
            result.Should().Be("[Data] VARBINARY(MAX) NULL");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_EnumColumn_ReturnsNVarChar()
        {
            // Act
            var result = typeof(TestEnum).ToSqlColumnDefinition("Status", defaultValue: TestEnum.Value1);

            // Assert - Enums are now stored as strings
            result.Should().Be("[Status] NVARCHAR(50) NOT NULL DEFAULT N'Value1'");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_EmptyColumnName_ThrowsArgumentException()
        {
            // Act
            var action = () => typeof(int).ToSqlColumnDefinition("");

            // Assert
            action.Should().Throw<ArgumentException>();
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_NullColumnName_ThrowsArgumentException()
        {
            // Act
            var action = () => typeof(int).ToSqlColumnDefinition(null);

            // Assert
            action.Should().Throw<ArgumentException>();
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_StringWithApostrophe_EscapesCorrectly()
        {
            // Act
            var result = typeof(string).ToSqlColumnDefinition("Description", defaultValue: "It's a test");

            // Assert
            result.Should().Be("[Description] NVARCHAR(255) NULL DEFAULT N'It''s a test'");
        }

        [TestMethod]
        [TestCategory("Extensions")]
        public void ToSqlColumnDefinition_DateTimeOffsetColumn_ReturnsDateTimeOffset()
        {
            // Act
            var result = typeof(DateTimeOffset).ToSqlColumnDefinition("EventTime");

            // Assert
            result.Should().Be("[EventTime] DATETIMEOFFSET NOT NULL");
        }
    }
}