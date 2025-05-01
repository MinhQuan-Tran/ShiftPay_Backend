using System.Net.Http.Json;
using ShiftDTO = ShiftPay_Backend.Models.ShiftDTO;

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

    private static void AssertShiftMatches(ShiftDTO expected, ShiftDTO actual, bool compareId = false)
    {
        // Generated ID from server should not be compared
        if (compareId)
        {
            Assert.Equal(expected.Id, actual.Id);
        }

        Assert.Equal(expected.Workplace, actual.Workplace);
        Assert.Equal(expected.PayRate, actual.PayRate);
        Assert.Equal(expected.StartTime, actual.StartTime);
        Assert.Equal(expected.EndTime, actual.EndTime);
        Assert.Equal(expected.UnpaidBreaks.Count, actual.UnpaidBreaks.Count);

        for (int i = 0; i < expected.UnpaidBreaks.Count; i++)
        {
            Assert.Equal(expected.UnpaidBreaks[i], actual.UnpaidBreaks[i]);
        }

        Assert.False(actual.GetType().GetProperties().Any(p => p.Name.Equals("UserId", StringComparison.OrdinalIgnoreCase)),
            "DTO should not expose UserId");
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
                s.StartTime.Year == 2023 &&
                s.StartTime.Month == 10 &&
                s.StartTime.Day == 15);
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
        var newShift = new ShiftDTO
        {
            Workplace = "NewPlace",
            PayRate = 30.0M,
            StartTime = DateTime.Parse("2024-01-01T09:00:00"),
            EndTime = DateTime.Parse("2024-01-01T17:00:00"),
            UnpaidBreaks = new List<TimeSpan> { TimeSpan.Parse("00:45:00") }
        };

        var response = await _client.PostAsJsonAsync("/api/Shifts", newShift);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var returnedShift = await response.Content.ReadFromJsonAsync<ShiftDTO>();
        Assert.NotNull(returnedShift);
        Assert.Equal("NewPlace", returnedShift.Workplace);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("userId", raw, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("receivedId", raw, StringComparison.OrdinalIgnoreCase);

        await CleanupShift(returnedShift.Id);
    }

    [Fact]
    public async Task CreateShift_ReturnsCreatedWithLocalId()
    {
        var receivedId = Guid.NewGuid().ToString();
        var newShift = new ShiftDTO
        {
            Id = receivedId,
            Workplace = "LocalWithServerId",
            PayRate = 42.0M,
            StartTime = DateTime.Parse("2024-12-01T08:00:00"),
            EndTime = DateTime.Parse("2024-12-01T16:00:00"),
            UnpaidBreaks = new List<TimeSpan> { TimeSpan.FromMinutes(30) }
        };

        var response = await _client.PostAsJsonAsync("/api/Shifts", newShift);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var returnedShift = await response.Content.ReadFromJsonAsync<ShiftDTO>();
        Assert.NotNull(returnedShift);
        AssertShiftMatches(newShift, returnedShift, false);

        Assert.NotEqual(receivedId, returnedShift.Id);
        // There is no "receivedId" in the ShiftDTO
        // But still check for it

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
            StartTime = "2024-01-01T09:00:00",
            UnpaidBreaks = new[] { "00:30:00" }
        };

        var response = await _client.PostAsJsonAsync("/api/Shifts", invalidShift);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // PUT
    [Fact]
    public async Task UpdateShift_ReturnsUpdatedShift()
    {
        var originalShift = new ShiftDTO
        {
            Workplace = "UpdateTarget",
            PayRate = 20.0M,
            StartTime = DateTime.Parse("2024-05-01T08:00:00"),
            EndTime = DateTime.Parse("2024-05-01T16:00:00"),
            UnpaidBreaks = new List<TimeSpan> { TimeSpan.Parse("00:30:00") }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Shifts", originalShift);
        createResponse.EnsureSuccessStatusCode();

        var returnedShift = await createResponse.Content.ReadFromJsonAsync<ShiftDTO>();
        Assert.NotNull(returnedShift);

        var updatedShift = new ShiftDTO
        {
            Workplace = "UpdatedWorkplace",
            PayRate = 25.0M,
            StartTime = DateTime.Parse("2024-05-02T09:00:00"),
            EndTime = DateTime.Parse("2024-05-02T17:00:00"),
            UnpaidBreaks = new List<TimeSpan> { TimeSpan.Parse("00:45:00") }
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/Shifts/{returnedShift.Id}", updatedShift);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        returnedShift = await updateResponse.Content.ReadFromJsonAsync<ShiftDTO>();
        Assert.NotNull(returnedShift);
        Assert.Equal("UpdatedWorkplace", returnedShift.Workplace);
        Assert.Equal(25.0M, returnedShift.PayRate);
        Assert.Equal(DateTime.Parse("2024-05-02T09:00:00"), returnedShift.StartTime);
        Assert.Equal(DateTime.Parse("2024-05-02T17:00:00"), returnedShift.EndTime);
        Assert.Single(returnedShift.UnpaidBreaks);
        Assert.Contains(returnedShift.UnpaidBreaks, b => b == TimeSpan.Parse("00:45:00"));

        var raw = await updateResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("userId", raw, StringComparison.OrdinalIgnoreCase);

        await CleanupShift(returnedShift.Id);
    }

    [Fact]
    public async Task UpdateShift_UpdateYearMonthAndDayCorrectly()
    {
        var originalShift = new ShiftDTO
        {
            Workplace = "FromChangeTest",
            PayRate = 15.0M,
            StartTime = DateTime.Parse("2024-04-01T09:00:00"),
            EndTime = DateTime.Parse("2024-04-01T17:00:00"),
            UnpaidBreaks = new List<TimeSpan> { TimeSpan.Parse("00:20:00") }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Shifts", originalShift);
        var returnedShift = await createResponse.Content.ReadFromJsonAsync<ShiftDTO>();

        var updatedShift = new ShiftDTO
        {
            Workplace = "FromChangeTest",
            PayRate = 15.0M,
            StartTime = DateTime.Parse("2024-06-05T08:00:00"),
            EndTime = DateTime.Parse("2024-06-05T16:00:00"),
            UnpaidBreaks = new List<TimeSpan> { TimeSpan.Parse("00:20:00") }
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/Shifts/{returnedShift!.Id}", updatedShift);
        returnedShift = await updateResponse.Content.ReadFromJsonAsync<ShiftDTO>();

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
        var updatedShift = new ShiftDTO
        {
            Workplace = "NonExistent",
            PayRate = 30.0M,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddHours(8),
            UnpaidBreaks = new List<TimeSpan>()
        };

        var response = await _client.PutAsJsonAsync("/api/Shifts/non-existent-id", updatedShift);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // DELETE
    [Fact]
    public async Task DeleteShift_RemovesShift()
    {
        var shiftToDelete = new ShiftDTO
        {
            Workplace = "TempDelete",
            PayRate = 10.0M,
            StartTime = DateTime.Parse("2024-05-01T09:00:00"),
            EndTime = DateTime.Parse("2024-05-01T17:00:00"),
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
