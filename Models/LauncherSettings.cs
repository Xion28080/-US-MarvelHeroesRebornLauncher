namespace MHRebornLauncher.Models;

public sealed class LauncherSettings
{
    public string SiteConfigUrl { get; set; } = "play.omeganode.org/SiteConfig.xml";
    public string NewsFeedUrl { get; set; } = "https://play.omeganode.org/launcher/news.json";
    public string EmailDomain { get; set; } = "omeganode.org";

    public bool RequireServerLogin { get; set; } = true;
    public string LoginApiUrl { get; set; } = "https://play.omeganode.org/launcher/login.php";
}
