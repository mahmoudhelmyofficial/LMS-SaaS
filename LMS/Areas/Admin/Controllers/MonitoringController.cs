using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// Admin Monitoring Dashboard Controller
/// Provides a beautiful UI for system monitoring
/// </summary>
[Area("Admin")]
[Authorize(Roles = "Admin")]
public class MonitoringController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}

