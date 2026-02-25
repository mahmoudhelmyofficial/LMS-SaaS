namespace LMS.Areas.Instructor.ViewModels;

/// <summary>
/// Model for adding a new category via AJAX
/// Used in Courses, Books controllers
/// </summary>
public class AddCategoryModel
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
}

/// <summary>
/// Model for adding a new subcategory via AJAX
/// Used in Courses, Books controllers
/// </summary>
public class AddSubcategoryModel
{
    public int ParentCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Model for adding a new question category via AJAX
/// Used in QuestionBank controller
/// </summary>
public class AddQuestionCategoryModel
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
}

/// <summary>
/// Model for adding a new question subcategory via AJAX
/// Used in QuestionBank controller
/// </summary>
public class AddQuestionSubcategoryModel
{
    public int ParentCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}






