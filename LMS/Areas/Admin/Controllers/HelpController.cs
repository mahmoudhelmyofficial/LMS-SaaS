using Microsoft.AspNetCore.Mvc;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// مركز المساعدة للإدارة - Admin Help Center Controller
/// Comprehensive knowledge base and documentation for administrators
/// </summary>
public class HelpController : AdminBaseController
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
        _logger.LogInformation("Admin accessed Help Center");
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
    /// دليل إدارة المستخدمين - User Management Guide
    /// </summary>
    public IActionResult UserManagement()
    {
        return View();
    }

    /// <summary>
    /// دليل إدارة الدورات - Course Management Guide
    /// </summary>
    public IActionResult CourseManagement()
    {
        return View();
    }

    /// <summary>
    /// دليل الإدارة المالية - Financial Management Guide
    /// </summary>
    public IActionResult FinancialManagement()
    {
        return View();
    }

    /// <summary>
    /// دليل أدوات التسويق - Marketing Tools Guide
    /// </summary>
    public IActionResult MarketingTools()
    {
        return View();
    }

    /// <summary>
    /// دليل التقارير والتحليلات - Reports & Analytics Guide
    /// </summary>
    public IActionResult ReportsAnalytics()
    {
        return View();
    }

    /// <summary>
    /// دليل الاتصالات - Communications Guide
    /// </summary>
    public IActionResult Communications()
    {
        return View();
    }

    /// <summary>
    /// دليل إعدادات المنصة - Platform Settings Guide
    /// </summary>
    public IActionResult PlatformSettings()
    {
        return View();
    }

    /// <summary>
    /// دليل الأمان والمراقبة - Security & Monitoring Guide
    /// </summary>
    public IActionResult SecurityMonitoring()
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

