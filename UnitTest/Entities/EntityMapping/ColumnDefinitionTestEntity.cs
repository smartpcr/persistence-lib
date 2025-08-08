//-------------------------------------------------------------------------------
// <copyright file="ColumnDefinitionTestEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.EntityMapping
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Reflection;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Extensions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("ColumnDefinitionTest")]
    public class ColumnDefinitionTestEntity : BaseEntity<Guid>
    {
        [PrimaryKey(Order = 1)]
        [Column("Id", SqlDbType.UniqueIdentifier, NotNull = true)]
        public new Guid Id { get; set; }

        [Column("RequiredString", SqlDbType.NVarChar, Size = 255, NotNull = true)]
        public string RequiredString { get; set; }

        [Column("OptionalString", SqlDbType.NVarChar, Size = 100, NotNull = false)]
        public string OptionalString { get; set; }

        [Column("TextColumn", SqlDbType.Text)]
        public string TextColumn { get; set; }

        [Column("IntColumn", SqlDbType.Int, NotNull = true)]
        public int IntColumn { get; set; }

        [Column("NullableInt", SqlDbType.Int, NotNull = false)]
        public int? NullableInt { get; set; }

        [Column("DecimalColumn", SqlDbType.Decimal, Precision = 10, Scale = 3, NotNull = true)]
        public decimal DecimalColumn { get; set; }

        [Column("BigDecimal", SqlDbType.Decimal, Precision = 28, Scale = 8)]
        public decimal? BigDecimal { get; set; }

        [Column("BitColumn", SqlDbType.Bit, NotNull = true)]
        public bool BitColumn { get; set; }

        [Column("DateTimeColumn", SqlDbType.DateTime, NotNull = true)]
        public DateTime DateTimeColumn { get; set; }

        [Column("DateTime2Column", SqlDbType.DateTime2)]
        public DateTime? DateTime2Column { get; set; }

        [Column("BinaryColumn", SqlDbType.VarBinary, Size = 1024)]
        public byte[] BinaryColumn { get; set; }

        [Column("MaxBinaryColumn", SqlDbType.VarBinary, Size = -1)]
        public byte[] MaxBinaryColumn { get; set; }

        [Column("FloatColumn", SqlDbType.Float)]
        public double FloatColumn { get; set; }

        [Column("RealColumn", SqlDbType.Real)]
        public float RealColumn { get; set; }

        [Column("BigIntColumn", SqlDbType.BigInt, NotNull = true)]
        public long BigIntColumn { get; set; }

        [Column("SmallIntColumn", SqlDbType.SmallInt)]
        public short SmallIntColumn { get; set; }

        [Column("TinyIntColumn", SqlDbType.TinyInt)]
        public byte TinyIntColumn { get; set; }

        [Column("UniqueIdColumn", SqlDbType.UniqueIdentifier)]
        [Unique]
        public Guid? UniqueIdColumn { get; set; }

        [Index("IX_IndexedColumn")]
        [Column("IndexedColumn", SqlDbType.NVarChar, Size = 50)]
        public string IndexedColumn { get; set; }

        [Column("ComputedColumn", SqlDbType.DateTime)]
        public DateTime ComputedColumn { get; set; }

        [ForeignKey("OtherEntity", "Id")]
        [Column("ForeignKeyColumn", SqlDbType.UniqueIdentifier)]
        public Guid? ForeignKeyColumn { get; set; }

        [NotMapped]
        public string IgnoredProperty { get; set; }
    }

    public class ColumnDefinitionTestEntityMapper : BaseEntityMapper<ColumnDefinitionTestEntity, Guid>
    {
        public IReadOnlyDictionary<PropertyInfo, PropertyMapping> GetColumnMappings() => this.GetPropertyMappings();

        public string TestGenerateCreateTableSql() => this.GenerateCreateTableSql();

        public List<string> TestGenerateCreateIndexSql() => this.GenerateCreateIndexSql().ToList();
    }
}