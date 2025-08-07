//-------------------------------------------------------------------------------
// <copyright file="SqlTokenType.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

// ReSharper disable InconsistentNaming
namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Parser
{
    /// <summary>
    /// Represents the different types of tokens that can be encountered in SQL parsing.
    /// Used by the SQL parser to categorize lexical elements during tokenization.
    /// </summary>
    public enum SqlTokenType
    {
        #region Keywords - Query Operations

        /// <summary>
        /// SELECT keyword - used to specify columns to retrieve.
        /// </summary>
        SELECT,

        /// <summary>
        /// FROM keyword - specifies the source table or tables.
        /// </summary>
        FROM,

        /// <summary>
        /// WHERE keyword - introduces conditional filtering.
        /// </summary>
        WHERE,

        /// <summary>
        /// GROUP keyword - used with BY for grouping results.
        /// </summary>
        GROUP,

        /// <summary>
        /// BY keyword - used with GROUP BY and ORDER BY clauses.
        /// </summary>
        BY,

        /// <summary>
        /// HAVING keyword - filters grouped results.
        /// </summary>
        HAVING,

        /// <summary>
        /// ORDER keyword - used with BY for sorting results.
        /// </summary>
        ORDER,

        /// <summary>
        /// ASC keyword - ascending sort order.
        /// </summary>
        ASC,

        /// <summary>
        /// DESC keyword - descending sort order.
        /// </summary>
        DESC,

        /// <summary>
        /// LIMIT keyword - limits the number of returned rows.
        /// </summary>
        LIMIT,

        /// <summary>
        /// OFFSET keyword - skips a specified number of rows.
        /// </summary>
        OFFSET,

        /// <summary>
        /// DISTINCT keyword - eliminates duplicate rows.
        /// </summary>
        DISTINCT,

        /// <summary>
        /// ALL keyword - includes all rows (opposite of DISTINCT).
        /// </summary>
        ALL,

        #endregion

        #region Keywords - Join Operations

        /// <summary>
        /// JOIN keyword - combines rows from multiple tables.
        /// </summary>
        JOIN,

        /// <summary>
        /// INNER keyword - used in INNER JOIN operations.
        /// </summary>
        INNER,

        /// <summary>
        /// LEFT keyword - used in LEFT JOIN operations.
        /// </summary>
        LEFT,

        /// <summary>
        /// RIGHT keyword - used in RIGHT JOIN operations.
        /// </summary>
        RIGHT,

        /// <summary>
        /// FULL keyword - used in FULL OUTER JOIN operations.
        /// </summary>
        FULL,

        /// <summary>
        /// Full Outer keyword - used in FULL OUTER JOIN operations.
        /// </summary>
        CROSS,

        /// <summary>
        /// OUTER keyword - used in OUTER JOIN operations.
        /// </summary>
        OUTER,

        /// <summary>
        /// ON keyword - specifies join conditions.
        /// </summary>
        ON,

        #endregion

        #region Keywords - Set Operations

        /// <summary>
        /// UNION keyword - combines results from multiple SELECT statements.
        /// </summary>
        UNION,

        /// <summary>
        /// WITH keyword - defines common table expressions (CTEs).
        /// </summary>
        WITH,

        /// <summary>
        /// RECURSIVE keyword - used with recursive CTEs.
        /// </summary>
        RECURSIVE,

        /// <summary>
        /// AS keyword - creates aliases for tables or columns.
        /// </summary>
        AS,

        #endregion

        #region Keywords - Data Modification

        /// <summary>
        /// INSERT keyword - adds new rows to a table.
        /// </summary>
        INSERT,

        /// <summary>
        /// INTO keyword - specifies the target table for INSERT operations.
        /// </summary>
        INTO,

        /// <summary>
        /// VALUES keyword - specifies the values to insert.
        /// </summary>
        VALUES,

        /// <summary>
        /// UPDATE keyword - modifies existing rows in a table.
        /// </summary>
        UPDATE,

        /// <summary>
        /// SET keyword - specifies column assignments in UPDATE statements.
        /// </summary>
        SET,

        /// <summary>
        /// DELETE keyword - removes rows from a table.
        /// </summary>
        DELETE,

        #endregion

        #region Keywords - Schema Operations

        /// <summary>
        /// CREATE keyword - creates database objects (tables, indexes, etc.).
        /// </summary>
        CREATE,

        /// <summary>
        /// TABLE keyword - specifies table operations.
        /// </summary>
        TABLE,

        /// <summary>
        /// INDEX keyword - creates indexes.
        /// </summary>
        INDEX,

        /// <summary>
        /// CONSTRAINT keyword - defines table constraints.
        /// </summary>
        CONSTRAINT,

        /// <summary>
        /// PRIMARY keyword - used with KEY in constraints.
        /// </summary>
        PRIMARY,

        /// <summary>
        /// KEY keyword - used in PRIMARY KEY and FOREIGN KEY declarations.
        /// </summary>
        KEY,

        /// <summary>
        /// FOREIGN keyword - defines foreign key constraints.
        /// </summary>
        FOREIGN,

        /// <summary>
        /// UNIQUE keyword - defines unique constraints or indexes.
        /// </summary>
        UNIQUE,

        /// <summary>
        /// REFERENCES keyword - specifies referenced table in foreign key constraints.
        /// </summary>
        REFERENCES,

        /// <summary>
        /// ALTER keyword - modifies existing database objects.
        /// </summary>
        ALTER,

        /// <summary>
        /// DROP keyword - removes database objects.
        /// </summary>
        DROP,

        #endregion

        #region Keywords - Logical Operators

        /// <summary>
        /// AND keyword - logical AND operator.
        /// </summary>
        AND,

        /// <summary>
        /// OR keyword - logical OR operator.
        /// </summary>
        OR,

        /// <summary>
        /// NOT keyword - logical NOT operator.
        /// </summary>
        NOT,

        #endregion

        #region Keywords - Comparison and Pattern Matching

        /// <summary>
        /// IN keyword - tests if a value exists in a list or subquery.
        /// </summary>
        IN,

        /// <summary>
        /// BETWEEN keyword - tests if a value is within a range.
        /// </summary>
        BETWEEN,

        /// <summary>
        /// LIKE keyword - pattern matching with wildcards.
        /// </summary>
        LIKE,

        /// <summary>
        /// IS keyword - used for NULL comparisons and boolean tests.
        /// </summary>
        IS,

        /// <summary>
        /// NULL keyword - represents null values.
        /// </summary>
        NULL,

        /// <summary>
        /// EXISTS keyword - tests for the existence of rows in a subquery.
        /// </summary>
        EXISTS,

        /// <summary>
        /// IF keyword - used in conditional clauses such as IF NOT EXISTS.
        /// </summary>
        IF,

        #endregion

        #region Keywords - Conditional Expressions

        /// <summary>
        /// CASE keyword - begins a conditional expression.
        /// </summary>
        CASE,

        /// <summary>
        /// WHEN keyword - specifies conditions in CASE expressions.
        /// </summary>
        WHEN,

        /// <summary>
        /// THEN keyword - specifies result values in CASE expressions.
        /// </summary>
        THEN,

        /// <summary>
        /// ELSE keyword - specifies default value in CASE expressions.
        /// </summary>
        ELSE,

        /// <summary>
        /// END keyword - terminates CASE expressions.
        /// </summary>
        END,

        #endregion

        #region Identifiers and Literals

        /// <summary>
        /// User-defined identifier (table names, column names, aliases, etc.).
        /// </summary>
        IDENTIFIER,

        /// <summary>
        /// String literal value enclosed in quotes.
        /// </summary>
        STRING_LITERAL,

        /// <summary>
        /// Numeric literal value (integer or decimal).
        /// </summary>
        NUMBER_LITERAL,

        #endregion

        #region Comparison Operators

        /// <summary>
        /// Equals operator (=).
        /// </summary>
        EQUALS,

        /// <summary>
        /// Not equals operator (!= or &lt;&gt;).
        /// </summary>
        NOT_EQUALS,

        /// <summary>
        /// Less than operator (&lt;).
        /// </summary>
        LESS_THAN,

        /// <summary>
        /// Greater than operator (&gt;).
        /// </summary>
        GREATER_THAN,

        /// <summary>
        /// Less than or equals operator (&lt;=).
        /// </summary>
        LESS_THAN_EQUALS,

        /// <summary>
        /// Greater than or equals operator (&gt;=).
        /// </summary>
        GREATER_THAN_EQUALS,

        #endregion

        #region Arithmetic Operators

        /// <summary>
        /// Addition operator (+).
        /// </summary>
        PLUS,

        /// <summary>
        /// Subtraction operator (-).
        /// </summary>
        MINUS,

        /// <summary>
        /// Multiplication operator (*).
        /// </summary>
        MULTIPLY,

        /// <summary>
        /// Division operator (/).
        /// </summary>
        DIVIDE,

        /// <summary>
        /// Modulo operator (%).
        /// </summary>
        MODULO,

        #endregion

        #region Delimiters and Punctuation

        /// <summary>
        /// Left parenthesis (.
        /// </summary>
        LEFT_PAREN,

        /// <summary>
        /// Right parenthesis ).
        /// </summary>
        RIGHT_PAREN,

        /// <summary>
        /// Comma separator (,).
        /// </summary>
        COMMA,

        /// <summary>
        /// Semicolon statement terminator (;).
        /// </summary>
        SEMICOLON,

        /// <summary>
        /// Dot/period for qualified names (.).
        /// </summary>
        DOT,

        /// <summary>
        /// Asterisk for wildcard selections (*).
        /// </summary>
        STAR,

        #endregion

        #region Special Tokens

        /// <summary>
        /// End of file marker - indicates no more tokens to process.
        /// </summary>
        EOF,

        /// <summary>
        /// Unknown or unrecognized token type.
        /// </summary>
        UNKNOWN

        #endregion
    }
}