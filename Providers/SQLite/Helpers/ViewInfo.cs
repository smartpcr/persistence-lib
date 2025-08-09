// -----------------------------------------------------------------------
// <copyright file="ViewInfo.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Helpers
{
    /// <summary>
    /// Represents view information.
    /// </summary>
    public class ViewInfo
    {
        /// <summary>
        /// Gets or sets the view name.
        /// </summary>
        public string ViewName { get; set; }

        /// <summary>
        /// Gets or sets the SQL create statement.
        /// </summary>
        public string CreateSql { get; set; }

        /// <summary>
        /// Gets or sets the root page number.
        /// </summary>
        public int RootPage { get; set; }
    }
}