using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShiftPay_Backend.Data;

namespace ShiftPay_Backend.Tests;

/// <summary>
/// Shared test fixture that initializes a single Cosmos DB connection for all tests.
/// Uses the Azure Cosmos DB Emulator with the "ShiftPay_Test" database.
/// The database is deleted and recreated at the start of each test run.
/// </summary>
public class CosmosDbTestFixture : IAsyncLifetime
{
	private const string EmulatorEndpoint = "https://localhost:8081/";
	private const string EmulatorKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
	private const string DatabaseName = "ShiftPay_Test"; // Use separate database for tests

	private CosmosClient? _cosmosClient;

	public ShiftPay_BackendContext CreateContext()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Cosmos:Endpoint"] = EmulatorEndpoint,
				["Cosmos:Key"] = EmulatorKey,
				["Cosmos:DatabaseName"] = DatabaseName
			})
			.Build();

		var options = new DbContextOptionsBuilder<ShiftPay_BackendContext>()
			.UseCosmos(EmulatorEndpoint, EmulatorKey, DatabaseName, cosmosOptions =>
			{
				cosmosOptions.HttpClientFactory(() =>
				{
					// Disable SSL validation for emulator
					var handler = new HttpClientHandler
					{
						ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
					};
					return new HttpClient(handler);
				});
				cosmosOptions.ConnectionMode(ConnectionMode.Gateway);
			})
			.Options;

		return new ShiftPay_BackendContext(options, configuration);
	}

	public async ValueTask InitializeAsync()
	{
		// Create the Cosmos client with emulator settings
		var cosmosClientOptions = new CosmosClientOptions
		{
			HttpClientFactory = () =>
			{
				var handler = new HttpClientHandler
				{
					ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
				};
				return new HttpClient(handler);
			},
			ConnectionMode = ConnectionMode.Gateway
		};

		_cosmosClient = new CosmosClient(EmulatorEndpoint, EmulatorKey, cosmosClientOptions);

		// Delete the database if it exists to ensure clean state
		// This also ensures unique key policies are properly applied (they can only be set at creation)
		try
		{
			var existingDatabase = _cosmosClient.GetDatabase(DatabaseName);
			await existingDatabase.DeleteAsync();
		}
		catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			// Database doesn't exist, that's fine
		}

		// Create fresh database
		var databaseResponse = await _cosmosClient.CreateDatabaseAsync(DatabaseName);
		var database = databaseResponse.Database;

		// Create containers with partition keys and unique key policies
		// Note: Unique keys are scoped within a partition

		// Shifts container - no unique key needed (Id is unique)
		await database.CreateContainerAsync(
			new ContainerProperties("Shifts", ["/UserId", "/YearMonth", "/Day"]));

		// WorkInfos container - Workplace should be unique per user (partition)
		var workInfosProperties = new ContainerProperties("WorkInfos", ["/UserId"])
		{
			UniqueKeyPolicy = new UniqueKeyPolicy
			{
				UniqueKeys =
				{
					new UniqueKey { Paths = { "/Workplace" } }
				}
			}
		};
		await database.CreateContainerAsync(workInfosProperties);

		// ShiftTemplates container - TemplateName should be unique per user (partition)
		var shiftTemplatesProperties = new ContainerProperties("ShiftTemplates", ["/UserId"])
		{
			UniqueKeyPolicy = new UniqueKeyPolicy
			{
				UniqueKeys =
				{
					new UniqueKey { Paths = { "/TemplateName" } }
				}
			}
		};
		await database.CreateContainerAsync(shiftTemplatesProperties);

		// Ensure EF Core schema is created
		await using var context = CreateContext();
		await context.Database.EnsureCreatedAsync();
	}

	public ValueTask DisposeAsync()
	{
		_cosmosClient?.Dispose();
		return ValueTask.CompletedTask;
	}
}

/// <summary>
/// Collection definition for sharing the CosmosDbTestFixture across all test classes.
/// The fixture is initialized once per test run, which deletes and recreates the database.
/// Each test class instance gets a unique user ID (via Guid) to ensure test isolation
/// without needing per-test cleanup.
/// </summary>
[CollectionDefinition("CosmosDb")]
public class CosmosDbCollection : ICollectionFixture<CosmosDbTestFixture>
{
}
