namespace MHRebornLauncher.Models;

public sealed class SocialDirectMessage
{
    public string MessageId { get; set; } = "";
    public string SenderAccountId { get; set; } = "";
    public string SenderPlayerName { get; set; } = "";
    public string RecipientAccountId { get; set; } = "";
    public string RecipientPlayerName { get; set; } = "";
    public string OtherAccountId { get; set; } = "";
    public string OtherPlayerName { get; set; } = "";
    public bool IsOutgoing { get; set; }
    public bool IsRead { get; set; }
    public string MessageBody { get; set; } = "";
    public long CreatedAtUtc { get; set; }
}

public sealed class SocialMessagesResponse
{
    public bool Success { get; set; }
    public string Error { get; set; } = "";
    public long Cursor { get; set; }
    public List<SocialDirectMessage> Messages { get; set; } = [];
}

public sealed class SocialMessageSendResponse
{
    public bool Success { get; set; }
    public string Error { get; set; } = "";
    public string CommandId { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
}
