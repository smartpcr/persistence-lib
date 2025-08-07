# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET 8.0 persistence library for Microsoft Azure Stack Services Update system. It provides a generic, attribute-driven ORM layer with SQLite as the primary database provider.

## Essential Commands

### Build and Test
```bash
# Build the solution
dotnet build

# Run all tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Clean and rebuild
dotnet clean && dotnet build
```

## Architecture

### Core Design Pattern
The library uses an **attribute-driven ORM** pattern where entities decorated with mapping attributes automatically get CRUD operations:

```csharp
[Table("TableName")]
public class Entity : IEntity<Guid>
{
    [PrimaryKey]
    [Column("Id", SqlDbType.Text)]
    public Guid Id { get; set; }
    
    [Column("Version", SqlDbType.BigInt)]
    public long Version { get; set; }
}
```

### Provider Architecture
- **Contracts** (`/Contracts/`): Defines all interfaces and base contracts
- **SQLite Provider** (`/Providers/SQLite/`): SQLite-specific implementation of `IPersistenceProvider<T, TKey>`
- All operations are strongly typed with constraints: `where T : class, IEntity<TKey> where TKey : IEquatable<TKey>`

### Key Interfaces
- `IPersistenceProvider<T, TKey>`: Main provider interface aggregating all operations
- `ICrudOperation<T, TKey>`: Basic CRUD with optimistic concurrency via version tracking
- `IQueryOperation<T, TKey>`: LINQ expression translation and pagination
- `IBatchOperation<T, TKey>`: Bulk operations with transaction support
- `IBulkOperation<T, TKey>`: High-performance import/export with progress reporting

### Critical Implementation Details

1. **Version Tracking**: Every entity has a `Version` property for optimistic concurrency control. Updates fail if version mismatch occurs.

2. **Soft Delete**: Optional versioning system that preserves entity history instead of hard deletes.

3. **Transaction Scope**: All batch operations support ACID transactions via `ITransactionScope`.

4. **Thread Safety**: Provider implementations must be thread-safe for concurrent access.

5. **Mapper Pattern**: `BaseEntityMapper<T, TKey>` handles attribute-based mapping between entities and database records.

## Testing Approach

Test entities are organized in `/UnitTest/Entities/` by feature area. When adding new tests:
1. Create test entities with appropriate attributes
2. Use existing test entities where possible (e.g., `TestEntity`, `CompositeKeyEntity`)
3. Test coverage scenarios are documented in `/docs/persistence_test_scenarios.md`

## Known Issues

1. Build currently failing with compilation errors - missing entity definitions and API mismatches
2. Test coverage at 13% - many scenarios need implementation
3. Some duplicate package references in Directory.Packages.props need cleanup

## Development Notes

- Code style: 4-space indentation, Microsoft copyright headers required
- All async methods should follow async/await patterns consistently
- Entity mappers cache metadata for performance - changes to attributes require restart
- SQLite configuration supports WAL mode for better concurrent access