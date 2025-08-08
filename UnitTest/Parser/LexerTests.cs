// -----------------------------------------------------------------------
// <copyright file="LexerTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Parser
{
    using System.Linq;
    using FluentAssertions;
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
            tokens.Count.Should().Be(5); // SELECT, *, FROM, users, EOF
            tokens[0].Type.Should().Be(SqlTokenType.SELECT);
            tokens[1].Type.Should().Be(SqlTokenType.STAR);
            tokens[2].Type.Should().Be(SqlTokenType.FROM);
            tokens[3].Type.Should().Be(SqlTokenType.IDENTIFIER);
            tokens[3].Value.Should().Be("users");
            tokens[4].Type.Should().Be(SqlTokenType.EOF);
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
            stringToken.Should().NotBeNull();
            stringToken.Value.Should().Be("John Doe");
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
            numberToken.Should().NotBeNull();
            numberToken.Value.Should().Be("25.5");
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
            tokens.Should().Contain(t => t.Type == SqlTokenType.EQUALS);
            tokens.Should().Contain(t => t.Type == SqlTokenType.AND);
            tokens.Should().Contain(t => t.Type == SqlTokenType.NOT_EQUALS);
            tokens.Should().Contain(t => t.Type == SqlTokenType.OR);
            tokens.Should().Contain(t => t.Type == SqlTokenType.GREATER_THAN_EQUALS);
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
            identifiers.Count.Should().Be(2);
            identifiers[0].Value.Should().Be("First Name");
            identifiers[1].Value.Should().Be("User Table");
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
            tokens[0].Type.Should().Be(SqlTokenType.SELECT);
            tokens[2].Type.Should().Be(SqlTokenType.FROM);
            tokens[4].Type.Should().Be(SqlTokenType.WHERE);
        }
    }
}
