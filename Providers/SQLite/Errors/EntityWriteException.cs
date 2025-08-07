// -----------------------------------------------------------------------
// <copyright file="EntityWriteException.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Errors
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class EntityWriteException : InvalidOperationException
    {
        public string EntityKey { get; }

        public EntityWriteException(string entityKey, string message) : base(message)
        {
            this.EntityKey = entityKey;
        }
    }
}
