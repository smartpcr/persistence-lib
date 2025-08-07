// -----------------------------------------------------------------------
// <copyright file="ConcurrencyConflictException.cs" company="Microsoft Corp.">
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

    public class ConcurrencyConflictException : InvalidOperationException
    {
        public string EntityKey { get; }
        public long CurrentVersion { get; }
        public long ExpectedVersion { get; }

        public ConcurrencyConflictException(string entityKey, long currentVersion, long expectedVersion, string message)
            : base(message)
        {
            this.EntityKey = entityKey;
            this.CurrentVersion = currentVersion;
            this.ExpectedVersion = expectedVersion;
        }
    }
}
