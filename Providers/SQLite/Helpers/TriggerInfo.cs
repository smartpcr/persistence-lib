// -----------------------------------------------------------------------
// <copyright file="TriggerInfo.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Helpers
{
    /// <summary>
    /// Represents trigger information.
    /// </summary>
    public class TriggerInfo
    {
        /// <summary>
        /// Gets or sets the trigger name.
        /// </summary>
        public string TriggerName { get; set; }

        /// <summary>
        /// Gets or sets the table name.
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Gets or sets the SQL create statement.
        /// </summary>
        public string CreateSql { get; set; }

        /// <summary>
        /// Gets or sets the trigger event (INSERT, UPDATE, DELETE).
        /// </summary>
        public string TriggerEvent { get; set; }

        /// <summary>
        /// Gets or sets the trigger timing (BEFORE, AFTER, INSTEAD OF).
        /// </summary>
        public string TriggerTiming { get; set; }
    }
}