// -----------------------------------------------------------------------
// <copyright file="IArchivableEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    using System;

    public interface IArchivableEntity<TKey> where TKey : IEquatable<TKey>
    {
        bool IsArchived { get; set; }
    }
}