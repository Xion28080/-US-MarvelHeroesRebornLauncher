namespace MHRebornLauncher.Models;

public sealed class LoginRequest
{
    public string EmailAddress { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class LoginResponse
{
    public bool Success { get; set; }
    public string PlayerName { get; set; } = "";
    public int UserLevel { get; set; }
    public string Error { get; set; } = "";
}
