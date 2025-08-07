//-------------------------------------------------------------------------------
// <copyright file="PropertyHidingEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    /// <summary>
    /// Test entity that hides a base property with NotMapped.
    /// </summary>
    [Table("TestPropertyHiding")]
    public class PropertyHidingEntity : BaseEntity<string>
    {
        [NotMapped]
        public new string Id { get; set; }

        [NotMapped]
        public new long Version { get; set; }

        [Column("TestId", SqlDbType.Text)]
        [PrimaryKey]
        public string TestId { get; set; }

        [Column("Value", SqlDbType.Text)]
        public string Value { get; set; }
    }
}