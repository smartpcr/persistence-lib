// -----------------------------------------------------------------------
// <copyright file="OrderBySpyQueryable.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts;

    /// <summary>
    /// A spy implementation of IQueryable that captures ordering expressions and converts them to SQL.
    /// </summary>
    internal class OrderBySpyQueryable<T, TKey> : IOrderedQueryable<T> 
        where T : class, IEntity<TKey> 
        where TKey : IEquatable<TKey>
    {
        private readonly BaseEntityMapper<T, TKey> mapper;
        private readonly List<(string Column, bool IsAscending)> orderByColumns;

        public OrderBySpyQueryable(BaseEntityMapper<T, TKey> mapper)
        {
            this.mapper = mapper;
            this.orderByColumns = new List<(string, bool)>();
            this.Expression = Expression.Constant(this);
            this.Provider = new OrderBySpyQueryProvider<T, TKey>(this);
        }

        public Type ElementType => typeof(T);
        public Expression Expression { get; }

        public IQueryProvider Provider { get; }

        public void AddOrderBy(string column, bool isAscending)
        {
            this.orderByColumns.Add((column, isAscending));
        }

        public string GetOrderBySql()
        {
            if (!this.orderByColumns.Any())
            {
                return string.Empty;
            }

            var orderByParts = this.orderByColumns.Select(ob => 
                $"{ob.Column} {(ob.IsAscending ? "ASC" : "DESC")}");
            
            return " ORDER BY " + string.Join(", ", orderByParts);
        }

        public IEnumerator<T> GetEnumerator()
        {
            throw new NotSupportedException("This queryable is only for capturing ORDER BY expressions.");
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        internal BaseEntityMapper<T, TKey> GetMapper() => this.mapper;
    }


    /// <summary>
    /// A spy implementation of IQueryable that captures ordering expressions and converts them to SQL.
    /// </summary>
    internal class OrderBySpyQueryable<T> : IOrderedQueryable<T>
    {
        private readonly ExpressionTranslator<T> translator;
        private readonly List<(string Column, bool IsAscending)> orderByColumns;
        private readonly Expression expression;

        public OrderBySpyQueryable(ExpressionTranslator<T> translator)
        {
            this.translator = translator;
            this.orderByColumns = new List<(string, bool)>();
            this.expression = Expression.Constant(this);
            this.Provider = new OrderBySpyQueryProvider<T>(this);
        }

        public Type ElementType => typeof(T);
        public Expression Expression => this.expression;
        public IQueryProvider Provider { get; }

        public void AddOrderBy(string column, bool isAscending)
        {
            this.orderByColumns.Add((column, isAscending));
        }

        public string GetOrderBySql()
        {
            if (!this.orderByColumns.Any())
            {
                return string.Empty;
            }

            var orderByParts = this.orderByColumns.Select(ob =>
                $"{ob.Column} {(ob.IsAscending ? "ASC" : "DESC")}");

            return "ORDER BY " + string.Join(", ", orderByParts);
        }

        public IEnumerator<T> GetEnumerator()
        {
            throw new NotSupportedException("This queryable is only for capturing ORDER BY expressions.");
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        internal ExpressionTranslator<T> GetTranslator() => this.translator;
    }
}
