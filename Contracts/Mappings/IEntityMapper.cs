// -----------------------------------------------------------------------
// <copyright file="IEntityMapper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq.Expressions;
    using System.Reflection;

    public interface IEntityMapper<T, TKey>
        where T : class, IEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        string TableName { get; }
        string SchemaName { get; }
        bool EnableSoftDelete { get; }
        bool EnableExpiry { get; }
        TimeSpan? ExpirySpan { get; }
        bool EnableArchive { get; }
        bool SyncWithList { get; }
        bool EnableAuditTrail { get; }
        Type[] Depends { get; }

        IReadOnlyDictionary<PropertyInfo, PropertyMapping> GetPropertyMappings();
        string GetPrimaryKeyColumn();
        List<string> GetPrimaryKeyColumns();
        
        /// <summary>
        /// Gets the fully qualified table name including schema, properly escaped for SQL.
        /// </summary>
        string GetFullTableName();

        List<string> GetSelectColumns();
        List<string> GetInsertColumns();
        List<string> GetUpdateColumns();
        void AddParameters(IDbCommand command, T entity);
        T MapFromReader(IDataReader reader);
        string GenerateCreateTableSql(bool includeIfNotExists = true);
        IEnumerable<string> GenerateCreateIndexSql();
        string GenerateWhereClause(Expression<Func<T, bool>> predicate);
        byte[] SerializeEntity(T entity);
        string SerializeKey(TKey key);
        TKey DeserializeKey(string serialized);
        IDbCommand CreateSelectCommand(
            TKey key,
            bool includeAllVersions = false,
            bool includeDeleted = false,
            bool includeExpired = false);

        /// <summary>
        /// Creates a database command with embedded SQL and parameters using context.
        /// This is the improved method that combines SQL generation with parameter binding.
        /// </summary>
        /// <param name="operation">The type of database operation.</param>
        /// <param name="context">The command context containing all necessary parameters.</param>
        /// <returns>A configured database command ready for execution.</returns>
        IDbCommand CreateCommand(DbOperationType operation, CommandContext<T, TKey> context);

        /// <summary>
        /// Maps an entity to a dictionary of parameter names and values.
        /// </summary>
        /// <param name="entity">The entity to map.</param>
        /// <returns>A dictionary containing parameter names (with @ prefix) and their values.</returns>
        Dictionary<string, object> MapEntityToParameters(T entity);

        /// <summary>
        /// Maps an ID value to a dictionary of parameter names and values.
        /// </summary>
        /// <param name="id">The ID value to map.</param>
        /// <returns>A dictionary containing the ID parameter name and value.</returns>
        Dictionary<string, object> MapIdToParameters(TKey id);

        /// <summary>
        /// Gets the column name for a given property name.
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <returns>The column name, or null if the property is not mapped.</returns>
        string GetColumnName(string propertyName);

        /// <summary>
        /// Generates the SELECT ALL SQL statement.
        /// </summary>
        /// <returns>The SELECT ALL SQL statement.</returns>
        string GenerateSelectAllSql();

        /// <summary>
        /// Generates the SELECT BY ID SQL statement.
        /// </summary>
        /// <returns>The SELECT BY ID SQL statement.</returns>
        string GenerateSelectByIdSql();

        /// <summary>
        /// Generates a SELECT SQL statement with a predicate expression.
        /// </summary>
        /// <param name="predicate">The predicate expression to translate to a WHERE clause.</param>
        /// <param name="options">The select options to use (optional).</param>
        /// <returns>The generated SELECT SQL statement with parameters.</returns>
        (string sql, Dictionary<string, object> parameters) GenerateSelectSql(
            Expression<Func<T, bool>> predicate,
            SelectOptions options = null);

        /// <summary>
        /// Generates the INSERT SQL statement.
        /// </summary>
        /// <returns>The INSERT SQL statement.</returns>
        string GenerateInsertSql();

        /// <summary>
        /// Generates the UPDATE SQL statement.
        /// </summary>
        /// <returns>The UPDATE SQL statement.</returns>
        string GenerateUpdateSql();

        /// <summary>
        /// Generates the DELETE SQL statement.
        /// </summary>
        /// <returns>The DELETE SQL statement.</returns>
        string GenerateDeleteSql();

        /// <summary>
        /// Generates a batch INSERT SQL statement for multiple entities.
        /// </summary>
        /// <param name="entityCount">The number of entities to insert.</param>
        /// <returns>The batch INSERT SQL statement.</returns>
        string GenerateBatchInsertSql(int entityCount);
    }
}
