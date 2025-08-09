// -----------------------------------------------------------------------
// <copyright file="RetryConfiguration.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config
{
    using System;

    /// <summary>
    /// Configuration for retry policy behavior in SQLite operations.
    /// </summary>
    public class RetryConfiguration
    {
        /// <summary>
        /// Enable or disable automatic retry for transient errors.
        /// Default is true. When false, transient errors will immediately fail without retry.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Maximum number of retry attempts for transient errors.
        /// Default is 3. Set to 0 to disable retries.
        /// </summary>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>
        /// Initial delay between retry attempts in milliseconds.
        /// Default is 100ms. This delay will increase exponentially with each retry.
        /// </summary>
        public int InitialDelayMs { get; set; } = 100;

        /// <summary>
        /// Maximum delay between retry attempts in milliseconds.
        /// Default is 5000ms (5 seconds). The exponential backoff will not exceed this value.
        /// </summary>
        public int MaxDelayMs { get; set; } = 5000;

        /// <summary>
        /// Backoff multiplier for exponential retry delays.
        /// Default is 2.0, meaning each retry waits twice as long as the previous one.
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Gets the initial delay as a TimeSpan.
        /// </summary>
        public TimeSpan InitialDelay => TimeSpan.FromMilliseconds(InitialDelayMs);

        /// <summary>
        /// Gets the maximum delay as a TimeSpan.
        /// </summary>
        public TimeSpan MaxDelay => TimeSpan.FromMilliseconds(MaxDelayMs);

        /// <summary>
        /// Creates a default RetryConfiguration instance.
        /// </summary>
        public static RetryConfiguration Default => new RetryConfiguration();

        /// <summary>
        /// Creates a RetryConfiguration with no retries (immediate failure).
        /// </summary>
        public static RetryConfiguration NoRetry => new RetryConfiguration
        {
            Enabled = false,
            MaxAttempts = 0
        };

        /// <summary>
        /// Creates a RetryConfiguration optimized for network storage scenarios.
        /// </summary>
        public static RetryConfiguration ForNetworkStorage => new RetryConfiguration
        {
            Enabled = true,
            MaxAttempts = 5,
            InitialDelayMs = 500,
            MaxDelayMs = 10000,
            BackoffMultiplier = 2.0
        };

        /// <summary>
        /// Creates a RetryConfiguration for high-contention scenarios.
        /// </summary>
        public static RetryConfiguration ForHighContention => new RetryConfiguration
        {
            Enabled = true,
            MaxAttempts = 10,
            InitialDelayMs = 50,
            MaxDelayMs = 2000,
            BackoffMultiplier = 1.5
        };

        /// <summary>
        /// Validates the configuration values.
        /// </summary>
        public void Validate()
        {
            if (MaxAttempts < 0)
            {
                throw new ArgumentException("MaxAttempts must be non-negative.", nameof(MaxAttempts));
            }

            if (InitialDelayMs < 0)
            {
                throw new ArgumentException("InitialDelayMs must be non-negative.", nameof(InitialDelayMs));
            }

            if (MaxDelayMs < InitialDelayMs)
            {
                throw new ArgumentException("MaxDelayMs must be greater than or equal to InitialDelayMs.", nameof(MaxDelayMs));
            }

            if (BackoffMultiplier < 1.0)
            {
                throw new ArgumentException("BackoffMultiplier must be at least 1.0.", nameof(BackoffMultiplier));
            }
        }

        /// <summary>
        /// Creates a copy of this configuration.
        /// </summary>
        public RetryConfiguration Clone()
        {
            return new RetryConfiguration
            {
                Enabled = this.Enabled,
                MaxAttempts = this.MaxAttempts,
                InitialDelayMs = this.InitialDelayMs,
                MaxDelayMs = this.MaxDelayMs,
                BackoffMultiplier = this.BackoffMultiplier
            };
        }
    }
}