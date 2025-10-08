using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShiftPay_Backend.Data;
using ShiftPay_Backend.Models;
using ShiftDTO = ShiftPay_Backend.Models.ShiftDTO;

namespace ShiftPay_Backend.Tests;

public class ShiftPayTestFixture : IAsyncLifetime
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

    private DistributedApplication _app = null!;
    private ShiftPay_BackendContext _context = null!;

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");

        var appHost = DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ShiftPay_Backend_AppHost>(["DcpPublisher:RandomizePorts=false"])
            .GetAwaiter()
            .GetResult();

        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        var services = new ServiceCollection();

        // Create a configuration object
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Test.json", optional: false, reloadOnChange: true)
            .Build();

        // Register IConfiguration
        services.AddSingleton<IConfiguration>(configuration);

        // Register DbContext
        services.AddDbContext<ShiftPay_BackendContext>(options =>
            options.UseCosmos(
                configuration["Cosmos:Endpoint"],
                configuration["Cosmos:Key"],
                configuration["Cosmos:DatabaseName"]
            ));

        var provider = services.BuildServiceProvider();
        _context = provider.GetRequiredService<ShiftPay_BackendContext>();

        await SeedTestDataAsync();

        Client = _app.CreateHttpClient("shiftpay-backend", "https");
    }

    public async Task ClearAllShiftsAsync()
    {
        var allShifts = await _context.Shifts.ToListAsync();
        _context.Shifts.RemoveRange(allShifts);
        await _context.SaveChangesAsync();

        Console.WriteLine($"###############################Cleared shifts from the database.");
    }

    public async Task SeedTestDataAsync()
    {
        await ClearAllShiftsAsync();

        // Add test data with distinct StartTime values for filtering and unpaid breaks
        var testShifts = new List<Shift>
        {
            new Shift
            {
                Id = Guid.Parse(S1),
                UserId = "test-user-id",
                Workplace = "McDonald",
                PayRate = 25,
                StartTime = new DateTime(2023, 10, 15, 9, 0, 0, DateTimeKind.Utc), // Matches year=2023, month=10, day=15
                EndTime = new DateTime(2023, 10, 15, 17, 0, 0, DateTimeKind.Utc),
                // Add unpaid breaks
                UnpaidBreaks = new List<TimeSpan> { TimeSpan.FromMinutes(30) }
            },
            new Shift
            {
                Id = Guid.Parse(S2),
                UserId = "test-user-id",
                Workplace = "KFC",
                PayRate = 30,
                StartTime = new DateTime(2023, 10, 14, 8, 0, 0, DateTimeKind.Utc), // Matches year=2023, month=10
                EndTime = new DateTime(2023, 10, 14, 16, 0, 0, DateTimeKind.Utc),
                // Add unpaid breaks
                UnpaidBreaks = new List<TimeSpan> { TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(20) }
            },
            new Shift
            {
                Id = Guid.Parse(S3),
                UserId = "test-user-id",
                Workplace = "Starbucks",
                PayRate = 28,
                StartTime = new DateTime(2023, 9, 15, 7, 0, 0, DateTimeKind.Utc), // Matches year=2023
                EndTime = new DateTime(2023, 9, 15, 15, 0, 0, DateTimeKind.Utc),
                // No unpaid breaks
                UnpaidBreaks = new List<TimeSpan>()
            },
            new Shift
            {
                Id = Guid.Parse(S4),
                UserId = "test-user-id",
                Workplace = "Burger King",
                PayRate = 20,
                StartTime = new DateTime(2022, 12, 25, 10, 0, 0, DateTimeKind.Utc), // Does not match any filter
                EndTime = new DateTime(2022, 12, 25, 18, 0, 0, DateTimeKind.Utc),
                // Add unpaid breaks
                UnpaidBreaks = new List<TimeSpan> { TimeSpan.FromMinutes(45) }
            },
            new Shift
            {
                Id = Guid.Parse(S5),
                UserId = "test-user-id-1", // Different user ID
                Workplace = "Starbucks",
                PayRate = 28,
                StartTime = new DateTime(2023, 2, 15, 7, 0, 0, DateTimeKind.Utc), // Matches year=2023
                EndTime = new DateTime(2023, 2, 15, 15, 0, 0, DateTimeKind.Utc),
                // No unpaid breaks
                UnpaidBreaks = new List<TimeSpan>()
            },
        };

        _context.Shifts.AddRange(testShifts);
        await _context.SaveChangesAsync();

        // Update TestDataShifts for use in tests
        this.TestDataShifts.Clear();
        this.TestDataShifts.AddRange(testShifts.Select(s => new ShiftDTO
        {
            Id = s.Id,
            Workplace = s.Workplace,
            PayRate = s.PayRate,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            UnpaidBreaks = s.UnpaidBreaks
        }));
    }

    public Task DisposeAsync()
    {
        return _app.DisposeAsync().AsTask();
    }
}
