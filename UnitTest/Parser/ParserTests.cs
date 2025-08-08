// -----------------------------------------------------------------------
// <copyright file="ParserTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Parser
{
    using System;
    using FluentAssertions;
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
            return (SelectStatement)parser.Parse();
        }

        [TestMethod]
        public void TestSimpleSelect()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT * FROM users");

            // Assert
            ast.Should().NotBeNull();
            ast.SelectList.Count.Should().Be(1);
            ast.SelectList[0].Expression.Should().BeOfType<ColumnExpression>();
            ((ColumnExpression)ast.SelectList[0].Expression).ColumnName.Should().Be("*");
            ast.From.Should().NotBeNull();
            ast.From.Table.TableName.Should().Be("users");
        }

        [TestMethod]
        public void TestSelectWithColumns()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT id, name, email FROM users");

            // Assert
            ast.SelectList.Count.Should().Be(3);
            ((ColumnExpression)ast.SelectList[0].Expression).ColumnName.Should().Be("id");
            ((ColumnExpression)ast.SelectList[1].Expression).ColumnName.Should().Be("name");
            ((ColumnExpression)ast.SelectList[2].Expression).ColumnName.Should().Be("email");
        }

        [TestMethod]
        public void TestSelectWithAlias()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT id AS user_id, name user_name FROM users");

            // Assert
            ast.SelectList.Count.Should().Be(2);
            ast.SelectList[0].Alias.Should().Be("user_id");
            ast.SelectList[1].Alias.Should().Be("user_name");
        }

        [TestMethod]
        public void TestSelectDistinct()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT DISTINCT name FROM users");

            // Assert
            ast.IsDistinct.Should().BeTrue();
        }

        [TestMethod]
        public void TestTableAlias()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT u.id FROM users u");

            // Assert
            ast.From.Table.TableName.Should().Be("users");
            ast.From.Table.Alias.Should().Be("u");
            var col = (ColumnExpression)ast.SelectList[0].Expression;
            col.TableAlias.Should().Be("u");
            col.ColumnName.Should().Be("id");
        }

        [TestMethod]
        public void TestWhereClause()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT * FROM users WHERE age > 18");

            // Assert
            ast.Where.Should().NotBeNull();
            ast.Where.Should().BeOfType<BinaryExpression>();
            var where = (BinaryExpression)ast.Where;
            where.Operator.Should().Be(SqlTokenType.GREATER_THAN);
            ((ColumnExpression)where.Left).ColumnName.Should().Be("age");
            ((LiteralExpression)where.Right).Value.Should().Be(18.0);
        }

        [TestMethod]
        public void TestWhereWithAndOr()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT * FROM users WHERE age > 18 AND active = 1 OR admin = 1");

            // Assert
            ast.Where.Should().NotBeNull();
            ast.Where.Should().BeOfType<BinaryExpression>();
            var root = (BinaryExpression)ast.Where;
            root.Operator.Should().Be(SqlTokenType.OR);
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
            ast.From.Joins.Count.Should().Be(1);
            var join = ast.From.Joins[0];
            join.Type.Should().Be(JoinType.Inner);
            join.Table.TableName.Should().Be("posts");
            join.Table.Alias.Should().Be("p");
            join.OnCondition.Should().NotBeNull();
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
            ast.From.Joins.Count.Should().Be(2);
            ast.From.Joins[0].Type.Should().Be(JoinType.Left);
            ast.From.Joins[1].Type.Should().Be(JoinType.Inner);
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
            ast.GroupBy.Count.Should().Be(1);
            ((ColumnExpression)ast.GroupBy[0]).ColumnName.Should().Be("user_id");
            ast.Having.Should().NotBeNull();
        }

        [TestMethod]
        public void TestOrderBy()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT * FROM users ORDER BY name ASC, age DESC");

            // Assert
            ast.OrderBy.Count.Should().Be(2);
            ast.OrderBy[0].IsAscending.Should().BeTrue();
            ast.OrderBy[1].IsAscending.Should().BeFalse();
        }

        [TestMethod]
        public void TestLimitOffset()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT * FROM users LIMIT 10 OFFSET 20");

            // Assert
            ast.Limit.Should().Be(10);
            ast.Offset.Should().Be(20);
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
            ast.CTEs.Count.Should().Be(1);
            ast.CTEs[0].Name.Should().Be("user_counts");
            ast.CTEs[0].Query.Should().NotBeNull();
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
            ast.CTEs.Count.Should().Be(1);
            ast.CTEs[0].Columns.Count.Should().Be(2);
            ast.CTEs[0].Columns[0].Should().Be("user_id");
            ast.CTEs[0].Columns[1].Should().Be("post_count");
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
            ast.CTEs.Count.Should().Be(2);
            ast.CTEs[0].Name.Should().Be("cte1");
            ast.CTEs[1].Name.Should().Be("cte2");
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
            caseExpr.Should().NotBeNull();
            caseExpr.WhenClauses.Count.Should().Be(2);
            caseExpr.ElseExpression.Should().NotBeNull();
            ast.SelectList[0].Alias.Should().Be("age_group");
        }

        [TestMethod]
        public void TestFunctionCalls()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT COUNT(*), MAX(age), CONCAT(first_name, last_name) FROM users");

            // Assert
            ast.SelectList.Count.Should().Be(3);

            var count = ast.SelectList[0].Expression as FunctionExpression;
            count.Should().NotBeNull();
            count.FunctionName.Should().Be("COUNT");
            count.Arguments.Count.Should().Be(1);

            var max = ast.SelectList[1].Expression as FunctionExpression;
            max.Should().NotBeNull();
            max.FunctionName.Should().Be("MAX");

            var concat = ast.SelectList[2].Expression as FunctionExpression;
            concat.Should().NotBeNull();
            concat.FunctionName.Should().Be("CONCAT");
            concat.Arguments.Count.Should().Be(2);
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
            ast.Where.Should().NotBeNull();
            var inExpr = ast.Where as FunctionExpression;
            inExpr.Should().NotBeNull();
            inExpr.FunctionName.Should().Be("IN");
            inExpr.Arguments[1].Should().BeOfType<SubqueryExpression>();
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
            ast.CTEs.Count.Should().Be(1);
            ast.SelectList.Count.Should().Be(3);
            ast.From.Joins.Count.Should().Be(1);
            ast.GroupBy.Count.Should().Be(2);
            ast.Having.Should().NotBeNull();
            ast.OrderBy.Count.Should().Be(1);
            ast.Limit.Should().Be(100);
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
            ast.From.Table.Schema.Should().Be("dbo");
            ast.From.Table.TableName.Should().Be("users");
        }

        [TestMethod]
        public void TestLikeOperator()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT * FROM users WHERE name LIKE 'John%'");

            // Assert
            var where = ast.Where as BinaryExpression;
            where.Should().NotBeNull();
            where.Operator.Should().Be(SqlTokenType.LIKE);
        }

        [TestMethod]
        public void TestNotOperator()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT * FROM users WHERE NOT active = 1");

            // Assert
            var where = ast.Where as UnaryExpression;
            where.Should().NotBeNull();
            where.Operator.Should().Be(SqlTokenType.NOT);
        }

        [TestMethod]
        public void TestNullLiteral()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT * FROM users WHERE deleted_at IS NULL");

            // Assert
            ast.Where.Should().NotBeNull();
            // Note: IS NULL would need additional implementation in the parser
            // This test demonstrates the NULL literal parsing
        }

        [TestMethod]
        public void TestSimpleArithmetic()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT price * 2 FROM orders");

            // Assert
            ast.Should().NotBeNull();
            ast.SelectList.Count.Should().Be(1);

            var expr = ast.SelectList[0].Expression;
            expr.Should().BeOfType<BinaryExpression>("Expression should be BinaryExpression for multiplication");

            var binaryExpr = (BinaryExpression)expr;
            binaryExpr.Operator.Should().Be(SqlTokenType.MULTIPLY);
            ((ColumnExpression)binaryExpr.Left).ColumnName.Should().Be("price");
            ((LiteralExpression)binaryExpr.Right).Value.Should().Be(2.0);
        }

        [TestMethod]
        public void TestArithmeticExpressions()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT price * quantity + tax - discount / 2 FROM orders");

            // Assert
            ast.Should().NotBeNull("AST should not be null");
            ast.SelectList.Should().NotBeNull("SelectList should not be null");
            ast.SelectList.Count.Should().Be(1, "Should have exactly one select item");

            var expr = ast.SelectList[0].Expression;
            expr.Should().NotBeNull("Expression should not be null");

            // Debug: Let's see what type we actually got
            if (!(expr is BinaryExpression))
            {
                throw new AssertFailedException($"Expected BinaryExpression but got {expr.GetType().Name}. " +
                           $"This might indicate the parser is not correctly parsing arithmetic expressions.");
            }

            expr.Should().BeOfType<BinaryExpression>();

            // The root should be a subtraction (last operation due to left-to-right precedence)
            var rootExpr = expr as BinaryExpression;
            rootExpr.Should().NotBeNull();
            rootExpr.Operator.Should().Be(SqlTokenType.MINUS);

            // Left side should be addition: (price * quantity) + tax
            var leftExpr = rootExpr.Left as BinaryExpression;
            leftExpr.Should().NotBeNull();
            leftExpr.Operator.Should().Be(SqlTokenType.PLUS);

            // Left side of addition should be multiplication: price * quantity
            var multiplyExpr = leftExpr.Left as BinaryExpression;
            multiplyExpr.Should().NotBeNull();
            multiplyExpr.Operator.Should().Be(SqlTokenType.MULTIPLY);
            ((ColumnExpression)multiplyExpr.Left).ColumnName.Should().Be("price");
            ((ColumnExpression)multiplyExpr.Right).ColumnName.Should().Be("quantity");

            // Right side of subtraction should be division: discount / 2
            var divideExpr = rootExpr.Right as BinaryExpression;
            divideExpr.Should().NotBeNull();
            divideExpr.Operator.Should().Be(SqlTokenType.DIVIDE);
            ((ColumnExpression)divideExpr.Left).ColumnName.Should().Be("discount");
            ((LiteralExpression)divideExpr.Right).Value.Should().Be(2.0);
        }

        [TestMethod]
        public void TestArithmeticExpressionsWithAlias()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT price * quantity AS total_price, tax + shipping fee FROM orders");

            // Assert
            ast.SelectList.Count.Should().Be(2);

            // First expression with explicit alias
            var expr1 = ast.SelectList[0].Expression as BinaryExpression;
            expr1.Should().NotBeNull();
            expr1.Operator.Should().Be(SqlTokenType.MULTIPLY);
            ast.SelectList[0].Alias.Should().Be("total_price");

            // Second expression with implicit alias
            var expr2 = ast.SelectList[1].Expression as BinaryExpression;
            expr2.Should().NotBeNull();
            expr2.Operator.Should().Be(SqlTokenType.PLUS);
            ast.SelectList[1].Alias.Should().Be("fee");
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
            ast.SelectList.Count.Should().Be(3);

            // First: (price + tax) * quantity
            var expr1 = ast.SelectList[0].Expression as BinaryExpression;
            expr1.Should().NotBeNull();
            expr1.Operator.Should().Be(SqlTokenType.MULTIPLY);
            ast.SelectList[0].Alias.Should().Be("total_with_tax");

            // Verify the grouped expression
            var grouped = expr1.Left as BinaryExpression;
            grouped.Should().NotBeNull();
            grouped.Operator.Should().Be(SqlTokenType.PLUS);

            // Second: price * 0.9
            var expr2 = ast.SelectList[1].Expression as BinaryExpression;
            expr2.Should().NotBeNull();
            expr2.Operator.Should().Be(SqlTokenType.MULTIPLY);
            ((LiteralExpression)expr2.Right).Value.Should().Be(0.9);

            // Third: CASE expression
            ast.SelectList[2].Expression.Should().BeOfType<CaseExpression>();
        }
    }
}
