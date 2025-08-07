//-------------------------------------------------------------------------------
// <copyright file="MultiForeignKeyEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    /// <summary>
    /// Entity with multiple foreign keys for testing.
    /// </summary>
    [Table("EntityWithMultipleForeignKeys")]
    public class MultiForeignKeyEntity : BaseEntity<string>
    {
        [Column("ParentId1", SqlDbType.Text)]
        [ForeignKey("Parent1", "Id", Name = "FK_Parent1", OnDelete = ForeignKeyAction.Cascade)]
        public string ParentId1 { get; set; }

        [Column("ParentId2", SqlDbType.Text)]
        [ForeignKey("Parent2", "Id", Name = "FK_Parent2", OnDelete = ForeignKeyAction.SetNull)]
        public string ParentId2 { get; set; }

        [Column("ParentId3", SqlDbType.Text)]
        [ForeignKey("Parent3", "Id", Name = "FK_Parent3", OnDelete = ForeignKeyAction.Restrict)]
        public string ParentId3 { get; set; }

        [Column("Data", SqlDbType.Text)]
        public string Data { get; set; }
    }
}