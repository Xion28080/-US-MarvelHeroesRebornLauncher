namespace MHRebornLauncher.Models;

public sealed class LoginRequest
{
    public string EmailAddress { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class SessionRefreshRequest
{
    public string RefreshToken { get; set; } = "";
}

public sealed class LoginResponse
{
    public bool Success { get; set; }
    public string PlayerName { get; set; } = "";
    public int UserLevel { get; set; }
    public string RankLabel { get; set; } = "";
    public string RankBadge { get; set; } = "";
    public string Error { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public long AccessTokenExpiresAtUtc { get; set; }
    public string RefreshToken { get; set; } = "";
    public long RefreshTokenExpiresAtUtc { get; set; }
}
