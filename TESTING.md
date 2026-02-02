# ShiftPay Backend Testing Documentation

## Table of Contents

- [Overview](#overview)
- [Test Architecture](#test-architecture)
- [Prerequisites](#prerequisites)
- [Running Tests](#running-tests)
- [Test Categories](#test-categories)
  - [Unit Tests (Controller Tests)](#unit-tests-controller-tests)
  - [Integration Tests](#integration-tests)
  - [Database Infrastructure Tests](#database-infrastructure-tests)
- [Test Fixtures & Helpers](#test-fixtures--helpers)
- [Test Isolation Strategy](#test-isolation-strategy)
- [Writing New Tests](#writing-new-tests)

---

## Overview

The test project (`ShiftPay_Backend.Tests`) uses **xUnit v3** as the testing framework and follows a multi-layered testing strategy:

| Test Type | Purpose | Database | Authentication |
|-----------|---------|----------|----------------|
| Unit Tests | Test controller logic in isolation | Cosmos DB Emulator | Mocked user context |
| Integration Tests | Test full HTTP pipeline | AppHost (Aspire) | Real middleware (fake auth) |
| Infrastructure Tests | Verify database constraints | Cosmos DB Emulator | N/A |

---

## Test Architecture

```
ShiftPay_Backend.Tests/
├── CosmosDbTestFixture.cs           # Shared Cosmos DB emulator fixture
├── ControllerTestHelper.cs          # Helper for creating controllers with test context
├── ShiftsControllerTests.cs         # Unit tests for ShiftsController
├── WorkInfosControllerTests.cs      # Unit tests for WorkInfosController
├── ShiftTemplatesControllerTests.cs # Unit tests for ShiftTemplatesController
├── DatabaseInfrastructureTests.cs   # Cosmos DB constraint tests
├── IntegrationTests.cs              # HTTP tests without authentication
├── AuthenticatedIntegrationTests.cs # HTTP tests with fake authentication
└── appsettings.Test.json            # Test environment configuration
```

---

## Prerequisites

### Azure Cosmos DB Emulator

Unit tests and infrastructure tests require the **Azure Cosmos DB Emulator** running locally.

- **Endpoint:** `https://localhost:8081/`
- **Database:** `ShiftPay_Test` (created automatically)

**Installation:**  
Download from [Azure Cosmos DB Emulator](https://learn.microsoft.com/en-us/azure/cosmos-db/local-emulator)

### .NET Aspire

Integration tests use **.NET Aspire's `DistributedApplicationTestingBuilder`** to spin up the full application stack.

---

## Running Tests

### Run All Tests

```bash
dotnet test
```

### Run Specific Test Category

```bash
# Unit tests only (Cosmos DB collection)
dotnet test --filter "Collection=CosmosDb"

# Integration tests only
dotnet test --filter "Collection=Aspire Integration"
```

### Run with Code Coverage

```bash
dotnet tool install -g dotnet-coverage
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test
```

---

## Test Categories

### Unit Tests (Controller Tests)

Test controller methods directly with a real Cosmos DB Emulator database but mocked HTTP context.

**Test Classes:**
- `ShiftsControllerTests`
- `WorkInfosControllerTests`
- `ShiftTemplatesControllerTests`

**Collection:** `[Collection("CosmosDb")]`

**Example:**

```csharp
[Collection("CosmosDb")]
public class ShiftsControllerTests
{
	private readonly CosmosDbTestFixture _fixture;
	private readonly string _testUserId = $"shifts-test-{Guid.NewGuid():N}";

	public ShiftsControllerTests(CosmosDbTestFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task GetShifts_WhenNoShiftsExist_ReturnsEmptyList()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

		// Act
		var result = await controller.GetShifts(null, null, null, null, null, null);

		// Assert
		var okResult = Assert.IsType<OkObjectResult>(result.Result);
		var shifts = Assert.IsAssignableFrom<IEnumerable<ShiftDTO>>(okResult.Value);
		Assert.Empty(shifts);
	}
}
```

**Covered Scenarios:**
- CRUD operations for each controller
- Filtering and query parameters (Shifts)
- Upsert behavior (WorkInfos, ShiftTemplates)
- Validation error handling
- Not found scenarios

---

### Integration Tests

Test the full HTTP pipeline using Aspire's testing infrastructure.

**Test Classes:**
- `IntegrationTests` — Tests without authentication (expects `401 Unauthorized`)
- `AuthenticatedIntegrationTests` — Tests with fake authentication enabled

**Collection:** `[Collection("Aspire Integration")]` (parallelization disabled)

**Example:**

```csharp
[Collection("Aspire Integration")]
public class IntegrationTests : IAsyncLifetime
{
	private DistributedApplication? _app;
	private HttpClient? _httpClient;

	public async ValueTask InitializeAsync()
	{
		var appHost = await DistributedApplicationTestingBuilder
			.CreateAsync<Projects.ShiftPay_Backend_AppHost>();
		
		_app = await appHost.BuildAsync();
		await _app.StartAsync();
		_httpClient = _app.CreateHttpClient("shiftpay-backend");
	}

	[Fact]
	public async Task GetShifts_WithoutAuth_ReturnsUnauthorized()
	{
		var response = await _httpClient!.GetAsync("/api/shifts");
		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}
}
```

**Covered Scenarios:**
- Health check endpoint
- OpenAPI spec availability
- Authentication enforcement (401 for unauthenticated requests)
- Full CRUD cycle with fake authentication
- Round-trip data integrity

---

### Database Infrastructure Tests

Verify Cosmos DB container setup and unique key constraints.

**Test Class:** `DatabaseInfrastructureTests`

**Collection:** `[Collection("CosmosDb")]`

**Covered Scenarios:**
- Container existence verification
- Unique key constraint on `WorkInfos.Workplace` per user
- Unique key constraint on `ShiftTemplates.TemplateName` per user
- Cross-user isolation (same name allowed for different users)

---

## Test Fixtures & Helpers

### CosmosDbTestFixture

Shared fixture that manages the Cosmos DB Emulator connection. Initialized once per test run.

**Behavior:**
1. Deletes existing `ShiftPay_Test` database (ensures clean state)
2. Creates fresh database with containers:
   - `Shifts` — Partition keys: `/UserId`, `/YearMonth`, `/Day`
   - `WorkInfos` — Partition key: `/UserId`, Unique key: `/Workplace`
   - `ShiftTemplates` — Partition key: `/UserId`, Unique key: `/TemplateName`
3. Runs EF Core schema initialization

```csharp
[CollectionDefinition("CosmosDb")]
public class CosmosDbCollection : ICollectionFixture<CosmosDbTestFixture> { }
```

### ControllerTestHelper

Static helper for creating controller instances with test user context.

**Methods:**
- `CreateShiftsController(context, userId)`
- `CreateWorkInfosController(context, userId)`
- `CreateShiftTemplatesController(context, userId)`
- `CreateTestUser(userId, userName)`

**Default Test User:**
- User ID: `test-user-id`
- User Name: `Test User`

---

## Test Isolation Strategy

Tests achieve isolation through **unique user IDs** rather than database cleanup between tests.

```csharp
// Each test class generates a unique user ID prefix
private readonly string _testUserId = $"shifts-test-{Guid.NewGuid():N}";
```

**Benefits:**
- Tests can run in parallel within the same collection
- No cleanup overhead between tests
- Database is only reset at the start of each test run

**Rules:**
- Each test class uses a unique user ID prefix
- Data created by one test is invisible to other tests (different partition)
- Database is wiped at fixture initialization (start of test run)

---

## Writing New Tests

### 1. Unit Test for a Controller Method

```csharp
[Collection("CosmosDb")]
public class MyControllerTests
{
	private readonly CosmosDbTestFixture _fixture;
	private readonly string _testUserId = $"mytest-{Guid.NewGuid():N}";

	public MyControllerTests(CosmosDbTestFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task MyMethod_WhenCondition_ReturnsExpectedResult()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateMyController(context, _testUserId);

		// Act
		var result = await controller.MyMethod();

		// Assert
		var okResult = Assert.IsType<OkObjectResult>(result.Result);
		// ... assertions
	}
}
```

### 2. Integration Test with HTTP Client

```csharp
[Collection("Aspire Integration")]
public class MyIntegrationTests : IAsyncLifetime
{
	private DistributedApplication? _app;
	private HttpClient? _httpClient;

	public async ValueTask InitializeAsync()
	{
		Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
		var appHost = await DistributedApplicationTestingBuilder
			.CreateAsync<Projects.ShiftPay_Backend_AppHost>();
		_app = await appHost.BuildAsync();
		await _app.StartAsync();
		_httpClient = _app.CreateHttpClient("shiftpay-backend");
	}

	public async ValueTask DisposeAsync()
	{
		_httpClient?.Dispose();
		if (_app is not null) await _app.DisposeAsync();
		Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
	}

	[Fact]
	public async Task MyEndpoint_ReturnsExpectedResponse()
	{
		var response = await _httpClient!.GetAsync("/api/myendpoint");
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}
}
```

### Test Naming Convention

Follow the pattern: `MethodName_WhenCondition_ExpectedBehavior`

Examples:
- `GetShifts_WhenNoShiftsExist_ReturnsEmptyList`
- `PostShift_WithValidData_ReturnsCreatedShift`
- `DeleteWorkInfo_WithPayRate_RemovesOnlyThatPayRate`
