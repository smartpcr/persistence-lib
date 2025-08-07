//-------------------------------------------------------------------------------
// <copyright file="SqlInjectionTestEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("SqlInjectionTest")]
    public class SqlInjectionTestEntity : BaseEntity<string>
    {
        [Column("Name", SqlDbType.Text)]
        public string Name { get; set; }

        [Column("Description", SqlDbType.Text)]
        public string Description { get; set; }
    }
}