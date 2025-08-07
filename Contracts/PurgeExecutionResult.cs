// -----------------------------------------------------------------------
// <copyright file="PurgeExecutionResult.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts
{
    public class PurgeExecutionResult
    {
        public long EntitiesPurged { get; set; }
        public long VersionsPurged { get; set; }
        public long SpaceReclaimed { get; set; }
        public PurgeStatistics Statistics { get; set; }
    }
}
