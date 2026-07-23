namespace MHRebornLauncher.Models;

public sealed class SavedLogin
{
    public string EmailAddress { get; set; } = "";

    // Retained because Marvel Heroes itself still requires the password on its
    // command line. Web APIs no longer use this value after sign-in.
    public string Password { get; set; } = "";

    public string RefreshToken { get; set; } = "";
    public long RefreshTokenExpiresAtUtc { get; set; }
}
