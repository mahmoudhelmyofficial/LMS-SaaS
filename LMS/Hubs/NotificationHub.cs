using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LMS.Hubs;

/// <summary>
/// مركز الإشعارات في الوقت الفعلي - Real-time Notification Hub
/// Enables push notifications to connected clients using SignalR
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// الانضمام إلى مجموعة المستخدم - Join user's notification group
    /// Called when a user connects to receive their notifications
    /// </summary>
    public async Task JoinUserGroup(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Attempted to join group with empty userId");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroupName(userId));
        _logger.LogDebug("User {UserId} joined notification group. ConnectionId: {ConnectionId}", 
            userId, Context.ConnectionId);
    }

    /// <summary>
    /// مغادرة مجموعة المستخدم - Leave user's notification group
    /// Called when a user wants to stop receiving notifications
    /// </summary>
    public async Task LeaveUserGroup(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetUserGroupName(userId));
        _logger.LogDebug("User {UserId} left notification group. ConnectionId: {ConnectionId}", 
            userId, Context.ConnectionId);
    }

    /// <summary>
    /// الانضمام إلى مجموعة الدور - Join role-based group (for broadcast notifications)
    /// </summary>
    public async Task JoinRoleGroup(string role)
    {
        if (string.IsNullOrEmpty(role))
        {
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetRoleGroupName(role));
        _logger.LogDebug("ConnectionId {ConnectionId} joined role group: {Role}", 
            Context.ConnectionId, role);
    }

    /// <summary>
    /// تحديد الإشعار كمقروء - Mark notification as read (client request)
    /// </summary>
    public async Task MarkNotificationRead(int notificationId)
    {
        // This can be handled by the controller, but we can emit an event
        // to update other connected clients for the same user
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (!string.IsNullOrEmpty(userId))
        {
            await Clients.Group(GetUserGroupName(userId))
                .SendAsync("NotificationRead", notificationId);
        }
    }

    /// <summary>
    /// عند الاتصال - On client connected
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (!string.IsNullOrEmpty(userId))
        {
            // Auto-join user to their notification group
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroupName(userId));
            
            // Join role-based groups if applicable
            if (Context.User?.IsInRole("Instructor") == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GetRoleGroupName("Instructor"));
            }
            if (Context.User?.IsInRole("Admin") == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GetRoleGroupName("Admin"));
            }
            if (Context.User?.IsInRole("Student") == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GetRoleGroupName("Student"));
            }
            
            _logger.LogInformation("User {UserId} connected to NotificationHub. ConnectionId: {ConnectionId}", 
                userId, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// عند قطع الاتصال - On client disconnected
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (!string.IsNullOrEmpty(userId))
        {
            _logger.LogInformation("User {UserId} disconnected from NotificationHub. ConnectionId: {ConnectionId}", 
                userId, Context.ConnectionId);
        }

        if (exception != null)
        {
            _logger.LogWarning(exception, "NotificationHub disconnected with error. ConnectionId: {ConnectionId}", 
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    #region Helper Methods

    /// <summary>
    /// الحصول على اسم مجموعة المستخدم - Get user group name
    /// </summary>
    private static string GetUserGroupName(string userId) => $"user_{userId}";

    /// <summary>
    /// الحصول على اسم مجموعة الدور - Get role group name
    /// </summary>
    private static string GetRoleGroupName(string role) => $"role_{role}";

    #endregion
}

/// <summary>
/// واجهة عميل الإشعارات - Notification Hub Client Interface
/// Defines methods that can be called on clients
/// </summary>
public interface INotificationHubClient
{
    /// <summary>
    /// استلام إشعار جديد - Receive new notification
    /// </summary>
    Task ReceiveNotification(NotificationPushDto notification);

    /// <summary>
    /// تحديث عدد غير المقروءة - Update unread count
    /// </summary>
    Task UpdateUnreadCount(int count);

    /// <summary>
    /// إشعار مقروء - Notification marked as read
    /// </summary>
    Task NotificationRead(int notificationId);

    /// <summary>
    /// تحديث جماعي - Bulk update notifications
    /// </summary>
    Task RefreshNotifications();
}

/// <summary>
/// نموذج الإشعار المرسل - Notification push DTO
/// </summary>
public class NotificationPushDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
    public string Icon { get; set; } = "feather-bell";
    public string Color { get; set; } = "primary";
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public string TimeAgo { get; set; } = string.Empty;
}


