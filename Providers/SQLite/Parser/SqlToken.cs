//-------------------------------------------------------------------------------
// <copyright file="SqlToken.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Parser
{
    /// <summary>
    /// Represents a lexical token identified during SQL parsing.
    /// A token is a sequence of characters that represents a single logical element
    /// in an SQL statement, such as keywords, identifiers, operators, or literals.
    /// </summary>
    public class SqlToken
    {
        #region Properties

        /// <summary>
        /// Gets the type of this token, indicating its lexical category.
        /// This determines how the token should be interpreted by the parser.
        /// </summary>
        /// <value>
        /// A <see cref="SqlTokenType"/> value that categorizes this token
        /// (e.g., keyword, identifier, operator, literal, etc.).
        /// </value>
        public SqlTokenType Type { get; }

        /// <summary>
        /// Gets the actual text content of this token as it appears in the SQL source.
        /// This is the literal string representation that was tokenized.
        /// </summary>
        /// <value>
        /// The string value of the token. For keywords, this would be the keyword text.
        /// For identifiers, this would be the identifier name. For literals, this would
        /// be the literal value including any quotes or delimiters.
        /// </value>
        public string Value { get; }

        /// <summary>
        /// Gets the zero-based character position where this token begins in the original SQL text.
        /// This is useful for error reporting, syntax highlighting, and debugging purposes.
        /// </summary>
        /// <value>
        /// A zero-based integer representing the starting character position of this token
        /// in the source SQL string.
        /// </value>
        public int Position { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlToken"/> class.
        /// </summary>
        /// <param name="type">
        /// The type of the token, indicating its lexical category.
        /// </param>
        /// <param name="value">
        /// The actual text content of the token as it appears in the SQL source.
        /// Cannot be null, but can be an empty string for certain token types.
        /// </param>
        /// <param name="position">
        /// The zero-based character position where this token begins in the original SQL text.
        /// Must be non-negative.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="value"/> is null.
        /// </exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when <paramref name="position"/> is negative.
        /// </exception>
        public SqlToken(SqlTokenType type, string value, int position)
        {
            this.Type = type;
            this.Value = value ?? throw new System.ArgumentNullException(nameof(value));
            this.Position = position >= 0 ? position : throw new System.ArgumentOutOfRangeException(nameof(position), "Position must be non-negative");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns a string representation of this token for debugging and logging purposes.
        /// The format is "TokenType: TokenValue" which provides a clear indication of both
        /// the token's category and its actual content.
        /// </summary>
        /// <returns>
        /// A string in the format "TokenType: TokenValue", for example:
        /// - "SELECT: SELECT"
        /// - "IDENTIFIER: CustomerName"
        /// - "STRING_LITERAL: 'Hello World'"
        /// - "NUMBER_LITERAL: 123.45"
        /// </returns>
        /// <example>
        /// <code>
        /// var token = new SqlToken(SqlTokenType.IDENTIFIER, "CustomerName", 10);
        /// Console.WriteLine(token.ToString()); // Output: "IDENTIFIER: CustomerName"
        /// </code>
        /// </example>
        public override string ToString() => $"{this.Type}: {this.Value}";

        #endregion
    }
}
