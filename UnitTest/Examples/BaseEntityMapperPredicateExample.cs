// -----------------------------------------------------------------------
// <copyright file="BaseEntityMapperPredicateExample.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Examples
{
    using System;
    using System.Linq.Expressions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities.CorePersistence;

    /// <summary>
    /// Example demonstrating the use of BaseEntityMapper with predicate expressions.
    /// </summary>
    public class BaseEntityMapperPredicateExample
    {
        public void Example1_SimplePredicateQuery()
        {
            // Create a mapper for your entity
            var mapper = new BaseEntityMapper<CrudTestEntity, Guid>();

            // Define a predicate expression
            Expression<Func<CrudTestEntity, bool>> predicate = 
                entity => entity.Name == "John Doe";

            // Generate SQL with the predicate
            var (sql, parameters) = mapper.GenerateSelectSql(predicate);

            // The generated SQL will include a WHERE clause based on the predicate
            // Example output:
            // sql: "SELECT Id, Name, Status, Version, CreatedTime, LastWriteTime FROM TestEntity WHERE (Name = @p0)"
            // parameters: { "@p0": "John Doe" }

            Console.WriteLine($"SQL: {sql}");
            foreach (var param in parameters)
            {
                Console.WriteLine($"Parameter {param.Key} = {param.Value}");
            }
        }

        public void Example2_CompoundPredicateQuery()
        {
            var mapper = new BaseEntityMapper<CrudTestEntity, Guid>();

            // Compound predicate with AND condition
            Expression<Func<CrudTestEntity, bool>> predicate = 
                entity => entity.Name == "John Doe" && entity.Status == "Active";

            var (sql, parameters) = mapper.GenerateSelectSql(predicate);

            // Generated SQL will have multiple conditions
            // Example output:
            // sql: "SELECT ... WHERE ((Name = @p0) AND (Status = @p1))"
            // parameters: { "@p0": "John Doe", "@p1": "Active" }
        }

        public void Example3_PredicateWithOptions()
        {
            var mapper = new BaseEntityMapper<CrudTestEntity, Guid>();

            // Predicate for filtering
            Expression<Func<CrudTestEntity, bool>> predicate = 
                entity => entity.Status == "Active";

            // Additional options for ordering and pagination
            var options = new SelectOptions
            {
                OrderBy = "CreatedTime DESC",
                Limit = 10,
                Offset = 20,
                IncludeDeleted = false,
                IncludeExpired = false
            };

            var (sql, parameters) = mapper.GenerateSelectSql(predicate, options);

            // Generated SQL will include WHERE, ORDER BY, LIMIT, and OFFSET
            // Example output:
            // sql: "SELECT ... WHERE (Status = @p0) ORDER BY CreatedTime DESC LIMIT 10 OFFSET 20"
            // parameters: { "@p0": "Active" }
        }

        public void Example4_StringOperationsPredicate()
        {
            var mapper = new BaseEntityMapper<CrudTestEntity, Guid>();

            // String operations: Contains, StartsWith, EndsWith
            Expression<Func<CrudTestEntity, bool>> containsPredicate = 
                entity => entity.Name.Contains("Smith");

            var (sql1, params1) = mapper.GenerateSelectSql(containsPredicate);
            // Generates: WHERE (Name LIKE @p0) with @p0 = "%Smith%"

            Expression<Func<CrudTestEntity, bool>> startsWithPredicate = 
                entity => entity.Name.StartsWith("John");

            var (sql2, params2) = mapper.GenerateSelectSql(startsWithPredicate);
            // Generates: WHERE (Name LIKE @p0) with @p0 = "John%"

            Expression<Func<CrudTestEntity, bool>> endsWithPredicate = 
                entity => entity.Name.EndsWith("Doe");

            var (sql3, params3) = mapper.GenerateSelectSql(endsWithPredicate);
            // Generates: WHERE (Name LIKE @p0) with @p0 = "%Doe"
        }

        public void Example5_DateTimeComparison()
        {
            var mapper = new BaseEntityMapper<CrudTestEntity, Guid>();

            var cutoffDate = DateTime.UtcNow.AddDays(-30);

            // Date comparison predicate
            Expression<Func<CrudTestEntity, bool>> predicate = 
                entity => entity.CreatedTime > cutoffDate;

            var (sql, parameters) = mapper.GenerateSelectSql(predicate);

            // Generated SQL with date comparison
            // Example output:
            // sql: "SELECT ... WHERE (CreatedTime > @p0)"
            // parameters: { "@p0": cutoffDate }
        }

        public void Example6_ComplexNestedPredicate()
        {
            var mapper = new BaseEntityMapper<CrudTestEntity, Guid>();

            // Complex nested conditions with OR and AND
            Expression<Func<CrudTestEntity, bool>> predicate = 
                entity => (entity.Name == "John" || entity.Name == "Jane") && 
                          entity.Status == "Active" &&
                          entity.CreatedTime > DateTime.UtcNow.AddDays(-7);

            var (sql, parameters) = mapper.GenerateSelectSql(predicate);

            // Generated SQL with nested conditions
            // Example output:
            // sql: "SELECT ... WHERE (((Name = @p0) OR (Name = @p1)) AND ((Status = @p2) AND (CreatedTime > @p3)))"
            // parameters: { "@p0": "John", "@p1": "Jane", "@p2": "Active", "@p3": [DateTime] }
        }

        public void Example7_NullPredicate()
        {
            var mapper = new BaseEntityMapper<CrudTestEntity, Guid>();

            // Null predicate returns all records (with default filters)
            var (sql, parameters) = mapper.GenerateSelectSql(null);

            // Generated SQL without WHERE clause (or with only default filters)
            // Example output:
            // sql: "SELECT Id, Name, Status, Version, CreatedTime, LastWriteTime FROM TestEntity"
            // parameters: { } (empty)
        }

        /// <summary>
        /// Example showing how to use the generated SQL with a database command.
        /// </summary>
        public void Example8_UsingWithDatabaseCommand()
        {
            var mapper = new BaseEntityMapper<CrudTestEntity, Guid>();

            Expression<Func<CrudTestEntity, bool>> predicate = 
                entity => entity.Status == "Active" && entity.Name.StartsWith("Test");

            var (sql, parameters) = mapper.GenerateSelectSql(predicate);

            // Use with SQLite (example)
            using (var connection = new System.Data.SQLite.SQLiteConnection("Data Source=test.db"))
            {
                connection.Open();
                using (var command = new System.Data.SQLite.SQLiteCommand(sql, connection))
                {
                    // Add all parameters to the command
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }

                    // Execute the query
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Process results
                        }
                    }
                }
            }
        }
    }
}