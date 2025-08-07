//-------------------------------------------------------------------------------
// <copyright file="ComplexConstraintEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System;
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    /// <summary>
    /// Entity with complex constraints for testing.
    /// </summary>
    [Table("EntityWithComplexConstraints")]
    public class ComplexConstraintEntity : BaseEntity<string>
    {
        [Column("Email", SqlDbType.NVarChar, Size = 255)]
        [Check("Email LIKE '%@%'", Name = "CK_Email_Valid")]
        [Unique("UQ_Email")]
        public string Email { get; set; }

        [Column("Phone", SqlDbType.VarChar, Size = 20)]
        [Check("Phone REGEXP '^[0-9-]+$'", Name = "CK_Phone_Format")]
        public string Phone { get; set; }

        [Column("Status", SqlDbType.VarChar, Size = 20)]
        [Check("Status IN ('Active', 'Inactive', 'Suspended')", Name = "CK_Status_Valid")]
        [Index("IX_Status")]
        public string Status { get; set; }

        [Column("Score", SqlDbType.Int)]
        [Check("Score BETWEEN 0 AND 100", Name = "CK_Score_Range")]
        public int Score { get; set; }

        [Column("CreatedDate", SqlDbType.DateTime, DefaultValue = "CURRENT_TIMESTAMP")]
        public DateTime CreatedDate { get; set; }
    }
}