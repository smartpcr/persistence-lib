//-------------------------------------------------------------------------------
// <copyright file="TransactionScope.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SQLite;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Traces;

    /// <summary>
    /// Implementation of ITransactionScope that manages transactional operations.
    /// Created by persistence provider and handles SQL translation internally.
    /// </summary>
    public class TransactionScope : ITransactionScope
    {
        private readonly string connectionString;
        private readonly ConcurrentQueue<(IDbCommand cmd, SqlExecMode execMode, Action<IDataReader> action)> commands;
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
            this.commands = new ConcurrentQueue<(IDbCommand cmd, SqlExecMode execMode, Action<IDataReader> action)>();

            PersistenceLogger.TransactionStart();
        }

        public void AddOperation<T, TKey>(ITransactionalOperation<T, T> operation)
            where T : class, IEntity<TKey>
            where TKey : IEquatable<TKey>
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            if (this.State != TransactionState.Active)
                throw new InvalidOperationException($"Cannot add operations to a {this.State} transaction.");

            lock (this.lockObject)
            {
                Action<IDataReader> action = null;
                if (operation.ExecMode == SqlExecMode.ExecuteReader)
                {
                    action = operation.OnAfterRead;
                }

                this.commands.Enqueue((operation.CommitCommand, operation.ExecMode, action));
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

        private async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (this.State != TransactionState.Active)
                throw new InvalidOperationException($"Cannot execute a {this.State} transaction.");

            this.State = TransactionState.Committing;

            await using var connection = new SQLiteConnection(this.connectionString);
            // Always pass cancellation token to OpenAsync for proper cancellation support
            await connection.OpenAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction();

            try
            {
                // Execute forward operations in sequence, chaining outputs to inputs
                lock (this.lockObject)
                {
                    foreach (var (command, execMode, action) in this.commands)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        command.Connection = connection;
                        command.Transaction = transaction;

                        switch (execMode)
                        {
                            case SqlExecMode.ExecuteNonQuery:
                                command.ExecuteNonQuery();
                                break;
                            case SqlExecMode.ExecuteReader:
                                using (var reader = command.ExecuteReader())
                                {
                                    action?.Invoke(reader);
                                }
                                break;
                            case SqlExecMode.ExecuteScalar:
                                command.ExecuteScalar();
                                break;
                        }
                    }
                }

                transaction.Commit();
                this.State = TransactionState.Committed;
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

            var operationList = new List<IDbCommand>();
            lock (this.lockObject)
            {
                operationList.AddRange(this.commands.Select(c => c.cmd));
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