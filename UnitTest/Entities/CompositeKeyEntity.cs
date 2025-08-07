//-------------------------------------------------------------------------------
// <copyright file="CompositeKeyEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("TestEntityComposite")]
    public class CompositeKeyEntity : BaseEntity<string>
    {
        [PrimaryKey(Order = 1)]
        [Column("Key1", SqlDbType.Text)]
        public string Key1 { get; set; }

        [PrimaryKey(Order = 2)]
        [Column("Key2", SqlDbType.Int)]
        public int Key2 { get; set; }

        [Column("Value", SqlDbType.Text)]
        public string Value { get; set; }

        [NotMapped]
        public new string Id { get; set; }

        [NotMapped]
        public new long Version { get; set; }
    }
}