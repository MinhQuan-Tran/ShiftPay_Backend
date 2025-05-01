using ShiftPay_Backend.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ShiftPay_Backend.Tests
{
    public class ShiftPayTestFixture : IAsyncLifetime
    {
        public string Token { get; private set; } = null!;
        public HttpClient Client { get; private set; } = null!;
        public List<ShiftDTO> TestDataShifts { get; } = new();

        private DistributedApplication _app;
        private AuthenticationService _authService;

        public async Task InitializeAsync()
        {
            _authService = new AuthenticationService();

            Token = await _authService.GetAccessToken();
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(Token) as JwtSecurityToken;
            var userId = jsonToken?.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

            var appHost = DistributedApplicationTestingBuilder
                .CreateAsync<Projects.ShiftPay_Backend_AppHost>(["DcpPublisher:RandomizePorts=false"])
                .GetAwaiter()
                .GetResult();

            appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
            {
                clientBuilder.AddStandardResilienceHandler();
            });

            _app = appHost.BuildAsync().GetAwaiter().GetResult();
            var resourceNotificationService = _app.Services.GetRequiredService<ResourceNotificationService>();
            await _app.StartAsync();

            Client = _app.CreateHttpClient("shiftpay-backend", "https");
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

            await resourceNotificationService
                .WaitForResourceAsync("shiftpay-backend", KnownResourceStates.Running)
                .WaitAsync(TimeSpan.FromSeconds(30));

            var newShifts = new[]
            {
                new Shift
                {
                    Workplace = "TestPlace1",
                    PayRate = 15.5M,
                    StartTime = DateTime.Parse("2023-10-15T09:00:00"),
                    EndTime = DateTime.Parse("2023-10-15T17:00:00"),
                    UnpaidBreaks = new List<TimeSpan> { TimeSpan.Parse("00:30:00") }
                },
                new Shift
                {
                    Workplace = "TestPlace2",
                    PayRate = 22.0M,
                    StartTime = DateTime.Parse("2023-09-19T09:00:00"),
                    EndTime = DateTime.Parse("2023-09-20T17:00:00"),
                    UnpaidBreaks = new List<TimeSpan> { TimeSpan.Parse("00:30:00"), TimeSpan.Parse("01:00:00") }
                },
                new Shift
                {
                    Workplace = "TestPlace3",
                    PayRate = 69.69M,
                    StartTime = DateTime.Parse("2023-09-15T09:00:00"),
                    EndTime = DateTime.Parse("2023-09-18T17:00:00"),
                    UnpaidBreaks = new List<TimeSpan>{ }
                }
            };

            foreach (var shift in newShifts)
            {
                var response = await Client.PostAsJsonAsync("/api/Shifts", shift);

                try
                {
                    response.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    await Client.DeleteAsync($"/api/Shifts/{response.Content.ReadFromJsonAsync<ShiftDTO>().Id}");
                }

                var returnedShift = await response.Content.ReadFromJsonAsync<ShiftDTO>();
                if (returnedShift?.Id is not null)
                {
                    TestDataShifts.Add(returnedShift);
                }
            }
        }

        public async Task DisposeAsync()
        {
            foreach (var shift in TestDataShifts)
            {
                try
                {
                    var response = await Client.DeleteAsync($"/api/Shifts/{shift.Id}");

                    if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
                    {
                        Console.WriteLine($"Failed to delete shift {shift.Id}: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception while deleting shift {shift.Id}: {ex.Message}");
                }
            }
        }
    }
}
