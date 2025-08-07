//-------------------------------------------------------------------------------
// <copyright file="SQLiteMapperTestEntity.cs" company="Microsoft Corp.">
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

    [Table("TestEntity")]
    public class SQLiteMapperTestEntity : BaseEntity<Guid>
    {
        [Column("Name", SqlDbType.NVarChar, Size = 100)]
        public string Name { get; set; }

        [Column("Count", SqlDbType.Int)]
        public int Count { get; set; }

        [Column("CreatedDate", SqlDbType.DateTime)]
        public DateTime CreatedDate { get; set; }

        [Column("Amount", SqlDbType.Decimal, Precision = 18, Scale = 6)]
        public decimal? Amount { get; set; }

        [Column("ComplexData", SqlDbType.Text)]
        public string ComplexData { get; set; }
    }

    public class SQLiteTestEntityMapper : BaseEntityMapper<SQLiteMapperTestEntity, Guid>
    {
        public PropertyInfo[] GetProperties() => this.GetPropertyMappings().Keys.ToArray();

        public string TestGetSqlType(Type type) => type.ToSqlTypeString();

        public string TestGenerateColumnName(string propertyName) => this.GetPropertyMappings()
            .FirstOrDefault(p => p.Key.Name ==propertyName).Value?.ColumnName;

        public string TestGenerateCreateTableSql() => this.GenerateCreateTableSql();

        public List<string> TestGenerateCreateIndexSql() => this.GenerateCreateIndexSql().ToList();
    }
}
