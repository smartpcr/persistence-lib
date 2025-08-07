using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using SqlParser;

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Parser.Tests
{
    [TestClass]
    public class LexerTests
    {
        [TestMethod]
        public void TestBasicTokenization()
        {
            // Arrange
            var input = "SELECT * FROM users";
            var lexer = new Lexer(input);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            Assert.AreEqual(5, tokens.Count); // SELECT, *, FROM, users, EOF
            Assert.AreEqual(TokenType.SELECT, tokens[0].Type);
            Assert.AreEqual(TokenType.STAR, tokens[1].Type);
            Assert.AreEqual(TokenType.FROM, tokens[2].Type);
            Assert.AreEqual(TokenType.IDENTIFIER, tokens[3].Type);
            Assert.AreEqual("users", tokens[3].Value);
            Assert.AreEqual(TokenType.EOF, tokens[4].Type);
        }

        [TestMethod]
        public void TestStringLiterals()
        {
            // Arrange
            var input = "WHERE name = 'John Doe'";
            var lexer = new Lexer(input);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.STRING_LITERAL);
            Assert.IsNotNull(stringToken);
            Assert.AreEqual("John Doe", stringToken.Value);
        }

        [TestMethod]
        public void TestNumberLiterals()
        {
            // Arrange
            var input = "WHERE age > 25.5";
            var lexer = new Lexer(input);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            var numberToken = tokens.FirstOrDefault(t => t.Type == TokenType.NUMBER_LITERAL);
            Assert.IsNotNull(numberToken);
            Assert.AreEqual("25.5", numberToken.Value);
        }

        [TestMethod]
        public void TestOperators()
        {
            // Arrange
            var input = "a = b AND c <> d OR e >= f";
            var lexer = new Lexer(input);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            Assert.IsTrue(tokens.Any(t => t.Type == TokenType.EQUALS));
            Assert.IsTrue(tokens.Any(t => t.Type == TokenType.AND));
            Assert.IsTrue(tokens.Any(t => t.Type == TokenType.NOT_EQUALS));
            Assert.IsTrue(tokens.Any(t => t.Type == TokenType.OR));
            Assert.IsTrue(tokens.Any(t => t.Type == TokenType.GREATER_THAN_EQUALS));
        }

        [TestMethod]
        public void TestBracketedIdentifiers()
        {
            // Arrange
            var input = "SELECT [First Name] FROM [User Table]";
            var lexer = new Lexer(input);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            var identifiers = tokens.Where(t => t.Type == TokenType.IDENTIFIER).ToList();
            Assert.AreEqual(2, identifiers.Count);
            Assert.AreEqual("First Name", identifiers[0].Value);
            Assert.AreEqual("User Table", identifiers[1].Value);
        }

        [TestMethod]
        public void TestCaseInsensitiveKeywords()
        {
            // Arrange
            var input = "select * FROM Users WHERE active = 1";
            var lexer = new Lexer(input);

            // Act
            var tokens = lexer.Tokenize();

            // Assert
            Assert.AreEqual(TokenType.SELECT, tokens[0].Type);
            Assert.AreEqual(TokenType.FROM, tokens[2].Type);
            Assert.AreEqual(TokenType.WHERE, tokens[4].Type);
        }
    }

    [TestClass]
    public class ParserTests
    {
        private SelectStatement ParseQuery(string sql)
        {
            var lexer = new Lexer(sql);
            var tokens = lexer.Tokenize();
            var parser = new SqlParser.SqlParser(tokens);
            return parser.Parse();
        }

        [TestMethod]
        public void TestSimpleSelect()
        {
            // Arrange & Act
            var ast = ParseQuery("SELECT * FROM users");

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
            var ast = ParseQuery("SELECT id, name, email FROM users");

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
            var ast = ParseQuery("SELECT id AS user_id, name user_name FROM users");

            // Assert
            Assert.AreEqual(2, ast.SelectList.Count);
            Assert.AreEqual("user_id", ast.SelectList[0].Alias);
            Assert.AreEqual("user_name", ast.SelectList[1].Alias);
        }

        [TestMethod]
        public void TestSelectDistinct()
        {
            // Arrange & Act
            var ast = ParseQuery("SELECT DISTINCT name FROM users");

            // Assert
            Assert.IsTrue(ast.IsDistinct);
        }

        [TestMethod]
        public void TestTableAlias()
        {
            // Arrange & Act
            var ast = ParseQuery("SELECT u.id FROM users u");

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
            var ast = ParseQuery("SELECT * FROM users WHERE age > 18");

            // Assert
            Assert.IsNotNull(ast.Where);
            Assert.IsInstanceOfType(ast.Where, typeof(BinaryExpression));
            var where = (BinaryExpression)ast.Where;
            Assert.AreEqual(TokenType.GREATER_THAN, where.Operator);
            Assert.AreEqual("age", ((ColumnExpression)where.Left).ColumnName);
            Assert.AreEqual(18.0, ((LiteralExpression)where.Right).Value);
        }

        [TestMethod]
        public void TestWhereWithAndOr()
        {
            // Arrange & Act
            var ast = ParseQuery("SELECT * FROM users WHERE age > 18 AND active = 1 OR admin = 1");

            // Assert
            Assert.IsNotNull(ast.Where);
            Assert.IsInstanceOfType(ast.Where, typeof(BinaryExpression));
            var root = (BinaryExpression)ast.Where;
            Assert.AreEqual(TokenType.OR, root.Operator);
        }

        [TestMethod]
        public void TestInnerJoin()
        {
            // Arrange & Act
            var ast = ParseQuery(@"
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
            var ast = ParseQuery(@"
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
            var ast = ParseQuery(@"
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
            var ast = ParseQuery("SELECT * FROM users ORDER BY name ASC, age DESC");

            // Assert
            Assert.AreEqual(2, ast.OrderBy.Count);
            Assert.IsTrue(ast.OrderBy[0].IsAscending);
            Assert.IsFalse(ast.OrderBy[1].IsAscending);
        }

        [TestMethod]
        public void TestLimitOffset()
        {
            // Arrange & Act
            var ast = ParseQuery("SELECT * FROM users LIMIT 10 OFFSET 20");

            // Assert
            Assert.AreEqual(10, ast.Limit);
            Assert.AreEqual(20, ast.Offset);
        }

        [TestMethod]
        public void TestSimpleCTE()
        {
            // Arrange & Act
            var ast = ParseQuery(@"
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
            var ast = ParseQuery(@"
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
            var ast = ParseQuery(@"
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
            var ast = ParseQuery(@"
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
            var ast = ParseQuery("SELECT COUNT(*), MAX(age), CONCAT(first_name, last_name) FROM users");

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
            var ast = ParseQuery(@"
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
            var ast = ParseQuery(@"
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
            ParseQuery("SELECT * WHERE id = 1");
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void TestInvalidSyntax_MissingParenthesis()
        {
            // This should throw an exception
            ParseQuery("SELECT * FROM users WHERE id IN (1, 2, 3");
        }

        [TestMethod]
        public void TestSchemaQualifiedTable()
        {
            // Arrange & Act
            var ast = ParseQuery("SELECT * FROM dbo.users");

            // Assert
            Assert.AreEqual("dbo", ast.From.Table.Schema);
            Assert.AreEqual("users", ast.From.Table.TableName);
        }

        [TestMethod]
        public void TestLikeOperator()
        {
            // Arrange & Act
            var ast = ParseQuery("SELECT * FROM users WHERE name LIKE 'John%'");

            // Assert
            var where = ast.Where as BinaryExpression;
            Assert.IsNotNull(where);
            Assert.AreEqual(TokenType.LIKE, where.Operator);
        }

        [TestMethod]
        public void TestNotOperator()
        {
            // Arrange & Act
            var ast = ParseQuery("SELECT * FROM users WHERE NOT active = 1");

            // Assert
            var where = ast.Where as UnaryExpression;
            Assert.IsNotNull(where);
            Assert.AreEqual(TokenType.NOT, where.Operator);
        }

        [TestMethod]
        public void TestNullLiteral()
        {
            // Arrange & Act
            var ast = ParseQuery("SELECT * FROM users WHERE deleted_at IS NULL");

            // Assert
            Assert.IsNotNull(ast.Where);
            // Note: IS NULL would need additional implementation in the parser
            // This test demonstrates the NULL literal parsing
        }

        [TestMethod]
        public void TestSimpleArithmetic()
        {
            // Arrange & Act
            var ast = ParseQuery("SELECT price * 2 FROM orders");

            // Assert
            Assert.IsNotNull(ast);
            Assert.AreEqual(1, ast.SelectList.Count);
            
            var expr = ast.SelectList[0].Expression;
            Assert.IsInstanceOfType(expr, typeof(BinaryExpression), 
                "Expression should be BinaryExpression for multiplication");
            
            var binaryExpr = (BinaryExpression)expr;
            Assert.AreEqual(TokenType.STAR, binaryExpr.Operator);
            Assert.AreEqual("price", ((ColumnExpression)binaryExpr.Left).ColumnName);
            Assert.AreEqual(2.0, ((LiteralExpression)binaryExpr.Right).Value);
        }

        [TestMethod]
        public void TestArithmeticExpressions()
        {
            // Arrange & Act
            var ast = ParseQuery("SELECT price * quantity + tax - discount / 2 FROM orders");

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
            Assert.AreEqual(TokenType.MINUS, rootExpr.Operator);
            
            // Left side should be addition: (price * quantity) + tax
            var leftExpr = rootExpr.Left as BinaryExpression;
            Assert.IsNotNull(leftExpr);
            Assert.AreEqual(TokenType.PLUS, leftExpr.Operator);
            
            // Left side of addition should be multiplication: price * quantity
            var multiplyExpr = leftExpr.Left as BinaryExpression;
            Assert.IsNotNull(multiplyExpr);
            Assert.AreEqual(TokenType.STAR, multiplyExpr.Operator);
            Assert.AreEqual("price", ((ColumnExpression)multiplyExpr.Left).ColumnName);
            Assert.AreEqual("quantity", ((ColumnExpression)multiplyExpr.Right).ColumnName);
            
            // Right side of subtraction should be division: discount / 2
            var divideExpr = rootExpr.Right as BinaryExpression;
            Assert.IsNotNull(divideExpr);
            Assert.AreEqual(TokenType.DIVIDE, divideExpr.Operator);
            Assert.AreEqual("discount", ((ColumnExpression)divideExpr.Left).ColumnName);
            Assert.AreEqual(2.0, ((LiteralExpression)divideExpr.Right).Value);
        }

        [TestMethod]
        public void TestArithmeticExpressionsWithAlias()
        {
            // Arrange & Act
            var ast = ParseQuery("SELECT price * quantity AS total_price, tax + shipping fee FROM orders");

            // Assert
            Assert.AreEqual(2, ast.SelectList.Count);
            
            // First expression with explicit alias
            var expr1 = ast.SelectList[0].Expression as BinaryExpression;
            Assert.IsNotNull(expr1);
            Assert.AreEqual(TokenType.STAR, expr1.Operator);
            Assert.AreEqual("total_price", ast.SelectList[0].Alias);
            
            // Second expression with implicit alias
            var expr2 = ast.SelectList[1].Expression as BinaryExpression;
            Assert.IsNotNull(expr2);
            Assert.AreEqual(TokenType.PLUS, expr2.Operator);
            Assert.AreEqual("fee", ast.SelectList[1].Alias);
        }

        [TestMethod]
        public void TestComplexArithmeticInSelect()
        {
            // Arrange & Act
            var ast = ParseQuery(@"
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
            Assert.AreEqual(TokenType.STAR, expr1.Operator);
            Assert.AreEqual("total_with_tax", ast.SelectList[0].Alias);
            
            // Verify the grouped expression
            var grouped = expr1.Left as BinaryExpression;
            Assert.IsNotNull(grouped);
            Assert.AreEqual(TokenType.PLUS, grouped.Operator);
            
            // Second: price * 0.9
            var expr2 = ast.SelectList[1].Expression as BinaryExpression;
            Assert.IsNotNull(expr2);
            Assert.AreEqual(TokenType.STAR, expr2.Operator);
            Assert.AreEqual(0.9, ((LiteralExpression)expr2.Right).Value);
            
            // Third: CASE expression
            Assert.IsInstanceOfType(ast.SelectList[2].Expression, typeof(CaseExpression));
        }
    }

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
            var parser = new SqlParser.SqlParser(tokens);

            // Act & Assert - Should not throw
            var ast = parser.Parse();
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
            var parser = new SqlParser.SqlParser(tokens);

            // Act
            var ast = parser.Parse();

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