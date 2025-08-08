# Persistence Layer Test Scenarios

## 1. Overview

This document provides comprehensive test scenarios for the persistence layer implementation, covering all aspects of entity mapping, CRUD operations, batch processing, transactions, auditing, and specialized features. Each scenario includes setup requirements, test steps, expected outcomes, and edge cases.

## 2. Test Implementation Status

The following list tracks the implementation status of all test scenarios described in this document:

### 2.1 Entity Mapping Tests (16/16)

#### 2.1.1 BaseEntityMapper Scenario (8/8)
- **DiscoverProperties_ExcludesNotMapped** ✅
  *Purpose:* Verify [NotMapped] properties are excluded from mapping

- **DiscoverProperties_IncludesAllPublicProperties** ✅
  *Purpose:* Ensure all public properties are discovered

- **GetSqlType_MapsAllSupportedTypes** ✅
  *Purpose:* Validate type mapping to SQL types

- **GetSqlType_HandlesNullableTypes** ✅
  *Purpose:* Check nullable type SQL mapping

- **GenerateColumnName_ConvertsPascalToSnakeCase** ✅
  *Purpose:* Test PascalCase to snake_case conversion

- **GenerateCreateTableSql_WithSoftDelete** ✅
  *Purpose:* Verify table creation with soft-delete columns

- **GenerateCreateTableSql_WithExpiry** ✅
  *Purpose:* Verify table creation with expiry columns

- **GenerateCreateIndexSql_CreatesRequiredIndexes** ✅
  *Purpose:* Ensure proper index generation

#### 2.1.2 SQLiteEntityMapper Scenario (8/8)
- **CreateCommand_GeneratesCorrectInsertSql** ✅
  *Purpose:* Validate INSERT SQL generation

- **CreateCommand_GeneratesCorrectUpdateSql** ✅
  *Purpose:* Validate UPDATE SQL generation

- **CreateCommand_GeneratesCorrectDeleteSql** ✅
  *Purpose:* Validate DELETE SQL generation

- **MapFromReader_MapsAllColumnTypes** ✅
  *Purpose:* Test data reader to entity mapping

- **MapFromReader_HandlesDBNull** ✅
  *Purpose:* Verify DBNull handling

- **AddParameters_BindsAllProperties** ✅
  *Purpose:* Test parameter binding

- **SerializeEntity_HandlesComplexObjects** ✅
  *Purpose:* Test JSON serialization

- **DeserializeEntity_ReconstructsObjects** ✅
  *Purpose:* Test JSON deserialization

### 2.2 Core Persistence Tests (22/24)

#### 2.2.1 Initialization Scenario (4/4)
- **InitializeAsync_CreatesRequiredTables** ✅
  *Purpose:* Verify database initialization

- **InitializeAsync_AppliesPragmaSettings** ✅
  *Purpose:* Check PRAGMA settings application

- **InitializeAsync_CreatesAuditTable** ✅
  *Purpose:* Verify audit table creation

- **InitializeAsync_IdempotentOperation** ✅
  *Purpose:* Ensure multiple init calls are safe

#### 2.2.2 CRUD Operations Scenario (18/20)
- **CreateAsync_ValidEntity_Success** ✅
  *Purpose:* Basic entity creation

- **CreateAsync_DuplicateKey_ThrowsException** ✅
  *Purpose:* Duplicate key handling

- **CreateAsync_NullEntity_ThrowsException** ✅
  *Purpose:* Null entity validation

- **CreateAsync_SetsTrackingFields** ✅
  *Purpose:* Verify tracking fields are set

- **CreateAsync_WithSoftDelete_CreatesVersion** ✅
  *Purpose:* Version creation for soft-delete

- **CreateAsync_WithExpiry_SetsExpirationTime** ✅
  *Purpose:* Expiration time setting

- **GetAsync_ExistingEntity_ReturnsEntity** ✅
  *Purpose:* Basic entity retrieval

- **GetAsync_NonExistentEntity_ReturnsNull** ✅
  *Purpose:* Non-existent entity handling

- **GetAsync_SoftDeletedEntity_ReturnsNull** ✅
  *Purpose:* Soft-deleted entity filtering

- **GetAsync_ExpiredEntity_ReturnsNull** ✅
  *Purpose:* Expired entity filtering

- **GetByKeyAsync_IncludeAllVersions_ReturnsHistory** ✅
  *Purpose:* Version history retrieval

- **GetByKeyAsync_IncludeDeleted_ReturnsSoftDeleted** ✅
  *Purpose:* Include soft-deleted entities

- **UpdateAsync_ValidEntity_Success** ✅
  *Purpose:* Basic entity update

- **UpdateAsync_ConcurrencyConflict_ThrowsException** ✅
  *Purpose:* Optimistic concurrency control

- **UpdateAsync_NonExistentEntity_ThrowsException** ✅
  *Purpose:* Update non-existent entity

- **UpdateAsync_IncrementsVersion** ✅
  *Purpose:* Version increment on update

- **UpdateAsync_WithSoftDelete_CreatesNewVersion** ✅
  *Purpose:* New version for soft-delete update

- **DeleteAsync_ExistingEntity_Success** ✅
  *Purpose:* Basic entity deletion

- **DeleteAsync_NonExistentEntity_Idempotent** ✅
  *Purpose:* Idempotent delete operation

- **DeleteAsync_SoftDelete_CreatesDeletedVersion** ✅
  *Purpose:* Soft-delete version creation

- **DeleteAsync_HardDelete_RemovesPhysically** ✅
  *Purpose:* Physical deletion

### 2.3 Batch Operations Tests (8/11)

#### 2.3.1 Batch Operations Scenario (8/11)
- **CreateAsync_BatchInsert_Success** ✅
  *Purpose:* Batch entity creation

- **CreateAsync_BatchWithFailure_RollsBack** ✅
  *Purpose:* Batch rollback on failure

- **CreateAsync_CustomBatchSize_ProcessesInBatches** ❌
  *Purpose:* Custom batch size handling

- **GetAllAsync_ReturnsAllEntities** ✅
  *Purpose:* Retrieve all entities

- **GetAllAsync_FiltersSoftDeleted** ✅
  *Purpose:* Filter soft-deleted in GetAll

- **GetAllAsync_FiltersExpired** ✅
  *Purpose:* Filter expired in GetAll

- **UpdateAsync_BatchUpdate_Success** ✅
  *Purpose:* Batch entity updates

- **UpdateAsync_AppliesUpdateFunction** ✅
  *Purpose:* Update function application

- **UpdateAsync_BatchConcurrencyConflict_Fails** ✅
  *Purpose:* Batch concurrency handling

- **DeleteAsync_BatchDelete_Success** ✅
  *Purpose:* Batch entity deletion

- **DeleteAsync_MixedExistence_HandlesGracefully** ✅
  *Purpose:* Mixed key existence handling

### 2.4 List Operations Tests (0/9)

#### 2.4.1 List Operations Scenario (0/9)
- **CreateListAsync_CreatesAllEntities** ❌
  *Purpose:* List creation atomicity

- **CreateListAsync_CreatesListMappings** ❌
  *Purpose:* List mapping creation

- **GetListAsync_ReturnsAssociatedEntities** ❌
  *Purpose:* List retrieval

- **GetListAsync_MaintainsOrder** ❌
  *Purpose:* List order preservation

- **GetListAsync_UsesCacheOnSecondCall** ❌
  *Purpose:* List caching

- **UpdateListAsync_ReplacesEntireList** ❌
  *Purpose:* List replacement

- **UpdateListAsync_InvalidatesCache** ❌
  *Purpose:* Cache invalidation on update

- **DeleteListAsync_RemovesAllAssociations** ❌
  *Purpose:* List deletion

- **DeleteListAsync_PreservesEntities** ❌
  *Purpose:* Entity preservation on list delete

### 2.5 Query Operations Tests (11/11)

#### 2.5.1 Query Operations Scenario (11/11)
- **QueryAsync_SimplePredicate_FiltersCorrectly** ✅
  *Purpose:* Basic LINQ filtering

- **QueryAsync_CompoundPredicate_AppliesAllConditions** ✅
  *Purpose:* Complex LINQ expressions

- **QueryAsync_StringOperations_TranslatesCorrectly** ✅
  *Purpose:* String operation translation

- **QueryAsync_OrderBy_SortsResults** ✅
  *Purpose:* Ordering support

- **QueryAsync_SkipTake_ImplementsPaging** ✅
  *Purpose:* Skip/Take pagination

- **QueryPagedAsync_ReturnsPagedResult** ✅
  *Purpose:* Paged result structure

- **QueryPagedAsync_CalculatesTotalPages** ✅
  *Purpose:* Page calculation

- **CountAsync_WithPredicate_ReturnsCorrectCount** ✅
  *Purpose:* Filtered count

- **CountAsync_WithoutPredicate_ReturnsTotal** ✅
  *Purpose:* Total count

- **ExistsAsync_ExistingEntity_ReturnsTrue** ✅
  *Purpose:* Existence check - positive

- **ExistsAsync_NonExistentEntity_ReturnsFalse** ✅
  *Purpose:* Existence check - negative

### 2.6 Bulk Operations Tests (10/12)

#### 2.6.1 Bulk Operations Scenario (10/12)
- **BulkImportAsync_LargeDataset_Success** ✅
  *Purpose:* Bulk import functionality

- **BulkImportAsync_ConflictResolution_Skip** ✅
  *Purpose:* Skip conflict handling

- **BulkImportAsync_ConflictResolution_Overwrite** ✅
  *Purpose:* Overwrite conflict handling

- **BulkImportAsync_ProgressReporting_UpdatesProgress** ✅
  *Purpose:* Progress reporting

- **BulkImportFromFileAsync_JsonFormat_Success** ✅
  *Purpose:* JSON file import

- **BulkImportFromFileAsync_CsvFormat_Success** ✅
  *Purpose:* CSV file import

- **BulkExportAsync_StreamsData_MemoryEfficient** ✅
  *Purpose:* Streaming export

- **BulkExportAsync_ChunkedFiles_CreatesMultiple** ❌
  *Purpose:* Chunked export

- **BulkExportAsync_Compression_ReducesSize** ✅
  *Purpose:* Export compression

- **PurgeAsync_AgeBasedRetention_RemovesOld** ✅
  *Purpose:* Age-based purging

- **PurgeAsync_PreviewMode_NoChanges** ✅
  *Purpose:* Preview mode

- **PurgeAsync_VacuumAfter_ReclaimsSpace** ✅
  *Purpose:* Space reclamation

### 2.7 Transaction Tests (0/5)

#### 2.7.1 Transaction Scope Scenario (0/5)
- **BeginTransaction_CreateUpdateDelete_Atomic** ❌
  *Purpose:* Transaction atomicity

- **BeginTransaction_Rollback_NoChanges** ❌
  *Purpose:* Transaction rollback

- **BeginTransaction_NestedScope_HandlesCorrectly** ❌
  *Purpose:* Nested transactions

- **BeginTransaction_Timeout_RollsBack** ❌
  *Purpose:* Transaction timeout

- **BeginTransaction_ConcurrentAccess_Isolated** ❌
  *Purpose:* Transaction isolation

### 2.8 Audit Trail Tests (0/6)

#### 2.8.1 Audit Trail Scenario (0/6)
- **WriteAuditRecord_Create_CapturesDetails** ❌
  *Purpose:* CREATE audit record

- **WriteAuditRecord_Update_CapturesOldAndNew** ❌
  *Purpose:* UPDATE audit record

- **WriteAuditRecord_Delete_CapturesFinalState** ❌
  *Purpose:* DELETE audit record

- **WriteAuditRecord_IncludesCallerInfo** ❌
  *Purpose:* Caller info capture

- **QueryAuditTrail_ByEntity_ReturnsHistory** ❌
  *Purpose:* Entity audit history

- **QueryAuditTrail_ByUser_ReturnsUserActivity** ❌
  *Purpose:* User activity audit

### 2.9 Configuration Tests (5/5)

#### 2.9.1 Configuration Scenario (5/5)
- **FromJsonFile_LoadsConfiguration** ✅
  *Purpose:* JSON config loading

- **ApplyPragmaSettings_SetsCorrectly** ✅
  *Purpose:* PRAGMA application

- **JournalMode_WAL_EnablesWriteAheadLog** ✅
  *Purpose:* WAL mode

- **CommandTimeout_AppliesToAllCommands** ✅
  *Purpose:* Timeout configuration

- **CacheSize_AffectsPerformance** ✅
  *Purpose:* Cache size impact

### 2.10 Performance Tests (6/6)

#### 2.10.1 Performance Scenario (6/6)
- **Create_SingleEntity_MeetsTarget** ✅
  *Purpose:* Single create performance

- **Read_SingleEntity_MeetsTarget** ✅
  *Purpose:* Single read performance

- **BatchCreate_1000Entities_MeetsTarget** ✅
  *Purpose:* Batch create performance

- **Query_1000Results_MeetsTarget** ✅
  *Purpose:* Query performance

- **BulkImport_10000Entities_MeetsTarget** ✅
  *Purpose:* Bulk import performance

- **ConcurrentOperations_100Threads_NoDeadlock** ✅
  *Purpose:* Concurrency testing

### 2.11 Error Handling Tests (3/5)

#### 2.11.1 Error Handling Scenario (3/5)
- **ConnectionLoss_TransientFailure_Retries** ❌
  *Purpose:* Transient failure retry

- **ConnectionLoss_PersistentFailure_ThrowsException** ❌
  *Purpose:* Persistent failure handling

- **ConstraintViolation_ForeignKey_HandledGracefully** ✅
  *Purpose:* FK constraint violation

- **ConstraintViolation_Unique_HandledGracefully** ✅
  *Purpose:* Unique constraint violation

- **DataTypeMismatch_ThrowsMeaningfulError** ✅
  *Purpose:* Type mismatch errors

### 2.12 Integration Tests (3/4)

#### 2.12.1 Integration Scenario (3/4)
- **EndToEnd_OrderProcessingWorkflow** ❌
  *Purpose:* Complete workflow test

- **EndToEnd_DataMigration** ✅
  *Purpose:* Migration scenario

- **ProviderSwitch_SQLiteToSqlServer** ✅
  *Purpose:* Provider switching

- **HighLoad_SustainedThroughput** ✅
  *Purpose:* Load testing

---

### 2.13 Summary Statistics

| Category | Implemented | Total | Coverage |
|----------|-------------|-------|----------|
| **Entity Mapping** | 16 | 16 | 100% |
| **Core Persistence** | 22 | 24 | 92% |
| **Batch Operations** | 8 | 11 | 73% |
| **List Operations** | 0 | 9 | 0% |
| **Query Operations** | 11 | 11 | 100% |
| **Bulk Operations** | 10 | 12 | 83% |
| **Transactions** | 0 | 5 | 0% |
| **Audit Trail** | 0 | 6 | 0% |
| **Configuration** | 5 | 5 | 100% |
| **Performance** | 6 | 6 | 100% |
| **Error Handling** | 3 | 5 | 60% |
| **Integration** | 3 | 4 | 75% |
| **TOTAL** | **84** | **114** | **74%** |

### Legend
- ✅ Implemented and passing
- ❌ Not implemented
- 🔄 In progress
- ⚠️ Implemented but failing

## 3. Entity Mapping Test Scenarios

### 3.1 BaseEntityMapper Tests

#### Scenario: Property Discovery and Mapping
**Setup**: Create test entity with various property types
```csharp
[Table("TestEntity")]
public class TestEntity : IEntity<Guid>
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Count { get; set; }
    public DateTime CreatedDate { get; set; }
    public decimal? Amount { get; set; }
    [NotMapped] public string Ignored { get; set; }
}
```

**Test Cases**:
1. **Property Reflection**
   - Verify all public properties are discovered
   - Confirm [NotMapped] properties are excluded
   - Validate property type mapping to SQL types
   - Check nullable type handling

2. **Column Name Generation**
   - Test PascalCase to snake_case conversion
   - Verify custom column name attributes
   - Validate reserved keyword escaping

3. **SQL Type Mapping**
   - String → NVARCHAR(MAX)
   - Int32 → INTEGER
   - DateTime → DATETIME
   - Decimal → DECIMAL(18,6)
   - Guid → VARCHAR(36)
   - Nullable types → NULL constraint

#### Scenario: Table Attribute Configuration
**Test Cases**:
1. **Soft Delete Configuration**
   ```csharp
   [Table("TestEntity", SoftDeleteEnabled = true)]
   ```
   - Verify Version table creation
   - Check IsDeleted column addition
   - Validate version tracking

2. **Expiry Configuration**
   ```csharp
   [Table("TestEntity", ExpirySpan = "01:00:00")]
   ```
   - Verify AbsoluteExpiration column
   - Test expiry calculation on insert
   - Validate expired entity filtering

3. **Audit Trail Configuration**
   ```csharp
   [Table("TestEntity", EnableAuditTrail = true)]
   ```
   - Verify AuditRecord table creation
   - Check audit record generation
   - Validate audit data capture

### 3.2 SQLiteEntityMapper Tests

#### Scenario: SQL Generation
**Test Cases**:
1. **CREATE TABLE Generation**
   - Verify correct column definitions
   - Check primary key constraints
   - Validate composite keys for versioned entities
   - Test IF NOT EXISTS clause

2. **Index Generation**
   - Primary key index creation
   - Version column index (when soft-delete enabled)
   - IsDeleted column index
   - Composite indexes for queries

3. **Parameter Binding**
   - Test all data type conversions
   - Verify null handling
   - Check parameter sanitization
   - Validate batch parameter generation

#### Scenario: Data Mapping
**Test Cases**:
1. **MapFromReader**
   - Map all column types correctly
   - Handle DBNull values
   - Preserve precision for decimals
   - Convert DateTime to UTC

2. **Serialization/Deserialization**
   - Complex object to JSON
   - JSON to object reconstruction
   - Preserve type information
   - Handle circular references

## 4. CRUD Operation Test Scenarios

### 4.1 Create Operations

#### Scenario: Basic Entity Creation
**Setup**: Simple entity without versioning
**Test Steps**:
1. Create new entity instance
2. Call CreateAsync
3. Verify entity in database
4. Check tracking fields set

**Expected Results**:
- CreatedTime = UtcNow
- LastWriteTime = CreatedTime
- Version = 1
- Entity retrievable by Id

**Edge Cases**:
- Duplicate key insertion (expect EntityAlreadyExistsException)
- Null entity (expect ArgumentNullException)
- Database connection failure
- Transaction timeout

#### Scenario: Versioned Entity Creation
**Setup**: Entity with soft-delete enabled
**Test Steps**:
1. Create entity with soft-delete
2. Verify Version table entry
3. Check version assignment
4. Validate soft-delete flag = false

**Edge Cases**:
- Recreating soft-deleted entity
- Version table corruption
- Concurrent version generation

### 4.2 Read Operations

#### Scenario: GetAsync - Single Entity Retrieval
**Test Cases**:
1. **Existing Entity**
   - Retrieve by valid key
   - Verify all properties mapped
   - Check version matches

2. **Non-existent Entity**
   - Request invalid key
   - Verify null returned
   - No exceptions thrown

3. **Soft-Deleted Entity**
   - Delete entity (soft)
   - GetAsync returns null
   - GetByKeyAsync with includeDeleted=true returns entity

4. **Expired Entity**
   - Create expired entity
   - GetAsync returns null
   - GetByKeyAsync with includeExpired=true returns entity

#### Scenario: GetByKeyAsync - Advanced Retrieval
**Test Cases**:
1. **Version History**
   ```csharp
   GetByKeyAsync(key, includeAllVersions: true)
   ```
   - Returns all versions
   - Ordered by version DESC
   - Includes deleted versions

2. **Filtered Retrieval**
   - includeDeleted=false filters IsDeleted
   - includeExpired=false filters expired
   - Combined filters work correctly

### 4.3 Update Operations

#### Scenario: Optimistic Concurrency Control
**Test Steps**:
1. Read entity (version 1)
2. Modify entity properties
3. Another process updates (version 2)
4. Attempt update with version 1
5. Expect ConcurrencyConflictException

**Test Cases**:
1. **Successful Update**
   - Version matches
   - Properties updated
   - Version incremented
   - LastWriteTime updated

2. **Concurrent Modification**
   - Version mismatch detected
   - Original data preserved
   - Exception with details

3. **Soft-Delete Update**
   - New version created
   - Old version preserved
   - Version chain maintained

### 4.4 Delete Operations

#### Scenario: Soft Delete
**Setup**: Entity with SoftDeleteEnabled=true
**Test Steps**:
1. Create entity
2. Delete entity
3. Verify new version with IsDeleted=true
4. Original version preserved

**Test Cases**:
1. **First Delete**
   - New version created
   - IsDeleted flag set
   - Entity not retrievable by GetAsync

2. **Already Deleted**
   - Second delete is idempotent
   - No new version created
   - Returns success

#### Scenario: Hard Delete
**Setup**: Entity with SoftDeleteEnabled=false
**Test Steps**:
1. Create entity
2. Delete entity
3. Verify physical removal
4. No version history

## 5. Batch Operation Test Scenarios

### 5.1 Batch Create

#### Scenario: Large Batch Insert
**Setup**: 10,000 entities to insert
**Test Cases**:
1. **Default Batch Size**
   - All entities in single transaction
   - Rollback on any failure
   - Performance measurement

2. **Custom Batch Size (100)**
   - 100 transactions of 100 entities
   - Partial success possible
   - Error aggregation

3. **Duplicate Key Handling**
   - One duplicate in batch
   - Entire batch rolls back
   - Clear error reporting

### 5.2 Batch Update

#### Scenario: Update Function Application
**Test Steps**:
```csharp
UpdateAsync(entities, e => { e.Status = "Processed"; return e; })
```

**Test Cases**:
1. **Successful Batch Update**
   - Function applied to all
   - Versions incremented
   - Audit records created

2. **Concurrency Conflicts**
   - Some entities modified
   - Batch fails with details
   - Unmodified entities preserved

### 5.3 Batch Delete

#### Scenario: Mass Deletion
**Test Cases**:
1. **Mixed Existence**
   - Some keys exist, some don't
   - Idempotent operation
   - Count reflects actual deletions

2. **Soft vs Hard Delete**
   - Soft creates versions
   - Hard removes physically
   - Performance comparison

## 6. List Operation Test Scenarios

### 6.1 List Management

#### Scenario: Shopping Cart Operations
**Setup**: Shopping cart with items
```csharp
var cartKey = "user:123:cart";
var items = new List<CartItem> { ... };
```

**Test Cases**:
1. **CreateListAsync**
   - All items created atomically
   - EntryListMapping records created
   - List retrievable by key

2. **GetListAsync**
   - Returns all associated items
   - Maintains insertion order
   - Filters deleted/expired

3. **UpdateListAsync**
   - Replaces entire list
   - Old mappings removed
   - New mappings created

4. **DeleteListAsync**
   - Removes all associations
   - Entities may be preserved
   - Mappings deleted

### 6.2 List Caching

#### Scenario: Cache Performance
**Test Cases**:
1. **Cache Hit**
   - First call loads from DB
   - Second call from cache
   - Performance improvement

2. **Cache Invalidation**
   - Update invalidates cache
   - Next read refreshes
   - Consistency maintained

## 7. Query Operation Test Scenarios

### 7.1 LINQ Expression Queries

#### Scenario: Complex Filtering
**Test Cases**:
1. **Simple Predicate**
   ```csharp
   QueryAsync(e => e.Status == "Active")
   ```
   - Translates to SQL WHERE
   - Parameterized query
   - Index usage

2. **Compound Conditions**
   ```csharp
   QueryAsync(e => e.Status == "Active" && e.Amount > 100)
   ```
   - AND/OR logic preserved
   - Multiple parameters
   - Correct precedence

3. **String Operations**
   ```csharp
   QueryAsync(e => e.Name.StartsWith("Test"))
   ```
   - LIKE operator usage
   - Case sensitivity
   - Pattern escaping

### 7.2 Pagination

#### Scenario: Large Dataset Paging
**Test Cases**:
1. **Basic Pagination**
   ```csharp
   QueryPagedAsync(null, pageSize: 50, pageNumber: 1)
   ```
   - Returns first 50
   - Total count included
   - Page metadata correct

2. **Filtered Pagination**
   - Predicate applied first
   - Then pagination
   - Consistent ordering

3. **Edge Cases**
   - Page beyond data
   - Page size = 0
   - Negative page number

### 7.3 Aggregation

#### Scenario: Count and Exists
**Test Cases**:
1. **CountAsync**
   - With/without predicate
   - Soft-delete aware
   - Performance optimized

2. **ExistsAsync**
   - Short-circuits on first match
   - More efficient than Count > 0
   - Predicate support

## 8. Bulk Operation Test Scenarios

### 8.1 Bulk Import

#### Scenario: Data Migration
**Setup**: 1 million records to import
**Test Cases**:
1. **Staging Table Import**
   - Create temporary table
   - Bulk insert to staging
   - Validate and move to main

2. **Conflict Resolution**
   - Skip: Ignore duplicates
   - Overwrite: Replace existing
   - Merge: Combine data

3. **Progress Reporting**
   ```csharp
   var progress = new Progress<BulkOperationProgress>(p =>
       Console.WriteLine($"{p.PercentComplete}%"));
   ```
   - Regular updates
   - ETA calculation
   - Cancellation support

### 8.2 Bulk Export

#### Scenario: Data Archival
**Test Cases**:
1. **Format Options**
   - JSON export
   - CSV export
   - Binary format
   - Compression

2. **Chunked Export**
   - Split large datasets
   - Multiple files
   - Manifest generation

3. **Streaming Export**
   - Memory efficient
   - No full load
   - Progressive writing

### 8.3 Purge Operations

#### Scenario: Retention Policy
**Setup**: 90-day retention policy
**Test Cases**:
1. **Age-Based Purge**
   ```csharp
   PurgeAsync(e => e.CreatedTime < DateTime.UtcNow.AddDays(-90))
   ```
   - Identifies old records
   - Preview mode first
   - Actual deletion

2. **Version Cleanup**
   - Keep latest N versions
   - Remove old versions
   - Preserve current state

3. **Space Reclamation**
   - VACUUM after purge
   - Measure space freed
   - Performance impact

## 9. Transaction Test Scenarios

### 9.1 Transaction Scope

#### Scenario: Multi-Entity Transaction
**Test Steps**:
```csharp
using (var scope = provider.BeginTransaction())
{
    await scope.CreateAsync(entity1);
    await scope.UpdateAsync(entity2);
    await scope.DeleteAsync(entity3.Id);
    await scope.CommitAsync();
}
```

**Test Cases**:
1. **Successful Transaction**
   - All operations succeed
   - Commit persists changes
   - Atomic execution

2. **Rollback on Error**
   - One operation fails
   - Automatic rollback
   - No partial changes

3. **Nested Transactions**
   - Inner scope handling
   - Savepoint support
   - Rollback behavior

### 9.2 Isolation Levels

#### Scenario: Concurrent Access
**Test Cases**:
1. **Read Committed**
   - Default isolation
   - No dirty reads
   - Allows non-repeatable reads

2. **Serializable**
   - Highest isolation
   - Prevents phantoms
   - Performance impact

## 10. Audit Trail Test Scenarios

### 10.1 Audit Record Generation

#### Scenario: Complete Audit Trail
**Test Cases**:
1. **CREATE Audit**
   - Record creation details
   - Caller information
   - Entity size tracking

2. **UPDATE Audit**
   - Old and new values
   - Version changes
   - Modification timestamp

3. **DELETE Audit**
   - Deletion reason
   - Final state capture
   - Soft vs hard delete

### 10.2 Audit Query

#### Scenario: Compliance Reporting
**Test Cases**:
1. **Entity History**
   - All changes to entity
   - Chronological order
   - User attribution

2. **User Activity**
   - All actions by user
   - Time period filter
   - Operation types

## 11. Performance Test Scenarios

### 11.1 Load Testing

#### Scenario: High Throughput
**Test Cases**:
1. **Concurrent Writes**
   - 100 concurrent threads
   - 1000 ops/second target
   - Contention handling

2. **Read Performance**
   - Cache effectiveness
   - Index usage
   - Query optimization

### 11.2 Stress Testing

#### Scenario: Resource Limits
**Test Cases**:
1. **Memory Pressure**
   - Large result sets
   - Streaming validation
   - Memory leak detection

2. **Connection Pool**
   - Max connections
   - Pool exhaustion
   - Recovery behavior

## 12. Error Handling Test Scenarios

### 12.1 Database Failures

#### Scenario: Connection Loss
**Test Cases**:
1. **Transient Failures**
   - Retry logic
   - Exponential backoff
   - Success after retry

2. **Persistent Failures**
   - Max retry exceeded
   - Graceful degradation
   - Error reporting

### 12.2 Data Integrity

#### Scenario: Corruption Detection
**Test Cases**:
1. **Constraint Violations**
   - Foreign key errors
   - Unique constraints
   - Check constraints

2. **Data Type Mismatches**
   - Type conversion errors
   - Truncation warnings
   - Precision loss

## 13. Configuration Test Scenarios

### 13.1 SqliteConfiguration

#### Scenario: Configuration Options
**Test Cases**:
1. **PRAGMA Settings**
   - Journal mode (WAL, DELETE)
   - Synchronous mode
   - Cache size
   - Page size

2. **Performance Tuning**
   - Command timeout
   - Busy timeout
   - Connection pooling
   - Index creation

### 13.2 JSON Configuration

#### Scenario: External Configuration
**Test Cases**:
1. **File-Based Config**
   ```json
   {
     "journalMode": "WAL",
     "cacheSize": 10000,
     "commandTimeout": 30
   }
   ```
   - Load from file
   - Apply settings
   - Validation

2. **Environment Override**
   - Config precedence
   - Environment variables
   - Default values

## 14. Edge Case Test Scenarios

### 14.1 Boundary Conditions

**Test Cases**:
1. **Empty Collections**
   - Batch operations with empty lists
   - Query with no results
   - Null handling

2. **Large Values**
   - Maximum string length
   - Integer overflow
   - Decimal precision

3. **Special Characters**
   - SQL injection attempts
   - Unicode handling
   - Reserved keywords

### 14.2 Race Conditions

**Test Cases**:
1. **Concurrent Creates**
   - Same key simultaneously
   - Version generation race
   - Audit table contention

2. **Read-Write Conflicts**
   - Read during update
   - Consistency guarantees
   - Lock timeout

## 15. Integration Test Scenarios

### 15.1 End-to-End Workflows

#### Scenario: Order Processing
**Test Steps**:
1. Create order (transaction)
2. Add items (list operation)
3. Update status (optimistic lock)
4. Query orders (pagination)
5. Archive old orders (bulk export)
6. Purge archived (retention)

**Validation**:
- Data consistency
- Audit trail complete
- Performance metrics
- Error recovery

### 15.2 Multi-Provider Tests

#### Scenario: Provider Switching
**Test Cases**:
1. **SQLite to SQL Server**
   - Data migration
   - Feature parity
   - Performance comparison

2. **Provider Abstraction**
   - Interface compliance
   - Behavior consistency
   - Error handling

## 16. Test Data Management

### 16.1 Test Data Setup

**Strategies**:
1. **Fixture Data**
   - Predefined test entities
   - Consistent state
   - Repeatable tests

2. **Random Generation**
   - Property-based testing
   - Edge case discovery
   - Large dataset creation

### 16.2 Test Cleanup

**Approaches**:
1. **Transaction Rollback**
   - Test in transaction
   - Auto-rollback
   - No cleanup needed

2. **Database Reset**
   - Truncate tables
   - Reset sequences
   - Clear cache

## 17. Test Automation

### 17.1 Unit Test Structure

```csharp
[TestClass]
public class SQLitePersistenceProviderTests
{
    private SQLitePersistenceProvider<TestEntity, Guid> provider;
    private string connectionString;

    [TestInitialize]
    public async Task Setup()
    {
        connectionString = "Data Source=:memory:";
        provider = new SQLitePersistenceProvider<TestEntity, Guid>(connectionString);
        await provider.InitializeAsync();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await provider.DisposeAsync();
    }

    [TestMethod]
    public async Task CreateAsync_ValidEntity_Success()
    {
        // Arrange
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "Test" };

        // Act
        var result = await provider.CreateAsync(entity, new CallerInfo());

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(entity.Id, result.Id);
        Assert.AreEqual(1, result.Version);
    }
}
```

### 17.2 Integration Test Structure

```csharp
[TestClass]
[TestCategory("Integration")]
public class PersistenceIntegrationTests
{
    private static string DatabasePath;

    [ClassInitialize]
    public static void ClassSetup(TestContext context)
    {
        DatabasePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        if (File.Exists(DatabasePath))
            File.Delete(DatabasePath);
    }
}
```

## 18. Performance Benchmarks

### 18.1 Benchmark Scenarios

```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net472)]
public class PersistenceBenchmarks
{
    [Benchmark]
    public async Task Create_SingleEntity()
    {
        // Benchmark implementation
    }

    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    public async Task Create_BatchEntities(int count)
    {
        // Benchmark implementation
    }
}
```

### 18.2 Performance Targets

| Operation | Target | Maximum |
|-----------|--------|---------|
| Single Create | < 10ms | 50ms |
| Single Read | < 5ms | 20ms |
| Batch Create (1000) | < 500ms | 2000ms |
| Query (1000 results) | < 100ms | 500ms |
| Bulk Import (10000) | < 5s | 30s |

## 19. Conclusion

This comprehensive test scenario document covers all aspects of the persistence layer implementation. Each scenario should be implemented as automated tests to ensure reliability, performance, and correctness of the persistence layer. Regular execution of these tests will help maintain code quality and catch regressions early in the development cycle.