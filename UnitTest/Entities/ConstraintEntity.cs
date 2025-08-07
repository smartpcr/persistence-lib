//-------------------------------------------------------------------------------
// <copyright file="ConstraintEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("TestEntityWithConstraints")]
    public class ConstraintEntity : BaseEntity<string>
    {
        [Column("Age", SqlDbType.Int)]
        [Check("Age >= 0 AND Age <= 150", Name = "CK_Age_Range")]
        public int Age { get; set; }

        [Column("Email", SqlDbType.Text)]
        [Unique("UQ_Email")]
        public string Email { get; set; }

        [Column("Status", SqlDbType.Text, DefaultValue = "Active")]
        public string Status { get; set; }
    }
}