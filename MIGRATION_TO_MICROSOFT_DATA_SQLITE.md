# Migration Guide: System.Data.SQLite to Microsoft.Data.Sqlite

## Package Changes

### Remove from Directory.Packages.props:
```xml
<!-- Remove these -->
<PackageVersion Include="Stub.System.Data.SQLite.Core.NetStandard" Version="1.0.119" />
<PackageVersion Include="System.Data.SQLite.Core" Version="1.0.119" />
```

### Add to Directory.Packages.props:
```xml
<!-- Add this -->
<PackageVersion Include="Microsoft.Data.Sqlite" Version="9.0.0" />
```

### Update .csproj:
```xml
<!-- Remove -->
<PackageReference Include="Stub.System.Data.SQLite.Core.NetStandard" />
<PackageReference Include="System.Data.SQLite.Core" />

<!-- Add -->
<PackageReference Include="Microsoft.Data.Sqlite" />
```

## Code Changes Required

### 1. Namespace Changes
```csharp
// Old
using System.Data.SQLite;

// New
using Microsoft.Data.Sqlite;
```

### 2. Class Name Changes
```csharp
// Old
SQLiteConnection
SQLiteCommand
SQLiteDataReader
SQLiteParameter
SQLiteTransaction
SQLiteException

// New
SqliteConnection
SqliteCommand
SqliteDataReader
SqliteParameter
SqliteTransaction
SqliteException
```

### 3. Connection String Changes
```csharp
// Old
var connectionString = $"Data Source={dbPath};Version=3;";

// New (Version=3 is not needed)
var connectionString = $"Data Source={dbPath}";
```

### 4. Error Code Changes
```csharp
// Old
catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Busy)

// New
catch (SqliteException ex) when (ex.SqliteErrorCode == 5) // SQLITE_BUSY
```

### 5. Parameter Syntax
```csharp
// Both support @ prefix for parameters
cmd.Parameters.AddWithValue("@id", id);
```

## Feature Differences

### Features Lost:
- `GetBytes()` method on DataReader (use `GetStream()` instead)
- Some extended result codes
- Built-in encryption support (need separate package)

### Features Gained:
- Better async support
- Built-in FTS5 support
- Better .NET Core integration
- Smaller package size
- Better performance in some scenarios

## Migration Strategy

### Phase 1: Compatibility Layer
Create an abstraction layer to minimize changes:

```csharp
// Create interfaces
public interface ISqliteConnection : IDisposable
{
    void Open();
    ISqliteCommand CreateCommand();
    // ... other methods
}

// Create wrapper classes
public class SqliteConnectionWrapper : ISqliteConnection
{
    private readonly SqliteConnection connection;
    // ... implementation
}
```

### Phase 2: Gradual Migration
1. Update one module at a time
2. Run tests after each module
3. Update error handling
4. Update connection string management

### Phase 3: Cleanup
1. Remove old packages
2. Remove compatibility layer if not needed
3. Optimize for new API features

## Testing Considerations

### Key Areas to Test:
1. **Transient Error Handling** - Error codes have changed
2. **Connection Pooling** - Behavior may differ
3. **Transaction Handling** - Ensure ACID properties maintained
4. **Performance** - Benchmark critical operations
5. **Concurrency** - Test multi-threaded scenarios

### Sample Test Updates:
```csharp
// Old test
[TestMethod]
public void TestSQLiteException()
{
    // Expects SQLiteException
}

// New test
[TestMethod]
public void TestSqliteException()
{
    // Expects SqliteException
}
```

## Pros of Migration

1. **Active Development** - Microsoft actively maintains it
2. **Better Integration** - First-class .NET Core/5+ support
3. **Performance** - Generally faster for async operations
4. **Smaller Size** - Reduced deployment size
5. **Modern API** - Cleaner, more intuitive API

## Cons of Migration

1. **Breaking Changes** - Requires code updates
2. **Testing Required** - Full regression testing needed
3. **Feature Gaps** - Some features not available
4. **Learning Curve** - Team needs to learn new API

## Decision Matrix

| Scenario | Recommendation |
|----------|---------------|
| New project | Use Microsoft.Data.Sqlite |
| Existing stable project | Keep System.Data.SQLite |
| Modernizing to .NET 6+ | Migrate to Microsoft.Data.Sqlite |
| Need encryption | Stay with System.Data.SQLite or use SQLCipher |
| Cross-platform priority | Use Microsoft.Data.Sqlite |

## Simplified Alternative

If migration is too complex, simply clean up current packages:

```xml
<!-- Just use the main package -->
<PackageReference Include="System.Data.SQLite.Core" Version="1.0.119" />
<!-- Remove the Stub package - it's redundant -->
```

This reduces complexity without requiring code changes.