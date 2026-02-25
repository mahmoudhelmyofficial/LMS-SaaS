using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using LMS.Services.Interfaces;

namespace LMS.Hubs;

/// <summary>
/// مركز الجلسات المباشرة - Live Class SignalR Hub
/// Manages real-time communication for live sessions
/// </summary>
[Authorize]
public class LiveClassHub : Hub
{
    private readonly ILiveSessionService _liveSessionService;
    private readonly ILogger<LiveClassHub> _logger;
    private static readonly Dictionary<string, HashSet<string>> _sessionParticipants = new();
    private static readonly object _lock = new();

    public LiveClassHub(ILiveSessionService liveSessionService, ILogger<LiveClassHub> logger)
    {
        _liveSessionService = liveSessionService;
        _logger = logger;
    }

    /// <summary>
    /// الانضمام للجلسة - Join a live session
    /// </summary>
    public async Task JoinSession(int liveClassId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId)) return;

        var groupName = $"LiveClass_{liveClassId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        lock (_lock)
        {
            if (!_sessionParticipants.ContainsKey(groupName))
                _sessionParticipants[groupName] = new HashSet<string>();
            _sessionParticipants[groupName].Add(userId);
        }

        try
        {
            var deviceType = Context.GetHttpContext()?.Request.Headers["User-Agent"].ToString().Contains("Mobile") == true ? "Mobile" : "Desktop";
            var ipAddress = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
            await _liveSessionService.RecordStudentJoinAsync(liveClassId, userId, deviceType, ipAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording join for user {UserId} in session {SessionId}", userId, liveClassId);
        }

        var count = GetParticipantCount(groupName);
        await Clients.Group(groupName).SendAsync("ParticipantCountUpdated", count);
        await Clients.Group(groupName).SendAsync("StudentJoined", userId);
    }

    /// <summary>
    /// مغادرة الجلسة - Leave a live session
    /// </summary>
    public async Task LeaveSession(int liveClassId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId)) return;

        var groupName = $"LiveClass_{liveClassId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        lock (_lock)
        {
            if (_sessionParticipants.ContainsKey(groupName))
                _sessionParticipants[groupName].Remove(userId);
        }

        try
        {
            await _liveSessionService.RecordStudentLeaveAsync(liveClassId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording leave for user {UserId} in session {SessionId}", userId, liveClassId);
        }

        var count = GetParticipantCount(groupName);
        await Clients.Group(groupName).SendAsync("ParticipantCountUpdated", count);
        await Clients.Group(groupName).SendAsync("StudentLeft", userId);
    }

    /// <summary>
    /// الحصول على عدد المشاركين - Get live participant count
    /// </summary>
    public Task<int> GetLiveParticipantCount(int liveClassId)
    {
        var groupName = $"LiveClass_{liveClassId}";
        return Task.FromResult(GetParticipantCount(groupName));
    }

    /// <summary>
    /// عند قطع الاتصال - On disconnected
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            List<string> groups;
            lock (_lock)
            {
                groups = _sessionParticipants
                    .Where(kv => kv.Value.Contains(userId))
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var group in groups)
                {
                    _sessionParticipants[group].Remove(userId);
                }
            }

            foreach (var group in groups)
            {
                var liveClassIdStr = group.Replace("LiveClass_", "");
                if (int.TryParse(liveClassIdStr, out var liveClassId))
                {
                    try
                    {
                        await _liveSessionService.RecordStudentLeaveAsync(liveClassId, userId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error recording disconnect leave for user {UserId}", userId);
                    }
                    var count = GetParticipantCount(group);
                    await Clients.Group(group).SendAsync("ParticipantCountUpdated", count);
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// الحصول على عدد المشاركين في المجموعة - Get participant count for group
    /// </summary>
    private static int GetParticipantCount(string groupName)
    {
        lock (_lock)
        {
            return _sessionParticipants.TryGetValue(groupName, out var participants) ? participants.Count : 0;
        }
    }
}
