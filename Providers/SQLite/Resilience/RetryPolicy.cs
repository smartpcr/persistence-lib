// -----------------------------------------------------------------------
// <copyright file="RetryPolicy.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Resilience
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Traces;

    /// <summary>
    /// Provides retry logic for transient SQLite errors.
    /// </summary>
    public class RetryPolicy
    {
        private readonly int maxRetryAttempts;
        private readonly TimeSpan initialDelay;
        private readonly TimeSpan maxDelay;
        private readonly double backoffMultiplier;

        /// <summary>
        /// Initializes a new instance of the <see cref="RetryPolicy"/> class.
        /// </summary>
        /// <param name="maxRetryAttempts">Maximum number of retry attempts. Default is 3.</param>
        /// <param name="initialDelay">Initial delay between retries. Default is 100ms.</param>
        /// <param name="maxDelay">Maximum delay between retries. Default is 5 seconds.</param>
        /// <param name="backoffMultiplier">Backoff multiplier for exponential backoff. Default is 2.</param>
        public RetryPolicy(
            int maxRetryAttempts = 3,
            TimeSpan? initialDelay = null,
            TimeSpan? maxDelay = null,
            double backoffMultiplier = 2.0)
        {
            this.maxRetryAttempts = maxRetryAttempts;
            this.initialDelay = initialDelay ?? TimeSpan.FromMilliseconds(100);
            this.maxDelay = maxDelay ?? TimeSpan.FromSeconds(5);
            this.backoffMultiplier = backoffMultiplier;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RetryPolicy"/> class from a RetryConfiguration.
        /// </summary>
        /// <param name="configuration">The retry configuration to use.</param>
        public RetryPolicy(RetryConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            configuration.Validate();

            this.maxRetryAttempts = configuration.MaxAttempts;
            this.initialDelay = configuration.InitialDelay;
            this.maxDelay = configuration.MaxDelay;
            this.backoffMultiplier = configuration.BackoffMultiplier;
        }

        /// <summary>
        /// Creates a RetryPolicy from configuration, or returns null if retry is disabled.
        /// </summary>
        /// <param name="configuration">The retry configuration.</param>
        /// <returns>A RetryPolicy instance if enabled, null otherwise.</returns>
        public static RetryPolicy FromConfiguration(RetryConfiguration configuration)
        {
            if (configuration == null || !configuration.Enabled || configuration.MaxAttempts == 0)
            {
                return null;
            }

            return new RetryPolicy(configuration);
        }

        /// <summary>
        /// Executes an async operation with retry logic for transient errors.
        /// </summary>
        public async Task<TResult> ExecuteAsync<TResult>(
            Func<Task<TResult>> operation,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            var attempt = 0;
            var delay = this.initialDelay;
            var startTime = Stopwatch.GetTimestamp();

            while (true)
            {
                try
                {
                    attempt++;
                    var result = await operation().ConfigureAwait(false);
                    
                    // Log success if there were retries
                    if (attempt > 1)
                    {
                        var elapsedMs = (long)((Stopwatch.GetTimestamp() - startTime) * 1000.0 / Stopwatch.Frequency);
                        PersistenceEventSource.Log.RetrySucceeded(attempt, elapsedMs, callerFile, callerMember, callerLine);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    var (isTransient, errorDetails) = SQLiteTransientErrorDetector.IsTransientError(ex);
                    
                    if (attempt <= this.maxRetryAttempts && isTransient)
                    {
                        // Log transient error
                        PersistenceEventSource.Log.TransientErrorDetected(
                            ex.GetType().Name, 
                            ex.Message, 
                            attempt, 
                            this.maxRetryAttempts, 
                            errorDetails, 
                            callerFile, 
                            callerMember, 
                            callerLine);
                        
                        if (attempt == this.maxRetryAttempts)
                        {
                            // Log retry exhausted
                            var elapsedMs = (long)((Stopwatch.GetTimestamp() - startTime) * 1000.0 / Stopwatch.Frequency);
                            PersistenceEventSource.Log.RetryExhausted(
                                attempt, 
                                elapsedMs, 
                                ex.GetType().Name, 
                                ex.Message, 
                                errorDetails, 
                                callerFile, 
                                callerMember, 
                                callerLine);
                            throw;
                        }

                        // Add jitter to prevent thundering herd problem
                        var jitter = TimeSpan.FromMilliseconds(new Random().Next(0, 100));
                        var delayWithJitter = delay + jitter;
                        
                        // Log retry operation
                        PersistenceEventSource.Log.RetryingOperation(
                            attempt + 1, 
                            this.maxRetryAttempts, 
                            (long)delayWithJitter.TotalMilliseconds, 
                            callerFile, 
                            callerMember, 
                            callerLine);

                        await Task.Delay(delayWithJitter, cancellationToken).ConfigureAwait(false);

                        // Calculate next delay with exponential backoff
                        delay = TimeSpan.FromMilliseconds(Math.Min(
                            delay.TotalMilliseconds * this.backoffMultiplier,
                            this.maxDelay.TotalMilliseconds));
                    }
                    else
                    {
                        // Log non-transient error
                        PersistenceEventSource.Log.NonTransientErrorDetected(
                            ex.GetType().Name, 
                            ex.Message, 
                            errorDetails, 
                            callerFile, 
                            callerMember, 
                            callerLine);
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Executes an async operation with retry logic for transient errors (void return).
        /// </summary>
        public async Task ExecuteAsync(
            Func<Task> operation,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            await ExecuteAsync(async () =>
            {
                await operation().ConfigureAwait(false);
                return true;
            }, cancellationToken, callerFile, callerMember, callerLine).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a synchronous operation with retry logic for transient errors.
        /// </summary>
        public TResult Execute<TResult>(
            Func<TResult> operation,
            [CallerFilePath] string callerFile = "",
            [CallerMemberName] string callerMember = "",
            [CallerLineNumber] int callerLine = 0)
        {
            var attempt = 0;
            var delay = this.initialDelay;
            var startTime = Stopwatch.GetTimestamp();

            while (true)
            {
                try
                {
                    attempt++;
                    var result = operation();
                    
                    // Log success if there were retries
                    if (attempt > 1)
                    {
                        var elapsedMs = (long)((Stopwatch.GetTimestamp() - startTime) * 1000.0 / Stopwatch.Frequency);
                        PersistenceEventSource.Log.RetrySucceeded(attempt, elapsedMs, callerFile, callerMember, callerLine);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    var (isTransient, errorDetails) = SQLiteTransientErrorDetector.IsTransientError(ex);
                    
                    if (attempt <= this.maxRetryAttempts && isTransient)
                    {
                        // Log transient error
                        PersistenceEventSource.Log.TransientErrorDetected(
                            ex.GetType().Name, 
                            ex.Message, 
                            attempt, 
                            this.maxRetryAttempts, 
                            errorDetails, 
                            callerFile, 
                            callerMember, 
                            callerLine);
                        
                        if (attempt == this.maxRetryAttempts)
                        {
                            // Log retry exhausted
                            var elapsedMs = (long)((Stopwatch.GetTimestamp() - startTime) * 1000.0 / Stopwatch.Frequency);
                            PersistenceEventSource.Log.RetryExhausted(
                                attempt, 
                                elapsedMs, 
                                ex.GetType().Name, 
                                ex.Message, 
                                errorDetails, 
                                callerFile, 
                                callerMember, 
                                callerLine);
                            throw;
                        }

                        // Add jitter to prevent thundering herd problem
                        var jitter = TimeSpan.FromMilliseconds(new Random().Next(0, 100));
                        var delayWithJitter = delay + jitter;
                        
                        // Log retry operation
                        PersistenceEventSource.Log.RetryingOperation(
                            attempt + 1, 
                            this.maxRetryAttempts, 
                            (long)delayWithJitter.TotalMilliseconds, 
                            callerFile, 
                            callerMember, 
                            callerLine);

                        Thread.Sleep(delayWithJitter);

                        // Calculate next delay with exponential backoff
                        delay = TimeSpan.FromMilliseconds(Math.Min(
                            delay.TotalMilliseconds * this.backoffMultiplier,
                            this.maxDelay.TotalMilliseconds));
                    }
                    else
                    {
                        // Log non-transient error
                        PersistenceEventSource.Log.NonTransientErrorDetected(
                            ex.GetType().Name, 
                            ex.Message, 
                            errorDetails, 
                            callerFile, 
                            callerMember, 
                            callerLine);
                        throw;
                    }
                }
            }
        }

    }
}