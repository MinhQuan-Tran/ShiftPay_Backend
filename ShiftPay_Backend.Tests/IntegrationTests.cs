using Microsoft.Extensions.Logging;
using ShiftPay_Backend.Models;
using System.Net.Http.Json;

namespace ShiftPay_Backend.Tests;

/// <summary>
/// Collection definition for integration tests to prevent parallel execution.
/// Integration tests share the AppHost and cannot run in parallel.
/// </summary>
[CollectionDefinition("Aspire Integration", DisableParallelization = true)]
public class AspireIntegrationCollection
{
}

/// <summary>
/// Integration tests for the ShiftPay Backend API using Aspire's DistributedApplicationTestingBuilder.
/// These tests spin up the full AppHost and test the API through HTTP requests.
/// </summary>
[Collection("Aspire Integration")]
public class IntegrationTests : IAsyncLifetime
{
	private DistributedApplication? _app;
	private HttpClient? _httpClient;
	private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

	public async Task InitializeAsync()
	{
		var appHost = await DistributedApplicationTestingBuilder
			.CreateAsync<Projects.ShiftPay_Backend_AppHost>();

		appHost.Services.AddLogging(logging =>
		{
			logging.SetMinimumLevel(LogLevel.Debug);
			logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
			logging.AddFilter("Aspire.", LogLevel.Warning);
		});

		appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
		{
			clientBuilder.AddStandardResilienceHandler();
		});

		_app = await appHost.BuildAsync();
		await _app.StartAsync();

		_httpClient = _app.CreateHttpClient("shiftpay-backend");

		// Wait for the resource to be ready
		using var cts = new CancellationTokenSource(DefaultTimeout);
		await _app.ResourceNotifications
			.WaitForResourceAsync("shiftpay-backend", KnownResourceStates.Running, cts.Token);
	}

	public async Task DisposeAsync()
	{
		_httpClient?.Dispose();
		if (_app is not null)
		{
			await _app.DisposeAsync();
		}
	}

	[Fact]
	public async Task HealthCheck_ReturnsOk()
	{
		// Act
		var response = await _httpClient!.GetAsync("/health");

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	[Fact]
	public async Task GetShifts_WithoutAuth_ReturnsUnauthorized()
	{
		// Act
		var response = await _httpClient!.GetAsync("/api/shifts");

		// Assert
		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task GetWorkInfos_WithoutAuth_ReturnsUnauthorized()
	{
		// Act
		var response = await _httpClient!.GetAsync("/api/workinfos");

		// Assert
		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task GetShiftTemplates_WithoutAuth_ReturnsUnauthorized()
	{
		// Act
		var response = await _httpClient!.GetAsync("/api/shifttemplates");

		// Assert
		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task PostShift_WithoutAuth_ReturnsUnauthorized()
	{
		// Arrange
		var shiftDto = new ShiftDTO
		{
			Workplace = "Test",
			PayRate = 20.00m,
			StartTime = DateTime.UtcNow.Date.AddHours(9),
			EndTime = DateTime.UtcNow.Date.AddHours(17),
			UnpaidBreaks = []
		};

		// Act
		var response = await _httpClient!.PostAsJsonAsync("/api/shifts", shiftDto);

		// Assert
		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task OpenApi_ReturnsOk()
	{
		// Act
		var response = await _httpClient!.GetAsync("/openapi/v1.json");

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		
		var content = await response.Content.ReadAsStringAsync();
		Assert.Contains("openapi", content);
	}
}
