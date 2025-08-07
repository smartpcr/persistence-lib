// -----------------------------------------------------------------------
// <copyright file="SqlParser.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;


    // Parser
    public class SqlParser
    {
        private readonly List<SqlToken> tokens;
        private int current;

        public SqlParser(List<SqlToken> tokens)
        {
            this.tokens = tokens;
            this.current = 0;
        }

        public SqlNode Parse()
        {
            if (this.Check(SqlTokenType.SELECT) || this.Check(SqlTokenType.WITH))
            {
                return this.ParseSelectStatement();
            }

            if (this.Check(SqlTokenType.CREATE))
            {
                return this.ParseCreateStatement();
            }

            throw new Exception("Unsupported statement type");
        }

        private SelectStatement ParseSelectStatement()
        {
            var select = new SelectStatement();

            // Parse CTEs if present
            if (this.Match(SqlTokenType.WITH))
            {
                select.CTEs = this.ParseCTEs();
            }

            this.Consume(SqlTokenType.SELECT, "Expected SELECT");

            // Parse DISTINCT
            if (this.Match(SqlTokenType.DISTINCT))
            {
                select.IsDistinct = true;
            }

            // Parse select list
            select.SelectList = this.ParseSelectList();

            // Parse FROM clause
            var hasFrom = false;
            if (this.Match(SqlTokenType.FROM))
            {
                select.From = this.ParseFromClause();
                hasFrom = true;
            }

            // Parse WHERE clause
            if (this.Match(SqlTokenType.WHERE))
            {
                if (!hasFrom)
                    throw new Exception("WHERE clause requires FROM");
                select.Where = this.ParseExpression();
            }

            // Parse GROUP BY
            if (this.Match(SqlTokenType.GROUP))
            {
                this.Consume(SqlTokenType.BY, "Expected BY after GROUP");
                select.GroupBy = this.ParseExpressionList();
            }

            // Parse HAVING
            if (this.Match(SqlTokenType.HAVING))
            {
                select.Having = this.ParseExpression();
            }

            // Parse ORDER BY
            if (this.Match(SqlTokenType.ORDER))
            {
                this.Consume(SqlTokenType.BY, "Expected BY after ORDER");
                select.OrderBy = this.ParseOrderByList();
            }

            // Parse LIMIT
            if (this.Match(SqlTokenType.LIMIT))
            {
                select.Limit = int.Parse(this.Consume(SqlTokenType.NUMBER_LITERAL, "Expected number after LIMIT").Value);
            }

            // Parse OFFSET
            if (this.Match(SqlTokenType.OFFSET))
            {
                select.Offset = int.Parse(this.Consume(SqlTokenType.NUMBER_LITERAL, "Expected number after OFFSET").Value);
            }

            return select;
        }

        private SqlNode ParseCreateStatement()
        {
            this.Consume(SqlTokenType.CREATE, "Expected CREATE");

            if (this.Match(SqlTokenType.TABLE))
            {
                return this.ParseCreateTable();
            }

            if (this.Match(SqlTokenType.INDEX))
            {
                return this.ParseCreateIndex();
            }

            throw new Exception("Expected TABLE or INDEX after CREATE");
        }

        private CreateTableStatement ParseCreateTable()
        {
            var stmt = new CreateTableStatement();

            // Optional IF NOT EXISTS
            if (this.Match(SqlTokenType.IF))
            {
                this.Consume(SqlTokenType.NOT, "Expected NOT after IF");
                this.Consume(SqlTokenType.EXISTS, "Expected EXISTS after NOT");
            }

            stmt.TableName = this.Consume(SqlTokenType.IDENTIFIER, "Expected table name").Value;
            this.Consume(SqlTokenType.LEFT_PAREN, "Expected ( after table name");

            var first = true;
            while (!this.Check(SqlTokenType.RIGHT_PAREN))
            {
                if (!first)
                {
                    this.Consume(SqlTokenType.COMMA, "Expected comma between definitions");
                }
                first = false;

                if (this.Check(SqlTokenType.CONSTRAINT) || this.Check(SqlTokenType.PRIMARY) ||
                    this.Check(SqlTokenType.UNIQUE) || this.Check(SqlTokenType.FOREIGN))
                {
                    stmt.Constraints.Add(this.ParseTableConstraint());
                }
                else
                {
                    stmt.Columns.Add(this.ParseColumnDefinition());
                }
            }

            this.Consume(SqlTokenType.RIGHT_PAREN, "Expected ) after table definition");
            return stmt;
        }

        private ColumnDefinition ParseColumnDefinition()
        {
            var column = new ColumnDefinition();
            column.Name = this.Consume(SqlTokenType.IDENTIFIER, "Expected column name").Value;
            column.DataType = this.Consume(SqlTokenType.IDENTIFIER, "Expected data type").Value;

            var sb = new StringBuilder(column.DataType);
            while (!this.Check(SqlTokenType.COMMA) && !this.Check(SqlTokenType.RIGHT_PAREN) &&
                   !this.Check(SqlTokenType.CONSTRAINT) && !this.Check(SqlTokenType.PRIMARY) &&
                   !this.Check(SqlTokenType.UNIQUE) && !this.Check(SqlTokenType.FOREIGN))
            {
                sb.Append(' ').Append(this.Advance().Value);
            }

            column.DataType = sb.ToString();
            return column;
        }

        private TableConstraint ParseTableConstraint()
        {
            var constraint = new TableConstraint();

            if (this.Match(SqlTokenType.CONSTRAINT))
            {
                constraint.Name = this.Consume(SqlTokenType.IDENTIFIER, "Expected constraint name").Value;
            }

            if (this.Match(SqlTokenType.PRIMARY))
            {
                this.Consume(SqlTokenType.KEY, "Expected KEY after PRIMARY");
                constraint.Type = ConstraintType.PrimaryKey;
            }
            else if (this.Match(SqlTokenType.UNIQUE))
            {
                constraint.Type = ConstraintType.Unique;
            }
            else if (this.Match(SqlTokenType.FOREIGN))
            {
                this.Consume(SqlTokenType.KEY, "Expected KEY after FOREIGN");
                constraint.Type = ConstraintType.ForeignKey;
            }
            else
            {
                throw new Exception("Unsupported constraint type");
            }

            this.Consume(SqlTokenType.LEFT_PAREN, "Expected ( after constraint type");
            do
            {
                constraint.Columns.Add(this.Consume(SqlTokenType.IDENTIFIER, "Expected column name").Value);
            } while (this.Match(SqlTokenType.COMMA));
            this.Consume(SqlTokenType.RIGHT_PAREN, "Expected ) after constraint columns");

            // For FOREIGN KEY, optionally parse REFERENCES clause but ignore details
            if (constraint.Type == ConstraintType.ForeignKey && this.Match(SqlTokenType.REFERENCES))
            {
                this.Consume(SqlTokenType.IDENTIFIER, "Expected referenced table");
                this.Consume(SqlTokenType.LEFT_PAREN, "Expected (");
                do
                {
                    this.Consume(SqlTokenType.IDENTIFIER, "Expected referenced column");
                } while (this.Match(SqlTokenType.COMMA));
                this.Consume(SqlTokenType.RIGHT_PAREN, "Expected )");
            }

            return constraint;
        }

        private CreateIndexStatement ParseCreateIndex()
        {
            var stmt = new CreateIndexStatement();
            stmt.IndexName = this.Consume(SqlTokenType.IDENTIFIER, "Expected index name").Value;
            this.Consume(SqlTokenType.ON, "Expected ON");
            stmt.TableName = this.Consume(SqlTokenType.IDENTIFIER, "Expected table name").Value;
            this.Consume(SqlTokenType.LEFT_PAREN, "Expected (");
            do
            {
                stmt.Columns.Add(this.Consume(SqlTokenType.IDENTIFIER, "Expected column name").Value);
            } while (this.Match(SqlTokenType.COMMA));
            this.Consume(SqlTokenType.RIGHT_PAREN, "Expected )");
            return stmt;
        }

        private List<CteDefinition> ParseCTEs()
        {
            var ctes = new List<CteDefinition>();
            var isRecursive = this.Match(SqlTokenType.RECURSIVE);

            do
            {
                var cte = new CteDefinition
                {
                    IsRecursive = isRecursive,
                    Name = this.Consume(SqlTokenType.IDENTIFIER, "Expected CTE name").Value
                };

                // Parse column list if present
                if (this.Match(SqlTokenType.LEFT_PAREN))
                {
                    do
                    {
                        cte.Columns.Add(this.Consume(SqlTokenType.IDENTIFIER, "Expected column name").Value);
                    } while (this.Match(SqlTokenType.COMMA));

                    this.Consume(SqlTokenType.RIGHT_PAREN, "Expected ) after column list");
                }

                this.Consume(SqlTokenType.AS, "Expected AS in CTE definition");
                this.Consume(SqlTokenType.LEFT_PAREN, "Expected ( before CTE query");

                cte.Query = this.ParseSelectStatement();

                this.Consume(SqlTokenType.RIGHT_PAREN, "Expected ) after CTE query");

                ctes.Add(cte);
            } while (this.Match(SqlTokenType.COMMA));

            return ctes;
        }

        private List<SelectItem> ParseSelectList()
        {
            var items = new List<SelectItem>();

            do
            {
                var item = new SelectItem { Expression = this.ParseExpression() };

                if (this.Match(SqlTokenType.AS))
                {
                    item.Alias = this.Consume(SqlTokenType.IDENTIFIER, "Expected alias").Value;
                }
                else if (this.Check(SqlTokenType.IDENTIFIER))
                {
                    // Implicit alias
                    item.Alias = this.Advance().Value;
                }

                items.Add(item);
            } while (this.Match(SqlTokenType.COMMA));

            return items;
        }

        private FromClause ParseFromClause()
        {
            var from = new FromClause { Table = this.ParseTableReference() };

            // Parse JOINs
            while (this.IsJoinKeyword())
            {
                from.Joins.Add(this.ParseJoin());
            }

            return from;
        }

        private bool IsJoinKeyword()
        {
            return this.Check(SqlTokenType.JOIN) || this.Check(SqlTokenType.INNER) ||
                   this.Check(SqlTokenType.LEFT) || this.Check(SqlTokenType.RIGHT) ||
                   this.Check(SqlTokenType.FULL) || this.Check(SqlTokenType.CROSS);
        }

        private JoinClause ParseJoin()
        {
            var join = new JoinClause();

            if (this.Match(SqlTokenType.INNER))
            {
                join.Type = JoinType.Inner;
                this.Match(SqlTokenType.JOIN); // Optional JOIN after INNER
            }
            else if (this.Match(SqlTokenType.LEFT))
            {
                join.Type = JoinType.Left;
                this.Match(SqlTokenType.OUTER); // Optional OUTER
                this.Consume(SqlTokenType.JOIN, "Expected JOIN");
            }
            else if (this.Match(SqlTokenType.RIGHT))
            {
                join.Type = JoinType.Right;
                this.Match(SqlTokenType.OUTER); // Optional OUTER
                this.Consume(SqlTokenType.JOIN, "Expected JOIN");
            }
            else if (this.Match(SqlTokenType.FULL))
            {
                join.Type = JoinType.Full;
                this.Match(SqlTokenType.OUTER); // Optional OUTER
                this.Consume(SqlTokenType.JOIN, "Expected JOIN");
            }
            else if (this.Match(SqlTokenType.CROSS))
            {
                join.Type = JoinType.Cross;
                this.Consume(SqlTokenType.JOIN, "Expected JOIN");
            }
            else if (this.Match(SqlTokenType.JOIN))
            {
                join.Type = JoinType.Inner; // Default to INNER
            }

            join.Table = this.ParseTableReference();

            if (this.Match(SqlTokenType.ON))
            {
                join.OnCondition = this.ParseExpression();
            }

            return join;
        }

        private TableReference ParseTableReference()
        {
            var table = new TableReference();

            if (this.Match(SqlTokenType.LEFT_PAREN))
            {
                // Subquery in FROM
                var subquery = this.ParseSelectStatement();
                this.Consume(SqlTokenType.RIGHT_PAREN, "Expected ) after subquery");
                table.Subquery = subquery;
            }
            else
            {
                var firstPart = this.Consume(SqlTokenType.IDENTIFIER, "Expected table name").Value;

                if (this.Match(SqlTokenType.DOT))
                {
                    table.Schema = firstPart;
                    table.TableName = this.Consume(SqlTokenType.IDENTIFIER, "Expected table name after schema").Value;
                }
                else
                {
                    table.TableName = firstPart;
                }
            }

            if (this.Match(SqlTokenType.AS))
            {
                table.Alias = this.Consume(SqlTokenType.IDENTIFIER, "Expected alias after AS").Value;
            }
            else if (this.Check(SqlTokenType.IDENTIFIER))
            {
                table.Alias = this.Advance().Value;
            }

            return table;
        }

        private Expression ParseExpression()
        {
            return this.ParseOr();
        }

        private Expression ParseOr()
        {
            var expr = this.ParseAnd();

            while (this.Match(SqlTokenType.OR))
            {
                var op = this.Previous().Type;
                var right = this.ParseAnd();
                expr = new BinaryExpression { Left = expr, Operator = op, Right = right };
            }

            return expr;
        }

        private Expression ParseAnd()
        {
            var expr = this.ParseNot();

            while (this.Match(SqlTokenType.AND))
            {
                var op = this.Previous().Type;
                var right = this.ParseNot();
                expr = new BinaryExpression { Left = expr, Operator = op, Right = right };
            }

            return expr;
        }

        private Expression ParseNot()
        {
            if (this.Match(SqlTokenType.NOT))
            {
                var op = this.Previous().Type;
                var expr = this.ParseNot();
                return new UnaryExpression { Operator = op, Operand = expr };
            }

            return this.ParseComparison();
        }

        private Expression ParseComparison()
        {
            var expr = this.ParseAddition();

            while (this.Match(SqlTokenType.EQUALS, SqlTokenType.NOT_EQUALS, SqlTokenType.LESS_THAN,
                         SqlTokenType.GREATER_THAN, SqlTokenType.LESS_THAN_EQUALS,
                         SqlTokenType.GREATER_THAN_EQUALS, SqlTokenType.LIKE, SqlTokenType.IN))
            {
                var op = this.Previous().Type;

                if (op == SqlTokenType.IN)
                {
                    this.Consume(SqlTokenType.LEFT_PAREN, "Expected ( after IN");

                    if (this.Check(SqlTokenType.SELECT))
                    {
                        var subquery = this.ParseSelectStatement();
                        this.Consume(SqlTokenType.RIGHT_PAREN, "Expected ) after subquery");
                        return new FunctionExpression
                        {
                            FunctionName = "IN",
                            Arguments = new List<Expression>
                            {
                                expr,
                                new SubqueryExpression { Query = subquery }
                            }
                        };
                    }

                    var values = new List<Expression>();
                    do
                    {
                        values.Add(this.ParseExpression());
                    } while (this.Match(SqlTokenType.COMMA));

                    this.Consume(SqlTokenType.RIGHT_PAREN, "Expected ) after IN values");

                    // For simplicity, represent IN as a function call
                    return new FunctionExpression
                    {
                        FunctionName = "IN",
                        Arguments = new List<Expression> { expr }.Concat(values).ToList()
                    };
                }

                var right = this.ParseAddition();
                expr = new BinaryExpression { Left = expr, Operator = op, Right = right };
            }

            return expr;
        }

        private Expression ParseAddition()
        {
            var expr = this.ParseMultiplication();

            while (this.Match(SqlTokenType.PLUS, SqlTokenType.MINUS))
            {
                var op = this.Previous().Type;
                var right = this.ParseMultiplication();
                expr = new BinaryExpression { Left = expr, Operator = op, Right = right };
            }

            return expr;
        }

        private Expression ParseMultiplication()
        {
            var expr = this.ParseUnary();

            while (this.Match(SqlTokenType.STAR, SqlTokenType.MULTIPLY, SqlTokenType.DIVIDE, SqlTokenType.MODULO))
            {
                var op = this.Previous().Type;
                if (op == SqlTokenType.STAR)
                {
                    op = SqlTokenType.MULTIPLY;
                }

                var right = this.ParseUnary();
                expr = new BinaryExpression { Left = expr, Operator = op, Right = right };
            }

            return expr;
        }

        private Expression ParseUnary()
        {
            if (this.Match(SqlTokenType.MINUS, SqlTokenType.PLUS))
            {
                var op = this.Previous().Type;
                var expr = this.ParseUnary();
                return new UnaryExpression { Operator = op, Operand = expr };
            }

            return this.ParsePrimary();
        }

        private Expression ParsePrimary()
        {
            // Literals
            if (this.Match(SqlTokenType.NUMBER_LITERAL))
            {
                return new LiteralExpression
                {
                    Value = double.Parse(this.Previous().Value),
                    Type = SqlTokenType.NUMBER_LITERAL
                };
            }

            if (this.Match(SqlTokenType.STRING_LITERAL))
            {
                return new LiteralExpression
                {
                    Value = this.Previous().Value,
                    Type = SqlTokenType.STRING_LITERAL
                };
            }

            if (this.Match(SqlTokenType.NULL))
            {
                return new LiteralExpression
                {
                    Value = null,
                    Type = SqlTokenType.NULL
                };
            }

            // CASE expression
            if (this.Match(SqlTokenType.CASE))
            {
                return this.ParseCaseExpression();
            }

            // Subquery
            if (this.Match(SqlTokenType.LEFT_PAREN))
            {
                if (this.Check(SqlTokenType.SELECT))
                {
                    var subquery = this.ParseSelectStatement();
                    this.Consume(SqlTokenType.RIGHT_PAREN, "Expected ) after subquery");
                    return new SubqueryExpression { Query = subquery };
                }

                // Grouped expression
                var expr = this.ParseExpression();
                this.Consume(SqlTokenType.RIGHT_PAREN, "Expected ) after expression");
                return expr;
            }

            // Column or function
            if (this.Match(SqlTokenType.IDENTIFIER))
            {
                var name = this.Previous().Value;

                if (this.Match(SqlTokenType.LEFT_PAREN))
                {
                    // Function call
                    var func = new FunctionExpression { FunctionName = name };

                    if (!this.Check(SqlTokenType.RIGHT_PAREN))
                    {
                        if (this.Match(SqlTokenType.DISTINCT))
                        {
                            func.IsDistinct = true;
                        }

                        do
                        {
                            func.Arguments.Add(this.ParseExpression());
                        } while (this.Match(SqlTokenType.COMMA));
                    }

                    this.Consume(SqlTokenType.RIGHT_PAREN, "Expected ) after function arguments");
                    return func;
                }
                else if (this.Match(SqlTokenType.DOT))
                {
                    // Table.Column or Table.*
                    if (this.Match(SqlTokenType.STAR))
                    {
                        return new ColumnExpression { TableAlias = name, ColumnName = "*" };
                    }

                    var columnName = this.Consume(SqlTokenType.IDENTIFIER, "Expected column name").Value;
                    return new ColumnExpression { TableAlias = name, ColumnName = columnName };
                }
                else
                {
                    // Simple column
                    return new ColumnExpression { ColumnName = name };
                }
            }

            if (this.Match(SqlTokenType.STAR))
            {
                return new ColumnExpression { ColumnName = "*" };
            }

            throw new Exception($"Unexpected SqlToken: {this.Peek()}");
        }

        private CaseExpression ParseCaseExpression()
        {
            var caseExpr = new CaseExpression();

            while (this.Match(SqlTokenType.WHEN))
            {
                var when = new WhenClause
                {
                    Condition = this.ParseExpression()
                };

                this.Consume(SqlTokenType.THEN, "Expected THEN after WHEN condition");
                when.Result = this.ParseExpression();

                caseExpr.WhenClauses.Add(when);
            }

            if (this.Match(SqlTokenType.ELSE))
            {
                caseExpr.ElseExpression = this.ParseExpression();
            }

            this.Consume(SqlTokenType.END, "Expected END to close CASE expression");

            return caseExpr;
        }

        private List<Expression> ParseExpressionList()
        {
            var expressions = new List<Expression>();

            do
            {
                expressions.Add(this.ParseExpression());
            } while (this.Match(SqlTokenType.COMMA));

            return expressions;
        }

        private List<OrderByItem> ParseOrderByList()
        {
            var items = new List<OrderByItem>();

            do
            {
                var item = new OrderByItem { Expression = this.ParseExpression() };

                if (this.Match(SqlTokenType.ASC))
                {
                    item.IsAscending = true;
                }
                else if (this.Match(SqlTokenType.DESC))
                {
                    item.IsAscending = false;
                }

                items.Add(item);
            } while (this.Match(SqlTokenType.COMMA));

            return items;
        }

        // Helper methods
        private bool Match(params SqlTokenType[] types)
        {
            foreach (var type in types)
            {
                if (this.Check(type))
                {
                    this.Advance();
                    return true;
                }
            }
            return false;
        }

        private bool Check(SqlTokenType type)
        {
            if (this.IsAtEnd()) return false;
            return this.Peek().Type == type;
        }

        private SqlToken Advance()
        {
            if (!this.IsAtEnd()) this.current++;
            return this.Previous();
        }

        private bool IsAtEnd()
        {
            return this.Peek().Type == SqlTokenType.EOF;
        }

        private SqlToken Peek()
        {
            return this.tokens[this.current];
        }

        private SqlToken Previous()
        {
            return this.tokens[this.current - 1];
        }

        private SqlToken Consume(SqlTokenType type, string message)
        {
            if (this.Check(type)) return this.Advance();
            throw new Exception($"{message} at position {this.Peek().Position}");
        }
    }

}
