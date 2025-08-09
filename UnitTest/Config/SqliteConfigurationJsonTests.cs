// -----------------------------------------------------------------------
// <copyright file="SqliteConfigurationJsonTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Config
{
    using System;
    using System.IO;
    using System.Text.Json;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SqliteConfigurationJsonTests
    {
        private string testConfigPath;
        private string testDirectory;

        [TestInitialize]
        public void Setup()
        {
            this.testDirectory = Path.Combine(Path.GetTempPath(), $"SqliteConfigTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(this.testDirectory);
            this.testConfigPath = Path.Combine(this.testDirectory, "sqlite.json");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.testDirectory))
            {
                Directory.Delete(this.testDirectory, true);
            }
        }

        [TestMethod]
        [TestCategory("Configuration")]
        public void FromJsonFile_NoFile_ReturnsDefaultConfiguration()
        {
            // Arrange
            var nonExistentPath = Path.Combine(this.testDirectory, "nonexistent.json");

            // Act
            var config = SqliteConfiguration.FromJsonFile(nonExistentPath);

            // Assert
            config.Should().NotBeNull();
            config.RetryPolicy.Should().NotBeNull();
            config.RetryPolicy.Enabled.Should().BeTrue("Retry should be enabled by default");
            config.RetryPolicy.MaxAttempts.Should().Be(3);
            config.RetryPolicy.InitialDelayMs.Should().Be(100);
            config.RetryPolicy.MaxDelayMs.Should().Be(5000);
            config.RetryPolicy.BackoffMultiplier.Should().Be(2.0);
            config.BusyTimeout.Should().Be(5000);
            config.CommandTimeout.Should().Be(30);
        }

        [TestMethod]
        [TestCategory("Configuration")]
        public void FromJsonFile_EmptyJson_ReturnsDefaultConfiguration()
        {
            // Arrange
            File.WriteAllText(this.testConfigPath, "{}");

            // Act
            var config = SqliteConfiguration.FromJsonFile(this.testConfigPath);

            // Assert
            config.Should().NotBeNull();
            config.RetryPolicy.Should().NotBeNull();
            config.RetryPolicy.Enabled.Should().BeTrue("Retry should be enabled by default even with empty JSON");
            config.RetryPolicy.MaxAttempts.Should().Be(3);
        }

        [TestMethod]
        [TestCategory("Configuration")]
        public void FromJsonFile_WithoutRetryPolicy_UsesDefaultRetryConfiguration()
        {
            // Arrange
            var json = @"{
                ""DbFile"": ""test.db"",
                ""BusyTimeout"": 10000,
                ""CommandTimeout"": 60
            }";
            File.WriteAllText(this.testConfigPath, json);

            // Act
            var config = SqliteConfiguration.FromJsonFile(this.testConfigPath);

            // Assert
            config.Should().NotBeNull();
            config.DbFile.Should().Be("test.db");
            config.BusyTimeout.Should().Be(10000);
            config.CommandTimeout.Should().Be(60);
            
            // RetryPolicy should have default values with retry enabled
            config.RetryPolicy.Should().NotBeNull();
            config.RetryPolicy.Enabled.Should().BeTrue("Retry should be enabled by default when RetryPolicy is missing");
            config.RetryPolicy.MaxAttempts.Should().Be(3);
            config.RetryPolicy.InitialDelayMs.Should().Be(100);
            config.RetryPolicy.MaxDelayMs.Should().Be(5000);
            config.RetryPolicy.BackoffMultiplier.Should().Be(2.0);
        }

        [TestMethod]
        [TestCategory("Configuration")]
        public void FromJsonFile_WithPartialRetryPolicy_MergesWithDefaults()
        {
            // Arrange
            var json = @"{
                ""DbFile"": ""test.db"",
                ""RetryPolicy"": {
                    ""MaxAttempts"": 5,
                    ""InitialDelayMs"": 200
                }
            }";
            File.WriteAllText(this.testConfigPath, json);

            // Act
            var config = SqliteConfiguration.FromJsonFile(this.testConfigPath);

            // Assert
            config.Should().NotBeNull();
            config.DbFile.Should().Be("test.db");
            
            config.RetryPolicy.Should().NotBeNull();
            config.RetryPolicy.Enabled.Should().BeTrue("Enabled should default to true");
            config.RetryPolicy.MaxAttempts.Should().Be(5);
            config.RetryPolicy.InitialDelayMs.Should().Be(200);
            config.RetryPolicy.MaxDelayMs.Should().Be(5000, "MaxDelayMs should use default when not specified");
            config.RetryPolicy.BackoffMultiplier.Should().Be(2.0, "BackoffMultiplier should use default when not specified");
        }

        [TestMethod]
        [TestCategory("Configuration")]
        public void FromJsonFile_WithFullRetryPolicy_UsesProvidedValues()
        {
            // Arrange
            var json = @"{
                ""DbFile"": ""test.db"",
                ""RetryPolicy"": {
                    ""Enabled"": true,
                    ""MaxAttempts"": 10,
                    ""InitialDelayMs"": 500,
                    ""MaxDelayMs"": 10000,
                    ""BackoffMultiplier"": 1.5
                }
            }";
            File.WriteAllText(this.testConfigPath, json);

            // Act
            var config = SqliteConfiguration.FromJsonFile(this.testConfigPath);

            // Assert
            config.Should().NotBeNull();
            config.RetryPolicy.Should().NotBeNull();
            config.RetryPolicy.Enabled.Should().BeTrue();
            config.RetryPolicy.MaxAttempts.Should().Be(10);
            config.RetryPolicy.InitialDelayMs.Should().Be(500);
            config.RetryPolicy.MaxDelayMs.Should().Be(10000);
            config.RetryPolicy.BackoffMultiplier.Should().Be(1.5);
        }

        [TestMethod]
        [TestCategory("Configuration")]
        public void FromJsonFile_WithDisabledRetry_RespectsConfiguration()
        {
            // Arrange
            var json = @"{
                ""DbFile"": ""test.db"",
                ""RetryPolicy"": {
                    ""Enabled"": false,
                    ""MaxAttempts"": 0
                }
            }";
            File.WriteAllText(this.testConfigPath, json);

            // Act
            var config = SqliteConfiguration.FromJsonFile(this.testConfigPath);

            // Assert
            config.Should().NotBeNull();
            config.RetryPolicy.Should().NotBeNull();
            config.RetryPolicy.Enabled.Should().BeFalse("Should respect explicit Enabled=false");
            config.RetryPolicy.MaxAttempts.Should().Be(0);
        }

        [TestMethod]
        [TestCategory("Configuration")]
        public void FromJsonFile_WithSqliteConfigurationSection_LoadsCorrectly()
        {
            // Arrange
            var json = @"{
                ""SqliteConfiguration"": {
                    ""DbFile"": ""section-test.db"",
                    ""BusyTimeout"": 8000,
                    ""RetryPolicy"": {
                        ""MaxAttempts"": 7
                    }
                }
            }";
            File.WriteAllText(this.testConfigPath, json);

            // Act
            var config = SqliteConfiguration.FromJsonFile(this.testConfigPath);

            // Assert
            config.Should().NotBeNull();
            config.DbFile.Should().Be("section-test.db");
            config.BusyTimeout.Should().Be(8000);
            config.RetryPolicy.MaxAttempts.Should().Be(7);
            config.RetryPolicy.Enabled.Should().BeTrue("Should default to enabled");
        }

        [TestMethod]
        [TestCategory("Configuration")]
        public void FromJsonFile_WithInvalidRetryConfiguration_ThrowsException()
        {
            // Arrange
            var json = @"{
                ""DbFile"": ""test.db"",
                ""RetryPolicy"": {
                    ""MaxAttempts"": -1
                }
            }";
            File.WriteAllText(this.testConfigPath, json);

            // Act & Assert
            var act = () => SqliteConfiguration.FromJsonFile(this.testConfigPath);
            act.Should().Throw<InvalidOperationException>()
                .WithInnerException<ArgumentException>()
                .WithMessage("*MaxAttempts must be non-negative*");
        }

        [TestMethod]
        [TestCategory("Configuration")]
        public void FromJsonFile_WithCompleteConfiguration_LoadsAllValues()
        {
            // Arrange
            var json = @"{
                ""DbFile"": ""complete.db"",
                ""CacheSize"": -4000,
                ""PageSize"": 8192,
                ""JournalMode"": ""WAL"",
                ""SynchronousMode"": ""Full"",
                ""BusyTimeout"": 15000,
                ""EnableForeignKeys"": false,
                ""CommandTimeout"": 120,
                ""RetryPolicy"": {
                    ""Enabled"": true,
                    ""MaxAttempts"": 5,
                    ""InitialDelayMs"": 250,
                    ""MaxDelayMs"": 8000,
                    ""BackoffMultiplier"": 2.5
                }
            }";
            File.WriteAllText(this.testConfigPath, json);

            // Act
            var config = SqliteConfiguration.FromJsonFile(this.testConfigPath);

            // Assert
            config.Should().NotBeNull();
            config.DbFile.Should().Be("complete.db");
            config.CacheSize.Should().Be(-4000);
            config.PageSize.Should().Be(8192);
            config.JournalMode.Should().Be(JournalMode.WAL);
            config.SynchronousMode.Should().Be(SynchronousMode.Full);
            config.BusyTimeout.Should().Be(15000);
            config.EnableForeignKeys.Should().BeFalse();
            config.CommandTimeout.Should().Be(120);
            
            config.RetryPolicy.Should().NotBeNull();
            config.RetryPolicy.Enabled.Should().BeTrue();
            config.RetryPolicy.MaxAttempts.Should().Be(5);
            config.RetryPolicy.InitialDelayMs.Should().Be(250);
            config.RetryPolicy.MaxDelayMs.Should().Be(8000);
            config.RetryPolicy.BackoffMultiplier.Should().Be(2.5);
        }

        [TestMethod]
        [TestCategory("Configuration")]
        public void FromJsonFileRequired_FileNotFound_ThrowsFileNotFoundException()
        {
            // Arrange
            var nonExistentPath = Path.Combine(this.testDirectory, "nonexistent.json");

            // Act & Assert
            var act = () => SqliteConfiguration.FromJsonFileRequired(nonExistentPath);
            act.Should().Throw<FileNotFoundException>()
                .WithMessage($"*Required SQLite configuration file not found*");
        }

        [TestMethod]
        [TestCategory("Configuration")]
        public void FromJsonFileRequired_FileExists_LoadsConfiguration()
        {
            // Arrange
            var json = @"{
                ""DbFile"": ""required.db"",
                ""BusyTimeout"": 7000
            }";
            File.WriteAllText(this.testConfigPath, json);

            // Act
            var config = SqliteConfiguration.FromJsonFileRequired(this.testConfigPath);

            // Assert
            config.Should().NotBeNull();
            config.DbFile.Should().Be("required.db");
            config.BusyTimeout.Should().Be(7000);
            config.RetryPolicy.Should().NotBeNull();
            config.RetryPolicy.Enabled.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Configuration")]
        public void RetryConfiguration_TimeSpanProperties_CalculateCorrectly()
        {
            // Arrange
            var json = @"{
                ""RetryPolicy"": {
                    ""InitialDelayMs"": 150,
                    ""MaxDelayMs"": 3000
                }
            }";
            File.WriteAllText(this.testConfigPath, json);

            // Act
            var config = SqliteConfiguration.FromJsonFile(this.testConfigPath);

            // Assert
            config.RetryPolicy.InitialDelay.Should().Be(TimeSpan.FromMilliseconds(150));
            config.RetryPolicy.MaxDelay.Should().Be(TimeSpan.FromMilliseconds(3000));
        }
    }
}