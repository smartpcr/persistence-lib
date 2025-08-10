//-------------------------------------------------------------------------------
// <copyright file="AuditTestEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.AuditTrail
{
    using System;
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("AuditTestEntity", EnableAuditTrail = true)]
    public class AuditTestEntity : BaseEntity<Guid>
    {
        [Column("Name", SqlDbType.NVarChar, Size = 100)]
        public string Name { get; set; }

        [Column("Status", SqlDbType.NVarChar, Size = 50)]
        public string Status { get; set; }

        [Column("Value", SqlDbType.Int)]
        public int Value { get; set; }
    }
}
