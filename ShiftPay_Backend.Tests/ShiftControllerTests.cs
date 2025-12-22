using System.Net;
using System.Net.Http.Json;
using ShiftDTO = ShiftPay_Backend.Models.ShiftDTO;

namespace ShiftPay_Backend.Tests;

public sealed class ShiftControllerTests(ShiftPayTestFixture fixture) : IClassFixture<ShiftPayTestFixture>, IAsyncLifetime
{
    private readonly HttpClient _client = fixture.Client;
    private readonly ShiftPayTestFixture _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ClearAllShiftsAsync();
        await _fixture.SeedShiftTestDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static void AssertShiftMatches(ShiftDTO expected, ShiftDTO actual)
    {
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
    public async Task GetAllShifts_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/api/Shifts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var returnedShifts = await ReadJsonAsync<List<ShiftDTO>>(response);

        var expectedShifts = _fixture.TestDataShifts
            .Where(s => s.Id != Guid.Parse(ShiftPayTestFixture.S5))
            .OrderBy(s => s.Id)
            .ToList();

        var actualShifts = returnedShifts.OrderBy(s => s.Id).ToList();

        Assert.Equal(expectedShifts.Count, actualShifts.Count);

        for (var i = 0; i < expectedShifts.Count; i++)
        {
            AssertShiftMatches(expectedShifts[i], actualShifts[i]);
        }
    }

    [Theory]
    [InlineData("year=2023")]
    [InlineData("month=10")]
    [InlineData("day=15")]
    [InlineData("year=2023&month=10&day=15")]
    [InlineData($"id={ShiftPayTestFixture.S1}&id={ShiftPayTestFixture.S3}")]
    public async Task GetMultipleShifts_WithFilter_ReturnsSuccess(string query)
    {
        var response = await _client.GetAsync($"/api/Shifts?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var returnedShifts = await ReadJsonAsync<List<ShiftDTO>>(response);

        var expectedShifts = _fixture.TestDataShifts
            .Where(s =>
                s.Id != Guid.Parse(ShiftPayTestFixture.S5) &&
                (!query.Contains("year", StringComparison.OrdinalIgnoreCase) || s.StartTime.Year == 2023) &&
                (!query.Contains("month", StringComparison.OrdinalIgnoreCase) || s.StartTime.Month == 10) &&
                (!query.Contains("day", StringComparison.OrdinalIgnoreCase) || s.StartTime.Day == 15) &&
                (!query.Contains("id", StringComparison.OrdinalIgnoreCase) || query.Split('&').Any(q => q.Contains(s.Id?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        Assert.Equal(expectedShifts.Count, returnedShifts.Count);

        foreach (var returnedShift in returnedShifts)
        {
            var expectedShift = expectedShifts.FirstOrDefault(s => s.Id == returnedShift.Id);
            Assert.NotNull(expectedShift);
            AssertShiftMatches(expectedShift, returnedShift);
        }

        Assert.DoesNotContain(returnedShifts, s => s.Id == Guid.Parse(ShiftPayTestFixture.S5));
    }

    [Theory]
    [InlineData("year=2025")]
    [InlineData("month=3")]
    [InlineData("day=30")]
    [InlineData("year=2023&month=2&day=15")]
    [InlineData($"id={ShiftPayTestFixture.S5}")]
    public async Task GetMultipleShifts_WithFilter_WhenNoShiftsExist_ReturnsEmptyList(string query)
    {
        var response = await _client.GetAsync($"/api/Shifts?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var returnedShifts = await ReadJsonAsync<List<ShiftDTO>>(response);
        Assert.Empty(returnedShifts);
    }

    [Theory]
    [InlineData(ShiftPayTestFixture.S1)]
    [InlineData(ShiftPayTestFixture.S2)]
    [InlineData(ShiftPayTestFixture.S3)]
    [InlineData(ShiftPayTestFixture.S4)]
    public async Task GetShiftById_ReturnsCorrectShift(string shiftId)
    {
        var expectedShift = _fixture.TestDataShifts.FirstOrDefault(s => s.Id?.ToString() == shiftId);
        Assert.NotNull(expectedShift);

        var response = await _client.GetAsync($"/api/Shifts/{shiftId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var returnedShift = await ReadJsonAsync<ShiftDTO>(response);
        AssertShiftMatches(expectedShift, returnedShift);
    }

    [Theory]
    [InlineData(ShiftPayTestFixture.S5)]
    [InlineData(ShiftPayTestFixture.S6)]
    public async Task GetShiftById_ReturnsNotFound(string shiftId)
    {
        var response = await _client.GetAsync($"/api/Shifts/{shiftId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
            UnpaidBreaks = [TimeSpan.Parse("00:45:00")],
        };

        var response = await _client.PostAsJsonAsync("/api/Shifts", newShift);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var returnedShift = await ReadJsonAsync<ShiftDTO>(response);
        AssertShiftMatches(newShift, returnedShift);
    }

    [Fact]
    public async Task CreateShift_WithInvalidModel_ReturnsBadRequest()
    {
        var invalidShift = new
        {
            Workplace = "InvalidPlace",
            StartTime = "2024-01-01T09:00:00",
            UnpaidBreaks = new[] { "00:30:00" },
        };

        var response = await _client.PostAsJsonAsync("/api/Shifts", invalidShift);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateMultipleShifts_ReturnsCreated()
    {
        var shifts = new[]
        {
            new ShiftDTO
            {
                Workplace = "LocalShift1",
                PayRate = 50.0M,
                StartTime = DateTime.Parse("2024-01-01T09:00:00"),
                EndTime = DateTime.Parse("2024-01-01T17:00:00"),
                UnpaidBreaks = [TimeSpan.FromMinutes(30)],
            },
            new ShiftDTO
            {
                Workplace = "LocalShift2",
                PayRate = 60.0M,
                StartTime = DateTime.Parse("2024-01-02T09:00:00"),
                EndTime = DateTime.Parse("2024-01-02T17:00:00"),
                UnpaidBreaks = [TimeSpan.FromMinutes(45)],
            },
            new ShiftDTO
            {
                Workplace = "LocalShift3",
                PayRate = 70.0M,
                StartTime = DateTime.Parse("2024-01-03T09:00:00"),
                EndTime = DateTime.Parse("2024-01-03T17:00:00"),
                UnpaidBreaks = [TimeSpan.FromMinutes(60)],
            },
        };

        var response = await _client.PostAsJsonAsync("/api/Shifts/batch", shifts);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var returnedShifts = await ReadJsonAsync<List<ShiftDTO>>(response);
        Assert.Equal(shifts.Length, returnedShifts.Count);

        foreach (var expected in shifts)
        {
            var actual = returnedShifts.FirstOrDefault(s => s.Workplace == expected.Workplace);
            Assert.NotNull(actual);
            AssertShiftMatches(expected, actual);
        }
    }

    [Fact]
    public async Task CreateMultipleShifts_WithOneInvalid_ReturnsBadRequest_AndNoShiftsAdded()
    {
        var shifts = new object[]
        {
            new
            {
                Workplace = "ValidShift1",
                PayRate = 50.0M,
                StartTime = "2024-01-01T09:00:00",
                EndTime = "2024-01-01T17:00:00",
                UnpaidBreaks = new[] { "00:30:00" },
            },
            new
            {
                Workplace = "InvalidShift",
                StartTime = "2024-01-02T09:00:00",
                EndTime = "2024-01-02T17:00:00",
                UnpaidBreaks = new[] { "00:45:00" },
            },
            new
            {
                Workplace = "ValidShift2",
                PayRate = 60.0M,
                StartTime = "2024-01-03T09:00:00",
                EndTime = "2024-01-03T17:00:00",
                UnpaidBreaks = new[] { "00:15:00" },
            },
        };

        var response = await _client.PostAsJsonAsync("/api/Shifts/batch", shifts);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var getAllResponse = await _client.GetAsync("/api/Shifts");
        var allShifts = await ReadJsonAsync<List<ShiftDTO>>(getAllResponse);

        Assert.DoesNotContain(allShifts, s => s.Workplace is "ValidShift1" or "ValidShift2" or "InvalidShift");
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
            UnpaidBreaks = [TimeSpan.Parse("00:30:00")],
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Shifts", originalShift);
        var createdShift = await ReadJsonAsync<ShiftDTO>(createResponse);

        var updatedShift = new ShiftDTO
        {
            Id = createdShift.Id,
            Workplace = "UpdatedWorkplace",
            PayRate = 25.0M,
            StartTime = DateTime.Parse("2024-05-02T09:00:00"),
            EndTime = DateTime.Parse("2024-05-02T17:00:00"),
            UnpaidBreaks = [TimeSpan.Parse("00:45:00")],
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/Shifts/{createdShift.Id}", updatedShift);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var returnedShift = await ReadJsonAsync<ShiftDTO>(updateResponse);
        AssertShiftMatches(updatedShift, returnedShift);
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
            UnpaidBreaks = [TimeSpan.Parse("00:20:00")],
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Shifts", originalShift);
        var createdShift = await ReadJsonAsync<ShiftDTO>(createResponse);

        var filter = "year=2024&month=6&day=5";

        var responseBeforeUpdate = await _client.GetAsync($"/api/Shifts?{filter}");
        Assert.Equal(HttpStatusCode.OK, responseBeforeUpdate.StatusCode);
        var shiftsBeforeUpdate = await ReadJsonAsync<List<ShiftDTO>>(responseBeforeUpdate);
        Assert.Empty(shiftsBeforeUpdate);

        var updatedShift = new ShiftDTO
        {
            Id = createdShift.Id,
            Workplace = "FromChangeTest",
            PayRate = 15.0M,
            StartTime = DateTime.Parse("2024-06-05T08:00:00"),
            EndTime = DateTime.Parse("2024-06-05T16:00:00"),
            UnpaidBreaks = [TimeSpan.Parse("00:20:00")],
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/Shifts/{createdShift.Id}", updatedShift);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var returnedShift = await ReadJsonAsync<ShiftDTO>(updateResponse);
        AssertShiftMatches(updatedShift, returnedShift);

        var responseAfterUpdate = await _client.GetAsync($"/api/Shifts?{filter}");
        Assert.Equal(HttpStatusCode.OK, responseAfterUpdate.StatusCode);

        var shiftsAfterUpdate = await ReadJsonAsync<List<ShiftDTO>>(responseAfterUpdate);
        var shift = Assert.Single(shiftsAfterUpdate);
        AssertShiftMatches(updatedShift, shift);
    }

    [Fact]
    public async Task UpdateShift_WithInvalidModel_ReturnsBadRequest()
    {
        var invalidShift = new
        {
            PayRate = 20.0M,
            From = "2024-06-01T08:00:00",
            To = "2024-06-01T16:00:00",
            UnpaidBreaks = new[] { "00:30:00" },
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
            UnpaidBreaks = [],
        };

        var response = await _client.PutAsJsonAsync($"/api/Shifts/{ShiftPayTestFixture.S6}", updatedShift);
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
            UnpaidBreaks = [],
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Shifts", shiftToDelete);
        var returnedShift = await ReadJsonAsync<ShiftDTO>(createResponse);

        var deleteResponse = await _client.DeleteAsync($"/api/Shifts/{returnedShift.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/Shifts/{returnedShift.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteShift_InvalidId_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync($"/api/Shifts/{ShiftPayTestFixture.S6}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteShift_NonExistentShift_ReturnsBadRequest()
    {
        var response = await _client.DeleteAsync("/api/Shifts/invalid-id");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteShifts_MultipleValidShifts_ReturnsNoContentAndRemovesShifts()
    {
        var shiftsToCreate = new[]
        {
            new ShiftDTO
            {
                Workplace = "DeleteTest1",
                PayRate = 20.0M,
                StartTime = DateTime.Parse("2024-01-01T09:00:00"),
                EndTime = DateTime.Parse("2024-01-01T17:00:00"),
                UnpaidBreaks = [],
            },
            new ShiftDTO
            {
                Workplace = "DeleteTest2",
                PayRate = 25.0M,
                StartTime = DateTime.Parse("2024-01-02T09:00:00"),
                EndTime = DateTime.Parse("2024-01-02T17:00:00"),
                UnpaidBreaks = [],
            },
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Shifts/batch", shiftsToCreate);
        var createdShifts = await ReadJsonAsync<List<ShiftDTO>>(createResponse);
        Assert.Equal(shiftsToCreate.Length, createdShifts.Count);

        var idsQuery = string.Join("&id=", createdShifts.Select(s => s.Id));
        var deleteResponse = await _client.DeleteAsync($"/api/Shifts?id={idsQuery}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        foreach (var shift in createdShifts)
        {
            var getResponse = await _client.GetAsync($"/api/Shifts/{shift.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }
    }

    [Fact]
    public async Task DeleteShifts_NonExistentShifts_ReturnsNotFound()
    {
        var nonExistentShiftIds = new[] { ShiftPayTestFixture.S6, ShiftPayTestFixture.S7 };
        var idsQuery = string.Join("&id=", nonExistentShiftIds);

        var deleteResponse = await _client.DeleteAsync($"/api/Shifts?id={idsQuery}");
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteShifts_InvalidShiftIds_ReturnsBadRequest()
    {
        var invalidIds = new[] { "non-existent-1", "non-existent-2" };
        var idsQuery = string.Join("&id=", invalidIds);

        var deleteResponse = await _client.DeleteAsync($"/api/Shifts?id={idsQuery}");
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
    }
}
