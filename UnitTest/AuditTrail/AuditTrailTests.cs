// -----------------------------------------------------------------------
// <copyright file="AuditTrailTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.AuditTrail
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config;
    using AuditTestEntity = Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.AuditTrail.AuditTestEntity;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Audit;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Helpers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Providers;

    [TestClass]
    public class AuditTrailTests : SQLiteTestBase
    {
        private string testDbPath;
        private string connectionString;
        private SQLitePersistenceProvider<AuditTestEntity, Guid> provider;
        private IAuditProvider auditProvider;
        private CallerInfo callerInfo;

        [TestInitialize]
        public async Task Setup()
        {
            this.testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
            this.connectionString = $"Data Source={this.testDbPath};Version=3;";
            var config = new SqliteConfiguration();
            this.provider = new SQLitePersistenceProvider<AuditTestEntity, Guid>(this.connectionString, config);
            await this.provider.InitializeAsync();
            this.auditProvider = new SQLiteAuditProvider(this.connectionString, config);

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

            this.SafeDeleteDatabase(this.testDbPath);
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
            var records = (await this.provider.GetByKeyAsync(
                entity.Id,
                callerInfo: this.callerInfo))?.ToList();
            records.Should().NotBeNull();
            records!.Count.Should().Be(1);
            records.First().Name.Should().Be("Audit Test");

            var sqlHelper = new SQLiteHelper(this.connectionString);
            var tables = await sqlHelper.GetTablesAsync();
            tables.Any(t => t.TableName == "AuditTestEntity").Should().BeTrue();
            tables.Any(t => t.TableName == "Audit").Should().BeTrue();

            var auditRecords = await this.auditProvider.GetAuditRecordsAsync(nameof(AuditTestEntity), null, CancellationToken.None);
            auditRecords.Should().NotBeNull();
            auditRecords.Count.Should().Be(2);
            var createAudit = auditRecords.First(ar => ar.Operation == AuditOperation.Create);
            createAudit.Should().NotBeNull();
            createAudit.Operation.Should().Be(AuditOperation.Create);
            createAudit.EntityId.Should().Be(entity.Id.ToString());
            createAudit.EntityType.Should().Be(nameof(AuditTestEntity));
            createAudit.Version.Should().Be(records.First().Version);

            var readAudit = auditRecords.First(ar => ar.Operation == AuditOperation.Read);
            readAudit.Should().NotBeNull();
            readAudit.Operation.Should().Be(AuditOperation.Read);
            readAudit.EntityId.Should().Be(entity.Id.ToString());
            readAudit.EntityType.Should().Be(nameof(AuditTestEntity));
            readAudit.Version.Should().Be(records.First().Version);
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
            var auditRecords = await this.auditProvider.GetAuditRecordsAsync(nameof(AuditTestEntity), null, CancellationToken.None);
            auditRecords.Should().NotBeNull();
            auditRecords.Count.Should().Be(2);
            var createAudit = auditRecords.First(ar => ar.Operation == AuditOperation.Create);
            createAudit.Should().NotBeNull();
            createAudit.Operation.Should().Be(AuditOperation.Create);
            createAudit.EntityId.Should().Be(entity.Id.ToString());
            createAudit.EntityType.Should().Be(nameof(AuditTestEntity));
            createAudit.Version.Should().Be(1);

            var updateAudit = auditRecords.First(ar => ar.Operation == AuditOperation.Update);
            updateAudit.Should().NotBeNull();
            updateAudit.Operation.Should().Be(AuditOperation.Update);
            updateAudit.EntityId.Should().Be(entity.Id.ToString());
            updateAudit.EntityType.Should().Be(nameof(AuditTestEntity));
            updateAudit.Version.Should().Be(2);
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
            var auditRecords = await this.auditProvider.GetAuditRecordsAsync(nameof(AuditTestEntity), null, CancellationToken.None);
            auditRecords.Should().NotBeNull();
            auditRecords.Count.Should().Be(2);
            var createAudit = auditRecords.First(ar => ar.Operation == AuditOperation.Create);
            createAudit.Should().NotBeNull();
            createAudit.Operation.Should().Be(AuditOperation.Create);
            createAudit.EntityId.Should().Be(entity.Id.ToString());
            createAudit.EntityType.Should().Be(nameof(AuditTestEntity));
            createAudit.Version.Should().Be(1);

            var deleteAudit = auditRecords.First(ar => ar.Operation == AuditOperation.Delete);
            deleteAudit.Should().NotBeNull();
            deleteAudit.Operation.Should().Be(AuditOperation.Delete);
            deleteAudit.EntityId.Should().Be(entity.Id.ToString());
            deleteAudit.EntityType.Should().Be(nameof(AuditTestEntity));
            deleteAudit.Version.Should().Be(1);
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
            var auditRecords = await this.auditProvider.GetAuditRecordsAsync(nameof(AuditTestEntity), null, CancellationToken.None);
            auditRecords.Should().NotBeNull();
            auditRecords.Count.Should().Be(1);
            var createAudit = auditRecords.First(ar => ar.Operation == AuditOperation.Create);
            createAudit.Should().NotBeNull();
            createAudit.Operation.Should().Be(AuditOperation.Create);
            createAudit.EntityId.Should().Be(entity.Id.ToString());
            createAudit.EntityType.Should().Be(nameof(AuditTestEntity));
            createAudit.Version.Should().Be(1);
            createAudit.CallerMember.Should().Be("TestMethod"); // Property name is CallerMember
            createAudit.CallerFile.Should().Be("TestFile.cs"); // Property name is CallerFile
            createAudit.CallerLineNumber.Should().Be(42);
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

            // Assert
            var auditRecords = await this.auditProvider.GetAuditRecordsAsync(nameof(AuditTestEntity), null, CancellationToken.None);
            auditRecords.Should().NotBeNull();
            auditRecords.Count.Should().Be(4);
            var createAudit = auditRecords.First(ar => ar.Operation == AuditOperation.Create);
            createAudit.Should().NotBeNull();
            createAudit.Operation.Should().Be(AuditOperation.Create);
            createAudit.EntityId.Should().Be(entity.Id.ToString());
            createAudit.EntityType.Should().Be(nameof(AuditTestEntity));
            createAudit.Version.Should().Be(1);

            var deleteAudit = auditRecords.First(ar => ar.Operation == AuditOperation.Delete);
            deleteAudit.Should().NotBeNull();
            deleteAudit.Operation.Should().Be(AuditOperation.Delete);
            deleteAudit.EntityId.Should().Be(entity.Id.ToString());
            deleteAudit.EntityType.Should().Be(nameof(AuditTestEntity));
            deleteAudit.Version.Should().Be(3);
        }
    }
}