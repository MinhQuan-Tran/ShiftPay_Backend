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
    public async Task GetAllShifts_WhenNoShiftsExist_ReturnsEmptyList()
    {
        // Arrange: Ensure the database is empty
        await _fixture.ClearAllShiftsAsync();

        // Act: Call the API to get all shifts
        var response = await _client.GetAsync("/api/Shifts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var returnedShifts = await response.Content.ReadFromJsonAsync<List<ShiftDTO>>();

        // Assert: Ensure the returned list is empty
        Assert.NotNull(returnedShifts);
        Assert.Empty(returnedShifts);

        // Seed test data again for other tests
        await _fixture.SeedTestDataAsync();
    }

    [Fact]
    public async Task GetAllShifts_ReturnsSuccess()
    {
        // Act: Call the API to get all shifts
        var response = await _client.GetAsync("/api/Shifts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Deserialize the response into a list of ShiftDTO
        var returnedShifts = await response.Content.ReadFromJsonAsync<List<ShiftDTO>>();
        Assert.NotNull(returnedShifts);

        // Filter the expected shifts to exclude s5
        var expectedShifts = _fixture.TestDataShifts
            .Where(s => s.Id != "s5") // Exclude s5
            .OrderBy(s => s.Id)
            .ToList();

        var actualShifts = returnedShifts.OrderBy(s => s.Id).ToList();

        // Assert: Check the number of shifts
        Assert.Equal(expectedShifts.Count, actualShifts.Count);

        // Compare each shift
        for (int i = 0; i < expectedShifts.Count; i++)
        {
            AssertShiftMatches(expectedShifts[i], actualShifts[i], compareId: true);
        }

        // Ensure s5 is not in the returned shifts
        Assert.DoesNotContain(returnedShifts, s => s.Id == "s5");

        // Ensure the response does not contain sensitive data like "userId"
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
        // Act: Call the API with the query
        var response = await _client.GetAsync($"/api/Shifts?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Deserialize the response into a list of ShiftDTO
        var returnedShifts = await response.Content.ReadFromJsonAsync<List<ShiftDTO>>();
        Assert.NotNull(returnedShifts);

        // Filter the expected shifts based on the query and exclude s5
        var expectedShifts = _fixture.TestDataShifts
            .Where(s =>
                s.Id != "s5" && // Exclude s5
                (!query.Contains("year") || s.StartTime.Year == 2023) &&
                (!query.Contains("month") || s.StartTime.Month == 10) &&
                (!query.Contains("day") || s.StartTime.Day == 15)
            )
            .ToList();

        // Assert: Compare each returned shift with the expected shifts
        Assert.Equal(expectedShifts.Count, returnedShifts.Count);
        foreach (var returnedShift in returnedShifts)
        {
            var expectedShift = expectedShifts.FirstOrDefault(s => s.Id == returnedShift.Id);
            Assert.NotNull(expectedShift); // Ensure the returned shift exists in the expected shifts
            AssertShiftMatches(expectedShift, returnedShift, compareId: true);
        }

        // Ensure s5 is not in the returned shifts
        Assert.DoesNotContain(returnedShifts, s => s.Id == "s5");

        // Ensure the response does not contain sensitive data like "userId"
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("userId", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("s1")]
    [InlineData("s2")]
    [InlineData("s3")]
    [InlineData("s4")]
    public async Task GetShiftById_ReturnsCorrectShift(string shiftId)
    {
        // Arrange: Get the expected shift
        var expectedShift = _fixture.TestDataShifts.FirstOrDefault(s => s.Id == shiftId);
        Assert.NotNull(expectedShift); // Ensure the shift exists in the test data

        // Act: Call the API to get the shift by ID
        var response = await _client.GetAsync($"/api/Shifts/{shiftId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Deserialize the response into a ShiftDTO
        var returnedShift = await response.Content.ReadFromJsonAsync<ShiftDTO>();
        Assert.NotNull(returnedShift);

        // Assert: Compare the returned shift with the expected shift
        AssertShiftMatches(expectedShift, returnedShift, compareId: true);

        // Ensure the response does not contain sensitive data like "userId"
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("userId", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetShiftById_ReturnsNotFound_ForDifferentUserId()
    {
        // Act: Call the API to get the shift with ID s5
        var response = await _client.GetAsync("/api/Shifts/s5");

        // Assert: Ensure no shift is found for s5
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

        // Act: Attempt to create a shift with an invalid model
        var response = await _client.PostAsJsonAsync("/api/Shifts", invalidShift);

        // Assert: Ensure the API returns a BadRequest status code
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // PUT
    [Fact]
    public async Task UpdateShift_ReturnsUpdatedShift()
    {
        // Arrange: Create the original shift
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

        var createdShift = await createResponse.Content.ReadFromJsonAsync<ShiftDTO>();
        Assert.NotNull(createdShift);

        // Arrange: Define the updated shift
        var updatedShift = new ShiftDTO
        {
            Id = createdShift.Id, // Use the ID of the created shift
            Workplace = "UpdatedWorkplace",
            PayRate = 25.0M,
            StartTime = DateTime.Parse("2024-05-02T09:00:00"),
            EndTime = DateTime.Parse("2024-05-02T17:00:00"),
            UnpaidBreaks = new List<TimeSpan> { TimeSpan.Parse("00:45:00") }
        };

        // Act: Update the shift
        var updateResponse = await _client.PutAsJsonAsync($"/api/Shifts/{createdShift.Id}", updatedShift);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var returnedShift = await updateResponse.Content.ReadFromJsonAsync<ShiftDTO>();
        Assert.NotNull(returnedShift);

        // Assert: Use AssertShiftMatches to validate the updated shift
        AssertShiftMatches(updatedShift, returnedShift, compareId: true);

        // Ensure the response does not contain sensitive data like "userId"
        var raw = await updateResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("userId", raw, StringComparison.OrdinalIgnoreCase);

        // Cleanup: Remove the created shift
        await CleanupShift(createdShift.Id);
    }

    [Fact]
    public async Task UpdateShift_UpdateYearMonthAndDayCorrectly()
    {
        // Arrange: Create the original shift
        var originalShift = new ShiftDTO
        {
            Workplace = "FromChangeTest",
            PayRate = 15.0M,
            StartTime = DateTime.Parse("2024-04-01T09:00:00"),
            EndTime = DateTime.Parse("2024-04-01T17:00:00"),
            UnpaidBreaks = new List<TimeSpan> { TimeSpan.Parse("00:20:00") }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/Shifts", originalShift);
        createResponse.EnsureSuccessStatusCode();

        var createdShift = await createResponse.Content.ReadFromJsonAsync<ShiftDTO>();
        Assert.NotNull(createdShift);

        // Act: Verify that no shifts are returned for the new filter before the update
        var filterBeforeUpdate = "year=2024&month=6&day=5";
        var responseBeforeUpdate = await _client.GetAsync($"/api/Shifts?{filterBeforeUpdate}");
        Assert.Equal(HttpStatusCode.OK, responseBeforeUpdate.StatusCode);

        var shiftsBeforeUpdate = await responseBeforeUpdate.Content.ReadFromJsonAsync<List<ShiftDTO>>();
        Assert.NotNull(shiftsBeforeUpdate);
        Assert.Empty(shiftsBeforeUpdate); // Ensure no shifts match the filter before the update

        // Arrange: Define the updated shift
        var updatedShift = new ShiftDTO
        {
            Id = createdShift.Id, // Use the ID of the created shift
            Workplace = "FromChangeTest",
            PayRate = 15.0M,
            StartTime = DateTime.Parse("2024-06-05T08:00:00"),
            EndTime = DateTime.Parse("2024-06-05T16:00:00"),
            UnpaidBreaks = new List<TimeSpan> { TimeSpan.Parse("00:20:00") }
        };

        // Act: Update the shift
        var updateResponse = await _client.PutAsJsonAsync($"/api/Shifts/{createdShift.Id}", updatedShift);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var returnedShift = await updateResponse.Content.ReadFromJsonAsync<ShiftDTO>();
        Assert.NotNull(returnedShift);

        // Assert: Verify the updated shift matches the expected values
        AssertShiftMatches(updatedShift, returnedShift, compareId: false);

        // Act: Verify that the updated shift is returned for the new filter
        var responseAfterUpdate = await _client.GetAsync($"/api/Shifts?{filterBeforeUpdate}");
        Assert.Equal(HttpStatusCode.OK, responseAfterUpdate.StatusCode);

        var shiftsAfterUpdate = await responseAfterUpdate.Content.ReadFromJsonAsync<List<ShiftDTO>>();
        Assert.NotNull(shiftsAfterUpdate);
        Assert.Single(shiftsAfterUpdate); // Ensure only one shift matches the filter after the update

        // Assert: Verify the returned shift matches the updated shift
        AssertShiftMatches(updatedShift, shiftsAfterUpdate.First(), compareId: true);

        // Cleanup: Remove the created shift
        await CleanupShift(createdShift.Id);
    }

    [Fact]
    public async Task UpdateShift_WithInvalidModel_ReturnsBadRequest()
    {
        var invalidShift = new
        {
            PayRate = 20.0M,
            From = "2024-06-01T08:00:00", // Invalid property name (should be StartTime)
            To = "2024-06-01T16:00:00",   // Invalid property name (should be EndTime)
            UnpaidBreaks = new[] { "00:30:00" }
        };

        // Act: Attempt to update a shift with an invalid model
        var response = await _client.PutAsJsonAsync("/api/Shifts/invalid-id", invalidShift);

        // Assert: Ensure the API returns a BadRequest status code
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

        // Act: Attempt to update a non-existent shift
        var response = await _client.PutAsJsonAsync("/api/Shifts/non-existent-id", updatedShift);

        // Assert: Ensure the API returns a NotFound status code
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
        // Act: Attempt to delete a shift with an invalid ID
        var response = await _client.DeleteAsync("/api/Shifts/invalid-id");

        // Assert: Ensure the API returns a NotFound status code
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
