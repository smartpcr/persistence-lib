// -----------------------------------------------------------------------
// <copyright file="ArithmeticParserTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Parser
{
    using System;
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
            return parser.Parse();
        }

        [TestMethod]
        public void TestSimpleAddition()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT a + b FROM table1");

            // Assert
            var expr = ast.SelectList[0].Expression;
            Assert.IsInstanceOfType(expr, typeof(BinaryExpression));
            var binExpr = (BinaryExpression)expr;
            Assert.AreEqual(SqlTokenType.PLUS, binExpr.Operator);
            Assert.IsInstanceOfType(binExpr.Left, typeof(ColumnExpression));
            Assert.IsInstanceOfType(binExpr.Right, typeof(ColumnExpression));
        }

        [TestMethod]
        public void TestSimpleMultiplication()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT a * b FROM table1");

            // Assert
            var expr = ast.SelectList[0].Expression;
            Assert.IsInstanceOfType(expr, typeof(BinaryExpression));
            var binExpr = (BinaryExpression)expr;
            Assert.AreEqual(SqlTokenType.MULTIPLY, binExpr.Operator);
        }

        [TestMethod]
        public void TestOperatorPrecedence()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT a + b * c FROM table1");

            // Assert
            var expr = ast.SelectList[0].Expression;
            Assert.IsInstanceOfType(expr, typeof(BinaryExpression));
            var addExpr = (BinaryExpression)expr;
            Assert.AreEqual(SqlTokenType.PLUS, addExpr.Operator);
            
            // Left should be column 'a'
            Assert.IsInstanceOfType(addExpr.Left, typeof(ColumnExpression));
            Assert.AreEqual("a", ((ColumnExpression)addExpr.Left).ColumnName);
            
            // Right should be 'b * c'
            Assert.IsInstanceOfType(addExpr.Right, typeof(BinaryExpression));
            var mulExpr = (BinaryExpression)addExpr.Right;
            Assert.AreEqual(SqlTokenType.MULTIPLY, mulExpr.Operator);
        }

        [TestMethod]
        public void TestComplexArithmeticExpression()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT price * quantity + tax - discount / 2 FROM orders");

            // Assert
            var expr = ast.SelectList[0].Expression;
            Assert.IsInstanceOfType(expr, typeof(BinaryExpression), "Root expression should be BinaryExpression");
            
            // The expression tree should be:
            // (price * quantity + tax) - (discount / 2)
            // Which is: ((price * quantity) + tax) - (discount / 2)
            var rootExpr = (BinaryExpression)expr;
            Assert.AreEqual(SqlTokenType.MINUS, rootExpr.Operator, "Root operator should be MINUS");
            
            // Left side: (price * quantity) + tax
            Assert.IsInstanceOfType(rootExpr.Left, typeof(BinaryExpression), "Left side should be BinaryExpression");
            var leftExpr = (BinaryExpression)rootExpr.Left;
            Assert.AreEqual(SqlTokenType.PLUS, leftExpr.Operator, "Left operator should be PLUS");
            
            // Right side: discount / 2
            Assert.IsInstanceOfType(rootExpr.Right, typeof(BinaryExpression), "Right side should be BinaryExpression");
            var rightExpr = (BinaryExpression)rootExpr.Right;
            Assert.AreEqual(SqlTokenType.DIVIDE, rightExpr.Operator, "Right operator should be DIVIDE");
        }

        [TestMethod]
        public void TestParenthesizedExpression()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT (a + b) * c FROM table1");

            // Assert
            var expr = ast.SelectList[0].Expression;
            Assert.IsInstanceOfType(expr, typeof(BinaryExpression));
            var mulExpr = (BinaryExpression)expr;
            Assert.AreEqual(SqlTokenType.MULTIPLY, mulExpr.Operator);
            
            // Left should be (a + b)
            Assert.IsInstanceOfType(mulExpr.Left, typeof(BinaryExpression));
            var addExpr = (BinaryExpression)mulExpr.Left;
            Assert.AreEqual(SqlTokenType.PLUS, addExpr.Operator);
        }

        [TestMethod]
        public void TestMixedArithmeticAndComparison()
        {
            // Arrange & Act
            var ast = this.ParseQuery("SELECT * FROM orders WHERE price * quantity > 100");

            // Assert
            Assert.IsNotNull(ast.Where);
            Assert.IsInstanceOfType(ast.Where, typeof(BinaryExpression));
            var compareExpr = (BinaryExpression)ast.Where;
            Assert.AreEqual(SqlTokenType.GREATER_THAN, compareExpr.Operator);
            
            // Left should be price * quantity
            Assert.IsInstanceOfType(compareExpr.Left, typeof(BinaryExpression));
            var mulExpr = (BinaryExpression)compareExpr.Left;
            Assert.AreEqual(SqlTokenType.MULTIPLY, mulExpr.Operator);
        }
    }
}