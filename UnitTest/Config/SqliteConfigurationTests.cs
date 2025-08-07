//-------------------------------------------------------------------------------
// <copyright file="SqliteConfigurationTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Config
{
    using System;
    using System.IO;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class SqliteConfigurationTests
    {
        private string tempConfigPath;
        private string originalDirectory;

        [TestInitialize]
        public void TestInitialize()
        {
            this.tempConfigPath = Path.Combine(Path.GetTempPath(), $"config_{Guid.NewGuid()}.json");
            this.originalDirectory = Directory.GetCurrentDirectory();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (File.Exists(this.tempConfigPath))
            {
                File.Delete(this.tempConfigPath);
            }

            // Clean up default sqlite.json if created
            var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "sqlite.json");
            if (File.Exists(defaultPath))
            {
                File.Delete(defaultPath);
            }

            Directory.SetCurrentDirectory(this.originalDirectory);
        }

        [TestMethod]
        public void FromJsonFile_WithNoFile_ShouldReturnDefaultConfiguration()
        {
            // Act
            var config = SqliteConfiguration.FromJsonFile("non-existent-file.json");

            // Assert
            config.Should().NotBeNull();
            config.CacheSize.Should().Be(-2000);
            config.PageSize.Should().Be(4096);
            config.JournalMode.Should().Be(JournalMode.WAL);
            config.SynchronousMode.Should().Be(SynchronousMode.Normal);
            config.BusyTimeout.Should().Be(5000);
            config.EnableForeignKeys.Should().BeTrue();
        }

        [TestMethod]
        public void FromJsonFile_WithConfigInSection_ShouldLoadCorrectly()
        {
            // Arrange
            var configData = new
            {
                SqliteConfiguration = new
                {
                    CacheSize = -8000,
                    PageSize = 16384,
                    JournalMode = "DELETE",
                    SynchronousMode = "Full",
                    BusyTimeout = 20000,
                    EnableForeignKeys = false
                }
            };

            File.WriteAllText(this.tempConfigPath, JsonConvert.SerializeObject(configData, Formatting.Indented));

            // Act
            var config = SqliteConfiguration.FromJsonFile(this.tempConfigPath);

            // Assert
            config.CacheSize.Should().Be(-8000);
            config.PageSize.Should().Be(16384);
            config.JournalMode.Should().Be(JournalMode.Delete);
            config.SynchronousMode.Should().Be(SynchronousMode.Full);
            config.BusyTimeout.Should().Be(20000);
            config.EnableForeignKeys.Should().BeFalse();
        }

        [TestMethod]
        public void FromJsonFile_WithConfigAtRoot_ShouldLoadCorrectly()
        {
            // Arrange
            var configData = new
            {
                CacheSize = -3000,
                PageSize = 8192,
                JournalMode = "MEMORY",
                SynchronousMode = "Off",
                BusyTimeout = 10000,
                EnableForeignKeys = true
            };

            File.WriteAllText(this.tempConfigPath, JsonConvert.SerializeObject(configData, Formatting.Indented));

            // Act
            var config = SqliteConfiguration.FromJsonFile(this.tempConfigPath);

            // Assert
            config.CacheSize.Should().Be(-3000);
            config.PageSize.Should().Be(8192);
            config.JournalMode.Should().Be(JournalMode.Memory);
            config.SynchronousMode.Should().Be(SynchronousMode.Off);
            config.BusyTimeout.Should().Be(10000);
            config.EnableForeignKeys.Should().BeTrue();
        }

        [TestMethod]
        public void FromJsonFile_WithNullPath_ShouldLookForDefaultFile()
        {
            // Arrange - Create sqlite.json in current directory
            var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "sqlite.json");
            var configData = new
            {
                CacheSize = -5000,
                PageSize = 4096,
                JournalMode = "PERSIST",
                SynchronousMode = "Normal",
                BusyTimeout = 7500,
                EnableForeignKeys = false
            };

            File.WriteAllText(defaultPath, JsonConvert.SerializeObject(configData, Formatting.Indented));

            // Act
            var config = SqliteConfiguration.FromJsonFile();

            // Assert
            config.CacheSize.Should().Be(-5000);
            config.JournalMode.Should().Be(JournalMode.Persist);
            config.BusyTimeout.Should().Be(7500);
            config.EnableForeignKeys.Should().BeFalse();
        }

        [TestMethod]
        public void FromJsonFile_WithInvalidJson_ShouldThrow()
        {
            // Arrange
            File.WriteAllText(this.tempConfigPath, "{ invalid json ]");

            // Act & Assert
            var action = () => SqliteConfiguration.FromJsonFile(this.tempConfigPath);
            action.Should().Throw<InvalidOperationException>()
                .WithMessage($"Failed to load SQLite configuration from '{this.tempConfigPath}'");
        }

        [TestMethod]
        public void FromJsonFileRequired_WithMissingFile_ShouldThrow()
        {
            // Act & Assert
            var action = () => SqliteConfiguration.FromJsonFileRequired("missing-file.json");
            action.Should().Throw<FileNotFoundException>()
                .WithMessage("Required SQLite configuration file not found: missing-file.json");
        }

        [TestMethod]
        public void FromJsonFileRequired_WithExistingFile_ShouldLoadCorrectly()
        {
            // Arrange
            var configData = new
            {
                CacheSize = -10000,
                EnableForeignKeys = true
            };

            File.WriteAllText(this.tempConfigPath, JsonConvert.SerializeObject(configData));

            // Act
            var config = SqliteConfiguration.FromJsonFileRequired(this.tempConfigPath);

            // Assert
            config.Should().NotBeNull();
            config.CacheSize.Should().Be(-10000);
            config.EnableForeignKeys.Should().BeTrue();
        }

        [TestMethod]
        public void FromJsonFile_WithPartialConfig_ShouldUseDefaultsForMissing()
        {
            // Arrange - Only specify some settings
            var configData = new
            {
                CacheSize = -15000,
                JournalMode = "WAL"
            };

            File.WriteAllText(this.tempConfigPath, JsonConvert.SerializeObject(configData));

            // Act
            var config = SqliteConfiguration.FromJsonFile(this.tempConfigPath);

            // Assert
            config.CacheSize.Should().Be(-15000);
            config.JournalMode.Should().Be(JournalMode.WAL);
            // These should have default values
            config.PageSize.Should().Be(4096);
            config.SynchronousMode.Should().Be(SynchronousMode.Normal);
            config.BusyTimeout.Should().Be(5000);
            config.EnableForeignKeys.Should().BeTrue();
        }
    }
}