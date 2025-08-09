// -----------------------------------------------------------------------
// <copyright file="ForeignKeyInfo.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Helpers
{
    /// <summary>
    /// Represents foreign key constraint information.
    /// </summary>
    public class ForeignKeyInfo
    {
        /// <summary>
        /// Gets or sets the constraint ID.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the sequence number.
        /// </summary>
        public int Sequence { get; set; }

        /// <summary>
        /// Gets or sets the source table name.
        /// </summary>
        public string FromTable { get; set; }

        /// <summary>
        /// Gets or sets the source column name.
        /// </summary>
        public string FromColumn { get; set; }

        /// <summary>
        /// Gets or sets the referenced table name.
        /// </summary>
        public string ToTable { get; set; }

        /// <summary>
        /// Gets or sets the referenced column name.
        /// </summary>
        public string ToColumn { get; set; }

        /// <summary>
        /// Gets or sets the ON UPDATE action.
        /// </summary>
        public string OnUpdate { get; set; }

        /// <summary>
        /// Gets or sets the ON DELETE action.
        /// </summary>
        public string OnDelete { get; set; }

        /// <summary>
        /// Gets or sets the MATCH clause.
        /// </summary>
        public string Match { get; set; }
    }
}