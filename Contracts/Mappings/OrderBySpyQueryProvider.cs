// -----------------------------------------------------------------------
// <copyright file="OrderBySpyQueryProvider.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

    /// <summary>
    /// Query provider that captures ORDER BY expressions.
    /// </summary>
    internal class OrderBySpyQueryProvider<T, TKey> : IQueryProvider
        where T : class, IEntity<TKey> 
        where TKey : IEquatable<TKey>
    {
        private readonly OrderBySpyQueryable<T, TKey> queryable;

        public OrderBySpyQueryProvider(OrderBySpyQueryable<T, TKey> queryable)
        {
            this.queryable = queryable;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            return this.CreateQuery<T>(expression);
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            // Check if this is an OrderBy or ThenBy method call
            if (expression is MethodCallExpression methodCall)
            {
                var methodName = methodCall.Method.Name;
                if (methodName == "OrderBy" || methodName == "OrderByDescending" ||
                    methodName == "ThenBy" || methodName == "ThenByDescending")
                {
                    // Extract the property selector from the lambda expression
                    if (methodCall.Arguments.Count >= 2 && 
                        methodCall.Arguments[1] is UnaryExpression unary &&
                        unary.Operand is LambdaExpression lambda &&
                        lambda.Body is MemberExpression member)
                    {
                        var propertyName = member.Member.Name;
                        var columnName = this.queryable.GetMapper().GetColumnName(propertyName);
                        
                        if (!string.IsNullOrEmpty(columnName))
                        {
                            bool isAscending = methodName == "OrderBy" || methodName == "ThenBy";
                            this.queryable.AddOrderBy(columnName, isAscending);
                        }
                    }
                }
            }

            // Return the same queryable to support chaining
            return (IQueryable<TElement>)(object)this.queryable;
        }

        public object Execute(Expression expression)
        {
            throw new NotSupportedException("This provider is only for capturing ORDER BY expressions.");
        }

        public TResult Execute<TResult>(Expression expression)
        {
            throw new NotSupportedException("This provider is only for capturing ORDER BY expressions.");
        }
    }


    /// <summary>
    /// Query provider that captures ORDER BY expressions.
    /// </summary>
    internal class OrderBySpyQueryProvider<TEntity> : IQueryProvider
    {
        private readonly OrderBySpyQueryable<TEntity> queryable;

        public OrderBySpyQueryProvider(OrderBySpyQueryable<TEntity> queryable)
        {
            this.queryable = queryable;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            return this.CreateQuery<TEntity>(expression);
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            // Check if this is an OrderBy or ThenBy method call
            if (expression is MethodCallExpression methodCall)
            {
                var methodName = methodCall.Method.Name;
                if (methodName == "OrderBy" || methodName == "OrderByDescending" ||
                    methodName == "ThenBy" || methodName == "ThenByDescending")
                {
                    // Extract the property selector from the lambda expression
                    if (methodCall.Arguments.Count >= 2 &&
                        methodCall.Arguments[1] is UnaryExpression unary &&
                        unary.Operand is LambdaExpression lambda &&
                        lambda.Body is MemberExpression member)
                    {
                        var propertyName = member.Member.Name;
                        var columnName = this.queryable.GetTranslator().GetColumnNameFromMapper(propertyName);

                        if (!string.IsNullOrEmpty(columnName))
                        {
                            bool isAscending = methodName == "OrderBy" || methodName == "ThenBy";
                            this.queryable.AddOrderBy(columnName, isAscending);
                        }
                    }
                }
            }

            // Return the same queryable to support chaining
            return (IQueryable<TElement>)(object)this.queryable;
        }

        public object Execute(Expression expression)
        {
            throw new NotSupportedException("This provider is only for capturing ORDER BY expressions.");
        }

        public TResult Execute<TResult>(Expression expression)
        {
            throw new NotSupportedException("This provider is only for capturing ORDER BY expressions.");
        }
    }
}