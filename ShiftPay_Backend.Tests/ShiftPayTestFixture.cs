using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShiftPay_Backend.Data;
using ShiftPay_Backend.Models;
using ShiftDTO = ShiftPay_Backend.Models.ShiftDTO;

namespace ShiftPay_Backend.Tests;

public sealed class ShiftPayTestFixture : IAsyncLifetime
{
    public const string S1 = "00000000-0000-0000-0000-000000000001";
    public const string S2 = "00000000-0000-0000-0000-000000000002";
    public const string S3 = "00000000-0000-0000-0000-000000000003";
    public const string S4 = "00000000-0000-0000-0000-000000000004";
    public const string S5 = "00000000-0000-0000-0000-000000000005";
    public const string S6 = "00000000-0000-0000-0000-000000000006"; // Non-existent shift
    public const string S7 = "00000000-0000-0000-0000-000000000007"; // Non-existent shift

    private const string CosmosDatabaseNameEnvVar = "Cosmos__DatabaseName";
    private const string SharedTestDatabaseName = "ShiftPay_Test_Shared";

    public HttpClient Client { get; private set; } = null!;
    public List<ShiftDTO> TestDataShifts { get; } = new();
    public List<WorkInfoDTO> TestDataWorkInfos { get; } = new();
    public List<ShiftTemplateDTO> TestDataShiftTemplates { get; } = new();

    private DistributedApplication _app = null!;
    private IServiceProvider _provider = null!;
    private ShiftPay_BackendContext _context = null!;

    private static readonly SemaphoreSlim _dbLock = new(1, 1);

    // Use unique UserId per fixture instance for test isolation
    private readonly string _testUserId = $"test-user-{Guid.NewGuid():N}";
    private readonly string _testUserIdOther = $"test-user-other-{Guid.NewGuid():N}";

    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Test.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        services.AddSingleton(configuration);

        services.AddDbContext<ShiftPay_BackendContext>(options =>
            options.UseCosmos(
                configuration["Cosmos:Endpoint"],
                configuration["Cosmos:Key"],
                configuration["Cosmos:DatabaseName"]));
        return services.BuildServiceProvider();
    }

    private static async Task EnsureCosmosProvisionedAsync(IConfiguration configuration)
    {
        var endpoint = configuration["Cosmos:Endpoint"];
        var key = configuration["Cosmos:Key"];
        var databaseName = configuration["Cosmos:DatabaseName"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("Missing configuration value: Cosmos:Endpoint");
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Missing configuration value: Cosmos:Key");
        }

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("Missing configuration value: Cosmos:DatabaseName");
        }

        using var cosmosClient = new CosmosClient(endpoint, key);

        // Create shared database and containers if they don't exist.
        // Key architectural decision: All tests share the same database and containers.
        // Test isolation is achieved through partition keys - each test fixture instance
        // uses a unique UserId (e.g., test-user-{guid}), and since all containers are
        // partitioned by UserId, tests naturally operate in isolated partitions and
        // cannot interfere with each other. This approach:
        // - Eliminates database creation/deletion overhead
        // - Mirrors production behavior (partition-based isolation)
        // - Supports safe parallel test execution
        // - Avoids orphaned databases from failed test runs
        var db = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);

        // These container names match `ShiftPay_BackendContext.OnModelCreating`.
        // Use PartitionKeyPaths even for single-path keys to avoid SDK paths that require PartitionKeyPath.
        await db.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties("Shifts", partitionKeyPaths: ["/UserId", "/YearMonth", "/Day"]));

        await db.Database.CreateContainerIfNotExistsAsync(new ContainerProperties("WorkInfos", partitionKeyPaths: ["/UserId"])
        {
            UniqueKeyPolicy = new UniqueKeyPolicy
            {
                UniqueKeys =
                {
                    new UniqueKey { Paths = { "/UserId", "/Workplace" } },
                },
            },
        });

        await db.Database.CreateContainerIfNotExistsAsync(new ContainerProperties("ShiftTemplates", partitionKeyPaths: ["/UserId"])
        {
            UniqueKeyPolicy = new UniqueKeyPolicy
            {
                UniqueKeys =
                {
                    new UniqueKey { Paths = { "/UserId", "/TemplateName" } },
                },
            },
        });
    }

    public async Task InitializeAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");

            // Use shared database for all tests
            Environment.SetEnvironmentVariable(CosmosDatabaseNameEnvVar, SharedTestDatabaseName);

            // Provision the database/containers before app and seeding use them.
            var bootstrapConfig = new ConfigurationBuilder()
                .AddJsonFile("appsettings.Test.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            await EnsureCosmosProvisionedAsync(bootstrapConfig);

            var appHost = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.ShiftPay_Backend_AppHost>(["DcpPublisher:RandomizePorts=false"]);

            _app = await appHost.BuildAsync();
            await _app.StartAsync();

            _provider = CreateServiceProvider();
            _context = _provider.GetRequiredService<ShiftPay_BackendContext>();

            await SeedShiftTestDataAsync();
            await SeedWorkInfoTestDataAsync();
            await SeedShiftTemplateTestDataAsync();

            // Create HTTP client with custom header for unique userId
            Client = _app.CreateHttpClient("shiftpay-backend", "https");
            Client.DefaultRequestHeaders.Add("X-Test-UserId", _testUserId);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private static async Task ClearAllAsync<TEntity>(DbSet<TEntity> set, ShiftPay_BackendContext context) where TEntity : class
    {
        var items = await set.ToListAsync();
        if (items.Count == 0)
        {
            return;
        }

        set.RemoveRange(items);

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Azure.Cosmos.CosmosException cosmos && cosmos.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Under Cosmos, deletes can race with other test runs; if some documents are already gone,
            // clear tracking and continue.
        }
        finally
        {
            context.ChangeTracker.Clear();
        }
    }

    public async Task ClearAllShiftsAsync() => await ClearAllAsync(_context.Shifts, _context);

    public async Task ClearAllWorkInfosAsync() => await ClearAllAsync(_context.WorkInfos, _context);

    public async Task ClearAllShiftTemplatesAsync() => await ClearAllAsync(_context.ShiftTemplates, _context);

    public async Task SeedShiftTestDataAsync()
    {
        // Best-effort cleanup; Cosmos deletes can race, so handle conflicts during insert below.
        await ClearAllShiftsAsync();

        var testShifts = new List<Shift>
        {
            new()
            {
                Id = Guid.Parse(S1),
                UserId = _testUserId,
                Workplace = "McDonald",
                PayRate = 25,
                StartTime = new DateTime(2023, 10, 15, 9, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2023, 10, 15, 17, 0, 0, DateTimeKind.Utc),
                UnpaidBreaks = [TimeSpan.FromMinutes(30)],
            },
            new()
            {
                Id = Guid.Parse(S2),
                UserId = _testUserId,
                Workplace = "KFC",
                PayRate = 30,
                StartTime = new DateTime(2023, 10, 14, 8, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2023, 10, 14, 16, 0, 0, DateTimeKind.Utc),
                UnpaidBreaks = [TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(20)],
            },
            new()
            {
                Id = Guid.Parse(S3),
                UserId = _testUserId,
                Workplace = "Starbucks",
                PayRate = 28,
                StartTime = new DateTime(2023, 9, 15, 7, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2023, 9, 15, 15, 0, 0, DateTimeKind.Utc),
                UnpaidBreaks = [],
            },
            new()
            {
                Id = Guid.Parse(S4),
                UserId = _testUserId,
                Workplace = "Burger King",
                PayRate = 20,
                StartTime = new DateTime(2022, 12, 25, 10, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2022, 12, 25, 18, 0, 0, DateTimeKind.Utc),
                UnpaidBreaks = [TimeSpan.FromMinutes(45)],
            },
            new()
            {
                Id = Guid.Parse(S5),
                UserId = _testUserIdOther,
                Workplace = "Starbucks",
                PayRate = 28,
                StartTime = new DateTime(2023, 2, 15, 7, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2023, 2, 15, 15, 0, 0, DateTimeKind.Utc),
                UnpaidBreaks = [],
            },
        };

        // Insert with conflict handling, since Cosmos may report conflicts if prior test runs left data.
        foreach (var shift in testShifts)
        {
            try
            {
                _context.Shifts.Add(shift);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Azure.Cosmos.CosmosException cosmos && cosmos.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                _context.ChangeTracker.Clear();

                var existing = await _context.Shifts
                    .WithPartitionKey(shift.UserId)
                    .FirstOrDefaultAsync(s => s.Id == shift.Id);

                if (existing is not null)
                {
                    _context.Shifts.Remove(existing);
                    await _context.SaveChangesAsync();
                    _context.ChangeTracker.Clear();
                }

                _context.Shifts.Add(shift);
                await _context.SaveChangesAsync();
            }
            finally
            {
                _context.ChangeTracker.Clear();
            }
        }

        TestDataShifts.Clear();
        TestDataShifts.AddRange(testShifts.Select(s => new ShiftDTO
        {
            Id = s.Id,
            Workplace = s.Workplace,
            PayRate = s.PayRate,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            UnpaidBreaks = s.UnpaidBreaks,
        }));
    }

    public async Task SeedWorkInfoTestDataAsync()
    {
        await ClearAllWorkInfosAsync();

        var testWorkInfos = new List<WorkInfo>
        {
            new()
            {
                Id = WorkInfo.CreateId("KFC"),
                UserId = _testUserId,
                Workplace = "KFC",
                PayRates = [25m, 30m],
            },
            new()
            {
                Id = WorkInfo.CreateId("McDonald"),
                UserId = _testUserId,
                Workplace = "McDonald",
                PayRates = [20m],
            },
            new()
            {
                Id = WorkInfo.CreateId("KFC"),
                UserId = _testUserIdOther,
                Workplace = "KFC",
                PayRates = [99m],
            },
        };

        // Insert with conflict handling, similar to shifts.
        foreach (var workInfo in testWorkInfos)
        {
            try
            {
                _context.WorkInfos.Add(workInfo);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Azure.Cosmos.CosmosException cosmos && cosmos.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                _context.ChangeTracker.Clear();

                var existing = await _context.WorkInfos
                    .WithPartitionKey(workInfo.UserId)
                    .FirstOrDefaultAsync(w => w.Workplace == workInfo.Workplace);

                if (existing is not null)
                {
                    _context.WorkInfos.Remove(existing);
                    await _context.SaveChangesAsync();
                    _context.ChangeTracker.Clear();
                }

                _context.WorkInfos.Add(workInfo);
                await _context.SaveChangesAsync();
            }
            finally
            {
                _context.ChangeTracker.Clear();
            }
        }

        TestDataWorkInfos.Clear();
        TestDataWorkInfos.AddRange(testWorkInfos.Select(w => w.ToDTO()));
    }

    public async Task SeedShiftTemplateTestDataAsync()
    {
        await ClearAllShiftTemplatesAsync();

        var testShiftTemplates = new List<ShiftTemplate>
        {
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000101"),
                UserId = _testUserId,
                TemplateName = "KFC-Open",
                Workplace = "KFC",
                PayRate = 30,
                StartTime = new DateTime(2023, 10, 10, 8, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2023, 10, 10, 16, 0, 0, DateTimeKind.Utc),
                UnpaidBreaks = [TimeSpan.FromMinutes(15)],
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000102"),
                UserId = _testUserId,
                TemplateName = "McDonald-Close",
                Workplace = "McDonald",
                PayRate = 25,
                StartTime = new DateTime(2023, 10, 11, 17, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2023, 10, 11, 23, 0, 0, DateTimeKind.Utc),
                UnpaidBreaks = [],
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000103"),
                UserId = _testUserIdOther,
                TemplateName = "OtherUser-Template",
                Workplace = "KFC",
                PayRate = 99,
                StartTime = new DateTime(2023, 10, 12, 9, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2023, 10, 12, 17, 0, 0, DateTimeKind.Utc),
                UnpaidBreaks = [],
            },
        };

        foreach (var template in testShiftTemplates)
        {
            try
            {
                _context.ShiftTemplates.Add(template);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Azure.Cosmos.CosmosException cosmos && cosmos.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                _context.ChangeTracker.Clear();

                var existing = await _context.ShiftTemplates
                    .WithPartitionKey(template.UserId)
                    .FirstOrDefaultAsync(t => t.TemplateName == template.TemplateName);

                if (existing is not null)
                {
                    _context.ShiftTemplates.Remove(existing);
                    await _context.SaveChangesAsync();
                    _context.ChangeTracker.Clear();
                }

                _context.ShiftTemplates.Add(template);
                await _context.SaveChangesAsync();
            }
            finally
            {
                _context.ChangeTracker.Clear();
            }
        }

        TestDataShiftTemplates.Clear();
        TestDataShiftTemplates.AddRange(testShiftTemplates.Select(t => t.ToDTO()));
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        if (_provider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (_provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
