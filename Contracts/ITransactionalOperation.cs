//-------------------------------------------------------------------------------
// <copyright file="ITransactionalOperation.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    using System.Data;
    using System.Data.Common;

    /// <summary>
    /// Delegate for AfterCommit event.
    /// </summary>
    public delegate void AfterReadEventHandler<TInput, TOutput>(ITransactionalOperation<TInput, TOutput> sender, IDataReader reader);

    /// <summary>
    /// Defines a transactional operation that can be committed or rolled back.
    /// Generic version supports entity-specific operations with type safety.
    /// </summary>
    public interface ITransactionalOperation<TInput, TOutput>
    {
        /// <summary>
        /// Gets the unique identifier for this operation.
        /// </summary>
        string OperationId { get; }

        /// <summary>
        /// Gets the operation description for logging.
        /// </summary>
        string Description { get; }

        SqlExecMode ExecMode { get; }

        TInput Input { get; set; }

        TOutput Output { get; set; }

        /// <summary>
        /// Event raised after the commit operation has completed successfully.
        /// </summary>
        event AfterReadEventHandler<TInput, TOutput> AfterRead;

        IDbCommand CommitCommand { get; }

        /// <summary>
        /// Raises the AfterCommit event.
        /// </summary>
        void OnAfterRead(IDataReader reader);
    }
}