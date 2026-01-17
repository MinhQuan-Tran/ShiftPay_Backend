# Test Suite Refactoring Summary

## Overview

The test suite for ShiftPay_Backend has been refactored to use a **shared Cosmos DB database** with **partition-based test isolation** instead of creating separate databases per test run.

## What Was Changed

### 1. ShiftPayTestFixture.cs

**Before:**
- Created a unique database for each test run: `ShiftPay_Test_{Guid}`
- Deleted and recreated the database on each initialization
- Used hardcoded `"test-user-id"` for all test data

**After:**
- Uses a single shared database: `ShiftPay_Test_Shared`
- Database and containers are created once and reused
- Each fixture instance generates a unique `UserId`: `test-user-{Guid}`
- Test data is seeded with the unique `UserId`
- HttpClient includes `X-Test-UserId` header for authentication

**Key changes:**
```csharp
// Unique UserId per fixture instance
private readonly string _testUserId = $"test-user-{Guid.NewGuid():N}";
private readonly string _testUserIdOther = $"test-user-other-{Guid.NewGuid():N}";

// Shared database name
private const string SharedTestDatabaseName = "ShiftPay_Test_Shared";

// No longer delete/recreate database
// await existingDb.DeleteAsync(); // REMOVED

// Add custom header to client
Client.DefaultRequestHeaders.Add("X-Test-UserId", _testUserId);
```

### 2. FakeAuthHandler.cs

**Before:**
- Always returned hardcoded `"test-user-id"`

**After:**
- Reads `X-Test-UserId` header from HTTP request
- Falls back to `"test-user-id"` if header is not present
- Creates claims with the appropriate `UserId`

**Key changes:**
```csharp
var userIdFromHeader = Context.Request.Headers["X-Test-UserId"].FirstOrDefault();
var userId = string.IsNullOrEmpty(userIdFromHeader) ? "test-user-id" : userIdFromHeader;
```

### 3. appsettings.Test.json

**Before:**
```json
{
  "Cosmos": {
    "DatabaseName": "ShiftPay"
  }
}
```

**After:**
```json
{
  "Cosmos": {
    "DatabaseName": "ShiftPay_Test_Shared"
  }
}
```

**Note:** The database name was changed to use a shared database. Test isolation is now achieved via unique UserId partition keys per test fixture instance, which is explained in the README.md documentation.

### 4. README.md (New)

Added comprehensive documentation covering:
- Test architecture and isolation strategy
- Configuration requirements
- How the shared database approach works
- Running tests with Cosmos DB Emulator
- Troubleshooting guide
- Design rationale

## Why These Changes Were Made

### Problem with Previous Approach

The original implementation created a **unique database** for each test run:
```csharp
private readonly string _cosmosDatabaseName = $"ShiftPay_Test_{Guid.NewGuid():N}";
```

While this provided isolation, it had several issues:
- ❌ Violated requirement: "use the same Cosmos database and same containers"
- ❌ High overhead: Creating/deleting databases is expensive
- ❌ Not production-like: Production uses partition keys for isolation, not separate databases
- ❌ Cleanup issues: Failed test runs could leave orphaned databases

### Benefits of New Approach

✅ **Meets all requirements**: Single shared database, same containers, partition-based isolation  
✅ **Better performance**: No database creation/deletion overhead  
✅ **More realistic**: Mirrors production behavior with partition-based isolation  
✅ **Cleaner**: No orphaned databases to clean up  
✅ **Scalable**: Tests can run in parallel safely  

## How Test Isolation Works

### Partition Key Architecture

All containers use `UserId` as part of their partition key:
- `Shifts`: `{ UserId, YearMonth, Day }`
- `WorkInfos`: `{ UserId }`
- `ShiftTemplates`: `{ UserId }`

### Isolation Mechanism

1. **Each test fixture instance** gets a unique `UserId` (e.g., `test-user-a1b2c3d4...`)
2. **Test data is seeded** with this unique `UserId`
3. **HTTP requests include** the `X-Test-UserId` header
4. **FakeAuthHandler** authenticates with the specified `UserId`
5. **Controllers query** using `WithPartitionKey(userId)`, naturally isolating data

Since Cosmos DB physically isolates data by partition key, tests **cannot interfere** with each other even when running in parallel.

## Testing the Changes

### Prerequisites
- Azure Cosmos DB Emulator running at `https://localhost:8081/`

### Running Tests
```bash
# All tests
dotnet test

# Specific controller
dotnet test --filter "FullyQualifiedName~ShiftControllerTests"

# Parallel execution (safe due to partition isolation)
dotnet test --parallel
```

### Verification

To verify isolation, you can:
1. Run tests in parallel: `dotnet test --parallel`
2. Check Cosmos DB Emulator Data Explorer - you'll see data for multiple unique `UserId` values
3. Each test class should have its own partition key space

## Impact on Existing Tests

**No changes required** to existing test code! The isolation mechanism is transparent:
- Tests continue to use `_fixture.Client` for HTTP requests
- Tests continue to seed/clear data via fixture methods
- Tests continue to assert on responses as before

The only difference is that each test class now operates in its own `UserId` partition automatically.

## Backward Compatibility

The changes are **backward compatible**:
- If `X-Test-UserId` header is not provided, `FakeAuthHandler` falls back to `"test-user-id"`
- This allows manual testing and debugging without modifying test code
- Existing integration tests or manual API calls continue to work

## Rollback Plan (if needed)

To rollback to the previous approach, revert these commits:
```bash
git revert 815a4cc 54c8ba5 c9c02f2
```

However, this would violate the requirement: "tests must use the same Cosmos database and same containers."

## Conclusion

The refactored test suite now:
- ✅ Uses a single shared Cosmos database (`ShiftPay_Test_Shared`)
- ✅ Uses the same containers across all tests
- ✅ Achieves isolation via partition keys (unique `UserId` per fixture)
- ✅ Meets all requirements from the problem statement
- ✅ Provides better performance and realism
- ✅ Supports parallel test execution

No changes to existing test code were necessary - the isolation mechanism is transparent and automatic.
