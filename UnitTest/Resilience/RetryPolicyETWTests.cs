// -----------------------------------------------------------------------
// <copyright file="RetryPolicyETWTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Resilience
{
    using System;
    using System.Collections.Generic;
    using System.Data.SQLite;
    using System.Diagnostics.Tracing;
    using System.Linq;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Resilience;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Traces;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class RetryPolicyETWTests
    {
        private TestEventListener eventListener;
        private List<EventWrittenEventArgs> capturedEvents;

        [TestInitialize]
        public void Setup()
        {
            this.capturedEvents = new List<EventWrittenEventArgs>();
            this.eventListener = new TestEventListener(this.capturedEvents);
            this.eventListener.EnableEvents(PersistenceEventSource.Log, EventLevel.Verbose);
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.eventListener?.Dispose();
        }

        [TestMethod]
        [TestCategory("Resilience")]
        [TestCategory("ETW")]
        public async Task ExecuteAsync_TransientError_LogsETWEvents()
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

            // Verify ETW events
            var transientErrorEvents = this.capturedEvents
                .Where(e => e.EventName == "TransientErrorDetected")
                .ToList();
            transientErrorEvents.Should().HaveCount(2); // Two transient errors before success

            var retryingEvents = this.capturedEvents
                .Where(e => e.EventName == "RetryingOperation")
                .ToList();
            retryingEvents.Should().HaveCount(2); // Two retry operations

            var successEvent = this.capturedEvents
                .FirstOrDefault(e => e.EventName == "RetrySucceeded");
            successEvent.Should().NotBeNull();
            successEvent.Payload[0].Should().Be(3); // Attempt count
        }

        [TestMethod]
        [TestCategory("Resilience")]
        [TestCategory("ETW")]
        public async Task ExecuteAsync_NonTransientError_LogsNonTransientETWEvent()
        {
            // Arrange
            var policy = new RetryPolicy(maxRetryAttempts: 3);

            // Act & Assert
            await policy.Invoking(p => p.ExecuteAsync(async () =>
            {
                await Task.Delay(1);
                throw new ArgumentException("Invalid argument");
            }))
            .Should().ThrowAsync<ArgumentException>();

            // Verify ETW events
            var nonTransientEvent = this.capturedEvents
                .FirstOrDefault(e => e.EventName == "NonTransientErrorDetected");
            nonTransientEvent.Should().NotBeNull();
            nonTransientEvent.Payload[0].Should().Be("ArgumentException");
            nonTransientEvent.Payload[1].Should().Be("Invalid argument");
        }

        [TestMethod]
        [TestCategory("Resilience")]
        [TestCategory("ETW")]
        public async Task ExecuteAsync_RetryExhausted_LogsExhaustedETWEvent()
        {
            // Arrange
            var policy = new RetryPolicy(
                maxRetryAttempts: 2,
                initialDelay: TimeSpan.FromMilliseconds(10));

            // Act & Assert
            await policy.Invoking(p => p.ExecuteAsync(async () =>
            {
                await Task.Delay(1);
                throw new SQLiteException(SQLiteErrorCode.Busy, "database is locked");
            }))
            .Should().ThrowAsync<SQLiteException>();

            // Verify ETW events
            var exhaustedEvent = this.capturedEvents
                .FirstOrDefault(e => e.EventName == "RetryExhausted");
            exhaustedEvent.Should().NotBeNull();
            exhaustedEvent.Payload[0].Should().Be(2); // Attempt count (maxRetryAttempts)
            exhaustedEvent.Payload[2].Should().Be("SQLiteException"); // Exception type
            exhaustedEvent.Payload[3].ToString().Should().Contain("database is locked"); // Exception message
            var errorDetails = exhaustedEvent.Payload[4].ToString(); // Error details from detector
            errorDetails.Should().Contain("TRANSIENT ERROR");
        }

        [TestMethod]
        [TestCategory("Resilience")]
        [TestCategory("ETW")]
        public void Execute_Synchronous_LogsETWEvents()
        {
            // Arrange
            var policy = new RetryPolicy(
                maxRetryAttempts: 3,
                initialDelay: TimeSpan.FromMilliseconds(10));
            var attemptCount = 0;

            // Act
            var result = policy.Execute(() =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new SQLiteException(SQLiteErrorCode.Locked, "table is locked");
                }
                return "success";
            });

            // Assert
            result.Should().Be("success");
            attemptCount.Should().Be(2);

            // Verify ETW events
            var transientErrorEvents = this.capturedEvents
                .Where(e => e.EventName == "TransientErrorDetected")
                .ToList();
            transientErrorEvents.Should().HaveCount(1);

            var successEvent = this.capturedEvents
                .FirstOrDefault(e => e.EventName == "RetrySucceeded");
            successEvent.Should().NotBeNull();
        }

        [TestMethod]
        [TestCategory("Resilience")]
        [TestCategory("ETW")]
        public async Task ExecuteAsync_VerifyErrorDetailsInETW()
        {
            // Arrange
            var policy = new RetryPolicy(
                maxRetryAttempts: 2,
                initialDelay: TimeSpan.FromMilliseconds(10));

            // Act
            await policy.ExecuteAsync(async () =>
            {
                await Task.Delay(1);
                throw new SQLiteException(SQLiteErrorCode.Busy, "database is locked");
            }).ContinueWith(t =>
            {
                // Ignore exception for this test
            });

            // Assert
            var transientErrorEvent = this.capturedEvents
                .FirstOrDefault(e => e.EventName == "TransientErrorDetected");
            
            transientErrorEvent.Should().NotBeNull();
            
            // Check error details payload
            var errorDetails = transientErrorEvent.Payload[4] as string;
            errorDetails.Should().NotBeNullOrEmpty();
            errorDetails.Should().Contain("TRANSIENT ERROR");
            errorDetails.Should().Contain("Database is busy");
        }

        private class TestEventListener : EventListener
        {
            private readonly List<EventWrittenEventArgs> events;

            public TestEventListener(List<EventWrittenEventArgs> events)
            {
                this.events = events;
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                this.events.Add(eventData);
            }
        }
    }
}