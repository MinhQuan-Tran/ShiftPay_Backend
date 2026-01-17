# ShiftPay Backend Test Suite

This test suite provides comprehensive coverage for all API controllers in the ShiftPay_Backend project: `ShiftsController`, `ShiftTemplatesController`, and `WorkInfosController`.

## Architecture Overview

### Test Structure

The test suite is organized as follows:

- **`ShiftPayTestFixture`**: Shared test fixture that manages the test environment lifecycle
- **`ShiftControllerTests`**: Tests for `ShiftsController` endpoints
- **`ShiftTemplateControllerTests`**: Tests for `ShiftTemplatesController` endpoints  
- **`WorkInfoControllerTests`**: Tests for `WorkInfosController` endpoints

Each test class uses `IClassFixture<ShiftPayTestFixture>` to share a common test environment while maintaining isolation.

## Test Isolation Strategy

### Shared Database, Unique Partition Keys

The test suite uses a **single shared Cosmos DB database** (`ShiftPay_Test_Shared`) with the same containers across all tests:

- `Shifts` container (partition key: `{ UserId, YearMonth, Day }`)
- `WorkInfos` container (partition key: `UserId`)
- `ShiftTemplates` container (partition key: `UserId`)

**Test isolation is achieved through unique `UserId` values** rather than creating separate databases or containers:

1. Each `ShiftPayTestFixture` instance generates a **unique `UserId`** (`test-user-{guid}`)
2. Test data is seeded with this unique `UserId`
3. The `FakeAuthHandler` reads the `X-Test-UserId` HTTP header to authenticate requests with the correct `UserId`
4. Since Cosmos DB partitioning is based on `UserId`, tests are naturally isolated at the partition level

### Benefits

✅ **True isolation**: Tests cannot interfere with each other due to separate partition keys  
✅ **Performance**: No database creation/deletion overhead between test runs  
✅ **Realism**: Tests use the same database/container structure as production  
✅ **Parallelization**: Tests can run in parallel safely  

## Test Configuration

### Prerequisites

- **Azure Cosmos DB Emulator** must be running locally at `https://localhost:8081/`
- The emulator uses the well-known authentication key (configured in `appsettings.Test.json`)

### Configuration Files

**`appsettings.Test.json`** (in ShiftPay_Backend.Tests):
```json
{
  "Cosmos": {
    "Endpoint": "https://localhost:8081/",
    "Key": "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
    "DatabaseName": "ShiftPay_Test_Shared"
  }
}
```

**`appsettings.Test.json`** (in ShiftPay_Backend):
```json
{
  "Authentication": {
    "UseFake": true
  },
  "CosmosDB-ConnectionString-Primary": "..."
}
```

## How It Works

### 1. Fixture Initialization

When a test class is instantiated:

1. `ShiftPayTestFixture.InitializeAsync()` is called
2. A unique `UserId` is generated for this fixture instance
3. The shared Cosmos database and containers are provisioned (if not already present)
4. The Aspire app host is started with fake authentication enabled
5. Test data is seeded with the unique `UserId`
6. An `HttpClient` is created with the `X-Test-UserId` header set

### 2. Request Authentication

When a test makes an HTTP request:

1. The `HttpClient` includes the `X-Test-UserId` header
2. `FakeAuthHandler` reads this header and creates a `ClaimsPrincipal` with the specified `UserId`
3. Controllers receive the authenticated user identity with the correct `UserId`
4. All database queries are scoped to this `UserId` via partition keys

### 3. Test Data Isolation

Each test class has its own:

- Unique `UserId` (e.g., `test-user-a1b2c3d4...`)
- Dedicated test data seeded under that `UserId`
- Partition-isolated queries (via Cosmos DB's natural partitioning)

Tests from different classes never see each other's data because they query different partitions.

### 4. Per-Test Reset

Each test method uses `IAsyncLifetime`:

```csharp
public async Task InitializeAsync()
{
    await _fixture.ClearAllShiftsAsync();
    await _fixture.SeedShiftTestDataAsync();
}
```

This ensures tests within a class start with a known, consistent state.

## Running the Tests

### Start Cosmos DB Emulator

**Windows:**
```powershell
# The Azure Cosmos DB Emulator should be installed and running
# Default endpoint: https://localhost:8081/
```

**Linux/macOS (via Docker):**
```bash
docker run -p 8081:8081 -p 10251:10251 -p 10252:10252 -p 10253:10253 -p 10254:10254 \
  --name=cosmosdb-emulator \
  -e AZURE_COSMOS_EMULATOR_PARTITION_COUNT=2 \
  -e AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE=false \
  -e AZURE_COSMOS_EMULATOR_IP_ADDRESS_OVERRIDE=127.0.0.1 \
  -it mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
```

### Run Tests

```bash
# Run all tests
dotnet test

# Run tests for a specific controller
dotnet test --filter "FullyQualifiedName~ShiftControllerTests"
dotnet test --filter "FullyQualifiedName~ShiftTemplateControllerTests"
dotnet test --filter "FullyQualifiedName~WorkInfoControllerTests"

# Run a specific test
dotnet test --filter "FullyQualifiedName~ShiftControllerTests.GetAllShifts_ReturnsSuccess"
```

## Key Implementation Details

### FakeAuthHandler

Located in `ShiftPay_Backend/Auth/FakeAuthHandler.cs`, this handler:

- Reads the `X-Test-UserId` HTTP header
- Falls back to `"test-user-id"` if no header is present (for manual testing)
- Creates authentication claims with the specified `UserId`

```csharp
var userIdFromHeader = Context.Request.Headers["X-Test-UserId"].FirstOrDefault();
var userId = string.IsNullOrEmpty(userIdFromHeader) ? "test-user-id" : userIdFromHeader;
```

### Test Fixture Lifecycle

- **One fixture instance per test class** (`IClassFixture<ShiftPayTestFixture>`)
- Each instance gets a unique `UserId`
- Database/containers are shared but data is isolated by partition key
- App host is started once per fixture and reused across tests in that class

### Seeding Methods

- `SeedShiftTestDataAsync()`: Seeds 4-5 test shifts
- `SeedWorkInfoTestDataAsync()`: Seeds 2-3 test work infos
- `SeedShiftTemplateTestDataAsync()`: Seeds 2-3 test templates

All use `_testUserId` for the primary user and `_testUserIdOther` for cross-user isolation testing.

## Test Coverage

### ShiftsController

- ✅ GET all shifts (with filtering by year/month/day/ids/time range)
- ✅ GET shift by ID
- ✅ POST create shift
- ✅ POST batch create shifts
- ✅ PUT update shift
- ✅ DELETE shift
- ✅ DELETE multiple shifts

### ShiftTemplatesController

- ✅ GET all templates
- ✅ GET template by name
- ✅ POST create template
- ✅ PUT update template
- ✅ DELETE template

### WorkInfosController

- ✅ GET all work infos
- ✅ GET work info by workplace
- ✅ POST create/update work info
- ✅ DELETE work info (entire or specific pay rate)

## Troubleshooting

### "Connection refused" or "Cosmos endpoint not available"

Ensure the Cosmos DB Emulator is running:
- Windows: Check the system tray
- Docker: `docker ps` should show the emulator container

### Tests fail with "Conflict" errors

This may happen if tests are interrupted. Clean up:
```bash
# Delete and recreate the test database using Cosmos DB Emulator UI
# Or via Azure Cosmos DB Explorer
```

### Tests are slow

- The first test run provisions the database/containers (one-time cost)
- Subsequent runs are fast as they reuse the existing database
- Consider running tests in parallel with `dotnet test --parallel`

## Design Rationale

### Why Unique UserIds Instead of Separate Databases?

1. **Performance**: Creating/deleting databases is expensive
2. **Realism**: Production uses partition keys for isolation, not separate databases
3. **Simplicity**: No need to manage database lifecycle or cleanup
4. **Scalability**: Cosmos DB is designed for partition-based isolation

### Why Not In-Memory Database?

The requirements explicitly state:
> Tests must run against the **Azure Cosmos DB Emulator**

This ensures tests validate:
- Actual Cosmos DB behavior (partition keys, unique constraints, etc.)
- EF Core Cosmos provider functionality
- Production-like performance characteristics

## Future Enhancements

- [ ] Add integration tests for concurrency/conflict scenarios
- [ ] Add performance benchmarks
- [ ] Add tests for batch operations edge cases
- [ ] Consider adding mutation testing for higher confidence
