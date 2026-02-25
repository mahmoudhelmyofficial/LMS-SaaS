using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.LiveSessions;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// البث المباشر - Live Classes Controller
/// </summary>
public class LiveClassesController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILiveSessionService _liveSessionService;
    private readonly ILogger<LiveClassesController> _logger;

    public LiveClassesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILiveSessionService liveSessionService,
        ILogger<LiveClassesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _liveSessionService = liveSessionService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة البث المباشر - Live classes list
    /// </summary>
    public async Task<IActionResult> Index(int? courseId, LiveClassStatus? status, LiveSessionPricingType? pricingType = null)
    {
        var userId = _currentUserService.UserId;

        var query = _context.LiveClasses
            .Include(lc => lc.Course)
            .Where(lc => lc.InstructorId == userId);

        if (courseId.HasValue)
        {
            query = query.Where(lc => lc.CourseId == courseId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(lc => lc.Status == status.Value);
        }

        if (pricingType.HasValue)
        {
            query = query.Where(lc => lc.PricingType == pricingType.Value);
        }

        var liveClasses = await query
            .OrderByDescending(lc => lc.ScheduledStartTime)
            .ToListAsync();

        ViewBag.Courses = await _context.Courses
            .Where(c => c.InstructorId == userId)
            .Select(c => new { c.Id, c.Title })
            .ToListAsync();

        ViewBag.CourseId = courseId;
        ViewBag.Status = status;
        ViewBag.PricingType = pricingType;

        return View(liveClasses);
    }

    /// <summary>
    /// جدولة بث مباشر - Schedule live class
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var userId = _currentUserService.UserId;

        ViewBag.Courses = new SelectList(
            await _context.Courses.Where(c => c.InstructorId == userId).ToListAsync(),
            "Id", "Title");

        ViewBag.PricingTypes = new SelectList(new[]
        {
            new { Value = (int)LiveSessionPricingType.Free, Text = "مجانية" },
            new { Value = (int)LiveSessionPricingType.Paid, Text = "مدفوعة" },
            new { Value = (int)LiveSessionPricingType.SubscriptionOnly, Text = "اشتراك فقط" }
        }, "Value", "Text");

        return View(new LiveClassCreateViewModel());
    }

    /// <summary>
    /// حفظ البث المباشر - Save live class
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LiveClassCreateViewModel model)
    {
        var userId = _currentUserService.UserId;

        if (!ModelState.IsValid)
        {
            ViewBag.Courses = new SelectList(
                await _context.Courses.Where(c => c.InstructorId == userId).ToListAsync(),
                "Id", "Title");
            return View(model);
        }

        try
        {
            // Verify course ownership
            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == model.CourseId && c.InstructorId == userId);

            if (course == null)
            {
                _logger.LogWarning("Course {CourseId} not found for instructor {InstructorId}", 
                    model.CourseId, userId);
                ModelState.AddModelError(nameof(model.CourseId), "الدورة غير موجودة أو ليس لديك صلاحية عليها");
                ViewBag.Courses = new SelectList(
                    await _context.Courses.Where(c => c.InstructorId == userId).ToListAsync(),
                    "Id", "Title");
                return View(model);
            }

            // Use BusinessRuleHelper for validation
            var (isValid, validationReason) = BusinessRuleHelper.ValidateLiveClass(
                model.ScheduledStartTime,
                model.ScheduledEndTime,
                model.MaxParticipants,
                model.ReminderMinutesBefore);

            if (!isValid)
            {
                _logger.LogWarning("Live class validation failed: {Reason}", validationReason);
                ModelState.AddModelError(string.Empty, validationReason!);
                ViewBag.Courses = new SelectList(
                    await _context.Courses.Where(c => c.InstructorId == userId).ToListAsync(),
                    "Id", "Title");
                return View(model);
            }

            // Additional validation
            if (string.IsNullOrWhiteSpace(model.MeetingUrl) && string.IsNullOrWhiteSpace(model.MeetingId))
            {
                ModelState.AddModelError(string.Empty, "يجب إدخال رابط الاجتماع أو معرف الاجتماع");
                ViewBag.Courses = new SelectList(
                    await _context.Courses.Where(c => c.InstructorId == userId).ToListAsync(),
                    "Id", "Title");
                return View(model);
            }

            // Read pricing fields from form
            var pricingType = LiveSessionPricingType.Free;
            if (Enum.TryParse<LiveSessionPricingType>(Request.Form["PricingType"].FirstOrDefault(), out var parsedPricingType))
                pricingType = parsedPricingType;

            decimal sessionPrice = 0;
            decimal.TryParse(Request.Form["Price"].FirstOrDefault(), out sessionPrice);

            var subject = Request.Form["Subject"].FirstOrDefault();
            var priceCurrency = Request.Form["PriceCurrency"].FirstOrDefault() ?? "EGP";

            LiveClass? liveClass = null;
            await _context.ExecuteInTransactionAsync(async () =>
            {
                liveClass = new LiveClass
                {
                    CourseId = model.CourseId,
                    LessonId = model.LessonId,
                    Title = model.Title,
                    Description = model.Description,
                    Agenda = model.Agenda,
                    ScheduledStartTime = model.ScheduledStartTime,
                    ScheduledEndTime = model.ScheduledEndTime,
                    DurationMinutes = model.DurationMinutes,
                    Platform = model.Platform,
                    MeetingUrl = model.MeetingUrl,
                    MeetingId = model.MeetingId,
                    Password = model.Password,
                    MaxParticipants = model.MaxParticipants,
                    AllowReplay = model.AllowReplay,
                    SendReminder = model.SendReminder,
                    ReminderMinutesBefore = model.ReminderMinutesBefore,
                    IsFreeForAll = model.IsFreeForAll,
                    InstructorId = userId!,
                    Status = LiveClassStatus.Scheduled,
                    PricingType = pricingType,
                    Price = pricingType == LiveSessionPricingType.Paid ? sessionPrice : 0,
                    PriceCurrency = priceCurrency,
                    Subject = subject
                };

                _context.LiveClasses.Add(liveClass);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation(
                "Live class {LiveClassId} '{Title}' scheduled by instructor {InstructorId} for {StartTime}", 
                liveClass!.Id, liveClass.Title, userId, model.ScheduledStartTime);

            SetSuccessMessage("تم جدولة البث المباشر بنجاح");
            return RedirectToAction(nameof(Details), new { id = liveClass.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating live class for instructor {InstructorId}", userId);
            SetErrorMessage("حدث خطأ أثناء جدولة البث المباشر. يرجى المحاولة مرة أخرى");
            ViewBag.Courses = new SelectList(
                await _context.Courses.Where(c => c.InstructorId == userId).ToListAsync(),
                "Id", "Title");
            return View(model);
        }
    }

    /// <summary>
    /// تفاصيل البث المباشر - Live class details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId;

        var liveClass = await _context.LiveClasses
            .Include(lc => lc.Course)
            .FirstOrDefaultAsync(lc => lc.Id == id && lc.InstructorId == userId);

        if (liveClass == null)
            return NotFound();

        var attendance = await _context.LiveClassAttendances
            .Include(a => a.Student)
            .Where(a => a.LiveClassId == id)
            .ToListAsync();

        ViewBag.Attendance = attendance;
        ViewBag.PurchaseCount = await _liveSessionService.GetSessionPurchaseCountAsync(id);
        return View(liveClass);
    }

    /// <summary>
    /// تعديل البث المباشر - Edit live class
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = _currentUserService.UserId;

        var liveClass = await _context.LiveClasses
            .FirstOrDefaultAsync(lc => lc.Id == id && lc.InstructorId == userId);

        if (liveClass == null)
            return NotFound();

        var viewModel = new LiveClassEditViewModel
        {
            Id = liveClass.Id,
            CourseId = liveClass.CourseId,
            LessonId = liveClass.LessonId,
            Title = liveClass.Title,
            Description = liveClass.Description,
            Agenda = liveClass.Agenda,
            ScheduledStartTime = liveClass.ScheduledStartTime,
            ScheduledEndTime = liveClass.ScheduledEndTime,
            DurationMinutes = liveClass.DurationMinutes,
            Platform = liveClass.Platform,
            MeetingUrl = liveClass.MeetingUrl,
            MeetingId = liveClass.MeetingId,
            Password = liveClass.Password,
            MaxParticipants = liveClass.MaxParticipants,
            AllowReplay = liveClass.AllowReplay,
            SendReminder = liveClass.SendReminder,
            ReminderMinutesBefore = liveClass.ReminderMinutesBefore,
            IsFreeForAll = liveClass.IsFreeForAll,
            Status = liveClass.Status
        };

        ViewBag.Courses = new SelectList(
            await _context.Courses.Where(c => c.InstructorId == userId).ToListAsync(),
            "Id", "Title", liveClass.CourseId);

        ViewBag.PricingTypes = new SelectList(new[]
        {
            new { Value = (int)LiveSessionPricingType.Free, Text = "مجانية" },
            new { Value = (int)LiveSessionPricingType.Paid, Text = "مدفوعة" },
            new { Value = (int)LiveSessionPricingType.SubscriptionOnly, Text = "اشتراك فقط" }
        }, "Value", "Text", (int)liveClass.PricingType);

        var purchaseCount = await _liveSessionService.GetSessionPurchaseCountAsync(id);
        ViewBag.CanEditPricing = purchaseCount == 0;
        ViewBag.CurrentPricingType = liveClass.PricingType;
        ViewBag.CurrentPrice = liveClass.Price;
        ViewBag.CurrentSubject = liveClass.Subject;

        return View(viewModel);
    }

    /// <summary>
    /// حفظ تعديلات البث المباشر - Save live class edits
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, LiveClassEditViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var userId = _currentUserService.UserId;

        var liveClass = await _context.LiveClasses
            .FirstOrDefaultAsync(lc => lc.Id == id && lc.InstructorId == userId);

        if (liveClass == null)
            return NotFound();

        if (ModelState.IsValid)
        {
            liveClass.CourseId = model.CourseId;
            liveClass.LessonId = model.LessonId;
            liveClass.Title = model.Title;
            liveClass.Description = model.Description;
            liveClass.Agenda = model.Agenda;
            liveClass.ScheduledStartTime = model.ScheduledStartTime;
            liveClass.ScheduledEndTime = model.ScheduledEndTime;
            liveClass.DurationMinutes = model.DurationMinutes;
            liveClass.Platform = model.Platform;
            liveClass.MeetingUrl = model.MeetingUrl;
            liveClass.MeetingId = model.MeetingId;
            liveClass.Password = model.Password;
            liveClass.MaxParticipants = model.MaxParticipants;
            liveClass.AllowReplay = model.AllowReplay;
            liveClass.SendReminder = model.SendReminder;
            liveClass.ReminderMinutesBefore = model.ReminderMinutesBefore;
            liveClass.IsFreeForAll = model.IsFreeForAll;

            // Update pricing fields only if no purchases exist
            var editPurchaseCount = await _liveSessionService.GetSessionPurchaseCountAsync(id);
            if (editPurchaseCount == 0)
            {
                if (Enum.TryParse<LiveSessionPricingType>(Request.Form["PricingType"].FirstOrDefault(), out var editPricingType))
                    liveClass.PricingType = editPricingType;

                if (decimal.TryParse(Request.Form["Price"].FirstOrDefault(), out var editPrice))
                    liveClass.Price = liveClass.PricingType == LiveSessionPricingType.Paid ? editPrice : 0;

                liveClass.PriceCurrency = Request.Form["PriceCurrency"].FirstOrDefault() ?? "EGP";
            }

            liveClass.Subject = Request.Form["Subject"].FirstOrDefault();

            await _context.SaveChangesAsync();

            SetSuccessMessage("تم تحديث البث المباشر بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }

        ViewBag.Courses = new SelectList(
            await _context.Courses.Where(c => c.InstructorId == userId).ToListAsync(),
            "Id", "Title", model.CourseId);

        return View(model);
    }

    /// <summary>
    /// بدء البث المباشر - Start live class
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int id)
    {
        var userId = _currentUserService.UserId;

        var liveClass = await _context.LiveClasses
            .FirstOrDefaultAsync(lc => lc.Id == id && lc.InstructorId == userId);

        if (liveClass == null)
            return NotFound();

        liveClass.Status = LiveClassStatus.Live;
        liveClass.ActualStartTime = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم بدء البث المباشر");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// إنهاء البث المباشر - End live class
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> End(int id)
    {
        var userId = _currentUserService.UserId;

        var liveClass = await _context.LiveClasses
            .FirstOrDefaultAsync(lc => lc.Id == id && lc.InstructorId == userId);

        if (liveClass == null)
            return NotFound();

        liveClass.Status = LiveClassStatus.Completed;
        liveClass.ActualEndTime = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم إنهاء البث المباشر");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// إلغاء البث المباشر - Cancel live class
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason = null)
    {
        var userId = _currentUserService.UserId;

        var liveClass = await _context.LiveClasses
            .FirstOrDefaultAsync(lc => lc.Id == id && lc.InstructorId == userId);

        if (liveClass == null)
            return NotFound();

        // Can only cancel scheduled or live classes
        if (liveClass.Status != LiveClassStatus.Scheduled && liveClass.Status != LiveClassStatus.Live)
        {
            SetErrorMessage("لا يمكن إلغاء هذه الجلسة في حالتها الحالية");
            return RedirectToAction(nameof(Details), new { id });
        }

        liveClass.Status = LiveClassStatus.Cancelled;
        liveClass.CancellationReason = reason ?? "تم الإلغاء بواسطة المدرس";
        await _context.SaveChangesAsync();

        // Notify registered students about cancellation
        try
        {
            var registeredStudentIds = await _context.LiveClassAttendances
                .Where(a => a.LiveClassId == id)
                .Select(a => a.StudentId)
                .Distinct()
                .ToListAsync();

            // Also include students who purchased the session
            var purchasedStudentIds = await _context.LiveSessionPurchases
                .Where(p => p.LiveClassId == id && p.Status == Domain.Enums.SessionPurchaseStatus.Active)
                .Select(p => p.StudentId)
                .ToListAsync();

            var allStudentIds = registeredStudentIds.Union(purchasedStudentIds).Distinct().ToList();

            if (allStudentIds.Any())
            {
                var notifications = allStudentIds.Select(studentId => new Domain.Entities.Notifications.Notification
                {
                    UserId = studentId,
                    Title = $"تم إلغاء الجلسة: {liveClass.Title}",
                    Message = $"تم إلغاء جلسة البث المباشر '{liveClass.Title}' المقررة في {liveClass.ScheduledStartTime:dd/MM/yyyy hh:mm tt}. السبب: {liveClass.CancellationReason}",
                    Type = Domain.Enums.NotificationType.LiveClass,
                    ActionUrl = $"/Student/LiveClasses/Details/{id}",
                    ActionText = "عرض التفاصيل",
                    IconClass = "fas fa-calendar-times",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                await _context.Notifications.AddRangeAsync(notifications);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Sent cancellation notifications to {Count} students for live class {LiveClassId}", 
                    allStudentIds.Count, id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send cancellation notifications for live class {LiveClassId}", id);
        }

        _logger.LogInformation(
            "Live class {LiveClassId} cancelled by instructor {InstructorId}. Reason: {Reason}", 
            id, userId, liveClass.CancellationReason);

        SetSuccessMessage("تم إلغاء البث المباشر بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// حذف البث المباشر - Delete live class
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _currentUserService.UserId;

        var liveClass = await _context.LiveClasses
            .Include(lc => lc.Attendances)
            .FirstOrDefaultAsync(lc => lc.Id == id && lc.InstructorId == userId);

        if (liveClass == null)
            return NotFound();

        // Check if live class has attendees
        if (liveClass.Attendances.Any())
        {
            SetErrorMessage("لا يمكن حذف جلسة بها سجلات حضور. يمكنك إلغاءها بدلاً من ذلك");
            return RedirectToAction(nameof(Details), new { id });
        }

        // Can only delete scheduled or cancelled classes without attendees
        if (liveClass.Status == LiveClassStatus.Live || liveClass.Status == LiveClassStatus.Completed)
        {
            SetErrorMessage("لا يمكن حذف جلسة مباشرة أو مكتملة");
            return RedirectToAction(nameof(Details), new { id });
        }

        _context.LiveClasses.Remove(liveClass);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Live class {LiveClassId} '{Title}' deleted by instructor {InstructorId}", 
            id, liveClass.Title, userId);

        SetSuccessMessage("تم حذف البث المباشر بنجاح");
        return RedirectToAction(nameof(Index));
    }
}
