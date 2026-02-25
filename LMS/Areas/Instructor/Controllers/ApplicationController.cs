using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// طلبات المدرس - Instructor Application Controller
/// </summary>
public class ApplicationController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPlatformSettingsService _platformSettings;
    private readonly ILogger<ApplicationController> _logger;

    public ApplicationController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IPlatformSettingsService platformSettings,
        ILogger<ApplicationController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _platformSettings = platformSettings;
        _logger = logger;
    }

    /// <summary>
    /// عرض الطلب - View my application
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = _currentUserService.UserId;
        
        var application = await _context.InstructorApplications
            .Include(a => a.Documents)
            .Include(a => a.ReviewedBy)
            .FirstOrDefaultAsync(a => a.UserId == userId);

        if (application == null)
        {
            SetInfoMessage("لم يتم العثور على طلب انضمام");
            return View("NoApplication");
        }

        // Get support email from platform settings
        var supportEmail = await _platformSettings.GetSettingAsync("SupportEmail", "support@lms.com");
        ViewBag.SupportEmail = supportEmail;

        return View(application);
    }

    /// <summary>
    /// تفاصيل الطلب - Application details
    /// </summary>
    public async Task<IActionResult> Details()
    {
        var userId = _currentUserService.UserId;
        
        var application = await _context.InstructorApplications
            .Include(a => a.Documents)
            .Include(a => a.ReviewedBy)
            .FirstOrDefaultAsync(a => a.UserId == userId);

        if (application == null)
            return NotFound();

        var supportEmail = await _platformSettings.GetSettingAsync("SupportEmail", "support@lms.com");
        ViewBag.SupportEmail = supportEmail;

        return View(application);
    }

    /// <summary>
    /// عرض المستندات - View documents
    /// </summary>
    public async Task<IActionResult> Documents()
    {
        var userId = _currentUserService.UserId;
        
        var application = await _context.InstructorApplications
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.UserId == userId);

        if (application == null)
            return NotFound();

        var documents = application.Documents.OrderBy(d => d.DocumentType).ToList();
        ViewBag.ApplicationId = application.Id;
        ViewBag.ApplicationStatus = application.Status;
        
        var supportEmail = await _platformSettings.GetSettingAsync("SupportEmail", "support@lms.com");
        ViewBag.SupportEmail = supportEmail;

        return View(documents);
    }

    /// <summary>
    /// سجل التعديلات - Application history
    /// </summary>
    public async Task<IActionResult> History()
    {
        var userId = _currentUserService.UserId;
        
        var applications = await _context.InstructorApplications
            .Include(a => a.ReviewedBy)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return View(applications);
    }

    /// <summary>
    /// حالة الطلب - Application status
    /// </summary>
    public async Task<IActionResult> Status()
    {
        var userId = _currentUserService.UserId;
        
        var application = await _context.InstructorApplications
            .Include(a => a.ReviewedBy)
            .FirstOrDefaultAsync(a => a.UserId == userId);

        if (application == null)
        {
            return View("NoApplication");
        }

        // Get instructor profile if approved
        var instructorProfile = await _context.InstructorProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        ViewBag.InstructorProfile = instructorProfile;
        
        var supportEmail = await _platformSettings.GetSettingAsync("SupportEmail", "support@lms.com");
        ViewBag.SupportEmail = supportEmail;

        return View(application);
    }

    /// <summary>
    /// تحميل مستند - Download document
    /// </summary>
    public async Task<IActionResult> DownloadDocument(int id)
    {
        var userId = _currentUserService.UserId;
        
        var document = await _context.InstructorDocuments
            .Include(d => d.Application)
            .FirstOrDefaultAsync(d => d.Id == id && d.Application!.UserId == userId);

        if (document == null)
            return NotFound();

        // In a real scenario, you would retrieve the file from storage
        // For now, we'll redirect to the URL
        if (!string.IsNullOrEmpty(document.FileUrl))
        {
            return Redirect(document.FileUrl);
        }

        return NotFound();
    }
}

