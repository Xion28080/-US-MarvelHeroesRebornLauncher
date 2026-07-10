namespace MHRebornLauncher.Models;

public sealed class PortalTokenRequest
{
    public string EmailAddress { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class PortalTokenResponse
{
    public bool Success { get; set; }
    public string Url { get; set; } = "";
    public string Error { get; set; } = "";
}
