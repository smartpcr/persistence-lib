//-------------------------------------------------------------------------------
// <copyright file="TableSoftDeleteValidationAttributeTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Validation
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Validation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="TableSoftDeleteValidationAttribute"/>.
    /// </summary>
    [TestClass]
    public class TableSoftDeleteValidationAttributeTests
    {
        [TestMethod]
        [TestCategory("UnitTest")]
        public void IsValid_WhenObjectIsNull_ReturnsSuccess()
        {
            // Arrange
            var attribute = new TableSoftDeleteValidationAttribute();
            var validationContext = new ValidationContext(new object());

            // Act
            var result = attribute.GetValidationResult(null, validationContext);

            // Assert
            result.Should().Be(ValidationResult.Success);
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void IsValid_WhenClassHasNoTableAttribute_ReturnsSuccess()
        {
            // Arrange
            var attribute = new TableSoftDeleteValidationAttribute();
            var testObject = new ClassWithoutTableAttribute();
            var validationContext = new ValidationContext(testObject);

            // Act
            var result = attribute.GetValidationResult(testObject, validationContext);

            // Assert
            result.Should().Be(ValidationResult.Success);
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void IsValid_WhenSoftDeleteEnabledIsFalse_ReturnsSuccess()
        {
            // Arrange
            var attribute = new TableSoftDeleteValidationAttribute();
            var testObject = new ClassWithSoftDeleteDisabled();
            var validationContext = new ValidationContext(testObject);

            // Act
            var result = attribute.GetValidationResult(testObject, validationContext);

            // Assert
            result.Should().Be(ValidationResult.Success);
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void IsValid_WhenSoftDeleteEnabledAndHasValidVersionProperty_ReturnsSuccess()
        {
            // Arrange
            var attribute = new TableSoftDeleteValidationAttribute();
            var testObject = new ValidClassWithSoftDeleteEnabled();
            var validationContext = new ValidationContext(testObject);

            // Act
            var result = attribute.GetValidationResult(testObject, validationContext);

            // Assert
            result.Should().Be(ValidationResult.Success);
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void IsValid_WhenSoftDeleteEnabledAndMissingVersionProperty_ReturnsValidationError()
        {
            // Arrange
            var attribute = new TableSoftDeleteValidationAttribute();
            var testObject = new ClassWithSoftDeleteEnabledButNoVersion();
            var validationContext = new ValidationContext(testObject);

            // Act
            var result = attribute.GetValidationResult(testObject, validationContext);

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBe(ValidationResult.Success);
            result.ErrorMessage.Should().Contain("does not have a Version property");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void IsValid_WhenVersionPropertyIsWrongType_ReturnsValidationError()
        {
            // Arrange
            var attribute = new TableSoftDeleteValidationAttribute();
            var testObject = new ClassWithWrongVersionType();
            var validationContext = new ValidationContext(testObject);

            // Act
            var result = attribute.GetValidationResult(testObject, validationContext);

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBe(ValidationResult.Success);
            result.ErrorMessage.Should().Contain("instead of 'long'");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void IsValid_WhenVersionPropertyIsReadOnly_ReturnsValidationError()
        {
            // Arrange
            var attribute = new TableSoftDeleteValidationAttribute();
            var testObject = new ClassWithReadOnlyVersion();
            var validationContext = new ValidationContext(testObject);

            // Act
            var result = attribute.GetValidationResult(testObject, validationContext);

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBe(ValidationResult.Success);
            result.ErrorMessage.Should().Contain("read-only");
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void IsValid_WhenInheritedClassHasVersionProperty_ReturnsSuccess()
        {
            // Arrange
            var attribute = new TableSoftDeleteValidationAttribute();
            var testObject = new InheritedClassWithVersion();
            var validationContext = new ValidationContext(testObject);

            // Act
            var result = attribute.GetValidationResult(testObject, validationContext);

            // Assert
            result.Should().Be(ValidationResult.Success);
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void ValidateType_WhenTypeIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            Action act = () => TableSoftDeleteValidationAttribute.ValidateType(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void ValidateType_WhenValidType_ReturnsSuccess()
        {
            // Act
            var result = TableSoftDeleteValidationAttribute.ValidateType(typeof(ValidClassWithSoftDeleteEnabled));

            // Assert
            result.Should().Be(ValidationResult.Success);
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void ValidateType_WhenInvalidType_ReturnsValidationError()
        {
            // Act
            var result = TableSoftDeleteValidationAttribute.ValidateType(typeof(ClassWithSoftDeleteEnabledButNoVersion));

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBe(ValidationResult.Success);
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void ValidateAssembly_WhenAssemblyIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            Action act = () => TableSoftDeleteValidationAttribute.ValidateAssembly(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void ValidateAssembly_ReturnsOnlyFailedValidations()
        {
            // Arrange
            var assembly = typeof(TableSoftDeleteValidationAttributeTests).Assembly;

            // Act
            var results = TableSoftDeleteValidationAttribute.ValidateAssembly(assembly);

            // Assert
            // Should find at least our test classes that fail validation
            results.Length.Should().BeGreaterThan(0);
            foreach (var result in results)
            {
                result.Should().NotBe(ValidationResult.Success);
            }
        }

        private class ClassWithoutTableAttribute
        {
            public long Version { get; set; }
        }

        [Table("TestTable", SoftDeleteEnabled = false)]
        private class ClassWithSoftDeleteDisabled
        {
            public long Version { get; set; }
        }

        [Table("TestTable", SoftDeleteEnabled = true)]
        private class ValidClassWithSoftDeleteEnabled
        {
            public long Version { get; set; }
        }

        [Table("TestTable", SoftDeleteEnabled = true)]
        private class ClassWithSoftDeleteEnabledButNoVersion
        {
            public string Name { get; set; }
        }

        [Table("TestTable", SoftDeleteEnabled = true)]
        private class ClassWithWrongVersionType
        {
            public int Version { get; set; }
        }

        [Table("TestTable", SoftDeleteEnabled = true)]
        private class ClassWithReadOnlyVersion
        {
            public long Version { get; }
        }

        [Table("TestTable", SoftDeleteEnabled = true)]
        private class InheritedClassWithVersion : ValidClassWithSoftDeleteEnabled
        {
            public string Name { get; set; }
        }
    }
}