// -----------------------------------------------------------------------
// <copyright file="SQLiteEntityMapperTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.EntityMapping
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SQLite;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Mappings;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class SQLiteEntityMapperTests
    {
        public class ComplexObject
        {
            public string Field1 { get; set; }
            public int Field2 { get; set; }
            public List<string> Items { get; set; }
        }

        private SQLiteEntityMapper<Entities.EntityMapping.SQLiteMapperTestEntity, Guid> mapper;
        private SqliteConfiguration configuration;

        [TestInitialize]
        public void Setup()
        {
            this.configuration = new SqliteConfiguration
            {
                CommandTimeout = 30
            };
            this.mapper = new SQLiteEntityMapper<Entities.EntityMapping.SQLiteMapperTestEntity, Guid>();
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void CreateCommand_GeneratesCorrectInsertSql()
        {
            // Arrange
            var entity = new Entities.EntityMapping.SQLiteMapperTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Test",
                Count = 5,
                CreatedDate = DateTime.UtcNow,
                Version = 1,
                CreatedTime = DateTime.UtcNow,
                LastWriteTime = DateTime.UtcNow
            };

            var context = new CommandContext<Entities.EntityMapping.SQLiteMapperTestEntity, Guid>
            {
                Entity = entity,
                CommandTimeout = 30
            };

            // Act
            using var command = this.mapper.CreateCommand(DbOperationType.Insert, context);

            // Assert
            Assert.IsNotNull(command);
            Assert.IsTrue(command.CommandText.StartsWith("INSERT INTO"), 
                "Command should be an INSERT statement");
            Assert.IsTrue(command.CommandText.Contains("TestEntity"), 
                "Should reference the correct table");
            Assert.AreEqual(CommandType.Text, command.CommandType);
            Assert.IsTrue(command.Parameters.Count > 0, "Should have parameters");
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void CreateCommand_GeneratesCorrectUpdateSql()
        {
            // Arrange
            var entity = new Entities.EntityMapping.SQLiteMapperTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Updated",
                Count = 10,
                Version = 2,
                LastWriteTime = DateTime.UtcNow
            };

            var context = new CommandContext<Entities.EntityMapping.SQLiteMapperTestEntity, Guid>
            {
                Entity = entity,
                Id = entity.Id,
                CommandTimeout = 30
            };

            // Act
            using var command = this.mapper.CreateCommand(DbOperationType.Update, context);

            // Assert
            Assert.IsNotNull(command);
            Assert.IsTrue(command.CommandText.StartsWith("UPDATE"), 
                "Command should be an UPDATE statement");
            Assert.IsTrue(command.CommandText.Contains("WHERE"), 
                "Should have WHERE clause");
            Assert.IsTrue(command.CommandText.Contains("Version"), 
                "Should check version for optimistic concurrency");
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void CreateCommand_GeneratesCorrectDeleteSql()
        {
            // Arrange
            var key = Guid.NewGuid();
            var context = new CommandContext<Entities.EntityMapping.SQLiteMapperTestEntity, Guid>
            {
                Id = key,
                CommandTimeout = 30
            };

            // Act
            using var command = this.mapper.CreateCommand(DbOperationType.Delete, context);

            // Assert
            Assert.IsNotNull(command);
            Assert.IsTrue(command.CommandText.StartsWith("DELETE FROM"), 
                "Command should be a DELETE statement");
            Assert.IsTrue(command.CommandText.Contains("WHERE"), 
                "Should have WHERE clause");
            Assert.IsTrue(command.CommandText.Contains("@Id"), 
                "Should have Id parameter");
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public async Task MapFromReader_MapsAllColumnTypes()
        {
            // Arrange
            var mockReader = new Mock<IDataReader>();
            var testGuid = Guid.NewGuid();
            var testDate = DateTime.UtcNow;
            
            mockReader.Setup(r => r.GetOrdinal("Id")).Returns(0);
            mockReader.Setup(r => r.GetOrdinal("Name")).Returns(1);
            mockReader.Setup(r => r.GetOrdinal("Count")).Returns(2);
            mockReader.Setup(r => r.GetOrdinal("CreatedDate")).Returns(3);
            mockReader.Setup(r => r.GetOrdinal("Amount")).Returns(4);
            mockReader.Setup(r => r.GetOrdinal("Version")).Returns(5);
            mockReader.Setup(r => r.GetOrdinal("CreatedTime")).Returns(6);
            mockReader.Setup(r => r.GetOrdinal("LastWriteTime")).Returns(7);
            
            mockReader.Setup(r => r.IsDBNull(It.IsAny<int>())).Returns(false);
            mockReader.Setup(r => r.IsDBNull(4)).Returns(false); // Amount is not null
            
            mockReader.Setup(r => r.GetValue(0)).Returns(testGuid.ToString());
            mockReader.Setup(r => r.GetValue(1)).Returns("Test Name");
            mockReader.Setup(r => r.GetValue(2)).Returns(42);
            mockReader.Setup(r => r.GetValue(3)).Returns(testDate);
            mockReader.Setup(r => r.GetValue(4)).Returns(123.45m);
            mockReader.Setup(r => r.GetValue(5)).Returns(1);
            mockReader.Setup(r => r.GetValue(6)).Returns(testDate);
            mockReader.Setup(r => r.GetValue(7)).Returns(testDate);
            
            mockReader.Setup(r => r.FieldCount).Returns(8);
            for (int i = 0; i < 8; i++)
            {
                var index = i;
                mockReader.Setup(r => r.GetName(index)).Returns(
                    new[] { "Id", "Name", "Count", "CreatedDate", "Amount", "Version", "CreatedTime", "LastWriteTime" }[index]
                );
            }

            // Act
            var entity = await this.mapper.MapFromReaderAsync(mockReader.Object);

            // Assert
            Assert.IsNotNull(entity);
            Assert.AreEqual(testGuid, entity.Id);
            Assert.AreEqual("Test Name", entity.Name);
            Assert.AreEqual(42, entity.Count);
            Assert.AreEqual(123.45m, entity.Amount);
            Assert.AreEqual(1, entity.Version);
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public async Task MapFromReader_HandlesDBNull()
        {
            // Arrange
            var mockReader = new Mock<IDataReader>();
            var testGuid = Guid.NewGuid();
            var testDate = DateTime.UtcNow;
            
            // Setup ordinals
            mockReader.Setup(r => r.GetOrdinal("Id")).Returns(0);
            mockReader.Setup(r => r.GetOrdinal("Name")).Returns(1);
            mockReader.Setup(r => r.GetOrdinal("Amount")).Returns(2);
            mockReader.Setup(r => r.GetOrdinal("Version")).Returns(3);
            mockReader.Setup(r => r.GetOrdinal("CreatedTime")).Returns(4);
            mockReader.Setup(r => r.GetOrdinal("LastWriteTime")).Returns(5);
            
            // Setup DBNull checks
            mockReader.Setup(r => r.IsDBNull(2)).Returns(true); // Amount is null
            mockReader.Setup(r => r.IsDBNull(It.Is<int>(i => i != 2))).Returns(false);
            
            // Setup values
            mockReader.Setup(r => r.GetValue(0)).Returns(testGuid.ToString());
            mockReader.Setup(r => r.GetValue(1)).Returns("Test");
            mockReader.Setup(r => r.GetValue(2)).Returns(DBNull.Value);
            mockReader.Setup(r => r.GetValue(3)).Returns(1);
            mockReader.Setup(r => r.GetValue(4)).Returns(testDate);
            mockReader.Setup(r => r.GetValue(5)).Returns(testDate);
            
            mockReader.Setup(r => r.FieldCount).Returns(6);
            for (int i = 0; i < 6; i++)
            {
                var index = i;
                mockReader.Setup(r => r.GetName(index)).Returns(
                    new[] { "Id", "Name", "Amount", "Version", "CreatedTime", "LastWriteTime" }[index]
                );
            }

            // Act
            var entity = await this.mapper.MapFromReaderAsync(mockReader.Object);

            // Assert
            Assert.IsNotNull(entity);
            Assert.AreEqual(testGuid, entity.Id);
            Assert.AreEqual("Test", entity.Name);
            Assert.IsNull(entity.Amount, "Nullable Amount should be null when DBNull");
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void AddParameters_BindsAllProperties()
        {
            // Arrange
            var command = new SQLiteCommand();
            var entity = new TestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Test",
                Count = 5,
                CreatedDate = DateTime.UtcNow,
                Amount = 100.50m,
                Version = 1,
                CreatedTime = DateTime.UtcNow,
                LastWriteTime = DateTime.UtcNow
            };

            // Act
            this.mapper.AddParameters(command, entity);

            // Assert
            Assert.IsTrue(command.Parameters.Contains("@Id"));
            Assert.IsTrue(command.Parameters.Contains("@Name"));
            Assert.IsTrue(command.Parameters.Contains("@Count"));
            Assert.IsTrue(command.Parameters.Contains("@CreatedDate"));
            Assert.IsTrue(command.Parameters.Contains("@Amount"));
            Assert.IsTrue(command.Parameters.Contains("@Version"));
            
            Assert.AreEqual(entity.Id.ToString(), command.Parameters["@Id"].Value);
            Assert.AreEqual(entity.Name, command.Parameters["@Name"].Value);
            Assert.AreEqual(entity.Count, command.Parameters["@Count"].Value);
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void SerializeEntity_HandlesComplexObjects()
        {
            // Arrange
            var entity = new TestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Test",
                ComplexData = new ComplexObject
                {
                    Field1 = "Value1",
                    Field2 = 42,
                    Items = new List<string> { "Item1", "Item2", "Item3" }
                },
                Version = 1,
                CreatedTime = DateTime.UtcNow,
                LastWriteTime = DateTime.UtcNow
            };

            // Act
            var json = this.mapper.SerializeEntity(entity);

            // Assert
            Assert.IsNotNull(json);
            Assert.IsTrue(json.Contains("\"Field1\":\"Value1\""));
            Assert.IsTrue(json.Contains("\"Field2\":42"));
            Assert.IsTrue(json.Contains("Item1"));
            
            // Verify it's valid JSON
            var deserialized = JsonConvert.DeserializeObject<TestEntity>(json);
            Assert.IsNotNull(deserialized);
        }

        [TestMethod]
        [TestCategory("EntityMapping")]
        public void DeserializeEntity_ReconstructsObjects()
        {
            // Arrange
            var original = new TestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Test Entity",
                Count = 100,
                Amount = 250.75m,
                ComplexData = new ComplexObject
                {
                    Field1 = "Complex Value",
                    Field2 = 999,
                    Items = new List<string> { "A", "B", "C" }
                },
                Version = 5,
                CreatedTime = DateTime.UtcNow,
                LastWriteTime = DateTime.UtcNow
            };
            
            var json = JsonConvert.SerializeObject(original);

            // Act
            var deserialized = this.mapper.DeserializeEntity(json);

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.Id, deserialized.Id);
            Assert.AreEqual(original.Name, deserialized.Name);
            Assert.AreEqual(original.Count, deserialized.Count);
            Assert.AreEqual(original.Amount, deserialized.Amount);
            Assert.IsNotNull(deserialized.ComplexData);
            Assert.AreEqual(original.ComplexData.Field1, deserialized.ComplexData.Field1);
            Assert.AreEqual(original.ComplexData.Field2, deserialized.ComplexData.Field2);
            Assert.AreEqual(3, deserialized.ComplexData.Items.Count);
            Assert.AreEqual("A", deserialized.ComplexData.Items[0]);
        }
    }
}