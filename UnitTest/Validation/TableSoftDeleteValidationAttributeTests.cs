//-------------------------------------------------------------------------------
// <copyright file="TableSoftDeleteValidationAttributeTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Validation
{
    using System;
    using System.ComponentModel.DataAnnotations;
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
            Assert.AreEqual(ValidationResult.Success, result);
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
            Assert.AreEqual(ValidationResult.Success, result);
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
            Assert.AreEqual(ValidationResult.Success, result);
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
            Assert.AreEqual(ValidationResult.Success, result);
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
            Assert.IsNotNull(result);
            Assert.AreNotEqual(ValidationResult.Success, result);
            Assert.IsTrue(result.ErrorMessage.Contains("does not have a Version property"));
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
            Assert.IsNotNull(result);
            Assert.AreNotEqual(ValidationResult.Success, result);
            Assert.IsTrue(result.ErrorMessage.Contains("instead of 'long'"));
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
            Assert.IsNotNull(result);
            Assert.AreNotEqual(ValidationResult.Success, result);
            Assert.IsTrue(result.ErrorMessage.Contains("read-only"));
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
            Assert.AreEqual(ValidationResult.Success, result);
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void ValidateType_WhenTypeIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => 
                TableSoftDeleteValidationAttribute.ValidateType(null));
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void ValidateType_WhenValidType_ReturnsSuccess()
        {
            // Act
            var result = TableSoftDeleteValidationAttribute.ValidateType(typeof(ValidClassWithSoftDeleteEnabled));

            // Assert
            Assert.AreEqual(ValidationResult.Success, result);
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void ValidateType_WhenInvalidType_ReturnsValidationError()
        {
            // Act
            var result = TableSoftDeleteValidationAttribute.ValidateType(typeof(ClassWithSoftDeleteEnabledButNoVersion));

            // Assert
            Assert.IsNotNull(result);
            Assert.AreNotEqual(ValidationResult.Success, result);
        }

        [TestMethod]
        [TestCategory("UnitTest")]
        public void ValidateAssembly_WhenAssemblyIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => 
                TableSoftDeleteValidationAttribute.ValidateAssembly(null));
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
            Assert.IsTrue(results.Length > 0);
            foreach (var result in results)
            {
                Assert.AreNotEqual(ValidationResult.Success, result);
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