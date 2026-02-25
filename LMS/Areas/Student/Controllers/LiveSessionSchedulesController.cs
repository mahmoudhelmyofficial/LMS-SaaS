using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LMS.Areas.Student.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Payments;
using LMS.Domain.Enums;
using LMS.Services.Interfaces;
using System.Security.Claims;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// جداول الحصص المباشرة - Live Session Schedules Controller
/// </summary>
public class LiveSessionSchedulesController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILiveSessionService _liveSessionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<LiveSessionSchedulesController> _logger;

    public LiveSessionSchedulesController(
        ApplicationDbContext context,
        ILiveSessionService liveSessionService,
        ICurrentUserService currentUserService,
        ILogger<LiveSessionSchedulesController> logger)
    {
        _context = context;
        _liveSessionService = liveSessionService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private string GetUserId() => _currentUserService.UserId ?? string.Empty;

    /// <summary>
    /// قائمة جداول الحصص المتاحة - Available Schedules List
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var userId = GetUserId();
        var schedules = await _context.LiveSessionSchedules
            .Include(s => s.Instructor)
            .Include(s => s.Course)
            .Include(s => s.Enrollments)
            .Where(s => s.IsPublished && !s.IsDeleted &&
                       (s.Status == LiveScheduleStatus.Published || s.Status == LiveScheduleStatus.Active))
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var viewModels = schedules.Select(s => new AvailableScheduleViewModel
        {
            Id = s.Id,
            Title = s.TitleAr ?? s.Title,
            TitleAr = s.TitleAr,
            Description = s.Description,
            Price = s.Price,
            OriginalPrice = s.OriginalPrice,
            Currency = s.Currency,
            StartDate = s.StartDate,
            EndDate = s.EndDate,
            TotalSessions = s.TotalSessions,
            EnrolledCount = s.EnrolledCount,
            MaxStudents = s.MaxStudents,
            InstructorName = s.Instructor?.FullName ?? s.Instructor?.UserName ?? "مدرس",
            ThumbnailUrl = s.ThumbnailUrl,
            Status = s.Status,
            IsEnrolled = s.Enrollments.Any(e => e.StudentId == userId && e.Status == ScheduleEnrollmentStatus.Active)
        }).ToList();

        return View(viewModels);
    }

    /// <summary>
    /// تفاصيل جدول الحصص - Schedule Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = GetUserId();
        var schedule = await _context.LiveSessionSchedules
            .Include(s => s.Instructor)
            .Include(s => s.Sessions.OrderBy(ss => ss.ScheduleOrder))
            .Include(s => s.Enrollments)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

        if (schedule == null)
        {
            SetErrorMessage("جدول الحصص غير موجود");
            return RedirectToAction(nameof(Index));
        }

        var vm = new ScheduleDetailsViewModel
        {
            Id = schedule.Id,
            Title = schedule.TitleAr ?? schedule.Title,
            TitleAr = schedule.TitleAr,
            Description = schedule.Description,
            Price = schedule.Price,
            OriginalPrice = schedule.OriginalPrice,
            Currency = schedule.Currency,
            StartDate = schedule.StartDate,
            EndDate = schedule.EndDate,
            TotalSessions = schedule.TotalSessions,
            EnrolledCount = schedule.EnrolledCount,
            MaxStudents = schedule.MaxStudents,
            InstructorName = schedule.Instructor?.FullName ?? schedule.Instructor?.UserName ?? "مدرس",
            ThumbnailUrl = schedule.ThumbnailUrl,
            IsEnrolled = schedule.Enrollments.Any(e => e.StudentId == userId && e.Status == ScheduleEnrollmentStatus.Active),
            Sessions = schedule.Sessions.Select(s => new ScheduleSessionPreviewViewModel
            {
                Id = s.Id,
                Title = s.Title,
                Subject = s.Subject,
                ScheduledStartTime = s.ScheduledStartTime,
                ScheduledEndTime = s.ScheduledEndTime,
                DurationMinutes = s.DurationMinutes,
                Platform = s.Platform,
                Status = s.Status,
                ScheduleOrder = s.ScheduleOrder
            }).ToList()
        };

        return View(vm);
    }

    /// <summary>
    /// صفحة شراء جدول الحصص - Purchase Schedule
    /// </summary>
    public async Task<IActionResult> Purchase(int id)
    {
        var userId = GetUserId();

        // Check if already enrolled
        var isEnrolled = await _liveSessionService.HasStudentPurchasedScheduleAsync(id, userId);
        if (isEnrolled)
        {
            SetInfoMessage("أنت مشترك بالفعل في هذا الجدول");
            return RedirectToAction(nameof(Details), new { id });
        }

        var schedule = await _context.LiveSessionSchedules
            .Include(s => s.Instructor)
            .Include(s => s.Sessions.OrderBy(ss => ss.ScheduleOrder))
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted && s.IsPublished);

        if (schedule == null)
        {
            SetErrorMessage("جدول الحصص غير متاح");
            return RedirectToAction(nameof(Index));
        }

        // Check max students
        if (schedule.MaxStudents.HasValue && schedule.EnrolledCount >= schedule.MaxStudents.Value)
        {
            SetWarningMessage("عذراً، تم اكتمال العدد المسموح به للمشتركين");
            return RedirectToAction(nameof(Details), new { id });
        }

        var vm = new ScheduleCheckoutViewModel
        {
            ScheduleId = schedule.Id,
            ScheduleTitle = schedule.TitleAr ?? schedule.Title,
            InstructorName = schedule.Instructor?.FullName ?? "مدرس",
            Price = schedule.Price,
            OriginalPrice = schedule.OriginalPrice,
            Currency = schedule.Currency,
            TotalSessions = schedule.TotalSessions,
            StartDate = schedule.StartDate,
            EndDate = schedule.EndDate,
            Sessions = schedule.Sessions.Select(s => new ScheduleSessionPreviewViewModel
            {
                Id = s.Id,
                Title = s.Title,
                Subject = s.Subject,
                ScheduledStartTime = s.ScheduledStartTime,
                ScheduledEndTime = s.ScheduledEndTime,
                DurationMinutes = s.DurationMinutes,
                Platform = s.Platform,
                Status = s.Status,
                ScheduleOrder = s.ScheduleOrder
            }).ToList()
        };

        // Load payment gateways
        var gateways = await _context.PaymentGatewaySettings
            .Where(g => g.IsEnabled && !g.IsDeleted)
            .OrderBy(g => g.DisplayOrder)
            .ToListAsync();
        ViewBag.PaymentGateways = gateways;

        return View(vm);
    }

    /// <summary>
    /// معالجة شراء جدول الحصص - Process Schedule Purchase
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessPurchase(int scheduleId, string paymentMethod)
    {
        var userId = GetUserId();

        try
        {
            var schedule = await _context.LiveSessionSchedules
                .FirstOrDefaultAsync(s => s.Id == scheduleId && !s.IsDeleted && s.IsPublished);

            if (schedule == null)
            {
                SetErrorMessage("جدول الحصص غير متاح");
                return RedirectToAction(nameof(Index));
            }

            // If free schedule
            if (schedule.Price == 0)
            {
                var enrollment = await _liveSessionService.CreateScheduleEnrollmentAsync(
                    scheduleId, userId, null, 0);
                await _liveSessionService.ActivateScheduleEnrollmentAsync(enrollment.Id);
                SetSuccessMessage("تم الاشتراك بنجاح في جدول الحصص!");
                return RedirectToAction(nameof(Details), new { id = scheduleId });
            }

            // Create payment record
            var payment = new Payment
            {
                TransactionId = $"SCH_{scheduleId}_{userId}_{DateTime.UtcNow.Ticks}",
                StudentId = userId,
                OriginalAmount = schedule.Price,
                TotalAmount = schedule.Price,
                Currency = schedule.Currency,
                Status = PaymentStatus.Pending,
                Provider = Enum.TryParse<PaymentProvider>(paymentMethod, out var provider) ? provider : PaymentProvider.Paymob,
                PaymentMethod = paymentMethod,
                Metadata = $"{{\"type\":\"schedule\",\"scheduleId\":{scheduleId}}}"
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            // Create pending enrollment
            var pendingEnrollment = await _liveSessionService.CreateScheduleEnrollmentAsync(
                scheduleId, userId, payment.Id, schedule.Price);

            // For now, simulate successful payment (in production, integrate with gateway)
            payment.Status = PaymentStatus.Completed;
            payment.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _liveSessionService.ActivateScheduleEnrollmentAsync(pendingEnrollment.Id);

            // Update schedule stats
            schedule.EnrolledCount++;
            schedule.TotalRevenue += schedule.Price;
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم الاشتراك بنجاح! يمكنك الآن حضور جميع الحصص في الجدول");
            return RedirectToAction(nameof(Details), new { id = scheduleId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing schedule purchase {ScheduleId} for user {UserId}", scheduleId, userId);
            SetErrorMessage("حدث خطأ أثناء معالجة الدفع. يرجى المحاولة مرة أخرى");
            return RedirectToAction(nameof(Purchase), new { id = scheduleId });
        }
    }

    /// <summary>
    /// جداولي - My Schedules
    /// </summary>
    public async Task<IActionResult> MySchedules(string? status = null)
    {
        var userId = GetUserId();
        var query = _context.LiveSessionScheduleEnrollments
            .Include(e => e.Schedule)
                .ThenInclude(s => s.Instructor)
            .Include(e => e.Schedule)
                .ThenInclude(s => s.Sessions)
            .Where(e => e.StudentId == userId)
            .AsQueryable();

        // Filter by status (default: show active only)
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ScheduleEnrollmentStatus>(status, out var enrollmentStatus))
        {
            query = query.Where(e => e.Status == enrollmentStatus);
        }
        else
        {
            query = query.Where(e => e.Status == ScheduleEnrollmentStatus.Active);
        }

        var enrollments = await query
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();

        var viewModels = enrollments.Select(e => new AvailableScheduleViewModel
        {
            Id = e.Schedule.Id,
            Title = e.Schedule.TitleAr ?? e.Schedule.Title,
            Description = e.Schedule.Description,
            Price = e.PaidAmount,
            Currency = e.Currency,
            StartDate = e.Schedule.StartDate,
            EndDate = e.Schedule.EndDate,
            TotalSessions = e.Schedule.TotalSessions,
            InstructorName = e.Schedule.Instructor?.FullName ?? "مدرس",
            ThumbnailUrl = e.Schedule.ThumbnailUrl,
            Status = e.Schedule.Status,
            IsEnrolled = true
        }).ToList();

        ViewBag.CurrentStatus = status;
        ViewBag.TotalActive = await _context.LiveSessionScheduleEnrollments
            .CountAsync(e => e.StudentId == userId && e.Status == ScheduleEnrollmentStatus.Active);
        ViewBag.TotalCancelled = await _context.LiveSessionScheduleEnrollments
            .CountAsync(e => e.StudentId == userId && e.Status == ScheduleEnrollmentStatus.Cancelled);
        ViewBag.TotalExpired = await _context.LiveSessionScheduleEnrollments
            .CountAsync(e => e.StudentId == userId && e.Status == ScheduleEnrollmentStatus.Expired);

        return View(viewModels);
    }
}
