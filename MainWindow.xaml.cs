using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.Web.WebView2.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MHRebornLauncher.Models;
using MHRebornLauncher.Services;

namespace MHRebornLauncher;

public partial class MainWindow : Window
{
    private readonly LauncherSettings _settings;
    private readonly string _currentEmail;
    private readonly string _currentPassword;
    private readonly LoginResponse _account;
    private readonly NewsFeedService _newsFeedService = new();
    private readonly ServerStatusService _serverStatusService = new();
    private readonly EventStatusService _eventStatusService = new();
    private readonly GameLauncherService _gameLauncherService = new();
    private readonly GamePathService _gamePathService = new();
    private readonly UpdateService _updateService = new();
    private readonly PortalService _portalService = new();
    private readonly SavedLoginService _savedLoginService = new();
    private readonly SettingsService _settingsService = new();
    private readonly DispatcherTimer _serverStatusTimer;
    private readonly DispatcherTimer _eventStatusTimer;
    private List<NewsPost> _posts = [];
    private int _currentPostIndex;

    public MainWindow(LauncherSettings settings, string email, string password, LoginResponse account)
    {
        InitializeComponent();
        _settings = settings;
        _currentEmail = email;
        _currentPassword = password;
        _account = account;

        LauncherVersionText.Text = $"Launcher v{GetLauncherVersion()}";
        PlayerNameText.Text = string.IsNullOrWhiteSpace(account.PlayerName) ? email : account.PlayerName;
        ApplyRank(account.UserLevel);

        _serverStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _serverStatusTimer.Tick += async (_, _) => await RefreshServerStatusAsync();
        _eventStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _eventStatusTimer.Tick += async (_, _) => await RefreshEventStatusAsync();

        Loaded += MainWindow_Loaded;
        Closed += (_, _) =>
        {
            _serverStatusTimer.Stop();
            _eventStatusTimer.Stop();
        };
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateGameDetectionUi();
        await InitializeNewsWebViewAsync();
        await Task.WhenAll(LoadNewsAsync(), RefreshServerStatusAsync(), RefreshEventStatusAsync());
        _serverStatusTimer.Start();
        _eventStatusTimer.Start();
        _ = CheckForLauncherUpdateAsync();
    }

    private void OptionsButton_Click(object sender, RoutedEventArgs e)
    {
        var optionsWindow = new OptionsWindow(_settings, _settingsService)
        {
            Owner = this
        };

        optionsWindow.ShowDialog();
        FooterText.Text = "Options saved.";
    }

    private void ApplyRank(int userLevel)
    {
        string label;
        Color color;
        switch (userLevel)
        {
            case 2:
                label = "ADMIN";
                color = Color.FromRgb(211, 126, 45);
                break;
            case 1:
                label = "MODERATOR";
                color = Color.FromRgb(55, 166, 103);
                break;
            default:
                label = "USER";
                color = Color.FromRgb(47, 112, 181);
                break;
        }
        RankBadgeText.Text = label;
        RankBadgeBorder.Background = new SolidColorBrush(color);
    }

    private async Task RefreshServerStatusAsync()
    {
        ServerStatus? status = await _serverStatusService.GetStatusAsync(_settings.ServerStatusUrl);
        if (status == null)
        {
            ServerStatusDot.Fill = new SolidColorBrush(Color.FromRgb(224, 82, 82));
            ServerStatusText.Text = "Server Offline";
            ServerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(224, 128, 128));
            return;
        }

        int players = status.PlayerCount;
        ServerStatusDot.Fill = new SolidColorBrush(Color.FromRgb(71, 190, 104));
        ServerStatusText.Text = players == 1 ? "Server Online • 1 Player" : $"Server Online • {players} Players";
        ServerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(153, 220, 171));
    }


    private async Task RefreshEventStatusAsync()
    {
        EventStatusResponse? response = await _eventStatusService.GetStatusAsync(_settings.EventStatusUrl);
        ActiveEventsPanel.Children.Clear();

        if (response == null)
        {
            ActiveEventsPanel.Children.Add(new TextBlock
            {
                Text = "Unable to load active events.",
                Foreground = new SolidColorBrush(Color.FromRgb(138, 149, 168)),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });
            EventsUpdatedText.Text = "Unavailable";
            return;
        }

        List<LiveEvent> events = response.ActiveEvents ?? [];
        if (events.Count == 0)
        {
            ActiveEventsPanel.Children.Add(new TextBlock
            {
                Text = "No public rotating events are active right now.",
                Foreground = new SolidColorBrush(Color.FromRgb(138, 149, 168)),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });
            EventsUpdatedText.Text = "NONE ACTIVE";
            return;
        }

        foreach (LiveEvent liveEvent in events)
            ActiveEventsPanel.Children.Add(CreateEventCard(liveEvent));

        EventsUpdatedText.Text = events.Count == 1 ? "1 ACTIVE" : $"{events.Count} ACTIVE";
    }

    private static Border CreateEventCard(LiveEvent liveEvent)
    {
        Color accent = liveEvent.Type.ToLowerInvariant() switch
        {
            "special" => Color.FromRgb(34, 211, 238),
            "weekly" => Color.FromRgb(250, 204, 21),
            "daily" => Color.FromRgb(74, 222, 128),
            _ => Color.FromRgb(56, 189, 248)
        };

        StackPanel content = new();
        content.Children.Add(new TextBlock
        {
            Text = liveEvent.Type.ToUpperInvariant(),
            Foreground = new SolidColorBrush(accent),
            FontSize = 9,
            FontWeight = FontWeights.Bold
        });
        content.Children.Add(new TextBlock
        {
            Text = liveEvent.Name,
            Foreground = Brushes.White,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0)
        });

        string endsText = FormatEventEnd(liveEvent);
        if (!string.IsNullOrWhiteSpace(endsText))
        {
            content.Children.Add(new TextBlock
            {
                Text = endsText,
                Foreground = new SolidColorBrush(Color.FromRgb(151, 161, 180)),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(31, 36, 47)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(110, accent.R, accent.G, accent.B)),
            BorderThickness = new Thickness(0, 0, 0, 0),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 8),
            Child = content
        };
    }

    private static string FormatEventEnd(LiveEvent liveEvent)
    {
        if (!string.IsNullOrWhiteSpace(liveEvent.EndsAt) &&
            DateTimeOffset.TryParse(liveEvent.EndsAt, out DateTimeOffset end))
        {
            return $"Ends {end.ToLocalTime():ddd, MMM d • h:mm tt}";
        }

        return string.IsNullOrWhiteSpace(liveEvent.EndsAtHuman)
            ? "Active now"
            : $"Ends {liveEvent.EndsAtHuman}";
    }

    private async Task LoadNewsAsync()
    {
        NewsStatusText.Text = "Loading news...";
        _posts = (await _newsFeedService.GetNewsAsync(_settings.NewsFeedUrl)).Take(5).ToList();
        if (_posts.Count == 0)
        {
            _posts.Add(new NewsPost
            {
                Title = "No news available",
                Body = "There are no published updates yet.",
                Category = "Launcher"
            });
        }

        ShowNews(0);
        NewsStatusText.Text = _posts.Count == 1
            ? "Showing the latest website article"
            : $"Showing the latest {_posts.Count} website articles — use the arrows to browse";
    }

    private void ShowNews(int index)
    {
        if (_posts.Count == 0) return;

        _currentPostIndex = (index + _posts.Count) % _posts.Count;
        NewsPost post = _posts[_currentPostIndex];
        FeaturedCategoryText.Text = string.IsNullOrWhiteSpace(post.Category) ? "NEWS" : post.Category.ToUpperInvariant();
        FeaturedDateText.Text = post.Date;
        FeaturedTitleText.Text = post.Title;
        RenderNewsArticle(post);
        NewsPositionText.Text = $"{_currentPostIndex + 1} / {_posts.Count}";
    }

    private async Task InitializeNewsWebViewAsync()
    {
        try
        {
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OmegaNode",
                "MHRebornLauncher",
                "WebView2");

            Directory.CreateDirectory(userDataFolder);

            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder);

            await NewsWebView.EnsureCoreWebView2Async(environment);
            TryRemoveLegacyWebViewFolder();

            NewsWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            NewsWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            NewsWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            NewsWebView.CoreWebView2.NavigationStarting += NewsWebView_NavigationStarting;
        }
        catch (Exception ex)
        {
            FooterText.Text = "News renderer unavailable.";
            Debug.WriteLine(ex);
        }
    }


    private static void TryRemoveLegacyWebViewFolder()
    {
        try
        {
            string legacyFolder = Path.Combine(AppContext.BaseDirectory, "MHRebornLauncher.exe.WebView2");
            if (Directory.Exists(legacyFolder))
                Directory.Delete(legacyFolder, recursive: true);
        }
        catch (Exception ex)
        {
            // A stale cache should never prevent the launcher from opening.
            Debug.WriteLine(ex);
        }
    }

    private void RenderNewsArticle(NewsPost post)
    {
        if (NewsWebView.CoreWebView2 == null)
            return;

        string content = !string.IsNullOrWhiteSpace(post.BodyHtml)
            ? post.BodyHtml
            : $"<p>{HtmlEncoder.Default.Encode(!string.IsNullOrWhiteSpace(post.Body) ? post.Body : post.PreviewText).Replace("\n", "<br>")}</p>";

        NewsWebView.NavigateToString(BuildNewsHtml(content));
    }

    private static string BuildNewsHtml(string articleHtml)
    {
        return $$"""
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<meta name="color-scheme" content="dark">
<style>
html, body { margin: 0; padding: 0; background: #181c25; color: rgba(235,241,250,.92); font-family: "Segoe UI", Arial, sans-serif; font-size: 15px; line-height: 1.55; }
body { overflow-x: hidden; padding-right: 12px; }
.discord-formatted { color: rgba(235,241,250,.92); font-size: .98rem; line-height: 1.55; }
.discord-formatted p { margin: 0 0 1rem; }
.discord-formatted h1, .discord-formatted h2, .discord-formatted h3, .discord-formatted h4, .discord-formatted h5, .discord-formatted h6 { color: #f5f8ff; font-weight: 800; line-height: 1.25; margin: 1.45rem 0 .65rem; }
.discord-formatted h1:first-child, .discord-formatted h2:first-child, .discord-formatted h3:first-child, .discord-formatted h4:first-child, .discord-formatted h5:first-child, .discord-formatted h6:first-child { margin-top: 0; }
.discord-formatted h1 { font-size: 1.55rem; }
.discord-formatted h2 { font-size: 1.35rem; }
.discord-formatted h3 { font-size: 1.12rem; }
.discord-formatted h4, .discord-formatted h5, .discord-formatted h6 { font-size: 1rem; }
.discord-formatted ul, .discord-formatted ol { margin: .35rem 0 1rem 1.25rem; padding-left: 1.15rem; }
.discord-formatted ul ul, .discord-formatted ul ol, .discord-formatted ol ul, .discord-formatted ol ol { margin: .15rem 0 .2rem 1rem; }
.discord-formatted li { margin: .22rem 0; padding-left: .15rem; }
.discord-formatted strong { color: #fff; font-weight: 800; }
.discord-formatted em { color: #f3f6ff; }
.discord-formatted a { color: #38bdf8; font-weight: 700; text-decoration: underline; text-underline-offset: 3px; }
.discord-formatted a:hover { color: #7dd3fc; text-decoration-thickness: 2px; }
.discord-formatted code { padding: .12rem .36rem; border-radius: .28rem; background: rgba(2,6,14,.55); border: 1px solid rgba(139,161,190,.16); color: #f1f5ff; font-family: Consolas, Monaco, "Courier New", monospace; font-size: .92em; }
.discord-formatted pre { overflow-x: auto; margin: 1rem 0; padding: 1rem; border-radius: .75rem; background: rgba(2,6,14,.68); border: 1px solid rgba(139,161,190,.16); }
.discord-formatted pre code { display: block; padding: 0; border: 0; background: transparent; white-space: pre; }
.discord-formatted blockquote { margin: .8rem 0 1rem; padding: .25rem 0 .25rem .9rem; border-left: 4px solid rgba(139,161,190,.55); color: rgba(235,241,250,.82); }
.discord-formatted hr { height: 1px; border: 0; margin: 1.2rem 0; background: rgba(139,161,190,.18); }
.discord-timestamp, .discord-spoiler { display: inline-block; padding: .05rem .28rem; border-radius: .25rem; background: rgba(139,161,190,.16); color: #edf3ff; }
.discord-spoiler { color: transparent; cursor: pointer; }
.discord-spoiler:hover { color: #edf3ff; }
::-webkit-scrollbar { width: 11px; }
::-webkit-scrollbar-track { background: #181c25; }
::-webkit-scrollbar-thumb { background: #596170; border-radius: 8px; border: 2px solid #181c25; }
::-webkit-scrollbar-thumb:hover { background: #717b8d; }
</style>
</head>
<body><div class="discord-formatted">{{articleHtml}}</div></body>
</html>
""";
    }

    private void NewsWebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (e.Uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase) ||
            e.Uri.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
            return;

        e.Cancel = true;
        OpenUrl(e.Uri);
    }

    private void PreviousNewsButton_Click(object sender, RoutedEventArgs e) => ShowNews(_currentPostIndex - 1);
    private void NextNewsButton_Click(object sender, RoutedEventArgs e) => ShowNews(_currentPostIndex + 1);
    private async void RefreshNewsButton_Click(object sender, RoutedEventArgs e) => await LoadNewsAsync();

    private void ReadMoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (_posts.Count == 0) return;
        OpenUrl(_posts[_currentPostIndex].Url);
    }

    private void AccountButton_Click(object sender, RoutedEventArgs e) => AccountPopup.IsOpen = !AccountPopup.IsOpen;

    private async void AccountPortalButton_Click(object sender, RoutedEventArgs e)
    {
        AccountPopup.IsOpen = false;
        FooterText.Text = "Opening Account Portal...";
        PortalTokenResponse result = await _portalService.CreatePortalLoginAsync(_settings, _currentEmail, _currentPassword);
        FooterText.Text = "Ready to launch.";
        if (!result.Success || string.IsNullOrWhiteSpace(result.Url))
        {
            MessageBox.Show(result.Error.Length > 0 ? result.Error : "Unable to create a secure portal sign-in.", "Account Portal", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        OpenUrl(result.Url);
    }

    private void SignOutButton_Click(object sender, RoutedEventArgs e)
    {
        _savedLoginService.Clear();
        string? exe = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(exe))
            Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true });
        Close();
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_gamePathService.IsGameFound || string.IsNullOrWhiteSpace(_gamePathService.GameExecutablePath))
        {
            MessageBox.Show(
                "Place MHRebornLauncher.exe and MHRebornLauncher.Updater.exe together in the root Marvel Heroes folder, directly beside the UnrealEngine3 folder.",
                "Game Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _gameLauncherService.Launch(_gamePathService.GameExecutablePath, _settings, _currentEmail, _currentPassword);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Launch Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateGameDetectionUi()
    {
        bool found = _gamePathService.IsGameFound;
        GameFoundDot.Fill = new SolidColorBrush(found ? Color.FromRgb(71, 190, 104) : Color.FromRgb(224, 82, 82));
        GameFoundText.Text = found ? "Game Ready" : "Game Not Found";
        GamePlacementText.Text = "Place MHRebornLauncher.exe and MHRebornLauncher.Updater.exe together in the root Marvel Heroes folder, directly beside the UnrealEngine3 folder.";
        GamePlacementText.Visibility = found ? Visibility.Collapsed : Visibility.Visible;
        PlayButton.IsEnabled = found;
    }

    private async Task CheckForLauncherUpdateAsync()
    {
        try
        {
            LauncherUpdateInfo? updateInfo = await _updateService.CheckForUpdateAsync(_settings);
            if (updateInfo == null) return;

            MessageBoxResult result = MessageBox.Show(
                $"A launcher update is available.\n\nCurrent: {updateInfo.CurrentVersion}\nLatest: {updateInfo.LatestVersion}\n\nInstall it now?",
                "Launcher Update Available", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (result != MessageBoxResult.Yes) return;

            FooterText.Text = "Downloading launcher update...";
            Progress<double> progress = new(v => FooterText.Text = $"Downloading launcher update... {Math.Clamp((int)Math.Round(v * 100), 0, 100)}%");
            string packagePath = await _updateService.DownloadUpdateAsync(updateInfo, progress);
            FooterText.Text = "Installing launcher update...";
            _updateService.StartUpdaterAndExit(packagePath);
        }
        catch (Exception ex)
        {
            FooterText.Text = "Ready to launch.";
            MessageBox.Show("The update could not be completed.\n\n" + ex.Message, "Update Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Unable to Open Link", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private static string GetLauncherVersion()
    {
        string? version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        return (version ?? "Unknown").Split('+')[0].Trim();
    }
}
