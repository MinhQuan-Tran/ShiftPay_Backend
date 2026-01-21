using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShiftPay_Backend.Data;

namespace ShiftPay_Backend.Tests;

/// <summary>
/// Shared test fixture that initializes a single Cosmos DB connection for all tests.
/// Uses the Azure Cosmos DB Emulator with the "ShiftPay" database.
/// </summary>
public class CosmosDbTestFixture : IAsyncLifetime
{
    private const string EmulatorEndpoint = "https://localhost:8081/";
    private const string EmulatorKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    private const string DatabaseName = "ShiftPay";

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

    public async Task InitializeAsync()
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

        // Create database if it doesn't exist
        var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseName);
        var database = databaseResponse.Database;

        // Create containers if they don't exist
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties("Shifts", new[] { "/UserId", "/YearMonth", "/Day" }));
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties("WorkInfos", new[] { "/UserId" }));
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties("ShiftTemplates", new[] { "/UserId" }));

        // Ensure EF Core schema is created
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        _cosmosClient?.Dispose();
        await Task.CompletedTask;
    }

    /// <summary>
    /// Cleans up test data for a specific user ID.
    /// Call this in test cleanup to isolate test data.
    /// </summary>
    public async Task CleanupUserDataAsync(string userId)
    {
        await using var context = CreateContext();

        var shifts = await context.Shifts
            .Where(s => s.UserId == userId)
            .ToListAsync();
        context.Shifts.RemoveRange(shifts);

        var workInfos = await context.WorkInfos
            .Where(w => w.UserId == userId)
            .ToListAsync();
        context.WorkInfos.RemoveRange(workInfos);

        var shiftTemplates = await context.ShiftTemplates
            .Where(st => st.UserId == userId)
            .ToListAsync();
        context.ShiftTemplates.RemoveRange(shiftTemplates);

        await context.SaveChangesAsync();
    }
}

/// <summary>
/// Collection definition for sharing the CosmosDbTestFixture across all test classes.
/// </summary>
[CollectionDefinition("CosmosDb")]
public class CosmosDbCollection : ICollectionFixture<CosmosDbTestFixture>
{
}
