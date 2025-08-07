//-------------------------------------------------------------------------------
// <copyright file="InvalidSqlTypeEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("InvalidSqlType")]
    public class InvalidSqlTypeEntity : BaseEntity<string>
    {
        [Column("InvalidType", SqlDbType.Xml)] // SQLite doesn't support XML
        public string InvalidType { get; set; }
    }
}