//-------------------------------------------------------------------------------
// <copyright file="TransactionalOperation.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite
{
    using System;
    using System.Data;
    using Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    /// <summary>
    /// Concrete implementation of a transactional operation
    /// </summary>
    public sealed class TransactionalOperation<TInput, TOutput> : ITransactionalOperation<TInput, TOutput>
    {
        public string OperationId { get; private set; }
        public string Description { get; private set; }
        public SqlExecMode ExecMode { get; private set; }
        public TInput Input { get; set; }
        public TOutput Output { get; set; }
        public IDbCommand CommitCommand { get; private set; }

        public event AfterReadEventHandler<TInput, TOutput> AfterRead;

        private TransactionalOperation()
        {
        }

        public static TransactionalOperation<T, T> Create<T, TKey>(
            IEntityMapper<T, TKey> entityMapper,
            DbOperationType opType,
            T fromValue,
            T toValue = null)
            where T : class, IEntity<TKey>
            where TKey : IEquatable<TKey>
        {
            var transactionalOperation = new TransactionalOperation<T, T>();
            transactionalOperation.Input = fromValue;
            transactionalOperation.Output = toValue;
            var mapper = entityMapper;

            transactionalOperation.OperationId = Guid.NewGuid().ToString();
            transactionalOperation.Description = $"{opType} operation for entity type {typeof(T).Name}";
            switch (opType)
            {
                case DbOperationType.Select:
                    transactionalOperation.ExecMode = SqlExecMode.ExecuteReader;
                    transactionalOperation.CommitCommand = mapper.CreateCommand(
                        DbOperationType.Select,
                        CommandContext<T, TKey>.ForSelect(fromValue.Id));
                    transactionalOperation.AfterRead += (_, reader) =>
                    {
                        if (reader.Read())
                        {
                            var output = mapper.MapFromReader(reader);
                            transactionalOperation.Output = output;
                        }
                        else
                        {
                            transactionalOperation.Output = null; // No results found
                        }
                    };
                    break;
                case DbOperationType.Insert:
                    transactionalOperation.ExecMode = SqlExecMode.ExecuteNonQuery;
                    transactionalOperation.CommitCommand = mapper.CreateCommand(
                        DbOperationType.Insert,
                        CommandContext<T, TKey>.ForInsert(fromValue));
                    break;
                case DbOperationType.Update:
                    transactionalOperation.ExecMode = SqlExecMode.ExecuteNonQuery;
                    transactionalOperation.CommitCommand = mapper.CreateCommand(
                        DbOperationType.Update,
                        CommandContext<T, TKey>.ForUpdate(toValue, fromValue));
                    break;
                case DbOperationType.Delete:
                    transactionalOperation.ExecMode = SqlExecMode.ExecuteNonQuery;
                    transactionalOperation.CommitCommand = mapper.CreateCommand(
                        DbOperationType.Delete,
                        CommandContext<T, TKey>.ForDelete(fromValue.Id));
                    break;
            }

            return transactionalOperation;
        }

        /// <summary>
        /// Raises the AfterCommit event.
        /// </summary>
        public void OnAfterRead(IDataReader reader)
        {
            this.AfterRead?.Invoke(this, reader);
        }
    }
}