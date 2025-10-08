using System.Net.Http.Json;
using ShiftDTO = ShiftPay_Backend.Models.ShiftDTO;

namespace ShiftPay_Backend.Tests;

public class ShiftControllerTests : IClassFixture<ShiftPayTestFixture>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ShiftPayTestFixture _fixture;

    public ShiftControllerTests(ShiftPayTestFixture fixture)
    {
        _client = fixture.Client;
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ClearAllShiftsAsync();
        await _fixture.SeedTestDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;


    private static void AssertShiftMatches(ShiftDTO expected, ShiftDTO actual)
    {
        // Don't compare Ids here
        Assert.Equal(expected.Workplace, actual.Workplace);
        Assert.Equal(expected.PayRate, actual.PayRate);
        Assert.Equal(expected.StartTime, actual.StartTime);
        Assert.Equal(expected.EndTime, actual.EndTime);
        Assert.Equal(expected.UnpaidBreaks.Count, actual.UnpaidBreaks.Count);

        // Check if expected has unpaid breaks (not null or empty)
        if (expected.UnpaidBreaks is null || expected.UnpaidBreaks.Count == 0)
        {
            Assert.Empty(actual.UnpaidBreaks); // Can only be empty array, not null
        }
        else
        {
            Assert.NotEmpty(expected.UnpaidBreaks);
            for (int i = 0; i < expected.UnpaidBreaks.Count; i++)
            {
                Assert.Equal(expected.UnpaidBreaks[i], actual.UnpaidBreaks[i]);
            }
        }

        Console.WriteLine($"Expected: {actual.GetType().GetProperties()}");

        Assert.False(actual.GetType().GetProperties().Any(p => p.Name.Equals("UserId", StringComparison.OrdinalIgnoreCase)),
            "DTO should not expose UserId");
    }

    // GET
    [Fact]
    public async Task GetAllShifts_ReturnsSuccess()
    {
        // Act: Call the API to get all shifts
        var response = await _client.GetAsync("/api/Shifts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Deserialize the response into a list of ShiftDTO
        var returnedShifts = await response.Content.ReadFromJsonAsync<List<ShiftDTO>>();
        Assert.NotNull(returnedShifts);

        // Filter the expected shifts to exclude S5
        var expectedShifts = _fixture.TestDataShifts
            .Where(s => s.Id != Guid.Parse(ShiftPayTestFixture.S5)) // Exclude S5
            .OrderBy(s => s.Id)
            .ToList();

        var actualShifts = returnedShifts.OrderBy(s => s.Id).ToList();

        Console.WriteLine($"Returned: {returnedShifts.Select(s => s.GetType().GetProperties())}");

        // Assert: Check the number of shifts
        Assert.Equal(expectedShifts.Count, actualShifts.Count);

        // Compare each shift
        for (int i = 0; i < expectedShifts.Count; i++)
        {
            AssertShiftMatches(expectedShifts[i], actualShifts[i]);
        }
    }

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

    [Theory]
    [InlineData("year=2023")]
    [InlineData("month=10")]
    [InlineData("day=15")]
    [InlineData("year=2023&month=10&day=15")]
    [InlineData($"id={ShiftPayTestFixture.S1}&id={ShiftPayTestFixture.S3}")]
    public async Task GetMultipleShifts_WithFilter_ReturnsSuccess(string query)
    {
        // Act: Call the API with the query
        var response = await _client.GetAsync($"/api/Shifts?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Deserialize the response into a list of ShiftDTO
        var returnedShifts = await response.Content.ReadFromJsonAsync<List<ShiftDTO>>();
        Assert.NotNull(returnedShifts);

        // Filter the expected shifts based on the query and exclude S5
        var expectedShifts = _fixture.TestDataShifts
            .Where(s =>
                s.Id != Guid.Parse(ShiftPayTestFixture.S5) && // Exclude S5
                (!query.Contains("year") || s.StartTime.Year == 2023) &&
                (!query.Contains("month") || s.StartTime.Month == 10) &&
                (!query.Contains("day") || s.StartTime.Day == 15) &&
                (!query.Contains("id") || query.Split('&').Any(q => q.Contains(s.Id?.ToString() ?? string.Empty))) // Check for specific IDs
            )
            .ToList();

        // Assert: Compare each returned shift with the expected shifts
        Assert.Equal(expectedShifts.Count, returnedShifts.Count);
        foreach (var returnedShift in returnedShifts)
        {
            var expectedShift = expectedShifts.FirstOrDefault(s => s.Id == returnedShift.Id);
            Assert.NotNull(expectedShift); // Ensure the returned shift exists in the expected shifts
            AssertShiftMatches(expectedShift, returnedShift);
        }

        // Ensure S5 is not in the returned shifts
        Assert.DoesNotContain(returnedShifts, s => s.Id == Guid.Parse(ShiftPayTestFixture.S5));
    }

    [Theory]
    [InlineData("year=2025")]
    [InlineData("month=3")]
    [InlineData("day=30")]
    [InlineData("year=2023&month=2&day=15")]
    [InlineData($"id={ShiftPayTestFixture.S5}")] // different user ID
    public async Task GetMultipleShifts_WithFilter_WhenNoShiftsExist_ReturnsEmptyList(string query)
    {
        // Act: Call the API with the query
        var response = await _client.GetAsync($"/api/Shifts?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Deserialize the response into a list of ShiftDTO
        var returnedShifts = await response.Content.ReadFromJsonAsync<List<ShiftDTO>>();
        Assert.NotNull(returnedShifts);

        // Assert: Compare each returned shift with the expected shifts
        Assert.Empty(returnedShifts);
    }


    [Theory]
    [InlineData(ShiftPayTestFixture.S1)]
    [InlineData(ShiftPayTestFixture.S2)]
    [InlineData(ShiftPayTestFixture.S3)]
    [InlineData(ShiftPayTestFixture.S4)]
    public async Task GetShiftById_ReturnsCorrectShift(string shiftId)
    {
        // Arrange: Get the expected shift
        var expectedShift = _fixture.TestDataShifts.FirstOrDefault(s => s.Id?.ToString() == shiftId);
        Assert.NotNull(expectedShift); // Ensure the shift exists in the test data

        // Act: Call the API to get the shift by ID
        var response = await _client.GetAsync($"/api/Shifts/{shiftId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Deserialize the response into a ShiftDTO
        var returnedShift = await response.Content.ReadFromJsonAsync<ShiftDTO>();
        Assert.NotNull(returnedShift);

        // Assert: Compare the returned shift with the expected shift
        AssertShiftMatches(expectedShift, returnedShift);
    }

    [Theory]
    [InlineData(ShiftPayTestFixture.S5)] // Different user ID
    [InlineData(ShiftPayTestFixture.S6)] // Non-existent ID
    public async Task GetShiftById_ReturnsNotFound(string shiftId)
    {
        // Act: Call the API to get the shift with ID
        var response = await _client.GetAsync($"/api/Shifts/{shiftId}");

        // Assert: Ensure no shift is found
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
        AssertShiftMatches(newShift, returnedShift);
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

    [Fact]
    public async Task CreateMultipleShifts_ReturnsCreated()
    {
        var shifts = new[] {
            new ShiftDTO
            {
                Workplace = "LocalShift1",
                PayRate = 50.0M,
                StartTime = DateTime.Parse("2024-01-01T09:00:00"),
                EndTime = DateTime.Parse("2024-01-01T17:00:00"),
                UnpaidBreaks = new List<TimeSpan> { TimeSpan.FromMinutes(30) }
            },
            new ShiftDTO
            {
                Workplace = "LocalShift2",
                PayRate = 60.0M,
                StartTime = DateTime.Parse("2024-01-02T09:00:00"),
                EndTime = DateTime.Parse("2024-01-02T17:00:00"),
                UnpaidBreaks = new List<TimeSpan> { TimeSpan.FromMinutes(45) }
            },
            new ShiftDTO
            {
                Workplace = "LocalShift3",
                PayRate = 70.0M,
                StartTime = DateTime.Parse("2024-01-03T09:00:00"),
                EndTime = DateTime.Parse("2024-01-03T17:00:00"),
                UnpaidBreaks = new List<TimeSpan> { TimeSpan.FromMinutes(60) }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/Shifts/batch", shifts);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var returnedShifts = await response.Content.ReadFromJsonAsync<List<ShiftDTO>>();
        Assert.NotNull(returnedShifts);
        Assert.Equal(shifts.Length, returnedShifts.Count);

        for (int i = 0; i < shifts.Length; i++)
        {
            // Not in same order
            var returnedShift = returnedShifts.FirstOrDefault(s => s.Workplace == shifts[i].Workplace);
            Assert.NotNull(returnedShift);
            AssertShiftMatches(shifts[i], returnedShift);
        }
    }

    [Fact]
    public async Task CreateMultipleShifts_WithOneInvalid_ReturnsBadRequest_AndNoShiftsAdded()
    {
        // Arrange: Prepare multiple shifts, one invalid (missing PayRate)
        var shifts = new object[]
        {
        new
        {
            Workplace = "ValidShift1",
            PayRate = 50.0M,
            StartTime = "2024-01-01T09:00:00",
            EndTime = "2024-01-01T17:00:00",
            UnpaidBreaks = new[] { "00:30:00" }
        },
        new
        {
            Workplace = "InvalidShift", // Missing PayRate (required)
            StartTime = "2024-01-02T09:00:00",
            EndTime = "2024-01-02T17:00:00",
            UnpaidBreaks = new[] { "00:45:00" }
        },
        new
        {
            Workplace = "ValidShift2",
            PayRate = 60.0M,
            StartTime = "2024-01-03T09:00:00",
            EndTime = "2024-01-03T17:00:00",
            UnpaidBreaks = new[] { "00:15:00" }
        }
        };

        // Act: Post to batch endpoint
        var response = await _client.PostAsJsonAsync("/api/Shifts/batch", shifts);

        // Assert: API should return BadRequest
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify no valid shifts were added by querying all shifts
        var getAllResponse = await _client.GetAsync("/api/Shifts");
        getAllResponse.EnsureSuccessStatusCode();

        var allShifts = await getAllResponse.Content.ReadFromJsonAsync<List<ShiftDTO>>();
        Assert.NotNull(allShifts);

        // None of the valid shifts should exist in the database
        Assert.DoesNotContain(allShifts, s => s.Workplace == "ValidShift1");
        Assert.DoesNotContain(allShifts, s => s.Workplace == "ValidShift2");
        Assert.DoesNotContain(allShifts, s => s.Workplace == "InvalidShift");
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
        AssertShiftMatches(updatedShift, returnedShift);
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
        AssertShiftMatches(updatedShift, returnedShift);

        // Act: Verify that the updated shift is returned for the new filter
        var responseAfterUpdate = await _client.GetAsync($"/api/Shifts?{filterBeforeUpdate}");
        Assert.Equal(HttpStatusCode.OK, responseAfterUpdate.StatusCode);

        var shiftsAfterUpdate = await responseAfterUpdate.Content.ReadFromJsonAsync<List<ShiftDTO>>();
        Assert.NotNull(shiftsAfterUpdate);
        Assert.Single(shiftsAfterUpdate); // Ensure only one shift matches the filter after the update

        // Assert: Verify the returned shift matches the updated shift
        AssertShiftMatches(updatedShift, shiftsAfterUpdate.First());
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
        var response = await _client.PutAsJsonAsync($"/api/Shifts/{ShiftPayTestFixture.S6}", updatedShift);

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
        var response = await _client.DeleteAsync($"/api/Shifts/{ShiftPayTestFixture.S6}");

        // Assert: Ensure the API returns a NotFound status code
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteShift_NonExistentShift_ReturnsNotFound()
    {
        // Act: Attempt to delete a shift with an non-existent ID
        var response = await _client.DeleteAsync("/api/Shifts/invalid-id");

        // Assert: Ensure the API returns a NotFound status code
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteShifts_MultipleValidShifts_ReturnsNoContentAndRemovesShifts()
    {
        // Arrange: Seed multiple shifts to delete
        var shiftsToCreate = new[]
        {
        new ShiftDTO
        {
            Workplace = "DeleteTest1",
            PayRate = 20.0M,
            StartTime = DateTime.Parse("2024-01-01T09:00:00"),
            EndTime = DateTime.Parse("2024-01-01T17:00:00"),
            UnpaidBreaks = new List<TimeSpan>()
        },
        new ShiftDTO
        {
            Workplace = "DeleteTest2",
            PayRate = 25.0M,
            StartTime = DateTime.Parse("2024-01-02T09:00:00"),
            EndTime = DateTime.Parse("2024-01-02T17:00:00"),
            UnpaidBreaks = new List<TimeSpan>()
        }
    };

        var createResponse = await _client.PostAsJsonAsync("/api/Shifts/batch", shiftsToCreate);
        createResponse.EnsureSuccessStatusCode();

        var createdShifts = await createResponse.Content.ReadFromJsonAsync<List<ShiftDTO>>();
        Assert.NotNull(createdShifts);
        Assert.Equal(shiftsToCreate.Length, createdShifts.Count);

        // Act: Delete the created shifts by IDs
        var idsQuery = string.Join("&id=", createdShifts.Select(s => s.Id));
        var deleteResponse = await _client.DeleteAsync($"/api/Shifts?id={idsQuery}");

        // Assert: Deletion successful
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Confirm deleted shifts are gone
        foreach (var shift in createdShifts)
        {
            var getResponse = await _client.GetAsync($"/api/Shifts/{shift.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }
    }

    [Fact]
    public async Task DeleteShifts_NonExistentShifts_ReturnsNotFound()
    {
        // Arrange: Use IDs unlikely to exist
        var nonExistentShiftIds = new[] { ShiftPayTestFixture.S6, ShiftPayTestFixture.S7 };
        var idsQuery = string.Join("&id=", nonExistentShiftIds);

        // Act: Attempt to delete shifts with invalid IDs
        var deleteResponse = await _client.DeleteAsync($"/api/Shifts?id={idsQuery}");

        // Assert: API returns 404 NotFound
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteShifts_InvalidShiftIds_ReturnsNotFound()
    {
        // Arrange: Use IDs unlikely to exist
        var invalidIds = new[] { "non-existent-1", "non-existent-2" };
        var idsQuery = string.Join("&id=", invalidIds);

        // Act: Attempt to delete shifts with invalid IDs
        var deleteResponse = await _client.DeleteAsync($"/api/Shifts?id={idsQuery}");

        // Assert: API returns 404 NotFound
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
    }
}
