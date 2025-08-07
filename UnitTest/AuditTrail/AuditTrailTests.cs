// -----------------------------------------------------------------------
// <copyright file="AuditTrailTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.AuditTrail
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AuditTrailTests
    {
        [Table("AuditTestEntity", EnableAuditTrail = true)]
        public class AuditTestEntity : BaseEntity<Guid>
        {
            [PrimaryKey]
            public new Guid Id { get; set; }
            public string Name { get; set; }
            public string Status { get; set; }
            public int Value { get; set; }
        }

        private string connectionString;
        private SQLitePersistenceProvider<AuditTestEntity, Guid> provider;
        private CallerInfo callerInfo;

        [TestInitialize]
        public async Task Setup()
        {
            this.connectionString = "Data Source=:memory:";
            var config = new SqliteConfiguration();
            this.provider = new SQLitePersistenceProvider<AuditTestEntity, Guid>(this.connectionString, config);
            await this.provider.InitializeAsync();
            
            this.callerInfo = new CallerInfo
            {
                CallerMemberName = "TestMethod",
                CallerFilePath = "TestFile.cs",
                CallerLineNumber = 42
            };
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (this.provider != null)
            {
                await this.provider.DisposeAsync();
            }
        }

        [TestMethod]
        [TestCategory("AuditTrail")]
        public async Task WriteAuditRecord_Create_CapturesDetails()
        {
            // Arrange
            var entity = new AuditTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Audit Test",
                Status = "Active",
                Value = 100
            };

            // Act
            await this.provider.CreateAsync(entity, this.callerInfo);

            // Assert
            var auditRecords = await this.provider.GetByKeyAsync(
                entity.Id,
                callerInfo: this.callerInfo);
            
            Assert.IsNotNull(auditRecords);
            Assert.AreEqual(1, auditRecords.Count());
            
            var createAudit = auditRecords.First();
            Assert.AreEqual("CREATE", createAudit.Operation);
            Assert.AreEqual(entity.Id.ToString(), createAudit.EntityId);
            Assert.AreEqual("AuditTestEntity", createAudit.EntityType);
            Assert.AreEqual("TestUser", createAudit.UserId);
            Assert.IsNotNull(createAudit.NewValue);
            Assert.IsNull(createAudit.OldValue);
        }

        [TestMethod]
        [TestCategory("AuditTrail")]
        public async Task WriteAuditRecord_Update_CapturesOldAndNew()
        {
            // Arrange
            var entity = new AuditTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Original Name",
                Status = "Active",
                Value = 100
            };
            var created = await this.provider.CreateAsync(entity, this.callerInfo);
            
            // Act
            created.Name = "Updated Name";
            created.Status = "Inactive";
            created.Value = 200;
            await this.provider.UpdateAsync(created, this.callerInfo);

            // Assert
            var auditRecords = await this.provider.QueryAuditTrailAsync(
                entityId: entity.Id.ToString(),
                callerInfo: this.callerInfo);
            
            Assert.IsTrue(auditRecords.Count >= 2);
            
            var updateAudit = auditRecords.FirstOrDefault(a => a.Operation == "UPDATE");
            Assert.IsNotNull(updateAudit);
            Assert.IsNotNull(updateAudit.OldValue);
            Assert.IsNotNull(updateAudit.NewValue);
            Assert.IsTrue(updateAudit.OldValue.Contains("Original Name"));
            Assert.IsTrue(updateAudit.NewValue.Contains("Updated Name"));
        }

        [TestMethod]
        [TestCategory("AuditTrail")]
        public async Task WriteAuditRecord_Delete_CapturesFinalState()
        {
            // Arrange
            var entity = new AuditTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "To Delete",
                Status = "Active",
                Value = 100
            };
            await this.provider.CreateAsync(entity, this.callerInfo);

            // Act
            await this.provider.DeleteAsync(entity.Id, this.callerInfo);

            // Assert
            var auditRecords = await this.provider.QueryAuditTrailAsync(
                entityId: entity.Id.ToString(),
                callerInfo: this.callerInfo);
            
            var deleteAudit = auditRecords.FirstOrDefault(a => a.Operation == "DELETE");
            Assert.IsNotNull(deleteAudit);
            Assert.IsNotNull(deleteAudit.OldValue);
            Assert.IsTrue(deleteAudit.OldValue.Contains("To Delete"));
        }

        [TestMethod]
        [TestCategory("AuditTrail")]
        public async Task WriteAuditRecord_IncludesCallerInfo()
        {
            // Arrange
            var entity = new AuditTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Caller Info Test",
                Value = 100
            };

            // Act
            await this.provider.CreateAsync(entity, this.callerInfo);

            // Assert
            var auditRecords = await this.provider.QueryAuditTrailAsync(
                entityId: entity.Id.ToString(),
                callerInfo: this.callerInfo);
            
            var audit = auditRecords.First();
            Assert.AreEqual("TestUser", audit.UserId);
            Assert.AreEqual(this.callerInfo.CorrelationId, audit.CorrelationId);
            Assert.AreEqual("TestMethod", audit.CallerMemberName);
            Assert.AreEqual("TestFile.cs", audit.CallerFilePath);
            Assert.AreEqual(42, audit.CallerLineNumber);
        }

        [TestMethod]
        [TestCategory("AuditTrail")]
        public async Task QueryAuditTrail_ByEntity_ReturnsHistory()
        {
            // Arrange
            var entity = new AuditTestEntity
            {
                Id = Guid.NewGuid(),
                Name = "Version 1",
                Status = "Active",
                Value = 100
            };
            
            // Create
            var created = await this.provider.CreateAsync(entity, this.callerInfo);
            
            // Update multiple times
            created.Name = "Version 2";
            await this.provider.UpdateAsync(created, this.callerInfo);
            
            created.Name = "Version 3";
            created.Status = "Inactive";
            await this.provider.UpdateAsync(created, this.callerInfo);
            
            // Delete
            await this.provider.DeleteAsync(created.Id, this.callerInfo);

            // Act
            var auditHistory = await this.provider.QueryAuditTrailAsync(
                entityId: entity.Id.ToString(),
                callerInfo: this.callerInfo);

            // Assert
            Assert.IsNotNull(auditHistory);
            Assert.IsTrue(auditHistory.Count >= 4); // CREATE, UPDATE, UPDATE, DELETE
            
            // Verify chronological order
            var operations = auditHistory.Select(a => a.Operation).ToList();
            Assert.AreEqual("CREATE", operations[0]);
            Assert.AreEqual("DELETE", operations[operations.Count - 1]);
        }

        [TestMethod]
        [TestCategory("AuditTrail")]
        public async Task QueryAuditTrail_ByUser_ReturnsUserActivity()
        {
            // Arrange
            var user1Caller = new CallerInfo { UserId = "User1", CorrelationId = Guid.NewGuid().ToString() };
            var user2Caller = new CallerInfo { UserId = "User2", CorrelationId = Guid.NewGuid().ToString() };
            
            // User1 creates entities
            for (int i = 0; i < 3; i++)
            {
                await this.provider.CreateAsync(
                    new AuditTestEntity { Id = Guid.NewGuid(), Name = $"User1 Entity {i}", Value = i },
                    user1Caller);
            }
            
            // User2 creates entities
            for (int i = 0; i < 2; i++)
            {
                await this.provider.CreateAsync(
                    new AuditTestEntity { Id = Guid.NewGuid(), Name = $"User2 Entity {i}", Value = i },
                    user2Caller);
            }

            // Act
            var user1Activity = await this.provider.QueryAuditTrailAsync(
                userId: "User1",
                callerInfo: this.callerInfo);
            
            var user2Activity = await this.provider.QueryAuditTrailAsync(
                userId: "User2",
                callerInfo: this.callerInfo);

            // Assert
            Assert.AreEqual(3, user1Activity.Count);
            Assert.IsTrue(user1Activity.All(a => a.UserId == "User1"));
            
            Assert.AreEqual(2, user2Activity.Count);
            Assert.IsTrue(user2Activity.All(a => a.UserId == "User2"));
        }
    }
}