using ShiftPay_Backend.Models;
using System.Net.Http.Json;

namespace ShiftPay_Backend.Tests;

public class ShiftControllerTests : IClassFixture<ShiftPayTestFixture>
{
    private readonly HttpClient _client;
    private readonly ShiftPayTestFixture _fixture;

    public ShiftControllerTests(ShiftPayTestFixture fixture)
    {
        _client = fixture.Client;
        _fixture = fixture;
    }

    private async Task CleanupShift(string? shiftId)
    {
        if (!string.IsNullOrEmpty(shiftId))
        {
            var deleteResponse = await _client.DeleteAsync($"/api/Shifts/{shiftId}");
            Assert.True(
                deleteResponse.IsSuccessStatusCode || deleteResponse.StatusCode == HttpStatusCode.NotFound,
                $"Cleanup failed for shift ID {shiftId} with status code {deleteResponse.StatusCode}"
            );
        }
    }

    // GET
    [Fact]
    public async Task GetAllShifts_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/api/Shifts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var returnedShifts = await response.Content.ReadFromJsonAsync<List<ShiftDTO>>();
        Assert.NotNull(returnedShifts);
        Assert.Contains(returnedShifts, s => s.Workplace.Contains("TestPlace"));

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("userId", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("year=2023")]
    [InlineData("month=10")]
    [InlineData("day=15")]
    [InlineData("year=2023&month=10&day=15")]
    public async Task GetShifts_WithFilter_ReturnsSuccess(string query)
    {
        var response = await _client.GetAsync($"/api/Shifts?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var returnedShifts = await response.Content.ReadFromJsonAsync<List<ShiftDTO>>();
        Assert.NotNull(returnedShifts);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("userId", raw, StringComparison.OrdinalIgnoreCase);

        if (query.Contains("year") && query.Contains("month") && query.Contains("day"))
        {
            Assert.Contains(returnedShifts, s =>
                s.From.Year == 2023 &&
                s.From.Month == 10 &&
                s.From.Day == 15);
        }
    }

    [Theory]
    [InlineData(0, "TestPlace1")]
    [InlineData(1, "TestPlace2")]
    [InlineData(2, "TestPlace3")]
    public async Task GetShiftById_ReturnsCorrectShift(int index, string expectedWorkplace)
    {
        var shiftId = _fixture.TestDataShifts[index].Id;

        var response = await _client.GetAsync($"/api/Shifts/{shiftId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var returnedShift = await response.Content.ReadFromJsonAsync<ShiftDTO>();
        Assert.NotNull(returnedShift);
        Assert.Equal(expectedWorkplace, returnedShift.Workplace);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("userId", raw, StringComparison.OrdinalIgnoreCase);
    }

    // POST
    [Fact]
    public async Task CreateShift_ReturnsCreated()
    {
        var newShift = new Shift
        {
            Workplace = "NewPlace",
            PayRate = 30.0M,
            From = DateTime.Parse("2024-01-01T09:00:00"),
            To = DateTime.Parse("2024-01-01T17:00:00"),
            UnpaidBreaks = new List<TimeSpan> { TimeSpan.Parse("00:45:00") }
        };

        var response = await _client.PostAsJsonAsync("/api/Shifts", newShift);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var returnedShift = await response.Content.ReadFromJsonAsync<ShiftDTO>();
        Assert.NotNull(returnedShift);
        Assert.Equal("NewPlace", returnedShift.Workplace);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("userId", raw, StringComparison.OrdinalIgnoreCase);

        await CleanupShift(returnedShift.Id);
    }

    [Fact]
    public async Task CreateShift_WithInvalidModel_ReturnsBadRequest()
    {
        var invalidShift = new
        {
            Workplace = "InvalidPlace",
            From = "2024-01-01T09:00:00",
            UnpaidBreaks = new[] { "00:30:00" }
        };

        var response = await _client.PostAsJsonAsync("/api/Shifts", invalidShift);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // PUT
    [Fact]
    public async Task UpdateShift_ReturnsUpdatedShift()
    {
        var originalShift = new Shift
        {
            Workplace = "UpdateTarget",
            PayRate = 20.0M,
            From = DateTime.Parse("2024-05-01T08:00:00"),
            To = DateTime.Parse("2024-05-01T16:00:00"),
            UnpaidBreaks = new List<TimeSpan> { TimeSpan.Parse("00:30:00") }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Shifts", originalShift);
        createResponse.EnsureSuccessStatusCode();

        var returnedShift = await createResponse.Content.ReadFromJsonAsync<ShiftDTO>();
        Assert.NotNull(returnedShift);

        var updatedShift = new Shift
        {
            Workplace = "UpdatedWorkplace",
            PayRate = 25.0M,
            From = DateTime.Parse("2024-05-02T09:00:00"),
            To = DateTime.Parse("2024-05-02T17:00:00"),
            UnpaidBreaks = new List<TimeSpan> { TimeSpan.Parse("00:45:00") }
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/Shifts/{returnedShift.Id}", updatedShift);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        returnedShift = await updateResponse.Content.ReadFromJsonAsync<ShiftDTO>();
        Assert.NotNull(returnedShift);
        Assert.Equal("UpdatedWorkplace", returnedShift.Workplace);
        Assert.Equal(25.0M, returnedShift.PayRate);
        Assert.Equal(DateTime.Parse("2024-05-02T09:00:00"), returnedShift.From);
        Assert.Equal(DateTime.Parse("2024-05-02T17:00:00"), returnedShift.To);
        Assert.Single(returnedShift.UnpaidBreaks);
        Assert.Contains(returnedShift.UnpaidBreaks, b => b == TimeSpan.Parse("00:45:00"));

        var raw = await updateResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("userId", raw, StringComparison.OrdinalIgnoreCase);

        await CleanupShift(returnedShift.Id);
    }

    [Fact]
    public async Task UpdateShift_UpdateYearMonthAndDayCorrectly()
    {
        var originalShift = new Shift
        {
            Workplace = "FromChangeTest",
            PayRate = 15.0M,
            From = DateTime.Parse("2024-04-01T09:00:00"),
            To = DateTime.Parse("2024-04-01T17:00:00"),
            UnpaidBreaks = new List<TimeSpan> { TimeSpan.Parse("00:20:00") }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Shifts", originalShift);
        var returnedShift = await createResponse.Content.ReadFromJsonAsync<ShiftDTO>();

        var updatedShift = new Shift
        {
            Workplace = "FromChangeTest",
            PayRate = 15.0M,
            From = DateTime.Parse("2024-06-05T08:00:00"),
            To = DateTime.Parse("2024-06-05T16:00:00"),
            UnpaidBreaks = new List<TimeSpan> { TimeSpan.Parse("00:20:00") }
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/Shifts/{returnedShift!.Id}", updatedShift);
        returnedShift = await updateResponse.Content.ReadFromJsonAsync<ShiftDTO>();

        Assert.Equal("2024-06", returnedShift?.YearMonth);
        Assert.Equal(5, returnedShift?.Day);

        await CleanupShift(returnedShift!.Id);
    }

    [Fact]
    public async Task UpdateShift_WithInvalidModel_ReturnsBadRequest()
    {
        var invalidShift = new
        {
            PayRate = 20.0M,
            From = "2024-06-01T08:00:00",
            To = "2024-06-01T16:00:00",
            UnpaidBreaks = new[] { "00:30:00" }
        };

        var response = await _client.PutAsJsonAsync("/api/Shifts/invalid-id", invalidShift);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateShift_NonExistentShift_ReturnsNotFound()
    {
        var updatedShift = new Shift
        {
            Workplace = "NonExistent",
            PayRate = 30.0M,
            From = DateTime.Now,
            To = DateTime.Now.AddHours(8),
            UnpaidBreaks = new List<TimeSpan>()
        };

        var response = await _client.PutAsJsonAsync("/api/Shifts/non-existent-id", updatedShift);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // DELETE
    [Fact]
    public async Task DeleteShift_RemovesShift()
    {
        var shiftToDelete = new Shift
        {
            Workplace = "TempDelete",
            PayRate = 10.0M,
            From = DateTime.Parse("2024-05-01T09:00:00"),
            To = DateTime.Parse("2024-05-01T17:00:00"),
            UnpaidBreaks = new List<TimeSpan>()
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Shifts", shiftToDelete);
        createResponse.EnsureSuccessStatusCode();

        var returnedShift = await createResponse.Content.ReadFromJsonAsync<ShiftDTO>();
        Assert.NotNull(returnedShift);

        var deleteResponse = await _client.DeleteAsync($"/api/Shifts/{returnedShift.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/Shifts/{returnedShift.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteShift_InvalidId_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/api/Shifts/invalid-id");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
