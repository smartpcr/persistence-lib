//-------------------------------------------------------------------------------
// <copyright file="DuplicatePrimaryKeyEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("DuplicatePrimaryKey")]
    public class DuplicatePrimaryKeyEntity : BaseEntity<string>
    {
        [PrimaryKey]
        [Column("Id1", SqlDbType.Text)]
        public string Id1 { get; set; }

        [PrimaryKey]
        [Column("Id2", SqlDbType.Text)]
        public string Id2 { get; set; }

        [NotMapped]
        public new string Id { get; set; }

        [NotMapped]
        public new long Version { get; set; }
    }
}