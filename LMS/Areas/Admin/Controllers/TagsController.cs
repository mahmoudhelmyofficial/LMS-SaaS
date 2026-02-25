using LMS.Areas.Admin.ViewModels;
using LMS.Data;
using LMS.Domain.Entities.Courses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Admin.Controllers;

/// <summary>
/// إدارة الوسوم - Tags Management Controller
/// </summary>
public class TagsController : AdminBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TagsController> _logger;
    private readonly ISystemConfigurationService _configService;

    public TagsController(
        ApplicationDbContext context, 
        ILogger<TagsController> logger,
        ISystemConfigurationService configService)
    {
        _context = context;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// قائمة الوسوم - Tags list
    /// </summary>
    public async Task<IActionResult> Index(string? name, bool? active, bool? featured, int page = 1)
    {
        var query = _context.Tags.AsQueryable();

        if (!string.IsNullOrEmpty(name))
            query = query.Where(t => t.Name.Contains(name));

        if (active.HasValue)
            query = query.Where(t => t.IsActive == active.Value);

        if (featured.HasValue)
            query = query.Where(t => t.IsFeatured == featured.Value);

        var pageSize = await _configService.GetPaginationSizeAsync("tags", 50);
        var totalCount = await query.CountAsync();
        var tags = await query
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TagListViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Slug = t.Slug,
                Description = t.Description,
                Color = t.Color,
                Icon = t.Icon,
                IsActive = t.IsActive,
                IsFeatured = t.IsFeatured,
                DisplayOrder = t.DisplayOrder,
                UsageCount = t.CourseTags.Count,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        ViewBag.Name = name;
        ViewBag.Active = active;
        ViewBag.Featured = featured;
        ViewBag.Page = page;
        ViewBag.TotalTags = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.PageSize = pageSize;
        ViewBag.ActiveTags = await _context.Tags.CountAsync(t => t.IsActive);
        ViewBag.FeaturedTags = await _context.Tags.CountAsync(t => t.IsFeatured);

        return View(tags);
    }

    /// <summary>
    /// تفاصيل الوسم - Tag details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var tag = await _context.Tags
            .Include(t => t.CourseTags)
                .ThenInclude(ct => ct.Course)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tag == null)
            return NotFound();

        ViewBag.Courses = tag.CourseTags.Select(ct => ct.Course).ToList();

        return View(tag);
    }

    /// <summary>
    /// إنشاء وسم جديد - Create new tag
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        var model = new TagViewModel
        {
            IsActive = true,
            IsFeatured = false,
            DisplayOrder = 0
        };

        return View(model);
    }

    /// <summary>
    /// حفظ الوسم الجديد - Save new tag
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TagViewModel model)
    {
        if (ModelState.IsValid)
        {
            // Auto-generate slug if not provided
            var slug = string.IsNullOrWhiteSpace(model.Slug)
                ? GenerateSlug(model.Name)
                : model.Slug.ToLower().Trim();

            // Check if slug is unique
            var existingTag = await _context.Tags
                .FirstOrDefaultAsync(t => t.Slug.ToLower() == slug);

            if (existingTag != null)
            {
                // Generate unique slug by appending a random suffix
                slug = $"{slug}-{Guid.NewGuid().ToString().Substring(0, 8)}";
            }

            var tag = new Tag
            {
                Name = model.Name,
                Slug = slug,
                Description = model.Description,
                Color = model.Color,
                Icon = model.Icon,
                IsActive = model.IsActive,
                IsFeatured = model.IsFeatured,
                DisplayOrder = model.DisplayOrder
            };

            _context.Tags.Add(tag);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Tag {TagName} created with ID {TagId}", model.Name, tag.Id);

            SetSuccessMessage("تم إنشاء الوسم بنجاح");
            return RedirectToAction(nameof(Details), new { id = tag.Id });
        }

        return View(model);
    }

    /// <summary>
    /// Generate URL-friendly slug from name
    /// </summary>
    private string GenerateSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        // Convert to lowercase and replace spaces with hyphens
        var slug = name.ToLower()
            .Replace(" ", "-")
            .Replace("_", "-");

        // Remove non-alphanumeric characters (except hyphens)
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");

        // Remove consecutive hyphens
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");

        // Trim hyphens from start and end
        slug = slug.Trim('-');

        return slug;
    }

    /// <summary>
    /// تعديل الوسم - Edit tag
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var tag = await _context.Tags
            .Include(t => t.CourseTags)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tag == null)
            return NotFound();

        var model = new TagViewModel
        {
            Id = tag.Id,
            Name = tag.Name,
            Slug = tag.Slug,
            Description = tag.Description,
            Color = tag.Color,
            Icon = tag.Icon,
            IsActive = tag.IsActive,
            IsFeatured = tag.IsFeatured,
            DisplayOrder = tag.DisplayOrder,
            UsageCount = tag.CourseTags.Count,
            CreatedAt = tag.CreatedAt,
            UpdatedAt = tag.UpdatedAt
        };

        return View(model);
    }

    /// <summary>
    /// حفظ تعديلات الوسم - Save tag changes
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TagViewModel model)
    {
        if (id != model.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            var tag = await _context.Tags.FindAsync(id);

            if (tag == null)
                return NotFound();

            // Auto-generate slug if not provided
            var slug = string.IsNullOrWhiteSpace(model.Slug)
                ? GenerateSlug(model.Name)
                : model.Slug.ToLower().Trim();

            // Check if slug is unique (excluding current record)
            var existingTag = await _context.Tags
                .FirstOrDefaultAsync(t => t.Slug.ToLower() == slug && t.Id != id);

            if (existingTag != null)
            {
                ModelState.AddModelError(nameof(model.Slug), "المعرف الفريد موجود بالفعل");
                return View(model);
            }

            tag.Name = model.Name;
            tag.Slug = slug;
            tag.Description = model.Description;
            tag.Color = model.Color;
            tag.Icon = model.Icon;
            tag.IsActive = model.IsActive;
            tag.IsFeatured = model.IsFeatured;
            tag.DisplayOrder = model.DisplayOrder;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Tag {TagId} updated", id);

            SetSuccessMessage("تم تحديث الوسم بنجاح");
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(model);
    }

    /// <summary>
    /// حذف الوسم - Delete tag
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var tag = await _context.Tags
            .Include(t => t.CourseTags)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tag == null)
            return NotFound();

        // Remove all course associations
        _context.CourseTags.RemoveRange(tag.CourseTags);
        _context.Tags.Remove(tag);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Tag {TagId} deleted", id);

        SetSuccessMessage("تم حذف الوسم بنجاح");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تبديل حالة التفعيل - Toggle active status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var tag = await _context.Tags.FindAsync(id);

        if (tag == null)
            return NotFound();

        tag.IsActive = !tag.IsActive;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Tag {TagId} {Status}", id, tag.IsActive ? "activated" : "deactivated");

        SetSuccessMessage($"تم {(tag.IsActive ? "تفعيل" : "تعطيل")} الوسم");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// تبديل حالة التمييز - Toggle featured status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFeatured(int id)
    {
        var tag = await _context.Tags.FindAsync(id);

        if (tag == null)
            return NotFound();

        tag.IsFeatured = !tag.IsFeatured;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Tag {TagId} featured status changed to {Status}", id, tag.IsFeatured);

        SetSuccessMessage($"تم {(tag.IsFeatured ? "تمييز" : "إلغاء تمييز")} الوسم");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// ربط الدورات بالوسوم - Manage course tags
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ManageCourseTags(int courseId)
    {
        var course = await _context.Courses
            .Include(c => c.CourseTags)
                .ThenInclude(ct => ct.Tag)
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course == null)
            return NotFound();

        var allTags = await _context.Tags
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync();

        var model = new CourseTagsViewModel
        {
            CourseId = courseId,
            CourseName = course.Title,
            SelectedTagIds = course.CourseTags.Select(ct => ct.TagId).ToList(),
            AvailableTags = allTags.Select(t => new TagViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Slug = t.Slug,
                Color = t.Color,
                Icon = t.Icon
            }).ToList(),
            CurrentTags = course.CourseTags.Select(ct => new TagViewModel
            {
                Id = ct.Tag.Id,
                Name = ct.Tag.Name,
                Slug = ct.Tag.Slug,
                Color = ct.Tag.Color,
                Icon = ct.Tag.Icon
            }).ToList()
        };

        return View(model);
    }

    /// <summary>
    /// حفظ ربط الدورات بالوسوم - Save course tags
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManageCourseTags(CourseTagsViewModel model)
    {
        if (ModelState.IsValid)
        {
            var course = await _context.Courses
                .Include(c => c.CourseTags)
                .FirstOrDefaultAsync(c => c.Id == model.CourseId);

            if (course == null)
                return NotFound();

            // Remove existing tags
            _context.CourseTags.RemoveRange(course.CourseTags);

            // Add new tags
            foreach (var tagId in model.SelectedTagIds)
            {
                course.CourseTags.Add(new CourseTag
                {
                    CourseId = model.CourseId,
                    TagId = tagId
                });
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Course {CourseId} tags updated", model.CourseId);

            SetSuccessMessage("تم تحديث وسوم الدورة بنجاح");
            return RedirectToAction("Details", "Courses", new { id = model.CourseId });
        }

        return View(model);
    }

    /// <summary>
    /// عرض الدورات المرتبطة بالوسم - Show courses associated with a tag
    /// </summary>
    public async Task<IActionResult> Courses(int id, int page = 1)
    {
        var tag = await _context.Tags
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tag == null)
            return NotFound();

        var pageSize = await _configService.GetPaginationSizeAsync("tag_courses", 20);
        var query = _context.CourseTags
            .Include(ct => ct.Course)
                .ThenInclude(c => c.Instructor)
            .Where(ct => ct.TagId == id);

        var totalCourses = await query.CountAsync();
        var courseTags = await query
            .OrderByDescending(ct => ct.Course.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var courses = courseTags.Select(ct => ct.Course).ToList();

        ViewBag.Tag = tag;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCourses / pageSize);
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCourses = totalCourses;

        return View(courses);
    }

    /// <summary>
    /// إحصائيات الوسوم - Tags statistics
    /// </summary>
    public async Task<IActionResult> Statistics()
    {
        var stats = await _context.Tags
            .Where(t => t.IsActive)
            .Select(t => new TagStatsViewModel
            {
                TagId = t.Id,
                TagName = t.Name,
                TotalCourses = t.CourseTags.Count,
                PublishedCourses = t.CourseTags.Count(ct => ct.Course.Status == Domain.Enums.CourseStatus.Published),
                DraftCourses = t.CourseTags.Count(ct => ct.Course.Status == Domain.Enums.CourseStatus.Draft),
                TotalEnrollments = t.CourseTags.SelectMany(ct => ct.Course.Enrollments).Count(),
                TotalRevenue = t.CourseTags.SelectMany(ct => ct.Course.Enrollments).Sum(e => e.PaidAmount),
                AverageRating = t.CourseTags.Any() ? t.CourseTags.Average(ct => ct.Course.AverageRating) : 0
            })
            .OrderByDescending(s => s.TotalCourses)
            .ToListAsync();

        return View(stats);
    }

    /// <summary>
    /// دمج الوسوم - Merge tags
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Merge(int? id = null)
    {
        ViewBag.Tags = await _context.Tags
            .OrderBy(t => t.Name)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();

        var model = new MergeTagsViewModel();
        
        // If an id is provided, pre-select it as the source tag
        if (id.HasValue)
        {
            var sourceTag = await _context.Tags.FindAsync(id.Value);
            if (sourceTag != null)
            {
                model.SourceTagId = sourceTag.Id;
                model.SourceTagName = sourceTag.Name;
                model.CoursesAffected = await _context.CourseTags.CountAsync(ct => ct.TagId == id.Value);
            }
        }

        return View(model);
    }

    /// <summary>
    /// تنفيذ دمج الوسوم - Execute merge tags
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Merge(MergeTagsViewModel model)
    {
        if (model.SourceTagId == model.TargetTagId)
        {
            ModelState.AddModelError("", "لا يمكن دمج الوسم مع نفسه");
        }

        if (ModelState.IsValid)
        {
            var sourceTag = await _context.Tags
                .Include(t => t.CourseTags)
                .FirstOrDefaultAsync(t => t.Id == model.SourceTagId);

            var targetTag = await _context.Tags
                .Include(t => t.CourseTags)
                .FirstOrDefaultAsync(t => t.Id == model.TargetTagId);

            if (sourceTag == null || targetTag == null)
                return NotFound();

            // Move all course tags from source to target
            var coursesToUpdate = sourceTag.CourseTags.ToList();

            foreach (var courseTag in coursesToUpdate)
            {
                // Check if target tag is already assigned to this course
                var existingAssignment = await _context.CourseTags
                    .FirstOrDefaultAsync(ct => ct.CourseId == courseTag.CourseId && ct.TagId == model.TargetTagId);

                if (existingAssignment == null)
                {
                    courseTag.TagId = model.TargetTagId;
                }
                else
                {
                    // Remove duplicate
                    _context.CourseTags.Remove(courseTag);
                }
            }

            // Delete source tag
            _context.Tags.Remove(sourceTag);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Tag {SourceTagId} merged into {TargetTagId}", model.SourceTagId, model.TargetTagId);

            SetSuccessMessage($"تم دمج الوسوم بنجاح. تم تحديث {coursesToUpdate.Count} دورة");
            return RedirectToAction(nameof(Details), new { id = model.TargetTagId });
        }

        ViewBag.Tags = await _context.Tags
            .OrderBy(t => t.Name)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();

        return View(model);
    }

    /// <summary>
    /// تحديث ترتيب العرض - Update display order
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrder(int id, int newOrder)
    {
        var tag = await _context.Tags.FindAsync(id);

        if (tag == null)
            return NotFound();

        tag.DisplayOrder = newOrder;
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    /// <summary>
    /// البحث عن الوسوم - Search tags (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Search(string query, int limit = 10)
    {
        var tags = await _context.Tags
            .Where(t => t.IsActive && t.Name.Contains(query))
            .OrderBy(t => t.Name)
            .Take(limit)
            .Select(t => new
            {
                id = t.Id,
                name = t.Name,
                slug = t.Slug,
                color = t.Color,
                icon = t.Icon
            })
            .ToListAsync();

        return Json(tags);
    }
}

