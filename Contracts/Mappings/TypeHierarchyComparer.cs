//-------------------------------------------------------------------------------
// <copyright file="TypeHierarchyComparer.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Comparer that orders types by their position in the inheritance hierarchy.
    /// The most derived type (furthest from the base) comes first.
    /// </summary>
    internal class TypeHierarchyComparer : IComparer<Type>
    {
        private readonly Type leafType;

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeHierarchyComparer"/> class.
        /// </summary>
        /// <param name="leafType">The most derived type in the hierarchy</param>
        public TypeHierarchyComparer(Type leafType)
        {
            this.leafType = leafType ?? throw new ArgumentNullException(nameof(leafType));
        }

        /// <summary>
        /// Compares two types based on their position in the inheritance hierarchy.
        /// </summary>
        /// <param name="x">The first type to compare</param>
        /// <param name="y">The second type to compare</param>
        /// <returns>
        /// -1 if x is more derived than y,
        /// 1 if y is more derived than x,
        /// 0 if they are at the same level or not in the same hierarchy
        /// </returns>
        public int Compare(Type x, Type y)
        {
            if (x == y)
            {
                return 0;
            }

            // Get the distance from each type to the leaf type
            var xDistance = this.GetDistanceToLeaf(x);
            var yDistance = this.GetDistanceToLeaf(y);

            // If either type is not in the hierarchy, treat them as equal
            if (xDistance == -1 || yDistance == -1)
            {
                return 0;
            }

            // The type closer to the leaf (smaller distance) should come first
            return xDistance.CompareTo(yDistance);
        }

        /// <summary>
        /// Gets the distance from a type to the leaf type in the inheritance hierarchy.
        /// </summary>
        /// <param name="type">The type to measure distance from</param>
        /// <returns>The number of inheritance levels to the leaf type, or -1 if not in hierarchy</returns>
        private int GetDistanceToLeaf(Type type)
        {
            if (type == null)
            {
                return -1;
            }

            // Check if the leaf type is assignable from the given type
            // This means type is an ancestor of leafType
            if (!type.IsAssignableFrom(this.leafType))
            {
                return -1;
            }

            // Count the distance from leafType up to type
            int distance = 0;
            Type current = this.leafType;

            while (current != null && current != type)
            {
                current = current.BaseType;
                distance++;
            }

            return current == type ? distance : -1;
        }
    }
}