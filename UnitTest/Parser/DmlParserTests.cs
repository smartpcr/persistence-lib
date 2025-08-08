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
            Assert.IsInstanceOfType(node, typeof(CreateTableStatement));
            var stmt = (CreateTableStatement)node;
            Assert.AreEqual("TestEntity", stmt.TableName);
            Assert.AreEqual(9, stmt.Columns.Count);
            Assert.AreEqual(1, stmt.Constraints.Count);
            var pk = stmt.Constraints[0];
            Assert.AreEqual(ConstraintType.PrimaryKey, pk.Type);
            CollectionAssert.AreEqual(new[] { "Id", "Version" }, pk.Columns);
        }

        [TestMethod]
        public void TestInsertStatement()
        {
            var sql = "INSERT INTO TestEntity (Name, Count, CreatedDate, Amount, ComplexData, CacheKey, Version, CreatedTime, LastWriteTime) " +
                      "VALUES (@Name, @Count, @CreatedDate, @Amount, @ComplexData, @CacheKey, @Version, @CreatedTime, @LastWriteTime)";

            var node = this.ParseStatement(sql);
            Assert.IsInstanceOfType(node, typeof(InsertStatement));
            var stmt = (InsertStatement)node;
            Assert.AreEqual("TestEntity", stmt.TableName);
            CollectionAssert.AreEqual(
                new[] { "Name", "Count", "CreatedDate", "Amount", "ComplexData", "CacheKey", "Version", "CreatedTime", "LastWriteTime" },
                stmt.Columns);
            Assert.AreEqual(9, stmt.Values.Count);
        }

        [TestMethod]
        public void TestUpdateStatement()
        {
            var sql = "UPDATE users SET name = 'Alice', age = 30 WHERE id = 1";
            var node = this.ParseStatement(sql);
            Assert.IsInstanceOfType(node, typeof(UpdateStatement));
            var stmt = (UpdateStatement)node;
            Assert.AreEqual("users", stmt.TableName);
            Assert.AreEqual(2, stmt.SetClauses.Count);
            Assert.AreEqual("name", stmt.SetClauses[0].Column);
            Assert.AreEqual("Alice", ((LiteralExpression)stmt.SetClauses[0].Value).Value);
            Assert.IsNotNull(stmt.Where);
        }

        [TestMethod]
        public void TestDeleteStatement()
        {
            var sql = "DELETE FROM users WHERE id = 1";
            var node = this.ParseStatement(sql);
            Assert.IsInstanceOfType(node, typeof(DeleteStatement));
            var stmt = (DeleteStatement)node;
            Assert.AreEqual("users", stmt.TableName);
            Assert.IsNotNull(stmt.Where);
        }
    }
}
