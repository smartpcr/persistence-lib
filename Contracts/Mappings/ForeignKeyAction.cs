//-------------------------------------------------------------------------------
// <copyright file="ForeignKeyAction.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings
{
    /// <summary>
    /// Defines the actions that can be taken when a foreign key constraint is violated.
    /// </summary>
    public enum ForeignKeyAction
    {
        /// <summary>
        /// No action is taken (default behavior).
        /// </summary>
        NoAction,

        /// <summary>
        /// The delete or update is cascaded to the referencing rows.
        /// </summary>
        Cascade,

        /// <summary>
        /// The foreign key values in the referencing rows are set to NULL.
        /// </summary>
        SetNull,

        /// <summary>
        /// The foreign key values in the referencing rows are set to their default values.
        /// </summary>
        SetDefault,

        /// <summary>
        /// The delete or update is restricted if there are referencing rows.
        /// </summary>
        Restrict
    }
}