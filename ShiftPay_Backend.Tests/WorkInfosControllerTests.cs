using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
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
	public async Task GetWorkInfo_FromRawLegacyDocument_PreservesRatesWithoutProfileFields()
	{
		var id = Guid.NewGuid();
		await using (var writeContext = _fixture.CreateContext())
		{
			var container = writeContext.Database.GetCosmosClient()
				.GetContainer(writeContext.Database.GetCosmosDatabaseId(), "WorkInfos");
			var document = new Dictionary<string, object>
			{
				["id"] = id.ToString(),
				["UserId"] = _testUserId,
				["Workplace"] = "Legacy hospital",
				["PayRates"] = new[] { 25.5m, 30m },
				["$type"] = nameof(WorkInfo)
			};
			await container.CreateItemAsync(document, new PartitionKey(_testUserId), cancellationToken: TestContext.Current.CancellationToken);
		}

		await using var readContext = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(readContext, _testUserId);
		var result = await controller.GetWorkInfo(id);
		var saved = Assert.IsType<WorkInfoDTO>(Assert.IsType<OkObjectResult>(result.Result).Value);

		Assert.Equal("Legacy hospital", saved.Workplace);
		Assert.Equal(new[] { 25.5m, 30m }, saved.PayRates);
		var profile = Assert.Single(saved.Profiles!);
		Assert.Equal("legacy", profile.Id);
		Assert.Equal(new[] { "legacy-25.5", "legacy-30" }, profile.PayRates.Select(rate => rate.Id));
		Assert.Equal(new[] { 25.5m, 30m }, profile.PayRates.Select(rate => rate.HourlyRate));
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
		Assert.True(createdWorkInfo.Id.HasValue);
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
		var initialResult = await controller.PostWorkInfo(initialWorkInfo);
		var initialCreated = Assert.IsType<CreatedAtActionResult>(initialResult.Result);
		var initialWorkInfoResult = Assert.IsType<WorkInfoDTO>(initialCreated.Value);
		Assert.True(initialWorkInfoResult.Id.HasValue);

		var additionalWorkInfo = new WorkInfoDTO
		{
			Id = initialWorkInfoResult.Id,
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
		var createdResult = await controller.PostWorkInfo(workInfoDto);
		var createdAt = Assert.IsType<CreatedAtActionResult>(createdResult.Result);
		var createdWorkInfo = Assert.IsType<WorkInfoDTO>(createdAt.Value);
		Assert.True(createdWorkInfo.Id.HasValue);
		var workInfoId = createdWorkInfo.Id.Value;

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
		var result = await controller.GetWorkInfo(Guid.NewGuid());

		// Assert
		Assert.IsType<NotFoundResult>(result.Result);
	}

	[Fact]
	public async Task GetWorkInfo_WithValidId_ReturnsWorkInfo()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		var workInfoDto = new WorkInfoDTO
		{
			Workplace = "Joe's Coffee & Tea",
			PayRates = [17.00m]
		};
		var createdResult = await controller.PostWorkInfo(workInfoDto);
		var createdAt = Assert.IsType<CreatedAtActionResult>(createdResult.Result);
		var createdWorkInfo = Assert.IsType<WorkInfoDTO>(createdAt.Value);
		Assert.True(createdWorkInfo.Id.HasValue);
		var workInfoId = createdWorkInfo.Id.Value;

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
		var createdResult = await controller.PostWorkInfo(workInfoDto);
		var createdAt = Assert.IsType<CreatedAtActionResult>(createdResult.Result);
		var createdWorkInfo = Assert.IsType<WorkInfoDTO>(createdAt.Value);
		Assert.True(createdWorkInfo.Id.HasValue);
		var workInfoId = createdWorkInfo.Id.Value;

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
		var createdResult = await controller.PostWorkInfo(workInfoDto);
		var createdAt = Assert.IsType<CreatedAtActionResult>(createdResult.Result);
		var createdWorkInfo = Assert.IsType<WorkInfoDTO>(createdAt.Value);
		Assert.True(createdWorkInfo.Id.HasValue);
		var workInfoId = createdWorkInfo.Id.Value;

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
		var result = await controller.DeleteWorkInfo(Guid.NewGuid(), null);

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
		var result = await controller.DeleteWorkInfo(Guid.NewGuid(), 15.00m);

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
	public async Task GetWorkInfo_WithEmptyId_ReturnsBadRequest()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		// Act
		var result = await controller.GetWorkInfo(Guid.Empty);

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
		var createdResult = await controller.PostWorkInfo(workInfoDto);
		var createdAt = Assert.IsType<CreatedAtActionResult>(createdResult.Result);
		var createdWorkInfo = Assert.IsType<WorkInfoDTO>(createdAt.Value);
		Assert.True(createdWorkInfo.Id.HasValue);
		var workInfoId = createdWorkInfo.Id.Value;

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
	public async Task DeleteWorkInfo_WithEmptyId_ReturnsBadRequest()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);

		// Act
		var result = await controller.DeleteWorkInfo(Guid.Empty, null);

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
		var initialResult = await controller.PostWorkInfo(new WorkInfoDTO
		{
			Workplace = "Merge Test",
			PayRates = [15.00m]
		});
		var initialCreated = Assert.IsType<CreatedAtActionResult>(initialResult.Result);
		var initialWorkInfoResult = Assert.IsType<WorkInfoDTO>(initialCreated.Value);
		Assert.True(initialWorkInfoResult.Id.HasValue);

		// Add more pay rates
		var result = await controller.PostWorkInfo(new WorkInfoDTO
		{
			Id = initialWorkInfoResult.Id,
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
	public async Task PostWorkInfo_WithProfiles_PersistsDistinctNamedRatesAtSameAmount()
	{
		Guid id;
		var profileId = Guid.NewGuid().ToString();
		await using (var context = _fixture.CreateContext())
		{
			var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);
			var result = await controller.PostWorkInfo(new WorkInfoDTO
			{
				Workplace = "Profile Hospital",
				Profiles =
				[
					new WorkProfile
					{
						Id = profileId,
						Position = "Nurse",
						Location = "City campus",
						PayRates =
						[
							new NamedPayRate { Id = Guid.NewGuid().ToString(), Name = "Overnight", HourlyRate = 35m },
							new NamedPayRate { Id = Guid.NewGuid().ToString(), Name = "Weekend", HourlyRate = 35m }
						]
					}
				]
			});
			id = Assert.IsType<WorkInfoDTO>(Assert.IsType<CreatedAtActionResult>(result.Result).Value).Id!.Value;
		}

		await using var readContext = _fixture.CreateContext();
		var readController = ControllerTestHelper.CreateWorkInfosController(readContext, _testUserId);
		var readResult = await readController.GetWorkInfo(id);
		var saved = Assert.IsType<WorkInfoDTO>(Assert.IsType<OkObjectResult>(readResult.Result).Value);
		var profile = Assert.Single(saved.Profiles!);
		Assert.Equal(profileId, profile.Id);
		Assert.Equal("Nurse", profile.Position);
		Assert.Equal("City campus", profile.Location);
		Assert.Equal(2, profile.PayRates.Count);
		Assert.Equal(new[] { 35m }, saved.PayRates);
	}

	[Fact]
	public async Task PostWorkInfo_WithDifferentLocalId_MergesIntoExistingWorkplace()
	{
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);
		var created = await controller.PostWorkInfo(new WorkInfoDTO { Workplace = "Synced workplace", PayRates = [25m] });
		var existingId = Assert.IsType<WorkInfoDTO>(Assert.IsType<CreatedAtActionResult>(created.Result).Value).Id!.Value;
		var profileId = Guid.NewGuid().ToString();

		var merged = await controller.PostWorkInfo(new WorkInfoDTO
		{
			Id = Guid.NewGuid(),
			Workplace = "Synced workplace",
			Profiles =
			[
				new WorkProfile
				{
					Id = profileId,
					Location = "City campus",
					PayRates = [new NamedPayRate { Id = Guid.NewGuid().ToString(), Name = "Overnight", HourlyRate = 30m }]
				}
			]
		});

		var saved = Assert.IsType<WorkInfoDTO>(Assert.IsType<OkObjectResult>(merged.Result).Value);
		Assert.Equal(existingId, saved.Id);
		Assert.Equal(new[] { 25m, 30m }, saved.PayRates.Order());
		Assert.Contains(saved.Profiles!, profile => profile.Id == profileId);
		Assert.Contains(saved.Profiles!, profile => profile.Id == "legacy");
	}

	[Fact]
	public async Task PutWorkInfo_ReplacesProfiles_AndPersistsRateRemovals()
	{
		Guid id;
		var profileId = Guid.NewGuid().ToString();
		var rateId = Guid.NewGuid().ToString();
		await using (var context = _fixture.CreateContext())
		{
			var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);
			var created = await controller.PostWorkInfo(new WorkInfoDTO
			{
				Workplace = "Editable Hospital",
				PayRates = [25m, 30m]
			});
			id = Assert.IsType<WorkInfoDTO>(Assert.IsType<CreatedAtActionResult>(created.Result).Value).Id!.Value;
			var updated = await controller.PutWorkInfo(id, new WorkInfoDTO
			{
				Id = id,
				Workplace = "Editable Hospital",
				PayRates = [25m, 30m],
				Profiles =
				[
					new WorkProfile
					{
						Id = profileId,
						Position = "Nurse",
						PayRates = [new NamedPayRate { Id = rateId, Name = "Standard", HourlyRate = 27m }]
					}
				]
			});
			Assert.IsType<OkObjectResult>(updated.Result);
		}

		await using var readContext = _fixture.CreateContext();
		var readController = ControllerTestHelper.CreateWorkInfosController(readContext, _testUserId);
		var result = await readController.GetWorkInfo(id);
		var saved = Assert.IsType<WorkInfoDTO>(Assert.IsType<OkObjectResult>(result.Result).Value);
		Assert.Equal(new[] { 27m }, saved.PayRates);
		Assert.Equal(profileId, Assert.Single(saved.Profiles!).Id);
		Assert.Equal(rateId, Assert.Single(saved.Profiles![0].PayRates).Id);
	}

	[Fact]
	public async Task PutWorkInfo_CannotRenameWorkplace()
	{
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateWorkInfosController(context, _testUserId);
		var created = await controller.PostWorkInfo(new WorkInfoDTO { Workplace = "Original workplace", PayRates = [25m] });
		var id = Assert.IsType<WorkInfoDTO>(Assert.IsType<CreatedAtActionResult>(created.Result).Value).Id!.Value;

		var result = await controller.PutWorkInfo(id, new WorkInfoDTO { Id = id, Workplace = "Other workplace", Profiles = [] });

		Assert.IsType<BadRequestObjectResult>(result.Result);
		await using var readContext = _fixture.CreateContext();
		var readController = ControllerTestHelper.CreateWorkInfosController(readContext, _testUserId);
		var read = await readController.GetWorkInfo(id);
		var saved = Assert.IsType<WorkInfoDTO>(Assert.IsType<OkObjectResult>(read.Result).Value);
		Assert.Equal("Original workplace", saved.Workplace);
		Assert.Equal(new[] { 25m }, saved.PayRates);
	}

	[Fact]
	public async Task PutWorkInfo_CannotUpdateAnotherUsersWorkplace()
	{
		await using var ownerContext = _fixture.CreateContext();
		var owner = ControllerTestHelper.CreateWorkInfosController(ownerContext, _testUserId);
		var created = await owner.PostWorkInfo(new WorkInfoDTO { Workplace = "Private workplace", PayRates = [25m] });
		var id = Assert.IsType<WorkInfoDTO>(Assert.IsType<CreatedAtActionResult>(created.Result).Value).Id!.Value;

		await using var otherContext = _fixture.CreateContext();
		var otherUser = ControllerTestHelper.CreateWorkInfosController(otherContext, $"other-{Guid.NewGuid():N}");
		var result = await otherUser.PutWorkInfo(id, new WorkInfoDTO { Id = id, Workplace = "Private workplace", Profiles = [] });

		Assert.IsType<NotFoundResult>(result.Result);
		var original = await owner.GetWorkInfo(id);
		var saved = Assert.IsType<WorkInfoDTO>(Assert.IsType<OkObjectResult>(original.Result).Value);
		Assert.Equal(new[] { 25m }, saved.PayRates);
	}

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
