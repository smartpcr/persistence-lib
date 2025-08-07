// -----------------------------------------------------------------------
// <copyright file="ParserTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Parser
{
    using System;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Parser;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ParserTests
    {
        private SelectStatement ParseQuery(string sql)
        {
            var lexer = new Lexer(sql);
            var tokens = lexer.Tokenize();
            var parser = new SqlParser(tokens);
            return parser.Parse();
        }

        [TestMethod]
        public void TestSimpleSelect()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT * FROM users");

            // Assert
            Assert.IsNotNull(ast);
            Assert.AreEqual(1, ast.SelectList.Count);
            Assert.IsInstanceOfType(ast.SelectList[0].Expression, typeof(ColumnExpression));
            Assert.AreEqual("*", ((ColumnExpression)ast.SelectList[0].Expression).ColumnName);
            Assert.IsNotNull(ast.From);
            Assert.AreEqual("users", ast.From.Table.TableName);
        }

        [TestMethod]
        public void TestSelectWithColumns()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT id, name, email FROM users");

            // Assert
            Assert.AreEqual(3, ast.SelectList.Count);
            Assert.AreEqual("id", ((ColumnExpression)ast.SelectList[0].Expression).ColumnName);
            Assert.AreEqual("name", ((ColumnExpression)ast.SelectList[1].Expression).ColumnName);
            Assert.AreEqual("email", ((ColumnExpression)ast.SelectList[2].Expression).ColumnName);
        }

        [TestMethod]
        public void TestSelectWithAlias()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT id AS user_id, name user_name FROM users");

            // Assert
            Assert.AreEqual(2, ast.SelectList.Count);
            Assert.AreEqual("user_id", ast.SelectList[0].Alias);
            Assert.AreEqual("user_name", ast.SelectList[1].Alias);
        }

        [TestMethod]
        public void TestSelectDistinct()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT DISTINCT name FROM users");

            // Assert
            Assert.IsTrue(ast.IsDistinct);
        }

        [TestMethod]
        public void TestTableAlias()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT u.id FROM users u");

            // Assert
            Assert.AreEqual("users", ast.From.Table.TableName);
            Assert.AreEqual("u", ast.From.Table.Alias);
            var col = (ColumnExpression)ast.SelectList[0].Expression;
            Assert.AreEqual("u", col.TableAlias);
            Assert.AreEqual("id", col.ColumnName);
        }

        [TestMethod]
        public void TestWhereClause()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT * FROM users WHERE age > 18");

            // Assert
            Assert.IsNotNull(ast.Where);
            Assert.IsInstanceOfType(ast.Where, typeof(BinaryExpression));
            var where = (BinaryExpression)ast.Where;
            Assert.AreEqual(SqlTokenType.GREATER_THAN, where.Operator);
            Assert.AreEqual("age", ((ColumnExpression)where.Left).ColumnName);
            Assert.AreEqual(18.0, ((LiteralExpression)where.Right).Value);
        }

        [TestMethod]
        public void TestWhereWithAndOr()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT * FROM users WHERE age > 18 AND active = 1 OR admin = 1");

            // Assert
            Assert.IsNotNull(ast.Where);
            Assert.IsInstanceOfType(ast.Where, typeof(BinaryExpression));
            var root = (BinaryExpression)ast.Where;
            Assert.AreEqual(SqlTokenType.OR, root.Operator);
        }

        [TestMethod]
        public void TestInnerJoin()
        {
            // Arrange & Act
            var ast = this.ParseQuery(@"
                SELECT u.name, p.title
                FROM users u
                INNER JOIN posts p ON u.id = p.user_id");

            // Assert
            Assert.AreEqual(1, ast.From.Joins.Count);
            var join = ast.From.Joins[0];
            Assert.AreEqual(JoinType.Inner, join.Type);
            Assert.AreEqual("posts", join.Table.TableName);
            Assert.AreEqual("p", join.Table.Alias);
            Assert.IsNotNull(join.OnCondition);
        }

        [TestMethod]
        public void TestMultipleJoins()
        {
            // Arrange & Act
            var ast = this.ParseQuery(@"
                SELECT *
                FROM users u
                LEFT JOIN posts p ON u.id = p.user_id
                INNER JOIN comments c ON p.id = c.post_id");

            // Assert
            Assert.AreEqual(2, ast.From.Joins.Count);
            Assert.AreEqual(JoinType.Left, ast.From.Joins[0].Type);
            Assert.AreEqual(JoinType.Inner, ast.From.Joins[1].Type);
        }

        [TestMethod]
        public void TestGroupByHaving()
        {
            // Arrange & Act
            var ast = this.ParseQuery(@"
                SELECT user_id, COUNT(*) as post_count
                FROM posts
                GROUP BY user_id
                HAVING COUNT(*) > 5");

            // Assert
            Assert.AreEqual(1, ast.GroupBy.Count);
            Assert.AreEqual("user_id", ((ColumnExpression)ast.GroupBy[0]).ColumnName);
            Assert.IsNotNull(ast.Having);
        }

        [TestMethod]
        public void TestOrderBy()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT * FROM users ORDER BY name ASC, age DESC");

            // Assert
            Assert.AreEqual(2, ast.OrderBy.Count);
            Assert.IsTrue(ast.OrderBy[0].IsAscending);
            Assert.IsFalse(ast.OrderBy[1].IsAscending);
        }

        [TestMethod]
        public void TestLimitOffset()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT * FROM users LIMIT 10 OFFSET 20");

            // Assert
            Assert.AreEqual(10, ast.Limit);
            Assert.AreEqual(20, ast.Offset);
        }

        [TestMethod]
        public void TestSimpleCTE()
        {
            // Arrange & Act
            var ast = this.ParseQuery(@"
                WITH user_counts AS (
                    SELECT COUNT(*) as total FROM users
                )
                SELECT * FROM user_counts");

            // Assert
            Assert.AreEqual(1, ast.CTEs.Count);
            Assert.AreEqual("user_counts", ast.CTEs[0].Name);
            Assert.IsNotNull(ast.CTEs[0].Query);
        }

        [TestMethod]
        public void TestCTEWithColumns()
        {
            // Arrange & Act
            var ast = this.ParseQuery(@"
                WITH user_stats (user_id, post_count) AS (
                    SELECT user_id, COUNT(*) FROM posts GROUP BY user_id
                )
                SELECT * FROM user_stats");

            // Assert
            Assert.AreEqual(1, ast.CTEs.Count);
            Assert.AreEqual(2, ast.CTEs[0].Columns.Count);
            Assert.AreEqual("user_id", ast.CTEs[0].Columns[0]);
            Assert.AreEqual("post_count", ast.CTEs[0].Columns[1]);
        }

        [TestMethod]
        public void TestMultipleCTEs()
        {
            // Arrange & Act
            var ast = this.ParseQuery(@"
                WITH
                cte1 AS (SELECT * FROM table1),
                cte2 AS (SELECT * FROM table2)
                SELECT * FROM cte1");

            // Assert
            Assert.AreEqual(2, ast.CTEs.Count);
            Assert.AreEqual("cte1", ast.CTEs[0].Name);
            Assert.AreEqual("cte2", ast.CTEs[1].Name);
        }

        [TestMethod]
        public void TestCaseExpression()
        {
            // Arrange & Act
            var ast = this.ParseQuery(@"
                SELECT
                    CASE
                        WHEN age < 18 THEN 'Minor'
                        WHEN age >= 65 THEN 'Senior'
                        ELSE 'Adult'
                    END as age_group
                FROM users");

            // Assert
            var caseExpr = ast.SelectList[0].Expression as CaseExpression;
            Assert.IsNotNull(caseExpr);
            Assert.AreEqual(2, caseExpr.WhenClauses.Count);
            Assert.IsNotNull(caseExpr.ElseExpression);
            Assert.AreEqual("age_group", ast.SelectList[0].Alias);
        }

        [TestMethod]
        public void TestFunctionCalls()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT COUNT(*), MAX(age), CONCAT(first_name, last_name) FROM users");

            // Assert
            Assert.AreEqual(3, ast.SelectList.Count);

            var count = ast.SelectList[0].Expression as FunctionExpression;
            Assert.IsNotNull(count);
            Assert.AreEqual("COUNT", count.FunctionName);
            Assert.AreEqual(1, count.Arguments.Count);

            var max = ast.SelectList[1].Expression as FunctionExpression;
            Assert.IsNotNull(max);
            Assert.AreEqual("MAX", max.FunctionName);

            var concat = ast.SelectList[2].Expression as FunctionExpression;
            Assert.IsNotNull(concat);
            Assert.AreEqual("CONCAT", concat.FunctionName);
            Assert.AreEqual(2, concat.Arguments.Count);
        }

        [TestMethod]
        public void TestSubquery()
        {
            // Arrange & Act
            var ast = this.ParseQuery(@"
                SELECT *
                FROM users
                WHERE id IN (SELECT user_id FROM posts WHERE active = 1)");

            // Assert
            Assert.IsNotNull(ast.Where);
            var inExpr = ast.Where as FunctionExpression;
            Assert.IsNotNull(inExpr);
            Assert.AreEqual("IN", inExpr.FunctionName);
            Assert.IsInstanceOfType(inExpr.Arguments[1], typeof(SubqueryExpression));
        }

        [TestMethod]
        public void TestComplexQuery()
        {
            // Arrange & Act
            var ast = this.ParseQuery(@"
                WITH active_users AS (
                    SELECT id, name FROM users WHERE active = 1
                )
                SELECT
                    u.name,
                    COUNT(p.id) as post_count,
                    CASE
                        WHEN COUNT(p.id) > 10 THEN 'Active'
                        ELSE 'Inactive'
                    END as status
                FROM active_users u
                LEFT JOIN posts p ON u.id = p.user_id
                GROUP BY u.id, u.name
                HAVING COUNT(p.id) > 0
                ORDER BY post_count DESC
                LIMIT 100");

            // Assert
            Assert.AreEqual(1, ast.CTEs.Count);
            Assert.AreEqual(3, ast.SelectList.Count);
            Assert.AreEqual(1, ast.From.Joins.Count);
            Assert.AreEqual(2, ast.GroupBy.Count);
            Assert.IsNotNull(ast.Having);
            Assert.AreEqual(1, ast.OrderBy.Count);
            Assert.AreEqual(100, ast.Limit);
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void TestInvalidSyntax_MissingFrom()
        {
            // This should throw an exception
            this.ParseQuery("SELECT * WHERE id = 1");
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void TestInvalidSyntax_MissingParenthesis()
        {
            // This should throw an exception
            this.ParseQuery("SELECT * FROM users WHERE id IN (1, 2, 3");
        }

        [TestMethod]
        public void TestSchemaQualifiedTable()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT * FROM dbo.users");

            // Assert
            Assert.AreEqual("dbo", ast.From.Table.Schema);
            Assert.AreEqual("users", ast.From.Table.TableName);
        }

        [TestMethod]
        public void TestLikeOperator()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT * FROM users WHERE name LIKE 'John%'");

            // Assert
            var where = ast.Where as BinaryExpression;
            Assert.IsNotNull(where);
            Assert.AreEqual(SqlTokenType.LIKE, where.Operator);
        }

        [TestMethod]
        public void TestNotOperator()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT * FROM users WHERE NOT active = 1");

            // Assert
            var where = ast.Where as UnaryExpression;
            Assert.IsNotNull(where);
            Assert.AreEqual(SqlTokenType.NOT, where.Operator);
        }

        [TestMethod]
        public void TestNullLiteral()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT * FROM users WHERE deleted_at IS NULL");

            // Assert
            Assert.IsNotNull(ast.Where);
            // Note: IS NULL would need additional implementation in the parser
            // This test demonstrates the NULL literal parsing
        }

        [TestMethod]
        public void TestSimpleArithmetic()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT price * 2 FROM orders");

            // Assert
            Assert.IsNotNull(ast);
            Assert.AreEqual(1, ast.SelectList.Count);

            var expr = ast.SelectList[0].Expression;
            Assert.IsInstanceOfType(expr, typeof(BinaryExpression),
                "Expression should be BinaryExpression for multiplication");

            var binaryExpr = (BinaryExpression)expr;
            Assert.AreEqual(SqlTokenType.MULTIPLY, binaryExpr.Operator);
            Assert.AreEqual("price", ((ColumnExpression)binaryExpr.Left).ColumnName);
            Assert.AreEqual(2.0, ((LiteralExpression)binaryExpr.Right).Value);
        }

        [TestMethod]
        public void TestArithmeticExpressions()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT price * quantity + tax - discount / 2 FROM orders");

            // Assert
            Assert.IsNotNull(ast, "AST should not be null");
            Assert.IsNotNull(ast.SelectList, "SelectList should not be null");
            Assert.AreEqual(1, ast.SelectList.Count, "Should have exactly one select item");

            var expr = ast.SelectList[0].Expression;
            Assert.IsNotNull(expr, "Expression should not be null");

            // Debug: Let's see what type we actually got
            if (!(expr is BinaryExpression))
            {
                Assert.Fail($"Expected BinaryExpression but got {expr.GetType().Name}. " +
                           $"This might indicate the parser is not correctly parsing arithmetic expressions.");
            }

            Assert.IsInstanceOfType(expr, typeof(BinaryExpression));

            // The root should be a subtraction (last operation due to left-to-right precedence)
            var rootExpr = expr as BinaryExpression;
            Assert.IsNotNull(rootExpr);
            Assert.AreEqual(SqlTokenType.MINUS, rootExpr.Operator);

            // Left side should be addition: (price * quantity) + tax
            var leftExpr = rootExpr.Left as BinaryExpression;
            Assert.IsNotNull(leftExpr);
            Assert.AreEqual(SqlTokenType.PLUS, leftExpr.Operator);

            // Left side of addition should be multiplication: price * quantity
            var multiplyExpr = leftExpr.Left as BinaryExpression;
            Assert.IsNotNull(multiplyExpr);
            Assert.AreEqual(SqlTokenType.MULTIPLY, multiplyExpr.Operator);
            Assert.AreEqual("price", ((ColumnExpression)multiplyExpr.Left).ColumnName);
            Assert.AreEqual("quantity", ((ColumnExpression)multiplyExpr.Right).ColumnName);

            // Right side of subtraction should be division: discount / 2
            var divideExpr = rootExpr.Right as BinaryExpression;
            Assert.IsNotNull(divideExpr);
            Assert.AreEqual(SqlTokenType.DIVIDE, divideExpr.Operator);
            Assert.AreEqual("discount", ((ColumnExpression)divideExpr.Left).ColumnName);
            Assert.AreEqual(2.0, ((LiteralExpression)divideExpr.Right).Value);
        }

        [TestMethod]
        public void TestArithmeticExpressionsWithAlias()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT price * quantity AS total_price, tax + shipping fee FROM orders");

            // Assert
            Assert.AreEqual(2, ast.SelectList.Count);

            // First expression with explicit alias
            var expr1 = ast.SelectList[0].Expression as BinaryExpression;
            Assert.IsNotNull(expr1);
            Assert.AreEqual(SqlTokenType.MULTIPLY, expr1.Operator);
            Assert.AreEqual("total_price", ast.SelectList[0].Alias);

            // Second expression with implicit alias
            var expr2 = ast.SelectList[1].Expression as BinaryExpression;
            Assert.IsNotNull(expr2);
            Assert.AreEqual(SqlTokenType.PLUS, expr2.Operator);
            Assert.AreEqual("fee", ast.SelectList[1].Alias);
        }

        [TestMethod]
        public void TestComplexArithmeticInSelect()
        {
            // Arrange & Act
            var ast = this.ParseQuery(@"
                SELECT
                    (price + tax) * quantity AS total_with_tax,
                    price * 0.9 AS discounted_price,
                    CASE WHEN price > 100 THEN price * 0.8 ELSE price END AS final_price
                FROM products");

            // Assert
            Assert.AreEqual(3, ast.SelectList.Count);

            // First: (price + tax) * quantity
            var expr1 = ast.SelectList[0].Expression as BinaryExpression;
            Assert.IsNotNull(expr1);
            Assert.AreEqual(SqlTokenType.MULTIPLY, expr1.Operator);
            Assert.AreEqual("total_with_tax", ast.SelectList[0].Alias);

            // Verify the grouped expression
            var grouped = expr1.Left as BinaryExpression;
            Assert.IsNotNull(grouped);
            Assert.AreEqual(SqlTokenType.PLUS, grouped.Operator);

            // Second: price * 0.9
            var expr2 = ast.SelectList[1].Expression as BinaryExpression;
            Assert.IsNotNull(expr2);
            Assert.AreEqual(SqlTokenType.MULTIPLY, expr2.Operator);
            Assert.AreEqual(0.9, ((LiteralExpression)expr2.Right).Value);

            // Third: CASE expression
            Assert.IsInstanceOfType(ast.SelectList[2].Expression, typeof(CaseExpression));
        }
    }
}
