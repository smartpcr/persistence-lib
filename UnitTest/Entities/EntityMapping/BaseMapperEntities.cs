//-------------------------------------------------------------------------------
// <copyright file="BaseMapperEntities.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.EntityMapping
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Extensions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    #region entities
    [Table("TestEntity")]
    public class BaseMapperTestEntity : BaseEntity<Guid>
    {
        public new Guid Id { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
        public DateTime CreatedDate { get; set; }
        public decimal? Amount { get; set; }
        [NotMapped]
        public string Ignored { get; set; }
    }

    [Table("TestEntity", SoftDeleteEnabled = true)]
    public class BaseMapperSoftDeleteEntity : BaseMapperTestEntity, IVersionedEntity<Guid>
    {
        public bool IsDeleted { get; set; }
    }

    [Table("TestEntity", ExpirySpanString = "01:00:00")]
    public class BaseMapperExpiryEntity : BaseMapperTestEntity, IExpirableEntity<Guid>
    {
        public DateTimeOffset? AbsoluteExpiration { get; set; }
    }
    #endregion

    #region entity mappers
    public class TestEntityMapper : BaseEntityMapper<BaseMapperTestEntity, Guid>
    {
        public PropertyInfo[] GetProperties() => this.GetPropertyMappings().Keys.ToArray();

        public string TestGetSqlType(Type type) => type.ToSqlTypeString();

        public string TestGenerateColumnName(string propertyName) => this.GetPropertyMappings()
            .FirstOrDefault(p => p.Key.Name ==propertyName).Value?.ColumnName;

        public string TestGenerateCreateTableSql() => this.GenerateCreateTableSql();

        public List<string> TestGenerateCreateIndexSql() => this.GenerateCreateIndexSql().ToList();
    }

    public class TestEntityMapperWithSoftDelete : BaseEntityMapper<BaseMapperSoftDeleteEntity, Guid>
    {
        public string TestGenerateCreateTableSql() => this.GenerateCreateTableSql();
    }

    public class TestEntityMapperWithExpiry : BaseEntityMapper<BaseMapperExpiryEntity, Guid>
    {
        public string TestGenerateCreateTableSql() => this.GenerateCreateTableSql();
    }
    #endregion
}
