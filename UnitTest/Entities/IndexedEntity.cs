//-------------------------------------------------------------------------------
// <copyright file="IndexedEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System;
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("TestEntityWithIndexes")]
    public class IndexedEntity : BaseEntity<string>
    {
        [Column("Email", SqlDbType.Text)]
        [Index("IX_Email", IsUnique = true)]
        public string Email { get; set; }

        [Column("Category", SqlDbType.Text)]
        [Index("IX_CategoryDate", Order = 1)]
        public string Category { get; set; }

        [Column("DateCreated", SqlDbType.DateTime)]
        [Index("IX_CategoryDate", Order = 2)]
        public DateTime DateCreated { get; set; }
    }
}