using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// موارد الدورات - Course Resources Controller (Redirects to LessonResources)
/// </summary>
public class ResourcesController : InstructorBaseController
{
    private readonly ILogger<ResourcesController> _logger;

    public ResourcesController(ILogger<ResourcesController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// قائمة جميع الموارد - All resources list
    /// Redirects to LessonResources/Index
    /// </summary>
    public IActionResult Index(int? courseId, int? lessonId, int page = 1)
    {
        _logger.LogInformation("Redirecting Resources/Index to LessonResources/Index");
        return RedirectToAction("Index", "LessonResources", new { courseId, lessonId, page });
    }
}

