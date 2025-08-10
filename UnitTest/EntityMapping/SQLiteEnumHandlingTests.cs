// -----------------------------------------------------------------------
// <copyright file="SQLiteEnumHandlingTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.EntityMapping
{
    using System;
    using System.Data;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Resilience;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SQLiteEnumHandlingTests
    {
        public enum TestStatus
        {
            Active = 1,
            Inactive = 2,
            Pending = 3
        }

        [Table("TestEntity")]
        public class TestEntityWithEnum : IEntity<Guid>
        {
            [PrimaryKey]
            [Column("Id", SqlDbType.UniqueIdentifier)]
            public Guid Id { get; set; }

            [Column("Status", SqlDbType.NVarChar)]
            public TestStatus Status { get; set; }

            [Column("Version", SqlDbType.BigInt)]
            public long Version { get; set; }

            public DateTimeOffset CreatedTime { get; set; }
            public DateTimeOffset LastWriteTime { get; set; }

            public long EstimateEntitySize()
            {
                return 100;
            }
        }

        private class TestSQLiteEntityMapper : SQLiteEntityMapper<TestEntityWithEnum, Guid>
        {
            public TestSQLiteEntityMapper() : base(new NoRetryPolicy())
            {
            }

            public object TestConvertParameterValue(object value)
            {
                return this.ConvertParameterValue(value);
            }
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        [TestCategory("EnumHandling")]
        public void SQLiteEntityMapper_ConvertParameterValue_EnumToString()
        {
            // Arrange
            var mapper = new TestSQLiteEntityMapper();
            var enumValue = TestStatus.Active;

            // Act
            var result = mapper.TestConvertParameterValue(enumValue);

            // Assert - Should delegate to base class which converts to string
            result.Should().BeOfType<string>();
            result.Should().Be("Active");
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        [TestCategory("EnumHandling")]
        public void SQLiteEntityMapper_ConvertParameterValue_NullableEnumToString()
        {
            // Arrange
            var mapper = new TestSQLiteEntityMapper();
            TestStatus? enumValue = TestStatus.Inactive;

            // Act
            var result = mapper.TestConvertParameterValue(enumValue);

            // Assert
            result.Should().BeOfType<string>();
            result.Should().Be("Inactive");
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        [TestCategory("EnumHandling")]
        public void SQLiteEntityMapper_ConvertParameterValue_NullEnum()
        {
            // Arrange
            var mapper = new TestSQLiteEntityMapper();
            TestStatus? enumValue = null;

            // Act
            var result = mapper.TestConvertParameterValue(enumValue);

            // Assert
            result.Should().Be(DBNull.Value);
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        [TestCategory("EnumHandling")]
        public void SQLiteEntityMapper_ConvertParameterValue_OtherTypes()
        {
            // Arrange
            var mapper = new TestSQLiteEntityMapper();

            // Act & Assert - Verify other type conversions still work
            mapper.TestConvertParameterValue(true).Should().Be(1);
            mapper.TestConvertParameterValue(false).Should().Be(0);
            mapper.TestConvertParameterValue(DateTime.Parse("2024-01-15T10:30:00"))
                .Should().Be("2024-01-15T10:30:00.0000000");
            mapper.TestConvertParameterValue(Guid.Parse("12345678-1234-1234-1234-123456789012"))
                .Should().Be("12345678-1234-1234-1234-123456789012");
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        [TestCategory("EnumHandling")]
        public void SQLiteEntityMapper_CreateCommand_WithEnum_UsesStringParameter()
        {
            // Arrange
            var mapper = new SQLiteEntityMapper<TestEntityWithEnum, Guid>(new NoRetryPolicy());
            var entity = new TestEntityWithEnum
            {
                Id = Guid.NewGuid(),
                Status = TestStatus.Pending,
                Version = 1,
                CreatedTime = DateTimeOffset.UtcNow,
                LastWriteTime = DateTimeOffset.UtcNow
            };

            var context = CommandContext<TestEntityWithEnum, Guid>.ForInsert(entity);

            // Act
            var command = mapper.CreateCommand(DbOperationType.Insert, context);

            // Assert
            command.Should().NotBeNull();
            command.CommandText.Should().Contain("INSERT INTO");
            
            // Find the Status parameter
            IDataParameter statusParam = null;
            foreach (IDataParameter param in command.Parameters)
            {
                if (param.ParameterName == "@Status")
                {
                    statusParam = param;
                    break;
                }
            }

            statusParam.Should().NotBeNull();
            statusParam.Value.Should().BeOfType<string>();
            statusParam.Value.Should().Be("Pending");
        }
    }

    // Mock retry policy for testing (no retries)
    internal class NoRetryPolicy : RetryPolicy
    {
        public NoRetryPolicy() : base(maxRetryAttempts: 0)
        {
        }
    }
}