//-------------------------------------------------------------------------------
// <copyright file="ErrorHandlingEntities.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.ErrorHandling
{
    using System;
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("ErrorTestEntity")]
    public class ErrorTestEntity : BaseEntity<Guid>
    {
        [Column("Name", SqlDbType.NVarChar, Size = 100)]
        public string Name { get; set; }

        [Column("UniqueField", SqlDbType.NVarChar, Size = 100)]
        public string UniqueField { get; set; }

        [Column("Value", SqlDbType.Int)]
        public int Value { get; set; }
    }

    [Table("ParentEntity")]
    public class ParentEntity : BaseEntity<Guid>
    {
        [Column("Name", SqlDbType.NVarChar, Size = 100)]
        public string Name { get; set; }
    }

    [Table("ChildEntity")]
    public class ChildEntity : BaseEntity<Guid>
    {
        [Column("ParentId", SqlDbType.NVarChar, Size = 36)]
        public Guid ParentId { get; set; }

        [Column("Name", SqlDbType.NVarChar, Size = 100)]
        public string Name { get; set; }
    }
}
