# SQLite Configuration Guide

## Overview

The SQLite persistence provider supports loading configuration from JSON files. By default, retry is **enabled** with sensible defaults to handle transient errors automatically.

## Configuration Loading

### Loading Order
1. If no JSON file exists, default configuration is used (with retry enabled)
2. If JSON file exists but has no `RetryPolicy` section, default retry configuration is used (enabled)
3. If JSON file has partial `RetryPolicy`, missing values use defaults
4. If JSON file has complete `RetryPolicy`, all values are used as specified

### Usage in Code

```csharp
// Load from default location (./sqlite.json)
var config = SqliteConfiguration.FromJsonFile();

// Load from specific file
var config = SqliteConfiguration.FromJsonFile("/path/to/config.json");

// Load with required file (throws if not found)
var config = SqliteConfiguration.FromJsonFileRequired("/path/to/config.json");
```

## Configuration Properties

### Core Settings
- `DbFile` - Path to SQLite database file
- `CacheSize` - Database page cache size (default: -2000 = ~2MB)
- `PageSize` - Database page size (default: 4096 bytes)
- `JournalMode` - WAL, Delete, Truncate, etc. (default: WAL)
- `SynchronousMode` - Off, Normal, Full, Extra (default: Normal)
- `BusyTimeout` - Milliseconds to wait for locks (default: 5000ms)
- `EnableForeignKeys` - Enable FK constraints (default: true)
- `CommandTimeout` - Command timeout in seconds (default: 30)

### Retry Policy Settings
- `Enabled` - Enable/disable retry (default: **true**)
- `MaxAttempts` - Maximum retry attempts (default: 3)
- `InitialDelayMs` - Initial retry delay (default: 100ms)
- `MaxDelayMs` - Maximum retry delay (default: 5000ms)
- `BackoffMultiplier` - Exponential backoff multiplier (default: 2.0)

## Configuration Examples

### Minimal Configuration
```json
{
  "DbFile": "data/application.db"
}
```
**Note:** Retry is enabled by default with 3 attempts

### Standard Configuration
```json
{
  "DbFile": "data/application.db",
  "BusyTimeout": 10000,
  "CommandTimeout": 60,
  "RetryPolicy": {
    "MaxAttempts": 5,
    "InitialDelayMs": 200
  }
}
```

### Network Storage Configuration
```json
{
  "DbFile": "\\\\network-share\\data\\application.db",
  "BusyTimeout": 15000,
  "CommandTimeout": 60,
  "RetryPolicy": {
    "Enabled": true,
    "MaxAttempts": 5,
    "InitialDelayMs": 500,
    "MaxDelayMs": 10000,
    "BackoffMultiplier": 2.0
  }
}
```

### High Contention Configuration
```json
{
  "DbFile": "data/high-traffic.db",
  "BusyTimeout": 30000,
  "JournalMode": "WAL",
  "RetryPolicy": {
    "MaxAttempts": 10,
    "InitialDelayMs": 50,
    "MaxDelayMs": 2000,
    "BackoffMultiplier": 1.5
  }
}
```

### Disable Retry
```json
{
  "DbFile": "data/application.db",
  "RetryPolicy": {
    "Enabled": false
  }
}
```

## Important Notes

1. **Retry is enabled by default** - Even if no configuration file exists or `RetryPolicy` is omitted
2. **Transient errors handled automatically** - Database locks, network issues, and I/O errors
3. **ETW logging included** - All retry operations are logged via ETW for monitoring
4. **Configuration validation** - Invalid values will throw exceptions at load time

## Transient Errors Handled

The retry policy automatically handles:
- `SQLITE_BUSY` - Database locked by another connection
- `SQLITE_LOCKED` - Table locked by another operation
- `SQLITE_IOERR` - I/O errors (network/disk)
- Network path issues
- Sharing violations
- Temporary resource unavailability

Non-transient errors (like syntax errors, constraint violations) fail immediately without retry.