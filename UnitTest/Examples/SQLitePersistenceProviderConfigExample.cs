//-------------------------------------------------------------------------------
// <copyright file="SQLitePersistenceProviderConfigExample.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Examples
{
    using System;
    using System.Data;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Example demonstrating how to use SQLitePersistenceProvider with JSON configuration
    /// </summary>
    public class SQLitePersistenceProviderConfigExample
    {
        public async Task Example1_DefaultConfiguration()
        {
            // Using default configuration
            var connectionString = "Data Source=myapp.db";
            var provider = new SQLitePersistenceProvider<Product, string>(connectionString);
            
            // Initialize will create database with default settings
            await provider.InitializeAsync();
            
            // Now ready to use
            var product = new Product 
            { 
                Id = "P001", 
                Name = "Laptop", 
                Price = 999.99m 
            };
            
            await provider.CreateAsync(product, new CallerInfo());
        }

        public async Task Example2_JsonFileConfiguration()
        {
            // Create configuration file
            var configJson = @"{
                ""SqliteConfiguration"": {
                    ""DbFile"": ""products.db"",
                    ""CacheSize"": -8000,
                    ""PageSize"": 8192,
                    ""JournalMode"": ""WAL"",
                    ""SynchronousMode"": ""Normal"",
                    ""BusyTimeout"": 10000,
                    ""EnableForeignKeys"": true
                }
            }";
            
            var configPath = "sqlite-config.json";
            File.WriteAllText(configPath, configJson);
            
            // Method 1: Use static factory method
            var connectionString = "Data Source=products.db";
            var provider = SQLitePersistenceProvider<Product, string>.CreateWithJsonConfig(
                connectionString, 
                configPath);
            
            // Method 2: Load config separately
            var config = SqliteConfiguration.FromJsonFile(configPath);
            var provider2 = new SQLitePersistenceProvider<Product, string>(connectionString, config);
            
            await provider.InitializeAsync();
            
            // The database is now configured with:
            // - 8MB cache (-8000 * 1024 bytes)
            // - 8KB page size
            // - WAL mode for better concurrency
            // - Normal synchronization
            // - 10 second busy timeout
            // - Foreign keys enabled
        }

        public async Task Example3_EnvironmentSpecificConfiguration()
        {
            // Build configuration from multiple sources
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true);
            
            var configuration = configBuilder.Build();
            
            // Extract connection string and config path from configuration
            var connectionString = configuration.GetConnectionString("SqliteDb");
            var configPath = configuration["SqliteConfigPath"];
            
            var provider = SQLitePersistenceProvider<Product, string>.CreateWithJsonConfig(
                connectionString, 
                configPath);
            
            await provider.InitializeAsync();
        }

        public async Task Example4_HighPerformanceConfiguration()
        {
            // Configuration optimized for performance
            var performanceConfig = @"{
                ""CacheSize"": -50000,
                ""PageSize"": 32768,
                ""JournalMode"": ""MEMORY"",
                ""SynchronousMode"": ""Off"",
                ""BusyTimeout"": 30000,
                ""EnableForeignKeys"": false
            }";
            
            var configPath = "high-performance-config.json";
            File.WriteAllText(configPath, performanceConfig);
            
            var connectionString = "Data Source=highperf.db";
            var provider = SQLitePersistenceProvider<Product, string>.CreateWithJsonConfig(
                connectionString, 
                configPath);
            
            await provider.InitializeAsync();
            
            // This configuration provides:
            // - Large 50MB cache for better performance
            // - 32KB pages for fewer I/O operations
            // - In-memory journaling (faster but less safe)
            // - No synchronization (maximum speed, risk of corruption)
            // - 30 second timeout for busy databases
            // - Foreign keys disabled for speed
        }

        public async Task Example5_SafetyFirstConfiguration()
        {
            // Configuration optimized for data safety
            var safetyConfig = @"{
                ""SqliteConfiguration"": {
                    ""CacheSize"": -2000,
                    ""PageSize"": 4096,
                    ""JournalMode"": ""WAL"",
                    ""SynchronousMode"": ""Extra"",
                    ""BusyTimeout"": 60000,
                    ""EnableForeignKeys"": true
                }
            }";
            
            var configPath = "safety-config.json";
            File.WriteAllText(configPath, safetyConfig);
            
            var connectionString = "Data Source=critical-data.db";
            var provider = SQLitePersistenceProvider<Product, string>.CreateWithJsonConfig(
                connectionString, 
                configPath);
            
            await provider.InitializeAsync();
            
            // This configuration provides:
            // - Standard 2MB cache
            // - Standard 4KB pages
            // - WAL mode for consistency and concurrency
            // - Extra synchronization for maximum durability
            // - 60 second timeout for heavily loaded systems
            // - Foreign keys for referential integrity
        }

        public async Task Example6_InMemoryDatabase()
        {
            // In-memory database for testing
            var connectionString = "Data Source=:memory:";
            var provider = new SQLitePersistenceProvider<Product, string>(connectionString);
            
            await provider.InitializeAsync();
            
            // Perfect for unit tests - fast and isolated
            var product = new Product 
            { 
                Id = "TEST-001", 
                Name = "Test Product" 
            };
            
            await provider.CreateAsync(product, new CallerInfo());
            
            // Database exists only for the lifetime of the connection
        }

        public async Task Example7_DefaultSqliteJsonFile()
        {
            // If you have a 'sqlite.json' file in your current directory,
            // it will be automatically used when you don't specify a config path
            
            // Assuming sqlite.json exists in current directory with content:
            // {
            //   "CacheSize": -4000,
            //   "PageSize": 8192,
            //   "JournalMode": "WAL",
            //   "SynchronousMode": "Normal",
            //   "BusyTimeout": 10000,
            //   "EnableForeignKeys": true
            // }
            
            var connectionString = "Data Source=app.db";
            
            // This will automatically look for and use sqlite.json
            var provider = SQLitePersistenceProvider<Product, string>.CreateWithJsonConfig(
                connectionString, 
                null); // null means use default sqlite.json
            
            await provider.InitializeAsync();
            
            // The configuration from sqlite.json is automatically applied
        }

        public async Task Example8_ConfigurationValidation()
        {
            // Use FromJsonFileRequired when config file MUST exist
            try
            {
                var config = SqliteConfiguration.FromJsonFileRequired("required-config.json");
                var provider = new SQLitePersistenceProvider<Product, string>(
                    "Data Source=app.db", 
                    config);
                    
                await provider.InitializeAsync();
            }
            catch (FileNotFoundException ex)
            {
                // Handle missing required configuration
                Console.WriteLine($"Configuration file not found: {ex.Message}");
            }
        }

        private class Product : BaseEntity<string>, IVersionedEntity<string>
        {
            [PrimaryKey(Order = 2)]
            [AuditField(AuditFieldType.Version)]
            [Column("Version", SqlDbType.BigInt, NotNull = true)]
            [Index("IX_CacheEntry_Version")]
            public new long Version
            {
                get => base.Version;
                set => base.Version = value;
            }

            public string Name { get; set; }
            public decimal Price { get; set; }
            public bool IsDeleted { get; set; }
        }
    }
}