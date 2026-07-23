namespace MHRebornLauncher.Models;

public sealed class SocialRelationshipItem
{
    public string AccountId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public long UpdatedAtUtc { get; set; }
    public long LastSeenAtUtc { get; set; }
    public string Presence { get; set; } = "Offline";
    public int UnreadCount { get; set; }
}

public sealed class SocialFriendsResponse
{
    public bool Success { get; set; }
    public string Error { get; set; } = "";
    public bool Imported { get; set; }
    public long ImportedAtUtc { get; set; }
    public long LastAttemptAtUtc { get; set; }
    public string ImportError { get; set; } = "";
    public bool PresenceAvailable { get; set; }
    public List<SocialPendingFriendChange> PendingChanges { get; set; } = [];
    public int TotalUnreadCount { get; set; }
    public List<SocialRelationshipItem> Friends { get; set; } = [];
    public List<SocialRelationshipItem> Ignored { get; set; } = [];
}

public sealed class SocialFriendActionResponse
{
    public bool Success { get; set; }
    public string Error { get; set; } = "";
    public string CommandId { get; set; } = "";
    public string Status { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string Operation { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class SocialPendingFriendChange
{
    public string AccountId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string Operation { get; set; } = "";
    public long CreatedAtUtc { get; set; }
}
