//-------------------------------------------------------------------------------
// <copyright file="EntityWithoutPrimaryKey.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    /// <summary>
    /// Test entity without primary key. 
    /// Uses property hiding with [NotMapped] to override the inherited Id property.
    /// </summary>
    [Table("EntityWithoutPrimaryKey")]
    public class EntityWithoutPrimaryKey : BaseEntity<string>
    {
        // Hide the inherited Id property with NotMapped to prevent it from being treated as a primary key
        [NotMapped]
        public new string Id { get; set; }
        
        [Column("Name", System.Data.SqlDbType.Text)]
        public string Name { get; set; }
    }
}