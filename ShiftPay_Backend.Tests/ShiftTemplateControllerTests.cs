using ShiftPay_Backend.Models;
using System.Net.Http.Json;

namespace ShiftPay_Backend.Tests;

public sealed class ShiftTemplateControllerTests(ShiftPayTestFixture fixture) : IClassFixture<ShiftPayTestFixture>, IAsyncLifetime
{
	private readonly HttpClient _client = fixture.Client;
	private readonly ShiftPayTestFixture _fixture = fixture;

	public async Task InitializeAsync()
	{
		await _fixture.ClearAllShiftTemplatesAsync();
		await _fixture.SeedShiftTemplateTestDataAsync();
	}

	public Task DisposeAsync() => Task.CompletedTask;

	private static void AssertShiftTemplateMatches(ShiftTemplateDTO expected, ShiftTemplateDTO actual)
	{
		Assert.Equal(expected.TemplateName, actual.TemplateName);
		Assert.Equal(expected.Workplace, actual.Workplace);
		Assert.Equal(expected.PayRate, actual.PayRate);
		Assert.Equal(expected.StartTime, actual.StartTime);
		Assert.Equal(expected.EndTime, actual.EndTime);

		var expectedBreaks = expected.UnpaidBreaks ?? [];
		var actualBreaks = actual.UnpaidBreaks ?? [];
		Assert.Equal(expectedBreaks, actualBreaks);

		Assert.DoesNotContain(
			actual.GetType().GetProperties(),
			p => p.Name.Equals("UserId", StringComparison.OrdinalIgnoreCase));
	}

	private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
	{
		response.EnsureSuccessStatusCode();
		var value = await response.Content.ReadFromJsonAsync<T>();
		Assert.NotNull(value);
		return value;
	}

	// GET
	[Fact]
	public async Task GetAllShiftTemplates_ReturnsOnlyCurrentUsersShiftTemplates()
	{
		var response = await _client.GetAsync("/api/ShiftTemplates");
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		var returned = await ReadJsonAsync<List<ShiftTemplateDTO>>(response);

		// FakeAuth user is "test-user-id" => only 2 seeded templates.
		Assert.Equal(2, returned.Count);
		Assert.Contains(returned, st => st.TemplateName == "KFC-Open");
		Assert.Contains(returned, st => st.TemplateName == "McDonald-Close");
		Assert.DoesNotContain(returned, st => st.TemplateName == "OtherUser-Template");
	}

	[Fact]
	public async Task GetShiftTemplate_ByTemplateName_ReturnsCorrectTemplate()
	{
		var response = await _client.GetAsync("/api/ShiftTemplates/KFC-Open");
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		var returned = await ReadJsonAsync<ShiftTemplateDTO>(response);

		var expected = _fixture.TestDataShiftTemplates.Single(t => t.TemplateName == "KFC-Open");
		AssertShiftTemplateMatches(expected, returned);
	}

	[Fact]
	public async Task GetShiftTemplate_WhenNotFound_ReturnsNotFound()
	{
		var response = await _client.GetAsync("/api/ShiftTemplates/DoesNotExist");
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	[Theory]
	[InlineData("%20%20KFC-Open")] // Leading spaces
	[InlineData("KFC-Open%20%20")] // Trailing spaces
	public async Task GetShiftTemplate_ByTemplateName_WithLeadingOrTrailingSpaces_ReturnsCorrectTemplate(string templateName)
	{
		var response = await _client.GetAsync($"/api/ShiftTemplates/{templateName}");
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		var returned = await ReadJsonAsync<ShiftTemplateDTO>(response);
		Assert.Equal("KFC-Open", returned.TemplateName);
	}

	// POST
	[Fact]
	public async Task PostShiftTemplate_NewTemplate_ReturnsCreated_AndCanBeRetrieved()
	{
		var newTemplate = new ShiftTemplateDTO
		{
			TemplateName = "Starbucks-Open",
			Workplace = "Starbucks",
			PayRate = 40m,
			StartTime = DateTime.Parse("2024-01-01T09:00:00Z"),
			EndTime = DateTime.Parse("2024-01-01T17:00:00Z"),
			UnpaidBreaks = [TimeSpan.FromMinutes(30)],
		};

		var postResponse = await _client.PostAsJsonAsync("/api/ShiftTemplates", newTemplate);
		Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

		var created = await ReadJsonAsync<ShiftTemplateDTO>(postResponse);
		AssertShiftTemplateMatches(newTemplate, created);

		var getResponse = await _client.GetAsync($"/api/ShiftTemplates/{Uri.EscapeDataString(newTemplate.TemplateName)}");
		Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

		var fetched = await ReadJsonAsync<ShiftTemplateDTO>(getResponse);
		AssertShiftTemplateMatches(newTemplate, fetched);
	}

	[Fact]
	public async Task PostShiftTemplate_WithExistingTemplateName_ReturnsConflict()
	{
		var existing = _fixture.TestDataShiftTemplates.Single(t => t.TemplateName == "KFC-Open");

		var payload = new ShiftTemplateDTO
		{
			TemplateName = existing.TemplateName,
			Workplace = existing.Workplace,
			PayRate = existing.PayRate,
			StartTime = existing.StartTime,
			EndTime = existing.EndTime,
			UnpaidBreaks = existing.UnpaidBreaks,
		};

		var response = await _client.PostAsJsonAsync("/api/ShiftTemplates", payload);
		Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
	}

	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData("   ")]
	public async Task PostShiftTemplate_WithEmptyOrWhitespaceTemplateName_ReturnsBadRequest(string templateName)
	{
		var payload = new ShiftTemplateDTO
		{
			TemplateName = templateName,
			Workplace = "KFC",
			PayRate = 10m,
			StartTime = DateTime.Parse("2024-01-01T09:00:00Z"),
			EndTime = DateTime.Parse("2024-01-01T17:00:00Z"),
			UnpaidBreaks = [],
		};

		var response = await _client.PostAsJsonAsync("/api/ShiftTemplates", payload);

		Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
	}

	[Fact]
	public async Task PostShiftTemplate_WithInvalidModel_ReturnsBadRequest()
	{
		var invalid = new
		{
			TemplateName = "Invalid",
			Workplace = "KFC",
			PayRate = 20m,
			StartTime = "2024-01-01T09:00:00Z",
		};

		var response = await _client.PostAsJsonAsync("/api/ShiftTemplates", invalid);
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	// PUT
	[Fact]
	public async Task PutShiftTemplate_ExistingTemplate_ReturnsUpdatedTemplate()
	{
		var update = new ShiftTemplateDTO
		{
			TemplateName = "KFC-Open",
			Workplace = "KFC",
			PayRate = 55m,
			StartTime = DateTime.Parse("2024-02-02T08:00:00Z"),
			EndTime = DateTime.Parse("2024-02-02T16:00:00Z"),
			UnpaidBreaks = [TimeSpan.FromMinutes(10)],
		};

		var response = await _client.PutAsJsonAsync($"/api/ShiftTemplates/{Uri.EscapeDataString(update.TemplateName)}", update);
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);

		var returned = await ReadJsonAsync<ShiftTemplateDTO>(response);
		AssertShiftTemplateMatches(update, returned);

		var getResponse = await _client.GetAsync($"/api/ShiftTemplates/{Uri.EscapeDataString(update.TemplateName)}");
		var fetched = await ReadJsonAsync<ShiftTemplateDTO>(getResponse);
		AssertShiftTemplateMatches(update, fetched);
	}

	[Fact]
	public async Task PutShiftTemplate_WhenNotFound_ReturnsNotFound()
	{
		var update = new ShiftTemplateDTO
		{
			TemplateName = "DoesNotExist",
			Workplace = "KFC",
			PayRate = 10m,
			StartTime = DateTime.Parse("2024-01-01T09:00:00Z"),
			EndTime = DateTime.Parse("2024-01-01T17:00:00Z"),
			UnpaidBreaks = [],
		};

		var response = await _client.PutAsJsonAsync("/api/ShiftTemplates/DoesNotExist", update);
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	[Theory]
	[InlineData(" ")]
	[InlineData("   ")]
	public async Task PutShiftTemplate_WithWhitespaceRouteTemplateName_ReturnsBadRequest(string templateName)
	{
		var update = new ShiftTemplateDTO
		{
			TemplateName = "KFC-Open",
			Workplace = "KFC",
			PayRate = 10m,
			StartTime = DateTime.Parse("2024-01-01T09:00:00Z"),
			EndTime = DateTime.Parse("2024-01-01T17:00:00Z"),
			UnpaidBreaks = [],
		};

		var response = await _client.PutAsJsonAsync($"/api/ShiftTemplates/{Uri.EscapeDataString(templateName)}", update);
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task PutShiftTemplate_WithEmptyRouteTemplateName_ReturnsMethodNotAllowed()
	{
		var update = new ShiftTemplateDTO
		{
			TemplateName = "KFC-Open",
			Workplace = "KFC",
			PayRate = 10m,
			StartTime = DateTime.Parse("2024-01-01T09:00:00Z"),
			EndTime = DateTime.Parse("2024-01-01T17:00:00Z"),
			UnpaidBreaks = [],
		};

		var response = await _client.PutAsJsonAsync("/api/ShiftTemplates/", update);
		Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
	}

	// DELETE
	[Fact]
	public async Task DeleteShiftTemplate_ByTemplateName_RemovesTemplate()
	{
		var deleteResponse = await _client.DeleteAsync("/api/ShiftTemplates/McDonald-Close");
		Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

		var getResponse = await _client.GetAsync("/api/ShiftTemplates/McDonald-Close");
		Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
	}

	[Fact]
	public async Task DeleteShiftTemplate_WhenNotFound_ReturnsNotFound()
	{
		var response = await _client.DeleteAsync("/api/ShiftTemplates/DoesNotExist");
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}
}
