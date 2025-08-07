//-------------------------------------------------------------------------------
// <copyright file="SequenceEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using System.Data;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    /// <summary>
    /// Entity with sequence for testing.
    /// </summary>
    [Table("EntityWithSequence")]
    public class SequenceEntity : BaseEntity<long>
    {
        [PrimaryKey(IsAutoIncrement = true, SequenceName = "SEQ_EntityId")]
        [Column("Id", SqlDbType.BigInt)]
        public new long Id { get; set; }

        [Column("Name", SqlDbType.Text)]
        public string Name { get; set; }
    }
}