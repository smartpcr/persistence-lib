// -----------------------------------------------------------------------
// <copyright file="RetryPolicyTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Resilience
{
    using System;
    using System.Data.SQLite;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Resilience;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class RetryPolicyTests
    {
        [TestMethod]
        [TestCategory("Resilience")]
        public async Task ExecuteAsync_SucceedsOnFirstAttempt_NoRetry()
        {
            // Arrange
            var policy = new RetryPolicy(maxRetryAttempts: 3);
            var attemptCount = 0;

            // Act
            var result = await policy.ExecuteAsync(async () =>
            {
                attemptCount++;
                await Task.Delay(1);
                return "success";
            });

            // Assert
            result.Should().Be("success");
            attemptCount.Should().Be(1);
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public async Task ExecuteAsync_TransientErrorThenSuccess_Retries()
        {
            // Arrange
            var policy = new RetryPolicy(
                maxRetryAttempts: 3,
                initialDelay: TimeSpan.FromMilliseconds(10));
            var attemptCount = 0;

            // Act
            var result = await policy.ExecuteAsync(async () =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    throw new SQLiteException(SQLiteErrorCode.Busy, "database is locked");
                }
                await Task.Delay(1);
                return "success";
            });

            // Assert
            result.Should().Be("success");
            attemptCount.Should().Be(3);
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public async Task ExecuteAsync_ExceedsMaxRetries_ThrowsOriginalException()
        {
            // Arrange
            var policy = new RetryPolicy(
                maxRetryAttempts: 2,
                initialDelay: TimeSpan.FromMilliseconds(10));
            var attemptCount = 0;

            // Act & Assert
            await policy.Invoking(p => p.ExecuteAsync(async () =>
            {
                attemptCount++;
                await Task.Delay(1);
                throw new SQLiteException(SQLiteErrorCode.Busy, "database is locked");
            }))
            .Should().ThrowAsync<SQLiteException>()
            .WithMessage("*database is locked*");

            attemptCount.Should().Be(2);
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public async Task ExecuteAsync_NonTransientError_NoRetry()
        {
            // Arrange
            var policy = new RetryPolicy(maxRetryAttempts: 3);
            var attemptCount = 0;

            // Act & Assert
            await policy.Invoking(p => p.ExecuteAsync(async () =>
            {
                attemptCount++;
                await Task.Delay(1);
                throw new ArgumentException("Invalid argument");
            }))
            .Should().ThrowAsync<ArgumentException>();

            attemptCount.Should().Be(1);
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void IsTransientError_DatabaseLocked_ReturnsTrue()
        {
            // Arrange
            var exception = new SQLiteException(SQLiteErrorCode.Busy, "database is locked");

            // Act
            var (isTransient, errorDetails) = SQLiteTransientErrorDetector.IsTransientError(exception);

            // Assert
            isTransient.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void IsTransientError_TableLocked_ReturnsTrue()
        {
            // Arrange
            var exception = new SQLiteException(SQLiteErrorCode.Locked, "database table is locked");

            // Act
            var (isTransient, errorDetails) = SQLiteTransientErrorDetector.IsTransientError(exception);

            // Assert
            isTransient.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void IsTransientError_IOException_ReturnsTrue()
        {
            // Arrange
            var exception = new IOException("Network path not found");

            // Act
            var (isTransient, errorDetails) = SQLiteTransientErrorDetector.IsTransientError(exception);

            // Assert
            isTransient.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void IsTransientError_TimeoutException_ReturnsTrue()
        {
            // Arrange
            var exception = new TimeoutException("Operation timed out");

            // Act
            var (isTransient, errorDetails) = SQLiteTransientErrorDetector.IsTransientError(exception);

            // Assert
            isTransient.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void IsTransientError_NonTransientSQLiteError_ReturnsFalse()
        {
            // Arrange
            var exception = new SQLiteException(SQLiteErrorCode.Constraint, "UNIQUE constraint failed");

            // Act
            var (isTransient, errorDetails) = SQLiteTransientErrorDetector.IsTransientError(exception);

            // Assert
            isTransient.Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void IsTransientError_RegularException_ReturnsFalse()
        {
            // Arrange
            var exception = new InvalidOperationException("Invalid operation");

            // Act
            var (isTransient, errorDetails) = SQLiteTransientErrorDetector.IsTransientError(exception);

            // Assert
            isTransient.Should().BeFalse();
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void IsTransientError_InnerTransientException_ReturnsTrue()
        {
            // Arrange
            var innerException = new SQLiteException(SQLiteErrorCode.Busy, "database is locked");
            var exception = new InvalidOperationException("Operation failed", innerException);

            // Act
            var (isTransient, errorDetails) = SQLiteTransientErrorDetector.IsTransientError(exception);

            // Assert
            isTransient.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public async Task ExecuteAsync_WithRetries_AttemptsMultipleTimes()
        {
            // Arrange
            var policy = new RetryPolicy(
                maxRetryAttempts: 3,
                initialDelay: TimeSpan.FromMilliseconds(10),
                maxDelay: TimeSpan.FromMilliseconds(100));
            
            var attemptCount = 0;

            // Act
            var result = await policy.ExecuteAsync(async () =>
            {
                attemptCount++;
                
                if (attemptCount < 3)
                {
                    throw new SQLiteException(SQLiteErrorCode.Busy, "database is locked");
                }
                
                await Task.Delay(1);
                return "success";
            });

            // Assert
            result.Should().Be("success");
            attemptCount.Should().Be(3); // Initial attempt + 2 retries
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void Execute_SynchronousSuccess_Works()
        {
            // Arrange
            var policy = new RetryPolicy(maxRetryAttempts: 3);
            var attemptCount = 0;

            // Act
            var result = policy.Execute(() =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new SQLiteException(SQLiteErrorCode.Busy, "database is locked");
                }
                return "success";
            });

            // Assert
            result.Should().Be("success");
            attemptCount.Should().Be(2);
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public async Task ExecuteAsync_CancellationRequested_ThrowsOperationCancelledException()
        {
            // Arrange
            var policy = new RetryPolicy(
                maxRetryAttempts: 3,
                initialDelay: TimeSpan.FromMilliseconds(100));
            
            using var cts = new CancellationTokenSource();
            var attemptCount = 0;

            // Act & Assert
            var task = policy.ExecuteAsync(async () =>
            {
                attemptCount++;
                if (attemptCount == 1)
                {
                    cts.Cancel();
                    throw new SQLiteException(SQLiteErrorCode.Busy, "database is locked");
                }
                await Task.Delay(1);
                return "success";
            }, cts.Token);

            await task.Invoking(t => t).Should().ThrowAsync<OperationCanceledException>();
            attemptCount.Should().Be(1);
        }
    }
}