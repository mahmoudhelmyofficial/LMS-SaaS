using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace LMS.Filters;

/// <summary>
/// فلتر التحقق من صحة بيانات المدرس - Instructor Validation Filter
/// Validates instructor context before action execution
/// Enterprise-level validation with proper error handling
/// </summary>
public class InstructorValidationFilter : IAsyncActionFilter
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<InstructorValidationFilter> _logger;

    public InstructorValidationFilter(
        ICurrentUserService currentUserService,
        ILogger<InstructorValidationFilter> logger)
    {
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Skip validation for non-controller actions
        if (context.Controller is not Controller controller)
        {
            await next();
            return;
        }

        // Check if user is authenticated
        if (!_currentUserService.IsAuthenticated)
        {
            _logger.LogDebug("User not authenticated, skipping validation");
            await next();
            return;
        }

        var userId = _currentUserService.UserId;
        var userName = _currentUserService.UserName;

        // Validate UserId is present
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning(
                "Authenticated user without UserId claim. UserName: {UserName}, Email: {Email}",
                userName,
                _currentUserService.Email);

            // Set error message in TempData
            controller.TempData["ErrorMessage"] = "خطأ في بيانات المستخدم. يرجى تسجيل الدخول مرة أخرى";

            // Redirect to login
            context.Result = new RedirectToActionResult("Login", "Account", new { area = "" });
            return;
        }

        // Validate user has instructor role
        if (!_currentUserService.IsInstructor && !_currentUserService.IsAdmin)
        {
            _logger.LogWarning(
                "User {UserName} (UserId: {UserId}) attempted to access instructor area without proper role",
                userName,
                userId);

            controller.TempData["ErrorMessage"] = "ليس لديك صلاحية للوصول إلى هذه الصفحة";
            context.Result = new RedirectToActionResult("AccessDenied", "Account", new { area = "" });
            return;
        }

        // Log successful validation for debugging
        _logger.LogDebug(
            "Instructor validation passed for UserId: {UserId}, UserName: {UserName}",
            userId,
            userName);

        await next();
    }
}

