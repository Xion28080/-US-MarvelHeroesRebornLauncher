using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MHRebornLauncher.Models;

namespace MHRebornLauncher;

public partial class FriendsWindow : Window
{
    private List<SocialRelationshipItem> _latestFriends = [];
    private HashSet<string> _mutedAccountIds = new(StringComparer.OrdinalIgnoreCase);
    public event EventHandler<FriendActionRequestedEventArgs>? FriendActionRequested;

    public void SetMutedAccounts(IEnumerable<string> accountIds)
    {
        _mutedAccountIds = new HashSet<string>(accountIds ?? [], StringComparer.OrdinalIgnoreCase);
    }
    public FriendsWindow()
    {
        InitializeComponent();
    }

    public void ApplySnapshot(SocialFriendsResponse response)
    {
        PendingChangesListPanel.Children.Clear();
        PendingChangesHeader.Visibility = Visibility.Collapsed;
        OnlineFriendsListPanel.Children.Clear();
        OfflineFriendsListPanel.Children.Clear();
        IgnoredListPanel.Children.Clear();

        if (!response.Success)
        {
            string message = string.IsNullOrWhiteSpace(response.Error)
                ? "Unable to load your in-game friend snapshot."
                : response.Error;
            FriendsSummaryText.Text = message;
            FriendsCountText.Text = "Unavailable";
            IgnoredCountText.Text = "Unavailable";
            PendingChangesHeader.Visibility = Visibility.Collapsed;
            OnlineFriendsListPanel.Children.Add(CreateSocialEmptyCard(message));
            IgnoredListPanel.Children.Add(CreateSocialEmptyCard("Ignored players are unavailable until the next successful refresh."));
            return;
        }

        if (!response.Imported)
        {
            FriendsSummaryText.Text = "No in-game social snapshot has been imported yet. Log this account into the game once, then refresh this panel.";
        }
        else if (response.ImportedAtUtc > 0)
        {
            DateTimeOffset imported = DateTimeOffset.FromUnixTimeSeconds(response.ImportedAtUtc).ToLocalTime();
            FriendsSummaryText.Text = $"In-game social snapshot imported {imported:MMM d, yyyy h:mm tt}. Presence is synchronized between the launcher and game.";
        }
        else
        {
            FriendsSummaryText.Text = "Your in-game social snapshot is available. Presence is synchronized between the launcher and game.";
        }

        List<SocialPendingFriendChange> pendingChanges = (response.PendingChanges ?? [])
            .OrderBy(change => change.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        PendingChangesHeader.Visibility = pendingChanges.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        PendingChangesCountText.Text = pendingChanges.Count.ToString();
        foreach (SocialPendingFriendChange change in pendingChanges)
            PendingChangesListPanel.Children.Add(CreatePendingChangeCard(change));

        List<SocialRelationshipItem> friends = (response.Friends ?? []).ToList();
        _latestFriends = friends;
        List<SocialRelationshipItem> ignored = (response.Ignored ?? [])
            .OrderBy(player => player.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Keep presence groups stable, but surface unread conversations first inside
        // each group. Friends without unread messages remain alphabetical.
        List<SocialRelationshipItem> onlineFriends = SortFriendGroup(friends
            .Where(friend => friend.Presence is "InGame" or "LauncherOnline"));
        List<SocialRelationshipItem> offlineFriends = SortFriendGroup(friends
            .Where(friend => friend.Presence is not ("InGame" or "LauncherOnline")));

        FriendsCountText.Text = response.TotalUnreadCount > 0
            ? $"{friends.Count} friends • {response.TotalUnreadCount} unread"
            : (friends.Count == 1 ? "1 friend" : $"{friends.Count} friends");
        OnlineFriendsCountText.Text = onlineFriends.Count.ToString();
        OfflineFriendsCountText.Text = offlineFriends.Count.ToString();
        IgnoredCountText.Text = ignored.Count == 1 ? "1 ignored" : $"{ignored.Count} ignored";

        foreach (SocialRelationshipItem friend in onlineFriends)
            OnlineFriendsListPanel.Children.Add(CreateSocialPersonCard(friend, false));
        foreach (SocialRelationshipItem friend in offlineFriends)
            OfflineFriendsListPanel.Children.Add(CreateSocialPersonCard(friend, false));
        foreach (SocialRelationshipItem player in ignored)
            IgnoredListPanel.Children.Add(CreateSocialPersonCard(player, true));

        if (onlineFriends.Count == 0)
            OnlineFriendsListPanel.Children.Add(CreateSocialEmptyCard(response.Imported ? "No friends are currently online." : "Waiting for the first in-game import."));
        if (offlineFriends.Count == 0)
            OfflineFriendsListPanel.Children.Add(CreateSocialEmptyCard(response.Imported ? "No friends are currently offline." : "Waiting for the first in-game import."));
        if (ignored.Count == 0)
            IgnoredListPanel.Children.Add(CreateSocialEmptyCard(response.Imported ? "No players are currently ignored." : "Waiting for the first in-game import."));
    }

    public void ShowLoadingState()
    {
        var loading = new SocialFriendsResponse { Success = true, Imported = false };
        ApplySnapshot(loading);
        FriendsSummaryText.Text = "Loading your in-game friend list...";
        FriendsCountText.Text = string.Empty;
        IgnoredCountText.Text = string.Empty;
        PendingChangesHeader.Visibility = Visibility.Collapsed;
        PendingChangesListPanel.Children.Clear();
        OnlineFriendsListPanel.Children.Clear();
        OfflineFriendsListPanel.Children.Clear();
        IgnoredListPanel.Children.Clear();
        OnlineFriendsListPanel.Children.Add(CreateSocialEmptyCard("Loading friend snapshot..."));
        OfflineFriendsListPanel.Children.Add(CreateSocialEmptyCard("Loading friend snapshot..."));
        IgnoredListPanel.Children.Add(CreateSocialEmptyCard("Loading ignored-player snapshot..."));
    }


    private static List<SocialRelationshipItem> SortFriendGroup(IEnumerable<SocialRelationshipItem> friends)
    {
        return friends
            .OrderByDescending(friend => friend.UnreadCount > 0)
            .ThenBy(friend => friend.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private Border CreateSocialPersonCard(SocialRelationshipItem person, bool ignored)
    {
        Grid grid = new();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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
        if (!ignored && person.UnreadCount > 0)
        {
            identity.Children.Add(new TextBlock
            {
                Text = person.UnreadCount == 1 ? "1 unread message" : $"{person.UnreadCount} unread messages",
                Foreground = new SolidColorBrush(Color.FromRgb(250, 204, 21)),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 3, 0, 0)
            });
        }
        Grid.SetColumn(identity, 1);
        grid.Children.Add(identity);

        bool conversationMuted = !ignored && _mutedAccountIds.Contains(person.AccountId);
        TextBlock mutedIcon = new()
        {
            Text = "🔕",
            FontFamily = new FontFamily("Segoe UI Emoji"),
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(155, 167, 186)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0),
            Visibility = conversationMuted ? Visibility.Visible : Visibility.Collapsed,
            ToolTip = conversationMuted ? "Conversation notifications are muted" : null
        };
        Grid.SetColumn(mutedIcon, 2);
        grid.Children.Add(mutedIcon);

        FrameworkElement action = new Border
        {
            Background = new SolidColorBrush(ignored ? Color.FromRgb(54, 24, 30) : Color.FromRgb(34, 41, 54)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(9, 3, 9, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = ignored ? "IGNORED" : "⋮",
                Foreground = new SolidColorBrush(ignored ? Color.FromRgb(248, 113, 113) : Color.FromRgb(155, 167, 186)),
                FontSize = ignored ? 9 : 15,
                FontWeight = FontWeights.Bold
            }
        };
        Grid.SetColumn(action, 3);
        grid.Children.Add(action);

        Border card = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(17, 22, 30)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(42, 48, 60)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(13, 10, 13, 10),
            Margin = new Thickness(0, 0, 0, 7),
            Child = grid,
            Tag = person.PlayerName,
            ToolTip = ignored ? null : new ToolTip
            {
                Content = "Right-click for friend options",
                Background = new SolidColorBrush(Color.FromRgb(17, 22, 30)),
                Foreground = new SolidColorBrush(Color.FromRgb(232, 238, 247)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 55, 68)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 5, 8, 5)
            }
        };

        if (!ignored)
        {
            ContextMenu menu = new();
            if (TryFindResource("DarkContextMenuStyle") is Style contextMenuStyle)
                menu.Style = contextMenuStyle;
            MenuItem messageItem = new()
            {
                Header = "Message",
                Tag = person
            };
            if (TryFindResource("DarkMenuItemStyle") is Style messageMenuItemStyle)
                messageItem.Style = messageMenuItemStyle;
            messageItem.Click += MessageFriendMenuItem_Click;
            bool isMuted = _mutedAccountIds.Contains(person.AccountId);
            MenuItem muteItem = new()
            {
                Header = isMuted ? "Unmute Conversation" : "Mute Conversation",
                Tag = person
            };
            if (TryFindResource("DarkMenuItemStyle") is Style muteMenuItemStyle)
                muteItem.Style = muteMenuItemStyle;
            muteItem.Click += MuteFriendMenuItem_Click;
            MenuItem removeItem = new()
            {
                Header = "Remove Friend",
                Tag = person.PlayerName
            };
            if (TryFindResource("DarkMenuItemStyle") is Style removeMenuItemStyle)
                removeItem.Style = removeMenuItemStyle;
            removeItem.Click += RemoveFriendMenuItem_Click;
            Separator separator = new();
            if (TryFindResource("DarkMenuSeparatorStyle") is Style separatorStyle)
                separator.Style = separatorStyle;
            menu.Items.Add(messageItem);
            menu.Items.Add(muteItem);
            menu.Items.Add(separator);
            menu.Items.Add(removeItem);
            card.ContextMenu = menu;
            card.MouseLeftButtonDown += FriendCard_MouseLeftButtonDown;
        }

        return card;
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

    private static Border CreatePendingChangeCard(SocialPendingFriendChange change)
    {
        bool isRemoval = change.Operation.Equals("remove", StringComparison.OrdinalIgnoreCase);
        Color accent = isRemoval ? Color.FromRgb(248, 113, 113) : Color.FromRgb(74, 222, 128);
        Color background = isRemoval ? Color.FromRgb(42, 21, 25) : Color.FromRgb(17, 37, 31);
        Color border = isRemoval ? Color.FromRgb(112, 48, 58) : Color.FromRgb(38, 91, 70);
        string label = isRemoval ? "REMOVE" : "ADD";
        string detail = isRemoval
            ? "Queued for removal when you next enter the game"
            : "Queued for addition when you next enter the game";

        Grid grid = new();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        StackPanel identity = new();
        identity.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(change.PlayerName) ? "Unknown Player" : change.PlayerName,
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13
        });
        identity.Children.Add(new TextBlock
        {
            Text = detail,
            Foreground = new SolidColorBrush(Color.FromRgb(190, 199, 214)),
            FontSize = 10,
            Margin = new Thickness(0, 3, 10, 0),
            TextWrapping = TextWrapping.Wrap
        });
        grid.Children.Add(identity);

        Border operationBadge = new()
        {
            Background = new SolidColorBrush(Color.FromArgb(42, accent.R, accent.G, accent.B)),
            BorderBrush = new SolidColorBrush(accent),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(8, 3, 8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(accent),
                FontSize = 9,
                FontWeight = FontWeights.Bold
            }
        };
        Grid.SetColumn(operationBadge, 1);
        grid.Children.Add(operationBadge);

        return new Border
        {
            Background = new SolidColorBrush(background),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(13, 10, 13, 10),
            Margin = new Thickness(0, 0, 0, 7),
            Child = grid
        };
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


    public void SetActionState(bool busy, string message)
    {
        AddFriendButton.IsEnabled = !busy;
        AddFriendNameTextBox.IsEnabled = !busy;
        StatusText.Text = message;
    }

    public void ClearAddFriendName() => AddFriendNameTextBox.Text = string.Empty;

    private void AddFriendButton_Click(object sender, RoutedEventArgs e) => RequestAddFriend();

    private void AddFriendNameTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) RequestAddFriend();
    }

    private void RequestAddFriend()
    {
        string name = AddFriendNameTextBox.Text.Trim();
        if (name.Length == 0) { StatusText.Text = "Enter a player name."; return; }
        FriendActionRequested?.Invoke(this, new FriendActionRequestedEventArgs(name, "add"));
    }


    private void MessageFriendMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: SocialRelationshipItem person })
            FriendActionRequested?.Invoke(this, new FriendActionRequestedEventArgs(person.PlayerName, "message", person.AccountId));
    }

    private void FriendCard_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is Border { Tag: string playerName })
        {
            SocialRelationshipItem? person = (_latestFriends ?? []).FirstOrDefault(f => string.Equals(f.PlayerName, playerName, StringComparison.OrdinalIgnoreCase));
            if (person is not null)
                FriendActionRequested?.Invoke(this, new FriendActionRequestedEventArgs(person.PlayerName, "message", person.AccountId));
        }
    }


    private void MuteFriendMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: SocialRelationshipItem person }) return;
        string operation = _mutedAccountIds.Contains(person.AccountId) ? "unmute" : "mute";
        FriendActionRequested?.Invoke(this, new FriendActionRequestedEventArgs(person.PlayerName, operation, person.AccountId));
    }

    private void RemoveFriendMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is string name && name.Length > 0)
            FriendActionRequested?.Invoke(this, new FriendActionRequestedEventArgs(name, "remove"));
    }

    private void ClosePanelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public sealed class FriendActionRequestedEventArgs(string playerName, string operation, string accountId = "") : EventArgs
{
    public string PlayerName { get; } = playerName;
    public string Operation { get; } = operation;
    public string AccountId { get; } = accountId;
}
