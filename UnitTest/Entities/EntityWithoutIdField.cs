// -----------------------------------------------------------------------
// <copyright file="EntityWithoutIdField.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("")]
    public class EntityWithoutIdField : BaseEntity<string>
    {
        // Hide the inherited Id property with NotMapped to prevent it from being treated as a primary key
        [NotMapped]
        public new string Id { get; set; }

        [PrimaryKey]
        public string Pk { get; set; }

        [Column("Name", System.Data.SqlDbType.Text)]
        public string Name { get; set; }
    }
}
