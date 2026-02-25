using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// الإعدادات - Settings Controller (Redirects to Profile/Settings)
/// </summary>
public class SettingsController : InstructorBaseController
{
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(ILogger<SettingsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// صفحة الإعدادات - Settings page
    /// Redirects to Profile/Settings
    /// </summary>
    public IActionResult Index()
    {
        _logger.LogInformation("Redirecting Settings/Index to Profile/Settings");
        return RedirectToAction("Settings", "Profile");
    }
}

