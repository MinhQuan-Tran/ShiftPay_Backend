using Microsoft.AspNetCore.Mvc;
using ShiftPay_Backend.Models;

namespace ShiftPay_Backend.Tests;

[Collection("CosmosDb")]
public class ShiftsControllerTests : IAsyncLifetime
{
    private readonly CosmosDbTestFixture _fixture;
    private readonly string _testUserId = $"shifts-test-{Guid.NewGuid():N}";

    public ShiftsControllerTests(CosmosDbTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.CleanupUserDataAsync(_testUserId);
    }

    [Fact]
    public async Task GetShifts_WhenNoShiftsExist_ReturnsEmptyList()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

        // Act
        var result = await controller.GetShifts(null, null, null, null, null, null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var shifts = Assert.IsAssignableFrom<IEnumerable<ShiftDTO>>(okResult.Value);
        Assert.Empty(shifts);
    }

    [Fact]
    public async Task PostShift_WithValidData_ReturnsCreatedShift()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

        var shiftDto = new ShiftDTO
        {
            Workplace = "Test Workplace",
            PayRate = 25.00m,
            StartTime = DateTime.UtcNow.Date.AddHours(9),
            EndTime = DateTime.UtcNow.Date.AddHours(17),
            UnpaidBreaks = [TimeSpan.FromMinutes(30)]
        };

        // Act
        var result = await controller.PostShift(shiftDto);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var createdShift = Assert.IsType<ShiftDTO>(createdResult.Value);
        Assert.NotNull(createdShift.Id);
        Assert.Equal(shiftDto.Workplace, createdShift.Workplace);
        Assert.Equal(shiftDto.PayRate, createdShift.PayRate);
        Assert.Equal(shiftDto.StartTime, createdShift.StartTime);
        Assert.Equal(shiftDto.EndTime, createdShift.EndTime);
    }

    [Fact]
    public async Task GetShift_WhenShiftExists_ReturnsShift()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

        var shiftDto = new ShiftDTO
        {
            Workplace = "Test Workplace",
            PayRate = 25.00m,
            StartTime = DateTime.UtcNow.Date.AddHours(9),
            EndTime = DateTime.UtcNow.Date.AddHours(17),
            UnpaidBreaks = []
        };

        var createResult = await controller.PostShift(shiftDto);
        var createdShift = Assert.IsType<ShiftDTO>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value);

        // Act
        var result = await controller.GetShift(createdShift.Id!.Value);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var retrievedShift = Assert.IsType<ShiftDTO>(okResult.Value);
        Assert.Equal(createdShift.Id, retrievedShift.Id);
        Assert.Equal(shiftDto.Workplace, retrievedShift.Workplace);
    }

    [Fact]
    public async Task GetShift_WhenShiftDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

        // Act
        var result = await controller.GetShift(Guid.NewGuid());

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetShifts_FilterByYear_ReturnsMatchingShifts()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

        var shift2024 = new ShiftDTO
        {
            Workplace = "Workplace 2024",
            PayRate = 20.00m,
            StartTime = new DateTime(2024, 6, 15, 9, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2024, 6, 15, 17, 0, 0, DateTimeKind.Utc),
            UnpaidBreaks = []
        };

        var shift2025 = new ShiftDTO
        {
            Workplace = "Workplace 2025",
            PayRate = 22.00m,
            StartTime = new DateTime(2025, 3, 10, 9, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2025, 3, 10, 17, 0, 0, DateTimeKind.Utc),
            UnpaidBreaks = []
        };

        await controller.PostShift(shift2024);
        await controller.PostShift(shift2025);

        // Act
        var result = await controller.GetShifts(2024, null, null, null, null, null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var shifts = Assert.IsAssignableFrom<IEnumerable<ShiftDTO>>(okResult.Value).ToList();
        Assert.Single(shifts);
        Assert.Equal("Workplace 2024", shifts[0].Workplace);
    }

    [Fact]
    public async Task PutShift_WithValidData_UpdatesShift()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

        var originalShift = new ShiftDTO
        {
            Workplace = "Original Workplace",
            PayRate = 20.00m,
            StartTime = DateTime.UtcNow.Date.AddHours(9),
            EndTime = DateTime.UtcNow.Date.AddHours(17),
            UnpaidBreaks = []
        };

        var createResult = await controller.PostShift(originalShift);
        var createdShift = Assert.IsType<ShiftDTO>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value);

        var updatedShift = new ShiftDTO
        {
            Id = createdShift.Id,
            Workplace = "Updated Workplace",
            PayRate = 30.00m,
            StartTime = createdShift.StartTime,
            EndTime = createdShift.EndTime,
            UnpaidBreaks = []
        };

        // Act
        var result = await controller.PutShift(createdShift.Id!.Value, updatedShift);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedShift = Assert.IsType<ShiftDTO>(okResult.Value);
        Assert.Equal("Updated Workplace", returnedShift.Workplace);
        Assert.Equal(30.00m, returnedShift.PayRate);
    }

    [Fact]
    public async Task PutShift_WhenShiftDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

        var shiftDto = new ShiftDTO
        {
            Workplace = "Test Workplace",
            PayRate = 20.00m,
            StartTime = DateTime.UtcNow.Date.AddHours(9),
            EndTime = DateTime.UtcNow.Date.AddHours(17),
            UnpaidBreaks = []
        };

        // Act
        var result = await controller.PutShift(Guid.NewGuid(), shiftDto);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task DeleteShift_WhenShiftExists_ReturnsNoContent()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

        var shiftDto = new ShiftDTO
        {
            Workplace = "Test Workplace",
            PayRate = 20.00m,
            StartTime = DateTime.UtcNow.Date.AddHours(9),
            EndTime = DateTime.UtcNow.Date.AddHours(17),
            UnpaidBreaks = []
        };

        var createResult = await controller.PostShift(shiftDto);
        var createdShift = Assert.IsType<ShiftDTO>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value);

        // Act
        var result = await controller.DeleteShift(createdShift.Id!.Value);

        // Assert
        Assert.IsType<NoContentResult>(result);

        // Verify deleted
        var getResult = await controller.GetShift(createdShift.Id!.Value);
        Assert.IsType<NotFoundObjectResult>(getResult.Result);
    }

    [Fact]
    public async Task DeleteShift_WhenShiftDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

        // Act
        var result = await controller.DeleteShift(Guid.NewGuid());

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task PostShiftBatch_WithValidData_CreatesMultipleShifts()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

        var shifts = new[]
        {
            new ShiftDTO
            {
                Workplace = "Workplace A",
                PayRate = 20.00m,
                StartTime = DateTime.UtcNow.Date.AddHours(9),
                EndTime = DateTime.UtcNow.Date.AddHours(17),
                UnpaidBreaks = []
            },
            new ShiftDTO
            {
                Workplace = "Workplace B",
                PayRate = 25.00m,
                StartTime = DateTime.UtcNow.Date.AddDays(1).AddHours(10),
                EndTime = DateTime.UtcNow.Date.AddDays(1).AddHours(18),
                UnpaidBreaks = []
            }
        };

        // Act
        var result = await controller.PostShiftBatch(shifts);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var createdShifts = Assert.IsAssignableFrom<IEnumerable<ShiftDTO>>(createdResult.Value).ToList();
        Assert.Equal(2, createdShifts.Count);
    }

    [Fact]
    public async Task PostShift_WithInvalidTimeRange_ReturnsBadRequest()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

        var invalidShift = new ShiftDTO
        {
            Workplace = "Test Workplace",
            PayRate = 20.00m,
            StartTime = DateTime.UtcNow.Date.AddHours(17),
            EndTime = DateTime.UtcNow.Date.AddHours(9), // End before start
            UnpaidBreaks = []
        };

        // Act
        var result = await controller.PostShift(invalidShift);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetShifts_FilterByIds_ReturnsOnlyMatchingShifts()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

        var shift1 = await controller.PostShift(new ShiftDTO
        {
            Workplace = "Workplace 1",
            PayRate = 20.00m,
            StartTime = DateTime.UtcNow.Date.AddHours(9),
            EndTime = DateTime.UtcNow.Date.AddHours(17),
            UnpaidBreaks = []
        });
        var created1 = Assert.IsType<ShiftDTO>(Assert.IsType<CreatedAtActionResult>(shift1.Result).Value);

        var shift2 = await controller.PostShift(new ShiftDTO
        {
            Workplace = "Workplace 2",
            PayRate = 22.00m,
            StartTime = DateTime.UtcNow.Date.AddDays(1).AddHours(9),
            EndTime = DateTime.UtcNow.Date.AddDays(1).AddHours(17),
            UnpaidBreaks = []
        });
        var created2 = Assert.IsType<ShiftDTO>(Assert.IsType<CreatedAtActionResult>(shift2.Result).Value);

        await controller.PostShift(new ShiftDTO
        {
            Workplace = "Workplace 3",
            PayRate = 24.00m,
            StartTime = DateTime.UtcNow.Date.AddDays(2).AddHours(9),
            EndTime = DateTime.UtcNow.Date.AddDays(2).AddHours(17),
            UnpaidBreaks = []
        });

        // Act - filter by first two IDs
        var result = await controller.GetShifts(null, null, null, null, null, [created1.Id!.Value, created2.Id!.Value]);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var shifts = Assert.IsAssignableFrom<IEnumerable<ShiftDTO>>(okResult.Value).ToList();
        Assert.Equal(2, shifts.Count);
    }
}
