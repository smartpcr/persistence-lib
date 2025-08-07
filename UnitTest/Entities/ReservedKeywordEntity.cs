//-------------------------------------------------------------------------------
// <copyright file="ReservedKeywordEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("ReservedKeyword")]
    public class ReservedKeywordEntity : BaseEntity<string>
    {
        [Column("Select", SqlDbType.Text)] // Using SQL reserved keyword
        public string Select { get; set; }

        [Column("From", SqlDbType.Text)] // Using SQL reserved keyword
        public string From { get; set; }

        [Column("Where", SqlDbType.Int)] // Using SQL reserved keyword
        public int Where { get; set; }
    }
}