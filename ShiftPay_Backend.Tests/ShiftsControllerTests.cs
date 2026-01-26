using Microsoft.AspNetCore.Mvc;
using ShiftPay_Backend.Models;

namespace ShiftPay_Backend.Tests;

[Collection("CosmosDb")]
public class ShiftsControllerTests
{
	private readonly CosmosDbTestFixture _fixture;

	// Each test class instance gets a unique user ID to ensure test isolation
	// Database is cleaned at the start of each test run, not after each test
	private readonly string _testUserId = $"shifts-test-{Guid.NewGuid():N}";

	public ShiftsControllerTests(CosmosDbTestFixture fixture)
	{
		_fixture = fixture;
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

	[Fact]
	public async Task DeleteShifts_WithYearFilter_DeletesMatchingShifts()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

		await controller.PostShift(new ShiftDTO
		{
			Workplace = "Workplace 2024",
			PayRate = 20.00m,
			StartTime = new DateTime(2024, 5, 15, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 5, 15, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		});

		await controller.PostShift(new ShiftDTO
		{
			Workplace = "Workplace 2025",
			PayRate = 22.00m,
			StartTime = new DateTime(2025, 5, 15, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2025, 5, 15, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		});

		// Act - delete 2024 shifts
		var result = await controller.DeleteShifts(2024, null, null, null, null, null);

		// Assert
		Assert.IsType<NoContentResult>(result);

		// Verify only 2025 shift remains
		var remainingShifts = await controller.GetShifts(null, null, null, null, null, null);
		var okResult = Assert.IsType<OkObjectResult>(remainingShifts.Result);
		var shifts = Assert.IsAssignableFrom<IEnumerable<ShiftDTO>>(okResult.Value).ToList();
		Assert.Single(shifts);
		Assert.Equal("Workplace 2025", shifts[0].Workplace);
	}

	[Fact]
	public async Task DeleteShifts_WhenNoMatchingShifts_ReturnsNotFound()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

		// Act - try to delete shifts from a year with no data
		var result = await controller.DeleteShifts(1999, null, null, null, null, null);

		// Assert
		Assert.IsType<NotFoundObjectResult>(result);
	}

	[Fact]
	public async Task GetShifts_FilterByTimeRange_ReturnsMatchingShifts()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

		var earlyShift = new ShiftDTO
		{
			Workplace = "Early Shift",
			PayRate = 20.00m,
			StartTime = new DateTime(2024, 6, 1, 6, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 6, 1, 14, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		var lateShift = new ShiftDTO
		{
			Workplace = "Late Shift",
			PayRate = 22.00m,
			StartTime = new DateTime(2024, 6, 1, 18, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 6, 1, 23, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		await controller.PostShift(earlyShift);
		await controller.PostShift(lateShift);

		// Act - filter by time range that only includes the early shift
		var result = await controller.GetShifts(
			null, null, null,
			new DateTime(2024, 6, 1, 5, 0, 0, DateTimeKind.Utc),
			new DateTime(2024, 6, 1, 15, 0, 0, DateTimeKind.Utc),
			null);

		// Assert
		var okResult = Assert.IsType<OkObjectResult>(result.Result);
		var shifts = Assert.IsAssignableFrom<IEnumerable<ShiftDTO>>(okResult.Value).ToList();
		Assert.Single(shifts);
		Assert.Equal("Early Shift", shifts[0].Workplace);
	}

	[Fact]
	public async Task GetShifts_FilterByMonthAndDay_ReturnsMatchingShifts()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

		await controller.PostShift(new ShiftDTO
		{
			Workplace = "June 15th Shift",
			PayRate = 20.00m,
			StartTime = new DateTime(2024, 6, 15, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 6, 15, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		});

		await controller.PostShift(new ShiftDTO
		{
			Workplace = "June 20th Shift",
			PayRate = 22.00m,
			StartTime = new DateTime(2024, 6, 20, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 6, 20, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		});

		// Act - filter by year, month, and day
		var result = await controller.GetShifts(2024, 6, 15, null, null, null);

		// Assert
		var okResult = Assert.IsType<OkObjectResult>(result.Result);
		var shifts = Assert.IsAssignableFrom<IEnumerable<ShiftDTO>>(okResult.Value).ToList();
		Assert.Single(shifts);
		Assert.Equal("June 15th Shift", shifts[0].Workplace);
	}

	[Fact]
	public async Task PutShift_WithPartitionChange_MovesShiftToNewPartition()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

		var originalShift = new ShiftDTO
		{
			Workplace = "Original",
			PayRate = 20.00m,
			StartTime = new DateTime(2024, 6, 15, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 6, 15, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		var createResult = await controller.PostShift(originalShift);
		var createdShift = Assert.IsType<ShiftDTO>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value);

		// Update to a different month (changes partition key)
		var updatedShift = new ShiftDTO
		{
			Id = createdShift.Id,
			Workplace = "Moved",
			PayRate = 25.00m,
			StartTime = new DateTime(2024, 7, 20, 10, 0, 0, DateTimeKind.Utc), // Different month and day
			EndTime = new DateTime(2024, 7, 20, 18, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		// Act
		var result = await controller.PutShift(createdShift.Id!.Value, updatedShift);

		// Assert
		var okResult = Assert.IsType<OkObjectResult>(result.Result);
		var returnedShift = Assert.IsType<ShiftDTO>(okResult.Value);
		Assert.Equal("Moved", returnedShift.Workplace);
		Assert.Equal(new DateTime(2024, 7, 20, 10, 0, 0, DateTimeKind.Utc), returnedShift.StartTime);

		// Verify old partition is empty
		var oldPartitionShifts = await controller.GetShifts(2024, 6, 15, null, null, null);
		var oldOkResult = Assert.IsType<OkObjectResult>(oldPartitionShifts.Result);
		Assert.Empty(Assert.IsAssignableFrom<IEnumerable<ShiftDTO>>(oldOkResult.Value));
	}

	[Fact]
	public async Task PutShift_WithMismatchedIds_ReturnsBadRequest()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

		var shiftDto = new ShiftDTO
		{
			Id = Guid.NewGuid(), // Different ID in payload
			Workplace = "Test",
			PayRate = 20.00m,
			StartTime = DateTime.UtcNow.Date.AddHours(9),
			EndTime = DateTime.UtcNow.Date.AddHours(17),
			UnpaidBreaks = []
		};

		// Act - route ID different from payload ID
		var result = await controller.PutShift(Guid.NewGuid(), shiftDto);

		// Assert
		Assert.IsType<BadRequestObjectResult>(result.Result);
	}

	[Fact]
	public async Task PostShift_WithNegativePayRate_ReturnsBadRequest()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

		var invalidShift = new ShiftDTO
		{
			Workplace = "Test Workplace",
			PayRate = -10.00m, // Negative pay rate
			StartTime = DateTime.UtcNow.Date.AddHours(9),
			EndTime = DateTime.UtcNow.Date.AddHours(17),
			UnpaidBreaks = []
		};

		// Act
		var result = await controller.PostShift(invalidShift);

		// Assert
		Assert.IsType<BadRequestObjectResult>(result.Result);
	}

	[Fact]
	public async Task PostShift_WithBreaksExceedingDuration_ReturnsBadRequest()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

		var invalidShift = new ShiftDTO
		{
			Workplace = "Test Workplace",
			PayRate = 20.00m,
			StartTime = DateTime.UtcNow.Date.AddHours(9),
			EndTime = DateTime.UtcNow.Date.AddHours(12), // 3 hour shift
			UnpaidBreaks = [TimeSpan.FromHours(4)] // 4 hour break
		};

		// Act
		var result = await controller.PostShift(invalidShift);

		// Assert
		Assert.IsType<BadRequestObjectResult>(result.Result);
	}

	[Fact]
	public async Task DeleteShifts_ByIds_DeletesOnlySpecifiedShifts()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftsController(context, _testUserId);

		var shift1Result = await controller.PostShift(new ShiftDTO
		{
			Workplace = "Shift 1",
			PayRate = 20.00m,
			StartTime = DateTime.UtcNow.Date.AddHours(9),
			EndTime = DateTime.UtcNow.Date.AddHours(17),
			UnpaidBreaks = []
		});
		var shift1 = Assert.IsType<ShiftDTO>(Assert.IsType<CreatedAtActionResult>(shift1Result.Result).Value);

		var shift2Result = await controller.PostShift(new ShiftDTO
		{
			Workplace = "Shift 2",
			PayRate = 22.00m,
			StartTime = DateTime.UtcNow.Date.AddDays(1).AddHours(9),
			EndTime = DateTime.UtcNow.Date.AddDays(1).AddHours(17),
			UnpaidBreaks = []
		});
		var shift2 = Assert.IsType<ShiftDTO>(Assert.IsType<CreatedAtActionResult>(shift2Result.Result).Value);

		await controller.PostShift(new ShiftDTO
		{
			Workplace = "Shift 3",
			PayRate = 24.00m,
			StartTime = DateTime.UtcNow.Date.AddDays(2).AddHours(9),
			EndTime = DateTime.UtcNow.Date.AddDays(2).AddHours(17),
			UnpaidBreaks = []
		});

		// Act - delete first two shifts by ID
		var result = await controller.DeleteShifts(null, null, null, null, null, [shift1.Id!.Value, shift2.Id!.Value]);

		// Assert
		Assert.IsType<NoContentResult>(result);

		// Verify only Shift 3 remains
		var remainingShifts = await controller.GetShifts(null, null, null, null, null, null);
		var okResult = Assert.IsType<OkObjectResult>(remainingShifts.Result);
		var shifts = Assert.IsAssignableFrom<IEnumerable<ShiftDTO>>(okResult.Value).ToList();
		Assert.Single(shifts);
		Assert.Equal("Shift 3", shifts[0].Workplace);
	}

	[Fact]
	public async Task PutShift_WithConcurrentModification_ReturnsConflict()
	{
		// Arrange - Create a shift using context1
		await using var context1 = _fixture.CreateContext();
		var controller1 = ControllerTestHelper.CreateShiftsController(context1, _testUserId);

		var originalShift = new ShiftDTO
		{
			Workplace = "Concurrency Test",
			PayRate = 20.00m,
			StartTime = new DateTime(2024, 12, 1, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 12, 1, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		var createResult = await controller1.PostShift(originalShift);
		var createdShift = Assert.IsType<ShiftDTO>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value);
		var shiftId = createdShift.Id!.Value;

		// Load and modify the entity in context1 to ensure it's tracked with current ETag
		// This simulates a user loading data before another user modifies it
		var trackedShift = await context1.Shifts.FindAsync(
			shiftId, _testUserId,
			createdShift.StartTime.ToString("yyyy-MM"),
			createdShift.StartTime.Day);
		Assert.NotNull(trackedShift);

		// Mark as modified so EF Core will use the tracked entity (with its ETag) on subsequent queries
		trackedShift.Workplace = "Pending modification";
		context1.Entry(trackedShift).State = Microsoft.EntityFrameworkCore.EntityState.Modified;

		// Modify and save using context2 (simulating another user updating first)
		// This changes the ETag in Cosmos DB
		await using var context2 = _fixture.CreateContext();
		var controller2 = ControllerTestHelper.CreateShiftsController(context2, _testUserId);

		var update2 = new ShiftDTO
		{
			Id = shiftId,
			Workplace = "Updated by User 2",
			PayRate = 25.00m,
			StartTime = createdShift.StartTime,
			EndTime = createdShift.EndTime,
			UnpaidBreaks = []
		};

		var result2 = await controller2.PutShift(shiftId, update2);
		Assert.IsType<OkObjectResult>(result2.Result); // User 2 succeeds, ETag changes in DB

		// Now context1 has a tracked entity with stale ETag
		// When PutShift's FilterShiftsAsync runs, it returns the tracked entity (with old ETag)
		var update1 = new ShiftDTO
		{
			Id = shiftId,
			Workplace = "Updated by User 1",
			PayRate = 30.00m,
			StartTime = createdShift.StartTime,
			EndTime = createdShift.EndTime,
			UnpaidBreaks = []
		};

		// Act - This should fail because context1's tracked entity has stale ETag
		var result1 = await controller1.PutShift(shiftId, update1);

		// Assert - Should return Conflict due to ETag mismatch
		Assert.IsType<ConflictObjectResult>(result1.Result);
	}

	[Fact]
	public async Task PutShift_WhenShiftDeletedDuringUpdate_ReturnsNotFound()
	{
		// Arrange
		await using var context1 = _fixture.CreateContext();
		var controller1 = ControllerTestHelper.CreateShiftsController(context1, _testUserId);

		var originalShift = new ShiftDTO
		{
			Workplace = "Delete During Update Test",
			PayRate = 20.00m,
			StartTime = new DateTime(2024, 12, 5, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 12, 5, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		var createResult = await controller1.PostShift(originalShift);
		var createdShift = Assert.IsType<ShiftDTO>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value);
		var shiftId = createdShift.Id!.Value;

		// Delete using context2
		await using var context2 = _fixture.CreateContext();
		var controller2 = ControllerTestHelper.CreateShiftsController(context2, _testUserId);
		var deleteResult = await controller2.DeleteShift(shiftId);
		Assert.IsType<NoContentResult>(deleteResult);

		// Now try to update the deleted shift using context1
		var updateDto = new ShiftDTO
		{
			Id = shiftId,
			Workplace = "Updated After Delete",
			PayRate = 25.00m,
			StartTime = createdShift.StartTime,
			EndTime = createdShift.EndTime,
			UnpaidBreaks = []
		};

		// Act
		var result = await controller1.PutShift(shiftId, updateDto);

		// Assert - Should return NotFound since shift was deleted
		Assert.IsType<NotFoundObjectResult>(result.Result);
	}
}
