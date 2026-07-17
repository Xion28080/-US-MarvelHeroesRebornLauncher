namespace MHRebornLauncher.Models;

public sealed class LauncherSettings
{
    public string DashboardPreviewMode { get; set; } = "live";
    public string SiteConfigUrl { get; set; } = "play.omeganode.org/SiteConfig.xml";
    public string NewsFeedUrl { get; set; } = "https://play.omeganode.org/launcher/news-feed.php";
    public string ServerStatusUrl { get; set; } = "https://play.omeganode.org/ServerStatus";
    public string EventStatusUrl { get; set; } = "https://play.omeganode.org/event-status.php";
    public string PlayerDashboardUrl { get; set; } = "https://play.omeganode.org/launcher/dashboard.php";
    public string CommunityGoalsUrl { get; set; } = "https://play.omeganode.org/community-goals.php";
    public string EmailDomain { get; set; } = "omeganode.org";

    public bool RequireServerLogin { get; set; } = true;
    public string LoginApiUrl { get; set; } = "https://play.omeganode.org/launcher/login.php";
    public string PortalTokenApiUrl { get; set; } = "https://play.omeganode.org/launcher/portal-token.php";

    public bool SkipLaunchSplash { get; set; } = false;
    public bool SkipStartupMovies { get; set; } = false;
    public bool MinimizeAfterLaunch { get; set; } = true;
    public bool RestoreAfterGameExit { get; set; } = true;
    public bool EnableDiscordRichPresence { get; set; } = true;

    public bool EnableLauncherUpdates { get; set; } = true;
    public string GitHubOwner { get; set; } = "Xion28080";
    public string GitHubRepository { get; set; } = "-US-MarvelHeroesRebornLauncher";
    public string ReleaseAssetName { get; set; } = "";
    public string ReleaseAssetPrefix { get; set; } = "MHRebornLauncher-";
}
