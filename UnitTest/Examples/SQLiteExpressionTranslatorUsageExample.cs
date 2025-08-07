//-------------------------------------------------------------------------------
// <copyright file="SQLiteExpressionTranslatorUsageExample.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Examples
{
    using System;
    using System.Data.SQLite;
    using System.Linq;
    using System.Linq.Expressions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    /// <summary>
    /// Example showing how to use the SQLiteExpressionTranslator with orderBy
    /// </summary>
    public class SQLiteExpressionTranslatorUsageExample
    {
        public void BuildQueryWithWhereAndOrderBy()
        {
            var translator = new ExpressionTranslator<Product>();
            
            // Example 1: Simple query with WHERE and ORDER BY
            Expression<Func<Product, bool>> whereClause = p => p.Price > 100 && p.Category == "Electronics";
            Func<IQueryable<Product>, IOrderedQueryable<Product>> orderBy = q => q.OrderBy(p => p.Name);
            
            var whereResult = translator.Translate(whereClause);
            var orderBySql = translator.TranslateOrderBy(orderBy);
            
            var fullQuery = $"SELECT * FROM Products WHERE {whereResult.Sql} {orderBySql}";
            // Result: SELECT * FROM Products WHERE ((Price > @p0) AND (Category = @p1)) ORDER BY Name ASC
            
            // Example 2: Complex query with multiple conditions and ordering
            Expression<Func<Product, bool>> complexWhere = p => 
                (p.Price > 50 && p.Price < 500) || 
                p.Category.Contains("Sale");
                
            Func<IQueryable<Product>, IOrderedQueryable<Product>> complexOrderBy = q => 
                q.OrderByDescending(p => p.Price)
                 .ThenBy(p => p.Name);
            
            var complexWhereResult = translator.Translate(complexWhere);
            var complexOrderBySql = translator.TranslateOrderBy(complexOrderBy);
            
            var complexQuery = $"SELECT * FROM Products WHERE {complexWhereResult.Sql} {complexOrderBySql}";
            // Result: SELECT * FROM Products WHERE (((Price > @p0) AND (Price < @p1)) OR (Category LIKE @p2)) ORDER BY Price DESC, Name ASC
            
            // Example 3: Using with SQLiteCommand
            using (var connection = new SQLiteConnection("Data Source=:memory:"))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = fullQuery;
                
                // Add parameters from the WHERE clause
                foreach (var param in whereResult.Parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }
                
                // Execute query
                // using (var reader = command.ExecuteReader()) { ... }
            }
            
            // Example 4: Pagination with ORDER BY
            Func<IQueryable<Product>, IOrderedQueryable<Product>> pageOrderBy = q => 
                q.OrderBy(p => p.Category).ThenByDescending(p => p.Price);
                
            var pageSql = translator.TranslateOrderBy(pageOrderBy);
            var pageQuery = $"SELECT * FROM Products {pageSql} LIMIT 10 OFFSET 20";
            // Result: SELECT * FROM Products ORDER BY Category ASC, Price DESC LIMIT 10 OFFSET 20
        }
        
        private class Product
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public decimal Price { get; set; }
            public string Category { get; set; }
        }
    }
}