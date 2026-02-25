using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Financial;
using LMS.Domain.Enums;
using LMS.Extensions;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة طرق السحب - Withdrawal Methods Management Controller
/// </summary>
public class WithdrawalMethodsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WithdrawalMethodsController> _logger;
    private readonly ISystemConfigurationService _configService;

    public WithdrawalMethodsController(
        ApplicationDbContext context, 
        ILogger<WithdrawalMethodsController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// قائمة طرق السحب - Withdrawal methods list
    /// </summary>
    public async Task<IActionResult> Index(bool? enabled, int page = 1)
    {
        var query = _context.WithdrawalMethods.AsQueryable();

        if (enabled.HasValue)
            query = query.Where(w => w.IsEnabled == enabled.Value);

        var pageSize = await _configService.GetPaginationSizeAsync("withdrawal_methods", 20);
        var totalCount = await query.CountAsync();
        var methods = await query
            .OrderBy(w => w.DisplayOrder)
            .ThenBy(w => w.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new WithdrawalMethodListViewModel
            {
                Id = w.Id,
                Name = w.Name,
                DisplayName = w.DisplayName,
                MethodType = w.MethodType,
                IconUrl = w.IconUrl,
                IsEnabled = w.IsEnabled,
                MinAmount = w.MinAmount,
                MaxAmount = w.MaxAmount,
                FeePercentage = w.FeePercentage,
                FixedFee = w.FixedFee,
                UsageCount = w.WithdrawalRequests.Count,
                DisplayOrder = w.DisplayOrder,
                CreatedAt = w.CreatedAt
            })
            .ToListAsync();

        ViewBag.Enabled = enabled;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.PageSize = pageSize;
        ViewBag.TotalMethods = await _context.WithdrawalMethods.CountAsync();
        ViewBag.EnabledMethods = await _context.WithdrawalMethods.CountAsync(w => w.IsEnabled);

        return View(methods);
    }

    /// <summary>
    /// تفاصيل طريقة السحب - Withdrawal method details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var method = await _context.WithdrawalMethods
            .Include(w => w.WithdrawalRequests)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (method == null)
            return NotFound();

        ViewBag.TotalRequests = method.WithdrawalRequests.Count;
        ViewBag.PendingRequests = method.WithdrawalRequests.Count(r => r.Status == WithdrawalStatus.Pending);
        ViewBag.ApprovedRequests = method.WithdrawalRequests.Count(r => r.Status == WithdrawalStatus.Approved);
        ViewBag.TotalAmount = method.WithdrawalRequests.Where(r => r.Status == WithdrawalStatus.Approved).Sum(r => r.Amount);

        return View(method);
    }

    /// <summary>
    /// إنشاء طريقة سحب جديدة - Create new withdrawal method
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        var model = new WithdrawalMethodViewModel
        {
            IsEnabled = true,
            MinAmount = 100,
            MaxAmount = 50000,
            FeePercentage = 0,
            FixedFee = 0,
            SupportedCurrencies = "[\"EGP\",\"USD\"]",
            ProcessingTime = "3-5 أيام عمل",
            RequiredFields = "[]",
            DisplayOrder = 0
        };

        return View(model);
    }

    /// <summary>
    /// حفظ طريقة السحب الجديدة - Save new withdrawal method
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WithdrawalMethodViewModel model)
    {
        if (model.MinAmount >= model.MaxAmount)
            ModelState.AddModelError(nameof(model.MaxAmount), "الحد الأقصى يجب أن يكون أكبر من الحد الأدنى");

        if (ModelState.IsValid)
        {
            // Validate JSON fields
            if (!IsValidJson(model.SupportedCurrencies))
            {
                ModelState.AddModelError(nameof(model.SupportedCurrencies), "تنسيق JSON غير صالح للعملات المدعومة");
                return View(model);
            }

            if (!IsValidJson(model.RequiredFields))
            {
                ModelState.AddModelError(nameof(model.RequiredFields), "تنسيق JSON غير صالح للحقول المطلوبة");
                return View(model);
            }

            // Check if name is unique
            var existingMethod = await _context.WithdrawalMethods
                .FirstOrDefaultAsync(w => w.Name.ToLower() == model.Name.ToLower());

            if (existingMethod != null)
            {
                ModelState.AddModelError(nameof(model.Name), "اسم الطريقة موجود بالفعل");
                return View(model);
            }

            var method = new WithdrawalMethod
            {
                Name = model.Name,
                DisplayName = model.DisplayName,
                MethodType = model.MethodType,
                Description = model.Description,
                IconUrl = model.IconUrl,
                IsEnabled = model.IsEnabled,
                MinAmount = model.MinAmount,
                MaxAmount = model.MaxAmount,
                FeePercentage = model.FeePercentage,
                FixedFee = model.FixedFee,
                SupportedCurrencies = model.SupportedCurrencies,
                ProcessingTime = model.ProcessingTime,
                RequiredFields = model.RequiredFields,
                Instructions = model.Instructions,
                DisplayOrder = model.DisplayOrder
            };

            _context.WithdrawalMethods.Add(method);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Withdrawal method {MethodName} created with ID {MethodId}", model.Name, method.Id);

            SetSuccessMessage(CultureExtensions.T("تم إنشاء طريقة السحب بنجاح", "Withdrawal method created successfully."));
            return RedirectToAction(nameof(Details), new { id = method.Id });
        }

        return View(model);
    }

    /// <summary>
    /// تعديل طريقة السحب - Edit withdrawal method
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var method = await _context.WithdrawalMethods
            .Include(w => w.WithdrawalRequests)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (method == null)
            return NotFound();

        var model = new WithdrawalMethodViewModel
        {
            Id = method.Id,
            Name = method.Name,
            DisplayName = method.DisplayName,
            MethodType = method.MethodType,
            Description = method.Description,
            IconUrl = method.IconUrl,
            IsEnabled = method.IsEnabled,
            MinAmount = method.MinAmount,
            MaxAmount = method.MaxAmount,
            FeePercentage = method.FeePercentage,
            FixedFee = method.FixedFee,
            SupportedCurrencies = method.SupportedCurrencies,
            ProcessingTime = method.ProcessingTime,
            RequiredFields = method.RequiredFields,
            Instructions = method.Instructions,
            DisplayOrder = method.DisplayOrder,
            UsageCount = method.WithdrawalRequests.Count,
            CreatedAt = method.CreatedAt
        };

        return View(model);
    }

    /// <summary>
    /// حفظ تعديلات طريقة السحب - Save withdrawal method changes
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, WithdrawalMethodViewModel model)
    {
        if (id != model.Id)
            return NotFound();

        if (model.MinAmount >= model.MaxAmount)
            ModelState.AddModelError(nameof(model.MaxAmount), "الحد الأقصى يجب أن يكون أكبر من الحد الأدنى");

        if (ModelState.IsValid)
        {
            // Validate JSON fields
            if (!IsValidJson(model.SupportedCurrencies))
            {
                ModelState.AddModelError(nameof(model.SupportedCurrencies), "تنسيق JSON غير صالح للعملات المدعومة");
                return View(model);
            }

            if (!IsValidJson(model.RequiredFields))
            {
                ModelState.AddModelError(nameof(model.RequiredFields), "تنسيق JSON غير صالح للحقول المطلوبة");
                return View(model);
            }

            var method = await _context.WithdrawalMethods.FindAsync(id);

            if (method == null)
                return NotFound();

            // Check if name is unique (excluding current record)
            var existingMethod = await _context.WithdrawalMethods
                .FirstOrDefaultAsync(w => w.Name.ToLower() == model.Name.ToLower() && w.Id != id);

            if (existingMethod != null)
            {
                ModelState.AddModelError(nameof(model.Name), "اسم الطريقة موجود بالفعل");
                return View(model);
            }

            method.Name = model.Name;
            method.DisplayName = model.DisplayName;
            method.MethodType = model.MethodType;
            method.Description = model.Description;
            method.IconUrl = model.IconUrl;
            method.IsEnabled = model.IsEnabled;
            method.MinAmount = model.MinAmount;
            method.MaxAmount = model.MaxAmount;
            method.FeePercentage = model.FeePercentage;
            method.FixedFee = model.FixedFee;
            method.SupportedCurrencies = model.SupportedCurrencies;
            method.ProcessingTime = model.ProcessingTime;
            method.RequiredFields = model.RequiredFields;
            method.Instructions = model.Instructions;
            method.DisplayOrder = model.DisplayOrder;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Withdrawal method {MethodId} updated", id);

            SetSuccessMessage(CultureExtensions.T("تم تحديث طريقة السحب بنجاح", "Withdrawal method updated successfully."));
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(model);
    }

    /// <summary>
    /// حذف طريقة السحب - Delete withdrawal method
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var method = await _context.WithdrawalMethods
            .Include(w => w.WithdrawalRequests)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (method == null)
            return NotFound();

        // Check if method is being used
        if (method.WithdrawalRequests.Any(r => r.Status == WithdrawalStatus.Pending))
        {
            SetErrorMessage(CultureExtensions.T("لا يمكن حذف طريقة السحب لأنها مستخدمة في طلبات معلقة", "Cannot delete the withdrawal method because it is used in pending requests."));
            return RedirectToAction(nameof(Details), new { id });
        }

        // Soft delete: just disable it
        method.IsEnabled = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Withdrawal method {MethodId} disabled", id);

        SetSuccessMessage(CultureExtensions.T("تم تعطيل طريقة السحب بنجاح", "Withdrawal method disabled successfully."));
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تبديل حالة التفعيل - Toggle enabled status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleEnabled(int id)
    {
        var method = await _context.WithdrawalMethods.FindAsync(id);

        if (method == null)
            return NotFound();

        method.IsEnabled = !method.IsEnabled;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Withdrawal method {MethodId} {Status}", id, method.IsEnabled ? "enabled" : "disabled");

        SetSuccessMessage(method.IsEnabled ? CultureExtensions.T("تم تفعيل طريقة السحب", "Withdrawal method enabled.") : CultureExtensions.T("تم تعطيل طريقة السحب", "Withdrawal method disabled."));
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// طلبات السحب المعلقة - Pending withdrawal requests
    /// </summary>
    public async Task<IActionResult> Requests(WithdrawalStatus? status, int page = 1)
    {
        var query = _context.WithdrawalRequests
            .Include(w => w.Instructor)
                .ThenInclude(i => i.InstructorProfile)
            .Include(w => w.WithdrawalMethod)
            .AsQueryable();

        // Default to pending if no status specified
        if (status.HasValue)
        {
            query = query.Where(w => w.Status == status.Value);
        }
        else
        {
            query = query.Where(w => w.Status == WithdrawalStatus.Pending);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("withdrawal_requests", 20);
        var totalCount = await query.CountAsync();
        var requests = await query
            .OrderByDescending(w => w.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Status = status;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.PageSize = pageSize;

        // Statistics
        ViewBag.PendingCount = await _context.WithdrawalRequests.CountAsync(w => w.Status == WithdrawalStatus.Pending);
        ViewBag.ApprovedCount = await _context.WithdrawalRequests.CountAsync(w => w.Status == WithdrawalStatus.Approved);
        ViewBag.RejectedCount = await _context.WithdrawalRequests.CountAsync(w => w.Status == WithdrawalStatus.Rejected);
        ViewBag.TotalPendingAmount = await _context.WithdrawalRequests
            .Where(w => w.Status == WithdrawalStatus.Pending)
            .SumAsync(w => (decimal?)w.Amount) ?? 0;

        return View(requests);
    }

    /// <summary>
    /// إحصائيات طرق السحب - Withdrawal methods statistics
    /// </summary>
    public async Task<IActionResult> Statistics()
    {
        var stats = await _context.WithdrawalMethods
            .Select(w => new WithdrawalMethodStatsViewModel
            {
                MethodId = w.Id,
                MethodName = w.DisplayName,
                TotalRequests = w.WithdrawalRequests.Count,
                PendingRequests = w.WithdrawalRequests.Count(r => r.Status == WithdrawalStatus.Pending),
                ApprovedRequests = w.WithdrawalRequests.Count(r => r.Status == WithdrawalStatus.Approved),
                RejectedRequests = w.WithdrawalRequests.Count(r => r.Status == WithdrawalStatus.Rejected),
                TotalAmount = w.WithdrawalRequests.Where(r => r.Status == WithdrawalStatus.Approved).Sum(r => r.Amount),
                TotalFees = w.WithdrawalRequests.Where(r => r.Status == WithdrawalStatus.Approved).Sum(r => r.Fee)
            })
            .ToListAsync();

        return View(stats);
    }

    /// <summary>
    /// تحديث ترتيب العرض - Update display order
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrder(int id, int newOrder)
    {
        var method = await _context.WithdrawalMethods.FindAsync(id);

        if (method == null)
            return NotFound();

        method.DisplayOrder = newOrder;
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    /// <summary>
    /// الموافقة على طلب السحب - Redirect to Payments/Withdrawals for unified processing
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ApproveRequest(int id)
    {
        SetInfoMessage(CultureExtensions.T("يرجى معالجة طلب السحب من صفحة المدفوعات > طلبات السحب لضمان تحديث الرصيد والإشعارات.", "Please process the withdrawal request from Payments > Withdrawals to ensure balance and notifications are updated."));
        return RedirectToAction("Withdrawals", "Payments", new { area = "Admin" });
    }

    /// <summary>
    /// رفض طلب السحب - Redirect to Payments/Withdrawals for unified processing
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RejectRequest(int id, string? reason)
    {
        SetInfoMessage(CultureExtensions.T("يرجى معالجة طلب السحب من صفحة المدفوعات > طلبات السحب لضمان تحديث الرصيد والإشعارات.", "Please process the withdrawal request from Payments > Withdrawals to ensure balance and notifications are updated."));
        return RedirectToAction("Withdrawals", "Payments", new { area = "Admin" });
    }

    /// <summary>
    /// إتمام طلب السحب (تم التحويل) - Complete withdrawal request
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteRequest(int id, string? transactionReference)
    {
        try
        {
            var request = await _context.WithdrawalRequests
                .Include(r => r.Instructor)
                    .ThenInclude(i => i.InstructorProfile)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
            {
                SetErrorMessage(CultureExtensions.T("طلب السحب غير موجود", "Withdrawal request not found."));
                return RedirectToAction(nameof(Requests));
            }

            if (request.Status != WithdrawalStatus.Approved && request.Status != WithdrawalStatus.Pending)
            {
                SetWarningMessage(CultureExtensions.T("لا يمكن إتمام هذا الطلب لأنه ليس في حالة مناسبة", "Cannot complete this request because it is not in a valid status."));
                return RedirectToAction(nameof(Requests));
            }

            request.Status = WithdrawalStatus.Completed;
            request.PaidAt = DateTime.UtcNow;
            request.ProcessedAt = DateTime.UtcNow;
            request.TransactionReference = transactionReference;

            // Update instructor balances via InstructorProfile
            if (request.Instructor?.InstructorProfile != null)
            {
                request.Instructor.InstructorProfile.PendingBalance -= request.Amount;
                request.Instructor.InstructorProfile.TotalWithdrawn += request.Amount;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Withdrawal request {RequestId} completed. Reference: {Reference}", 
                id, transactionReference ?? "N/A");
            SetSuccessMessage(CultureExtensions.T("تم إتمام طلب السحب بنجاح", "Withdrawal request completed successfully."));
            return RedirectToAction(nameof(Requests));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing withdrawal request {RequestId}", id);
            SetErrorMessage(CultureExtensions.T("حدث خطأ أثناء إتمام طلب السحب", "An error occurred while completing the withdrawal request."));
            return RedirectToAction(nameof(Requests));
        }
    }

    /// <summary>
    /// تفاصيل طلب السحب - Withdrawal request details
    /// </summary>
    public async Task<IActionResult> RequestDetails(int id)
    {
        var request = await _context.WithdrawalRequests
            .Include(r => r.Instructor)
                .ThenInclude(i => i.InstructorProfile)
            .Include(r => r.WithdrawalMethod)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
            return NotFound();

        return View(request);
    }

    #region Private Helpers

    private bool IsValidJson(string json)
    {
        try
        {
            System.Text.Json.JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}

