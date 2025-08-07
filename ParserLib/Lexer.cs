//-------------------------------------------------------------------------------
// <copyright file="Lexer.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// A lexical analyzer (lexer) for SQL statements that converts input text into a sequence of tokens.
    /// The lexer performs tokenization by scanning the input character by character and identifying
    /// keywords, identifiers, operators, literals, and delimiters.
    /// </summary>
    public class Lexer
    {
        #region Private Fields

        /// <summary>
        /// The input SQL text to be tokenized.
        /// </summary>
        private readonly string input;

        /// <summary>
        /// The current position in the input text being processed.
        /// </summary>
        private int position;

        /// <summary>
        /// Dictionary mapping SQL keywords to their corresponding token types.
        /// Uses case-insensitive comparison for keyword matching.
        /// </summary>
        private readonly Dictionary<string, SqlTokenType> keywords;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="Lexer"/> class.
        /// </summary>
        /// <param name="input">
        /// The SQL input text to be tokenized. Cannot be null.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="input"/> is null.
        /// </exception>
        public Lexer(string input)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.position = 0;
            this.keywords = this.InitializeKeywords();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Tokenizes the input SQL text and returns a list of tokens.
        /// The tokenization process scans the input from left to right, identifying
        /// and categorizing each lexical element.
        /// </summary>
        /// <returns>
        /// A list of <see cref="SqlToken"/> objects representing the tokenized input.
        /// The last token in the list is always an EOF (End of File) token.
        /// </returns>
        public List<SqlToken> Tokenize()
        {
            var tokens = new List<SqlToken>();

            while (this.position < this.input.Length)
            {
                this.SkipWhitespace();
                if (this.position >= this.input.Length)
                {
                    break;
                }

                var sqlToken = this.NextToken();
                if (sqlToken.Type != SqlTokenType.UNKNOWN)
                {
                    tokens.Add(sqlToken);
                }
            }

            tokens.Add(new SqlToken(SqlTokenType.EOF, string.Empty, this.position));
            return tokens;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Initializes the dictionary of SQL keywords and their corresponding token types.
        /// Uses case-insensitive string comparison for keyword matching.
        /// </summary>
        /// <returns>
        /// A dictionary mapping keyword strings to their token types.
        /// </returns>
        private Dictionary<string, SqlTokenType> InitializeKeywords()
        {
            return new Dictionary<string, SqlTokenType>(StringComparer.OrdinalIgnoreCase)
            {
                ["SELECT"] = SqlTokenType.SELECT,
                ["FROM"] = SqlTokenType.FROM,
                ["WHERE"] = SqlTokenType.WHERE,
                ["JOIN"] = SqlTokenType.JOIN,
                ["INNER"] = SqlTokenType.INNER,
                ["LEFT"] = SqlTokenType.LEFT,
                ["RIGHT"] = SqlTokenType.RIGHT,
                ["FULL"] = SqlTokenType.FULL,
                ["CROSS"] = SqlTokenType.CROSS,
                ["OUTER"] = SqlTokenType.OUTER,
                ["ON"] = SqlTokenType.ON,
                ["WITH"] = SqlTokenType.WITH,
                ["AS"] = SqlTokenType.AS,
                ["RECURSIVE"] = SqlTokenType.RECURSIVE,
                ["UNION"] = SqlTokenType.UNION,
                ["ALL"] = SqlTokenType.ALL,
                ["DISTINCT"] = SqlTokenType.DISTINCT,
                ["GROUP"] = SqlTokenType.GROUP,
                ["BY"] = SqlTokenType.BY,
                ["HAVING"] = SqlTokenType.HAVING,
                ["ORDER"] = SqlTokenType.ORDER,
                ["ASC"] = SqlTokenType.ASC,
                ["DESC"] = SqlTokenType.DESC,
                ["LIMIT"] = SqlTokenType.LIMIT,
                ["OFFSET"] = SqlTokenType.OFFSET,
                ["AND"] = SqlTokenType.AND,
                ["OR"] = SqlTokenType.OR,
                ["NOT"] = SqlTokenType.NOT,
                ["IN"] = SqlTokenType.IN,
                ["BETWEEN"] = SqlTokenType.BETWEEN,
                ["LIKE"] = SqlTokenType.LIKE,
                ["IS"] = SqlTokenType.IS,
                ["NULL"] = SqlTokenType.NULL,
                ["EXISTS"] = SqlTokenType.EXISTS,
                ["CASE"] = SqlTokenType.CASE,
                ["WHEN"] = SqlTokenType.WHEN,
                ["THEN"] = SqlTokenType.THEN,
                ["ELSE"] = SqlTokenType.ELSE,
                ["END"] = SqlTokenType.END,
            };
        }

        /// <summary>
        /// Skips whitespace characters (spaces, tabs, newlines, etc.) in the input.
        /// Advances the position until a non-whitespace character is found or the end of input is reached.
        /// </summary>
        private void SkipWhitespace()
        {
            while (this.position < this.input.Length && char.IsWhiteSpace(this.input[this.position]))
            {
                this.position++;
            }
        }

        /// <summary>
        /// Reads and returns the next token from the input.
        /// Determines the token type based on the current character and reads the appropriate
        /// token value (single character, multi-character operator, string literal, number, or identifier).
        /// </summary>
        /// <returns>
        /// A <see cref="SqlToken"/> representing the next lexical element in the input.
        /// </returns>
        private SqlToken NextToken()
        {
            var startPos = this.position;
            var ch = this.input[this.position];

            // Single character tokens
            switch (ch)
            {
                case '(':
                    this.position++;
                    return new SqlToken(SqlTokenType.LEFT_PAREN, "(", startPos);
                case ')':
                    this.position++;
                    return new SqlToken(SqlTokenType.RIGHT_PAREN, ")", startPos);
                case ',':
                    this.position++;
                    return new SqlToken(SqlTokenType.COMMA, ",", startPos);
                case ';':
                    this.position++;
                    return new SqlToken(SqlTokenType.SEMICOLON, ";", startPos);
                case '.':
                    this.position++;
                    return new SqlToken(SqlTokenType.DOT, ".", startPos);
                case '*':
                    this.position++;
                    return new SqlToken(SqlTokenType.STAR, "*", startPos);
                case '+':
                    this.position++;
                    return new SqlToken(SqlTokenType.PLUS, "+", startPos);
                case '-':
                    this.position++;
                    return new SqlToken(SqlTokenType.MINUS, "-", startPos);
                case '/':
                    this.position++;
                    return new SqlToken(SqlTokenType.DIVIDE, "/", startPos);
                case '%':
                    this.position++;
                    return new SqlToken(SqlTokenType.MODULO, "%", startPos);
            }

            // Multi-character operators
            if (ch == '=')
            {
                this.position++;
                return new SqlToken(SqlTokenType.EQUALS, "=", startPos);
            }
            else if (ch == '<')
            {
                this.position++;
                if (this.position < this.input.Length && this.input[this.position] == '=')
                {
                    this.position++;
                    return new SqlToken(SqlTokenType.LESS_THAN_EQUALS, "<=", startPos);
                }
                else if (this.position < this.input.Length && this.input[this.position] == '>')
                {
                    this.position++;
                    return new SqlToken(SqlTokenType.NOT_EQUALS, "<>", startPos);
                }

                return new SqlToken(SqlTokenType.LESS_THAN, "<", startPos);
            }
            else if (ch == '>')
            {
                this.position++;
                if (this.position < this.input.Length && this.input[this.position] == '=')
                {
                    this.position++;
                    return new SqlToken(SqlTokenType.GREATER_THAN_EQUALS, ">=", startPos);
                }

                return new SqlToken(SqlTokenType.GREATER_THAN, ">", startPos);
            }
            else if (ch == '!')
            {
                this.position++;
                if (this.position < this.input.Length && this.input[this.position] == '=')
                {
                    this.position++;
                    return new SqlToken(SqlTokenType.NOT_EQUALS, "!=", startPos);
                }
            }

            // String literals
            if (ch == '\'' || ch == '"')
            {
                return this.ReadStringLiteral(ch);
            }

            // Numbers
            if (char.IsDigit(ch))
            {
                return this.ReadNumber();
            }

            // Identifiers and keywords
            if (char.IsLetter(ch) || ch == '_' || ch == '[')
            {
                return this.ReadIdentifierOrKeyword();
            }

            // Unknown character
            this.position++;
            return new SqlToken(SqlTokenType.UNKNOWN, ch.ToString(), startPos);
        }

        /// <summary>
        /// Reads a string literal token enclosed in quotes.
        /// Handles escape sequences and returns the string content without the enclosing quotes.
        /// </summary>
        /// <param name="quoteChar">
        /// The quote character that delimits the string (single or double quote).
        /// </param>
        /// <returns>
        /// A <see cref="SqlToken"/> of type STRING_LITERAL containing the string content.
        /// </returns>
        private SqlToken ReadStringLiteral(char quoteChar)
        {
            var startPos = this.position;
            var sb = new StringBuilder();
            this.position++; // Skip opening quote

            while (this.position < this.input.Length && this.input[this.position] != quoteChar)
            {
                if (this.input[this.position] == '\\' && this.position + 1 < this.input.Length)
                {
                    this.position++;
                    sb.Append(this.input[this.position]);
                }
                else
                {
                    sb.Append(this.input[this.position]);
                }

                this.position++;
            }

            if (this.position < this.input.Length)
            {
                this.position++; // Skip closing quote
            }

            return new SqlToken(SqlTokenType.STRING_LITERAL, sb.ToString(), startPos);
        }

        /// <summary>
        /// Reads a numeric literal token.
        /// Supports both integer and decimal numbers.
        /// </summary>
        /// <returns>
        /// A <see cref="SqlToken"/> of type NUMBER_LITERAL containing the numeric value as a string.
        /// </returns>
        private SqlToken ReadNumber()
        {
            var startPos = this.position;
            var sb = new StringBuilder();

            while (this.position < this.input.Length && (char.IsDigit(this.input[this.position]) || this.input[this.position] == '.'))
            {
                sb.Append(this.input[this.position]);
                this.position++;
            }

            return new SqlToken(SqlTokenType.NUMBER_LITERAL, sb.ToString(), startPos);
        }

        /// <summary>
        /// Reads an identifier or keyword token.
        /// Handles both regular identifiers and bracketed identifiers.
        /// If the read text matches a keyword, returns the appropriate keyword token type.
        /// </summary>
        /// <returns>
        /// A <see cref="SqlToken"/> that is either a keyword token or an IDENTIFIER token.
        /// </returns>
        private SqlToken ReadIdentifierOrKeyword()
        {
            var startPos = this.position;
            var sb = new StringBuilder();

            // Handle bracketed identifiers [ColumnName]
            if (this.input[this.position] == '[')
            {
                this.position++;
                while (this.position < this.input.Length && this.input[this.position] != ']')
                {
                    sb.Append(this.input[this.position]);
                    this.position++;
                }

                if (this.position < this.input.Length)
                {
                    this.position++; // Skip closing bracket
                }

                return new SqlToken(SqlTokenType.IDENTIFIER, sb.ToString(), startPos);
            }

            // Regular identifiers
            while (this.position < this.input.Length &&
                   (char.IsLetterOrDigit(this.input[this.position]) || this.input[this.position] == '_'))
            {
                sb.Append(this.input[this.position]);
                this.position++;
            }

            var value = sb.ToString();
            if (this.keywords.TryGetValue(value, out var keywordType))
            {
                return new SqlToken(keywordType, value, startPos);
            }

            return new SqlToken(SqlTokenType.IDENTIFIER, value, startPos);
        }

        #endregion
    }
}
