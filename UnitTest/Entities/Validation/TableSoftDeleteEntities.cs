//-------------------------------------------------------------------------------
// <copyright file="TableSoftDeleteEntities.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.Validation
{
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    [Table("TestTable", SoftDeleteEnabled = false)]
    public class ClassWithSoftDeleteDisabled
    {
        public string Id { get; set; }
    }

    [Table("TestTable", SoftDeleteEnabled = true)]
    public class ValidClassWithSoftDeleteEnabled : BaseEntity<string>, IVersionedEntity<string>
    {
        public bool IsDeleted { get; set; }
    }

    [Table("TestTable", SoftDeleteEnabled = true)]
    public class ClassWithSoftDeleteEnabledButNoVersion
    {
        public string Id { get; set; }
    }

    [Table("TestTable", SoftDeleteEnabled = true)]
    public class ClassWithWrongVersionType
    {
        public string Id { get; set; }
        public int Version { get; set; }
    }

    [Table("TestTable", SoftDeleteEnabled = true)]
    public class ClassWithReadOnlyVersion
    {
        public string Id { get; set; }
        public long Version { get; }
    }

    public class BaseClassWithVersion
    {
        public long Version { get; set; }
    }

    [Table("TestTable", SoftDeleteEnabled = true)]
    public class InheritedClassWithVersion : BaseClassWithVersion
    {
        public string Id { get; set; }
    }
}
