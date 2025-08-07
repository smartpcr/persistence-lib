//-------------------------------------------------------------------------------
// <copyright file="ComputedColumnEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("TestEntityComputed")]
    public class ComputedColumnEntity : BaseEntity<string>
    {
        [Column("FirstName", SqlDbType.Text)]
        public string FirstName { get; set; }

        [Column("LastName", SqlDbType.Text)]
        public string LastName { get; set; }

        [Column("FullName", SqlDbType.Text)]
        [Computed("FirstName || ' ' || LastName")]
        public string FullName { get; set; }
    }
}