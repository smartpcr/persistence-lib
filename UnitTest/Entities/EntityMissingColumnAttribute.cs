//-------------------------------------------------------------------------------
// <copyright file="EntityMissingColumnAttribute.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("EntityMissingColumn")]
    public class EntityMissingColumnAttribute : BaseEntity<string>
    {
        // Missing Column attribute
        public string UnmappedProperty { get; set; }
    }
}