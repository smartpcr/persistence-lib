//-------------------------------------------------------------------------------
// <copyright file="HierarchicalEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    /// <summary>
    /// Hierarchical entity with self-referencing foreign key for testing.
    /// </summary>
    [Table("HierarchicalEntity")]
    public class HierarchicalEntity : BaseEntity<string>
    {
        [Column("ParentId", SqlDbType.Text)]
        [ForeignKey("HierarchicalEntity", "Id", Name = "FK_Parent")]
        public string ParentId { get; set; }

        [Column("Name", SqlDbType.Text)]
        public string Name { get; set; }

        [Column("Level", SqlDbType.Int)]
        public int Level { get; set; }

        [Column("Path", SqlDbType.Text)]
        public string Path { get; set; }
    }
}