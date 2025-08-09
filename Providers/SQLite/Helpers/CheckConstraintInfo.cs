// -----------------------------------------------------------------------
// <copyright file="CheckConstraintInfo.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Helpers
{
    /// <summary>
    /// Represents check constraint information.
    /// </summary>
    public class CheckConstraintInfo
    {
        /// <summary>
        /// Gets or sets the constraint name.
        /// </summary>
        public string ConstraintName { get; set; }

        /// <summary>
        /// Gets or sets the table name.
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Gets or sets the check expression.
        /// </summary>
        public string CheckExpression { get; set; }
    }
}