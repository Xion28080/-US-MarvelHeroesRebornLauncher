using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
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
    private readonly PlayerDashboardService _playerDashboardService = new();
    private readonly SocialFriendsService _socialFriendsService = new();
    private readonly SocialPresenceService _socialPresenceService = new();
    private readonly SocialMessagesService _socialMessagesService = new();
    private readonly AuthService _authService = new();
    private readonly GameLauncherService _gameLauncherService = new();
    private readonly GamePathService _gamePathService = new();
    private readonly UpdateService _updateService = new();
    private readonly PortalService _portalService = new();
    private readonly SavedLoginService _savedLoginService = new();
    private readonly SettingsService _settingsService = new();
    private readonly DiscordRichPresenceService _discordPresenceService = new();
    private readonly MediaPlayer _dmSoundPlayer = new();
    private string? _dmSoundFilePath;
    private readonly DispatcherTimer _serverStatusTimer;
    private readonly DispatcherTimer _eventStatusTimer;
    private readonly DispatcherTimer _dashboardTimer;
    private readonly DispatcherTimer _goalCountdownTimer;
    private readonly DispatcherTimer _gameProcessTimer;
    private readonly DispatcherTimer _presenceTimer;
    private readonly DispatcherTimer _friendsTimer;
    private readonly DispatcherTimer _messagesTimer;
    private PlayerDashboardResponse? _dashboard;
    private DashboardNotice? _activeNotice;
    private FriendsWindow? _friendsWindow;
    private SocialFriendsResponse? _latestFriendsResponse;
    private readonly List<SocialDirectMessage> _messageCache = [];
    private ConversationWindow? _conversationWindow;
    private long _messageCursor = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds();
    private int _activeDashboardTab; // 0=news, 1=goals, 2=account
    private bool _updatingPreviewCombo;
    private bool _pendingRewardsPopupShown;
    private bool _pendingRewardsPopupQueued;
    private bool _gameWasRunning;
    private DateTime _gameLaunchTimeUtc = DateTime.MinValue;
    private bool _gameWindowSeen;
    private int? _launchedGameProcessId;
    private bool _serverOnline;
    private bool _shutdownPresenceSent;
    private bool _friendsRefreshRunning;
    private bool _presenceUpdateRunning;
    private bool _messageNotificationBaselineEstablished;
    private string? _activeEventName;
    private long _primaryScheduledGoalStartUtc;
    private long _upcomingGoalStartUtc;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FlashWindowInfo flashInfo);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo monitorInfo);

    private const uint MonitorDefaultToNearest = 2;
    private const uint FlashWindowTray = 0x00000002;
    private const uint FlashWindowTimerNoForeground = 0x0000000C;

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashWindowInfo
    {
        public uint Size;
        public IntPtr WindowHandle;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }
    private List<NewsPost> _posts = [];
    private int _currentPostIndex;

    public MainWindow(LauncherSettings settings, string email, string password, LoginResponse account)
    {
        // Assign constructor dependencies before InitializeComponent(). The preview
        // ComboBox raises SelectionChanged while XAML is being created, so these
        // fields must already be available when that handler runs.
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _currentEmail = email ?? string.Empty;
        _currentPassword = password ?? string.Empty;
        _account = account ?? throw new ArgumentNullException(nameof(account));

        InitializeComponent();

        LauncherVersionText.Text = $"Launcher v{GetLauncherVersion()}";
        PlayerNameText.Text = string.IsNullOrWhiteSpace(account.PlayerName) ? email : account.PlayerName;
        ApplyRank(account.UserLevel);

        _serverStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _serverStatusTimer.Tick += async (_, _) => await RefreshServerStatusAsync();
        _eventStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _eventStatusTimer.Tick += async (_, _) => await RefreshEventStatusAsync();
        _dashboardTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _dashboardTimer.Tick += async (_, _) => await RefreshDashboardAsync();
        _goalCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _goalCountdownTimer.Tick += (_, _) => UpdateCommunityGoalCountdowns();
        _gameProcessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _gameProcessTimer.Tick += (_, _) => RefreshGameProcessState();
        _presenceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Random.Shared.Next(14, 19)) };
        _presenceTimer.Tick += async (_, _) => await PresenceTimer_TickAsync();
        _friendsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _friendsTimer.Tick += async (_, _) => await FriendsTimer_TickAsync();
        _messagesTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _messagesTimer.Tick += async (_, _) => await RefreshMessagesAsync();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        Closed += (_, _) =>
        {
            _serverStatusTimer.Stop();
            _eventStatusTimer.Stop();
            _dashboardTimer.Stop();
            _goalCountdownTimer.Stop();
            _gameProcessTimer.Stop();
            _presenceTimer.Stop();
            _friendsTimer.Stop();
            _messagesTimer.Stop();
            if (_friendsWindow is not null)
            {
                _friendsWindow.Closed -= FriendsWindow_Closed;
            _friendsWindow.FriendActionRequested -= FriendsWindow_FriendActionRequested;
                _friendsWindow.Close();
                _friendsWindow = null;
            }
            if (_conversationWindow is not null)
            {
                _conversationWindow.MessageSendRequested -= ConversationWindow_MessageSendRequested;
                _conversationWindow.ConversationActivated -= ConversationWindow_ConversationActivated;
                _conversationWindow.ConversationMuteRequested -= ConversationWindow_ConversationMuteRequested;
                _conversationWindow.Close();
                _conversationWindow = null;
            }
            _dmSoundPlayer.Close();
            _discordPresenceService.Dispose();
        };
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_shutdownPresenceSent)
            return;

        _shutdownPresenceSent = true;
        _presenceTimer.Stop();
        _socialPresenceService.UpdateBeforeShutdown(_settings, _account.AccessToken);
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateGameDetectionUi();
        await InitializeNewsWebViewAsync();
        _discordPresenceService.SetEnabled(_settings.EnableDiscordRichPresence);
        await Task.WhenAll(LoadNewsAsync(), RefreshServerStatusAsync(), RefreshEventStatusAsync(), RefreshDashboardAsync(), RefreshFriendsAsync(), RefreshLauncherPresenceAsync());
        UpdateDiscordPresence();
        _serverStatusTimer.Start();
        _eventStatusTimer.Start();
        _dashboardTimer.Start();
        _goalCountdownTimer.Start();
        _gameProcessTimer.Start();
        _presenceTimer.Start();
        _friendsTimer.Start();
        _messagesTimer.Start();
        await RefreshMessagesAsync();
        _ = CheckForLauncherUpdateAsync();
    }

    private void OptionsButton_Click(object sender, RoutedEventArgs e)
    {
        var optionsWindow = new OptionsWindow(_settings, _settingsService)
        {
            Owner = this
        };

        optionsWindow.ShowDialog();
        _discordPresenceService.SetEnabled(_settings.EnableDiscordRichPresence);
        UpdateDiscordPresence();
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
            _serverOnline = false;
            UpdateDiscordPresence();
            return;
        }

        int players = status.PlayerCount;
        ServerStatusDot.Fill = new SolidColorBrush(Color.FromRgb(71, 190, 104));
        ServerStatusText.Text = players == 1 ? "Server Online • 1 Player" : $"Server Online • {players} Players";
        ServerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(153, 220, 171));
        _serverOnline = true;
        UpdateDiscordPresence();
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
            _activeEventName = null;
            UpdateDiscordPresence();
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
            _activeEventName = null;
            UpdateDiscordPresence();
            return;
        }

        foreach (LiveEvent liveEvent in events)
            ActiveEventsPanel.Children.Add(CreateEventCard(liveEvent));

        EventsUpdatedText.Text = events.Count == 1 ? "1 ACTIVE" : $"{events.Count} ACTIVE";
        _activeEventName = events[0].Name;
        UpdateDiscordPresence();
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


    private async Task RefreshDashboardAsync()
    {
        PlayerDashboardResponse response = await _playerDashboardService.GetAsync(_settings, _account.AccessToken);
        if (!response.Success)
        {
            if (_activeDashboardTab == 1)
            {
                GoalTitleText.Text = "Dashboard unavailable";
                GoalDescriptionText.Text = string.IsNullOrWhiteSpace(response.Error) ? "Unable to load Community Goals." : response.Error;
            }
            return;
        }

        _dashboard = response;
        ApplyDashboardNotice(response.Notice);
        RenderCommunityGoal(response.CommunityGoal, response.UpcomingCommunityGoal);
        RenderAccountDashboard(response);
        QueuePendingRewardsPopup(response);
        if (_activeDashboardTab == 1)
            NewsStatusText.Text = "Community Dashboard refreshes automatically every 30 seconds";
        else if (_activeDashboardTab == 2)
            NewsStatusText.Text = "Account Dashboard refreshes automatically every 30 seconds";
    }

    private async Task PresenceTimer_TickAsync()
    {
        if (_presenceUpdateRunning)
            return;

        _presenceUpdateRunning = true;
        try
        {
            await RefreshLauncherPresenceAsync();
        }
        finally
        {
            _presenceUpdateRunning = false;
            // Jitter prevents many launchers opened together from writing in lockstep.
            _presenceTimer.Interval = TimeSpan.FromSeconds(Random.Shared.Next(14, 19));
        }
    }

    private async Task FriendsTimer_TickAsync()
    {
        if (_friendsRefreshRunning)
            return;

        _friendsRefreshRunning = true;
        try
        {
            await RefreshFriendsAsync();
        }
        finally
        {
            _friendsRefreshRunning = false;
            // Poll quickly while the panel is visible, and back off when it is closed.
            int minimum = _friendsWindow is not null ? 3 : 8;
            int maximumExclusive = _friendsWindow is not null ? 5 : 11;
            _friendsTimer.Interval = TimeSpan.FromSeconds(Random.Shared.Next(minimum, maximumExclusive));
        }
    }

    private async Task RefreshLauncherPresenceAsync()
    {
        await _socialPresenceService.UpdateAsync(_settings, _account.AccessToken, true);
    }

    private async Task RefreshFriendsAsync()
    {
        SocialFriendsResponse response = await _socialFriendsService.GetAsync(_settings, _account.AccessToken);
        _latestFriendsResponse = response;
        UpdateFriendsUnreadBadge(response.Success ? response.TotalUnreadCount : 0);
        if (_conversationWindow is not null)
        {
            foreach (string accountId in _conversationWindow.OpenAccountIds.ToList())
            {
                SocialRelationshipItem? friend = (response.Friends ?? []).FirstOrDefault(item =>
                    string.Equals(item.AccountId, accountId, StringComparison.OrdinalIgnoreCase));
                _conversationWindow.UpdatePresence(accountId, friend?.Presence ?? "Offline", friend?.UnreadCount ?? 0);
            }
        }
        FriendsListPanel.Children.Clear();
        IgnoredListPanel.Children.Clear();

        if (_friendsWindow is not null)
        {
            _friendsWindow.SetMutedAccounts(GetMutedAccountIds());
            _friendsWindow.ApplySnapshot(response);
        }

        if (!response.Success)
        {
            string message = string.IsNullOrWhiteSpace(response.Error)
                ? "Unable to load your in-game friend snapshot."
                : response.Error;
            FriendsSummaryText.Text = message;
            FriendsCountText.Text = "Unavailable";
            IgnoredCountText.Text = "Unavailable";
            FriendsListPanel.Children.Add(CreateSocialEmptyCard(message));
            IgnoredListPanel.Children.Add(CreateSocialEmptyCard("Ignored players are unavailable until the next successful refresh."));
            return;
        }

        if (!response.Imported)
        {
            FriendsSummaryText.Text = "No in-game social snapshot has been imported yet. Log this account into the game once, then refresh this page.";
        }
        else if (response.ImportedAtUtc > 0)
        {
            DateTimeOffset imported = DateTimeOffset.FromUnixTimeSeconds(response.ImportedAtUtc).ToLocalTime();
            FriendsSummaryText.Text = $"Read-only in-game snapshot imported {imported:MMM d, yyyy h:mm tt}. Presence is synchronized between the launcher and game.";
        }
        else
        {
            FriendsSummaryText.Text = "Your read-only in-game social snapshot is available. Presence is synchronized between the launcher and game.";
        }

        List<SocialRelationshipItem> friends = response.Friends ?? [];
        List<SocialRelationshipItem> ignored = response.Ignored ?? [];
        FriendsCountText.Text = friends.Count == 1 ? "1 friend" : $"{friends.Count} friends";
        IgnoredCountText.Text = ignored.Count == 1 ? "1 ignored" : $"{ignored.Count} ignored";

        foreach (SocialRelationshipItem friend in friends)
            FriendsListPanel.Children.Add(CreateSocialPersonCard(friend, false));
        foreach (SocialRelationshipItem player in ignored)
            IgnoredListPanel.Children.Add(CreateSocialPersonCard(player, true));

        if (friends.Count == 0)
            FriendsListPanel.Children.Add(CreateSocialEmptyCard(response.Imported ? "No friends are currently in your in-game friend list." : "Waiting for the first in-game import."));
        if (ignored.Count == 0)
            IgnoredListPanel.Children.Add(CreateSocialEmptyCard(response.Imported ? "No players are currently ignored." : "Waiting for the first in-game import."));

        if (_activeDashboardTab == 3)
            NewsStatusText.Text = "Friend snapshots refresh automatically every 30 seconds";
    }

    private static Border CreateSocialPersonCard(SocialRelationshipItem person, bool ignored)
    {
        Grid grid = new();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        (string presenceText, Color presenceColor) = GetPresenceDisplay(person, ignored);
        System.Windows.Shapes.Ellipse dot = new()
        {
            Width = 9,
            Height = 9,
            Fill = new SolidColorBrush(presenceColor),
            Margin = new Thickness(0, 0, 11, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        grid.Children.Add(dot);

        StackPanel identity = new();
        identity.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(person.PlayerName) ? "Unknown Player" : person.PlayerName,
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13
        });
        identity.Children.Add(new TextBlock
        {
            Text = presenceText,
            Foreground = new SolidColorBrush(presenceColor),
            FontSize = 10,
            Margin = new Thickness(0, 3, 0, 0)
        });
        Grid.SetColumn(identity, 1);
        grid.Children.Add(identity);

        Border badge = new()
        {
            Background = new SolidColorBrush(ignored ? Color.FromRgb(54, 24, 30) : Color.FromRgb(34, 41, 54)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(9, 3, 9, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = ignored ? "IGNORED" : "READ ONLY",
                Foreground = new SolidColorBrush(ignored ? Color.FromRgb(248, 113, 113) : Color.FromRgb(155, 167, 186)),
                FontSize = 9,
                FontWeight = FontWeights.Bold
            }
        };
        Grid.SetColumn(badge, 2);
        grid.Children.Add(badge);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(17, 22, 30)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(42, 48, 60)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(13, 10, 13, 10),
            Margin = new Thickness(0, 0, 0, 7),
            Child = grid
        };
    }

    private static (string Text, Color Color) GetPresenceDisplay(SocialRelationshipItem person, bool ignored)
    {
        if (ignored)
            return ("Ignored in game", Color.FromRgb(239, 68, 68));

        return person.Presence switch
        {
            "InGame" => ("In Game", Color.FromRgb(56, 189, 248)),
            "LauncherOnline" => ("Online", Color.FromRgb(74, 222, 128)),
            _ => (FormatLastSeen(person.LastSeenAtUtc), Color.FromRgb(98, 107, 123))
        };
    }

    private static string FormatLastSeen(long lastSeenAtUtc)
    {
        if (lastSeenAtUtc <= 0)
            return "Offline";

        DateTimeOffset lastSeen = DateTimeOffset.FromUnixTimeSeconds(lastSeenAtUtc).ToLocalTime();
        TimeSpan elapsed = DateTimeOffset.Now - lastSeen;
        if (elapsed.TotalMinutes < 2) return "Offline • just now";
        if (elapsed.TotalHours < 1) return $"Offline • {(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalDays < 1) return $"Offline • {(int)elapsed.TotalHours}h ago";
        if (elapsed.TotalDays < 30) return $"Offline • {(int)elapsed.TotalDays}d ago";
        return $"Offline • {lastSeen:MMM d}";
    }

    private static Border CreateSocialEmptyCard(string message) => new()
    {
        Background = new SolidColorBrush(Color.FromRgb(17, 22, 30)),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(13),
        Margin = new Thickness(0, 0, 0, 7),
        Child = new TextBlock
        {
            Text = message,
            Foreground = new SolidColorBrush(Color.FromRgb(138, 149, 168)),
            TextWrapping = TextWrapping.Wrap
        }
    };

    private void QueuePendingRewardsPopup(PlayerDashboardResponse response)
    {
        if (_pendingRewardsPopupShown || _pendingRewardsPopupQueued)
            return;

        List<CommunityGoalRewardClaimDashboard> pendingClaims = (response.RewardClaims ?? [])
            .Where(claim => claim != null && string.Equals(claim.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (pendingClaims.Count == 0)
            return;

        _pendingRewardsPopupQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _pendingRewardsPopupQueued = false;
            if (_pendingRewardsPopupShown || !IsLoaded)
                return;

            _pendingRewardsPopupShown = true;
            var popup = new PendingRewardsWindow(pendingClaims)
            {
                Owner = this
            };
            popup.ShowDialog();
        }), DispatcherPriority.ApplicationIdle);
    }

    private void ApplyDashboardNotice(DashboardNotice? notice)
    {
        _activeNotice = notice;
        if (notice == null || string.IsNullOrWhiteSpace(notice.Message))
        {
            NoticeBanner.Visibility = Visibility.Collapsed;
            return;
        }

        Color accent = notice.Severity.ToLowerInvariant() switch
        {
            "critical" => Color.FromRgb(239, 68, 68),
            "warning" => Color.FromRgb(250, 204, 21),
            _ => Color.FromRgb(56, 189, 248)
        };
        NoticeBanner.BorderBrush = new SolidColorBrush(accent);
        NoticeTitleText.Foreground = new SolidColorBrush(accent);
        NoticeTitleText.Text = notice.Title;
        NoticeMessageText.Text = notice.Message;
        NoticeActionButton.Visibility = string.IsNullOrWhiteSpace(notice.Url) ? Visibility.Collapsed : Visibility.Visible;
        NoticeBanner.Visibility = Visibility.Visible;
    }

    private void RenderCommunityGoal(CommunityGoalDashboard? activeGoal, CommunityGoalDashboard? upcomingGoal)
    {
        CommunityRewardsPanel.Children.Clear();
        RankRewardsPanel.Children.Clear();
        ContributorsPanel.Children.Clear();

        _primaryScheduledGoalStartUtc = 0;
        _upcomingGoalStartUtc = upcomingGoal?.StartTimeUtc ?? 0;
        RenderUpcomingGoalCard(activeGoal is not null ? upcomingGoal : null);

        CommunityGoalDashboard? goal = activeGoal ?? upcomingGoal;
        bool isScheduled = activeGoal is null && upcomingGoal is not null;

        if (goal == null)
        {
            GoalStatusBadge.Background = new SolidColorBrush(Color.FromRgb(35, 42, 54));
            GoalStatusText.Foreground = new SolidColorBrush(Color.FromRgb(159, 169, 186));
            GoalStatusText.Text = "INACTIVE";
            GoalTitleText.Text = "No active Community Goal";
            GoalDescriptionText.Text = "There is no server-wide goal running or scheduled right now. Check back later for the next community objective.";
            GoalProgressText.Text = "0 / 0";
            GoalPercentText.Text = "0.0%";
            GoalProgressBar.Value = 0;
            GoalProgressBar.Visibility = Visibility.Visible;
            GoalEndsLabelText.Text = "ENDS";
            GoalEndsText.Text = "Not active";
            GoalPlayerContributionLabelText.Text = "YOUR CONTRIBUTION";
            GoalPlayerContributionText.Text = "No active goal";
            ContributorsHeadingText.Text = "Top Contributors";
            CommunityRewardsPanel.Children.Add(CreateMutedText("No rewards are currently configured."));
            RankRewardsPanel.Children.Add(CreateMutedText("No contributor rewards are currently configured."));
            ContributorsPanel.Children.Add(CreateMutedText("No contributors yet."));
            UpdateCommunityGoalCountdowns();
            return;
        }

        GoalTitleText.Text = goal.Name;
        GoalDescriptionText.Text = goal.Description;

        if (isScheduled)
        {
            _primaryScheduledGoalStartUtc = goal.StartTimeUtc;
            GoalStatusBadge.Background = new SolidColorBrush(Color.FromRgb(69, 56, 18));
            GoalStatusText.Foreground = new SolidColorBrush(Color.FromRgb(250, 204, 21));
            GoalStatusText.Text = "SCHEDULED";
            GoalProgressText.Text = goal.StartTimeUtc > 0
                ? DateTimeOffset.FromUnixTimeSeconds(goal.StartTimeUtc).ToLocalTime().ToString("ddd, MMM d • h:mm tt")
                : "Start time not set";
            GoalPercentText.Text = "";
            GoalProgressBar.Value = 0;
            GoalProgressBar.Visibility = Visibility.Collapsed;
            GoalEndsLabelText.Text = "STARTS";
            GoalEndsText.Text = goal.StartTimeUtc > 0
                ? DateTimeOffset.FromUnixTimeSeconds(goal.StartTimeUtc).ToLocalTime().ToString("ddd, MMM d • h:mm tt")
                : "Not set";
            GoalPlayerContributionLabelText.Text = "STARTS IN";
            GoalPlayerContributionText.Text = FormatCountdown(goal.StartTimeUtc);
            ContributorsHeadingText.Text = "Contributor Rankings";
        }
        else
        {
            GoalStatusBadge.Background = new SolidColorBrush(Color.FromRgb(21, 60, 50));
            GoalStatusText.Foreground = new SolidColorBrush(Color.FromRgb(117, 230, 173));
            GoalStatusText.Text = "ACTIVE";
            GoalProgressText.Text = $"{goal.CurrentCount:N0} / {goal.TargetCount:N0}";
            GoalPercentText.Text = $"{goal.Percent:0.0}%";
            GoalProgressBar.Value = Math.Clamp(goal.Percent, 0, 100);
            GoalProgressBar.Visibility = Visibility.Visible;
            GoalEndsLabelText.Text = "ENDS";
            GoalEndsText.Text = goal.EndTimeUtc > 0
                ? DateTimeOffset.FromUnixTimeSeconds(goal.EndTimeUtc).ToLocalTime().ToString("ddd, MMM d • h:mm tt")
                : "Not set";
            GoalPlayerContributionLabelText.Text = "YOUR CONTRIBUTION";
            GoalPlayerContributionText.Text = goal.PlayerRank > 0
                ? $"{goal.PlayerContribution:N0} • Rank #{goal.PlayerRank}"
                : goal.PlayerContribution > 0 ? $"{goal.PlayerContribution:N0} contributed" : "No contribution yet";
            ContributorsHeadingText.Text = "Top Contributors";
        }

        AddRewardItems(CommunityRewardsPanel, goal.CommunityReward, Color.FromRgb(34, 211, 238));
        if (CommunityRewardsPanel.Children.Count == 0)
            CommunityRewardsPanel.Children.Add(CreateMutedText("No community reward configured."));

        foreach (DashboardRankReward rankReward in goal.RankRewards)
        {
            Color accent = rankReward.RankStart switch
            {
                1 => Color.FromRgb(250, 204, 21),
                2 => Color.FromRgb(192, 132, 252),
                _ => Color.FromRgb(34, 211, 238)
            };
            string label = rankReward.RankStart == rankReward.RankEnd
                ? $"#{rankReward.RankStart} CONTRIBUTOR"
                : $"#{rankReward.RankStart}–#{rankReward.RankEnd} CONTRIBUTORS";
            RankRewardsPanel.Children.Add(CreateRankRewardCard(label, rankReward.Reward, accent));
        }
        if (RankRewardsPanel.Children.Count == 0)
            RankRewardsPanel.Children.Add(CreateMutedText("No contributor rewards configured."));

        if (isScheduled)
        {
            ContributorsPanel.Children.Add(CreateMutedText("Progress and contributor rankings will appear when the goal begins."));
        }
        else
        {
            foreach (DashboardContributor contributor in goal.TopContributors)
                ContributorsPanel.Children.Add(CreateContributorRow(contributor));
            if (ContributorsPanel.Children.Count == 0)
                ContributorsPanel.Children.Add(CreateMutedText("No contributors yet."));
        }

        UpdateCommunityGoalCountdowns();
    }

    private void RenderUpcomingGoalCard(CommunityGoalDashboard? goal)
    {
        if (goal is null)
        {
            UpcomingGoalCard.Visibility = Visibility.Collapsed;
            UpcomingGoalTitleText.Text = "";
            UpcomingGoalDescriptionText.Text = "";
            UpcomingGoalStartsText.Text = "";
            UpcomingGoalCountdownText.Text = "";
            return;
        }

        UpcomingGoalTitleText.Text = goal.Name;
        UpcomingGoalDescriptionText.Text = string.IsNullOrWhiteSpace(goal.Description)
            ? $"{goal.GoalType} • Target {goal.TargetCount:N0}"
            : goal.Description;
        UpcomingGoalStartsText.Text = goal.StartTimeUtc > 0
            ? DateTimeOffset.FromUnixTimeSeconds(goal.StartTimeUtc).ToLocalTime().ToString("ddd, MMM d • h:mm tt")
            : "Start time not set";
        UpcomingGoalCountdownText.Text = FormatCountdown(goal.StartTimeUtc);
        UpcomingGoalCard.Visibility = Visibility.Visible;
    }

    private void UpdateCommunityGoalCountdowns()
    {
        if (_primaryScheduledGoalStartUtc > 0)
            GoalPlayerContributionText.Text = FormatCountdown(_primaryScheduledGoalStartUtc);
        if (_upcomingGoalStartUtc > 0 && UpcomingGoalCard.Visibility == Visibility.Visible)
            UpcomingGoalCountdownText.Text = FormatCountdown(_upcomingGoalStartUtc);
    }

    private static string FormatCountdown(long startTimeUtc)
    {
        if (startTimeUtc <= 0)
            return "Start time unavailable";

        TimeSpan remaining = DateTimeOffset.FromUnixTimeSeconds(startTimeUtc) - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
            return "Starting now...";

        int days = (int)remaining.TotalDays;
        if (days > 0)
            return $"{days}d {remaining.Hours}h {remaining.Minutes}m {remaining.Seconds}s";
        if (remaining.Hours > 0)
            return $"{remaining.Hours}h {remaining.Minutes}m {remaining.Seconds}s";
        return $"{remaining.Minutes}m {remaining.Seconds}s";
    }

    private static TextBlock CreateMutedText(string text) => new()
    {
        Text = text,
        Foreground = new SolidColorBrush(Color.FromRgb(138, 149, 168)),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 4, 0, 4)
    };

    private static void AddRewardItems(Panel panel, DashboardReward reward, Color accent)
    {
        if (reward.G > 0)
        {
            panel.Children.Add(CreateRewardCategoryGroup(
                "CURRENCY",
                new[] { $"{reward.G:N0} G" },
                Color.FromRgb(250, 204, 21)));
        }

        foreach (IGrouping<string, DashboardRewardItem> categoryGroup in reward.Items
                     .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                     .GroupBy(item => NormalizeRewardCategory(item.Category), StringComparer.OrdinalIgnoreCase))
        {
            string category = categoryGroup.Key;
            Color categoryAccent = RewardCategoryAccent(category, accent);
            panel.Children.Add(CreateRewardCategoryGroup(
                category.ToUpperInvariant(),
                categoryGroup.Select(item => item.Name),
                categoryAccent));
        }
    }

    private static string NormalizeRewardCategory(string? category)
    {
        string value = (category ?? string.Empty).Trim();
        if (value.Length == 0)
            return "Other Rewards";

        return value.ToLowerInvariant() switch
        {
            "costume" or "costumes" => "Costumes",
            "character token" or "character tokens" or "hero token" or "hero tokens" => "Character Tokens",
            "team-up" or "team-ups" or "teamup" or "teamups" => "Team-Ups",
            "artifact" or "artifacts" => "Artifacts",
            "item" or "items" => "Items",
            "currency" => "Currency",
            _ => value
        };
    }

    private static Color RewardCategoryAccent(string category, Color fallback)
    {
        return category.ToLowerInvariant() switch
        {
            "costumes" => Color.FromRgb(244, 114, 182),
            "character tokens" => Color.FromRgb(34, 211, 238),
            "team-ups" => Color.FromRgb(74, 222, 128),
            "artifacts" => Color.FromRgb(251, 146, 60),
            "currency" => Color.FromRgb(250, 204, 21),
            _ => fallback
        };
    }

    private static Border CreateRewardCategoryGroup(string category, IEnumerable<string> rewardNames, Color accent)
    {
        StackPanel stack = new();
        stack.Children.Add(new TextBlock
        {
            Text = category,
            Foreground = new SolidColorBrush(accent),
            FontSize = 8,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 5)
        });

        foreach (string rewardName in rewardNames.Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            stack.Children.Add(new TextBlock
            {
                Text = rewardName,
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 220,
                Margin = new Thickness(0, 0, 0, 3)
            });
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(16, 22, 30)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(130, accent.R, accent.G, accent.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 0, 8, 8),
            Child = stack
        };
    }

    private static Border CreateRankRewardCard(string label, DashboardReward reward, Color accent)
    {
        WrapPanel items = new();
        AddRewardItems(items, reward, accent);
        StackPanel content = new();
        content.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(accent), FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 7) });
        content.Children.Add(items);
        return new Border
        {
            Width = 245,
            MinHeight = 92,
            Background = new SolidColorBrush(Color.FromRgb(17, 22, 30)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(145, accent.R, accent.G, accent.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 10, 10),
            Child = content
        };
    }

    private static Border CreateContributorRow(DashboardContributor contributor)
    {
        Color accent = contributor.Rank switch
        {
            1 => Color.FromRgb(250, 204, 21),
            2 or 3 => Color.FromRgb(192, 132, 252),
            _ => Color.FromRgb(34, 211, 238)
        };
        Grid grid = new();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = $"#{contributor.Rank}", Foreground = new SolidColorBrush(accent), FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 7, 0) });
        TextBlock name = new() { Text = contributor.PlayerName, Foreground = new SolidColorBrush(accent), FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(name, 1); grid.Children.Add(name);
        TextBlock count = new() { Text = contributor.ContributionCount.ToString("N0"), Foreground = Brushes.White, FontWeight = FontWeights.Bold };
        Grid.SetColumn(count, 2); grid.Children.Add(count);
        return new Border { Background = new SolidColorBrush(Color.FromRgb(17, 22, 30)), CornerRadius = new CornerRadius(5), Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 0, 0, 6), Child = grid };
    }

    private void NewsTabButton_Click(object sender, RoutedEventArgs e) => ShowDashboardTab(0);
    private void GoalsTabButton_Click(object sender, RoutedEventArgs e) => ShowDashboardTab(1);
    private void AccountTabButton_Click(object sender, RoutedEventArgs e) => ShowDashboardTab(2);
    private void ShowDashboardTab(int tab)
    {
        _activeDashboardTab = tab;
        NewsPanel.Visibility = tab == 0 ? Visibility.Visible : Visibility.Collapsed;
        GoalsPanel.Visibility = tab == 1 ? Visibility.Visible : Visibility.Collapsed;
        AccountPanel.Visibility = tab == 2 ? Visibility.Visible : Visibility.Collapsed;
        FriendsPanel.Visibility = Visibility.Collapsed;
        Color active = Color.FromRgb(20, 139, 235);
        Color inactive = Color.FromRgb(48, 55, 68);
        NewsTabButton.Background = new SolidColorBrush(tab == 0 ? active : inactive);
        GoalsTabButton.Background = new SolidColorBrush(tab == 1 ? active : inactive);
        AccountTabButton.Background = new SolidColorBrush(tab == 2 ? active : inactive);
        RefreshContentButton.Content = tab switch
        {
            0 => "REFRESH NEWS",
            1 => "REFRESH GOAL",
            2 => "REFRESH ACCOUNT",
            _ => "REFRESH"
        };
        NewsStatusText.Text = tab switch
        {
            0 => _posts.Count > 1
                ? $"Showing the latest {_posts.Count} website articles — use the arrows to browse"
                : "Showing the latest website article",
            1 => "Community Dashboard refreshes automatically every 30 seconds",
            2 => "Account Dashboard refreshes automatically every 30 seconds",
            _ => string.Empty
        };
    }

    private async void RefreshContentButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeDashboardTab == 0)
            await LoadNewsAsync();
        else
            await RefreshDashboardAsync();
    }

    private void NoticeActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeNotice is not null) OpenUrl(_activeNotice.Url);
    }

    private void OpenCommunityGoalsButton_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl(!string.IsNullOrWhiteSpace(_dashboard?.CommunityGoalsUrl) ? _dashboard.CommunityGoalsUrl : _settings.CommunityGoalsUrl);
    }


    private void RenderAccountDashboard(PlayerDashboardResponse response)
    {
        AccountDashboard account = response.Account ?? new AccountDashboard();
        AccountPlayerNameText.Text = string.IsNullOrWhiteSpace(account.PlayerName) ? _account.PlayerName : account.PlayerName;
        AccountEmailText.Text = string.IsNullOrWhiteSpace(account.EmailAddress) ? _currentEmail : account.EmailAddress;
        AccountRankStatusText.Text = $"{account.Rank} • {account.Status}";
        AccountTwoFactorText.Text = account.TwoFactorAvailable
            ? $"Two-Factor Authentication: {(account.TwoFactorEnabled ? "Enabled" : "Not Enabled")}" 
            : $"Two-Factor Authentication: {account.TwoFactorStatus}";
        AccountTwoFactorText.Foreground = new SolidColorBrush(account.TwoFactorEnabled ? Color.FromRgb(117, 230, 173) : Color.FromRgb(250, 204, 21));
        string recoveryLabel = !account.RecoveryEmailSet ? "Not Set" : account.RecoveryEmailVerified ? "Verified" : "Pending Verification";
        AccountRecoveryText.Text = $"Recovery Email: {recoveryLabel}" + (string.IsNullOrWhiteSpace(account.RecoveryEmailMasked) ? "" : $" • {account.RecoveryEmailMasked}");
        AccountRecoveryText.Foreground = new SolidColorBrush(account.RecoveryEmailVerified ? Color.FromRgb(117, 230, 173) : Color.FromRgb(250, 204, 21));

        AccountRewardClaimsPanel.Children.Clear();
        foreach (CommunityGoalRewardClaimDashboard claim in response.RewardClaims)
            AccountRewardClaimsPanel.Children.Add(CreateRewardClaimCard(claim));
        if (AccountRewardClaimsPanel.Children.Count == 0)
            AccountRewardClaimsPanel.Children.Add(CreateMutedText("No pending or recently delivered Community Goal rewards."));

        AccountGoalHistoryPanel.Children.Clear();
        foreach (CommunityGoalHistoryDashboard goal in response.CommunityGoalHistory)
            AccountGoalHistoryPanel.Children.Add(CreateGoalHistoryRow(goal));
        if (AccountGoalHistoryPanel.Children.Count == 0)
            AccountGoalHistoryPanel.Children.Add(CreateMutedText("No completed Community Goals are available yet."));

        PreviewModeBorder.Visibility = account.IsAdministrator && account.PreviewEnabled ? Visibility.Visible : Visibility.Collapsed;
        if (PreviewModeBorder.Visibility == Visibility.Visible)
        {
            _updatingPreviewCombo = true;
            foreach (object item in DashboardPreviewCombo.Items)
            {
                if (item is ComboBoxItem combo && string.Equals(combo.Tag?.ToString(), response.PreviewMode, StringComparison.OrdinalIgnoreCase))
                {
                    DashboardPreviewCombo.SelectedItem = combo;
                    break;
                }
            }
            _updatingPreviewCombo = false;
        }
    }

    private static Border CreateRewardClaimCard(CommunityGoalRewardClaimDashboard claim)
    {
        bool pending = claim.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase);
        Color accent = pending ? Color.FromRgb(250, 204, 21) : Color.FromRgb(117, 230, 173);
        WrapPanel rewards = new();
        AddRewardItems(rewards, claim.Reward, accent);
        StackPanel content = new();
        content.Children.Add(new TextBlock { Text = $"{claim.GoalName} • {claim.RewardSource}", FontWeight = FontWeights.Bold, FontSize = 14 });
        content.Children.Add(new TextBlock
        {
            Text = pending ? "PENDING — Enter the game to receive this reward." : "DELIVERED",
            Foreground = new SolidColorBrush(accent), FontWeight = FontWeights.Bold, FontSize = 10, Margin = new Thickness(0, 4, 0, 8)
        });
        content.Children.Add(rewards);
        return new Border { Background = new SolidColorBrush(Color.FromRgb(17, 22, 30)), BorderBrush = new SolidColorBrush(accent), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(13), Margin = new Thickness(0, 0, 0, 8), Child = content };
    }

    private static Border CreateGoalHistoryRow(CommunityGoalHistoryDashboard goal)
    {
        Grid grid = new();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        StackPanel left = new();
        left.Children.Add(new TextBlock { Text = goal.Name, FontWeight = FontWeights.Bold });
        string detail = $"{goal.FinalCount:N0} / {goal.TargetCount:N0}";
        if (goal.PlayerContribution > 0) detail += $" • You: {goal.PlayerContribution:N0}";
        if (goal.PlayerRank > 0) detail += $" • Rank #{goal.PlayerRank}";
        left.Children.Add(new TextBlock { Text = detail, Foreground = new SolidColorBrush(Color.FromRgb(170, 180, 197)), FontSize = 11, Margin = new Thickness(0, 3, 0, 0) });
        grid.Children.Add(left);
        TextBlock status = new() { Text = goal.Status.ToUpperInvariant(), Foreground = new SolidColorBrush(Color.FromRgb(117, 230, 173)), FontSize = 10, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(status, 1); grid.Children.Add(status);
        return new Border { Background = new SolidColorBrush(Color.FromRgb(17, 22, 30)), CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 9, 12, 9), Margin = new Thickness(0, 0, 0, 7), Child = grid };
    }

    private async void DashboardPreviewCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged fires once while InitializeComponent builds the ComboBox.
        // Ignore that initialization event; only react after the window is loaded.
        if (!IsLoaded || _updatingPreviewCombo || sender is not ComboBox comboBox ||
            comboBox.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        _settings.DashboardPreviewMode = item.Tag?.ToString() ?? "live";
        _settingsService.Save(_settings);
        await RefreshDashboardAsync();
    }

    private async void OpenAccountPortalButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            PortalTokenResponse result = await _portalService.CreatePortalLoginAsync(_settings, _account.AccessToken);
            if (result.Success && !string.IsNullOrWhiteSpace(result.Url)) OpenUrl(result.Url);
            else FooterText.Text = string.IsNullOrWhiteSpace(result.Error) ? "Unable to open Account Portal." : result.Error;
        }
        catch (Exception ex)
        {
            FooterText.Text = $"Unable to open Account Portal: {ex.Message}";
        }
    }

    private void RefreshGameProcessState()
    {
        bool running = IsGameRunning();
        if (running)
        {
            PlayButton.Content = "GAME RUNNING";
            PlayButton.IsEnabled = false;
            GameFoundText.Text = "Game Running";
            UpdateDiscordPresence(gameRunningOverride: true);
        }
        else
        {
            if (_gameWasRunning && _settings.RestoreAfterGameExit && WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
                Activate();
            }

            _gameWasRunning = false;
            _gameLaunchTimeUtc = DateTime.MinValue;
            _gameWindowSeen = false;
            _launchedGameProcessId = null;

            // Explicitly restore every game-state UI value. UpdateGameDetectionUi()
            // previously re-enabled the button but left its old GAME RUNNING label behind.
            UpdateGameDetectionUi();
            UpdateDiscordPresence(gameRunningOverride: false);
        }
    }

    private void UpdateDiscordPresence(bool? gameRunningOverride = null)
    {
        bool gameRunning = gameRunningOverride ?? _gameWasRunning;
        _discordPresenceService.Update(gameRunning, _serverOnline, _activeEventName);
    }

    private bool IsGameRunning()
    {
        string? gamePath = _gamePathService.GameExecutablePath;
        if (string.IsNullOrWhiteSpace(gamePath))
            return false;

        string expectedPath;
        try
        {
            expectedPath = Path.GetFullPath(gamePath);
        }
        catch
        {
            return false;
        }

        bool startupGracePeriod = _gameLaunchTimeUtc != DateTime.MinValue
            && DateTime.UtcNow - _gameLaunchTimeUtc < TimeSpan.FromSeconds(45);

        // The executable initially started by the launcher may hand the actual game
        // window to another process. Scan every matching executable and look for a
        // real, user-visible game window instead of trusting the original PID.
        string processName = Path.GetFileNameWithoutExtension(expectedPath);
        bool matchingProcessExists = false;

        try
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    process.Refresh();
                    if (process.HasExited)
                        continue;

                    string? processPath = null;
                    try
                    {
                        processPath = process.MainModule?.FileName;
                    }
                    catch
                    {
                        // If Windows denies MainModule access, only trust the exact PID
                        // created by this launcher session.
                        if (_launchedGameProcessId != process.Id)
                            continue;
                    }

                    if (!string.IsNullOrWhiteSpace(processPath))
                    {
                        string fullProcessPath;
                        try
                        {
                            fullProcessPath = Path.GetFullPath(processPath);
                        }
                        catch
                        {
                            continue;
                        }

                        if (!string.Equals(fullProcessPath, expectedPath, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    matchingProcessExists = true;

                    if (IsUsableGameWindow(process.MainWindowHandle))
                    {
                        _gameWindowSeen = true;
                        return true;
                    }
                }
            }
        }
        catch
        {
            // Fall through to the session-state checks below.
        }

        // Once a real Marvel Heroes window has appeared, its disappearance means the
        // playable session is over. Ignore any headless process left behind afterward.
        if (_gameWindowSeen)
            return false;

        // During startup, keep the button locked while the game creates its real window.
        if (startupGracePeriod && matchingProcessExists)
            return true;

        return false;
    }

    private static bool IsUsableGameWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero || !IsWindowVisible(handle))
            return false;

        // A minimized game is still running even though Windows moves its rectangle
        // off-screen, so accept iconic windows before checking dimensions.
        if (IsIconic(handle))
            return true;

        int titleLength = GetWindowTextLength(handle);
        if (titleLength <= 0)
            return false;

        var title = new StringBuilder(titleLength + 1);
        _ = GetWindowText(handle, title, title.Capacity);
        if (string.IsNullOrWhiteSpace(title.ToString()))
            return false;

        if (!GetWindowRect(handle, out NativeRect rect))
            return false;

        int width = Math.Abs(rect.Right - rect.Left);
        int height = Math.Abs(rect.Bottom - rect.Top);

        // Reject tiny or hidden helper windows that can linger after the game closes.
        return width >= 320 && height >= 240;
    }

    private void AccountButton_Click(object sender, RoutedEventArgs e) => AccountPopup.IsOpen = !AccountPopup.IsOpen;

    private async void AccountPortalButton_Click(object sender, RoutedEventArgs e)
    {
        AccountPopup.IsOpen = false;
        FooterText.Text = "Opening Account Portal...";
        PortalTokenResponse result = await _portalService.CreatePortalLoginAsync(_settings, _account.AccessToken);
        FooterText.Text = "Ready to launch.";
        if (!result.Success || string.IsNullOrWhiteSpace(result.Url))
        {
            MessageBox.Show(result.Error.Length > 0 ? result.Error : "Unable to create a secure portal sign-in.", "Account Portal", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        OpenUrl(result.Url);
    }

    private async void SignOutButton_Click(object sender, RoutedEventArgs e)
    {
        await _socialPresenceService.UpdateAsync(_settings, _account.AccessToken, false);
        await _authService.RevokeAsync(_settings, _account.AccessToken);
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
            _gameLaunchTimeUtc = DateTime.UtcNow;
            _gameWindowSeen = false;
            Process launchedProcess = _gameLauncherService.Launch(_gamePathService.GameExecutablePath, _settings, _currentEmail, _currentPassword);
            _launchedGameProcessId = launchedProcess.Id;
            launchedProcess.Dispose();
            _gameWasRunning = true;
            RefreshGameProcessState();
            if (_settings.MinimizeAfterLaunch)
                WindowState = WindowState.Minimized;
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
        PlayButton.Content = "PLAY";
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


    private void FriendsDrawerButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleFriendsWindow();
    }

    private void ToggleFriendsWindow()
    {
        if (_friendsWindow is not null)
        {
            _friendsWindow.Close();
            return;
        }

        _friendsWindow = new FriendsWindow
        {
            Owner = this
        };
        _friendsWindow.Closed += FriendsWindow_Closed;
        _friendsWindow.FriendActionRequested += FriendsWindow_FriendActionRequested;
        _friendsWindow.SetMutedAccounts(GetMutedAccountIds());
        _friendsWindow.ShowLoadingState();
        _friendsWindow.Show();
        DockFriendsWindow();
        UpdateFriendsButtonState();
        _friendsTimer.Interval = TimeSpan.FromSeconds(3);

        if (_latestFriendsResponse is not null)
            _friendsWindow.ApplySnapshot(_latestFriendsResponse);
        else
            _ = RefreshFriendsAsync();
    }


    private async void FriendsWindow_FriendActionRequested(object? sender, FriendActionRequestedEventArgs e)
    {
        if (e.Operation.Equals("message", StringComparison.OrdinalIgnoreCase))
        {
            OpenConversation(e.AccountId, e.PlayerName);
            return;
        }

        if (e.Operation is "mute" or "unmute")
        {
            SetConversationMuted(e.AccountId, e.Operation == "mute");
            return;
        }

        if (_friendsWindow is null) return;
        string verb = e.Operation.Equals("remove", StringComparison.OrdinalIgnoreCase) ? "Removing" : "Adding";
        _friendsWindow.SetActionState(true, $"{verb} {e.PlayerName}...");

        SocialFriendActionResponse submitted = await _socialFriendsService.SubmitActionAsync(
            _settings, _account.AccessToken, e.PlayerName, e.Operation);
        if (!submitted.Success || string.IsNullOrWhiteSpace(submitted.CommandId))
        {
            _friendsWindow?.SetActionState(false, string.IsNullOrWhiteSpace(submitted.Error) ? "Friend change failed." : submitted.Error);
            return;
        }

        if (submitted.Status.Equals("QueuedOffline", StringComparison.OrdinalIgnoreCase))
        {
            _friendsWindow.ClearAddFriendName();
            _friendsWindow.SetActionState(false, string.IsNullOrWhiteSpace(submitted.Message)
                ? "Queued safely. This change will apply the next time you enter the game."
                : submitted.Message);
            await RefreshFriendsAsync();
            return;
        }

        SocialFriendActionResponse status = submitted;
        for (int attempt = 0; attempt < 15; attempt++)
        {
            await Task.Delay(1000);
            status = await _socialFriendsService.GetActionStatusAsync(_settings, _account.AccessToken, submitted.CommandId);
            if (!status.Success) continue;
            if (status.Status is "succeeded" or "failed" or "expired") break;
        }

        if (_friendsWindow is null) return;
        if (status.Success && status.Status == "succeeded")
        {
            _friendsWindow.ClearAddFriendName();
            _friendsWindow.SetActionState(false, string.IsNullOrWhiteSpace(status.Message) ? "Friend list updated." : status.Message);
            await Task.Delay(1200);
            await RefreshFriendsAsync();
        }
        else
        {
            string error = !string.IsNullOrWhiteSpace(status.Message) ? status.Message
                : !string.IsNullOrWhiteSpace(status.Error) ? status.Error
                : "The game server did not complete the friend change in time.";
            _friendsWindow.SetActionState(false, error);
        }
    }

    private async Task RefreshMessagesAsync()
    {
        SocialMessagesResponse response = await _socialMessagesService.GetAsync(_settings, _account.AccessToken, _messageCursor);
        if (!response.Success)
            return;

        bool shouldNotify = false;
        foreach (SocialDirectMessage message in response.Messages ?? [])
        {
            if (_messageCache.Any(existing => existing.MessageId == message.MessageId))
                continue;

            _messageCache.Add(message);
            bool matchingConversationFocused = _conversationWindow?.IsConversationActiveAndFocused(message.OtherAccountId) == true;
            if (_conversationWindow?.ContainsConversation(message.OtherAccountId) == true)
            {
                _conversationWindow.AddMessages([message]);
                if (!message.IsOutgoing && matchingConversationFocused)
                    _ = MarkConversationReadAsync(message.OtherAccountId);
            }

            if (_messageNotificationBaselineEstablished && !message.IsOutgoing && !matchingConversationFocused
                && !IsConversationMuted(message.OtherAccountId))
                shouldNotify = true;
        }

        if (response.Cursor > _messageCursor)
            _messageCursor = response.Cursor;

        if (!_messageNotificationBaselineEstablished)
            _messageNotificationBaselineEstablished = true;
        else if (shouldNotify && !IsGameRunning())
        {
            if (_settings.EnableDmTaskbarFlash)
                FlashLauncherTaskbar();
            if (_settings.EnableDmNotificationSound)
                PlayDmNotificationSound();
        }
    }

    private void FlashLauncherTaskbar()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
            return;

        FlashWindowInfo info = new()
        {
            Size = (uint)Marshal.SizeOf<FlashWindowInfo>(),
            WindowHandle = handle,
            Flags = FlashWindowTray | FlashWindowTimerNoForeground,
            Count = 3,
            Timeout = 0
        };
        _ = FlashWindowEx(ref info);
    }

    private void OpenConversation(string accountId, string playerName)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            return;

        if (_conversationWindow is null)
        {
            _conversationWindow = new ConversationWindow { Owner = this };
            _conversationWindow.MessageSendRequested += ConversationWindow_MessageSendRequested;
            _conversationWindow.ConversationActivated += ConversationWindow_ConversationActivated;
            _conversationWindow.ConversationMuteRequested += ConversationWindow_ConversationMuteRequested;
            _conversationWindow.Closed += ConversationWindow_Closed;
            _conversationWindow.Show();
        }
        else if (!_conversationWindow.IsVisible)
        {
            _conversationWindow.Show();
        }

        SocialRelationshipItem? friend = (_latestFriendsResponse?.Friends ?? []).FirstOrDefault(item =>
            string.Equals(item.AccountId, accountId, StringComparison.OrdinalIgnoreCase));
        _conversationWindow.SetMutedAccounts(GetMutedAccountIds());
        _conversationWindow.OpenConversation(
            accountId,
            playerName,
            friend?.Presence ?? "Offline",
            friend?.UnreadCount ?? 0,
            _messageCache.Where(message => string.Equals(message.OtherAccountId, accountId, StringComparison.OrdinalIgnoreCase)));
        _conversationWindow.Activate();
        _ = MarkConversationReadAsync(accountId);
        _ = RefreshMessagesAsync();
    }

    private void ConversationWindow_ConversationActivated(object? sender, ConversationActivatedEventArgs e)
    {
        _ = MarkConversationReadAsync(e.AccountId);
    }

    private void ConversationWindow_Closed(object? sender, EventArgs e)
    {
        if (_conversationWindow is null)
            return;
        _conversationWindow.MessageSendRequested -= ConversationWindow_MessageSendRequested;
        _conversationWindow.ConversationActivated -= ConversationWindow_ConversationActivated;
        _conversationWindow.ConversationMuteRequested -= ConversationWindow_ConversationMuteRequested;
        _conversationWindow.Closed -= ConversationWindow_Closed;
        _conversationWindow = null;
    }

    private async Task MarkConversationReadAsync(string otherAccountId)
    {
        if (string.IsNullOrWhiteSpace(otherAccountId))
            return;

        if (await _socialMessagesService.MarkReadAsync(_settings, _account.AccessToken, otherAccountId))
            await RefreshFriendsAsync();
    }

    private async void ConversationWindow_MessageSendRequested(object? sender, ConversationMessageSendEventArgs e)
    {
        if (sender is not ConversationWindow window)
            return;

        window.SetSendState(true, "Sending message...");
        SocialMessageSendResponse submitted = await _socialMessagesService.SendAsync(
            _settings, _account.AccessToken, e.AccountId, e.PlayerName, e.MessageBody);

        if (!submitted.Success)
        {
            string error = string.IsNullOrWhiteSpace(submitted.Error) ? "Unable to send the message." : submitted.Error;
            window.SetSendState(false, error);
            MessageBox.Show(this, error, "Message Not Sent", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        window.ClearMessageInput();
        if (submitted.Status is "delivered" or "queued")
        {
            window.SetSendState(false, submitted.Message);
            await Task.Delay(250);
            await RefreshMessagesAsync();
            return;
        }

        SocialMessageSendResponse status = submitted;
        if (!string.IsNullOrWhiteSpace(submitted.CommandId))
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                await Task.Delay(500);
                status = await _socialMessagesService.GetSendStatusAsync(
                    _settings, _account.AccessToken, submitted.CommandId);
                if (status.Success && status.Status is "succeeded" or "failed" or "expired")
                    break;
            }
        }

        if (status.Success && string.Equals(status.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
        {
            window.SetSendState(false, string.IsNullOrWhiteSpace(status.Message) ? "Message delivered." : status.Message);
            await Task.Delay(250);
            await RefreshMessagesAsync();
        }
        else
        {
            string error = !string.IsNullOrWhiteSpace(status.Message) ? status.Message
                : !string.IsNullOrWhiteSpace(status.Error) ? status.Error
                : "The game server did not deliver the message in time.";
            window.SetSendState(false, error);
            MessageBox.Show(this, error, "Message Not Sent", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }


    private void ConversationWindow_ConversationMuteRequested(object? sender, ConversationMuteRequestedEventArgs e)
    {
        SetConversationMuted(e.AccountId, e.Mute);
    }

    private HashSet<string> GetMutedAccountIds()
    {
        _settings.MutedConversationAccountIds ??= [];
        return new HashSet<string>(_settings.MutedConversationAccountIds, StringComparer.OrdinalIgnoreCase);
    }

    private bool IsConversationMuted(string accountId) => GetMutedAccountIds().Contains(accountId);

    private void SetConversationMuted(string accountId, bool muted)
    {
        if (string.IsNullOrWhiteSpace(accountId)) return;
        HashSet<string> mutedIds = GetMutedAccountIds();
        if (muted) mutedIds.Add(accountId); else mutedIds.Remove(accountId);
        _settings.MutedConversationAccountIds = mutedIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();
        _settingsService.Save(_settings);
        _friendsWindow?.SetMutedAccounts(mutedIds);
        _conversationWindow?.SetMutedAccounts(mutedIds);
        if (_latestFriendsResponse is not null && _friendsWindow is not null)
            _friendsWindow.ApplySnapshot(_latestFriendsResponse);
    }

    private void InitializeDmNotificationSound()
    {
        try
        {
            var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/JarvisMessage.wav"));
            if (resource?.Stream is null)
                return;

            string soundDirectory = Path.Combine(LauncherPaths.DataRoot, "Sounds");
            Directory.CreateDirectory(soundDirectory);
            _dmSoundFilePath = Path.Combine(soundDirectory, "JarvisMessage.wav");

            using (resource.Stream)
            using (FileStream output = new(_dmSoundFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                resource.Stream.CopyTo(output);

            _dmSoundPlayer.Open(new Uri(_dmSoundFilePath, UriKind.Absolute));
        }
        catch (Exception ex)
        {
            LogService.Error("Unable to initialize the DM notification sound", ex);
        }
    }

    private void PlayDmNotificationSound()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_dmSoundFilePath))
                InitializeDmNotificationSound();

            if (string.IsNullOrWhiteSpace(_dmSoundFilePath))
                return;

            _dmSoundPlayer.Stop();
            _dmSoundPlayer.Position = TimeSpan.Zero;
            _dmSoundPlayer.Volume = Math.Clamp(_settings.DmNotificationVolumePercent, 0, 100) / 100.0;
            _dmSoundPlayer.Play();
        }
        catch (Exception ex)
        {
            LogService.Error("Unable to play the DM notification sound", ex);
        }
    }

    private void FriendsWindow_Closed(object? sender, EventArgs e)
    {
        if (_friendsWindow is not null)
        {
            _friendsWindow.Closed -= FriendsWindow_Closed;
            _friendsWindow.FriendActionRequested -= FriendsWindow_FriendActionRequested;
        }
        _friendsWindow = null;
        UpdateFriendsButtonState();
        _friendsTimer.Interval = TimeSpan.FromSeconds(Random.Shared.Next(8, 11));
    }

    private void UpdateFriendsUnreadBadge(int unreadCount)
    {
        if (FriendsUnreadBadge is null || FriendsUnreadBadgeText is null)
            return;

        int safeCount = Math.Max(0, unreadCount);
        FriendsUnreadBadge.Visibility = safeCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        FriendsUnreadBadgeText.Text = safeCount > 99 ? "99+" : safeCount.ToString();

        if (FriendsDrawerButton?.ToolTip is ToolTip toolTip)
        {
            toolTip.Content = new TextBlock
            {
                Text = safeCount > 0
                    ? $"Friends • {safeCount} unread message{(safeCount == 1 ? string.Empty : "s")}"
                    : "Friends",
                Foreground = new SolidColorBrush(Color.FromRgb(232, 238, 247))
            };
        }
    }

    private void UpdateFriendsButtonState()
    {
        if (FriendsDrawerButton is null || FriendsDrawerChevronText is null)
            return;

        bool open = _friendsWindow is not null;
        FriendsDrawerChevronText.Text = open ? "‹" : "›";
        FriendsDrawerButton.Background = new SolidColorBrush(open ? Color.FromRgb(20, 139, 235) : Colors.Transparent);
        FriendsDrawerButton.BorderBrush = new SolidColorBrush(open ? Color.FromRgb(20, 139, 235) : Color.FromRgb(58, 67, 82));
    }

    private void DockFriendsWindow()
    {
        if (_friendsWindow is null)
            return;

        Rect workArea = GetCurrentMonitorWorkArea();
        double screenMargin = 8;
        double gap = 10;
        double dockWidth = 360;

        // Match the Friends panel to the full visible height of the main launcher.
        // Clamp to the monitor work area so the panel remains fully reachable.
        double availableHeight = workArea.Bottom - workArea.Top - (screenMargin * 2);
        double height = Math.Max(420, Math.Min(ActualHeight, availableHeight));

        double top = WindowState == WindowState.Maximized ? workArea.Top + screenMargin : Top;
        top = Math.Max(workArea.Top + screenMargin, Math.Min(top, workArea.Bottom - height - screenMargin));

        double preferredRightDockLeft = Left + ActualWidth + gap;
        double left;

        bool hasRoomOnRight = preferredRightDockLeft + dockWidth <= workArea.Right - screenMargin;
        if (hasRoomOnRight && WindowState != WindowState.Maximized)
        {
            left = preferredRightDockLeft;
        }
        else
        {
            left = workArea.Right - dockWidth - screenMargin;
        }

        left = Math.Max(workArea.Left + screenMargin, Math.Min(left, workArea.Right - dockWidth - screenMargin));

        _friendsWindow.Width = dockWidth;
        _friendsWindow.Height = height;
        _friendsWindow.Left = left;
        _friendsWindow.Top = top;
    }

    private Rect GetCurrentMonitorWorkArea()
    {
        IntPtr windowHandle = new WindowInteropHelper(this).Handle;
        IntPtr monitorHandle = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);

        if (monitorHandle == IntPtr.Zero)
            return SystemParameters.WorkArea;

        MonitorInfo monitorInfo = new()
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };

        if (!GetMonitorInfo(monitorHandle, ref monitorInfo))
            return SystemParameters.WorkArea;

        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        double scaleX = dpi.DpiScaleX <= 0 ? 1 : dpi.DpiScaleX;
        double scaleY = dpi.DpiScaleY <= 0 ? 1 : dpi.DpiScaleY;

        return new Rect(
            monitorInfo.WorkArea.Left / scaleX,
            monitorInfo.WorkArea.Top / scaleY,
            (monitorInfo.WorkArea.Right - monitorInfo.WorkArea.Left) / scaleX,
            (monitorInfo.WorkArea.Bottom - monitorInfo.WorkArea.Top) / scaleY);
    }

    private void Window_PositionChanged(object? sender, EventArgs e)
    {
        DockFriendsWindow();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DockFriendsWindow();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left || IsInsideButton(e.OriginalSource as DependencyObject))
            return;

        if (e.ClickCount == 2 && ResizeMode != ResizeMode.NoResize)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            DragMove();
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Button)
                return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private void MinimizeWindowButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeWindowButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseWindowButton_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (MaximizeWindowButton is not null)
            MaximizeWindowButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";

        if (_friendsWindow is null)
            return;

        if (WindowState == WindowState.Minimized)
        {
            _friendsWindow.Hide();
        }
        else
        {
            if (!_friendsWindow.IsVisible)
                _friendsWindow.Show();
            DockFriendsWindow();
        }
    }

}
