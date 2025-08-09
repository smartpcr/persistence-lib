// -----------------------------------------------------------------------
// <copyright file="SQLiteTransientErrorDetectorTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Resilience
{
    using System;
    using System.Data.SQLite;
    using System.IO;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Resilience;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SQLiteTransientErrorDetectorTests
    {
        [TestMethod]
        [TestCategory("Resilience")]
        public void IsTransientError_NullException_ReturnsFalse()
        {
            // Act
            var (isTransient, errorDetails) = SQLiteTransientErrorDetector.IsTransientError(null);

            // Assert
            isTransient.Should().BeFalse();
            errorDetails.Should().Be("No exception provided");
        }

        [TestMethod]
        [TestCategory("Resilience")]
        [DataRow(5, true)]   // SQLITE_BUSY
        [DataRow(6, true)]   // SQLITE_LOCKED
        [DataRow(10, true)]  // SQLITE_IOERR
        [DataRow(14, true)]  // SQLITE_CANTOPEN
        [DataRow(17, true)]  // SQLITE_PROTOCOL
        [DataRow(262, true)] // SQLITE_BUSY_RECOVERY
        [DataRow(518, true)] // SQLITE_LOCKED_SHAREDCACHE
        [DataRow(261, true)] // SQLITE_BUSY_SNAPSHOT
        [DataRow(19, false)] // SQLITE_CONSTRAINT (not transient)
        [DataRow(23, false)] // SQLITE_AUTH (not transient)
        public void IsTransientError_SQLiteErrorCodes_ReturnsExpected(int errorCode, bool expectedResult)
        {
            // Arrange
            var exception = new SQLiteException((SQLiteErrorCode)errorCode, "test error");

            // Act
            var (isTransient, errorDetails) = SQLiteTransientErrorDetector.IsTransientError(exception);

            // Assert
            isTransient.Should().Be(expectedResult);
        }

        [TestMethod]
        [TestCategory("Resilience")]
        [DataRow("database is locked", true)]
        [DataRow("database table is locked", true)]
        [DataRow("unable to open database", true)]
        [DataRow("disk i/o error", true)]
        [DataRow("connection was closed", true)]
        [DataRow("connection was lost", true)]
        [DataRow("database is temporarily locked", true)]
        [DataRow("deadlock", true)]
        [DataRow("busy", true)]
        [DataRow("timeout expired", true)]
        [DataRow("network path", true)]
        [DataRow("UNIQUE constraint failed", false)]
        [DataRow("syntax error", false)]
        [DataRow("no such table", false)]
        public void IsTransientError_ErrorMessages_ReturnsExpected(string message, bool expectedResult)
        {
            // Arrange
            var exception = new SQLiteException(SQLiteErrorCode.Unknown, message);

            // Act
            var (isTransient, errorDetails) = SQLiteTransientErrorDetector.IsTransientError(exception);

            // Assert
            isTransient.Should().Be(expectedResult, $"Message '{message}' should be {(expectedResult ? "transient" : "non-transient")}");
        }

        [TestMethod]
        [TestCategory("Resilience")]
        [DataRow("being used by another process", true)]
        [DataRow("network path", true)]
        [DataRow("sharing violation", true)]
        [DataRow("lock violation", true)]
        [DataRow("temporarily unavailable", true)]
        [DataRow("insufficient system resources", true)]
        [DataRow("semaphore timeout", true)]
        [DataRow("file not found", false)]
        [DataRow("access denied", false)]
        public void IsTransientError_IOExceptionMessages_ReturnsExpected(string message, bool expectedResult)
        {
            // Arrange
            var exception = new IOException(message);

            // Act
            var (isTransient, errorDetails) = SQLiteTransientErrorDetector.IsTransientError(exception);

            // Assert
            isTransient.Should().Be(expectedResult);
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void IsTransientError_OperationCanceledException_WithTimeout_ReturnsTrue()
        {
            // Arrange
            var exception = new OperationCanceledException("The operation has timed out.");

            // Act
            var (isTransient, errorDetails) = SQLiteTransientErrorDetector.IsTransientError(exception);

            // Assert
            isTransient.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void IsTransientError_NestedTransientException_ReturnsTrue()
        {
            // Arrange
            var innerMost = new SQLiteException(SQLiteErrorCode.Busy, "database is locked");
            var middle = new InvalidOperationException("Middle exception", innerMost);
            var outer = new ApplicationException("Outer exception", middle);

            // Act
            var (isTransient, errorDetails) = SQLiteTransientErrorDetector.IsTransientError(outer);

            // Assert
            isTransient.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void GetTransientErrorDescription_TransientError_ReturnsDescription()
        {
            // Arrange
            var exception = new SQLiteException(SQLiteErrorCode.Busy, "database is locked");

            // Act
            var (isTransient, description) = SQLiteTransientErrorDetector.IsTransientError(exception);

            // Assert
            isTransient.Should().BeTrue();
            description.Should().NotBeNull();
            description.Should().Contain("Database is busy");
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void GetTransientErrorDescription_NonTransientError_ReturnsDescription()
        {
            // Arrange
            var exception = new ArgumentException("Invalid argument");

            // Act
            var (isTransient, description) = SQLiteTransientErrorDetector.IsTransientError(exception);

            // Assert
            isTransient.Should().BeFalse();
            description.Should().NotBeNull();
            description.Should().Contain("NON-TRANSIENT ERROR");
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void IsTransientError_IOErrorSubcodes_ReturnsTrue()
        {
            // Arrange
            // SQLITE_IOERR_READ = SQLITE_IOERR | (1<<8) = 10 | 256 = 266
            var exception = new SQLiteException((SQLiteErrorCode)266, "I/O error during read");

            // Act
            var (isTransient, errorDetails) = SQLiteTransientErrorDetector.IsTransientError(exception);

            // Assert
            isTransient.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void GetTransientErrorDescription_VariousErrors_ReturnsAppropriateDescriptions()
        {
            // Arrange & Act & Assert
            var busyEx = new SQLiteException(SQLiteErrorCode.Busy, "busy");
            var (isBusyTransient, busyDesc) = SQLiteTransientErrorDetector.IsTransientError(busyEx);
            isBusyTransient.Should().BeTrue();
            busyDesc.Should().Contain("Database is busy");

            var ioEx = new IOException("network path not found");
            var (isIoTransient, ioDesc) = SQLiteTransientErrorDetector.IsTransientError(ioEx);
            isIoTransient.Should().BeTrue();
            ioDesc.Should().Contain("Network-related I/O error");

            var timeoutEx = new TimeoutException();
            var (isTimeoutTransient, timeoutDesc) = SQLiteTransientErrorDetector.IsTransientError(timeoutEx);
            isTimeoutTransient.Should().BeTrue();
            timeoutDesc.Should().Contain("Operation timed out");
        }
    }
}