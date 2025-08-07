//-------------------------------------------------------------------------------
// <copyright file="TransactionTestEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.Transaction
{
    using System.Data;
    using System;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("TransactionTestEntity")]
    public class TransactionTestEntity : BaseEntity<Guid>
    {
        [Column("Name", SqlDbType.NVarChar, Size = 100)]
        public string Name { get; set; }

        [Column("Value", SqlDbType.Int)]
        public int Value { get; set; }
    }
}
