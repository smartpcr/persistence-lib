# Transient Error Handling in SQLite Persistence Provider

## Overview

The SQLite Persistence Provider now includes built-in transient error handling with automatic retry logic. This feature helps handle temporary issues such as:

- Database file locks (SQLITE_BUSY)
- Table locks (SQLITE_LOCKED)
- Network connectivity issues (for network-attached storage)
- Temporary I/O errors
- Timeout exceptions

## How It Works

### Retry Policy

The provider uses an exponential backoff retry policy:
1. When a transient error occurs, the operation is automatically retried
2. Each retry waits longer than the previous one (exponential backoff)
3. A small random jitter is added to prevent thundering herd problems
4. After the maximum number of retries, the original exception is thrown

### Transient Error Detection

The following errors are considered transient and will trigger a retry:

**SQLite Errors:**
- `SQLITE_BUSY` (5): Database is locked
- `SQLITE_LOCKED` (6): A table in the database is locked
- `SQLITE_IOERR` (10): Disk I/O error
- `SQLITE_CANTOPEN` (14): Unable to open database file

**Other Errors:**
- `IOException`: File system and network-related errors
- `TimeoutException`: Operation timeouts
- Any error message containing:
  - "database is locked"
  - "database table is locked"
  - "unable to open database"
  - "disk i/o error"
  - "connection was closed"
  - "connection was lost"

## Configuration

### Via JSON Configuration File

Create a `sqlite.json` file with the following settings:

```json
{
  "SqliteConfiguration": {
    "DbFile": "data/myapp.db",
    "BusyTimeout": 5000,
    "CommandTimeout": 30,
    
    "RetryPolicy": {
      "Enabled": true,
      "MaxAttempts": 3,
      "InitialDelayMs": 100,
      "MaxDelayMs": 5000,
      "BackoffMultiplier": 2.0
    }
  }
}
```

### Configuration Options

The `RetryPolicy` object contains the following settings:

| Setting | Default | Description |
|---------|---------|-------------|
| `Enabled` | `true` | Enable/disable automatic retry for transient errors |
| `MaxAttempts` | `3` | Maximum number of retry attempts (0 to disable) |
| `InitialDelayMs` | `100` | Initial delay between retries in milliseconds |
| `MaxDelayMs` | `5000` | Maximum delay between retries in milliseconds |
| `BackoffMultiplier` | `2.0` | Multiplier for exponential backoff |

### Via Code

```csharp
var config = new SqliteConfiguration
{
    DbFile = "data/myapp.db",
    BusyTimeout = 5000,
    
    // Retry policy settings
    RetryPolicy = new RetryConfiguration
    {
        Enabled = true,
        MaxAttempts = 3,
        InitialDelayMs = 100,
        MaxDelayMs = 5000,
        BackoffMultiplier = 2.0
    }
};

var provider = new SQLitePersistenceProvider<MyEntity, Guid>(
    connectionString, 
    config);
```

### Using Pre-defined Configurations

The `RetryConfiguration` class provides several pre-defined configurations:

```csharp
// Default configuration (3 retries, 100ms initial delay)
config.RetryPolicy = RetryConfiguration.Default;

// No retry - fail immediately on transient errors
config.RetryPolicy = RetryConfiguration.NoRetry;

// Optimized for network storage (5 retries, 500ms initial delay)
config.RetryPolicy = RetryConfiguration.ForNetworkStorage;

// For high-contention scenarios (10 retries, 50ms initial delay)
config.RetryPolicy = RetryConfiguration.ForHighContention;
```

## Retry Behavior Example

With default settings, if a transient error occurs:

1. **First attempt**: Immediate execution
2. **First retry**: After ~100ms (+ jitter)
3. **Second retry**: After ~200ms (+ jitter)
4. **Third retry**: After ~400ms (+ jitter)
5. **Failure**: Original exception is thrown

The actual delays include a random jitter of 0-100ms to prevent synchronized retries from multiple threads.

## Best Practices

### 1. Configure BusyTimeout

Always set a reasonable `BusyTimeout` in addition to retry policy:

```json
{
  "BusyTimeout": 5000,  // 5 seconds
  "RetryPolicy": {
    "Enabled": true
  }
}
```

The `BusyTimeout` handles short locks at the SQLite level, while the retry policy handles longer-lasting issues.

### 2. Adjust for Network Storage

When using network-attached storage (NAS, SAN, or network shares), use the pre-defined configuration or customize:

```json
{
  "RetryPolicy": {
    "Enabled": true,
    "MaxAttempts": 5,
    "InitialDelayMs": 500,
    "MaxDelayMs": 10000,
    "BackoffMultiplier": 2.0
  }
}
```

Or in code:
```csharp
config.RetryPolicy = RetryConfiguration.ForNetworkStorage;
```

### 3. Monitor Retry Frequency

If retries happen frequently, investigate the root cause:
- Check for long-running transactions
- Review concurrent access patterns
- Consider using WAL mode for better concurrency
- Verify network stability for remote storage

### 4. Transaction Scope

Retries work best with properly scoped transactions:

```csharp
// Good: Short transaction scope
await provider.BatchOperationAsync(async batch =>
{
    await batch.CreateAsync(entity1);
    await batch.CreateAsync(entity2);
});

// Avoid: Long-running transactions
var transaction = await provider.BeginTransactionAsync();
// ... many operations over a long time ...
await transaction.CommitAsync();
```

## Limitations

1. **Reader Operations**: When using data readers, only the initial connection/execution is retried. Once a reader is open, subsequent read operations are not retried.

2. **Transaction Rollback**: If a retry occurs within a transaction, the entire transaction may need to be retried from the beginning.

3. **Non-Idempotent Operations**: Be careful with operations that have side effects. The retry mechanism assumes operations are safe to retry.

## Troubleshooting

### Retries Not Working

1. Check if retry policy is enabled:
   ```json
   "RetryPolicy": {
     "Enabled": true
   }
   ```

2. Verify the error is considered transient (check error codes and messages)

3. Ensure maximum retry attempts is greater than 0

### Too Many Retries

1. Reduce `MaxAttempts`
2. Increase `InitialDelayMs` for longer initial wait
3. Investigate root cause of locks/errors

### Performance Impact

1. Retries add latency - monitor operation times
2. Consider disabling retries for read-heavy workloads
3. Tune delays based on your specific scenario

## Example Usage

```csharp
// Configuration
var config = SqliteConfiguration.FromJsonFile("sqlite.json");

// Create provider with retry policy
var provider = new SQLitePersistenceProvider<Customer, Guid>(
    "Data Source=customers.db", 
    config);

// Operations will automatically retry on transient errors
try
{
    var customer = await provider.CreateAsync(newCustomer, callerInfo);
    Console.WriteLine($"Customer created: {customer.Id}");
}
catch (SQLiteException ex)
{
    // This exception is thrown only after all retries have been exhausted
    Console.WriteLine($"Failed after {config.RetryPolicy.MaxAttempts} retries: {ex.Message}");
}
```

## Migration from Previous Versions

The retry policy is enabled by default but can be disabled for backward compatibility:

```json
{
  "RetryPolicy": {
    "Enabled": false
  }
}
```

Or use the pre-defined no-retry configuration:

```csharp
config.RetryPolicy = RetryConfiguration.NoRetry;
```

This will restore the previous behavior where transient errors immediately fail without retry.