using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

public class AffiliatesController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ISystemConfigurationService _configService;

    public AffiliatesController(
        ApplicationDbContext context,
        ISystemConfigurationService configService)
    {
        _context = context;
        _configService = configService;
    }

    public async Task<IActionResult> Index(bool? isActive, int page = 1)
    {
        var query = _context.AffiliateLinks
            .Include(a => a.AffiliateUser)
            .Include(a => a.Course)
            .AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(a => a.IsActive == isActive.Value);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("affiliates", 20);
        var totalCount = await query.CountAsync();
        var affiliates = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AffiliateDisplayViewModel
            {
                Id = a.Id,
                AffiliateName = a.AffiliateUser != null 
                    ? $"{a.AffiliateUser.FirstName ?? ""} {a.AffiliateUser.LastName ?? ""}".Trim()
                    : "Unknown Affiliate",
                AffiliateEmail = a.AffiliateUser.Email ?? string.Empty,
                UniqueCode = a.AffiliateCode,
                CommissionRate = a.CommissionRate,
                TotalClicks = a.ClickCount,
                TotalConversions = a.ConversionCount,
                TotalEarnings = a.TotalEarnings,
                CreatedAt = a.CreatedAt,
                IsActive = a.IsActive
            })
            .ToListAsync();

        ViewBag.IsActive = isActive;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.PageSize = pageSize;

        return View(affiliates);
    }

    public async Task<IActionResult> Commissions(string? status, int page = 1)
    {
        var query = _context.AffiliateCommissions
            .Include(c => c.AffiliateLink)
                .ThenInclude(a => a.AffiliateUser)
            .Include(c => c.Payment)
                .ThenInclude(p => p.Course)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(c => c.Status == status);
        }

        var pageSize = await _configService.GetPaginationSizeAsync("affiliate_commissions", 20);
        var totalCount = await query.CountAsync();
        var commissions = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new AffiliateCommissionDisplayViewModel
            {
                Id = c.Id,
                AffiliateName = c.AffiliateLink != null && c.AffiliateLink.AffiliateUser != null
                    ? $"{c.AffiliateLink.AffiliateUser.FirstName ?? ""} {c.AffiliateLink.AffiliateUser.LastName ?? ""}".Trim()
                    : "Unknown Affiliate",
                CourseName = c.Payment != null && c.Payment.Course != null ? c.Payment.Course.Title : "غير محدد",
                SaleAmount = c.SaleAmount,
                CommissionRate = c.AppliedRate,
                CommissionAmount = c.CommissionAmount,
                Status = c.Status,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        ViewBag.Status = status;
        ViewBag.Page = page;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.PageSize = pageSize;

        return View(commissions);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveCommission(int id)
    {
        var commission = await _context.AffiliateCommissions.FindAsync(id);
        if (commission == null)
            return NotFound();

        commission.Status = "Approved";
        commission.ApprovedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم الموافقة على العمولة");
        return RedirectToAction(nameof(Commissions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("[area]/[controller]/[action]/{id}")]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var affiliate = await _context.AffiliateLinks.FindAsync(id);
        if (affiliate == null)
            return NotFound();

        affiliate.IsActive = !affiliate.IsActive;
        await _context.SaveChangesAsync();

        SetSuccessMessage(affiliate.IsActive ? "تم تفعيل الشريك بنجاح" : "تم تعطيل الشريك بنجاح");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("[area]/[controller]/[action]/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var affiliate = await _context.AffiliateLinks
            .Include(a => a.Commissions)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (affiliate == null)
            return NotFound();

        // Remove all associated commissions first
        if (affiliate.Commissions.Any())
        {
            _context.AffiliateCommissions.RemoveRange(affiliate.Commissions);
        }

        _context.AffiliateLinks.Remove(affiliate);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف الشريك وجميع العمولات المرتبطة به بنجاح");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("[area]/[controller]/[action]/{id}")]
    public async Task<IActionResult> RejectCommission(int id, string? reason)
    {
        var commission = await _context.AffiliateCommissions.FindAsync(id);
        if (commission == null)
            return NotFound();

        commission.Status = "Rejected";
        commission.RejectionReason = reason ?? "لم يتم تحديد سبب";

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم رفض العمولة");
        return RedirectToAction(nameof(Commissions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("[area]/[controller]/[action]/{id}")]
    public async Task<IActionResult> MarkAsPaid(int id)
    {
        var commission = await _context.AffiliateCommissions.FindAsync(id);
        if (commission == null)
            return NotFound();

        if (commission.Status != "Approved")
        {
            SetErrorMessage("يمكن تعيين العمولة كمدفوعة فقط بعد الموافقة عليها");
            return RedirectToAction(nameof(Commissions));
        }

        commission.Status = "Paid";
        commission.PaidAt = DateTime.UtcNow;

        // Update affiliate's paid earnings
        var affiliate = await _context.AffiliateLinks.FindAsync(commission.AffiliateLinkId);
        if (affiliate != null)
        {
            affiliate.PaidEarnings += commission.CommissionAmount;
            affiliate.PendingEarnings -= commission.CommissionAmount;
        }

        await _context.SaveChangesAsync();

        SetSuccessMessage("تم تعيين العمولة كمدفوعة");
        return RedirectToAction(nameof(Commissions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveSelectedCommissions([FromForm] string ids)
    {
        if (string.IsNullOrEmpty(ids))
        {
            SetErrorMessage("الرجاء تحديد عمولة واحدة على الأقل");
            return RedirectToAction(nameof(Commissions));
        }

        var idList = ids.Split(',').Select(int.Parse).ToList();
        var commissions = await _context.AffiliateCommissions
            .Where(c => idList.Contains(c.Id) && c.Status == "Pending")
            .ToListAsync();

        if (!commissions.Any())
        {
            SetErrorMessage("لم يتم العثور على عمولات قيد الانتظار");
            return RedirectToAction(nameof(Commissions));
        }

        foreach (var commission in commissions)
        {
            commission.Status = "Approved";
            commission.ApprovedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        SetSuccessMessage($"تم الموافقة على {commissions.Count} عمولة بنجاح");
        return RedirectToAction(nameof(Commissions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("[area]/[controller]/[action]/{id}")]
    public async Task<IActionResult> DeleteCommission(int id)
    {
        var commission = await _context.AffiliateCommissions.FindAsync(id);
        if (commission == null)
            return NotFound();

        _context.AffiliateCommissions.Remove(commission);
        await _context.SaveChangesAsync();

        SetSuccessMessage("تم حذف العمولة بنجاح");
        return RedirectToAction(nameof(Commissions));
    }
}

