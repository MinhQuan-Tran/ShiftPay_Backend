namespace ShiftPay_Backend.Tests
{
    public class ShiftPayHealthCheckTests
    {
        [Fact]
        public async Task BackendHealthCheck_ReturnsOk()
        {
            var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.ShiftPay_Backend_AppHost>();

            appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
            {
                clientBuilder.AddStandardResilienceHandler();
            });

            await using var app = await appHost.BuildAsync();
            var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();

            await app.StartAsync();

            await resourceNotificationService
                .WaitForResourceAsync("shiftpay-backend", KnownResourceStates.Running)
                .WaitAsync(TimeSpan.FromSeconds(30));

            var httpClient = app.CreateHttpClient("shiftpay-backend");
            var response = await httpClient.GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
