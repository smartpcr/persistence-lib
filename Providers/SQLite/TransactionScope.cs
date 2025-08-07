//-------------------------------------------------------------------------------
// <copyright file="TransactionScope.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite
{
    using System;
    using System.Collections.Generic;
    using System.Data.SQLite;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts;
    using Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Traces;

    /// <summary>
    /// Implementation of ITransactionScope that manages transactional operations.
    /// Created by persistence provider and handles SQL translation internally.
    /// </summary>
    public class TransactionScope<T, TKey> : ITransactionScope<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        private readonly string connectionString;
        private readonly List<ITransactionalOperation<T, T>> operations = new List<ITransactionalOperation<T, T>>();
        private readonly IEntityMapper<T, TKey> mapper = new SQLiteEntityMapper<T, TKey>();
        private readonly object lockObject = new object();
        private bool disposed;
        private bool shouldCommit = true; // Default to commit unless explicitly rolled back

        public string TransactionId { get; }
        public TransactionState State { get; private set; }
        public DateTimeOffset StartTime { get; }

        public TransactionScope(string connectionString)
        {
            this.connectionString = connectionString;
            this.TransactionId = Guid.NewGuid().ToString();
            this.State = TransactionState.Active;
            this.StartTime = DateTimeOffset.UtcNow;

            PersistenceLogger.TransactionStart();
        }

        public void AddOperation(ITransactionalOperation<T, T> operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            if (this.State != TransactionState.Active)
                throw new InvalidOperationException($"Cannot add operations to a {this.State} transaction.");

            lock (this.lockObject)
            {
                this.operations.Add(operation);
            }
        }

        public void AddOperations(IEnumerable<ITransactionalOperation<T, T>> operations)
        {
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));

            if (this.State != TransactionState.Active)
                throw new InvalidOperationException($"Cannot add operations to a {this.State} transaction.");

            lock (this.lockObject)
            {
                this.operations.AddRange(operations);
            }
        }

        /// <summary>
        /// Marks the transaction for rollback. The actual rollback will occur during disposal.
        /// </summary>
        public void Rollback()
        {
            if (this.State != TransactionState.Active)
                throw new InvalidOperationException($"Cannot rollback a {this.State} transaction.");

            this.shouldCommit = false;
            PersistenceLogger.TransactionRollback();
        }

        /// <summary>
        /// Marks the transaction for commit. This is the default behavior.
        /// </summary>
        public void Commit()
        {
            if (this.State != TransactionState.Active)
                throw new InvalidOperationException($"Cannot commit a {this.State} transaction.");

            this.shouldCommit = true;
            PersistenceLogger.TransactionCommit();
        }

        private async Task<bool> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (this.State != TransactionState.Active)
                throw new InvalidOperationException($"Cannot execute a {this.State} transaction.");

            this.State = TransactionState.Committing;

            using var connection = new SQLiteConnection(this.connectionString);
            // Always pass cancellation token to OpenAsync for proper cancellation support
            await connection.OpenAsync(cancellationToken);
            using var transaction = connection.BeginTransaction();

            try
            {
                // Execute forward operations in sequence, chaining outputs to inputs
                lock (this.lockObject)
                {
                    foreach (var transactionalOperation in this.operations)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Fire BeforeCommit event using the proper method
                        transactionalOperation.OnBeforeCommit();

                        var cmd = transactionalOperation.CommitCommand;
                        cmd.Connection = connection;
                        
                        switch (transactionalOperation.ExecMode)
                        {
                            case SqlExecMode.ExecuteReader:
                                var reader = cmd.ExecuteReader();
                                var result = this.mapper.MapFromReader(reader);
                                transactionalOperation.Output = result;
                                break;
                            case SqlExecMode.ExecuteNonQuery:
                                cmd.ExecuteNonQuery();
                                break;
                            case SqlExecMode.ExecuteScalar:
                                cmd.ExecuteScalar();
                                break;
                        }

                        // Fire AfterCommit event using the proper method
                        transactionalOperation.OnAfterCommit();
                    }
                }

                transaction.Commit();
                this.State = TransactionState.Committed;
                return true;
            }
            catch (Exception ex)
            {
                this.State = TransactionState.RollingBack;
                PersistenceLogger.TransactionFailed(ex);
                transaction.Rollback();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (this.disposed)
                return;

            var operationList = new List<ITransactionalOperation<T, T>>();
            lock (this.lockObject)
            {
                operationList.AddRange(this.operations);
            }

            if (this.State == TransactionState.Active && operationList.Count > 0)
            {
                if (this.shouldCommit)
                {
                    await this.ExecuteAsync(CancellationToken.None);
                }
            }

            this.disposed = true;
        }
    }
}