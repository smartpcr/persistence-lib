// -----------------------------------------------------------------------
// <copyright file="SQLiteTransientErrorDetector.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable InconsistentNaming
namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config
{
    using System;
    using System.Collections.Generic;
    using System.Data.SQLite;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public static class SQLiteTransientErrorDetector
    {
        // SQLite base result codes
        private const int SQLITE_OK = 0;
        private const int SQLITE_ERROR = 1;
        private const int SQLITE_BUSY = 5;
        private const int SQLITE_LOCKED = 6;
        private const int SQLITE_IOERR = 10;
        private const int SQLITE_CORRUPT = 11;
        private const int SQLITE_CANTOPEN = 14;
        private const int SQLITE_PROTOCOL = 17;
        private const int SQLITE_CONSTRAINT = 19;

        // Extended result codes
        private const int SQLITE_BUSY_RECOVERY = 261; // 5 | (1<<8)
        private const int SQLITE_BUSY_SNAPSHOT = 517; // 5 | (2<<8)
        private const int SQLITE_BUSY_TIMEOUT = 773; // 5 | (3<<8)
        private const int SQLITE_LOCKED_SHAREDCACHE = 518; // 6 | (2<<8)
        private const int SQLITE_IOERR_READ = 266; // 10 | (1<<8)
        private const int SQLITE_IOERR_SHORT_READ = 522; // 10 | (2<<8)
        private const int SQLITE_IOERR_WRITE = 778; // 10 | (3<<8)
        private const int SQLITE_IOERR_LOCK = 3850; // 10 | (15<<8)

        // Windows error codes
        private const int ERROR_SHARING_VIOLATION = 32;
        private const int ERROR_LOCK_VIOLATION = 33;
        private const int ERROR_NETNAME_DELETED = 64;
        private const int ERROR_SEM_TIMEOUT = 121;
        private const int ERROR_NETWORK_UNREACHABLE = 1231;
        private const int ERROR_CONNECTION_ABORTED = 1236;

        private static readonly HashSet<string> TransientErrorPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // SQLite specific
            "database is locked",
            "database table is locked",
            "database is temporarily locked",
            "unable to open database",
            "cannot open database file",
            "disk i/o error",
            "temporary disk i/o error",
            "unable to acquire",
            "deadlock",
            "busy",

            // Connection/Network
            "connection was closed",
            "connection was lost",
            "connection broken",
            "connection reset",
            "network error",
            "network path",
            "network location",
            "network name",
            "remote computer",
            "network unreachable",
            "no more connections",

            // Timeout
            "timeout expired",
            "timed out",
            "semaphore timeout",

            // File system
            "being used by another process",
            "sharing violation",
            "lock violation",
            "access is denied",
            "temporarily unavailable",
            "resource temporarily unavailable",
            "insufficient system resources",

            // Pipe/Stream
            "broken pipe",
            "pipe is being closed",
            "pipe has been ended",
            "bad file descriptor",

            // System
            "interrupted system call",
            "cannot operate on a closed"
        };

        /// <summary>
        /// Determines if an exception represents a transient error and provides detailed error information.
        /// </summary>
        /// <param name="exception">The exception to analyze.</param>
        /// <returns>A tuple containing whether the error is transient and a detailed error description.</returns>
        public static (bool isTransientError, string errorDescription) IsTransientError(Exception exception)
        {
            if (exception == null)
                return (false, "No exception provided");

            var errorDetails = new List<string>();
            var isTransient = false;
            var exceptionType = exception.GetType().Name;

            // Check for SQLiteException
            if (exception is SQLiteException sqliteEx)
            {
                var (sqliteTransient, sqliteDetails) = AnalyzeSQLiteException(sqliteEx);
                isTransient = sqliteTransient;
                errorDetails.AddRange(sqliteDetails);
            }
            // Check for IOException
            else if (exception is IOException ioEx)
            {
                var (ioTransient, ioDetails) = AnalyzeIOException(ioEx);
                isTransient = ioTransient;
                errorDetails.AddRange(ioDetails);
            }
            // Check for TimeoutException
            else if (exception is TimeoutException)
            {
                isTransient = true;
                errorDetails.Add("Operation timed out - temporary condition");
                errorDetails.Add("Consider increasing timeout values or optimizing the operation");
            }
            // Check for TaskCanceledException
            else if (exception is TaskCanceledException tcEx)
            {
                isTransient = true;
                errorDetails.Add("Task was cancelled");
                if (tcEx.InnerException is TimeoutException)
                    errorDetails.Add("Cancellation due to timeout");
                else
                    errorDetails.Add("Possible timeout or explicit cancellation");
            }
            // Check for OperationCanceledException
            else if (exception is OperationCanceledException opCancelEx)
            {
                var isUserCancellation = IsUserCancellation(opCancelEx);
                if (!isUserCancellation)
                {
                    isTransient = true;
                    errorDetails.Add("Operation cancelled (possibly due to timeout)");
                }
                else
                {
                    errorDetails.Add("User-initiated cancellation");
                }
            }
            // Check for UnauthorizedAccessException
            else if (exception is UnauthorizedAccessException)
            {
                var message = exception.Message?.ToLowerInvariant() ?? "";
                if (TransientErrorPatterns.Any(pattern => message.Contains(pattern)))
                {
                    isTransient = true;
                    errorDetails.Add("Temporary permission issue detected");
                }
                else
                {
                    errorDetails.Add("Permission denied - likely permanent");
                }
            }
            // Check for Win32Exception
            else if (exception is System.ComponentModel.Win32Exception win32Ex)
            {
                var (win32Transient, win32Details) = AnalyzeWin32Exception(win32Ex);
                isTransient = win32Transient;
                errorDetails.AddRange(win32Details);
            }
            // Check for AggregateException
            else if (exception is AggregateException aggEx)
            {
                errorDetails.Add($"AggregateException with {aggEx.InnerExceptions.Count} inner exception(s)");
                foreach (var innerEx in aggEx.InnerExceptions)
                {
                    var (innerTransient, innerDescription) = IsTransientError(innerEx);
                    if (innerTransient)
                    {
                        isTransient = true;
                        errorDetails.Add($"  • {innerDescription}");
                    }
                }
            }

            // For any exception type, check message patterns
            if (!isTransient)
            {
                var message = exception.Message?.ToLowerInvariant() ?? "";
                var matchedPattern = TransientErrorPatterns.FirstOrDefault(pattern => message.Contains(pattern.ToLowerInvariant()));
                if (matchedPattern != null)
                {
                    isTransient = true;
                    errorDetails.Add($"Transient error pattern detected: '{matchedPattern}'");
                }
            }

            // Check inner exception recursively
            if (exception.InnerException != null)
            {
                var (innerTransient, innerDescription) = IsTransientError(exception.InnerException);
                if (innerTransient)
                {
                    isTransient = true;
                    errorDetails.Add($"Inner exception: {innerDescription}");
                }
                else if (errorDetails.Count == 0)
                {
                    errorDetails.Add($"Inner: {exception.InnerException.GetType().Name} - {exception.InnerException.Message}");
                }
            }

            // Build final error description
            var errorDescription = BuildErrorDescription(exceptionType, exception.Message, errorDetails, isTransient);

            return (isTransient, errorDescription);
        }

        private static (bool isTransient, List<string> details) AnalyzeSQLiteException(SQLiteException sqliteEx)
        {
            var details = new List<string>();
            var isTransient = false;
            var resultCode = (int)sqliteEx.ResultCode;
            var errorCode = sqliteEx.ErrorCode;

            details.Add($"SQLite Error - ResultCode: {sqliteEx.ResultCode} ({resultCode}), ExtendedCode: {errorCode}");

            // Check specific result codes
            switch (resultCode)
            {
                case SQLITE_BUSY:
                    isTransient = true;
                    details.Add("Database is busy - another process has locked the database");
                    break;

                case SQLITE_LOCKED:
                    isTransient = true;
                    details.Add("Database table is locked - waiting for lock to be released");
                    break;

                case SQLITE_IOERR:
                    isTransient = true;
                    details.Add("I/O error occurred - temporary disk or network issue");
                    break;

                case SQLITE_CANTOPEN:
                    isTransient = true;
                    details.Add("Cannot open database file - file may be temporarily inaccessible");
                    break;

                case SQLITE_PROTOCOL:
                    isTransient = true;
                    details.Add("Database lock protocol error - synchronization issue");
                    break;

                case SQLITE_CORRUPT:
                    var message = sqliteEx.Message?.ToLowerInvariant() ?? "";
                    if (message.Contains("malformed") || message.Contains("network"))
                    {
                        isTransient = true;
                        details.Add("Database corruption on network drive - may be transient");
                    }
                    else
                    {
                        details.Add("Database corruption detected - likely permanent");
                    }

                    break;
            }

            // Check extended error codes
            switch (errorCode)
            {
                case SQLITE_BUSY_RECOVERY:
                    isTransient = true;
                    details.Add("Another process is recovering a WAL mode database");
                    break;
                case SQLITE_BUSY_SNAPSHOT:
                    isTransient = true;
                    details.Add("Database is busy with a snapshot");
                    break;
                case SQLITE_BUSY_TIMEOUT:
                    isTransient = true;
                    details.Add("Busy timeout exceeded");
                    break;
                case SQLITE_LOCKED_SHAREDCACHE:
                    isTransient = true;
                    details.Add("Shared cache lock conflict");
                    break;
            }

            // Check if any IOERR variant
            if ((resultCode & 0xFF) == SQLITE_IOERR)
            {
                isTransient = true;
                var ioErrSubcode = (errorCode >> 8);
                switch (ioErrSubcode)
                {
                    case 1: details.Add("Read operation failed"); break;
                    case 2: details.Add("Short read error"); break;
                    case 3: details.Add("Write operation failed"); break;
                    case 15: details.Add("Failed to obtain file lock"); break;
                    default: details.Add($"I/O error variant {ioErrSubcode}"); break;
                }
            }

            // Check error message for additional patterns
            if (!isTransient)
            {
                var message = sqliteEx.Message?.ToLowerInvariant() ?? "";
                if (TransientErrorPatterns.Any(pattern => message.Contains(pattern.ToLowerInvariant())))
                {
                    isTransient = true;
                }
            }

            return (isTransient, details);
        }

        private static (bool isTransient, List<string> details) AnalyzeIOException(IOException ioEx)
        {
            var details = new List<string>();
            var isTransient = true; // Most IOExceptions are transient

            details.Add($"IOException - HResult: 0x{ioEx.HResult:X8}");

            var message = ioEx.Message?.ToLowerInvariant() ?? "";

            // Analyze specific patterns
            if (message.Contains("being used by another process"))
                details.Add("File locked by another process (possibly antivirus/backup)");
            else if (message.Contains("network"))
                details.Add("Network-related I/O error");
            else if (message.Contains("sharing violation"))
                details.Add("File sharing violation");
            else if (message.Contains("access is denied"))
                details.Add("Access denied - temporary permission or lock issue");
            else if (message.Contains("insufficient"))
                details.Add("Insufficient system resources");
            else
                details.Add("File system or network I/O error");

            // Check Windows error codes
            var errorCode = ioEx.HResult & 0xFFFF;
            switch (errorCode)
            {
                case ERROR_SHARING_VIOLATION:
                    details.Add("Windows: Sharing violation (ERROR_SHARING_VIOLATION)");
                    break;
                case ERROR_LOCK_VIOLATION:
                    details.Add("Windows: Lock violation (ERROR_LOCK_VIOLATION)");
                    break;
                case ERROR_NETNAME_DELETED:
                    details.Add("Windows: Network name deleted (ERROR_NETNAME_DELETED)");
                    break;
                case ERROR_SEM_TIMEOUT:
                    details.Add("Windows: Semaphore timeout (ERROR_SEM_TIMEOUT)");
                    break;
                case ERROR_NETWORK_UNREACHABLE:
                    details.Add("Windows: Network unreachable (ERROR_NETWORK_UNREACHABLE)");
                    break;
                case ERROR_CONNECTION_ABORTED:
                    details.Add("Windows: Connection aborted (ERROR_CONNECTION_ABORTED)");
                    break;
            }

            return (isTransient, details);
        }

        private static (bool isTransient, List<string> details) AnalyzeWin32Exception(System.ComponentModel.Win32Exception win32Ex)
        {
            var details = new List<string>();
            var isTransient = false;

            details.Add($"Win32Exception - NativeErrorCode: {win32Ex.NativeErrorCode} (0x{win32Ex.NativeErrorCode:X})");

            // Check for transient Windows error codes
            switch (win32Ex.NativeErrorCode)
            {
                case ERROR_SHARING_VIOLATION:
                case ERROR_LOCK_VIOLATION:
                case ERROR_NETNAME_DELETED:
                case ERROR_SEM_TIMEOUT:
                case ERROR_NETWORK_UNREACHABLE:
                case ERROR_CONNECTION_ABORTED:
                    isTransient = true;
                    details.Add("Transient Windows system error");
                    break;
            }

            // Check message patterns
            var message = win32Ex.Message?.ToLowerInvariant() ?? "";
            if (TransientErrorPatterns.Any(pattern => message.Contains(pattern.ToLowerInvariant())))
            {
                isTransient = true;
            }

            return (isTransient, details);
        }

        private static bool IsUserCancellation(OperationCanceledException cancelEx)
        {
            if (cancelEx?.CancellationToken != null && cancelEx.CancellationToken.IsCancellationRequested)
            {
                var message = cancelEx.Message?.ToLowerInvariant() ?? string.Empty;
                return !message.Contains("timeout") && !message.Contains("timed out");
            }

            return false;
        }

        private static string BuildErrorDescription(string exceptionType, string message, List<string> details, bool isTransient)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{exceptionType}: {message}");

            if (details.Count > 0)
            {
                sb.AppendLine("\nDetails:");
                foreach (var detail in details)
                {
                    sb.AppendLine($"  • {detail}");
                }
            }

            sb.AppendLine($"\nAnalysis: {(isTransient ? "TRANSIENT ERROR" : "NON-TRANSIENT ERROR")}");

            if (isTransient)
            {
                sb.AppendLine("Recommendation: Retry with exponential backoff (100ms, 200ms, 400ms, etc.)");
                sb.AppendLine("Max recommended retries: 3-5 attempts");
            }
            else
            {
                sb.AppendLine("Recommendation: Do not retry automatically. Investigation required.");
                sb.AppendLine("Check: Database integrity, file permissions, disk space, network connectivity");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets recommended retry delay in milliseconds for the given attempt number.
        /// </summary>
        public static int GetRecommendedRetryDelay(int attemptNumber)
        {
            var baseDelay = Math.Min(100 * Math.Pow(2, attemptNumber), 5000);
            var jitter = new Random().Next(0, 100);
            return (int)baseDelay + jitter;
        }
    }
}