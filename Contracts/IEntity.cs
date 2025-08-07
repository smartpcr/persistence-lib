//-------------------------------------------------------------------------------
// <copyright file="IEntity.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    using System;

    /// <summary>
    /// Defines the contract for persistable entities with strongly-typed keys.
    /// </summary>
    /// <typeparam name="TKey">The primary key type</typeparam>
    public interface IEntity<TKey> where TKey : IEquatable<TKey>
    {
        /// <summary>
        /// Gets or sets the unique identifier for the entity.
        /// </summary>
        TKey Id { get; set; }

        /// <summary>
        /// Gets or sets the version number of the entity, it serves as a concurrency token.
        /// It's a global sequence number when soft delete is enabled.
        /// </summary>
        long Version { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the entity was created.
        /// </summary>
        DateTimeOffset CreatedTime { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the entity was last modified.
        /// </summary>
        DateTimeOffset LastWriteTime { get; set; }

        long EstimateEntitySize();
    }
}
