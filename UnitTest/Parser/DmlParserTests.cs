// -----------------------------------------------------------------------
// <copyright file="DmlParserTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Parser
{
    using FluentAssertions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Parser;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DmlParserTests
    {
        private SqlNode ParseStatement(string sql)
        {
            var lexer = new Lexer(sql);
            var tokens = lexer.Tokenize();
            var parser = new SqlParser(tokens);
            return parser.Parse();
        }

        [TestMethod]
        public void TestCreateTable()
        {
            var sql = "CREATE TABLE users (id INT, name TEXT)";
            var node = this.ParseStatement(sql);
            node.Should().BeOfType<CreateTableStatement>();
            var stmt = (CreateTableStatement)node;
            stmt.TableName.Should().Be("users");
            stmt.Columns.Count.Should().Be(2);
            stmt.Columns[0].Name.Should().Be("id");
            stmt.Columns[0].DataType.Should().Be("INT");
        }

        [TestMethod]
        public void TestCreateIndex()
        {
            var sql = "CREATE INDEX idx_users_name ON users(name)";
            var node = this.ParseStatement(sql);
            node.Should().BeOfType<CreateIndexStatement>();
            var stmt = (CreateIndexStatement)node;
            stmt.IndexName.Should().Be("idx_users_name");
            stmt.TableName.Should().Be("users");
            stmt.Columns.Count.Should().Be(1);
            stmt.Columns[0].Should().Be("name");
        }

        [TestMethod]
        public void TestCreateTableWithPrimaryKeyConstraint()
        {
            var sql = "CREATE TABLE users (id INT, name TEXT, CONSTRAINT pk_users PRIMARY KEY (id))";
            var node = this.ParseStatement(sql);
            node.Should().BeOfType<CreateTableStatement>();
            var stmt = (CreateTableStatement)node;
            stmt.Constraints.Count.Should().Be(1);
            var constraint = stmt.Constraints[0];
            constraint.Type.Should().Be(ConstraintType.PrimaryKey);
            constraint.Columns.Should().BeEquivalentTo(new[] { "id" });
        }

        [TestMethod]
        public void TestCreateTableIfNotExistsWithCompositePrimaryKey()
        {
            var sql = "CREATE TABLE IF NOT EXISTS TestEntity (" +
                      "IsDeleted BIT, " +
                      "Id UNIQUEIDENTIFIER NOT NULL, " +
                      "Name NVARCHAR(255), " +
                      "Count INT, " +
                      "CreatedDate DATETIME2, " +
                      "Amount DECIMAL(18,2), " +
                      "Version BIGINT NOT NULL, " +
                      "CreatedTime TEXT NOT NULL, " +
                      "LastWriteTime DATETIME NOT NULL, " +
                      "PRIMARY KEY (Id, Version)" +
                      ")";

            var node = this.ParseStatement(sql);
            node.Should().BeOfType<CreateTableStatement>();
            var stmt = (CreateTableStatement)node;
            stmt.TableName.Should().Be("TestEntity");
            stmt.Columns.Count.Should().Be(9);
            stmt.Constraints.Count.Should().Be(1);
            var pk = stmt.Constraints[0];
            pk.Type.Should().Be(ConstraintType.PrimaryKey);
            pk.Columns.Should().BeEquivalentTo(new[] { "Id", "Version" });
        }
    }
}
