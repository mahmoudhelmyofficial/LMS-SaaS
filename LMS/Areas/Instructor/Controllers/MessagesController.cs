using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Learning;
using LMS.Domain.Entities.Notifications;
using LMS.Domain.Entities.Social;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// رسائل المدرس - Instructor Messages Controller
/// </summary>
public class MessagesController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<MessagesController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// صندوق الوارد - Inbox
    /// </summary>
    public async Task<IActionResult> Index(int page = 1)
    {
        try
        {
            if (page < 1) page = 1;

            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Index: UserId is null or empty");
                SetErrorMessage("حدث خطأ في المصادقة. يرجى تسجيل الدخول مرة أخرى.");
                return RedirectToAction("Index", "Dashboard");
            }

            const int pageSize = 20;

            // Calculate total count for pagination
            var totalCount = await _context.DirectMessages
                .CountAsync(m => m.RecipientId == userId && !m.RecipientDeleted);

            var messages = await _context.DirectMessages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .Where(m => m.RecipientId == userId && !m.RecipientDeleted)
                .OrderByDescending(m => m.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;
            ViewBag.PageSize = pageSize;
            ViewBag.UnreadCount = await _context.DirectMessages
                .CountAsync(m => m.RecipientId == userId && !m.IsRead && !m.RecipientDeleted);

            _logger.LogInformation("Instructor {InstructorId} viewed inbox. Page: {Page}, Total: {TotalCount}", userId, page, totalCount);

            return View(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading inbox for instructor {InstructorId}", _currentUserService.UserId);
            SetErrorMessage("حدث خطأ أثناء تحميل الرسائل الواردة.");
            return View(new List<DirectMessage>());
        }
    }

    /// <summary>
    /// الرسائل المرسلة - Sent messages
    /// </summary>
    public async Task<IActionResult> Sent(int page = 1)
    {
        try
        {
            if (page < 1) page = 1;

            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Sent: UserId is null or empty");
                SetErrorMessage("حدث خطأ في المصادقة. يرجى تسجيل الدخول مرة أخرى.");
                return RedirectToAction("Index", "Dashboard");
            }

            const int pageSize = 20;

            // Calculate total count for pagination
            var totalCount = await _context.DirectMessages
                .CountAsync(m => m.SenderId == userId && !m.SenderDeleted);

            var messages = await _context.DirectMessages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .Where(m => m.SenderId == userId && !m.SenderDeleted)
                .OrderByDescending(m => m.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;
            ViewBag.PageSize = pageSize;

            _logger.LogInformation("Instructor {InstructorId} viewed sent messages. Page: {Page}, Total: {TotalCount}", userId, page, totalCount);

            return View(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading sent messages for instructor {InstructorId}", _currentUserService.UserId);
            SetErrorMessage("حدث خطأ أثناء تحميل الرسائل المرسلة.");
            return View(new List<DirectMessage>());
        }
    }

    /// <summary>
    /// عرض الرسالة - View message
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Details: UserId is null or empty");
                SetErrorMessage("حدث خطأ في المصادقة. يرجى تسجيل الدخول مرة أخرى.");
                return RedirectToAction("Index", "Dashboard");
            }

            var message = await _context.DirectMessages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .FirstOrDefaultAsync(m => m.Id == id && 
                    (m.SenderId == userId || m.RecipientId == userId));

            if (message == null)
            {
                _logger.LogWarning("Details: Message {MessageId} not found for instructor {InstructorId}", id, userId);
                SetErrorMessage("الرسالة غير موجودة أو ليس لديك صلاحية عليها.");
                return NotFound();
            }

            // Check if message is deleted by the current user
            if ((message.SenderId == userId && message.SenderDeleted) ||
                (message.RecipientId == userId && message.RecipientDeleted))
            {
                _logger.LogWarning("Details: Message {MessageId} is deleted for instructor {InstructorId}", id, userId);
                SetErrorMessage("الرسالة محذوفة.");
                return RedirectToAction(nameof(Index));
            }

            // Mark as read if recipient is viewing
            if (message.RecipientId == userId && !message.IsRead)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Message {MessageId} marked as read by instructor {InstructorId}", id, userId);
            }

            return View(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading message {MessageId} for instructor {InstructorId}", id, _currentUserService.UserId);
            SetErrorMessage("حدث خطأ أثناء تحميل الرسالة.");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// إرسال رسالة جديدة - Send new message
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Compose(string? receiverId, int? courseId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Compose GET: UserId is null or empty");
                SetErrorMessage("حدث خطأ في المصادقة. يرجى تسجيل الدخول مرة أخرى.");
                return RedirectToAction("Index", "Dashboard");
            }

            var model = new MessageComposeViewModel
            {
                ReceiverId = receiverId ?? string.Empty
            };

            // Get students from instructor's courses
            await LoadStudentsListAsync(userId);

            if (courseId.HasValue)
            {
                var course = await _context.Courses
                    .FirstOrDefaultAsync(c => c.Id == courseId.Value && c.InstructorId == userId);
                
                if (course != null)
                {
                    ViewBag.CourseName = course.Title;
                }
                else
                {
                    _logger.LogWarning("Compose GET: Course {CourseId} not found or not owned by instructor {InstructorId}", courseId.Value, userId);
                    SetWarningMessage("الدورة المحددة غير موجودة أو ليس لديك صلاحية عليها.");
                }
            }

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading compose page for instructor {InstructorId}", _currentUserService.UserId);
            SetErrorMessage("حدث خطأ أثناء تحميل صفحة إرسال الرسالة.");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// حفظ الرسالة الجديدة - Save new message
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Compose(MessageComposeViewModel model)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Compose POST: UserId is null or empty");
                SetErrorMessage("حدث خطأ في المصادقة. يرجى تسجيل الدخول مرة أخرى.");
                return RedirectToAction("Index", "Dashboard");
            }

            if (!ModelState.IsValid)
            {
                await LoadStudentsListAsync(userId);
                return View(model);
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(model.ReceiverId))
            {
                ModelState.AddModelError(nameof(model.ReceiverId), "المستلم مطلوب");
                SetErrorMessage("يرجى اختيار المستلم");
                await LoadStudentsListAsync(userId);
                return View(model);
            }

            // Verify the recipient is a student enrolled in instructor's courses
            var isEnrolled = await _context.Enrollments
                .AnyAsync(e => e.StudentId == model.ReceiverId && e.Course.InstructorId == userId);

            if (!isEnrolled)
            {
                ModelState.AddModelError(nameof(model.ReceiverId), "المستلم يجب أن يكون طالباً مسجلاً في إحدى دوراتك");
                SetErrorMessage("المستلم يجب أن يكون طالباً مسجلاً في إحدى دوراتك");
                _logger.LogWarning("Instructor {InstructorId} attempted to send message to non-enrolled student {StudentId}.", userId, model.ReceiverId);
                await LoadStudentsListAsync(userId);
                return View(model);
            }

            // Validate message content
            var (isValid, reason) = BusinessRuleHelper.ValidateMessage(model.Subject, model.Body);
            if (!isValid)
            {
                ModelState.AddModelError(string.Empty, reason!);
                SetErrorMessage(reason!);
                await LoadStudentsListAsync(userId);
                return View(model);
            }

            DirectMessage? message = null;
            try
            {
                await _context.ExecuteInTransactionAsync(async () =>
                {
                    message = new DirectMessage
                    {
                        SenderId = userId,
                        RecipientId = model.ReceiverId,
                        Subject = model.Subject?.Trim() ?? string.Empty,
                        Message = model.Body?.Trim() ?? string.Empty,
                        SentAt = DateTime.UtcNow,
                        IsRead = false,
                        ReplyToId = model.ParentMessageId
                    };

                    _context.DirectMessages.Add(message);
                    await _context.SaveChangesAsync();

                    // Create in-app notification for the recipient
                    var notificationMessage = model.Body.Length > 100 ? model.Body.Substring(0, 100) + "..." : model.Body;
                    var notification = new Notification
                    {
                        UserId = model.ReceiverId,
                        Title = $"رسالة جديدة: {model.Subject}",
                        Message = notificationMessage,
                        Type = Domain.Enums.NotificationType.NewMessage,
                        ActionUrl = $"/Student/Messages/Details/{message.Id}",
                        ActionText = "عرض الرسالة",
                        IconClass = "fas fa-envelope",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync();
                });

                _logger.LogInformation("Instructor {InstructorId} sent message to student {StudentId}. Message ID: {MessageId}", userId, model.ReceiverId, message!.Id);
                SetSuccessMessage("تم إرسال الرسالة بنجاح");
                return RedirectToAction(nameof(Sent));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message from instructor {InstructorId} to student {StudentId}.", userId, model.ReceiverId);
                SetErrorMessage("حدث خطأ أثناء إرسال الرسالة. يرجى المحاولة مرة أخرى.");
                ModelState.AddModelError(string.Empty, "حدث خطأ أثناء إرسال الرسالة");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Compose POST for instructor {InstructorId}", _currentUserService.UserId);
            SetErrorMessage("حدث خطأ غير متوقع. يرجى المحاولة مرة أخرى.");
        }

        // Reload students list on error
        await LoadStudentsListAsync(_currentUserService.UserId!);
        return View(model);
    }

    private async Task LoadStudentsListAsync(string instructorId)
    {
        try
        {
            var students = await _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.Course)
                .Where(e => e.Course.InstructorId == instructorId
                    && e.Student != null
                    && e.Status == EnrollmentStatus.Active)
                .Select(e => new StudentDropdownItem
                {
                    StudentId = e.StudentId,
                    FullName = ((e.Student.FirstName ?? "") + " " + (e.Student.LastName ?? "")).Trim(),
                    Email = e.Student.Email ?? string.Empty,
                    CourseName = e.Course.Title ?? "دورة غير معروفة"
                })
                .Distinct()
                .OrderBy(s => s.FullName)
                .ToListAsync();

            // Fallback for empty names
            foreach (var student in students)
            {
                if (string.IsNullOrWhiteSpace(student.FullName))
                    student.FullName = "غير معروف";
            }

            ViewBag.Students = students;
            _logger.LogInformation("Loaded {Count} students for instructor {InstructorId}", students.Count, instructorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading students list for instructor {InstructorId}", instructorId);
            ViewBag.Students = new List<StudentDropdownItem>();
        }
    }

    /// <summary>
    /// إرسال رسالة جماعية - Send bulk message to students
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> BulkMessage(int? courseId)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("BulkMessage GET: UserId is null or empty");
                SetErrorMessage("حدث خطأ في المصادقة. يرجى تسجيل الدخول مرة أخرى.");
                return RedirectToAction("Index", "Dashboard");
            }

            var model = new BulkMessageViewModel
            {
                CourseId = courseId
            };

            // Get instructor's courses with student count
            await LoadCoursesListAsync(userId);

            if (courseId.HasValue)
            {
                var course = await _context.Courses
                    .FirstOrDefaultAsync(c => c.Id == courseId.Value && c.InstructorId == userId);
                
                if (course == null)
                {
                    _logger.LogWarning("BulkMessage GET: Course {CourseId} not found or not owned by instructor {InstructorId}", courseId.Value, userId);
                    SetWarningMessage("الدورة المحددة غير موجودة أو ليس لديك صلاحية عليها.");
                }
            }

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading bulk message page for instructor {InstructorId}", _currentUserService.UserId);
            SetErrorMessage("حدث خطأ أثناء تحميل صفحة الرسالة الجماعية.");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// حفظ الرسالة الجماعية - Save bulk message
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkMessage(BulkMessageViewModel model)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("BulkMessage POST: UserId is null or empty");
                SetErrorMessage("حدث خطأ في المصادقة. يرجى تسجيل الدخول مرة أخرى.");
                return RedirectToAction("Index", "Dashboard");
            }

            if (!ModelState.IsValid)
            {
                await LoadCoursesListAsync(userId);
                return View(model);
            }

            // Validate message content
            var (isValid, reason) = BusinessRuleHelper.ValidateMessage(model.Subject, model.Body);
            if (!isValid)
            {
                ModelState.AddModelError(string.Empty, reason!);
                SetErrorMessage(reason!);
                await LoadCoursesListAsync(userId);
                return View(model);
            }

            // Verify course ownership if course is specified
            if (model.CourseId.HasValue)
            {
                var courseExists = await _context.Courses
                    .AnyAsync(c => c.Id == model.CourseId.Value && c.InstructorId == userId);
                
                if (!courseExists)
                {
                    ModelState.AddModelError(nameof(model.CourseId), "الدورة المحددة غير موجودة أو ليس لديك صلاحية عليها");
                    SetErrorMessage("الدورة المحددة غير موجودة أو ليس لديك صلاحية عليها");
                    _logger.LogWarning("Instructor {InstructorId} attempted to send bulk message to course {CourseId} they don't own", userId, model.CourseId.Value);
                    await LoadCoursesListAsync(userId);
                    return View(model);
                }
            }

            var recipientsCount = 0;
            try
            {
                await _context.ExecuteInTransactionAsync(async () =>
                {
                    // Get recipients based on course selection (only active enrollments)
                    var recipients = model.CourseId.HasValue
                        ? await _context.Enrollments
                            .Where(e => e.CourseId == model.CourseId.Value 
                                && e.Course.InstructorId == userId
                                && e.Status == EnrollmentStatus.Active)
                            .Select(e => e.StudentId)
                            .Distinct()
                            .ToListAsync()
                        : await _context.Enrollments
                            .Where(e => e.Course.InstructorId == userId
                                && e.Status == EnrollmentStatus.Active)
                            .Select(e => e.StudentId)
                            .Distinct()
                            .ToListAsync();

                    if (!recipients.Any())
                    {
                        throw new InvalidOperationException("لا يوجد طلاب لإرسال الرسالة إليهم");
                    }

                    recipientsCount = recipients.Count;
                    var notificationMessage = model.Body.Length > 100 ? model.Body.Substring(0, 100) + "..." : model.Body;
                    var messages = new List<DirectMessage>();
                    var notifications = new List<Notification>();

                    // Create message and notification for each recipient
                    foreach (var recipientId in recipients)
                    {
                        var message = new DirectMessage
                        {
                            SenderId = userId,
                            RecipientId = recipientId,
                            Subject = model.Subject?.Trim() ?? string.Empty,
                            Message = model.Body?.Trim() ?? string.Empty,
                            SentAt = DateTime.UtcNow,
                            IsRead = false
                        };
                        messages.Add(message);

                        var notification = new Notification
                        {
                            UserId = recipientId,
                            Title = $"رسالة جديدة: {model.Subject}",
                            Message = notificationMessage,
                            Type = Domain.Enums.NotificationType.NewMessage,
                            ActionUrl = $"/Student/Messages/Index",
                            ActionText = "عرض الرسائل",
                            IconClass = "fas fa-envelope",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        };
                        notifications.Add(notification);
                    }

                    // Bulk insert for better performance
                    await _context.DirectMessages.AddRangeAsync(messages);
                    await _context.SaveChangesAsync();

                    // Update notification ActionUrl with actual message IDs
                    for (int i = 0; i < notifications.Count; i++)
                    {
                        notifications[i].ActionUrl = $"/Student/Messages/Details/{messages[i].Id}";
                    }

                    await _context.Notifications.AddRangeAsync(notifications);
                    await _context.SaveChangesAsync();
                });

                _logger.LogInformation("Instructor {InstructorId} sent bulk message to {RecipientsCount} students.", userId, recipientsCount);
                SetSuccessMessage($"تم إرسال الرسالة إلى {recipientsCount} طالب بنجاح");
                return RedirectToAction(nameof(Sent));
            }
            catch (InvalidOperationException opEx)
            {
                ModelState.AddModelError(string.Empty, opEx.Message);
                SetErrorMessage(opEx.Message);
                await LoadCoursesListAsync(userId);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending bulk message from instructor {InstructorId}.", userId);
                SetErrorMessage("حدث خطأ أثناء إرسال الرسائل. يرجى المحاولة مرة أخرى.");
                ModelState.AddModelError(string.Empty, "حدث خطأ أثناء إرسال الرسائل");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in BulkMessage POST for instructor {InstructorId}", _currentUserService.UserId);
            SetErrorMessage("حدث خطأ غير متوقع. يرجى المحاولة مرة أخرى.");
        }

        // Reload courses list on error
        await LoadCoursesListAsync(_currentUserService.UserId!);
        return View(model);
    }

    private async Task LoadCoursesListAsync(string instructorId)
    {
        try
        {
            var courses = await _context.Courses
                .Where(c => c.InstructorId == instructorId)
                .Select(c => new { c.Id, Title = c.Title ?? "دورة بدون عنوان" })
                .OrderBy(c => c.Title)
                .ToListAsync();

            var coursesWithCounts = new List<CourseWithCountDto>();
            foreach (var course in courses)
            {
                var count = await _context.Enrollments
                    .CountAsync(e => e.CourseId == course.Id && e.Status == EnrollmentStatus.Active);
                coursesWithCounts.Add(new CourseWithCountDto
                {
                    Id = course.Id,
                    Title = course.Title,
                    TotalStudents = count
                });
            }

            ViewBag.Courses = coursesWithCounts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading courses list for instructor {InstructorId}", instructorId);
            ViewBag.Courses = new List<CourseWithCountDto>();
        }
    }

    /// <summary>
    /// الرد على رسالة - Reply to message
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Reply(int id)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Reply: UserId is null or empty");
                SetErrorMessage("حدث خطأ في المصادقة. يرجى تسجيل الدخول مرة أخرى.");
                return RedirectToAction("Index", "Dashboard");
            }

            // Allow replying to both received and sent messages
            var originalMessage = await _context.DirectMessages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .FirstOrDefaultAsync(m => m.Id == id && 
                    (m.SenderId == userId || m.RecipientId == userId));

            if (originalMessage == null)
            {
                _logger.LogWarning("Reply: Message {MessageId} not found for instructor {InstructorId}", id, userId);
                SetErrorMessage("الرسالة غير موجودة أو ليس لديك صلاحية عليها.");
                return NotFound();
            }

            // Check if message is deleted
            if ((originalMessage.SenderId == userId && originalMessage.SenderDeleted) ||
                (originalMessage.RecipientId == userId && originalMessage.RecipientDeleted))
            {
                _logger.LogWarning("Reply: Message {MessageId} is deleted for instructor {InstructorId}", id, userId);
                SetErrorMessage("الرسالة محذوفة.");
                return RedirectToAction(nameof(Index));
            }

            // Determine reply recipient
            // If instructor is the recipient, reply to sender
            // If instructor is the sender, reply to recipient (for continuing conversation)
            var replyToId = originalMessage.RecipientId == userId 
                ? originalMessage.SenderId 
                : originalMessage.RecipientId;

            // Verify the reply recipient is a student enrolled in instructor's courses
            var isEnrolled = await _context.Enrollments
                .AnyAsync(e => e.StudentId == replyToId && e.Course.InstructorId == userId);

            if (!isEnrolled)
            {
                _logger.LogWarning("Reply: Reply recipient {RecipientId} is not enrolled in instructor {InstructorId} courses", replyToId, userId);
                SetErrorMessage("لا يمكن الرد على هذه الرسالة. المستلم غير مسجل في دوراتك.");
                return RedirectToAction(nameof(Index));
            }

            var subject = originalMessage.Subject ?? string.Empty;
            var replySubject = subject.StartsWith("Re: ", StringComparison.OrdinalIgnoreCase) 
                ? subject 
                : $"Re: {subject}";

            var model = new MessageComposeViewModel
            {
                ReceiverId = replyToId,
                Subject = replySubject,
                ParentMessageId = originalMessage.Id
            };

            ViewBag.OriginalMessage = originalMessage;
            await LoadStudentsListAsync(userId);

            return View("Compose", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading reply page for message {MessageId} by instructor {InstructorId}", id, _currentUserService.UserId);
            SetErrorMessage("حدث خطأ أثناء تحميل صفحة الرد.");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// حذف رسالة - Delete message
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Delete: UserId is null or empty");
                SetErrorMessage("حدث خطأ في المصادقة. يرجى تسجيل الدخول مرة أخرى.");
                return RedirectToAction("Index", "Dashboard");
            }

            var message = await _context.DirectMessages
                .FirstOrDefaultAsync(m => m.Id == id && 
                    (m.SenderId == userId || m.RecipientId == userId));

            if (message == null)
            {
                _logger.LogWarning("Delete: Message {MessageId} not found for instructor {InstructorId}.", id, userId);
                SetErrorMessage("الرسالة غير موجودة أو ليس لديك صلاحية عليها.");
                return NotFound();
            }

            // Check if already deleted by this user
            if ((message.SenderId == userId && message.SenderDeleted) ||
                (message.RecipientId == userId && message.RecipientDeleted))
            {
                _logger.LogInformation("Delete: Message {MessageId} already deleted by instructor {InstructorId}.", id, userId);
                SetInfoMessage("الرسالة محذوفة بالفعل.");
                return RedirectToAction(nameof(Index));
            }

            var isSender = message.SenderId == userId;
            try
            {
                await _context.ExecuteInTransactionAsync(async () =>
                {
                    if (message.SenderId == userId)
                    {
                        message.SenderDeleted = true;
                    }

                    if (message.RecipientId == userId)
                    {
                        message.RecipientDeleted = true;
                    }

                    await _context.SaveChangesAsync();
                });

                _logger.LogInformation("Instructor {InstructorId} deleted message {MessageId}.", userId, id);
                SetSuccessMessage("تم حذف الرسالة بنجاح");
                
                // Redirect based on where the message was viewed from
                if (isSender)
                    return RedirectToAction(nameof(Sent));
                else
                    return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId} by instructor {InstructorId}.", id, userId);
                SetErrorMessage("حدث خطأ أثناء حذف الرسالة.");
                return RedirectToAction(nameof(Index));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Delete action for message {MessageId} by instructor {InstructorId}", id, _currentUserService.UserId);
            SetErrorMessage("حدث خطأ غير متوقع أثناء حذف الرسالة.");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// تحديد رسالة كمقروءة - Mark single message as read
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("MarkAsRead: UserId is null or empty");
                return Json(new { success = false, message = "حدث خطأ في المصادقة" });
            }

            var message = await _context.DirectMessages
                .FirstOrDefaultAsync(m => m.Id == id && m.RecipientId == userId && !m.RecipientDeleted);

            if (message == null)
            {
                _logger.LogWarning("MarkAsRead: Message {MessageId} not found for recipient {UserId}", id, userId);
                return Json(new { success = false, message = "الرسالة غير موجودة" });
            }

            if (message.IsRead)
            {
                return Json(new { success = true, message = "الرسالة مقروءة بالفعل" });
            }

            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Message {MessageId} marked as read by instructor {InstructorId}", id, userId);
            return Json(new { success = true, message = "تم تحديد الرسالة كمقروءة" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking message {MessageId} as read", id);
            return Json(new { success = false, message = "حدث خطأ أثناء تحديث الرسالة" });
        }
    }

    /// <summary>
    /// عدد الرسائل غير المقروءة - Unread messages count (API)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUnreadCount()
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { unreadCount = 0 });
            }

            var count = await _context.DirectMessages
                .CountAsync(m => m.RecipientId == userId && !m.IsRead && !m.RecipientDeleted);

            return Json(new { unreadCount = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count for instructor {InstructorId}.", _currentUserService.UserId);
            return Json(new { unreadCount = 0 });
        }
    }

    /// <summary>
    /// تحديد جميع الرسائل كمقروءة - Mark all as read
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("MarkAllAsRead: UserId is null or empty");
                SetErrorMessage("حدث خطأ في المصادقة. يرجى تسجيل الدخول مرة أخرى.");
                return RedirectToAction("Index", "Dashboard");
            }

            var markedCount = 0;
            try
            {
                await _context.ExecuteInTransactionAsync(async () =>
                {
                    // Filter out deleted messages
                    var unreadMessages = await _context.DirectMessages
                        .Where(m => m.RecipientId == userId && !m.IsRead && !m.RecipientDeleted)
                        .ToListAsync();

                    if (!unreadMessages.Any())
                    {
                        return;
                    }

                    markedCount = unreadMessages.Count;
                    foreach (var message in unreadMessages)
                    {
                        message.IsRead = true;
                        message.ReadAt = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync();
                });

                if (markedCount == 0)
                {
                    SetInfoMessage("لا توجد رسائل غير مقروءة.");
                    return RedirectToAction(nameof(Index));
                }

                _logger.LogInformation("Instructor {InstructorId} marked {Count} messages as read.", userId, markedCount);
                SetSuccessMessage($"تم تحديد {markedCount} رسالة كمقروءة");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all messages as read for instructor {InstructorId}.", userId);
                SetErrorMessage("حدث خطأ أثناء تحديث الرسائل.");
                return RedirectToAction(nameof(Index));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MarkAllAsRead for instructor {InstructorId}", _currentUserService.UserId);
            SetErrorMessage("حدث خطأ غير متوقع أثناء تحديث الرسائل.");
            return RedirectToAction(nameof(Index));
        }
    }
}

