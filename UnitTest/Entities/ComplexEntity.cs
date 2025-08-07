//-------------------------------------------------------------------------------
// <copyright file="ComplexEntity.cs" company="Microsoft Corp.">
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
    /// Complex entity with various data types for testing.
    /// </summary>
    [Table("ComplexEntity", SoftDeleteEnabled = true)]
    public class ComplexEntity : BaseEntity<Guid>, IVersionedEntity<string>
    {
        [PrimaryKey(Order = 2)]
        [AuditField(AuditFieldType.Version)]
        [Column("Version", SqlDbType.BigInt, NotNull = true)]
        [Index("IX_CacheEntry_Version")]
        public new long Version
        {
            get => base.Version;
            set => base.Version = value;
        }

        [Column("StringField", SqlDbType.NVarChar, Size = 100)]
        public string StringField { get; set; }

        [Column("IntField", SqlDbType.Int)]
        public int IntField { get; set; }

        [Column("DecimalField", SqlDbType.Decimal, Precision = 18, Scale = 2)]
        public decimal DecimalField { get; set; }

        [Column("DateTimeField", SqlDbType.DateTime)]
        public DateTime DateTimeField { get; set; }

        [Column("DateTimeOffsetField", SqlDbType.DateTimeOffset)]
        public DateTimeOffset DateTimeOffsetField { get; set; }

        [Column("BoolField", SqlDbType.Bit)]
        public bool BoolField { get; set; }

        [Column("GuidField", SqlDbType.UniqueIdentifier)]
        public Guid GuidField { get; set; }

        [Column("ByteArrayField", SqlDbType.VarBinary)]
        public byte[] ByteArrayField { get; set; }

        [Column("NullableIntField", SqlDbType.Int)]
        public int? NullableIntField { get; set; }

        [Column("EnumField", SqlDbType.Int)]
        public TestEnum EnumField { get; set; }

        public bool IsDeleted { get; set; }
    }

    /// <summary>
    /// Test enumeration.
    /// </summary>
    public enum TestEnum
    {
        Value1 = 1,
        Value2 = 2,
        Value3 = 3
    }
}