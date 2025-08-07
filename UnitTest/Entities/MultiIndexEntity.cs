//-------------------------------------------------------------------------------
// <copyright file="MultiIndexEntity.cs" company="Microsoft Corp.">
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
    /// Entity with multiple indexes for testing.
    /// </summary>
    [Table("EntityWithMultipleIndexes")]
    public class MultiIndexEntity : BaseEntity<string>
    {
        [Column("Field1", SqlDbType.Text)]
        [Index("IX_Field1")]
        [Index("IX_Composite_1_2", Order = 1)]
        public string Field1 { get; set; }

        [Column("Field2", SqlDbType.Int)]
        [Index("IX_Field2")]
        [Index("IX_Composite_1_2", Order = 2)]
        [Index("IX_Composite_2_3", Order = 1)]
        public int Field2 { get; set; }

        [Column("Field3", SqlDbType.DateTime)]
        [Index("IX_Field3", IsUnique = true)]
        [Index("IX_Composite_2_3", Order = 2)]
        public DateTime Field3 { get; set; }

        [Column("Field4", SqlDbType.Text)]
        [Index("IX_Included", Order = 1)]
        public string Field4 { get; set; }

        [Column("Field5", SqlDbType.Text)]
        [Index("IX_Included", IsIncluded = true)]
        public string Field5 { get; set; }
    }
}