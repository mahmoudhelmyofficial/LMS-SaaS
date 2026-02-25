using LMS.Data;
using LMS.Domain.Entities.Social;
using LMS.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMS.Areas.Student.Controllers;

/// <summary>
/// مجموعات الدراسة - Study Groups Controller
/// </summary>
public class StudyGroupsController : StudentBaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<StudyGroupsController> _logger;

    public StudyGroupsController(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<StudyGroupsController> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// قائمة مجموعات الدراسة - Study groups list
    /// </summary>
    public async Task<IActionResult> Index()
    {
        try
        {
            var userId = _currentUserService.UserId!;

            // Get user's study groups
            var myGroups = await _context.StudyGroupMembers
                .Include(sgm => sgm.StudyGroup)
                    .ThenInclude(sg => sg.Course)
                .Include(sgm => sgm.StudyGroup)
                    .ThenInclude(sg => sg.Members)
                .Where(sgm => sgm.UserId == userId)
                .Select(sgm => sgm.StudyGroup)
                .ToListAsync();

            // Get available groups to join (from enrolled courses)
            var enrolledCourseIds = await _context.Enrollments
                .Where(e => e.StudentId == userId)
                .Select(e => e.CourseId)
                .ToListAsync();

            var myGroupIds = myGroups.Select(g => g.Id).ToHashSet();

            var availableGroups = await _context.StudyGroups
                .Include(sg => sg.Course)
                .Include(sg => sg.Members)
                .Where(sg => enrolledCourseIds.Contains(sg.CourseId) && 
                            !myGroupIds.Contains(sg.Id) &&
                            sg.IsActive)
                .ToListAsync();

            var viewModel = new StudyGroupsIndexViewModel
            {
                MyGroups = myGroups,
                AvailableGroups = availableGroups
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading study groups");
            // Return empty view instead of redirecting to dashboard
            // This likely means the StudyGroups tables don't exist yet
            SetWarningMessage("ميزة مجموعات الدراسة قيد التطوير");
            var emptyViewModel = new StudyGroupsIndexViewModel
            {
                MyGroups = new List<StudyGroup>(),
                AvailableGroups = new List<StudyGroup>()
            };
            return View(emptyViewModel);
        }
    }

    /// <summary>
    /// تفاصيل المجموعة - Group details
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var userId = _currentUserService.UserId!;

        var group = await _context.StudyGroups
            .Include(sg => sg.Course)
            .Include(sg => sg.Members)
                .ThenInclude(m => m.User)
            .Include(sg => sg.Creator)
            .FirstOrDefaultAsync(sg => sg.Id == id);

        if (group == null)
            return NotFound();

        // Check if user is a member
        var isMember = group.Members.Any(m => m.UserId == userId);
        ViewBag.IsMember = isMember;
        ViewBag.IsCreator = group.CreatorId == userId;

        return View(group);
    }

    /// <summary>
    /// إنشاء مجموعة جديدة - Create new study group
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var userId = _currentUserService.UserId!;

        // Get user's enrolled courses
        var enrolledCourses = await _context.Enrollments
            .Include(e => e.Course)
            .Where(e => e.StudentId == userId)
            .Select(e => e.Course)
            .ToListAsync();

        ViewBag.Courses = enrolledCourses;
        return View();
    }

    /// <summary>
    /// حفظ مجموعة جديدة - Save new study group
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateStudyGroupViewModel model)
    {
        var userId = _currentUserService.UserId!;

        if (!ModelState.IsValid)
        {
            var enrolledCourses = await _context.Enrollments
                .Include(e => e.Course)
                .Where(e => e.StudentId == userId)
                .Select(e => e.Course)
                .ToListAsync();

            ViewBag.Courses = enrolledCourses;
            return View(model);
        }

        try
        {
            var studyGroup = new StudyGroup
            {
                Name = model.Name,
                Description = model.Description,
                CourseId = model.CourseId,
                CreatorId = userId,
                MaxMembers = model.MaxMembers ?? 10,
                IsActive = true
            };

            _context.StudyGroups.Add(studyGroup);
            await _context.SaveChangesAsync();

            // Add creator as first member
            var membership = new StudyGroupMember
            {
                StudyGroupId = studyGroup.Id,
                UserId = userId,
                Role = GroupRole.Admin,
                JoinedAt = DateTime.UtcNow
            };

            _context.StudyGroupMembers.Add(membership);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم إنشاء المجموعة بنجاح");
            return RedirectToAction(nameof(Details), new { id = studyGroup.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating study group");
            SetErrorMessage("حدث خطأ أثناء إنشاء المجموعة");
            return View(model);
        }
    }

    /// <summary>
    /// الانضمام إلى مجموعة - Join study group
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(int groupId)
    {
        var userId = _currentUserService.UserId!;

        var group = await _context.StudyGroups
            .Include(sg => sg.Members)
            .FirstOrDefaultAsync(sg => sg.Id == groupId);

        if (group == null)
            return NotFound();

        // Check if already a member
        if (group.Members.Any(m => m.UserId == userId))
        {
            SetErrorMessage("أنت عضو في هذه المجموعة بالفعل");
            return RedirectToAction(nameof(Index));
        }

        // Check max members
        if (group.MaxMembers > 0 && group.Members.Count >= group.MaxMembers)
        {
            SetErrorMessage("المجموعة ممتلئة");
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var membership = new StudyGroupMember
            {
                StudyGroupId = groupId,
                UserId = userId,
                Role = GroupRole.Member,
                JoinedAt = DateTime.UtcNow
            };

            _context.StudyGroupMembers.Add(membership);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم الانضمام إلى المجموعة بنجاح");
            return RedirectToAction(nameof(Details), new { id = groupId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining study group {GroupId}", groupId);
            SetErrorMessage("حدث خطأ أثناء الانضمام");
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// مغادرة المجموعة - Leave study group
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(int groupId)
    {
        var userId = _currentUserService.UserId!;

        var membership = await _context.StudyGroupMembers
            .FirstOrDefaultAsync(sgm => sgm.StudyGroupId == groupId && sgm.UserId == userId);

        if (membership == null)
        {
            SetErrorMessage("أنت لست عضواً في هذه المجموعة");
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _context.StudyGroupMembers.Remove(membership);
            await _context.SaveChangesAsync();

            SetSuccessMessage("تم مغادرة المجموعة");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving study group {GroupId}", groupId);
            SetErrorMessage("حدث خطأ أثناء المغادرة");
            return RedirectToAction(nameof(Index));
        }
    }
}

#region View Models

public class StudyGroupsIndexViewModel
{
    public List<StudyGroup> MyGroups { get; set; } = new();
    public List<StudyGroup> AvailableGroups { get; set; } = new();
}

public class CreateStudyGroupViewModel
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CourseId { get; set; }
    public int? MaxMembers { get; set; }
}

#endregion

