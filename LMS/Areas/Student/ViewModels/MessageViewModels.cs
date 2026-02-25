using System.ComponentModel.DataAnnotations;

namespace LMS.Areas.Student.ViewModels;

/// <summary>
/// Compose message view model
/// </summary>
public class ComposeMessageViewModel
{
    [Required(ErrorMessage = "المستلم مطلوب")]
    [Display(Name = "المستلم")]
    public string? ReceiverId { get; set; }

    [Required(ErrorMessage = "الموضوع مطلوب")]
    [MaxLength(200)]
    [Display(Name = "الموضوع")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "نص الرسالة مطلوب")]
    [Display(Name = "الرسالة")]
    public string Body { get; set; } = string.Empty;

    public int? ParentMessageId { get; set; }
}

/// <summary>
/// Message thread view model
/// </summary>
public class MessageThreadViewModel
{
    public string OtherUserId { get; set; } = string.Empty;
    public string OtherUserName { get; set; } = string.Empty;
    public string? OtherUserAvatar { get; set; }
    public int TotalMessages { get; set; }
    public int UnreadCount { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public string? LastMessagePreview { get; set; }
}

