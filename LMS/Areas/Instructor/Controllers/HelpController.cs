using Microsoft.AspNetCore.Mvc;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// مركز المساعدة للمدرس - Instructor Help Center Controller
/// Comprehensive knowledge base and documentation for instructors
/// </summary>
public class HelpController : InstructorBaseController
{
    private readonly ILogger<HelpController> _logger;

    public HelpController(ILogger<HelpController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// الصفحة الرئيسية لمركز المساعدة - Help Center Home
    /// </summary>
    public IActionResult Index()
    {
        _logger.LogInformation("Instructor accessed Help Center");
        return View();
    }

    /// <summary>
    /// دليل البدء السريع - Getting Started Guide
    /// </summary>
    public IActionResult GettingStarted()
    {
        return View();
    }

    /// <summary>
    /// دليل إنشاء الدورات - Course Creation Guide
    /// </summary>
    public IActionResult CourseCreation()
    {
        return View();
    }

    /// <summary>
    /// دليل إدارة المحتوى - Content Management Guide
    /// </summary>
    public IActionResult ContentManagement()
    {
        return View();
    }

    /// <summary>
    /// دليل الجلسات المباشرة والجدولة - Live Sessions & Scheduling Guide
    /// </summary>
    public IActionResult LiveSessions()
    {
        return View();
    }

    /// <summary>
    /// دليل الأرباح والتسويق - Earnings & Marketing Guide
    /// </summary>
    public IActionResult EarningsMarketing()
    {
        return View();
    }

    /// <summary>
    /// دليل إدارة الطلاب - Student Management Guide
    /// </summary>
    public IActionResult StudentManagement()
    {
        return View();
    }

    /// <summary>
    /// دليل التحليلات - Analytics Guide
    /// </summary>
    public IActionResult Analytics()
    {
        return View();
    }

    /// <summary>
    /// الأسئلة الشائعة - Frequently Asked Questions
    /// </summary>
    public IActionResult FAQ()
    {
        return View();
    }

    /// <summary>
    /// بحث في المساعدة - Search Help
    /// </summary>
    [HttpGet]
    public IActionResult Search(string query)
    {
        ViewBag.SearchQuery = query;
        return View();
    }
}

