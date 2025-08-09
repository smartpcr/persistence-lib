// -----------------------------------------------------------------------
// <copyright file="SQLiteExpressionTranslator.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    /// <summary>
    /// SQLite-specific expression translator that handles DateTime values stored as text.
    /// </summary>
    public class SQLiteExpressionTranslator<T> : ExpressionTranslator<T>
    {
        public SQLiteExpressionTranslator() : base()
        {
        }

        public SQLiteExpressionTranslator(
            IReadOnlyDictionary<PropertyInfo, PropertyMapping> propertyMappings,
            Func<string> getPrimaryKeyColumn)
            : base(propertyMappings, getPrimaryKeyColumn)
        {
        }

        /// <summary>
        /// SQLite stores DateTime as text, so we need to use datetime() function for comparisons.
        /// </summary>
        protected override bool RequiresDateTimeConversion()
        {
            return true;
        }

        /// <summary>
        /// Wraps DateTime column names with SQLite's datetime() function.
        /// </summary>
        protected override string FormatDateTimeColumn(string columnName)
        {
            return $"datetime({columnName})";
        }

        /// <summary>
        /// Wraps DateTime parameters with SQLite's datetime() function.
        /// SQLite expects ISO 8601 format, which is handled by the parameter value.
        /// </summary>
        protected override string FormatDateTimeParameter(string parameterName)
        {
            return $"datetime({parameterName})";
        }

        /// <summary>
        /// Stores DateTime values as ISO 8601 strings for SQLite.
        /// </summary>
        protected override void StoreParameterValue(string parameterName, object value)
        {
            if (value is DateTime dt)
            {
                this.parameters[parameterName] = dt.ToString("O");
            }
            else if (value is DateTimeOffset dto)
            {
                this.parameters[parameterName] = dto.UtcDateTime.ToString("O");
            }
            else
            {
                this.parameters[parameterName] = value;
            }
        }

        /// <summary>
        /// Override to handle DateTime conversion for SQLite.
        /// Converts DateTime values to ISO 8601 strings before storing as parameters.
        /// </summary>
        protected override void VisitWithDateTimeConversion(Expression expression)
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
                // DateTime method call - evaluate and convert to ISO 8601 string
                var value = this.GetValue(expression);
                var paramName = $"@p{this.parameterIndex++}";
                
                // Convert DateTime to ISO 8601 string for SQLite
                if (value is DateTime dt)
                {
                    this.parameters[paramName] = dt.ToString("O");
                }
                else if (value is DateTimeOffset dto)
                {
                    this.parameters[paramName] = dto.UtcDateTime.ToString("O");
                }
                else
                {
                    this.parameters[paramName] = value;
                }
                
                this.sql.Append(this.FormatDateTimeParameter(paramName));
            }
            else if (expression is ConstantExpression constantExpr &&
                     (constantExpr.Value is DateTime || constantExpr.Value is DateTimeOffset))
            {
                // DateTime constant - convert to ISO 8601 string
                var paramName = $"@p{this.parameterIndex++}";
                
                if (constantExpr.Value is DateTime dt)
                {
                    this.parameters[paramName] = dt.ToString("O");
                }
                else if (constantExpr.Value is DateTimeOffset dto)
                {
                    this.parameters[paramName] = dto.UtcDateTime.ToString("O");
                }
                else
                {
                    this.parameters[paramName] = constantExpr.Value;
                }
                
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