using Microsoft.AspNetCore.Mvc;
using ShiftPay_Backend.Models;

namespace ShiftPay_Backend.Tests;

[Collection("CosmosDb")]
public class ShiftTemplatesControllerTests
{
	private readonly CosmosDbTestFixture _fixture;

	// Each test class instance gets a unique user ID to ensure test isolation
	// Database is cleaned at the start of each test run, not after each test
	private readonly string _testUserId = $"templates-test-{Guid.NewGuid():N}";

	public ShiftTemplatesControllerTests(CosmosDbTestFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task GetShiftTemplates_WhenNoTemplatesExist_ReturnsEmptyList()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		// Act
		var result = await controller.GetShiftTemplates();

		// Assert
		var okResult = Assert.IsType<OkObjectResult>(result.Result);
		var templates = Assert.IsAssignableFrom<IEnumerable<ShiftTemplateDTO>>(okResult.Value);
		Assert.Empty(templates);
	}

	[Fact]
	public async Task PostShiftTemplate_WithNewTemplate_CreatesTemplate()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		var templateDto = new ShiftTemplateDTO
		{
			TemplateName = "Morning Shift",
			Workplace = "Coffee Shop",
			PayRate = 18.00m,
			StartTime = new DateTime(2024, 1, 1, 6, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 14, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = [TimeSpan.FromMinutes(30)]
		};

		// Act
		var result = await controller.PostShiftTemplate(templateDto);

		// Assert
		var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
		var createdTemplate = Assert.IsType<ShiftTemplateDTO>(createdResult.Value);
		Assert.NotNull(createdTemplate.Id);
		Assert.Equal(templateDto.TemplateName, createdTemplate.TemplateName);
		Assert.Equal(templateDto.Workplace, createdTemplate.Workplace);
		Assert.Equal(templateDto.PayRate, createdTemplate.PayRate);
	}

	[Fact]
	public async Task PostShiftTemplate_WithExistingTemplateName_UpdatesTemplate()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		var initialTemplate = new ShiftTemplateDTO
		{
			TemplateName = "Evening Shift",
			Workplace = "Restaurant",
			PayRate = 20.00m,
			StartTime = new DateTime(2024, 1, 1, 17, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 23, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};
		await controller.PostShiftTemplate(initialTemplate);

		var updatedTemplate = new ShiftTemplateDTO
		{
			TemplateName = "Evening Shift", // Same name
			Workplace = "Restaurant",
			PayRate = 25.00m, // Updated pay rate
			StartTime = new DateTime(2024, 1, 1, 18, 0, 0, DateTimeKind.Utc), // Updated time
			EndTime = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = [TimeSpan.FromMinutes(15)]
		};

		// Act
		var result = await controller.PostShiftTemplate(updatedTemplate);

		// Assert
		var okResult = Assert.IsType<OkObjectResult>(result.Result);
		var returnedTemplate = Assert.IsType<ShiftTemplateDTO>(okResult.Value);
		Assert.Equal(25.00m, returnedTemplate.PayRate);
		Assert.Equal(new DateTime(2024, 1, 1, 18, 0, 0, DateTimeKind.Utc), returnedTemplate.StartTime);
	}

	[Fact]
	public async Task GetShiftTemplate_WhenTemplateExists_ReturnsTemplate()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		var templateDto = new ShiftTemplateDTO
		{
			TemplateName = "Day Shift",
			Workplace = "Office",
			PayRate = 22.00m,
			StartTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = [TimeSpan.FromHours(1)]
		};
		var createResult = await controller.PostShiftTemplate(templateDto);
		var createdResult = Assert.IsType<CreatedAtActionResult>(createResult.Result);
		var createdTemplate = Assert.IsType<ShiftTemplateDTO>(createdResult.Value);
		Assert.NotNull(createdTemplate.Id);

		// Act
		var result = await controller.GetShiftTemplate(createdTemplate.Id.Value);

		// Assert
		var okResult = Assert.IsType<OkObjectResult>(result.Result);
		var retrievedTemplate = Assert.IsType<ShiftTemplateDTO>(okResult.Value);
		Assert.Equal("Day Shift", retrievedTemplate.TemplateName);
		Assert.Equal("Office", retrievedTemplate.Workplace);
		Assert.Equal(22.00m, retrievedTemplate.PayRate);
	}

	[Fact]
	public async Task GetShiftTemplate_WhenTemplateDoesNotExist_ReturnsNotFound()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		// Act
		var result = await controller.GetShiftTemplate(Guid.NewGuid());

		// Assert
		Assert.IsType<NotFoundObjectResult>(result.Result);
	}

	[Fact]
	public async Task GetShiftTemplate_WithSpecialCharacterTemplateName_ReturnsTemplate()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		var templateDto = new ShiftTemplateDTO
		{
			TemplateName = "Weekend Special & Holiday",
			Workplace = "Retail Store",
			PayRate = 28.00m,
			StartTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 18, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};
		var createResult = await controller.PostShiftTemplate(templateDto);
		var createdResult = Assert.IsType<CreatedAtActionResult>(createResult.Result);
		var createdTemplate = Assert.IsType<ShiftTemplateDTO>(createdResult.Value);
		Assert.NotNull(createdTemplate.Id);

		// Act
		var result = await controller.GetShiftTemplate(createdTemplate.Id.Value);

		// Assert
		var okResult = Assert.IsType<OkObjectResult>(result.Result);
		var retrievedTemplate = Assert.IsType<ShiftTemplateDTO>(okResult.Value);
		Assert.Equal("Weekend Special & Holiday", retrievedTemplate.TemplateName);
	}

	[Fact]
	public async Task DeleteShiftTemplate_WhenTemplateExists_ReturnsNoContent()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		var templateDto = new ShiftTemplateDTO
		{
			TemplateName = "Template To Delete",
			Workplace = "Test Workplace",
			PayRate = 15.00m,
			StartTime = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 16, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};
		var createResult = await controller.PostShiftTemplate(templateDto);
		var createdResult = Assert.IsType<CreatedAtActionResult>(createResult.Result);
		var createdTemplate = Assert.IsType<ShiftTemplateDTO>(createdResult.Value);
		Assert.NotNull(createdTemplate.Id);

		// Act
		var result = await controller.DeleteShiftTemplate(createdTemplate.Id.Value);

		// Assert
		Assert.IsType<NoContentResult>(result);

		// Verify deleted
		var getResult = await controller.GetShiftTemplate(createdTemplate.Id.Value);
		Assert.IsType<NotFoundObjectResult>(getResult.Result);
	}

	[Fact]
	public async Task DeleteShiftTemplate_WhenTemplateDoesNotExist_ReturnsNotFound()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		// Act
		var result = await controller.DeleteShiftTemplate(Guid.NewGuid());

		// Assert
		Assert.IsType<NotFoundObjectResult>(result);
	}

	[Fact]
	public async Task PostShiftTemplate_WithEmptyTemplateName_ReturnsBadRequest()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		var templateDto = new ShiftTemplateDTO
		{
			TemplateName = "   ", // Whitespace only
			Workplace = "Test Workplace",
			PayRate = 15.00m,
			StartTime = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 16, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		// Act
		var result = await controller.PostShiftTemplate(templateDto);

		// Assert
		Assert.IsType<BadRequestObjectResult>(result.Result);
	}

	[Fact]
	public async Task PostShiftTemplate_WithInvalidTimeRange_ReturnsBadRequest()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		var templateDto = new ShiftTemplateDTO
		{
			TemplateName = "Invalid Template",
			Workplace = "Test Workplace",
			PayRate = 15.00m,
			StartTime = new DateTime(2024, 1, 1, 17, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc), // End before start
			UnpaidBreaks = []
		};

		// Act
		var result = await controller.PostShiftTemplate(templateDto);

		// Assert
		Assert.IsType<BadRequestObjectResult>(result.Result);
	}

	[Fact]
	public async Task GetShiftTemplates_ReturnsAllTemplatesForUser()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		await controller.PostShiftTemplate(new ShiftTemplateDTO
		{
			TemplateName = "Template A",
			Workplace = "Workplace A",
			PayRate = 15.00m,
			StartTime = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 16, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		});
		await controller.PostShiftTemplate(new ShiftTemplateDTO
		{
			TemplateName = "Template B",
			Workplace = "Workplace B",
			PayRate = 18.00m,
			StartTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		});
		await controller.PostShiftTemplate(new ShiftTemplateDTO
		{
			TemplateName = "Template C",
			Workplace = "Workplace C",
			PayRate = 20.00m,
			StartTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 18, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		});

		// Act
		var result = await controller.GetShiftTemplates();

		// Assert
		var okResult = Assert.IsType<OkObjectResult>(result.Result);
		var templates = Assert.IsAssignableFrom<IEnumerable<ShiftTemplateDTO>>(okResult.Value).ToList();
		Assert.Equal(3, templates.Count);
	}

	[Fact]
	public async Task PostShiftTemplate_WithNegativePayRate_ReturnsBadRequest()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		var templateDto = new ShiftTemplateDTO
		{
			TemplateName = "Invalid Pay Template",
			Workplace = "Test Workplace",
			PayRate = -10.00m, // Negative pay rate
			StartTime = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 16, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		// Act
		var result = await controller.PostShiftTemplate(templateDto);

		// Assert
		Assert.IsType<BadRequestObjectResult>(result.Result);
	}

	[Fact]
	public async Task PostShiftTemplate_WithBreaksExceedingShiftDuration_ReturnsBadRequest()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		var templateDto = new ShiftTemplateDTO
		{
			TemplateName = "Invalid Breaks Template",
			Workplace = "Test Workplace",
			PayRate = 15.00m,
			StartTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc), // 3 hour shift
			UnpaidBreaks = [TimeSpan.FromHours(4)] // 4 hour break exceeds shift
		};

		// Act
		var result = await controller.PostShiftTemplate(templateDto);

		// Assert
		Assert.IsType<BadRequestObjectResult>(result.Result);
	}

	[Fact]
	public async Task GetShiftTemplate_WithEmptyTemplateName_ReturnsBadRequest()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		// Act
		var result = await controller.GetShiftTemplate(Guid.Empty);

		// Assert
		Assert.IsType<BadRequestObjectResult>(result.Result);
	}

	[Fact]
	public async Task DeleteShiftTemplate_WithEmptyTemplateName_ReturnsBadRequest()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		// Act
		var result = await controller.DeleteShiftTemplate(Guid.Empty);

		// Assert
		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task PostShiftTemplate_WithEmptyWorkplace_ReturnsBadRequest()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		var templateDto = new ShiftTemplateDTO
		{
			TemplateName = "Valid Template",
			Workplace = "   ", // Whitespace only
			PayRate = 15.00m,
			StartTime = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 16, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		// Act
		var result = await controller.PostShiftTemplate(templateDto);

		// Assert
		Assert.IsType<BadRequestObjectResult>(result.Result);
	}

	[Fact]
	public async Task PostShiftTemplate_WithNegativeBreaks_ReturnsBadRequest()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		var templateDto = new ShiftTemplateDTO
		{
			TemplateName = "Invalid Breaks",
			Workplace = "Test Workplace",
			PayRate = 15.00m,
			StartTime = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 16, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = [TimeSpan.FromMinutes(-30)] // Negative break
		};

		// Act
		var result = await controller.PostShiftTemplate(templateDto);

		// Assert
		Assert.IsType<BadRequestObjectResult>(result.Result);
	}

	[Fact]
	public async Task PostShiftTemplate_UpdateExistingWithInvalidData_ReturnsBadRequest()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		// Create a valid template first
		await controller.PostShiftTemplate(new ShiftTemplateDTO
		{
			TemplateName = "Existing Template",
			Workplace = "Valid Workplace",
			PayRate = 20.00m,
			StartTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		});

		// Try to update with invalid pay rate
		var invalidUpdate = new ShiftTemplateDTO
		{
			TemplateName = "Existing Template", // Same name to trigger update
			Workplace = "Valid Workplace",
			PayRate = -5.00m, // Invalid negative pay rate
			StartTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		// Act
		var result = await controller.PostShiftTemplate(invalidUpdate);

		// Assert
		Assert.IsType<BadRequestObjectResult>(result.Result);
	}

	[Fact]
	public async Task PostShiftTemplate_WithMultipleBreaks_CreatesSuccessfully()
	{
		// Arrange
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		var templateDto = new ShiftTemplateDTO
		{
			TemplateName = "Multiple Breaks Template",
			Workplace = "Test Workplace",
			PayRate = 20.00m,
			StartTime = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 18, 0, 0, DateTimeKind.Utc), // 10 hour shift
			UnpaidBreaks = [TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(15)] // 1 hour total
		};

		// Act
		var result = await controller.PostShiftTemplate(templateDto);

		// Assert
		var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
		var createdTemplate = Assert.IsType<ShiftTemplateDTO>(createdResult.Value);
		Assert.Equal(3, createdTemplate.UnpaidBreaks.Count);
	}

	#region Unique Key Constraint Tests

	[Fact]
	public async Task PostShiftTemplate_DuplicateTemplateName_UpdatesExisting()
	{
		// Arrange - TemplateName is unique per user, so posting same name should update
		await using var context = _fixture.CreateContext();
		var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

		var originalTemplate = new ShiftTemplateDTO
		{
			TemplateName = "Unique Template Test",
			Workplace = "Original Workplace",
			PayRate = 15.00m,
			StartTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		var result1 = await controller.PostShiftTemplate(originalTemplate);
		Assert.IsType<CreatedAtActionResult>(result1.Result);

		// Act - Post same template name with different data
		var updatedTemplate = new ShiftTemplateDTO
		{
			TemplateName = "Unique Template Test", // Same name
			Workplace = "Updated Workplace",
			PayRate = 25.00m,
			StartTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 18, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		var result2 = await controller.PostShiftTemplate(updatedTemplate);

		// Assert - Should return OK (update) not Created
		var okResult = Assert.IsType<OkObjectResult>(result2.Result);
		var returnedTemplate = Assert.IsType<ShiftTemplateDTO>(okResult.Value);
		Assert.Equal("Unique Template Test", returnedTemplate.TemplateName);
		Assert.Equal("Updated Workplace", returnedTemplate.Workplace);
		Assert.Equal(25.00m, returnedTemplate.PayRate);
	}

	[Fact]
	public async Task PostShiftTemplate_SameTemplateNameDifferentUsers_BothSucceed()
	{
		// Arrange - Unique keys are scoped to partition (userId), so different users can have same template name
		var userId1 = $"template-unique-user1-{Guid.NewGuid():N}";
		var userId2 = $"template-unique-user2-{Guid.NewGuid():N}";

		await using var context1 = _fixture.CreateContext();
		await using var context2 = _fixture.CreateContext();
		var controller1 = ControllerTestHelper.CreateShiftTemplatesController(context1, userId1);
		var controller2 = ControllerTestHelper.CreateShiftTemplatesController(context2, userId2);

		var templateDto = new ShiftTemplateDTO
		{
			TemplateName = "Shared Template Name",
			Workplace = "Some Workplace",
			PayRate = 20.00m,
			StartTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		};

		// Act
		var result1 = await controller1.PostShiftTemplate(templateDto);
		var result2 = await controller2.PostShiftTemplate(templateDto);

		// Assert - Both should succeed since they're in different partitions (users)
		var createdResult1 = Assert.IsType<CreatedAtActionResult>(result1.Result);
		var createdResult2 = Assert.IsType<CreatedAtActionResult>(result2.Result);
		var createdTemplate1 = Assert.IsType<ShiftTemplateDTO>(createdResult1.Value);
		var createdTemplate2 = Assert.IsType<ShiftTemplateDTO>(createdResult2.Value);
		Assert.NotNull(createdTemplate1.Id);
		Assert.NotNull(createdTemplate2.Id);

		// Verify both templates exist separately
		var getResult1 = await controller1.GetShiftTemplate(createdTemplate1.Id.Value);
		var getResult2 = await controller2.GetShiftTemplate(createdTemplate2.Id.Value);

		Assert.IsType<OkObjectResult>(getResult1.Result);
		Assert.IsType<OkObjectResult>(getResult2.Result);
	}

	[Fact]
	public async Task GetShiftTemplates_WithUniqueConstraint_ReturnsOnlyUserTemplates()
	{
		// Arrange - Create templates for two users with same names
		var userId1 = $"isolation-user1-{Guid.NewGuid():N}";
		var userId2 = $"isolation-user2-{Guid.NewGuid():N}";

		await using var context1 = _fixture.CreateContext();
		await using var context2 = _fixture.CreateContext();
		var controller1 = ControllerTestHelper.CreateShiftTemplatesController(context1, userId1);
		var controller2 = ControllerTestHelper.CreateShiftTemplatesController(context2, userId2);

		// Create 2 templates for user1
		await controller1.PostShiftTemplate(new ShiftTemplateDTO
		{
			TemplateName = "Template A",
			Workplace = "Workplace",
			PayRate = 15.00m,
			StartTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 17, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		});
		await controller1.PostShiftTemplate(new ShiftTemplateDTO
		{
			TemplateName = "Template B",
			Workplace = "Workplace",
			PayRate = 18.00m,
			StartTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 18, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		});

		// Create 1 template for user2 with same name as user1's template
		await controller2.PostShiftTemplate(new ShiftTemplateDTO
		{
			TemplateName = "Template A",
			Workplace = "Different Workplace",
			PayRate = 20.00m,
			StartTime = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2024, 1, 1, 16, 0, 0, DateTimeKind.Utc),
			UnpaidBreaks = []
		});

		// Act
		var result1 = await controller1.GetShiftTemplates();
		var result2 = await controller2.GetShiftTemplates();

		// Assert - Each user should only see their own templates
		var okResult1 = Assert.IsType<OkObjectResult>(result1.Result);
		var templates1 = Assert.IsAssignableFrom<IEnumerable<ShiftTemplateDTO>>(okResult1.Value).ToList();
		Assert.Equal(2, templates1.Count);

		var okResult2 = Assert.IsType<OkObjectResult>(result2.Result);
		var templates2 = Assert.IsAssignableFrom<IEnumerable<ShiftTemplateDTO>>(okResult2.Value).ToList();
		Assert.Single(templates2);
		Assert.Equal("Different Workplace", templates2[0].Workplace);
	}

	#endregion
}
