using Microsoft.AspNetCore.Mvc;
using ShiftPay_Backend.Models;

namespace ShiftPay_Backend.Tests;

[Collection("CosmosDb")]
public class WorkInfosControllerTests
{
	private readonly CosmosDbTestFixture _fixture;

	// Each test class instance gets a unique user ID to ensure test isolation
	// Database is cleaned at the start of each test run, not after each test
	private readonly string _testUserId = $"workinfos-test-{Guid.NewGuid():N}";

	public WorkInfosControllerTests(CosmosDbTestFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task GetWorkInfos_WhenNoWorkInfosExist_ReturnsEmptyList()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		// Act
		var result = await controller.GetWorkInfos();

		// Assert
		var okResult = Assert.IsType<OkObjectResult>(result.Result);
		var workInfos = Assert.IsAssignableFrom<IEnumerable<WorkInfoDTO>>(okResult.Value);
		Assert.Empty(workInfos);
	}

	[Fact]
	public async Task PostWorkInfo_WithNewWorkplace_CreatesWorkInfo()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		var workInfoDto = new WorkInfoDTO
		{
			Workplace = "Test Cafe",
			PayRates = [15.00m, 18.00m, 20.00m]
		};

		// Act
		var result = await controller.PostWorkInfo(workInfoDto);

		// Assert
		var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
		var createdWorkInfo = Assert.IsType<WorkInfoDTO>(createdResult.Value);
		Assert.Equal(workInfoDto.Workplace, createdWorkInfo.Workplace);
		Assert.Equal(workInfoDto.PayRates.Count, createdWorkInfo.PayRates.Count);
	}

	[Fact]
	public async Task PostWorkInfo_WithExistingWorkplace_MergesPayRates()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		var initialWorkInfo = new WorkInfoDTO
		{
			Workplace = "Test Restaurant",
			PayRates = [15.00m, 18.00m]
		};
		await controller.PostWorkInfo(initialWorkInfo);

		var additionalWorkInfo = new WorkInfoDTO
		{
			Workplace = "Test Restaurant",
			PayRates = [18.00m, 20.00m, 22.00m] // 18.00m is duplicate
		};

		// Act
		var result = await controller.PostWorkInfo(additionalWorkInfo);

		// Assert
		var okResult = Assert.IsType<OkObjectResult>(result.Result);
		var updatedWorkInfo = Assert.IsType<WorkInfoDTO>(okResult.Value);
		Assert.Equal("Test Restaurant", updatedWorkInfo.Workplace);
		// Should have merged unique pay rates: 15, 18, 20, 22
		Assert.Equal(4, updatedWorkInfo.PayRates.Count);
		Assert.Contains(15.00m, updatedWorkInfo.PayRates);
		Assert.Contains(18.00m, updatedWorkInfo.PayRates);
		Assert.Contains(20.00m, updatedWorkInfo.PayRates);
		Assert.Contains(22.00m, updatedWorkInfo.PayRates);
	}

	[Fact]
	public async Task GetWorkInfo_WhenWorkplaceExists_ReturnsWorkInfo()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		var workInfoDto = new WorkInfoDTO
		{
			Workplace = "Test Store",
			PayRates = [16.50m]
		};
		await controller.PostWorkInfo(workInfoDto);
		var workInfoId = WorkInfo.CreateId(workInfoDto.Workplace);

		// Act
		var result = await controller.GetWorkInfo(workInfoId);

		// Assert
		var okResult = Assert.IsType<OkObjectResult>(result.Result);
		var retrievedWorkInfo = Assert.IsType<WorkInfoDTO>(okResult.Value);
		Assert.Equal("Test Store", retrievedWorkInfo.Workplace);
		Assert.Single(retrievedWorkInfo.PayRates);
		Assert.Equal(16.50m, retrievedWorkInfo.PayRates[0]);
	}

	[Fact]
	public async Task GetWorkInfo_WhenWorkplaceDoesNotExist_ReturnsNotFound()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		// Act
		var result = await controller.GetWorkInfo(WorkInfo.CreateId("NonExistent Workplace"));

		// Assert
		Assert.IsType<NotFoundResult>(result.Result);
	}

	[Fact]
	public async Task GetWorkInfo_WithSpecialCharactersInId_ReturnsWorkInfo()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		var workInfoDto = new WorkInfoDTO
		{
			Workplace = "Joe's Coffee & Tea",
			PayRates = [17.00m]
		};
		await controller.PostWorkInfo(workInfoDto);
		var workInfoId = WorkInfo.CreateId(workInfoDto.Workplace);

		// Act
		var result = await controller.GetWorkInfo(workInfoId);

		// Assert
		var okResult = Assert.IsType<OkObjectResult>(result.Result);
		var retrievedWorkInfo = Assert.IsType<WorkInfoDTO>(okResult.Value);
		Assert.Equal("Joe's Coffee & Tea", retrievedWorkInfo.Workplace);
	}

	[Fact]
	public async Task DeleteWorkInfo_WhenWorkplaceExists_ReturnsNoContent()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		var workInfoDto = new WorkInfoDTO
		{
			Workplace = "Workplace To Delete",
			PayRates = [15.00m]
		};
		await controller.PostWorkInfo(workInfoDto);
		var workInfoId = WorkInfo.CreateId(workInfoDto.Workplace);

		// Act
		var result = await controller.DeleteWorkInfo(workInfoId, null);

		// Assert
		Assert.IsType<NoContentResult>(result);

		// Verify deleted
		var getResult = await controller.GetWorkInfo(workInfoId);
		Assert.IsType<NotFoundResult>(getResult.Result);
	}

	[Fact]
	public async Task DeleteWorkInfo_WithPayRate_RemovesOnlyThatPayRate()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		var workInfoDto = new WorkInfoDTO
		{
			Workplace = "Multi Rate Workplace",
			PayRates = [15.00m, 18.00m, 20.00m]
		};
		await controller.PostWorkInfo(workInfoDto);
		var workInfoId = WorkInfo.CreateId(workInfoDto.Workplace);

		// Act - delete only the 18.00m pay rate
		var result = await controller.DeleteWorkInfo(workInfoId, 18.00m);

		// Assert
		Assert.IsType<NoContentResult>(result);

		// Verify pay rate removed
		var getResult = await controller.GetWorkInfo(workInfoId);
		var okResult = Assert.IsType<OkObjectResult>(getResult.Result);
		var updatedWorkInfo = Assert.IsType<WorkInfoDTO>(okResult.Value);
		Assert.Equal(2, updatedWorkInfo.PayRates.Count);
		Assert.DoesNotContain(18.00m, updatedWorkInfo.PayRates);
		Assert.Contains(15.00m, updatedWorkInfo.PayRates);
		Assert.Contains(20.00m, updatedWorkInfo.PayRates);
	}

	[Fact]
	public async Task DeleteWorkInfo_WhenIdDoesNotExist_ReturnsNoContent()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		// Act - deleting non-existent id without payRate parameter
		var result = await controller.DeleteWorkInfo(WorkInfo.CreateId("NonExistent"), null);

		// Assert - returns NoContent even if not found (idempotent delete)
		Assert.IsType<NoContentResult>(result);
	}

	[Fact]
	public async Task DeleteWorkInfo_WithPayRateWhenIdDoesNotExist_ReturnsNotFound()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		// Act - trying to delete pay rate from non-existent id
		var result = await controller.DeleteWorkInfo(WorkInfo.CreateId("NonExistent"), 15.00m);

		// Assert
		Assert.IsType<NotFoundResult>(result);
	}

	[Fact]
	public async Task PostWorkInfo_WithEmptyWorkplace_ReturnsBadRequest()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		var workInfoDto = new WorkInfoDTO
		{
			Workplace = "   ", // Whitespace only
			PayRates = [15.00m]
		};

		// Act
		var result = await controller.PostWorkInfo(workInfoDto);

		// Assert
		Assert.IsType<BadRequestObjectResult>(result.Result);
	}

	[Fact]
	public async Task GetWorkInfo_WithEmptyWorkplace_ReturnsBadRequest()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		// Act
		var result = await controller.GetWorkInfo("   ");

		// Assert
		Assert.IsType<BadRequestObjectResult>(result.Result);
	}

	[Fact]
	public async Task GetWorkInfos_ReturnsAllWorkInfosForUser()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		await controller.PostWorkInfo(new WorkInfoDTO { Workplace = "Workplace A", PayRates = [15.00m] });
		await controller.PostWorkInfo(new WorkInfoDTO { Workplace = "Workplace B", PayRates = [18.00m] });
		await controller.PostWorkInfo(new WorkInfoDTO { Workplace = "Workplace C", PayRates = [20.00m] });

		// Act
		var result = await controller.GetWorkInfos();

		// Assert
		var okResult = Assert.IsType<OkObjectResult>(result.Result);
		var workInfos = Assert.IsAssignableFrom<IEnumerable<WorkInfoDTO>>(okResult.Value).ToList();
		Assert.Equal(3, workInfos.Count);
	}

	[Fact]
	public async Task PostWorkInfo_WithNegativePayRate_ReturnsBadRequest()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		var workInfoDto = new WorkInfoDTO
		{
			Workplace = "Test Workplace",
			PayRates = [-10.00m] // Negative pay rate
		};

		// Act
		var result = await controller.PostWorkInfo(workInfoDto);

		// Assert
		Assert.IsType<BadRequestObjectResult>(result.Result);
	}

	[Fact]
	public async Task DeleteWorkInfo_WithNonExistentPayRate_DoesNotModifyWorkInfo()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		var workInfoDto = new WorkInfoDTO
		{
			Workplace = "Test Workplace",
			PayRates = [15.00m, 20.00m]
		};
		await controller.PostWorkInfo(workInfoDto);
		var workInfoId = WorkInfo.CreateId(workInfoDto.Workplace);

		// Act - try to delete a pay rate that doesn't exist
		var result = await controller.DeleteWorkInfo(workInfoId, 999.00m);

		// Assert
		Assert.IsType<NoContentResult>(result);

		// Verify pay rates unchanged
		var getResult = await controller.GetWorkInfo(workInfoId);
		var okResult = Assert.IsType<OkObjectResult>(getResult.Result);
		var workInfo = Assert.IsType<WorkInfoDTO>(okResult.Value);
		Assert.Equal(2, workInfo.PayRates.Count);
	}

	[Fact]
	public async Task DeleteWorkInfo_WithEmptyWorkplace_ReturnsBadRequest()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		// Act
		var result = await controller.DeleteWorkInfo("   ", null);

		// Assert
		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task PostWorkInfo_AddingPayRatesToExisting_MergesCorrectly()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		// Create initial work info
		await controller.PostWorkInfo(new WorkInfoDTO
		{
			Workplace = "Merge Test",
			PayRates = [15.00m]
		});

		// Add more pay rates
		var result = await controller.PostWorkInfo(new WorkInfoDTO
		{
			Workplace = "Merge Test",
			PayRates = [20.00m, 25.00m]
		});

		// Assert
		var okResult = Assert.IsType<OkObjectResult>(result.Result);
		var workInfo = Assert.IsType<WorkInfoDTO>(okResult.Value);
		Assert.Equal(3, workInfo.PayRates.Count);
		Assert.Contains(15.00m, workInfo.PayRates);
		Assert.Contains(20.00m, workInfo.PayRates);
		Assert.Contains(25.00m, workInfo.PayRates);
	}

	#region Unique Key Constraint Tests

	[Fact]
	public async Task PostWorkInfo_SameWorkplaceDifferentUsers_BothSucceed()
	{
		// Arrange - Unique keys are scoped to partition (userId), so different users can have same workplace
		var userId1 = $"unique-test-user1-{Guid.NewGuid():N}";
		var userId2 = $"unique-test-user2-{Guid.NewGuid():N}";

		await using var context1 = _fixture.CreateContext();
		await using var context2 = _fixture.CreateContext();
		var controller1 = ControllerTestHelper.CreateWorkInfosController(context1, userId1);
		var controller2 = ControllerTestHelper.CreateWorkInfosController(context2, userId2);

		var workInfoDto = new WorkInfoDTO
		{
			Workplace = "Shared Workplace Name",
			PayRates = [15.00m]
		};

		// Act
		var result1 = await controller1.PostWorkInfo(workInfoDto);
		var result2 = await controller2.PostWorkInfo(workInfoDto);

		// Assert - Both should succeed since they're in different partitions
		Assert.IsType<CreatedAtActionResult>(result1.Result);
		Assert.IsType<CreatedAtActionResult>(result2.Result);
	}

	#endregion
}
