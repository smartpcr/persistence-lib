//-------------------------------------------------------------------------------
// <copyright file="EntityWithoutTableAttribute.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities
{
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

    public class EntityWithoutTableAttribute : BaseEntity<string>
    {
        public string Name { get; set; }
    }
}