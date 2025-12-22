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

    public HttpClient Client { get; private set; } = null!;
    public List<ShiftDTO> TestDataShifts { get; } = new();
    public List<WorkInfoDTO> TestDataWorkInfos { get; } = new();

    private DistributedApplication _app = null!;
    private IServiceProvider _provider = null!;
    private ShiftPay_BackendContext _context = null!;

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");

        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ShiftPay_Backend_AppHost>(["DcpPublisher:RandomizePorts=false"]);

        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        _provider = CreateServiceProvider();
        _context = _provider.GetRequiredService<ShiftPay_BackendContext>();

        await SeedShiftTestDataAsync();
		await SeedWorkInfoTestDataAsync();

		Client = _app.CreateHttpClient("shiftpay-backend", "https");
    }

    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Test.json", optional: false, reloadOnChange: true)
            .Build();

        services.AddSingleton(configuration);

        services.AddDbContext<ShiftPay_BackendContext>(options =>
            options.UseCosmos(
                configuration["Cosmos:Endpoint"],
                configuration["Cosmos:Key"],
                configuration["Cosmos:DatabaseName"]));

        return services.BuildServiceProvider();
    }

    public async Task ClearAllShiftsAsync() => await ClearAllAsync(_context.Shifts);

    public async Task ClearAllWorkInfosAsync() => await ClearAllAsync(_context.WorkInfos);

    private async Task ClearAllAsync<TEntity>(DbSet<TEntity> set) where TEntity : class
    {
        var items = await set.ToListAsync();
        if (items.Count == 0)
        {
            return;
        }

        set.RemoveRange(items);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Azure.Cosmos.CosmosException cosmos && cosmos.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Under Cosmos, deletes can race with other test runs; if some documents are already gone,
            // clear tracking and continue.
        }
        finally
        {
            _context.ChangeTracker.Clear();
        }
    }

    public async Task SeedShiftTestDataAsync()
    {
        // Best-effort cleanup; Cosmos deletes can race, so handle conflicts during insert below.
        await ClearAllShiftsAsync();

        var testShifts = new List<Shift>
        {
            new()
            {
                Id = Guid.Parse(S1),
                UserId = "test-user-id",
                Workplace = "McDonald",
                PayRate = 25,
                StartTime = new DateTime(2023, 10, 15, 9, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2023, 10, 15, 17, 0, 0, DateTimeKind.Utc),
                UnpaidBreaks = [TimeSpan.FromMinutes(30)],
            },
            new()
            {
                Id = Guid.Parse(S2),
                UserId = "test-user-id",
                Workplace = "KFC",
                PayRate = 30,
                StartTime = new DateTime(2023, 10, 14, 8, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2023, 10, 14, 16, 0, 0, DateTimeKind.Utc),
                UnpaidBreaks = [TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(20)],
            },
            new()
            {
                Id = Guid.Parse(S3),
                UserId = "test-user-id",
                Workplace = "Starbucks",
                PayRate = 28,
                StartTime = new DateTime(2023, 9, 15, 7, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2023, 9, 15, 15, 0, 0, DateTimeKind.Utc),
                UnpaidBreaks = [],
            },
            new()
            {
                Id = Guid.Parse(S4),
                UserId = "test-user-id",
                Workplace = "Burger King",
                PayRate = 20,
                StartTime = new DateTime(2022, 12, 25, 10, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2022, 12, 25, 18, 0, 0, DateTimeKind.Utc),
                UnpaidBreaks = [TimeSpan.FromMinutes(45)],
            },
            new()
            {
                Id = Guid.Parse(S5),
                UserId = "test-user-id-1",
                Workplace = "Starbucks",
                PayRate = 28,
                StartTime = new DateTime(2023, 2, 15, 7, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2023, 2, 15, 15, 0, 0, DateTimeKind.Utc),
                UnpaidBreaks = [],
            },
        };

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

                // If the item already exists (e.g. from a prior run), remove it explicitly then retry.
                // IMPORTANT: Shifts use a *composite* partition key (UserId, YearMonth, Day). We must query
                // with the full key so EF can delete the correct document.
                var existing = await _context.Shifts
                    .WithPartitionKey(shift.UserId, shift.YearMonth, shift.Day)
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
                Id = Guid.Parse("00000000-0000-0000-0000-000000010001"),
                UserId = "test-user-id",
                Workplace = "KFC",
                PayRates = [25m, 30m],
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000010002"),
                UserId = "test-user-id",
                Workplace = "McDonald",
                PayRates = [20m],
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000010003"),
                UserId = "test-user-id-1",
                Workplace = "KFC",
                PayRates = [99m],
            },
        };

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
                    .WithPartitionKey(workInfo.UserId, workInfo.Workplace)
                    .FirstOrDefaultAsync();

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
