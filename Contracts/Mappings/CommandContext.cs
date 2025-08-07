//-------------------------------------------------------------------------------
// <copyright file="CommandContext.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Context class for command creation that encapsulates all parameters needed for database operations.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TKey">The key type.</typeparam>
    public class CommandContext<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        /// <summary>
        /// Gets or sets the entity ID for operations that require it.
        /// </summary>
        public TKey Id { get; set; }

        /// <summary>
        /// Gets or sets the entity for insert/update operations.
        /// </summary>
        public T Entity { get; set; }

        /// <summary>
        /// Gets or sets the old entity for optimistic concurrency control.
        /// </summary>
        public T OldEntity { get; set; }

        /// <summary>
        /// Gets or sets multiple entities for batch operations.
        /// </summary>
        public IEnumerable<T> Entities { get; set; }

        /// <summary>
        /// Gets or sets select options for query operations.
        /// </summary>
        public SelectOptions SelectOptions { get; set; }

        /// <summary>
        /// Gets or sets additional WHERE clause parameters.
        /// </summary>
        public Dictionary<string, object> WhereParameters { get; set; }

        /// <summary>
        /// Gets or sets the command timeout in seconds.
        /// </summary>
        public int? CommandTimeout { get; set; }

        /// <summary>
        /// Gets or sets the transaction for the command.
        /// </summary>
        public System.Data.IDbTransaction Transaction { get; set; }

        /// <summary>
        /// Creates a context for a select by ID operation.
        /// </summary>
        public static CommandContext<T, TKey> ForSelect(TKey id, SelectOptions options = null)
        {
            return new CommandContext<T, TKey>
            {
                Id = id,
                SelectOptions = options ?? new SelectOptions()
            };
        }

        /// <summary>
        /// Creates a context for an insert operation.
        /// </summary>
        public static CommandContext<T, TKey> ForInsert(T entity)
        {
            return new CommandContext<T, TKey>
            {
                Entity = entity
            };
        }

        /// <summary>
        /// Creates a context for an update operation.
        /// </summary>
        public static CommandContext<T, TKey> ForUpdate(T entity, T oldEntity = null)
        {
            return new CommandContext<T, TKey>
            {
                Entity = entity,
                OldEntity = oldEntity
            };
        }

        /// <summary>
        /// Creates a context for a delete operation.
        /// </summary>
        public static CommandContext<T, TKey> ForDelete(TKey id, T entity = null)
        {
            return new CommandContext<T, TKey>
            {
                Id = id,
                Entity = entity
            };
        }

        /// <summary>
        /// Creates a context for a batch insert operation.
        /// </summary>
        public static CommandContext<T, TKey> ForBatchInsert(IEnumerable<T> entities)
        {
            return new CommandContext<T, TKey>
            {
                Entities = entities
            };
        }
    }
}