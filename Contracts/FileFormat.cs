//-------------------------------------------------------------------------------
// <copyright file="FileFormat.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    /// <summary>
    /// Supported file formats for bulk import/export operations.
    /// </summary>
    public enum FileFormat
    {
        /// <summary>
        /// JSON format (default) - supports complex objects and nested structures.
        /// </summary>
        Json = 0,

        /// <summary>
        /// CSV format - flat structure, better for simple entities and spreadsheet compatibility.
        /// </summary>
        Csv = 1,

        /// <summary>
        /// Auto-detect format based on file extension.
        /// </summary>
        Auto = 2
    }
}