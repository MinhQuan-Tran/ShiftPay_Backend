using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ShiftPay_Backend.Controllers;
using ShiftPay_Backend.Data;
using System.Security.Claims;

namespace ShiftPay_Backend.Tests;

/// <summary>
/// Helper class for creating controller instances with test user context.
/// </summary>
public static class ControllerTestHelper
{
    public const string TestUserId = "test-user-id";
    public const string TestUserName = "Test User";

    /// <summary>
    /// Creates a ClaimsPrincipal for a test user.
    /// </summary>
    public static ClaimsPrincipal CreateTestUser(string userId = TestUserId, string userName = TestUserName)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userName)
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Creates a ShiftsController with test user context.
    /// </summary>
    public static ShiftsController CreateShiftsController(ShiftPay_BackendContext context, string userId = TestUserId)
    {
        var logger = new LoggerFactory().CreateLogger<ShiftsController>();
        var controller = new ShiftsController(logger, context);
        SetupControllerContext(controller, userId);
        return controller;
    }

    /// <summary>
    /// Creates a WorkInfosController with test user context.
    /// </summary>
    public static WorkInfosController CreateWorkInfosController(ShiftPay_BackendContext context, string userId = TestUserId)
    {
        var logger = new LoggerFactory().CreateLogger<WorkInfosController>();
        var controller = new WorkInfosController(logger, context);
        SetupControllerContext(controller, userId);
        return controller;
    }

    /// <summary>
    /// Creates a ShiftTemplatesController with test user context.
    /// </summary>
    public static ShiftTemplatesController CreateShiftTemplatesController(ShiftPay_BackendContext context, string userId = TestUserId)
    {
        var logger = new LoggerFactory().CreateLogger<ShiftTemplatesController>();
        var controller = new ShiftTemplatesController(logger, context);
        SetupControllerContext(controller, userId);
        return controller;
    }

    private static void SetupControllerContext(ControllerBase controller, string userId)
    {
        var httpContext = new DefaultHttpContext
        {
            User = CreateTestUser(userId)
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }
}
