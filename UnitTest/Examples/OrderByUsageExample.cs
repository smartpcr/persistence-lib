//-------------------------------------------------------------------------------
// <copyright file="OrderByUsageExample.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Examples
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

    /// <summary>
    /// Example showing how to use the orderBy parameter in QueryAsync
    /// </summary>
    public class OrderByUsageExample
    {
        private readonly IPersistenceProvider<TestEntity, string> provider;

        public OrderByUsageExample(IPersistenceProvider<TestEntity, string> provider)
        {
            this.provider = provider;
        }

        public async Task DemonstrateOrderByUsage()
        {
            // Prepare test data
            var entities = new[]
            {
                new TestEntity { Id = "q-1", Name = "Charlie", Value = 30 },
                new TestEntity { Id = "q-2", Name = "Alice", Value = 10 },
                new TestEntity { Id = "q-3", Name = "Bob", Value = 20 },
                new TestEntity { Id = "q-4", Name = "David", Value = 25 }
            };

            await this.provider.CreateAsync(entities, new CallerInfo());

            // Example 1: Order by Name ascending
            var orderedByName = await this.provider.QueryAsync(
                null,
                q => q.OrderBy(e => e.Name),
                new CallerInfo());
            // Result: Alice, Bob, Charlie, David

            // Example 2: Order by Value descending
            var orderedByValueDesc = await this.provider.QueryAsync(
                null,
                q => q.OrderByDescending(e => e.Value),
                new CallerInfo());
            // Result: Charlie (30), David (25), Bob (20), Alice (10)

            // Example 3: Order by Name then by Value
            var orderedByNameThenValue = await this.provider.QueryAsync(
                e => e.Value > 15,
                q => q.OrderBy(e => e.Name).ThenBy(e => e.Value),
                new CallerInfo());
            // Result: Bob (20), Charlie (30), David (25) - filtered and ordered

            // Example 4: Complex ordering with multiple ThenBy
            var complexOrdering = await this.provider.QueryAsync(
                null,
                q => q.OrderBy(e => e.IsActive)
                              .ThenByDescending(e => e.Value)
                              .ThenBy(e => e.Name),
                new CallerInfo());

            // Example 5: No ordering (pass null)
            var unordered = await this.provider.QueryAsync(
                null,
                null,
                new CallerInfo());
            // Result: Default database order

            // Example 6: With paged query
            var pagedResult = await this.provider.QueryPagedAsync(
                null,
                10,
                1,
                q => q.OrderBy(e => e.Name));
        }

        public class TestEntity : BaseEntity<string>
        {
            public string Name { get; set; }
            public int Value { get; set; }
            public bool IsActive { get; set; }
            public DateTime CreatedDate { get; set; }
            public string CreatedBy { get; set; }
            public DateTime ModifiedDate { get; set; }
            public string ModifiedBy { get; set; }
            public DateTime? DeletedDate { get; set; }
            public string DeletedBy { get; set; }
        }
    }
}