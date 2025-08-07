// -----------------------------------------------------------------------
// <copyright file="DmlParserTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Parser
{
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
            Assert.IsInstanceOfType(node, typeof(CreateTableStatement));
            var stmt = (CreateTableStatement)node;
            Assert.AreEqual("users", stmt.TableName);
            Assert.AreEqual(2, stmt.Columns.Count);
            Assert.AreEqual("id", stmt.Columns[0].Name);
            Assert.AreEqual("INT", stmt.Columns[0].DataType);
        }

        [TestMethod]
        public void TestCreateIndex()
        {
            var sql = "CREATE INDEX idx_users_name ON users(name)";
            var node = this.ParseStatement(sql);
            Assert.IsInstanceOfType(node, typeof(CreateIndexStatement));
            var stmt = (CreateIndexStatement)node;
            Assert.AreEqual("idx_users_name", stmt.IndexName);
            Assert.AreEqual("users", stmt.TableName);
            Assert.AreEqual(1, stmt.Columns.Count);
            Assert.AreEqual("name", stmt.Columns[0]);
        }

        [TestMethod]
        public void TestCreateTableWithPrimaryKeyConstraint()
        {
            var sql = "CREATE TABLE users (id INT, name TEXT, CONSTRAINT pk_users PRIMARY KEY (id))";
            var node = this.ParseStatement(sql);
            Assert.IsInstanceOfType(node, typeof(CreateTableStatement));
            var stmt = (CreateTableStatement)node;
            Assert.AreEqual(1, stmt.Constraints.Count);
            var constraint = stmt.Constraints[0];
            Assert.AreEqual(ConstraintType.PrimaryKey, constraint.Type);
            CollectionAssert.AreEqual(new[] { "id" }, constraint.Columns);
        }
    }
}
