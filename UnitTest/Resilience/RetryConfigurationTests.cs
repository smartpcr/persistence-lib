// -----------------------------------------------------------------------
// <copyright file="RetryConfigurationTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Resilience
{
    using System;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Config;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Resilience;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class RetryConfigurationTests
    {
        [TestMethod]
        [TestCategory("Resilience")]
        public void Default_Configuration_HasExpectedValues()
        {
            // Arrange & Act
            var config = RetryConfiguration.Default;

            // Assert
            config.Enabled.Should().BeTrue();
            config.MaxAttempts.Should().Be(3);
            config.InitialDelayMs.Should().Be(100);
            config.MaxDelayMs.Should().Be(5000);
            config.BackoffMultiplier.Should().Be(2.0);
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void NoRetry_Configuration_DisablesRetries()
        {
            // Arrange & Act
            var config = RetryConfiguration.NoRetry;

            // Assert
            config.Enabled.Should().BeFalse();
            config.MaxAttempts.Should().Be(0);
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void ForNetworkStorage_Configuration_HasIncreasedValues()
        {
            // Arrange & Act
            var config = RetryConfiguration.ForNetworkStorage;

            // Assert
            config.Enabled.Should().BeTrue();
            config.MaxAttempts.Should().Be(5);
            config.InitialDelayMs.Should().Be(500);
            config.MaxDelayMs.Should().Be(10000);
            config.BackoffMultiplier.Should().Be(2.0);
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void ForHighContention_Configuration_HasManyRetries()
        {
            // Arrange & Act
            var config = RetryConfiguration.ForHighContention;

            // Assert
            config.Enabled.Should().BeTrue();
            config.MaxAttempts.Should().Be(10);
            config.InitialDelayMs.Should().Be(50);
            config.MaxDelayMs.Should().Be(2000);
            config.BackoffMultiplier.Should().Be(1.5);
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void TimeSpan_Properties_ReturnCorrectValues()
        {
            // Arrange
            var config = new RetryConfiguration
            {
                InitialDelayMs = 250,
                MaxDelayMs = 7500
            };

            // Act & Assert
            config.InitialDelay.Should().Be(TimeSpan.FromMilliseconds(250));
            config.MaxDelay.Should().Be(TimeSpan.FromMilliseconds(7500));
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void Validate_ValidConfiguration_DoesNotThrow()
        {
            // Arrange
            var config = new RetryConfiguration
            {
                MaxAttempts = 5,
                InitialDelayMs = 100,
                MaxDelayMs = 5000,
                BackoffMultiplier = 2.0
            };

            // Act & Assert
            config.Invoking(c => c.Validate()).Should().NotThrow();
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void Validate_NegativeMaxAttempts_Throws()
        {
            // Arrange
            var config = new RetryConfiguration { MaxAttempts = -1 };

            // Act & Assert
            config.Invoking(c => c.Validate())
                .Should().Throw<ArgumentException>()
                .WithMessage("*MaxAttempts*");
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void Validate_NegativeInitialDelay_Throws()
        {
            // Arrange
            var config = new RetryConfiguration { InitialDelayMs = -100 };

            // Act & Assert
            config.Invoking(c => c.Validate())
                .Should().Throw<ArgumentException>()
                .WithMessage("*InitialDelayMs*");
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void Validate_MaxDelayLessThanInitial_Throws()
        {
            // Arrange
            var config = new RetryConfiguration
            {
                InitialDelayMs = 1000,
                MaxDelayMs = 500
            };

            // Act & Assert
            config.Invoking(c => c.Validate())
                .Should().Throw<ArgumentException>()
                .WithMessage("*MaxDelayMs*");
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void Validate_BackoffMultiplierLessThanOne_Throws()
        {
            // Arrange
            var config = new RetryConfiguration { BackoffMultiplier = 0.5 };

            // Act & Assert
            config.Invoking(c => c.Validate())
                .Should().Throw<ArgumentException>()
                .WithMessage("*BackoffMultiplier*");
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void Clone_CreatesIndependentCopy()
        {
            // Arrange
            var original = new RetryConfiguration
            {
                Enabled = true,
                MaxAttempts = 5,
                InitialDelayMs = 200,
                MaxDelayMs = 8000,
                BackoffMultiplier = 3.0
            };

            // Act
            var clone = original.Clone();
            original.MaxAttempts = 10;

            // Assert
            clone.Should().NotBeSameAs(original);
            clone.Enabled.Should().Be(true);
            clone.MaxAttempts.Should().Be(5);
            clone.InitialDelayMs.Should().Be(200);
            clone.MaxDelayMs.Should().Be(8000);
            clone.BackoffMultiplier.Should().Be(3.0);
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void RetryPolicy_FromConfiguration_CreatesPolicy()
        {
            // Arrange
            var config = new RetryConfiguration
            {
                Enabled = true,
                MaxAttempts = 3,
                InitialDelayMs = 100,
                MaxDelayMs = 5000,
                BackoffMultiplier = 2.0
            };

            // Act
            var policy = RetryPolicy.FromConfiguration(config);

            // Assert
            policy.Should().NotBeNull();
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void RetryPolicy_FromDisabledConfiguration_ReturnsNull()
        {
            // Arrange
            var config = new RetryConfiguration { Enabled = false };

            // Act
            var policy = RetryPolicy.FromConfiguration(config);

            // Assert
            policy.Should().BeNull();
        }

        [TestMethod]
        [TestCategory("Resilience")]
        public void RetryPolicy_FromZeroAttemptsConfiguration_ReturnsNull()
        {
            // Arrange
            var config = new RetryConfiguration
            {
                Enabled = true,
                MaxAttempts = 0
            };

            // Act
            var policy = RetryPolicy.FromConfiguration(config);

            // Assert
            policy.Should().BeNull();
        }
    }
}