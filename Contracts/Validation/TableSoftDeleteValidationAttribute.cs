//-------------------------------------------------------------------------------
// <copyright file="TableSoftDeleteValidationAttribute.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Validation
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Reflection;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    /// <summary>
    /// Validates that classes decorated with <see cref="TableAttribute"/> where SoftDeleteEnabled=true 
    /// have a Version property of type long.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class TableSoftDeleteValidationAttribute : ValidationAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TableSoftDeleteValidationAttribute"/> class.
        /// </summary>
        public TableSoftDeleteValidationAttribute()
            : base("Classes with [Table] attribute where SoftDeleteEnabled=true must have a Version property of type long.")
        {
        }

        /// <summary>
        /// Validates that the class has a Version property when Table attribute has SoftDeleteEnabled=true.
        /// </summary>
        /// <param name="value">The object to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>A <see cref="ValidationResult"/> indicating success or failure.</returns>
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null)
            {
                return ValidationResult.Success;
            }

            Type type = value.GetType();
            
            // Check if the class has Table attribute
            var tableAttribute = type.GetCustomAttribute<TableAttribute>();
            if (tableAttribute == null)
            {
                return ValidationResult.Success;
            }

            // Only validate if SoftDeleteEnabled is true
            if (!tableAttribute.SoftDeleteEnabled)
            {
                return ValidationResult.Success;
            }

            // Check for Version property
            var versionProperty = type.GetProperty("Version", 
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            if (versionProperty == null)
            {
                return new ValidationResult(
                    $"Type '{type.Name}' has [Table] attribute with SoftDeleteEnabled=true but does not have a Version property.",
                    new[] { "Version" });
            }

            // Validate that Version property is of type long
            if (versionProperty.PropertyType != typeof(long))
            {
                return new ValidationResult(
                    $"Type '{type.Name}' has Version property but it is of type '{versionProperty.PropertyType.Name}' instead of 'long'.",
                    new[] { "Version" });
            }

            // Check if Version property is writable
            if (!versionProperty.CanWrite)
            {
                return new ValidationResult(
                    $"Type '{type.Name}' has Version property but it is read-only. Version property must have a setter.",
                    new[] { "Version" });
            }

            return ValidationResult.Success;
        }

        /// <summary>
        /// Validates a type (static validation method).
        /// </summary>
        /// <param name="type">The type to validate.</param>
        /// <returns>A <see cref="ValidationResult"/> indicating success or failure.</returns>
        public static ValidationResult ValidateType(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            var tableAttribute = type.GetCustomAttribute<TableAttribute>();
            if (tableAttribute == null)
            {
                return ValidationResult.Success;
            }

            if (!tableAttribute.SoftDeleteEnabled)
            {
                return ValidationResult.Success;
            }

            var versionProperties = type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .Where(p => p.Name.Equals("Version", StringComparison.Ordinal))
                .ToList();

            PropertyInfo versionProperty;
            if (versionProperties.Count > 1)
            {
                versionProperty = versionProperties.OrderBy(p => p.DeclaringType, new TypeHierarchyComparer(type)).First();
            }
            else
            {
                versionProperty = versionProperties.FirstOrDefault();
            }

            if (versionProperty == null)
            {
                return new ValidationResult(
                    $"Type '{type.FullName}' has [Table] attribute with SoftDeleteEnabled=true but does not have a Version property.");
            }

            if (versionProperty.PropertyType != typeof(long))
            {
                return new ValidationResult(
                    $"Type '{type.FullName}' has Version property but it is of type '{versionProperty.PropertyType.Name}' instead of 'long'.");
            }

            if (versionProperty.CanWrite != true)
            {
                return new ValidationResult(
                    $"Type '{type.FullName}' has Version property but it is read-only. Version property must have a setter.");
            }

            return ValidationResult.Success;
        }

        /// <summary>
        /// Validates all types in an assembly that have Table attributes.
        /// </summary>
        /// <param name="assembly">The assembly to validate.</param>
        /// <returns>An array of validation results for types that failed validation.</returns>
        public static ValidationResult[] ValidateAssembly(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            var typesWithTableAttribute = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<TableAttribute>() != null)
                .ToArray();

            var validationResults = typesWithTableAttribute
                .Select(type => new { Type = type, Result = ValidateType(type) })
                .Where(x => x.Result != ValidationResult.Success)
                .Select(x => x.Result)
                .ToArray();

            return validationResults;
        }
    }
}