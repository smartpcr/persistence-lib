// -----------------------------------------------------------------------
// <copyright file="ColumnInfo.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Helpers
{
    /// <summary>
    /// Represents column information.
    /// </summary>
    public class ColumnInfo
    {
        /// <summary>
        /// Gets or sets the column ID.
        /// </summary>
        public int ColumnId { get; set; }

        /// <summary>
        /// Gets or sets the column name.
        /// </summary>
        public string ColumnName { get; set; }

        /// <summary>
        /// Gets or sets the data type.
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// Gets or sets whether the column can be null.
        /// </summary>
        public bool IsNullable { get; set; }

        /// <summary>
        /// Gets or sets the default value.
        /// </summary>
        public string DefaultValue { get; set; }

        /// <summary>
        /// Gets or sets whether this column is part of the primary key.
        /// </summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>
        /// Gets or sets whether this column is auto-increment.
        /// </summary>
        public bool IsAutoIncrement { get; set; }

        /// <summary>
        /// Gets or sets whether this column is unique.
        /// </summary>
        public bool IsUnique { get; set; }

        /// <summary>
        /// Gets or sets the collation sequence.
        /// </summary>
        public string Collation { get; set; }

        /// <summary>
        /// Gets or sets whether this column is hidden.
        /// </summary>
        public bool IsHidden { get; set; }

        /// <summary>
        /// Gets or sets whether this column is generated.
        /// </summary>
        public bool IsGenerated { get; set; }
    }
}