//-------------------------------------------------------------------------------
// <copyright file="ExpressionTranslator.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;

    /// <summary>
    /// Translates LINQ Expression trees to SQL WHERE clauses.
    /// </summary>
    public class ExpressionTranslator<T> : ExpressionVisitor
    {
        protected readonly StringBuilder sql = new StringBuilder();
        protected readonly Dictionary<string, object> parameters = new Dictionary<string, object>();
        private readonly IReadOnlyDictionary<System.Reflection.PropertyInfo, PropertyMapping> propertyMappings;
        private readonly Func<string> getPrimaryKeyColumn;
        protected int parameterIndex;

        public ExpressionTranslator() : this(null, null)
        {
        }

        public ExpressionTranslator(
            IReadOnlyDictionary<System.Reflection.PropertyInfo, PropertyMapping> propertyMappings,
            Func<string> getPrimaryKeyColumn)
        {
            this.propertyMappings = propertyMappings;
            this.getPrimaryKeyColumn = getPrimaryKeyColumn;
        }

        public class TranslationResult
        {
            public string Sql { get; set; }

            /// <summary>
            /// Note: key already has "@" prefix.
            /// </summary>
            public Dictionary<string, object> Parameters { get; set; }
        }

        public TranslationResult Translate(Expression<Func<T, bool>> expression)
        {
            this.sql.Clear();
            this.Visit(expression.Body);
            return new TranslationResult
            {
                Sql = this.sql.ToString(),
                Parameters = this.parameters
            };
        }

        public string TranslateOrderBy(Func<IQueryable<T>, IOrderedQueryable<T>> orderBy, bool ascending = true)
        {
            if (orderBy == null)
                return string.Empty;

            // Create a spy queryable to capture the ordering expressions
            var spyQueryable = new OrderBySpyQueryable<T>(this);

            try
            {
                // Execute the orderBy function with our spy queryable
                orderBy(spyQueryable);

                // Get the captured ORDER BY clause
                return spyQueryable.GetOrderBySql();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to translate ORDER BY expression to SQL.", ex);
            }
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            this.sql.Append("(");

            // Check if we need special handling for DateTime comparisons
            bool isDateTimeComparison = this.IsDateTimeExpression(node.Left) || this.IsDateTimeExpression(node.Right);

            if (isDateTimeComparison && this.RequiresDateTimeConversion())
            {
                // Handle DateTime comparisons with conversion
                this.VisitWithDateTimeConversion(node.Left);
            }
            else
            {
                this.Visit(node.Left);
            }

            switch (node.NodeType)
            {
                case ExpressionType.Equal:
                    this.sql.Append(" = ");
                    break;
                case ExpressionType.NotEqual:
                    this.sql.Append(" <> ");
                    break;
                case ExpressionType.LessThan:
                    this.sql.Append(" < ");
                    break;
                case ExpressionType.LessThanOrEqual:
                    this.sql.Append(" <= ");
                    break;
                case ExpressionType.GreaterThan:
                    this.sql.Append(" > ");
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    this.sql.Append(" >= ");
                    break;
                case ExpressionType.AndAlso:
                    this.sql.Append(" AND ");
                    break;
                case ExpressionType.OrElse:
                    this.sql.Append(" OR ");
                    break;
                default:
                    throw new NotSupportedException($"Binary operator {node.NodeType} is not supported");
            }

            if (isDateTimeComparison && this.RequiresDateTimeConversion())
            {
                // Handle DateTime comparisons with conversion
                this.VisitWithDateTimeConversion(node.Right);
            }
            else
            {
                this.Visit(node.Right);
            }

            this.sql.Append(")");
            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression != null && node.Expression.NodeType == ExpressionType.Parameter)
            {
                // This is a property access on the parameter (e.g., x.UpdateName)
                var columnName = this.GetColumnName(node.Member.Name);

                // Check if this is a DateTime column that needs special handling
                if (this.IsDateTimeProperty(node) && this.RequiresDateTimeConversion())
                {
                    this.sql.Append(this.FormatDateTimeColumn(columnName));
                }
                else
                {
                    this.sql.Append(columnName);
                }
            }
            else
            {
                // This is a constant value access
                var value = this.GetValue(node);
                var paramName = $"@p{this.parameterIndex++}";
                
                // Check if this is a DateTime value that needs special handling
                if ((value is DateTime || value is DateTimeOffset) && this.RequiresDateTimeConversion())
                {
                    this.StoreParameterValue(paramName, value);
                    this.sql.Append(this.FormatDateTimeParameter(paramName));
                }
                else
                {
                    this.parameters[paramName] = value;
                    this.sql.Append(paramName);
                }
            }

            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            var paramName = $"@p{this.parameterIndex++}";
            
            // Check if this is a DateTime constant that needs special handling
            if ((node.Value is DateTime || node.Value is DateTimeOffset) && this.RequiresDateTimeConversion())
            {
                this.StoreParameterValue(paramName, node.Value);
                this.sql.Append(this.FormatDateTimeParameter(paramName));
            }
            else
            {
                this.parameters[paramName] = node.Value;
                this.sql.Append(paramName);
            }
            
            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Handle DateTime methods
            if (node.Object != null && (node.Object.Type == typeof(DateTime) || node.Object.Type == typeof(DateTimeOffset)))
            {
                // For DateTime methods like AddDays, AddMonths, etc., evaluate the expression and use the result as a constant
                if (node.Method.Name.StartsWith("Add"))
                {
                    try
                    {
                        // Evaluate the DateTime method call to get the actual DateTime value
                        var value = this.GetValue(node);
                        var paramName = $"@p{this.parameterIndex++}";
                        
                        if (this.RequiresDateTimeConversion())
                        {
                            this.StoreParameterValue(paramName, value);
                            this.sql.Append(this.FormatDateTimeParameter(paramName));
                        }
                        else
                        {
                            this.parameters[paramName] = value;
                            this.sql.Append(paramName);
                        }
                        
                        return node;
                    }
                    catch
                    {
                        // If we can't evaluate it (e.g., it references a property), fall through to error
                        throw new NotSupportedException($"DateTime method {node.Method.Name} with non-constant arguments is not supported in SQL translation");
                    }
                }
            }

            if (node.Method.Name == "Contains")
            {
                if (node.Object != null)
                {
                    // String.Contains
                    this.Visit(node.Object);
                    this.sql.Append(" LIKE ");
                    var value = this.GetValue(node.Arguments[0]);
                    var paramName = $"@p{this.parameterIndex++}";
                    this.parameters[paramName] = $"%{value}%";
                    this.sql.Append(paramName);
                }
                else if (node.Arguments.Count == 2)
                {
                    // List.Contains
                    this.Visit(node.Arguments[1]);
                    this.sql.Append(" IN (");
                    var values = this.GetValue(node.Arguments[0]) as IEnumerable;
                    var paramNames = new List<string>();
                    foreach (var value in values)
                    {
                        var paramName = $"@p{this.parameterIndex++}";
                        this.parameters[paramName] = value;
                        paramNames.Add(paramName);
                    }
                    this.sql.Append(string.Join(", ", paramNames));
                    this.sql.Append(")");
                }
            }
            else if (node.Method.Name == "StartsWith")
            {
                this.Visit(node.Object);
                this.sql.Append(" LIKE ");
                var value = this.GetValue(node.Arguments[0]);
                var paramName = $"@p{this.parameterIndex++}";
                this.parameters[paramName] = $"{value}%";
                this.sql.Append(paramName);
            }
            else if (node.Method.Name == "EndsWith")
            {
                this.Visit(node.Object);
                this.sql.Append(" LIKE ");
                var value = this.GetValue(node.Arguments[0]);
                var paramName = $"@p{this.parameterIndex++}";
                this.parameters[paramName] = $"%{value}";
                this.sql.Append(paramName);
            }
            else
            {
                throw new NotSupportedException($"Method {node.Method.Name} is not supported");
            }

            return node;
        }

        protected internal string GetColumnName(string propertyName)
        {
            return this.GetColumnNameFromMapper(propertyName);
        }

        internal string GetColumnNameFromMapper(string propertyName)
        {
            // If property mappings are available, use them to get the correct column name
            if (this.propertyMappings != null)
            {
                var propertyMapping = this.propertyMappings
                    .FirstOrDefault(p => p.Key.Name == propertyName);

                if (propertyMapping.Value != null)
                {
                    return propertyMapping.Value.ColumnName;
                }
            }

            // Fallback to property name mappings
            return propertyName switch
            {
                "Id" => this.GetPrimaryKeyColumn(),
                "Key" => this.GetPrimaryKeyColumn(),
                _ => propertyName
            };
        }

        internal string GetPrimaryKeyColumn()
        {
            // If getPrimaryKeyColumn delegate is available, use it
            if (this.getPrimaryKeyColumn != null)
            {
                return this.getPrimaryKeyColumn();
            }

            // Fallback to type-based determination
            var type = typeof(T);
            if (type.Name == "UpdateEntity") return "UpdateId";
            if (type.Name == "CacheEntry") return "CacheKey";
            return "Id";
        }

        protected object GetValue(Expression expression)
        {
            var lambda = Expression.Lambda(expression);
            var compiled = lambda.Compile();
            return compiled.DynamicInvoke();
        }

        /// <summary>
        /// Determines if DateTime columns/values require special conversion (e.g., for SQLite).
        /// </summary>
        protected virtual bool RequiresDateTimeConversion()
        {
            return false; // Default: no conversion needed
        }

        /// <summary>
        /// Stores a parameter value, allowing derived classes to convert the value if needed.
        /// </summary>
        protected virtual void StoreParameterValue(string parameterName, object value)
        {
            this.parameters[parameterName] = value; // Default: store as-is
        }

        /// <summary>
        /// Formats a DateTime column name for database-specific comparison.
        /// </summary>
        protected virtual string FormatDateTimeColumn(string columnName)
        {
            return columnName; // Default: no formatting
        }

        /// <summary>
        /// Formats a DateTime parameter for database-specific comparison.
        /// </summary>
        protected virtual string FormatDateTimeParameter(string parameterName)
        {
            return parameterName; // Default: no formatting
        }

        /// <summary>
        /// Checks if an expression represents a DateTime value or property.
        /// </summary>
        protected bool IsDateTimeExpression(Expression expression)
        {
            if (expression.Type == typeof(DateTime) || expression.Type == typeof(DateTime?))
                return true;

            if (expression.Type == typeof(DateTimeOffset) || expression.Type == typeof(DateTimeOffset?))
                return true;

            return false;
        }

        /// <summary>
        /// Checks if a member expression is a DateTime property.
        /// </summary>
        protected bool IsDateTimeProperty(MemberExpression node)
        {
            return node.Type == typeof(DateTime) || node.Type == typeof(DateTime?) ||
                   node.Type == typeof(DateTimeOffset) || node.Type == typeof(DateTimeOffset?);
        }

        /// <summary>
        /// Visits an expression with DateTime conversion applied.
        /// </summary>
        protected virtual void VisitWithDateTimeConversion(Expression expression)
        {
            if (expression is MemberExpression memberExpr &&
                memberExpr.Expression != null &&
                memberExpr.Expression.NodeType == ExpressionType.Parameter && this.IsDateTimeProperty(memberExpr))
            {
                // This is a DateTime property access on the parameter
                var columnName = this.GetColumnName(memberExpr.Member.Name);
                this.sql.Append(this.FormatDateTimeColumn(columnName));
            }
            else if (expression is MethodCallExpression methodCall &&
                     methodCall.Object != null &&
                     (methodCall.Object.Type == typeof(DateTime) || methodCall.Object.Type == typeof(DateTimeOffset)))
            {
                // DateTime method call - evaluate and format
                var value = this.GetValue(expression);
                var paramName = $"@p{this.parameterIndex++}";
                this.parameters[paramName] = value;
                this.sql.Append(this.FormatDateTimeParameter(paramName));
            }
            else if (expression is ConstantExpression constantExpr &&
                     (constantExpr.Value is DateTime || constantExpr.Value is DateTimeOffset))
            {
                // DateTime constant
                var paramName = $"@p{this.parameterIndex++}";
                this.parameters[paramName] = constantExpr.Value;
                this.sql.Append(this.FormatDateTimeParameter(paramName));
            }
            else
            {
                // Default visit
                this.Visit(expression);
            }
        }

    }
}