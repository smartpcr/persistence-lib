//-------------------------------------------------------------------------------
// <copyright file="SqliteConfiguration.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using Microsoft.Extensions.Configuration;

    public class SqliteConfiguration
    {
        /// <summary>
        /// File path to the SQLite database file.
        /// </summary>
        public string DbFile { get; set; }

        /// <summary>
        /// The suggested maximum number of database pages SQLite will hold in RAM per open database connection.
        /// It's an upper bound on the page cache. Default value is '-2000', which means "enough pages to use ≈ 2000 × 1024 bytes"
        /// of memory (~2MB), regardless of the page size.
        /// N &gt; 0: sets the cache to N pages.
        /// K &lt; 0: sets the cache so that K × 1024 bytes of memory is used
        ///
        /// NOTE: This is a CONNECTION-SPECIFIC pragma. The value only persists for the lifetime of the current connection.
        /// Each new connection will start with the default cache size unless explicitly set.
        /// </summary>
        public int CacheSize { get; set; } = -2000; // 2MB cache

        /// <summary>
        /// The unit of I/O in SQLite, default to 4096 bytes per page.
        /// Every database file is a sequence of fixed-size pages; internal B-tree nodes, table rows, index entries—all live inside pages.
        ///
        /// NOTE: This is a DATABASE-SPECIFIC pragma. The page size is stored in the database file header and persists.
        /// This pragma can only be set on an empty database before any tables are created.
        /// </summary>
        [Range(512, 65536)]
        public int PageSize { get; set; } = 4096;

        /// <summary>
        /// How logs are maintained.
        ///
        /// NOTE: This is a DATABASE-SPECIFIC pragma. The journal mode is stored in the database file and persists
        /// across connections. Once set to WAL mode, it remains in WAL mode for all future connections.
        /// </summary>
        public JournalMode JournalMode { get; set; } = JournalMode.WAL;

        /// <summary>
        /// Manages how often fsync() should be called.
        ///
        /// NOTE: This is a CONNECTION-SPECIFIC pragma. The synchronous setting only affects the current connection
        /// and must be set for each new connection.
        /// </summary>
        public SynchronousMode SynchronousMode { get; set; } = SynchronousMode.Normal;

        /// <summary>
        /// When a table is locked by another connection, SQLite will sleep and retry for up to the specified number of milliseconds
        /// before returning SQLITE_BUSY.
        /// By default, the busy timeout is 0 ms—meaning "don't wait at all." If you need to smooth over transient locks
        /// under normal concurrency, you should explicitly set a nonzero timeout each time you open a connection.
        /// The setting applies per database connection and only lasts for that session. If you close and reopen the connection,
        /// you must issue PRAGMA busy_timeout again
        ///
        /// NOTE: This is a CONNECTION-SPECIFIC pragma. Must be set for each new connection.
        /// </summary>
        public int BusyTimeout { get; set; } = 5000; // 5 seconds

        /// <summary>
        /// By default, SQLite does not enforce foreign-key constraints. This choice was made for backwards compatibility.
        /// Unless you explicitly turn them on, SQLite will happily let you insert "orphan" child rows or delete parent rows
        /// without cascading or even raising an error.
        /// The FK-enforcement flag lives in memory on each database connection. You must run it after opening every new connection.
        ///
        /// NOTE: This is a CONNECTION-SPECIFIC pragma. Must be enabled for each new connection.
        /// </summary>
        public bool EnableForeignKeys { get; set; } = true;

        /// <summary>
        /// The command timeout in seconds. This sets how long a single SQLite command will wait before timing out.
        /// Default is 30 seconds. Set to 0 for no timeout.
        ///
        /// NOTE: This is applied to each SQLiteCommand instance created by the persistence provider.
        /// </summary>
        public int CommandTimeout { get; set; } = 30;

        /// <summary>
        /// Creates a SqliteConfiguration instance from a JSON file.
        /// </summary>
        /// <param name="jsonFilePath">Path to the JSON configuration file. If null, looks for 'sqlite.json' in current directory.</param>
        /// <returns>SqliteConfiguration instance populated from the JSON file, or default configuration if file not found.</returns>
        public static SqliteConfiguration FromJsonFile(string jsonFilePath = null)
        {
            var config = new SqliteConfiguration();

            // Determine the config file path
            var configPath = jsonFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), "sqlite.json");

            // If file doesn't exist, return default configuration
            if (!File.Exists(configPath))
            {
                return config;
            }

            try
            {
                // Load configuration from JSON file
                var configBuilder = new ConfigurationBuilder()
                    .SetBasePath(Path.GetDirectoryName(configPath)!)
                    .AddJsonFile(Path.GetFileName(configPath), optional: false, reloadOnChange: false);

                var configuration = configBuilder.Build();

                // Try to bind from "SqliteConfiguration" section first
                var section = configuration.GetSection("SqliteConfiguration");
                if (section.Exists())
                {
                    section.Bind(config);
                }
                else
                {
                    // If no section, try binding from root
                    configuration.Bind(config);
                }

                return config;
            }
            catch (Exception ex)
            {
                // Log error if needed, return default configuration
                // In production, you might want to log this error
                throw new InvalidOperationException($"Failed to load SQLite configuration from '{configPath}'", ex);
            }
        }

        /// <summary>
        /// Creates a SqliteConfiguration instance from a JSON file, throwing an exception if the file is not found.
        /// </summary>
        /// <param name="jsonFilePath">Path to the JSON configuration file. If null, looks for 'sqlite.json' in current directory.</param>
        /// <returns>SqliteConfiguration instance populated from the JSON file.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the configuration file is not found.</exception>
        public static SqliteConfiguration FromJsonFileRequired(string jsonFilePath = null)
        {
            var configPath = jsonFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), "sqlite.json");

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"Required SQLite configuration file not found: {configPath}");
            }

            return FromJsonFile(jsonFilePath);
        }
    }
}