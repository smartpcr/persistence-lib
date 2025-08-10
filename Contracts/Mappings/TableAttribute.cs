//-------------------------------------------------------------------------------
// <copyright file="TableAttribute.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings
{
    using System;

    /// <summary>
    /// Specifies the database table name and schema for an entity class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class TableAttribute : Attribute
    {
        /// <summary>
        /// Gets the name of the table.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Specify entity types that this table depends on, via foreign keys.
        /// </summary>
        public Type[] Depends { get; set; } = null;

        /// <summary>
        /// Gets or sets the schema name. Default is null since SQLite does not support schema.
        /// For other DB provider that supports schema, it's default to "dbo".
        /// </summary>
        public string Schema { get; set; } = null;

        /// <summary>
        /// Gets or sets whether soft-delete is enabled for this table.
        /// When true, the primary key becomes composite (Id + Version) and rows are immutable.
        /// When false, only the Id field is used as primary key and rows can be updated.
        /// Default is true for backward compatibility.
        /// Entity must implement IVersionedEntity&lt;TKey&gt; interface to support versioning.
        /// </summary>
        public bool SoftDeleteEnabled { get; set; } = false;

        /// <summary>
        /// A flag indicating whether this table should be synchronized with a list-entity mapping.
        /// This is only enabled when list key is used to read/write entities.
        /// The table EntityListMapping will be used to synchronize entities with a list.
        /// Entity does not need to implement any extra interfaces to support this.
        /// </summary>
        public bool SyncWithList { get; set; } = false;

        /// <summary>
        /// Gets or sets the expiry span for entities in this table.
        /// When set, entities must have CreatedTime and AbsoluteExpiration properties.
        /// AbsoluteExpiration will be automatically set to CreatedTime + ExpirySpan.
        /// Default is null (no expiration).
        /// </summary>
        public TimeSpan? ExpirySpan { get; set; } = null;

        /// <summary>
        /// Gets or sets the expiry span as a string in the format "d.hh:mm:ss".
        /// This is a convenience property for setting ExpirySpan in attribute declarations.
        /// Example: "7.00:00:00" for 7 days, "1.00:00:00" for 1 day, "0.01:00:00" for 1 hour.
        /// Entity must implement IExpirableEntity&lt;TKey&gt; interface to support expiration.
        /// </summary>
        public string ExpirySpanString
        {
            get => this.ExpirySpan?.ToString();
            set => this.ExpirySpan = string.IsNullOrEmpty(value) ? null : TimeSpan.Parse(value);
        }

        /// <summary>
        /// Gets or sets whether expired entities should be archived after expiration.
        /// This property is only relevant when ExpirySpan is set.
        /// When true, expired entities are moved to an archive table instead of being deleted.
        /// Default is false.
        /// Entity must implement IArchivableEntity&lt;TKey&gt; interface to support archiving.
        /// </summary>
        public bool EnableArchive { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether the audit trail feature is enabled.
        /// Entity does not need to implement any extra interfaces to support audit trail.
        /// When true, all changes to the entity will be logged in Audit table.
        /// </summary>
        public bool EnableAuditTrail { get; set; } = false;

        public TableAttribute(string name)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }
}