using Microsoft.AspNetCore.Mvc;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// مركز المساعدة للطالب - Student Help Center Controller
/// Comprehensive knowledge base and documentation for students
/// </summary>
public class HelpController : StudentBaseController
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
        _logger.LogInformation("Student accessed Help Center");
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
    /// دليل دوراتي - My Courses Guide
    /// </summary>
    public IActionResult MyCourses()
    {
        return View();
    }

    /// <summary>
    /// دليل التعلم - Learning Guide
    /// </summary>
    public IActionResult Learning()
    {
        return View();
    }

    /// <summary>
    /// دليل الاختبارات والواجبات - Assessments Guide
    /// </summary>
    public IActionResult Assessments()
    {
        return View();
    }

    /// <summary>
    /// دليل الإنجازات والشهادات - Achievements Guide
    /// </summary>
    public IActionResult Achievements()
    {
        return View();
    }

    /// <summary>
    /// دليل المشتريات والفواتير - Purchases Guide
    /// </summary>
    public IActionResult Purchases()
    {
        return View();
    }

    /// <summary>
    /// دليل التواصل - Communication Guide
    /// </summary>
    public IActionResult Communication()
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

