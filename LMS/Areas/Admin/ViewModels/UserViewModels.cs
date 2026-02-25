using System.ComponentModel.DataAnnotations;
using LMS.Domain.Entities.Users;

namespace LMS.Areas.Admin.ViewModels;

/// <summary>
/// نموذج قائمة المستخدمين - Users List ViewModel
/// </summary>
public class UsersListViewModel
{
    public List<ApplicationUser> Users { get; set; } = new();
    
    // Statistics
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int NewUsersThisMonth { get; set; }
    public int PendingUsers { get; set; }
    
    // Growth Percentage
    public decimal GrowthPercentage { get; set; }
    
    // Pagination
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
}

/// <summary>
/// نموذج تعديل المستخدم - Edit User ViewModel
/// </summary>
public class UserEditViewModel
{
    /// <summary>
    /// معرف المستخدم - User ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// الاسم الأول - First name
    /// </summary>
    [Required(ErrorMessage = "الاسم الأول مطلوب")]
    [MaxLength(100, ErrorMessage = "الاسم الأول يجب ألا يتجاوز 100 حرف")]
    [Display(Name = "الاسم الأول")]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// الاسم الأخير - Last name
    /// </summary>
    [Required(ErrorMessage = "الاسم الأخير مطلوب")]
    [MaxLength(100, ErrorMessage = "الاسم الأخير يجب ألا يتجاوز 100 حرف")]
    [Display(Name = "الاسم الأخير")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// البريد الإلكتروني - Email (readonly)
    /// </summary>
    [Display(Name = "البريد الإلكتروني")]
    public string? Email { get; set; }

    /// <summary>
    /// رقم الهاتف - Phone number
    /// </summary>
    [Display(Name = "رقم الهاتف")]
    [Phone(ErrorMessage = "رقم الهاتف غير صالح")]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// هل نشط - Is active
    /// </summary>
    [Display(Name = "الحساب نشط")]
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// نموذج إنشاء مستخدم - Create User ViewModel
/// </summary>
public class UserCreateViewModel
{
    /// <summary>
    /// الاسم الأول - First name
    /// </summary>
    [Required(ErrorMessage = "الاسم الأول مطلوب")]
    [MaxLength(100, ErrorMessage = "الاسم الأول يجب ألا يتجاوز 100 حرف")]
    [Display(Name = "الاسم الأول")]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// الاسم الأخير - Last name
    /// </summary>
    [Required(ErrorMessage = "الاسم الأخير مطلوب")]
    [MaxLength(100, ErrorMessage = "الاسم الأخير يجب ألا يتجاوز 100 حرف")]
    [Display(Name = "الاسم الأخير")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// اسم المستخدم - Username
    /// </summary>
    [Required(ErrorMessage = "اسم المستخدم مطلوب")]
    [MaxLength(100, ErrorMessage = "اسم المستخدم يجب ألا يتجاوز 100 حرف")]
    [Display(Name = "اسم المستخدم")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// البريد الإلكتروني - Email
    /// </summary>
    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
    [Display(Name = "البريد الإلكتروني")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// رقم الهاتف - Phone number
    /// </summary>
    [Display(Name = "رقم الهاتف")]
    [Phone(ErrorMessage = "رقم الهاتف غير صالح")]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// كلمة المرور - Password
    /// </summary>
    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    [MinLength(8, ErrorMessage = "كلمة المرور يجب أن تكون 8 أحرف على الأقل")]
    [Display(Name = "كلمة المرور")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// تأكيد كلمة المرور - Confirm Password
    /// </summary>
    [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
    [Compare("Password", ErrorMessage = "كلمة المرور وتأكيد كلمة المرور غير متطابقتين")]
    [Display(Name = "تأكيد كلمة المرور")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>
    /// تاريخ الميلاد - Date of birth
    /// </summary>
    [Display(Name = "تاريخ الميلاد")]
    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// السيرة الذاتية - Bio
    /// </summary>
    [Display(Name = "السيرة الذاتية")]
    [MaxLength(500)]
    public string? Bio { get; set; }

    /// <summary>
    /// الدور - Role
    /// </summary>
    [Display(Name = "الدور")]
    public string? Role { get; set; }

    /// <summary>
    /// تفعيل البريد الإلكتروني - Email Confirmed
    /// </summary>
    public bool EmailConfirmed { get; set; } = true;

    /// <summary>
    /// تفعيل رقم الهاتف - Phone Number Confirmed
    /// </summary>
    public bool PhoneNumberConfirmed { get; set; }

    /// <summary>
    /// إرسال بريد ترحيبي - Send Welcome Email
    /// </summary>
    public bool SendWelcomeEmail { get; set; } = true;
}

/// <summary>
/// نموذج تغيير دور المستخدم - Change User Role ViewModel
/// </summary>
public class UserRoleViewModel
{
    /// <summary>
    /// معرف المستخدم - User ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// الأدوار الحالية - Current roles
    /// </summary>
    public List<string> CurrentRoles { get; set; } = new();

    /// <summary>
    /// الدور المحدد - Selected role
    /// </summary>
    [Display(Name = "الدور")]
    public string? SelectedRole { get; set; }
}

/// <summary>
/// نموذج إحصائيات المستخدمين - User Statistics ViewModel
/// </summary>
public class UserStatisticsViewModel
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int TotalStudents { get; set; }
    public int TotalInstructors { get; set; }
    public int TotalAdmins { get; set; }
    public int NewUsersThisMonth { get; set; }
    public int NewUsersToday { get; set; }
    public int UsersLoggedInToday { get; set; }
    
    // Verification stats
    public int EmailVerifiedCount { get; set; }
    public int PhoneVerifiedCount { get; set; }
    public int TwoFactorEnabledCount { get; set; }
    public int ProfileCompletedCount { get; set; }
    
    // Chart data
    public List<UserGrowthData> WeeklyGrowth { get; set; } = new();
}

public class UserGrowthData
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

