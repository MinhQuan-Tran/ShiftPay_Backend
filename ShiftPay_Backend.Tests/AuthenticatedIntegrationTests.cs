using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShiftPay_Backend.Models;
using System.Net.Http.Json;

namespace ShiftPay_Backend.Tests;

/// <summary>
/// Integration tests for the ShiftPay Backend API with fake authentication enabled.
/// These tests test the full HTTP pipeline including authentication middleware.
/// </summary>
[Collection("Aspire Integration")]
public class AuthenticatedIntegrationTests : IAsyncLifetime
{
	private DistributedApplication? _app;
	private HttpClient? _httpClient;
	private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

	public async ValueTask InitializeAsync()
	{
		// Set environment to Test to enable fake auth before creating the builder
		Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");

		var appHost = await DistributedApplicationTestingBuilder
			.CreateAsync<Projects.ShiftPay_Backend_AppHost>();

		// Configure the backend to use fake authentication for testing
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

	public async ValueTask DisposeAsync()
	{
		_httpClient?.Dispose();
		if (_app is not null)
		{
			await _app.DisposeAsync();
		}
		// Reset environment
		Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
	}

	[Fact]
	public async Task GetShifts_WithFakeAuth_ReturnsOk()
	{
		// Act
		var response = await _httpClient!.GetAsync("/api/shifts");

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		var shifts = await response.Content.ReadFromJsonAsync<List<ShiftDTO>>();
		Assert.NotNull(shifts);
	}

	[Fact]
	public async Task PostShift_WithFakeAuth_CreatesShiftAndReturnsCreated()
	{
		// Arrange
		var shiftDto = new ShiftDTO
		{
			Workplace = "Integration Test Workplace",
			PayRate = 25.00m,
			StartTime = new DateTime(2024, 8, 15, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 8, 15, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = [TimeSpan.FromMinutes(30)]
		};

		// Act
		var response = await _httpClient!.PostAsJsonAsync("/api/shifts", shiftDto);

		// Assert
		Assert.Equal(HttpStatusCode.Created, response.StatusCode);

		var createdShift = await response.Content.ReadFromJsonAsync<ShiftDTO>();
		Assert.NotNull(createdShift);
		Assert.NotNull(createdShift.Id);
		Assert.Equal(shiftDto.Workplace, createdShift.Workplace);
		Assert.Equal(shiftDto.PayRate, createdShift.PayRate);
	}

	[Fact]
	public async Task PostAndGetShift_RoundTrip_ReturnsCreatedShift()
	{
		// Arrange
		var shiftDto = new ShiftDTO
		{
			Workplace = "Round Trip Test",
			PayRate = 30.00m,
			StartTime = new DateTime(2024, 9, 20, 10, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 9, 20, 18, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		// Act - Create
		var createResponse = await _httpClient!.PostAsJsonAsync("/api/shifts", shiftDto);
		Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

		var createdShift = await createResponse.Content.ReadFromJsonAsync<ShiftDTO>();
		Assert.NotNull(createdShift?.Id);

		// Act - Get
		var getResponse = await _httpClient!.GetAsync($"/api/shifts/{createdShift.Id}");

		// Assert
		Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

		var retrievedShift = await getResponse.Content.ReadFromJsonAsync<ShiftDTO>();
		Assert.NotNull(retrievedShift);
		Assert.Equal(createdShift.Id, retrievedShift.Id);
		Assert.Equal(shiftDto.Workplace, retrievedShift.Workplace);
	}

	[Fact]
	public async Task PostUpdateDeleteShift_FullCycle_WorksCorrectly()
	{
		// Arrange - Create
		var shiftDto = new ShiftDTO
		{
			Workplace = "Full Cycle Test",
			PayRate = 22.00m,
			StartTime = new DateTime(2024, 10, 1, 8, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 10, 1, 16, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		var createResponse = await _httpClient!.PostAsJsonAsync("/api/shifts", shiftDto);
		Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
		var createdShift = await createResponse.Content.ReadFromJsonAsync<ShiftDTO>();
		Assert.NotNull(createdShift?.Id);

		// Act - Update
		var updatedDto = new ShiftDTO
		{
			Id = createdShift.Id,
			Workplace = "Updated Workplace",
			PayRate = 28.00m,
			StartTime = createdShift.StartTime,
			EndTime = createdShift.EndTime,
			UnpaidBreaks = []
		};

		var updateResponse = await _httpClient!.PutAsJsonAsync($"/api/shifts/{createdShift.Id}", updatedDto);
		Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

		var updatedShift = await updateResponse.Content.ReadFromJsonAsync<ShiftDTO>();
		Assert.Equal("Updated Workplace", updatedShift?.Workplace);
		Assert.Equal(28.00m, updatedShift?.PayRate);

		// Act - Delete
		var deleteResponse = await _httpClient!.DeleteAsync($"/api/shifts/{createdShift.Id}");
		Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

		// Verify deleted
		var getDeletedResponse = await _httpClient!.GetAsync($"/api/shifts/{createdShift.Id}");
		Assert.Equal(HttpStatusCode.NotFound, getDeletedResponse.StatusCode);
	}

	[Fact]
	public async Task GetWorkInfos_WithFakeAuth_ReturnsOk()
	{
		// Act
		var response = await _httpClient!.GetAsync("/api/workinfos");

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		var workInfos = await response.Content.ReadFromJsonAsync<List<WorkInfoDTO>>();
		Assert.NotNull(workInfos);
	}

	[Fact]
	public async Task PostWorkInfo_WithFakeAuth_CreatesWorkInfo()
	{
		// Arrange
		var workInfoDto = new WorkInfoDTO
		{
			Workplace = "Integration Test Shop",
			PayRates = [15.00m, 18.00m, 20.00m]
		};

		// Act
		var response = await _httpClient!.PostAsJsonAsync("/api/workinfos", workInfoDto);

		// Assert
		Assert.True(
			response.StatusCode == HttpStatusCode.Created ||
			response.StatusCode == HttpStatusCode.OK, // OK if already exists (merge)
			$"Expected Created or OK, got {response.StatusCode}");

		var resultWorkInfo = await response.Content.ReadFromJsonAsync<WorkInfoDTO>();
		Assert.NotNull(resultWorkInfo);
		Assert.Equal(workInfoDto.Workplace, resultWorkInfo.Workplace);
	}

	[Fact]
	public async Task GetShiftTemplates_WithFakeAuth_ReturnsOk()
	{
		// Act
		var response = await _httpClient!.GetAsync("/api/shifttemplates");

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		var templates = await response.Content.ReadFromJsonAsync<List<ShiftTemplateDTO>>();
		Assert.NotNull(templates);
	}

	[Fact]
	public async Task PostShiftTemplate_WithFakeAuth_CreatesTemplate()
	{
		// Arrange
		var templateDto = new ShiftTemplateDTO
		{
			TemplateName = $"Integration Test Template {Guid.NewGuid():N}",
			Workplace = "Test Cafe",
			PayRate = 22.00m,
			StartTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = [TimeSpan.FromMinutes(30)]
		};

		// Act
		var response = await _httpClient!.PostAsJsonAsync("/api/shifttemplates", templateDto);

		// Assert
		Assert.True(
			response.StatusCode == HttpStatusCode.Created ||
			response.StatusCode == HttpStatusCode.OK, // OK if already exists (update)
			$"Expected Created or OK, got {response.StatusCode}");

		var createdTemplate = await response.Content.ReadFromJsonAsync<ShiftTemplateDTO>();
		Assert.NotNull(createdTemplate);
		Assert.Equal(templateDto.TemplateName, createdTemplate.TemplateName);
		Assert.Equal(templateDto.Workplace, createdTemplate.Workplace);
	}

	[Fact]
	public async Task PostInvalidShift_ReturnsBadRequest()
	{
		// Arrange - Invalid shift (end before start)
		var invalidShift = new ShiftDTO
		{
			Workplace = "Invalid Test",
			PayRate = 20.00m,
			StartTime = new DateTime(2024, 8, 15, 17, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 8, 15, 9, 0, 0, DateTimeKind.Utc), // End before start
			UnpaidBreaks = []
		};

		// Act
		var response = await _httpClient!.PostAsJsonAsync("/api/shifts", invalidShift);

		// Assert
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task GetNonExistentShift_ReturnsNotFound()
	{
		// Act
		var response = await _httpClient!.GetAsync($"/api/shifts/{Guid.NewGuid()}");

		// Assert
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	[Fact]
	public async Task BatchPostShifts_CreatesMultipleShifts()
	{
		// Arrange
		var shifts = new[]
		{
			new ShiftDTO
			{
				Workplace = "Batch Test A",
				PayRate = 20.00m,
				StartTime = new DateTime(2024, 11, 1, 9, 0, 0, DateTimeKind.Utc),
				EndTime = new DateTime(2024, 11, 1, 17, 0, 0, DateTimeKind.Utc),
				UnpaidBreaks = []
			},
			new ShiftDTO
			{
				Workplace = "Batch Test B",
				PayRate = 22.00m,
				StartTime = new DateTime(2024, 11, 2, 10, 0, 0, DateTimeKind.Utc),
				EndTime = new DateTime(2024, 11, 2, 18, 0, 0, DateTimeKind.Utc),
				UnpaidBreaks = []
			}
		};

		// Act
		var response = await _httpClient!.PostAsJsonAsync("/api/shifts/batch", shifts);

		// Assert
		Assert.Equal(HttpStatusCode.Created, response.StatusCode);

		var createdShifts = await response.Content.ReadFromJsonAsync<List<ShiftDTO>>();
		Assert.NotNull(createdShifts);
		Assert.Equal(2, createdShifts.Count);
	}

	[Fact]
	public async Task GetShiftTemplate_WithSpecialCharacters_ReturnsTemplate()
	{
		// Arrange - Create template with special characters: | (pipe) and / (forward slash)
		var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
		var templateName = $"Test | O/N {uniqueSuffix}"; // Unique name to avoid conflicts
		var templateDto = new ShiftTemplateDTO
		{
			TemplateName = templateName,
			Workplace = "Fast Food",
			PayRate = 22.00m,
			StartTime = new DateTime(2024, 1, 1, 22, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 2, 6, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = [TimeSpan.FromMinutes(30)]
		};

		var createResponse = await _httpClient!.PostAsJsonAsync("/api/shifttemplates", templateDto);
		Assert.True(
			createResponse.StatusCode == HttpStatusCode.Created ||
			createResponse.StatusCode == HttpStatusCode.OK,
			$"Expected Created or OK, got {createResponse.StatusCode}");
		var createdTemplate = await createResponse.Content.ReadFromJsonAsync<ShiftTemplateDTO>();
		Assert.NotNull(createdTemplate?.Id);

		// Act
		var getResponse = await _httpClient!.GetAsync($"/api/shifttemplates/{createdTemplate.Id}");

		// Assert
		Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

		var retrievedTemplate = await getResponse.Content.ReadFromJsonAsync<ShiftTemplateDTO>();
		Assert.NotNull(retrievedTemplate);
		Assert.Equal(templateName, retrievedTemplate.TemplateName);
		Assert.Equal("Fast Food", retrievedTemplate.Workplace);
	}

	[Fact]
	public async Task DeleteShiftTemplate_WithSpecialCharacters_ReturnsNoContent()
	{
		// Arrange - Create and then delete template with special characters
		var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
		var templateName = $"Delete | Test/Name {uniqueSuffix}";
		var templateDto = new ShiftTemplateDTO
		{
			TemplateName = templateName,
			Workplace = "Test Workplace",
			PayRate = 20.00m,
			StartTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		var createResponse = await _httpClient!.PostAsJsonAsync("/api/shifttemplates", templateDto);
		Assert.True(
			createResponse.StatusCode == HttpStatusCode.Created ||
			createResponse.StatusCode == HttpStatusCode.OK,
			$"Expected Created or OK, got {createResponse.StatusCode}");
		var createdTemplate = await createResponse.Content.ReadFromJsonAsync<ShiftTemplateDTO>();
		Assert.NotNull(createdTemplate?.Id);

		// Act
		var deleteResponse = await _httpClient!.DeleteAsync($"/api/shifttemplates/{createdTemplate.Id}");

		// Assert
		Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

		// Verify deleted
		var getResponse = await _httpClient!.GetAsync($"/api/shifttemplates/{createdTemplate.Id}");
		Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
	}
}
