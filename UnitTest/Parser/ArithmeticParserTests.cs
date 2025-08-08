// -----------------------------------------------------------------------
// <copyright file="ArithmeticParserTests.cs" company="Microsoft Corp.">
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
    public class ArithmeticParserTests
    {
        private SelectStatement ParseQuery(string sql)
        {
            var lexer = new Lexer(sql);
            var tokens = lexer.Tokenize();
            var parser = new SqlParser(tokens);
            return (SelectStatement)parser.Parse();
        }

        [TestMethod]
        public void TestSimpleAddition()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT a + b FROM table1");

            // Assert
            var expr = ast.SelectList[0].Expression;
            expr.Should().BeOfType<BinaryExpression>();
            var binExpr = (BinaryExpression)expr;
            binExpr.Operator.Should().Be(SqlTokenType.PLUS);
            binExpr.Left.Should().BeOfType<ColumnExpression>();
            binExpr.Right.Should().BeOfType<ColumnExpression>();
        }

        [TestMethod]
        public void TestSimpleMultiplication()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT a * b FROM table1");

            // Assert
            var expr = ast.SelectList[0].Expression;
            expr.Should().BeOfType<BinaryExpression>();
            var binExpr = (BinaryExpression)expr;
            binExpr.Operator.Should().Be(SqlTokenType.MULTIPLY);
        }

        [TestMethod]
        public void TestOperatorPrecedence()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT a + b * c FROM table1");

            // Assert
            var expr = ast.SelectList[0].Expression;
            expr.Should().BeOfType<BinaryExpression>();
            var addExpr = (BinaryExpression)expr;
            addExpr.Operator.Should().Be(SqlTokenType.PLUS);
            
            // Left should be column 'a'
            addExpr.Left.Should().BeOfType<ColumnExpression>();
            ((ColumnExpression)addExpr.Left).ColumnName.Should().Be("a");
            
            // Right should be 'b * c'
            addExpr.Right.Should().BeOfType<BinaryExpression>();
            var mulExpr = (BinaryExpression)addExpr.Right;
            mulExpr.Operator.Should().Be(SqlTokenType.MULTIPLY);
        }

        [TestMethod]
        public void TestComplexArithmeticExpression()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT price * quantity + tax - discount / 2 FROM orders");

            // Assert
            var expr = ast.SelectList[0].Expression;
            expr.Should().BeOfType<BinaryExpression>("Root expression should be BinaryExpression");
            
            // The expression tree should be:
            // (price * quantity + tax) - (discount / 2)
            // Which is: ((price * quantity) + tax) - (discount / 2)
            var rootExpr = (BinaryExpression)expr;
            rootExpr.Operator.Should().Be(SqlTokenType.MINUS, "Root operator should be MINUS");
            
            // Left side: (price * quantity) + tax
            rootExpr.Left.Should().BeOfType<BinaryExpression>("Left side should be BinaryExpression");
            var leftExpr = (BinaryExpression)rootExpr.Left;
            leftExpr.Operator.Should().Be(SqlTokenType.PLUS, "Left operator should be PLUS");
            
            // Right side: discount / 2
            rootExpr.Right.Should().BeOfType<BinaryExpression>("Right side should be BinaryExpression");
            var rightExpr = (BinaryExpression)rootExpr.Right;
            rightExpr.Operator.Should().Be(SqlTokenType.DIVIDE, "Right operator should be DIVIDE");
        }

        [TestMethod]
        public void TestParenthesizedExpression()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT (a + b) * c FROM table1");

            // Assert
            var expr = ast.SelectList[0].Expression;
            expr.Should().BeOfType<BinaryExpression>();
            var mulExpr = (BinaryExpression)expr;
            mulExpr.Operator.Should().Be(SqlTokenType.MULTIPLY);
            
            // Left should be (a + b)
            mulExpr.Left.Should().BeOfType<BinaryExpression>();
            var addExpr = (BinaryExpression)mulExpr.Left;
            addExpr.Operator.Should().Be(SqlTokenType.PLUS);
        }

        [TestMethod]
        public void TestMixedArithmeticAndComparison()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT * FROM orders WHERE price * quantity > 100");

            // Assert
            ast.Where.Should().NotBeNull();
            ast.Where.Should().BeOfType<BinaryExpression>();
            var compareExpr = (BinaryExpression)ast.Where;
            compareExpr.Operator.Should().Be(SqlTokenType.GREATER_THAN);
            
            // Left should be price * quantity
            compareExpr.Left.Should().BeOfType<BinaryExpression>();
            var mulExpr = (BinaryExpression)compareExpr.Left;
            mulExpr.Operator.Should().Be(SqlTokenType.MULTIPLY);
        }
    }
}