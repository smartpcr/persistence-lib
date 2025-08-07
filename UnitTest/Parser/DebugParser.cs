// -----------------------------------------------------------------------
// <copyright file="DebugParser.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Parser
{
    using System;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Parser;

    public class DebugParser
    {
        public static SelectStatement ParseSqlStatement(string sql)
        {
            var lexer = new Lexer(sql);
            var tokens = lexer.Tokenize();

            Console.WriteLine("Tokens:");
            foreach (var token in tokens)
            {
                Console.WriteLine($"  {token.Type}: {token.Value}");
            }

            var parser = new SqlParser(tokens);
            var ast = parser.Parse();

            return ast;
        }

        public static void DebugArithmeticExpression()
        {
            var sql = "SELECT price * quantity + tax - discount / 2 FROM orders";
            var lexer = new Lexer(sql);
            var tokens = lexer.Tokenize();

            Console.WriteLine("Tokens:");
            foreach (var token in tokens)
            {
                Console.WriteLine($"  {token.Type}: {token.Value}");
            }
            
            var parser = new SqlParser(tokens);
            var ast = (SelectStatement)parser.Parse();
            
            Console.WriteLine("\nAST:");
            Console.WriteLine($"SelectList Count: {ast.SelectList.Count}");
            var expr = ast.SelectList[0].Expression;
            Console.WriteLine($"Expression Type: {expr.GetType().Name}");

            if (expr is BinaryExpression binExpr)
            {
                PrintBinaryExpression(binExpr, 0);
            }
            else if (expr is ColumnExpression colExpr)
            {
                Console.WriteLine($"Column: {colExpr.ColumnName}");
            }
        }

        private static void PrintBinaryExpression(BinaryExpression expr, int indent)
        {
            var indentStr = new string(' ', indent * 2);
            Console.WriteLine($"{indentStr}BinaryOp: {expr.Operator}");
            Console.WriteLine($"{indentStr}Left:");
            PrintExpression(expr.Left, indent + 1);
            Console.WriteLine($"{indentStr}Right:");
            PrintExpression(expr.Right, indent + 1);
        }

        private static void PrintExpression(Expression expr, int indent)
        {
            var indentStr = new string(' ', indent * 2);
            if (expr is BinaryExpression binExpr)
            {
                PrintBinaryExpression(binExpr, indent);
            }
            else if (expr is ColumnExpression colExpr)
            {
                Console.WriteLine($"{indentStr}Column: {colExpr.ColumnName}");
            }
            else if (expr is LiteralExpression litExpr)
            {
                Console.WriteLine($"{indentStr}Literal: {litExpr.Value}");
            }
            else
            {
                Console.WriteLine($"{indentStr}{expr.GetType().Name}");
            }
        }
    }
}