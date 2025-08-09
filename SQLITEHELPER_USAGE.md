# SQLiteHelper Usage Guide

## Overview

The `SQLiteHelper` class provides comprehensive database inspection capabilities for SQLite databases. It can read database statistics, table definitions, indexes, constraints, and generate detailed reports.

## Features

- **Database Statistics** - File size, page count, encoding, last modified date
- **Table Information** - Columns, data types, constraints, row counts
- **Index Details** - Unique/partial indexes, indexed columns
- **Foreign Keys** - Relationships, cascade rules
- **Check Constraints** - Validation rules
- **Triggers** - Event-based actions
- **Views** - Virtual tables
- **Report Generation** - Formatted text reports

## Basic Usage

```csharp
using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite.Helpers;

// Create helper with connection string - it manages its own connection
var connectionString = "Data Source=mydb.db;Version=3;";
await using var helper = new SQLiteHelper(connectionString);

// Get complete database information
var dbInfo = await helper.GetDatabaseInfoAsync();

// Generate a readable report
var report = helper.GenerateDatabaseReport(dbInfo);
Console.WriteLine(report);
```

### Important Notes
- `SQLiteHelper` now implements `IAsyncDisposable` and manages its own connection
- Always use `await using` or explicitly call `DisposeAsync()` when done
- The helper maintains a single connection for all operations, improving performance
- Thread-safe through internal semaphore synchronization

## Getting Database Statistics

```csharp
await using var helper = new SQLiteHelper(connectionString);
var stats = await helper.GetDatabaseStatsAsync();

Console.WriteLine($"File Size: {stats.FormattedFileSize}");
Console.WriteLine($"Page Count: {stats.PageCount}");
Console.WriteLine($"Tables: {stats.TableCount}");
Console.WriteLine($"Indexes: {stats.IndexCount}");
Console.WriteLine($"Last Modified: {stats.LastModified}");
```

## Inspecting Tables

```csharp
await using var helper = new SQLiteHelper(connectionString);
var tables = await helper.GetTablesAsync();
    
    foreach (var table in tables)
    {
        Console.WriteLine($"\nTable: {table.TableName}");
        Console.WriteLine($"  Row Count: {table.RowCount:N0}");
        Console.WriteLine($"  Has Primary Key: {table.HasPrimaryKey}");
        Console.WriteLine($"  WITHOUT ROWID: {table.IsWithoutRowId}");
        Console.WriteLine($"  STRICT: {table.IsStrict}");
        
        Console.WriteLine("  Columns:");
        foreach (var col in table.Columns)
        {
            var pk = col.IsPrimaryKey ? " [PK]" : "";
            var nullable = col.IsNullable ? "NULL" : "NOT NULL";
            var autoInc = col.IsAutoIncrement ? " AUTOINCREMENT" : "";
            
            Console.WriteLine($"    - {col.ColumnName}: {col.DataType} {nullable}{pk}{autoInc}");
            
            if (!string.IsNullOrEmpty(col.DefaultValue))
                Console.WriteLine($"      Default: {col.DefaultValue}");
        }
    }
}
```

## Analyzing Indexes

```csharp
await using var helper = new SQLiteHelper(connectionString);
var indexes = await helper.GetAllIndexesAsync();

foreach (var index in indexes)
{
    Console.WriteLine($"\nIndex: {index.IndexName} on {index.TableName}");
    Console.WriteLine($"  Unique: {index.IsUnique}");
    Console.WriteLine($"  Partial: {index.IsPartial}");
    
    if (index.IsPartial)
        Console.WriteLine($"  WHERE: {index.WhereClause}");
    
    Console.WriteLine("  Columns:");
    foreach (var col in index.Columns.Where(c => c.IsKey))
    {
        var desc = col.IsDescending ? " DESC" : "";
        Console.WriteLine($"    - {col.ColumnName}{desc}");
    }
}
```

## Checking Foreign Keys

```csharp
await using var helper = new SQLiteHelper(connectionString);
var foreignKeys = await helper.GetAllForeignKeysAsync();

var groupedFKs = foreignKeys.GroupBy(fk => fk.FromTable);
foreach (var group in groupedFKs)
{
    Console.WriteLine($"\nTable: {group.Key}");
    foreach (var fk in group)
    {
        Console.WriteLine($"  {fk.FromColumn} -> {fk.ToTable}({fk.ToColumn})");
        Console.WriteLine($"    ON DELETE: {fk.OnDelete}");
        Console.WriteLine($"    ON UPDATE: {fk.OnUpdate}");
    }
}
```

## Finding Check Constraints

```csharp
await using var helper = new SQLiteHelper(connectionString);
var tables = await helper.GetTablesAsync();

foreach (var table in tables)
{
    var constraints = await helper.GetTableCheckConstraintsAsync(table.TableName);
    if (constraints.Any())
    {
        Console.WriteLine($"\nCheck Constraints for {table.TableName}:");
        foreach (var constraint in constraints)
        {
            Console.WriteLine($"  CHECK({constraint.CheckExpression})");
        }
    }
}
```

## Examining Triggers

```csharp
await using var helper = new SQLiteHelper(connectionString);
var tables = await helper.GetTablesAsync();

foreach (var table in tables)
{
    var triggers = await helper.GetTableTriggersAsync(table.TableName);
    if (triggers.Any())
    {
        Console.WriteLine($"\nTriggers for {table.TableName}:");
        foreach (var trigger in triggers)
        {
            Console.WriteLine($"  {trigger.TriggerName}: {trigger.TriggerTiming} {trigger.TriggerEvent}");
        }
    }
}
```

## Viewing Database Views

```csharp
await using var helper = new SQLiteHelper(connectionString);
var views = await helper.GetViewsAsync();
foreach (var view in views)
{
    Console.WriteLine($"\nView: {view.ViewName}");
    Console.WriteLine($"  SQL: {view.CreateSql}");
}
```

## Sample Report Output

```
================================================================================
DATABASE REPORT: mydb.db
================================================================================

DATABASE SETTINGS:
----------------------------------------
SQLite Version:     3.39.0
File Size:          256 KB
Page Size:          4096 bytes
Page Count:         64
Free Pages:         2
Cache Size:         -2000
Journal Mode:       WAL
Encoding:           UTF-8
Auto Vacuum:        NONE
Foreign Keys:       Enabled
Last Modified:      2024-01-15 10:30:45

OBJECT SUMMARY:
----------------------------------------
Tables:             5
Indexes:            8
Triggers:           2
Views:              1

TABLES:
--------------------------------------------------------------------------------

[Users] - 1,234 rows
  Columns:
    - Id: INTEGER [PK, AUTO, NOT NULL]
    - Name: TEXT [NOT NULL]
    - Email: TEXT [NOT NULL]
    - CreatedAt: DATETIME DEFAULT CURRENT_TIMESTAMP
  Indexes:
    - IX_Users_Email: UNIQUE (Email)
    
[Products] - 567 rows
  Columns:
    - Id: INTEGER [PK, AUTO, NOT NULL]
    - Name: TEXT [NOT NULL]
    - Price: REAL [NOT NULL]
    - Stock: INTEGER DEFAULT 0
  Indexes:
    - IX_Products_Name: (Name)
  Check Constraints:
    - CHECK(Price > 0)
    
[Orders] - 3,456 rows
  Columns:
    - Id: INTEGER [PK, AUTO, NOT NULL]
    - UserId: INTEGER [NOT NULL]
    - ProductId: INTEGER [NOT NULL]
    - Quantity: INTEGER [NOT NULL]
    - OrderDate: DATETIME DEFAULT CURRENT_TIMESTAMP
  Foreign Keys:
    - UserId -> Users(Id) ON DELETE CASCADE ON UPDATE CASCADE
    - ProductId -> Products(Id) ON DELETE RESTRICT ON UPDATE CASCADE
```

## Advanced Features

### Detecting Table Types

```csharp
var dbInfo = await helper.GetDatabaseInfoAsync();

// Find WITHOUT ROWID tables
var withoutRowIdTables = dbInfo.Tables.Where(t => t.IsWithoutRowId);

// Find STRICT tables (SQLite 3.37+)
var strictTables = dbInfo.Tables.Where(t => t.IsStrict);

// Find tables with generated columns
var tablesWithGeneratedCols = dbInfo.Tables
    .Where(t => t.Columns.Any(c => c.IsGenerated));
```

### Analyzing Database Health

```csharp
var dbInfo = await helper.GetDatabaseInfoAsync();

// Check fragmentation
var fragmentation = (double)dbInfo.Stats.FreePageCount / dbInfo.Stats.PageCount * 100;
if (fragmentation > 20)
{
    Console.WriteLine($"Database is {fragmentation:F1}% fragmented. Consider VACUUM.");
}

// Check for missing indexes
foreach (var table in dbInfo.Tables.Where(t => t.RowCount > 1000))
{
    if (!table.Indexes.Any())
    {
        Console.WriteLine($"Large table {table.TableName} has no indexes!");
    }
}

// Check foreign key integrity
if (!dbInfo.ForeignKeysEnabled && dbInfo.ForeignKeys.Any())
{
    Console.WriteLine("WARNING: Foreign keys defined but not enforced!");
}
```

### Export Schema to SQL

```csharp
var dbInfo = await helper.GetDatabaseInfoAsync();
var schemaBuilder = new StringBuilder();

foreach (var table in dbInfo.Tables)
{
    schemaBuilder.AppendLine(table.CreateSql);
    schemaBuilder.AppendLine();
    
    foreach (var index in table.Indexes)
    {
        if (!string.IsNullOrEmpty(index.CreateSql))
        {
            schemaBuilder.AppendLine(index.CreateSql);
        }
    }
}

File.WriteAllText("schema.sql", schemaBuilder.ToString());
```

## Performance Considerations

- **Connection Management**: Helper maintains a single connection, automatically opened when needed
- **Thread Safety**: Internal semaphore ensures thread-safe access to the connection
- **Large Databases**: Row counts can be slow on large tables
- **Memory Usage**: Be careful with very large schemas
- **Caching**: Consider caching `DatabaseInfo` if schema doesn't change frequently
- **Disposal**: Always dispose the helper to free the connection

## Error Handling

```csharp
try
{
    var dbInfo = await helper.GetDatabaseInfoAsync();
}
catch (SQLiteException ex)
{
    Console.WriteLine($"Database error: {ex.Message}");
}
catch (FileNotFoundException)
{
    Console.WriteLine("Database file not found");
}
catch (UnauthorizedAccessException)
{
    Console.WriteLine("Access denied to database file");
}
```

## Integration with Persistence Provider

```csharp
// Use with SqliteConfiguration
var config = SqliteConfiguration.FromJsonFile();
var connectionString = $"Data Source={config.DbFile};Version=3;";

// Analyze database before optimization
await using (var helper = new SQLiteHelper(connectionString))
{
    var beforeInfo = await helper.GetDatabaseInfoAsync();
    Console.WriteLine($"Before: {beforeInfo.Stats.PageCount} pages, " +
                      $"{beforeInfo.Stats.FreePageCount} free");
}

// Run VACUUM (using separate connection since VACUUM requires exclusive access)
using (var connection = new SQLiteConnection(connectionString))
{
    await connection.OpenAsync();
    using (var cmd = new SQLiteCommand("VACUUM", connection))
    {
        await cmd.ExecuteNonQueryAsync();
    }
}

// Check results
await using (var helper = new SQLiteHelper(connectionString))
{
    var afterInfo = await helper.GetDatabaseInfoAsync();
    Console.WriteLine($"After: {afterInfo.Stats.PageCount} pages, " +
                      $"{afterInfo.Stats.FreePageCount} free");
}
```

## Use Cases

1. **Database Documentation** - Generate schema documentation
2. **Migration Planning** - Understand existing structure before changes
3. **Performance Analysis** - Find missing indexes, fragmentation
4. **Integrity Checking** - Verify constraints and relationships
5. **Development Tools** - Build database explorers or admin tools
6. **Testing** - Verify database structure in tests
7. **Monitoring** - Track database growth and health
8. **Debugging** - Understand database state during troubleshooting