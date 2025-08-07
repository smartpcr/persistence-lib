// -----------------------------------------------------------------------
// <copyright file="ParserIntegrationTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Parser
{
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Parser;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class IntegrationTests
    {
        [TestMethod]
        [DataRow("SELECT * FROM users")]
        [DataRow("SELECT id, name FROM users WHERE active = 1")]
        [DataRow("SELECT u.*, p.title FROM users u JOIN posts p ON u.id = p.user_id")]
        [DataRow("WITH cte AS (SELECT * FROM users) SELECT * FROM cte")]
        [DataRow("SELECT COUNT(*) FROM users GROUP BY department HAVING COUNT(*) > 5")]
        public void TestValidQueries(string sql)
        {
            // Arrange
            var lexer = new Lexer(sql);
            var tokens = lexer.Tokenize();
            var parser = new SqlParser(tokens);

            // Act & Assert - Should not throw
            var ast = (SelectStatement)parser.Parse();
            Assert.IsNotNull(ast);
        }

        [TestMethod]
        public void TestRealWorldQuery()
        {
            // Arrange
            var sql = @"
                WITH monthly_sales AS (
                    SELECT
                        DATE_TRUNC('month', order_date) as month,
                        SUM(total_amount) as revenue,
                        COUNT(DISTINCT customer_id) as customers
                    FROM orders
                    WHERE order_date >= '2023-01-01'
                    GROUP BY DATE_TRUNC('month', order_date)
                ),
                customer_segments AS (
                    SELECT
                        customer_id,
                        CASE
                            WHEN total_spent > 1000 THEN 'VIP'
                            WHEN total_spent > 500 THEN 'Regular'
                            ELSE 'Basic'
                        END as segment
                    FROM (
                        SELECT customer_id, SUM(total_amount) as total_spent
                        FROM orders
                        GROUP BY customer_id
                    ) customer_totals
                )
                SELECT
                    ms.month,
                    ms.revenue,
                    ms.customers,
                    cs.segment,
                    COUNT(DISTINCT o.order_id) as orders_count
                FROM monthly_sales ms
                CROSS JOIN customer_segments cs
                LEFT JOIN orders o ON DATE_TRUNC('month', o.order_date) = ms.month
                    AND o.customer_id = cs.customer_id
                GROUP BY ms.month, ms.revenue, ms.customers, cs.segment
                ORDER BY ms.month DESC, cs.segment
                LIMIT 100";

            var lexer = new Lexer(sql);
            var tokens = lexer.Tokenize();
            var parser = new SqlParser(tokens);

            // Act
            var ast = (SelectStatement)parser.Parse();

            // Assert
            Assert.IsNotNull(ast);
            Assert.AreEqual(2, ast.CTEs.Count);
            Assert.IsTrue(ast.From.Joins.Count > 0);
            Assert.IsTrue(ast.GroupBy.Count > 0);
            Assert.IsTrue(ast.OrderBy.Count > 0);
            Assert.AreEqual(100, ast.Limit);
        }
    }
}
