using LMS.Areas.Instructor.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Financial;
using LMS.Domain.Entities.Notifications;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Helpers;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LMS.Areas.Instructor.Controllers;

/// <summary>
/// طلبات سحب الأرباح - Withdrawal Requests Controller
/// </summary>
public class WithdrawalRequestsController : InstructorBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;
    private readonly ICurrencyService _currencyService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WithdrawalRequestsController> _logger;

    public WithdrawalRequestsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IEmailService emailService,
        ICurrencyService currencyService,
        IMemoryCache cache,
        ILogger<WithdrawalRequestsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _emailService = emailService;
        _currencyService = currencyService;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// قائمة الطلبات - Withdrawal Requests List
    /// </summary>
    public async Task<IActionResult> Index(WithdrawalStatus? status)
    {
        await SetDefaultCurrencyAsync(_context, _currencyService, _cache, _logger);
        
        var userId = _currentUserService.UserId;

        var query = _context.WithdrawalRequests
            .Include(wr => wr.WithdrawalMethod)
            .Include(wr => wr.ProcessedBy)
            .Where(wr => wr.InstructorId == userId)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(wr => wr.Status == status.Value);
        }

        var requests = await query
            .OrderByDescending(wr => wr.CreatedAt)
            .ToListAsync();

        // Get instructor profile for available balance
        var instructorProfile = await _context.InstructorProfiles
            .FirstOrDefaultAsync(ip => ip.UserId == userId);

        ViewBag.AvailableBalance = instructorProfile?.AvailableBalance ?? 0;
        ViewBag.MinimumWithdrawal = instructorProfile?.MinimumWithdrawal ?? 100;
        ViewBag.Status = status;

        return View(requests);
    }

    /// <summary>
    /// إنشاء طلب سحب - Create Withdrawal Request
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await SetDefaultCurrencyAsync(_context, _currencyService, _cache, _logger);
        
        var userId = _currentUserService.UserId;

        var instructorProfile = await _context.InstructorProfiles
            .FirstOrDefaultAsync(ip => ip.UserId == userId);

        if (instructorProfile == null)
        {
            SetErrorMessage("ملف المدرس غير موجود");
            return RedirectToAction(nameof(Index));
        }

        ViewBag.AvailableBalance = instructorProfile.AvailableBalance;
        ViewBag.MinimumWithdrawal = instructorProfile.MinimumWithdrawal;
        
        var withdrawalMethods = await _context.WithdrawalMethods
            .Where(wm => wm.IsEnabled && wm.IsActive)
            .OrderBy(wm => wm.DisplayOrder)
            .ToListAsync();
        
        ViewBag.WithdrawalMethods = withdrawalMethods;
        
        // Log if no methods found and show warning
        if (withdrawalMethods == null || withdrawalMethods.Count == 0)
        {
            _logger.LogWarning("No active withdrawal methods found for instructor {InstructorId}. Total methods in DB: {TotalCount}", 
                userId, await _context.WithdrawalMethods.CountAsync());
            SetWarningMessage("لا توجد طرق سحب متاحة حالياً. يرجى الاتصال بالإدارة لإضافة طرق السحب.");
        }

        return View(new WithdrawalRequestCreateViewModel
        {
            AvailableBalance = instructorProfile.AvailableBalance,
            MinimumWithdrawal = instructorProfile.MinimumWithdrawal
        });
    }

    /// <summary>
    /// حفظ طلب السحب - Save Withdrawal Request
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WithdrawalRequestCreateViewModel model)
    {
        var userId = _currentUserService.UserId;

        if (!ModelState.IsValid)
        {
            await PopulateViewBagData(userId);
            return View(model);
        }

        try
        {
            var instructorProfile = await _context.InstructorProfiles
                .FirstOrDefaultAsync(ip => ip.UserId == userId);

            if (instructorProfile == null)
            {
                _logger.LogWarning("Instructor profile not found for user {UserId}", userId);
                SetErrorMessage("ملف المدرس غير موجود");
                return RedirectToAction(nameof(Index));
            }

            // Check if instructor is approved
            if (instructorProfile.Status != "Approved")
            {
                _logger.LogWarning("Instructor {UserId} attempted withdrawal but not approved", userId);
                SetErrorMessage("يجب أن يكون حسابك معتمداً لطلب السحب");
                return RedirectToAction(nameof(Index));
            }

            // Get last withdrawal date for validation
            var lastWithdrawal = await _context.WithdrawalRequests
                .Where(wr => wr.InstructorId == userId && wr.Status == WithdrawalStatus.Completed)
                .OrderByDescending(wr => wr.ProcessedAt)
                .FirstOrDefaultAsync();

            // Use BusinessRuleHelper for comprehensive validation
            var (isValid, validationReason) = BusinessRuleHelper.ValidateWithdrawal(
                model.Amount,
                instructorProfile.AvailableBalance,
                instructorProfile.MinimumWithdrawal,
                lastWithdrawal?.ProcessedAt);

            if (!isValid)
            {
                _logger.LogWarning("Withdrawal validation failed for instructor {UserId}: {Reason}", 
                    userId, validationReason);
                ModelState.AddModelError(nameof(model.Amount), validationReason!);
                await PopulateViewBagData(userId);
                return View(model);
            }

            // Check for pending withdrawal requests
            var hasPendingRequest = await _context.WithdrawalRequests
                .AnyAsync(wr => wr.InstructorId == userId && wr.Status == WithdrawalStatus.Pending);

            if (hasPendingRequest)
            {
                _logger.LogWarning("Instructor {UserId} has pending withdrawal request", userId);
                SetErrorMessage("لديك طلب سحب قيد المراجعة بالفعل. يرجى الانتظار حتى تتم معالجته");
                return RedirectToAction(nameof(Index));
            }

            // Verify withdrawal method exists and is active
            var withdrawalMethod = await _context.WithdrawalMethods
                .FirstOrDefaultAsync(wm => wm.Id == model.WithdrawalMethodId && wm.IsActive);

            if (withdrawalMethod == null)
            {
                _logger.LogWarning("Withdrawal method {MethodId} not found or inactive", model.WithdrawalMethodId);
                ModelState.AddModelError(nameof(model.WithdrawalMethodId), "طريقة السحب غير متاحة");
                await PopulateViewBagData(userId);
                return View(model);
            }

            // Validate withdrawal method amount limits
            if (model.Amount < withdrawalMethod.MinAmount)
            {
                _logger.LogWarning("Withdrawal amount {Amount} below minimum {Min} for method {Method}", 
                    model.Amount, withdrawalMethod.MinAmount, withdrawalMethod.Name);
                ModelState.AddModelError(nameof(model.Amount), 
                    $"الحد الأدنى للسحب باستخدام {withdrawalMethod.Name} هو {withdrawalMethod.MinAmount} جنيه");
                await PopulateViewBagData(userId);
                return View(model);
            }

            if (model.Amount > withdrawalMethod.MaxAmount)
            {
                _logger.LogWarning("Withdrawal amount {Amount} exceeds maximum {Max} for method {Method}", 
                    model.Amount, withdrawalMethod.MaxAmount, withdrawalMethod.Name);
                ModelState.AddModelError(nameof(model.Amount), 
                    $"الحد الأقصى للسحب باستخدام {withdrawalMethod.Name} هو {withdrawalMethod.MaxAmount} جنيه");
                await PopulateViewBagData(userId);
                return View(model);
            }

            // Validate account details
            if (string.IsNullOrWhiteSpace(model.AccountDetails))
            {
                ModelState.AddModelError(nameof(model.AccountDetails), "يجب إدخال تفاصيل الحساب");
                await PopulateViewBagData(userId);
                return View(model);
            }

            // Calculate fees
            var fee = (model.Amount * withdrawalMethod.FeePercentage / 100) + withdrawalMethod.FeeFixed;
            var netAmount = model.Amount - fee;

            if (netAmount <= 0)
            {
                _logger.LogWarning("Net amount is zero or negative after fees for amount {Amount}", model.Amount);
                ModelState.AddModelError(nameof(model.Amount), 
                    "المبلغ المطلوب صغير جداً. الرسوم ستكون أكبر من المبلغ");
                await PopulateViewBagData(userId);
                return View(model);
            }

            WithdrawalRequest? request = null;
            await _context.ExecuteInTransactionAsync(async () =>
            {
                request = new WithdrawalRequest
                {
                    InstructorId = userId!,
                    Amount = model.Amount,
                    Currency = model.Currency,
                    Fee = fee,
                    NetAmount = netAmount,
                    Status = WithdrawalStatus.Pending,
                    WithdrawalMethodId = model.WithdrawalMethodId,
                    AccountDetails = model.AccountDetails,
                    InstructorNotes = model.Notes
                };

                _context.WithdrawalRequests.Add(request);
                
                // Update instructor profile - deduct from available balance
                instructorProfile.AvailableBalance -= model.Amount;
                instructorProfile.LastModifiedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Get instructor user for notifications
                var instructor = await _context.Users.FindAsync(userId);
                if (instructor?.Email != null)
                {
                    try
                    {
                        // Send confirmation email to instructor
                        await _emailService.SendWithdrawalRequestAsync(
                            instructor.Email,
                            instructor.FullName,
                            model.Amount,
                            "Pending"
                        );

                        _logger.LogInformation("Sent withdrawal request confirmation email to instructor {InstructorId}", 
                            userId);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx, "Failed to send withdrawal email to {Email}", instructor.Email);
                        // Don't fail the transaction if email fails
                    }
                }

                // Create in-app notification for instructor
                var notification = new Notification
                {
                    UserId = userId!,
                    Title = "تم استلام طلب السحب",
                    Message = $"تم استلام طلب سحب بقيمة {model.Amount} {model.Currency}. المبلغ الصافي بعد الرسوم: {netAmount:F2} {model.Currency}. سيتم مراجعته من قبل الإدارة.",
                    Type = NotificationType.PaymentUpdate,
                    ActionUrl = $"/Instructor/WithdrawalRequests/Details/{request.Id}",
                    ActionText = "عرض التفاصيل",
                    IconClass = "fas fa-money-bill-wave",
                    IsRead = false
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
            });

            _logger.LogInformation(
                "Withdrawal request {RequestId} created by instructor {InstructorId}. Amount: {Amount}, Fee: {Fee}, Net: {NetAmount}", 
                request!.Id, userId, model.Amount, fee, netAmount);

            SetSuccessMessage($"تم إنشاء طلب السحب بنجاح. المبلغ الصافي بعد الرسوم: {netAmount:F2} {model.Currency}. سيتم مراجعته من قبل الإدارة.");
            return RedirectToAction(nameof(Details), new { id = request.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating withdrawal request for instructor {InstructorId}", userId);
            SetErrorMessage("حدث خطأ أثناء إنشاء طلب السحب. يرجى المحاولة مرة أخرى");
            await PopulateViewBagData(userId);
            return View(model);
        }
    }

    /// <summary>
    /// Helper method to populate ViewBag data
    /// </summary>
    private async Task PopulateViewBagData(string? userId)
    {
        var instructorProfile = await _context.InstructorProfiles
            .FirstOrDefaultAsync(ip => ip.UserId == userId);

        ViewBag.AvailableBalance = instructorProfile?.AvailableBalance ?? 0;
        ViewBag.MinimumWithdrawal = instructorProfile?.MinimumWithdrawal ?? BusinessRuleHelper.MinimumWithdrawalAmount;
        ViewBag.WithdrawalMethods = await _context.WithdrawalMethods
            .Where(wm => wm.IsEnabled && wm.IsActive)
            .OrderBy(wm => wm.DisplayOrder)
            .ToListAsync();
        
        // Log if no methods found
        if (ViewBag.WithdrawalMethods == null || ((List<WithdrawalMethod>)ViewBag.WithdrawalMethods).Count == 0)
        {
            _logger.LogWarning("No active withdrawal methods found when populating ViewBag for instructor {InstructorId}", userId);
        }
    }

    /// <summary>
    /// تفاصيل الطلب - Request Details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        await SetDefaultCurrencyAsync(_context, _currencyService, _cache, _logger);
        
        var userId = _currentUserService.UserId;

        var request = await _context.WithdrawalRequests
            .Include(wr => wr.WithdrawalMethod)
            .Include(wr => wr.ProcessedBy)
            .FirstOrDefaultAsync(wr => wr.Id == id && wr.InstructorId == userId);

        if (request == null)
            return NotFound();

        return View(request);
    }

    /// <summary>
    /// تأكيد استلام المبلغ - Confirm instructor received the amount (Approved → Completed)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmReceived(int id)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("ConfirmReceived: UserId is null or empty");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            var request = await _context.WithdrawalRequests
                .FirstOrDefaultAsync(wr => wr.Id == id && wr.InstructorId == userId);

            if (request == null)
            {
                _logger.LogWarning("Withdrawal request {RequestId} not found for instructor {InstructorId}", id, userId);
                return NotFound();
            }

            if (request.Status != WithdrawalStatus.Approved)
            {
                _logger.LogWarning("ConfirmReceived: request {RequestId} has status {Status}, expected Approved", id, request.Status);
                SetErrorMessage("لا يمكن تأكيد الاستلام إلا لطلبات تمت الموافقة عليها فقط");
                return RedirectToAction(nameof(Details), new { id });
            }

            await _context.ExecuteInTransactionAsync(async () =>
            {
                request.Status = WithdrawalStatus.Completed;
                request.InstructorConfirmedReceivedAt = DateTime.UtcNow;
                request.PaidAt = DateTime.UtcNow;
                request.UpdatedAt = DateTime.UtcNow;

                var notification = new Notification
                {
                    UserId = userId,
                    Title = "تم تأكيد استلام المبلغ",
                    Message = $"تم تسجيل تأكيد استلامك لمبلغ {request.NetAmount} {request.Currency} من طلب السحب #{request.Id}.",
                    Type = NotificationType.PaymentUpdate,
                    ActionUrl = $"/Instructor/WithdrawalRequests/Details/{request.Id}",
                    ActionText = "عرض التفاصيل",
                    IconClass = "fas fa-check-circle",
                    IsRead = false
                };
                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();
            });

            _logger.LogInformation("Withdrawal request {RequestId} confirmed received by instructor {InstructorId}", id, userId);
            SetSuccessMessage("تم تأكيد استلام المبلغ بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ConfirmReceived for request {RequestId}, instructor {InstructorId}. Message: {Message}, Inner: {Inner}",
                id, userId, ex.Message, ex.InnerException?.Message);
            SetErrorMessage("حدث خطأ أثناء تأكيد الاستلام");
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// إلغاء الطلب - Cancel Request
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = _currentUserService.UserId;

        try
        {
            var request = await _context.WithdrawalRequests
                .FirstOrDefaultAsync(wr => wr.Id == id && wr.InstructorId == userId);

            if (request == null)
            {
                _logger.LogWarning("Withdrawal request {RequestId} not found for instructor {InstructorId}", 
                    id, userId);
                return NotFound();
            }

            // Validate cancellation is allowed
            if (request.Status != WithdrawalStatus.Pending)
            {
                _logger.LogWarning("Cannot cancel withdrawal request {RequestId} with status {Status}", 
                    id, request.Status);
                SetErrorMessage("لا يمكن إلغاء طلب قيد المعالجة أو مكتمل");
                return RedirectToAction(nameof(Details), new { id });
            }

            var requestAmount = request.Amount;
            var requestCurrency = request.Currency;
            await _context.ExecuteInTransactionAsync(async () =>
            {
                request.Status = WithdrawalStatus.Cancelled;
                request.CancelledAt = DateTime.UtcNow;
                request.LastModifiedAt = DateTime.UtcNow;

                // Return amount to available balance
                var instructorProfile = await _context.InstructorProfiles
                    .FirstOrDefaultAsync(ip => ip.UserId == userId);

                if (instructorProfile == null)
                {
                    _logger.LogError("Instructor profile not found for user {UserId} during cancellation", userId);
                    throw new InvalidOperationException("ملف المدرس غير موجود");
                }

                instructorProfile.AvailableBalance += request.Amount;
                instructorProfile.LastModifiedAt = DateTime.UtcNow;

                // Create notification
                var notification = new Notification
                {
                    UserId = userId!,
                    Title = "تم إلغاء طلب السحب",
                    Message = $"تم إلغاء طلب السحب بقيمة {request.Amount} {request.Currency}. تم إرجاع المبلغ إلى رصيدك المتاح.",
                    Type = NotificationType.PaymentUpdate,
                    ActionUrl = $"/Instructor/WithdrawalRequests/Details/{request.Id}",
                    ActionText = "عرض التفاصيل",
                    IconClass = "fas fa-times-circle",
                    IsRead = false
                };
                _context.Notifications.Add(notification);

                await _context.SaveChangesAsync();
            });

            _logger.LogInformation(
                "Withdrawal request {RequestId} cancelled by instructor {InstructorId}. Amount {Amount} returned to balance", 
                id, userId, requestAmount);

            SetSuccessMessage($"تم إلغاء الطلب بنجاح. تم إرجاع {requestAmount} {requestCurrency} إلى رصيدك المتاح");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling withdrawal request {RequestId}", id);
            SetErrorMessage("حدث خطأ أثناء إلغاء الطلب");
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}

