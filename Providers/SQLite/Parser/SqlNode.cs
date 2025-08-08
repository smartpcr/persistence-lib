// -----------------------------------------------------------------------
// <copyright file="SqlNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Parser
{
    using System.Collections.Generic;

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
        public SelectStatement Subquery { get; set; }
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

    // DML nodes
    public class InsertStatement : SqlNode
    {
        public string TableName { get; set; }
        public List<string> Columns { get; set; } = new List<string>();
        public List<Expression> Values { get; set; } = new List<Expression>();
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
        public SqlTokenType Type { get; set; }
    }

    public class BinaryExpression : Expression
    {
        public Expression Left { get; set; }
        public SqlTokenType Operator { get; set; }
        public Expression Right { get; set; }
    }

    public class UnaryExpression : Expression
    {
        public SqlTokenType Operator { get; set; }
        public Expression Operand { get; set; }
    }

    public class FunctionExpression : Expression
    {
        public string FunctionName { get; set; }
        public List<Expression> Arguments { get; set; } = new List<Expression>();
        public bool IsDistinct { get; set; }
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

    // DDL nodes
    public class CreateTableStatement : SqlNode
    {
        public string TableName { get; set; }
        public List<ColumnDefinition> Columns { get; set; } = new List<ColumnDefinition>();
        public List<TableConstraint> Constraints { get; set; } = new List<TableConstraint>();
    }

    public class ColumnDefinition : SqlNode
    {
        public string Name { get; set; }
        public string DataType { get; set; }
    }

    public class TableConstraint : SqlNode
    {
        public string Name { get; set; }
        public ConstraintType Type { get; set; }
        public List<string> Columns { get; set; } = new List<string>();
    }

    public enum ConstraintType
    {
        PrimaryKey,
        Unique,
        ForeignKey,
    }

    public class CreateIndexStatement : SqlNode
    {
        public string IndexName { get; set; }
        public string TableName { get; set; }
        public List<string> Columns { get; set; } = new List<string>();
    }
}
