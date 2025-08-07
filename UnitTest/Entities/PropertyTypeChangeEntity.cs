//-------------------------------------------------------------------------------
// <copyright file="PropertyTypeChangeEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    /// <summary>
    /// Test entity that hides a regular property with a different type.
    /// </summary>
    [Table("TestPropertyTypeChange")]
    public class PropertyTypeChangeEntity : BaseEntity<string>
    {
        [Column("Data", SqlDbType.Text)]
        public new string Version { get; set; }
    }
}