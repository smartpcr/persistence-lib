//-------------------------------------------------------------------------------
// <copyright file="ITransactionScope.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Defines a transaction scope that manages a collection of transactional operations.
    /// The scope is created by a persistence provider and handles SQL translation internally.
    /// </summary>
    public interface ITransactionScope : IAsyncDisposable
    {
        /// <summary>
        /// Gets the unique transaction identifier.
        /// </summary>
        string TransactionId { get; }

        /// <summary>
        /// Gets the current state of the transaction.
        /// </summary>
        TransactionState State { get; }

        /// <summary>
        /// Gets the time when the transaction started.
        /// </summary>
        DateTimeOffset StartTime { get; }

        /// <summary>
        /// Adds a forward operation to the transaction.
        /// Operations are chained - output of one becomes input of the next.
        /// </summary>
        /// <param name="operation">The forward operation</param>
        void AddOperation<T, TKey>(ITransactionalOperation<T, T> operation)
            where T : class, IEntity<TKey>
            where TKey : IEquatable<TKey>;

        void Commit();

        void Rollback();
    }
}