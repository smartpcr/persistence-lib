//-------------------------------------------------------------------------------
// <copyright file="NoSoftDeleteEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("TestEntityWithoutSoftDelete", SoftDeleteEnabled = false)]
    public class NoSoftDeleteEntity : BaseEntity<string>
    {
        [Column("Value", SqlDbType.Decimal)]
        public decimal Value { get; set; }
    }
}