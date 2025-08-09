// -----------------------------------------------------------------------
// <copyright file="ResilientSQLiteCommand.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Resilience
{
    using System;
    using System.Data;
    using System.Data.SQLite;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Wrapper for SQLiteCommand that provides automatic retry logic for transient errors.
    /// </summary>
    public class ResilientSQLiteCommand : IAsyncDisposable, IDisposable
    {
        private readonly SQLiteCommand command;
        private readonly RetryPolicy retryPolicy;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResilientSQLiteCommand"/> class.
        /// </summary>
        public ResilientSQLiteCommand(SQLiteCommand command, RetryPolicy retryPolicy = null)
        {
            this.command = command ?? throw new ArgumentNullException(nameof(command));
            this.retryPolicy = retryPolicy;
        }

        /// <summary>
        /// Gets or sets the underlying SQLite command.
        /// </summary>
        public SQLiteCommand Command => this.command;

        /// <summary>
        /// Gets or sets the command text.
        /// </summary>
        public string CommandText
        {
            get => this.command.CommandText;
            set => this.command.CommandText = value;
        }

        /// <summary>
        /// Gets or sets the command type.
        /// </summary>
        public CommandType CommandType
        {
            get => this.command.CommandType;
            set => this.command.CommandType = value;
        }

        /// <summary>
        /// Gets or sets the connection.
        /// </summary>
        public SQLiteConnection Connection
        {
            get => this.command.Connection;
            set => this.command.Connection = value;
        }

        /// <summary>
        /// Gets or sets the transaction.
        /// </summary>
        public SQLiteTransaction Transaction
        {
            get => this.command.Transaction;
            set => this.command.Transaction = value;
        }

        /// <summary>
        /// Gets the parameter collection.
        /// </summary>
        public SQLiteParameterCollection Parameters => this.command.Parameters;

        /// <summary>
        /// Executes a non-query command with retry logic.
        /// </summary>
        public async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default)
        {
            if (this.retryPolicy != null)
            {
                return await this.retryPolicy.ExecuteAsync(
                    async () => await this.command.ExecuteNonQueryAsync(cancellationToken),
                    cancellationToken);
            }

            return await this.command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Executes a non-query command synchronously with retry logic.
        /// </summary>
        public int ExecuteNonQuery()
        {
            if (this.retryPolicy != null)
            {
                return this.retryPolicy.Execute(() => this.command.ExecuteNonQuery());
            }

            return this.command.ExecuteNonQuery();
        }

        /// <summary>
        /// Executes a scalar command with retry logic.
        /// </summary>
        public async Task<object> ExecuteScalarAsync(CancellationToken cancellationToken = default)
        {
            if (this.retryPolicy != null)
            {
                return await this.retryPolicy.ExecuteAsync(
                    async () => await this.command.ExecuteScalarAsync(cancellationToken),
                    cancellationToken);
            }

            return await this.command.ExecuteScalarAsync(cancellationToken);
        }

        /// <summary>
        /// Executes a scalar command synchronously with retry logic.
        /// </summary>
        public object ExecuteScalar()
        {
            if (this.retryPolicy != null)
            {
                return this.retryPolicy.Execute(() => this.command.ExecuteScalar());
            }

            return this.command.ExecuteScalar();
        }

        /// <summary>
        /// Executes a reader command with retry logic.
        /// Note: The reader itself is not wrapped, only the initial connection/execution is retried.
        /// </summary>
        public async Task<SQLiteDataReader> ExecuteReaderAsync(CancellationToken cancellationToken = default)
        {
            if (this.retryPolicy != null)
            {
                return await this.retryPolicy.ExecuteAsync(
                    async () => (SQLiteDataReader)await this.command.ExecuteReaderAsync(cancellationToken),
                    cancellationToken);
            }

            return (SQLiteDataReader)await this.command.ExecuteReaderAsync(cancellationToken);
        }

        /// <summary>
        /// Executes a reader command synchronously with retry logic.
        /// </summary>
        public SQLiteDataReader ExecuteReader()
        {
            if (this.retryPolicy != null)
            {
                return this.retryPolicy.Execute(() => this.command.ExecuteReader());
            }

            return this.command.ExecuteReader();
        }

        /// <summary>
        /// Creates a resilient SQLite command.
        /// </summary>
        public static ResilientSQLiteCommand Create(string commandText, SQLiteConnection connection, RetryPolicy retryPolicy = null)
        {
            var command = new SQLiteCommand(commandText, connection);
            return new ResilientSQLiteCommand(command, retryPolicy);
        }

        /// <summary>
        /// Creates a resilient SQLite command with transaction.
        /// </summary>
        public static ResilientSQLiteCommand Create(string commandText, SQLiteConnection connection, SQLiteTransaction transaction, RetryPolicy retryPolicy = null)
        {
            var command = new SQLiteCommand(commandText, connection, transaction);
            return new ResilientSQLiteCommand(command, retryPolicy);
        }

        /// <summary>
        /// Disposes the underlying command.
        /// </summary>
        public void Dispose()
        {
            this.command?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (this.command != null)
            {
                await this.command.DisposeAsync();
            }
        }
    }
}