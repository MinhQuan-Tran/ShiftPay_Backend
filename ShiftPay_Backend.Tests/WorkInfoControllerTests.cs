using System.Net;
using System.Net.Http.Json;
using ShiftPay_Backend.Models;

namespace ShiftPay_Backend.Tests;

public sealed class WorkInfoControllerTests(ShiftPayTestFixture fixture) : IClassFixture<ShiftPayTestFixture>, IAsyncLifetime
{
    private readonly HttpClient _client = fixture.Client;
    private readonly ShiftPayTestFixture _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ClearAllWorkInfosAsync();
        await _fixture.SeedWorkInfoTestDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static void AssertWorkInfoMatches(WorkInfoDTO expected, WorkInfoDTO actual)
    {
        Assert.Equal(expected.Workplace, actual.Workplace);

        var expectedRates = expected.PayRates ?? [];
        var actualRates = actual.PayRates ?? [];
        Assert.Equal(expectedRates.Order().ToList(), actualRates.Order().ToList());

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
    public async Task GetAllWorkInfos_ReturnsOnlyCurrentUsersWorkInfos()
    {
        var response = await _client.GetAsync("/api/WorkInfos");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var returned = await ReadJsonAsync<List<WorkInfoDTO>>(response);

        // FakeAuth user is "test-user-id" => only 2 seeded work infos.
        Assert.Equal(2, returned.Count);
        Assert.Contains(returned, wi => wi.Workplace == "KFC");
        Assert.Contains(returned, wi => wi.Workplace == "McDonald");

        Assert.DoesNotContain(returned, wi => wi.PayRates.Contains(99m));
    }

    [Fact]
    public async Task GetAllWorkInfos_AfterDeletingKnownWorkplaces_DoesNotReturnThem()
    {
        var deleteKfc = await _client.DeleteAsync("/api/WorkInfos/KFC");
        Assert.Equal(HttpStatusCode.NoContent, deleteKfc.StatusCode);

        var deleteMc = await _client.DeleteAsync("/api/WorkInfos/McDonald");
        Assert.Equal(HttpStatusCode.NoContent, deleteMc.StatusCode);

        var getKfc = await _client.GetAsync("/api/WorkInfos/KFC");
        Assert.Equal(HttpStatusCode.NotFound, getKfc.StatusCode);

        var getMc = await _client.GetAsync("/api/WorkInfos/McDonald");
        Assert.Equal(HttpStatusCode.NotFound, getMc.StatusCode);

        var listResponse = await _client.GetAsync("/api/WorkInfos");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var returned = await ReadJsonAsync<List<WorkInfoDTO>>(listResponse);
        Assert.DoesNotContain(returned, wi => wi.Workplace is "KFC" or "McDonald");

        await _fixture.SeedWorkInfoTestDataAsync();
    }

    [Fact]
    public async Task GetWorkInfo_ByWorkplace_ReturnsCorrectWorkInfo()
    {
        var response = await _client.GetAsync("/api/WorkInfos/KFC");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var returned = await ReadJsonAsync<WorkInfoDTO>(response);

        var expected = _fixture.TestDataWorkInfos.Single(w => w.Workplace == "KFC" && w.PayRates.Contains(25m));
        AssertWorkInfoMatches(expected, returned);
    }

    [Fact]
    public async Task GetWorkInfo_WhenNotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/WorkInfos/DoesNotExist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // POST
    [Fact]
    public async Task PostWorkInfo_NewWorkplace_ReturnsCreated_AndCanBeRetrieved()
    {
        var newWorkInfo = new WorkInfoDTO
        {
            Workplace = "NewWorkplace",
            PayRates = [42m],
        };

        var postResponse = await _client.PostAsJsonAsync("/api/WorkInfos", newWorkInfo);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var created = await ReadJsonAsync<WorkInfoDTO>(postResponse);
        AssertWorkInfoMatches(newWorkInfo, created);

        var getResponse = await _client.GetAsync("/api/WorkInfos/NewWorkplace");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await ReadJsonAsync<WorkInfoDTO>(getResponse);
        AssertWorkInfoMatches(newWorkInfo, fetched);
    }

    [Fact]
    public async Task PostWorkInfo_ExistingWorkplace_UnionsPayRates_ReturnsCreated()
    {
        var update = new WorkInfoDTO
        {
            Workplace = "KFC",
            PayRates = [30m, 35m],
        };

        var postResponse = await _client.PostAsJsonAsync("/api/WorkInfos", update);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var returned = await ReadJsonAsync<WorkInfoDTO>(postResponse);

        Assert.Equal("KFC", returned.Workplace);
        Assert.Contains(25m, returned.PayRates);
        Assert.Contains(30m, returned.PayRates);
        Assert.Contains(35m, returned.PayRates);

        var getResponse = await _client.GetAsync("/api/WorkInfos/KFC");
        var fetched = await ReadJsonAsync<WorkInfoDTO>(getResponse);

        Assert.Contains(25m, fetched.PayRates);
        Assert.Contains(30m, fetched.PayRates);
        Assert.Contains(35m, fetched.PayRates);
    }

    [Fact]
    public async Task PostWorkInfo_WithInvalidModel_ReturnsBadRequest()
    {
        var invalid = new
        {
            PayRates = new[] { 10.0m },
        };

        var response = await _client.PostAsJsonAsync("/api/WorkInfos", invalid);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // DELETE
    [Fact]
    public async Task DeleteWorkInfo_ByWorkplace_RemovesWorkInfo()
    {
        var deleteResponse = await _client.DeleteAsync("/api/WorkInfos/McDonald");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync("/api/WorkInfos/McDonald");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteWorkInfo_RemoveSpecificPayRate_LeavesOtherRates()
    {
        var deleteResponse = await _client.DeleteAsync("/api/WorkInfos/KFC?payRate=25");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync("/api/WorkInfos/KFC");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await ReadJsonAsync<WorkInfoDTO>(getResponse);
        Assert.DoesNotContain(25m, fetched.PayRates);
        Assert.Contains(30m, fetched.PayRates);
    }

    [Fact]
    public async Task DeleteWorkInfo_RemoveMissingPayRate_DoesNotChangeEntity()
    {
        var beforeResponse = await _client.GetAsync("/api/WorkInfos/KFC");
        var before = await ReadJsonAsync<WorkInfoDTO>(beforeResponse);

        var deleteResponse = await _client.DeleteAsync("/api/WorkInfos/KFC?payRate=12345");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var afterResponse = await _client.GetAsync("/api/WorkInfos/KFC");
        var after = await ReadJsonAsync<WorkInfoDTO>(afterResponse);

        Assert.Equal(before.PayRates.Order().ToList(), after.PayRates.Order().ToList());
    }

    [Fact]
    public async Task DeleteWorkInfo_WhenNotFound_ReturnsNoContent()
    {
        var response = await _client.DeleteAsync("/api/WorkInfos/DoesNotExist");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
