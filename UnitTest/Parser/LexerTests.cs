// -----------------------------------------------------------------------
// <copyright file="LexerTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Parser
{
    using System.Linq;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Parser;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

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
            Assert.AreEqual(SqlTokenType.SELECT, tokens[0].Type);
            Assert.AreEqual(SqlTokenType.STAR, tokens[1].Type);
            Assert.AreEqual(SqlTokenType.FROM, tokens[2].Type);
            Assert.AreEqual(SqlTokenType.IDENTIFIER, tokens[3].Type);
            Assert.AreEqual("users", tokens[3].Value);
            Assert.AreEqual(SqlTokenType.EOF, tokens[4].Type);
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
            var stringToken = tokens.FirstOrDefault(t => t.Type == SqlTokenType.STRING_LITERAL);
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
            var numberToken = tokens.FirstOrDefault(t => t.Type == SqlTokenType.NUMBER_LITERAL);
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
            Assert.IsTrue(tokens.Any(t => t.Type == SqlTokenType.EQUALS));
            Assert.IsTrue(tokens.Any(t => t.Type == SqlTokenType.AND));
            Assert.IsTrue(tokens.Any(t => t.Type == SqlTokenType.NOT_EQUALS));
            Assert.IsTrue(tokens.Any(t => t.Type == SqlTokenType.OR));
            Assert.IsTrue(tokens.Any(t => t.Type == SqlTokenType.GREATER_THAN_EQUALS));
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
            var identifiers = tokens.Where(t => t.Type == SqlTokenType.IDENTIFIER).ToList();
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
            Assert.AreEqual(SqlTokenType.SELECT, tokens[0].Type);
            Assert.AreEqual(SqlTokenType.FROM, tokens[2].Type);
            Assert.AreEqual(SqlTokenType.WHERE, tokens[4].Type);
        }
    }
}
