//-------------------------------------------------------------------------------
// <copyright file="TableExpirationValidationAttribute.cs" company="Microsoft Corp.">
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
    /// Validates that classes decorated with <see cref="TableAttribute"/> where ExpirySpan is set 
    /// have appropriate expiration properties.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class TableExpirationValidationAttribute : ValidationAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TableExpirationValidationAttribute"/> class.
        /// </summary>
        public TableExpirationValidationAttribute()
            : base("Classes with [Table] attribute where ExpirySpan is set must have CreationTime and AbsoluteExpiration properties.")
        {
        }

        /// <summary>
        /// Validates that the class has appropriate expiration properties when Table attribute has ExpirySpan set.
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

            // Only validate if ExpirySpan is set
            if (!tableAttribute.ExpirySpan.HasValue)
            {
                return ValidationResult.Success;
            }

            // Check for CreationTime property
            var creationTimeProperty = type.GetProperty("CreationTime", 
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            if (creationTimeProperty == null)
            {
                return new ValidationResult(
                    $"Type '{type.Name}' has [Table] attribute with ExpirySpan set but does not have a CreationTime property.",
                    new[] { "CreationTime" });
            }

            // Validate that CreationTime property is of type DateTimeOffset or DateTimeOffset?
            if (creationTimeProperty.PropertyType != typeof(DateTimeOffset) && 
                creationTimeProperty.PropertyType != typeof(DateTimeOffset?))
            {
                return new ValidationResult(
                    $"Type '{type.Name}' has CreationTime property but it is of type '{creationTimeProperty.PropertyType.Name}' instead of 'DateTimeOffset' or 'DateTimeOffset?'.",
                    new[] { "CreationTime" });
            }

            // Check if CreationTime property is writable
            if (!creationTimeProperty.CanWrite)
            {
                return new ValidationResult(
                    $"Type '{type.Name}' has CreationTime property but it is read-only. CreationTime property must have a setter.",
                    new[] { "CreationTime" });
            }

            // Check for AbsoluteExpiration property
            var absoluteExpirationProperty = type.GetProperty("AbsoluteExpiration", 
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            if (absoluteExpirationProperty == null)
            {
                return new ValidationResult(
                    $"Type '{type.Name}' has [Table] attribute with ExpirySpan set but does not have an AbsoluteExpiration property.",
                    new[] { "AbsoluteExpiration" });
            }

            // Validate that AbsoluteExpiration property is of type DateTimeOffset or DateTimeOffset?
            if (absoluteExpirationProperty.PropertyType != typeof(DateTimeOffset) && 
                absoluteExpirationProperty.PropertyType != typeof(DateTimeOffset?))
            {
                return new ValidationResult(
                    $"Type '{type.Name}' has AbsoluteExpiration property but it is of type '{absoluteExpirationProperty.PropertyType.Name}' instead of 'DateTimeOffset' or 'DateTimeOffset?'.",
                    new[] { "AbsoluteExpiration" });
            }

            // Check if AbsoluteExpiration property is writable
            if (!absoluteExpirationProperty.CanWrite)
            {
                return new ValidationResult(
                    $"Type '{type.Name}' has AbsoluteExpiration property but it is read-only. AbsoluteExpiration property must have a setter.",
                    new[] { "AbsoluteExpiration" });
            }

            // If EnableArchive is true, check for IsArchived property
            if (tableAttribute.EnableArchive)
            {
                var isArchivedProperty = type.GetProperty("IsArchived",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

                if (isArchivedProperty == null)
                {
                    return new ValidationResult(
                        $"Type '{type.Name}' has [Table] attribute with EnableArchive=true but does not have an IsArchived property.",
                        new[] { "IsArchived" });
                }

                if (isArchivedProperty.PropertyType != typeof(bool))
                {
                    return new ValidationResult(
                        $"Type '{type.Name}' has IsArchived property but it is of type '{isArchivedProperty.PropertyType.Name}' instead of 'bool'.",
                        new[] { "IsArchived" });
                }

                if (!isArchivedProperty.CanWrite)
                {
                    return new ValidationResult(
                        $"Type '{type.Name}' has IsArchived property but it is read-only. IsArchived property must have a setter.",
                        new[] { "IsArchived" });
                }
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
            if (tableAttribute == null || !tableAttribute.ExpirySpan.HasValue)
            {
                return ValidationResult.Success;
            }

            // Check CreationTime property
            var creationTimeProperty = type.GetProperty("CreationTime",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            if (creationTimeProperty == null)
            {
                return new ValidationResult(
                    $"Type '{type.FullName}' has [Table] attribute with ExpirySpan set but does not have a CreationTime property.");
            }

            if (creationTimeProperty.PropertyType != typeof(DateTimeOffset) && 
                creationTimeProperty.PropertyType != typeof(DateTimeOffset?))
            {
                return new ValidationResult(
                    $"Type '{type.FullName}' has CreationTime property but it is of type '{creationTimeProperty.PropertyType.FullName}' instead of 'System.DateTimeOffset' or 'System.Nullable<System.DateTimeOffset>'.");
            }

            if (!creationTimeProperty.CanWrite)
            {
                return new ValidationResult(
                    $"Type '{type.FullName}' has CreationTime property but it is read-only. CreationTime property must have a setter.");
            }

            // Check AbsoluteExpiration property
            var absoluteExpirationProperty = type.GetProperty("AbsoluteExpiration",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            if (absoluteExpirationProperty == null)
            {
                return new ValidationResult(
                    $"Type '{type.FullName}' has [Table] attribute with ExpirySpan set but does not have an AbsoluteExpiration property.");
            }

            if (absoluteExpirationProperty.PropertyType != typeof(DateTimeOffset) && 
                absoluteExpirationProperty.PropertyType != typeof(DateTimeOffset?))
            {
                return new ValidationResult(
                    $"Type '{type.FullName}' has AbsoluteExpiration property but it is of type '{absoluteExpirationProperty.PropertyType.FullName}' instead of 'System.DateTimeOffset' or 'System.Nullable<System.DateTimeOffset>'.");
            }

            if (!absoluteExpirationProperty.CanWrite)
            {
                return new ValidationResult(
                    $"Type '{type.FullName}' has AbsoluteExpiration property but it is read-only. AbsoluteExpiration property must have a setter.");
            }

            if (tableAttribute.EnableArchive)
            {
                var isArchivedProperty = type.GetProperty("IsArchived",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

                if (isArchivedProperty == null)
                {
                    return new ValidationResult(
                        $"Type '{type.FullName}' has [Table] attribute with EnableArchive=true but does not have an IsArchived property.");
                }

                if (isArchivedProperty.PropertyType != typeof(bool))
                {
                    return new ValidationResult(
                        $"Type '{type.FullName}' has IsArchived property but it is of type '{isArchivedProperty.PropertyType.FullName}' instead of 'System.Boolean'.");
                }

                if (!isArchivedProperty.CanWrite)
                {
                    return new ValidationResult(
                        $"Type '{type.FullName}' has IsArchived property but it is read-only. IsArchived property must have a setter.");
                }
            }

            return ValidationResult.Success;
        }

        /// <summary>
        /// Validates all types in an assembly that have Table attributes with expiration support.
        /// </summary>
        /// <param name="assembly">The assembly to validate.</param>
        /// <returns>An array of validation results for types that failed validation.</returns>
        public static ValidationResult[] ValidateAssembly(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            var typesWithExpiration = assembly.GetTypes()
                .Where(t => 
                {
                    var attr = t.GetCustomAttribute<TableAttribute>();
                    return attr != null && attr.ExpirySpan.HasValue;
                })
                .ToArray();

            var validationResults = typesWithExpiration
                .Select(type => new { Type = type, Result = ValidateType(type) })
                .Where(x => x.Result != ValidationResult.Success)
                .Select(x => x.Result)
                .ToArray();

            return validationResults;
        }
    }
}