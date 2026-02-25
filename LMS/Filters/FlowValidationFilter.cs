using LMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LMS.Filters;

/// <summary>
/// Flow Validation Action Filter
/// Automatically validates user access and provides smart redirects
/// </summary>
public class FlowValidationFilter : IAsyncActionFilter
{
    private readonly IFlowValidationService _flowValidation;
    private readonly ILogger<FlowValidationFilter> _logger;

    public FlowValidationFilter(
        IFlowValidationService flowValidation,
        ILogger<FlowValidationFilter> logger)
    {
        _flowValidation = flowValidation;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var userId = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        // Skip for anonymous users or non-validated actions
        if (string.IsNullOrEmpty(userId))
        {
            await next();
            return;
        }

        // Check for flow validation attributes
        var validateLesson = context.ActionDescriptor.EndpointMetadata
            .OfType<ValidateLessonAccessAttribute>()
            .FirstOrDefault();

        var validateCourse = context.ActionDescriptor.EndpointMetadata
            .OfType<ValidateCourseAccessAttribute>()
            .FirstOrDefault();

        var validateQuiz = context.ActionDescriptor.EndpointMetadata
            .OfType<ValidateQuizAccessAttribute>()
            .FirstOrDefault();

        // Validate lesson access
        if (validateLesson != null)
        {
            var lessonIdParam = context.ActionArguments
                .FirstOrDefault(p => p.Key.Equals("lessonId", StringComparison.OrdinalIgnoreCase) ||
                                    p.Key.Equals("id", StringComparison.OrdinalIgnoreCase));

            if (lessonIdParam.Value is int lessonId)
            {
                var result = await _flowValidation.ValidateLessonAccessAsync(userId, lessonId);
                if (!result.IsValid)
                {
                    HandleValidationFailure(context, result, validateLesson.ShowMessage);
                    return;
                }
            }
        }

        // Validate course access
        if (validateCourse != null)
        {
            var courseIdParam = context.ActionArguments
                .FirstOrDefault(p => p.Key.Equals("courseId", StringComparison.OrdinalIgnoreCase) ||
                                    p.Key.Equals("id", StringComparison.OrdinalIgnoreCase));

            if (courseIdParam.Value is int courseId)
            {
                var result = await _flowValidation.ValidateCourseAccessAsync(userId, courseId);
                if (!result.IsValid)
                {
                    HandleValidationFailure(context, result, validateCourse.ShowMessage);
                    return;
                }
            }
        }

        // Validate quiz access
        if (validateQuiz != null)
        {
            var quizIdParam = context.ActionArguments
                .FirstOrDefault(p => p.Key.Equals("quizId", StringComparison.OrdinalIgnoreCase) ||
                                    p.Key.Equals("id", StringComparison.OrdinalIgnoreCase));

            if (quizIdParam.Value is int quizId)
            {
                var result = await _flowValidation.ValidateQuizAccessAsync(userId, quizId);
                if (!result.IsValid)
                {
                    HandleValidationFailure(context, result, validateQuiz.ShowMessage);
                    return;
                }
            }
        }

        await next();
    }

    private void HandleValidationFailure(ActionExecutingContext context, FlowValidationResult result, bool showMessage)
    {
        _logger.LogWarning(
            "Flow validation failed: {ErrorCode} - {ErrorMessage} - Redirecting to {RedirectUrl}",
            result.ErrorCode, result.ErrorMessage, result.RedirectUrl);

        if (showMessage && context.Controller is Controller controller)
        {
            controller.TempData["ErrorMessage"] = result.ErrorMessage;
        }

        context.Result = new RedirectResult(result.RedirectUrl ?? "/");
    }
}

/// <summary>
/// Attribute to validate lesson access
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class ValidateLessonAccessAttribute : Attribute
{
    public bool ShowMessage { get; set; } = true;
}

/// <summary>
/// Attribute to validate course access
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class ValidateCourseAccessAttribute : Attribute
{
    public bool ShowMessage { get; set; } = true;
}

/// <summary>
/// Attribute to validate quiz access
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class ValidateQuizAccessAttribute : Attribute
{
    public bool ShowMessage { get; set; } = true;
}

