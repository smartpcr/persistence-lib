//-------------------------------------------------------------------------------
// <copyright file="ConfigTestEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.Configuration
{
    using System;
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("ConfigTestEntity")]
    public class ConfigTestEntity : BaseEntity<Guid>
    {
        [Column("Name", SqlDbType.NVarChar, Size = 100)]
        public string Name { get; set; }
    }
}
