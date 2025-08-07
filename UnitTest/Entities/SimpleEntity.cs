//-------------------------------------------------------------------------------
// <copyright file="SimpleEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("TestEntity", SoftDeleteEnabled = false)]
    public class SimpleEntity : BaseEntity<string>
    {
        [Column("Name", SqlDbType.Text)]
        public string Name { get; set; }

        [Column("Age", SqlDbType.Int)]
        public int Age { get; set; }
    }
}