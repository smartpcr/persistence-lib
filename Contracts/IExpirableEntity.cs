// -----------------------------------------------------------------------
// <copyright file="IExpirableEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    using System;

    public interface IExpirableEntity<TKey> where TKey : IEquatable<TKey>
    {
        DateTimeOffset? AbsoluteExpiration { get; set; }
    }
}