//-------------------------------------------------------------------------------
// <copyright file="NotMappedEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("TestEntityNotMapped")]
    public class NotMappedEntity : BaseEntity<string>
    {
        [Column("MappedProperty", SqlDbType.Text)]
        public string MappedProperty { get; set; }

        [NotMapped]
        public string NotMappedProperty { get; set; }

        [NotMapped]
        public ComplexObject ComplexProperty { get; set; }
    }
}