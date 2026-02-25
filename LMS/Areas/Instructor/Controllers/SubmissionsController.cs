using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// تسليمات التكليفات - Submissions Controller (Redirects to AssignmentSubmissions)
/// </summary>
public class SubmissionsController : InstructorBaseController
{
    private readonly ILogger<SubmissionsController> _logger;

    public SubmissionsController(ILogger<SubmissionsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// قائمة جميع التسليمات - All submissions list
    /// Redirects to AssignmentSubmissions/Index
    /// </summary>
    public IActionResult Index(int? assignmentId, int? courseId, string? status)
    {
        _logger.LogInformation("Redirecting Submissions/Index to AssignmentSubmissions/Index");
        return RedirectToAction("Index", "AssignmentSubmissions", new { assignmentId, courseId, status });
    }
}

