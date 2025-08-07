//-------------------------------------------------------------------------------
// <copyright file="TableSoftDeleteValidator.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    /// <summary>
    /// Static validator for Table attribute soft delete requirements.
    /// </summary>
    public static class TableSoftDeleteValidator
    {
        /// <summary>
        /// Validates that a type with Table attribute and SoftDeleteEnabled=true has a proper Version property.
        /// </summary>
        /// <param name="type">The type to validate.</param>
        /// <exception cref="ArgumentNullException">Thrown when type is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
        public static void ValidateType(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            var tableAttribute = type.GetCustomAttribute<TableAttribute>();
            if (tableAttribute == null || !tableAttribute.SoftDeleteEnabled)
            {
                return;
            }

            var versionProperty = type.GetProperty("Version",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            if (versionProperty == null)
            {
                throw new InvalidOperationException(
                    $"Type '{type.FullName}' has [Table] attribute with SoftDeleteEnabled=true but does not have a Version property. " +
                    $"Classes with soft delete enabled must have a public Version property of type long.");
            }

            if (versionProperty.PropertyType != typeof(long))
            {
                throw new InvalidOperationException(
                    $"Type '{type.FullName}' has Version property but it is of type '{versionProperty.PropertyType.FullName}' instead of 'System.Int64'. " +
                    $"The Version property must be of type long.");
            }

            if (!versionProperty.CanWrite)
            {
                throw new InvalidOperationException(
                    $"Type '{type.FullName}' has Version property but it is read-only. " +
                    $"The Version property must have a setter.");
            }
        }

        /// <summary>
        /// Validates all types in an assembly that have Table attributes.
        /// </summary>
        /// <param name="assembly">The assembly to validate.</param>
        /// <exception cref="ArgumentNullException">Thrown when assembly is null.</exception>
        /// <exception cref="AggregateException">Thrown when one or more types fail validation.</exception>
        public static void ValidateAssembly(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            var typesWithTableAttribute = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<TableAttribute>() != null)
                .ToArray();

            var validationErrors = new List<Exception>();

            foreach (var type in typesWithTableAttribute)
            {
                try
                {
                    ValidateType(type);
                }
                catch (InvalidOperationException ex)
                {
                    validationErrors.Add(ex);
                }
            }

            if (validationErrors.Any())
            {
                throw new AggregateException(
                    $"Validation failed for {validationErrors.Count} type(s) in assembly '{assembly.GetName().Name}'.",
                    validationErrors);
            }
        }

        /// <summary>
        /// Validates all loaded assemblies for Table attribute soft delete requirements.
        /// </summary>
        /// <returns>A dictionary of assembly names to their validation errors, if any.</returns>
        public static Dictionary<string, Exception[]> ValidateAllLoadedAssemblies()
        {
            var results = new Dictionary<string, Exception[]>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !a.GlobalAssemblyCache)
                .ToArray();

            foreach (var assembly in assemblies)
            {
                try
                {
                    ValidateAssembly(assembly);
                }
                catch (AggregateException aggEx)
                {
                    results[assembly.GetName().Name] = aggEx.InnerExceptions.ToArray();
                }
            }

            return results;
        }

        /// <summary>
        /// Checks if a type is valid according to Table soft delete requirements.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type is valid, false otherwise.</returns>
        public static bool IsTypeValid(Type type)
        {
            if (type == null)
            {
                return false;
            }

            try
            {
                ValidateType(type);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}