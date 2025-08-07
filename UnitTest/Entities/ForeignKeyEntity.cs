//-------------------------------------------------------------------------------
// <copyright file="ForeignKeyEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("TestEntityWithForeignKey")]
    public class ForeignKeyEntity : BaseEntity<string>
    {
        [Column("ParentId", SqlDbType.Text)]
        [ForeignKey("TestEntity", "Id")]
        public string ParentId { get; set; }

        [Column("Name", SqlDbType.Text)]
        public string Name { get; set; }
    }
}