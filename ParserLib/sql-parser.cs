using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SqlParser
{
    // Token types for lexical analysis
    public enum TokenType
    {
        // Keywords
        SELECT, FROM, WHERE, JOIN, INNER, LEFT, RIGHT, FULL, OUTER, ON,
        WITH, AS, RECURSIVE, UNION, ALL, DISTINCT, GROUP, BY, HAVING,
        ORDER, ASC, DESC, LIMIT, OFFSET, INSERT, INTO, VALUES, UPDATE,
        SET, DELETE, CREATE, TABLE, ALTER, DROP, AND, OR, NOT, IN,
        BETWEEN, LIKE, IS, NULL, EXISTS, CASE, WHEN, THEN, ELSE, END,
        
        // Identifiers and literals
        IDENTIFIER, STRING_LITERAL, NUMBER_LITERAL,
        
        // Operators
        EQUALS, NOT_EQUALS, LESS_THAN, GREATER_THAN, LESS_THAN_EQUALS,
        GREATER_THAN_EQUALS, PLUS, MINUS, MULTIPLY, DIVIDE, MODULO,
        
        // Delimiters
        LEFT_PAREN, RIGHT_PAREN, COMMA, SEMICOLON, DOT, STAR,
        
        // Special
        EOF, UNKNOWN
    }

    public class Token
    {
        public TokenType Type { get; }
        public string Value { get; }
        public int Position { get; }

        public Token(TokenType type, string value, int position)
        {
            Type = type;
            Value = value;
            Position = position;
        }

        public override string ToString() => $"{Type}: {Value}";
    }

    public class Lexer
    {
        private readonly string _input;
        private int _position;
        private readonly Dictionary<string, TokenType> _keywords;

        public Lexer(string input)
        {
            _input = input;
            _position = 0;
            _keywords = InitializeKeywords();
        }

        private Dictionary<string, TokenType> InitializeKeywords()
        {
            return new Dictionary<string, TokenType>(StringComparer.OrdinalIgnoreCase)
            {
                ["SELECT"] = TokenType.SELECT,
                ["FROM"] = TokenType.FROM,
                ["WHERE"] = TokenType.WHERE,
                ["JOIN"] = TokenType.JOIN,
                ["INNER"] = TokenType.INNER,
                ["LEFT"] = TokenType.LEFT,
                ["RIGHT"] = TokenType.RIGHT,
                ["FULL"] = TokenType.FULL,
                ["OUTER"] = TokenType.OUTER,
                ["ON"] = TokenType.ON,
                ["WITH"] = TokenType.WITH,
                ["AS"] = TokenType.AS,
                ["RECURSIVE"] = TokenType.RECURSIVE,
                ["UNION"] = TokenType.UNION,
                ["ALL"] = TokenType.ALL,
                ["DISTINCT"] = TokenType.DISTINCT,
                ["GROUP"] = TokenType.GROUP,
                ["BY"] = TokenType.BY,
                ["HAVING"] = TokenType.HAVING,
                ["ORDER"] = TokenType.ORDER,
                ["ASC"] = TokenType.ASC,
                ["DESC"] = TokenType.DESC,
                ["LIMIT"] = TokenType.LIMIT,
                ["OFFSET"] = TokenType.OFFSET,
                ["AND"] = TokenType.AND,
                ["OR"] = TokenType.OR,
                ["NOT"] = TokenType.NOT,
                ["IN"] = TokenType.IN,
                ["BETWEEN"] = TokenType.BETWEEN,
                ["LIKE"] = TokenType.LIKE,
                ["IS"] = TokenType.IS,
                ["NULL"] = TokenType.NULL,
                ["EXISTS"] = TokenType.EXISTS,
                ["CASE"] = TokenType.CASE,
                ["WHEN"] = TokenType.WHEN,
                ["THEN"] = TokenType.THEN,
                ["ELSE"] = TokenType.ELSE,
                ["END"] = TokenType.END,
            };
        }

        public List<Token> Tokenize()
        {
            var tokens = new List<Token>();

            while (_position < _input.Length)
            {
                SkipWhitespace();
                if (_position >= _input.Length) break;

                var token = NextToken();
                if (token.Type != TokenType.UNKNOWN)
                    tokens.Add(token);
            }

            tokens.Add(new Token(TokenType.EOF, "", _position));
            return tokens;
        }

        private void SkipWhitespace()
        {
            while (_position < _input.Length && char.IsWhiteSpace(_input[_position]))
                _position++;
        }

        private Token NextToken()
        {
            var startPos = _position;
            var ch = _input[_position];

            // Single character tokens
            switch (ch)
            {
                case '(': _position++; return new Token(TokenType.LEFT_PAREN, "(", startPos);
                case ')': _position++; return new Token(TokenType.RIGHT_PAREN, ")", startPos);
                case ',': _position++; return new Token(TokenType.COMMA, ",", startPos);
                case ';': _position++; return new Token(TokenType.SEMICOLON, ";", startPos);
                case '.': _position++; return new Token(TokenType.DOT, ".", startPos);
                case '*': _position++; return new Token(TokenType.STAR, "*", startPos);
                case '+': _position++; return new Token(TokenType.PLUS, "+", startPos);
                case '-': _position++; return new Token(TokenType.MINUS, "-", startPos);
                case '/': _position++; return new Token(TokenType.DIVIDE, "/", startPos);
                case '%': _position++; return new Token(TokenType.MODULO, "%", startPos);
            }

            // Multi-character operators
            if (ch == '=')
            {
                _position++;
                return new Token(TokenType.EQUALS, "=", startPos);
            }
            else if (ch == '<')
            {
                _position++;
                if (_position < _input.Length && _input[_position] == '=')
                {
                    _position++;
                    return new Token(TokenType.LESS_THAN_EQUALS, "<=", startPos);
                }
                else if (_position < _input.Length && _input[_position] == '>')
                {
                    _position++;
                    return new Token(TokenType.NOT_EQUALS, "<>", startPos);
                }
                return new Token(TokenType.LESS_THAN, "<", startPos);
            }
            else if (ch == '>')
            {
                _position++;
                if (_position < _input.Length && _input[_position] == '=')
                {
                    _position++;
                    return new Token(TokenType.GREATER_THAN_EQUALS, ">=", startPos);
                }
                return new Token(TokenType.GREATER_THAN, ">", startPos);
            }
            else if (ch == '!')
            {
                _position++;
                if (_position < _input.Length && _input[_position] == '=')
                {
                    _position++;
                    return new Token(TokenType.NOT_EQUALS, "!=", startPos);
                }
            }

            // String literals
            if (ch == '\'' || ch == '"')
            {
                return ReadStringLiteral(ch);
            }

            // Numbers
            if (char.IsDigit(ch))
            {
                return ReadNumber();
            }

            // Identifiers and keywords
            if (char.IsLetter(ch) || ch == '_' || ch == '[')
            {
                return ReadIdentifierOrKeyword();
            }

            // Unknown character
            _position++;
            return new Token(TokenType.UNKNOWN, ch.ToString(), startPos);
        }

        private Token ReadStringLiteral(char quoteChar)
        {
            var startPos = _position;
            var sb = new StringBuilder();
            _position++; // Skip opening quote

            while (_position < _input.Length && _input[_position] != quoteChar)
            {
                if (_input[_position] == '\\' && _position + 1 < _input.Length)
                {
                    _position++;
                    sb.Append(_input[_position]);
                }
                else
                {
                    sb.Append(_input[_position]);
                }
                _position++;
            }

            if (_position < _input.Length)
                _position++; // Skip closing quote

            return new Token(TokenType.STRING_LITERAL, sb.ToString(), startPos);
        }

        private Token ReadNumber()
        {
            var startPos = _position;
            var sb = new StringBuilder();

            while (_position < _input.Length && (char.IsDigit(_input[_position]) || _input[_position] == '.'))
            {
                sb.Append(_input[_position]);
                _position++;
            }

            return new Token(TokenType.NUMBER_LITERAL, sb.ToString(), startPos);
        }

        private Token ReadIdentifierOrKeyword()
        {
            var startPos = _position;
            var sb = new StringBuilder();

            // Handle bracketed identifiers
            if (_input[_position] == '[')
            {
                _position++;
                while (_position < _input.Length && _input[_position] != ']')
                {
                    sb.Append(_input[_position]);
                    _position++;
                }
                if (_position < _input.Length)
                    _position++; // Skip closing bracket
                
                return new Token(TokenType.IDENTIFIER, sb.ToString(), startPos);
            }

            // Regular identifiers
            while (_position < _input.Length && 
                   (char.IsLetterOrDigit(_input[_position]) || _input[_position] == '_'))
            {
                sb.Append(_input[_position]);
                _position++;
            }

            var value = sb.ToString();
            if (_keywords.TryGetValue(value, out var keywordType))
            {
                return new Token(keywordType, value, startPos);
            }

            return new Token(TokenType.IDENTIFIER, value, startPos);
        }
    }

    // AST Node base classes
    public abstract class SqlNode { }

    public class SelectStatement : SqlNode
    {
        public List<CteDefinition> CTEs { get; set; } = new List<CteDefinition>();
        public bool IsDistinct { get; set; }
        public List<SelectItem> SelectList { get; set; } = new List<SelectItem>();
        public FromClause From { get; set; }
        public Expression Where { get; set; }
        public List<Expression> GroupBy { get; set; } = new List<Expression>();
        public Expression Having { get; set; }
        public List<OrderByItem> OrderBy { get; set; } = new List<OrderByItem>();
        public int? Limit { get; set; }
        public int? Offset { get; set; }
    }

    public class CteDefinition : SqlNode
    {
        public string Name { get; set; }
        public List<string> Columns { get; set; } = new List<string>();
        public SelectStatement Query { get; set; }
        public bool IsRecursive { get; set; }
    }

    public class SelectItem : SqlNode
    {
        public Expression Expression { get; set; }
        public string Alias { get; set; }
    }

    public class FromClause : SqlNode
    {
        public TableReference Table { get; set; }
        public List<JoinClause> Joins { get; set; } = new List<JoinClause>();
    }

    public class TableReference : SqlNode
    {
        public string Schema { get; set; }
        public string TableName { get; set; }
        public string Alias { get; set; }
    }

    public class JoinClause : SqlNode
    {
        public JoinType Type { get; set; }
        public TableReference Table { get; set; }
        public Expression OnCondition { get; set; }
    }

    public enum JoinType
    {
        Inner, Left, Right, Full, Cross
    }

    public class OrderByItem : SqlNode
    {
        public Expression Expression { get; set; }
        public bool IsAscending { get; set; } = true;
    }

    // Expression nodes
    public abstract class Expression : SqlNode { }

    public class ColumnExpression : Expression
    {
        public string TableAlias { get; set; }
        public string ColumnName { get; set; }
    }

    public class LiteralExpression : Expression
    {
        public object Value { get; set; }
        public TokenType Type { get; set; }
    }

    public class BinaryExpression : Expression
    {
        public Expression Left { get; set; }
        public TokenType Operator { get; set; }
        public Expression Right { get; set; }
    }

    public class UnaryExpression : Expression
    {
        public TokenType Operator { get; set; }
        public Expression Operand { get; set; }
    }

    public class FunctionExpression : Expression
    {
        public string FunctionName { get; set; }
        public List<Expression> Arguments { get; set; } = new List<Expression>();
    }

    public class CaseExpression : Expression
    {
        public List<WhenClause> WhenClauses { get; set; } = new List<WhenClause>();
        public Expression ElseExpression { get; set; }
    }

    public class WhenClause
    {
        public Expression Condition { get; set; }
        public Expression Result { get; set; }
    }

    public class SubqueryExpression : Expression
    {
        public SelectStatement Query { get; set; }
    }

    // Parser
    public class SqlParser
    {
        private readonly List<Token> _tokens;
        private int _current;

        public SqlParser(List<Token> tokens)
        {
            _tokens = tokens;
            _current = 0;
        }

        public SelectStatement Parse()
        {
            return ParseSelectStatement();
        }

        private SelectStatement ParseSelectStatement()
        {
            var select = new SelectStatement();

            // Parse CTEs if present
            if (Match(TokenType.WITH))
            {
                select.CTEs = ParseCTEs();
            }

            Consume(TokenType.SELECT, "Expected SELECT");

            // Parse DISTINCT
            if (Match(TokenType.DISTINCT))
            {
                select.IsDistinct = true;
            }

            // Parse select list
            select.SelectList = ParseSelectList();

            // Parse FROM clause
            if (Match(TokenType.FROM))
            {
                select.From = ParseFromClause();
            }

            // Parse WHERE clause
            if (Match(TokenType.WHERE))
            {
                select.Where = ParseExpression();
            }

            // Parse GROUP BY
            if (Match(TokenType.GROUP))
            {
                Consume(TokenType.BY, "Expected BY after GROUP");
                select.GroupBy = ParseExpressionList();
            }

            // Parse HAVING
            if (Match(TokenType.HAVING))
            {
                select.Having = ParseExpression();
            }

            // Parse ORDER BY
            if (Match(TokenType.ORDER))
            {
                Consume(TokenType.BY, "Expected BY after ORDER");
                select.OrderBy = ParseOrderByList();
            }

            // Parse LIMIT
            if (Match(TokenType.LIMIT))
            {
                select.Limit = int.Parse(Consume(TokenType.NUMBER_LITERAL, "Expected number after LIMIT").Value);
            }

            // Parse OFFSET
            if (Match(TokenType.OFFSET))
            {
                select.Offset = int.Parse(Consume(TokenType.NUMBER_LITERAL, "Expected number after OFFSET").Value);
            }

            return select;
        }

        private List<CteDefinition> ParseCTEs()
        {
            var ctes = new List<CteDefinition>();
            bool isRecursive = Match(TokenType.RECURSIVE);

            do
            {
                var cte = new CteDefinition
                {
                    IsRecursive = isRecursive,
                    Name = Consume(TokenType.IDENTIFIER, "Expected CTE name").Value
                };

                // Parse column list if present
                if (Match(TokenType.LEFT_PAREN))
                {
                    do
                    {
                        cte.Columns.Add(Consume(TokenType.IDENTIFIER, "Expected column name").Value);
                    } while (Match(TokenType.COMMA));
                    
                    Consume(TokenType.RIGHT_PAREN, "Expected ) after column list");
                }

                Consume(TokenType.AS, "Expected AS in CTE definition");
                Consume(TokenType.LEFT_PAREN, "Expected ( before CTE query");
                
                cte.Query = ParseSelectStatement();
                
                Consume(TokenType.RIGHT_PAREN, "Expected ) after CTE query");
                
                ctes.Add(cte);
            } while (Match(TokenType.COMMA));

            return ctes;
        }

        private List<SelectItem> ParseSelectList()
        {
            var items = new List<SelectItem>();

            if (Check(TokenType.STAR))
            {
                Advance();
                items.Add(new SelectItem 
                { 
                    Expression = new ColumnExpression { ColumnName = "*" } 
                });
            }
            else
            {
                do
                {
                    var item = new SelectItem { Expression = ParseExpression() };
                    
                    if (Match(TokenType.AS))
                    {
                        item.Alias = Consume(TokenType.IDENTIFIER, "Expected alias").Value;
                    }
                    else if (Check(TokenType.IDENTIFIER))
                    {
                        // Implicit alias
                        item.Alias = Advance().Value;
                    }
                    
                    items.Add(item);
                } while (Match(TokenType.COMMA));
            }

            return items;
        }

        private FromClause ParseFromClause()
        {
            var from = new FromClause { Table = ParseTableReference() };

            // Parse JOINs
            while (IsJoinKeyword())
            {
                from.Joins.Add(ParseJoin());
            }

            return from;
        }

        private bool IsJoinKeyword()
        {
            return Check(TokenType.JOIN) || Check(TokenType.INNER) || 
                   Check(TokenType.LEFT) || Check(TokenType.RIGHT) || 
                   Check(TokenType.FULL);
        }

        private JoinClause ParseJoin()
        {
            var join = new JoinClause();

            if (Match(TokenType.INNER))
            {
                join.Type = JoinType.Inner;
                Match(TokenType.JOIN); // Optional JOIN after INNER
            }
            else if (Match(TokenType.LEFT))
            {
                join.Type = JoinType.Left;
                Match(TokenType.OUTER); // Optional OUTER
                Consume(TokenType.JOIN, "Expected JOIN");
            }
            else if (Match(TokenType.RIGHT))
            {
                join.Type = JoinType.Right;
                Match(TokenType.OUTER); // Optional OUTER
                Consume(TokenType.JOIN, "Expected JOIN");
            }
            else if (Match(TokenType.FULL))
            {
                join.Type = JoinType.Full;
                Match(TokenType.OUTER); // Optional OUTER
                Consume(TokenType.JOIN, "Expected JOIN");
            }
            else if (Match(TokenType.JOIN))
            {
                join.Type = JoinType.Inner; // Default to INNER
            }

            join.Table = ParseTableReference();

            if (Match(TokenType.ON))
            {
                join.OnCondition = ParseExpression();
            }

            return join;
        }

        private TableReference ParseTableReference()
        {
            var table = new TableReference();
            var firstPart = Consume(TokenType.IDENTIFIER, "Expected table name").Value;

            if (Match(TokenType.DOT))
            {
                table.Schema = firstPart;
                table.TableName = Consume(TokenType.IDENTIFIER, "Expected table name after schema").Value;
            }
            else
            {
                table.TableName = firstPart;
            }

            if (Match(TokenType.AS))
            {
                table.Alias = Consume(TokenType.IDENTIFIER, "Expected alias after AS").Value;
            }
            else if (Check(TokenType.IDENTIFIER))
            {
                table.Alias = Advance().Value;
            }

            return table;
        }

        private Expression ParseExpression()
        {
            return ParseOr();
        }

        private Expression ParseOr()
        {
            var expr = ParseAnd();

            while (Match(TokenType.OR))
            {
                var op = Previous().Type;
                var right = ParseAnd();
                expr = new BinaryExpression { Left = expr, Operator = op, Right = right };
            }

            return expr;
        }

        private Expression ParseAnd()
        {
            var expr = ParseNot();

            while (Match(TokenType.AND))
            {
                var op = Previous().Type;
                var right = ParseNot();
                expr = new BinaryExpression { Left = expr, Operator = op, Right = right };
            }

            return expr;
        }

        private Expression ParseNot()
        {
            if (Match(TokenType.NOT))
            {
                var op = Previous().Type;
                var expr = ParseNot();
                return new UnaryExpression { Operator = op, Operand = expr };
            }

            return ParseComparison();
        }

        private Expression ParseComparison()
        {
            var expr = ParseAddition();

            while (Match(TokenType.EQUALS, TokenType.NOT_EQUALS, TokenType.LESS_THAN,
                         TokenType.GREATER_THAN, TokenType.LESS_THAN_EQUALS, 
                         TokenType.GREATER_THAN_EQUALS, TokenType.LIKE, TokenType.IN))
            {
                var op = Previous().Type;
                
                if (op == TokenType.IN)
                {
                    Consume(TokenType.LEFT_PAREN, "Expected ( after IN");
                    var values = new List<Expression>();
                    
                    do
                    {
                        values.Add(ParseExpression());
                    } while (Match(TokenType.COMMA));
                    
                    Consume(TokenType.RIGHT_PAREN, "Expected ) after IN values");
                    
                    // For simplicity, represent IN as a function call
                    return new FunctionExpression 
                    { 
                        FunctionName = "IN", 
                        Arguments = new List<Expression> { expr }.Concat(values).ToList()
                    };
                }
                
                var right = ParseAddition();
                expr = new BinaryExpression { Left = expr, Operator = op, Right = right };
            }

            return expr;
        }

        private Expression ParseAddition()
        {
            var expr = ParseMultiplication();

            while (Match(TokenType.PLUS, TokenType.MINUS))
            {
                var op = Previous().Type;
                var right = ParseMultiplication();
                expr = new BinaryExpression { Left = expr, Operator = op, Right = right };
            }

            return expr;
        }

        private Expression ParseMultiplication()
        {
            var expr = ParseUnary();

            while (Match(TokenType.STAR, TokenType.DIVIDE, TokenType.MODULO))
            {
                var op = Previous().Type;
                var right = ParseUnary();
                expr = new BinaryExpression { Left = expr, Operator = op, Right = right };
            }

            return expr;
        }

        private Expression ParseUnary()
        {
            if (Match(TokenType.MINUS, TokenType.PLUS))
            {
                var op = Previous().Type;
                var expr = ParseUnary();
                return new UnaryExpression { Operator = op, Operand = expr };
            }

            return ParsePrimary();
        }

        private Expression ParsePrimary()
        {
            // Literals
            if (Match(TokenType.NUMBER_LITERAL))
            {
                return new LiteralExpression 
                { 
                    Value = double.Parse(Previous().Value), 
                    Type = TokenType.NUMBER_LITERAL 
                };
            }

            if (Match(TokenType.STRING_LITERAL))
            {
                return new LiteralExpression 
                { 
                    Value = Previous().Value, 
                    Type = TokenType.STRING_LITERAL 
                };
            }

            if (Match(TokenType.NULL))
            {
                return new LiteralExpression 
                { 
                    Value = null, 
                    Type = TokenType.NULL 
                };
            }

            // CASE expression
            if (Match(TokenType.CASE))
            {
                return ParseCaseExpression();
            }

            // Subquery
            if (Match(TokenType.LEFT_PAREN))
            {
                if (Check(TokenType.SELECT))
                {
                    var subquery = ParseSelectStatement();
                    Consume(TokenType.RIGHT_PAREN, "Expected ) after subquery");
                    return new SubqueryExpression { Query = subquery };
                }
                
                // Grouped expression
                var expr = ParseExpression();
                Consume(TokenType.RIGHT_PAREN, "Expected ) after expression");
                return expr;
            }

            // Column or function
            if (Match(TokenType.IDENTIFIER))
            {
                var name = Previous().Value;
                
                if (Match(TokenType.LEFT_PAREN))
                {
                    // Function call
                    var func = new FunctionExpression { FunctionName = name };
                    
                    if (!Check(TokenType.RIGHT_PAREN))
                    {
                        do
                        {
                            func.Arguments.Add(ParseExpression());
                        } while (Match(TokenType.COMMA));
                    }
                    
                    Consume(TokenType.RIGHT_PAREN, "Expected ) after function arguments");
                    return func;
                }
                else if (Match(TokenType.DOT))
                {
                    // Table.Column
                    var columnName = Consume(TokenType.IDENTIFIER, "Expected column name").Value;
                    return new ColumnExpression { TableAlias = name, ColumnName = columnName };
                }
                else
                {
                    // Simple column
                    return new ColumnExpression { ColumnName = name };
                }
            }

            if (Match(TokenType.STAR))
            {
                return new ColumnExpression { ColumnName = "*" };
            }

            throw new Exception($"Unexpected token: {Peek()}");
        }

        private CaseExpression ParseCaseExpression()
        {
            var caseExpr = new CaseExpression();

            while (Match(TokenType.WHEN))
            {
                var when = new WhenClause
                {
                    Condition = ParseExpression()
                };
                
                Consume(TokenType.THEN, "Expected THEN after WHEN condition");
                when.Result = ParseExpression();
                
                caseExpr.WhenClauses.Add(when);
            }

            if (Match(TokenType.ELSE))
            {
                caseExpr.ElseExpression = ParseExpression();
            }

            Consume(TokenType.END, "Expected END to close CASE expression");
            
            return caseExpr;
        }

        private List<Expression> ParseExpressionList()
        {
            var expressions = new List<Expression>();
            
            do
            {
                expressions.Add(ParseExpression());
            } while (Match(TokenType.COMMA));
            
            return expressions;
        }

        private List<OrderByItem> ParseOrderByList()
        {
            var items = new List<OrderByItem>();
            
            do
            {
                var item = new OrderByItem { Expression = ParseExpression() };
                
                if (Match(TokenType.ASC))
                {
                    item.IsAscending = true;
                }
                else if (Match(TokenType.DESC))
                {
                    item.IsAscending = false;
                }
                
                items.Add(item);
            } while (Match(TokenType.COMMA));
            
            return items;
        }

        // Helper methods
        private bool Match(params TokenType[] types)
        {
            foreach (var type in types)
            {
                if (Check(type))
                {
                    Advance();
                    return true;
                }
            }
            return false;
        }

        private bool Check(TokenType type)
        {
            if (IsAtEnd()) return false;
            return Peek().Type == type;
        }

        private Token Advance()
        {
            if (!IsAtEnd()) _current++;
            return Previous();
        }

        private bool IsAtEnd()
        {
            return Peek().Type == TokenType.EOF;
        }

        private Token Peek()
        {
            return _tokens[_current];
        }

        private Token Previous()
        {
            return _tokens[_current - 1];
        }

        private Token Consume(TokenType type, string message)
        {
            if (Check(type)) return Advance();
            throw new Exception($"{message} at position {Peek().Position}");
        }
    }

    // Example usage and test
    class Program
    {
        static void Main(string[] args)
        {
            // Test queries
            var testQueries = new[]
            {
                @"SELECT * FROM users",
                
                @"SELECT u.id, u.name, p.title 
                  FROM users u 
                  INNER JOIN posts p ON u.id = p.user_id 
                  WHERE u.active = 1",
                
                @"WITH user_posts AS (
                    SELECT user_id, COUNT(*) as post_count 
                    FROM posts 
                    GROUP BY user_id
                  )
                  SELECT u.name, up.post_count 
                  FROM users u 
                  LEFT JOIN user_posts up ON u.id = up.user_id
                  ORDER BY up.post_count DESC
                  LIMIT 10",
                
                @"SELECT 
                    CASE 
                        WHEN age < 18 THEN 'Minor'
                        WHEN age >= 65 THEN 'Senior'
                        ELSE 'Adult'
                    END as age_group,
                    COUNT(*) as count
                  FROM users
                  GROUP BY age_group
                  HAVING count > 10"
            };

            foreach (var query in testQueries)
            {
                Console.WriteLine($"\nParsing query:\n{query}\n");
                
                try
                {
                    var lexer = new Lexer(query);
                    var tokens = lexer.Tokenize();
                    
                    Console.WriteLine("Tokens:");
                    foreach (var token in tokens.Where(t => t.Type != TokenType.EOF))
                    {
                        Console.WriteLine($"  {token}");
                    }
                    
                    var parser = new SqlParser(tokens);
                    var ast = parser.Parse();
                    
                    Console.WriteLine("\nParse successful!");
                    Console.WriteLine($"Select items: {ast.SelectList.Count}");
                    if (ast.From != null)
                        Console.WriteLine($"From table: {ast.From.Table.TableName}");
                    Console.WriteLine($"Joins: {ast.From?.Joins.Count ?? 0}");
                    Console.WriteLine($"CTEs: {ast.CTEs.Count}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Parse error: {ex.Message}");
                }
                
                Console.WriteLine(new string('-', 50));
            }
        }
    }
}