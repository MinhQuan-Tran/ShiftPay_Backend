using Microsoft.AspNetCore.Mvc;
using ShiftPay_Backend.Models;

namespace ShiftPay_Backend.Tests;

[Collection("CosmosDb")]
public class ShiftTemplatesControllerTests : IAsyncLifetime
{
    private readonly CosmosDbTestFixture _fixture;
    private readonly string _testUserId = $"templates-test-{Guid.NewGuid():N}";

    public ShiftTemplatesControllerTests(CosmosDbTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.CleanupUserDataAsync(_testUserId);
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
        await controller.PostShiftTemplate(templateDto);

        // Act
        var result = await controller.GetShiftTemplate("Day Shift");

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
        var result = await controller.GetShiftTemplate("NonExistent Template");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetShiftTemplate_WithEncodedTemplateName_ReturnsTemplate()
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
        await controller.PostShiftTemplate(templateDto);

        // Act - URL encoded template name
        var result = await controller.GetShiftTemplate("Weekend%20Special%20%26%20Holiday");

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
        await controller.PostShiftTemplate(templateDto);

        // Act
        var result = await controller.DeleteShiftTemplate("Template To Delete");

        // Assert
        Assert.IsType<NoContentResult>(result);

        // Verify deleted
        var getResult = await controller.GetShiftTemplate("Template To Delete");
        Assert.IsType<NotFoundObjectResult>(getResult.Result);
    }

    [Fact]
    public async Task DeleteShiftTemplate_WhenTemplateDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var controller = ControllerTestHelper.CreateShiftTemplatesController(context, _testUserId);

        // Act
        var result = await controller.DeleteShiftTemplate("NonExistent Template");

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
        var result = await controller.GetShiftTemplate("   ");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
