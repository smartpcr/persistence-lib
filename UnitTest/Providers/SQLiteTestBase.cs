//-------------------------------------------------------------------------------
// <copyright file="SQLiteTestBase.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Providers
{
    using System;
    using System.Data.SQLite;
    using System.IO;
    using System.Threading;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Base class for SQLite persistence provider tests that handles proper cleanup
    /// of database files and connections.
    /// </summary>
    public abstract class SQLiteTestBase
    {
        /// <summary>
        /// Safely deletes a SQLite database file, handling connection pool cleanup
        /// and file locking issues.
        /// </summary>
        /// <param name="dbPath">Path to the database file to delete</param>
        protected void SafeDeleteDatabase(string dbPath)
        {
            SQLiteProviderSharedState.ClearState();

            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                return;
            }

            try
            {
                // First attempt: Clear all connection pools
                SQLiteConnection.ClearAllPools();
                Thread.Sleep(100); // Give the system time to release handles

                File.Delete(dbPath);
            }
            catch (IOException)
            {
                // Second attempt: Force garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Thread.Sleep(200);

                try
                {
                    File.Delete(dbPath);
                }
                catch (Exception ex)
                {
                    // Log warning but don't fail the test
                    this.TestContext.WriteLine($"Warning: Unable to delete test database '{dbPath}': {ex.Message}");
                }
            }

            // Also try to delete journal and wal files if they exist
            var journalPath = dbPath + "-journal";
            var walPath = dbPath + "-wal";
            var shmPath = dbPath + "-shm";

            foreach (var auxiliaryFile in new[] { journalPath, walPath, shmPath })
            {
                if (File.Exists(auxiliaryFile))
                {
                    try
                    {
                        File.Delete(auxiliaryFile);
                    }
                    catch
                    {
                        // Ignore auxiliary file deletion errors
                    }
                }
            }
        }

        /// <summary>
        /// Creates a connection string with pooling disabled for test scenarios.
        /// </summary>
        /// <param name="dbPath">Path to the database file</param>
        /// <returns>Connection string with pooling disabled</returns>
        protected static string CreateTestConnectionString(string dbPath)
        {
            return $"Data Source={dbPath};Version=3;Pooling=False;";
        }

        /// <summary>
        /// Creates a connection string with pooling enabled (default behavior).
        /// </summary>
        /// <param name="dbPath">Path to the database file</param>
        /// <returns>Connection string with pooling enabled</returns>
        protected static string CreatePooledConnectionString(string dbPath)
        {
            return $"Data Source={dbPath};Version=3;";
        }

        /// <summary>
        /// Gets or sets the MSTest TestContext.
        /// </summary>
        public TestContext TestContext { get; set; }
    }
}