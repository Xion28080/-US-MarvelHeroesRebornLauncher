using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MHRebornLauncher.Models;

namespace MHRebornLauncher;

public partial class ConversationWindow : Window
{
    private const double SingleConversationWidth = 500;
    private const double MinimumSidebarWidth = 150;
    private const double MaximumSidebarWidth = 310;
    private readonly Dictionary<string, ConversationState> _conversations = new(StringComparer.OrdinalIgnoreCase);
    private string _activeAccountId = string.Empty;
    private bool _canSend;
    private bool _sidebarExpanded;
    private double _currentSidebarWidth;
    private HashSet<string> _mutedAccountIds = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<ConversationMessageSendEventArgs>? MessageSendRequested;
    public event EventHandler<ConversationActivatedEventArgs>? ConversationActivated;
    public event EventHandler<ConversationMuteRequestedEventArgs>? ConversationMuteRequested;

    public string ActiveAccountId => _activeAccountId;
    public IReadOnlyCollection<string> OpenAccountIds => _conversations.Keys;

    public ConversationWindow()
    {
        InitializeComponent();
    }

    public bool ContainsConversation(string accountId) => _conversations.ContainsKey(accountId);

    public void SetMutedAccounts(IEnumerable<string> accountIds)
    {
        _mutedAccountIds = new HashSet<string>(accountIds ?? [], StringComparer.OrdinalIgnoreCase);

        // Keep the active conversation header synchronized when mute state changes
        // from either the Friends window or a conversation context menu.
        ConversationTitleMutedIcon.Visibility =
            !string.IsNullOrWhiteSpace(_activeAccountId) && _mutedAccountIds.Contains(_activeAccountId)
                ? Visibility.Visible
                : Visibility.Collapsed;

        RebuildConversationTabs();
    }

    public bool IsConversationActiveAndFocused(string accountId) =>
        IsActive && string.Equals(_activeAccountId, accountId, StringComparison.OrdinalIgnoreCase);

    public void OpenConversation(string accountId, string playerName, string presence, int unreadCount, IEnumerable<SocialDirectMessage> messages)
    {
        if (string.IsNullOrWhiteSpace(accountId)) return;

        if (!_conversations.TryGetValue(accountId, out ConversationState? state))
        {
            state = new ConversationState(accountId, playerName);
            _conversations.Add(accountId, state);
        }
        else if (!string.IsNullOrWhiteSpace(playerName))
        {
            state.PlayerName = playerName;
        }

        state.Presence = presence;
        state.UnreadCount = Math.Max(0, unreadCount);
        AddMessagesToState(state, messages);
        SelectConversation(accountId, true);
        RebuildConversationTabs();
    }

    public void UpdatePresence(string accountId, string presence, int unreadCount)
    {
        if (!_conversations.TryGetValue(accountId, out ConversationState? state)) return;
        state.Presence = presence;
        state.UnreadCount = Math.Max(0, unreadCount);
        if (string.Equals(_activeAccountId, accountId, StringComparison.OrdinalIgnoreCase))
            ApplyPresence(presence);
        RebuildConversationTabs();
    }

    public void AddMessages(IEnumerable<SocialDirectMessage> messages)
    {
        bool activeChanged = false;
        foreach (SocialDirectMessage message in messages.OrderBy(item => item.CreatedAtUtc))
        {
            if (!_conversations.TryGetValue(message.OtherAccountId, out ConversationState? state))
                continue;
            if (!state.MessageIds.Add(message.MessageId)) continue;
            state.Messages.Add(message);
            if (string.Equals(_activeAccountId, message.OtherAccountId, StringComparison.OrdinalIgnoreCase))
                activeChanged = true;
        }
        if (activeChanged) RenderActiveConversation(true);
    }

    public void SetSendState(bool sending, string message = "")
    {
        MessageInputTextBox.IsEnabled = !sending && _canSend;
        SendButton.IsEnabled = !sending && _canSend;
        SendButton.Content = sending ? "SENDING" : "SEND";
        if (!string.IsNullOrWhiteSpace(message)) SetInputToolTip(message);

        // Sending temporarily disables the input, which causes WPF to move keyboard
        // focus elsewhere. Restore focus after every completed send so the player can
        // continue typing without clicking the message box again.
        if (!sending && MessageInputTextBox.IsEnabled)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                MessageInputTextBox.Focus();
                Keyboard.Focus(MessageInputTextBox);
                MessageInputTextBox.CaretIndex = MessageInputTextBox.Text.Length;
            });
        }
    }

    public void ClearMessageInput() => MessageInputTextBox.Clear();

    private void SelectConversation(string accountId, bool raiseActivated)
    {
        if (!_conversations.TryGetValue(accountId, out ConversationState? selectedState)) return;
        selectedState.UnreadCount = 0;
        _activeAccountId = accountId;
        RenderActiveConversation(false);
        RebuildConversationTabs();
        if (raiseActivated)
            ConversationActivated?.Invoke(this, new ConversationActivatedEventArgs(accountId));
    }

    private void RenderActiveConversation(bool preserveNearBottom)
    {
        if (!_conversations.TryGetValue(_activeAccountId, out ConversationState? state)) return;
        bool shouldScroll = !preserveNearBottom || MessagesScrollViewer.ScrollableHeight - MessagesScrollViewer.VerticalOffset < 80;
        ConversationTitleText.Text = state.PlayerName;
        ConversationTitleMutedIcon.Visibility = _mutedAccountIds.Contains(state.AccountId) ? Visibility.Visible : Visibility.Collapsed;
        MessagesPanel.Children.Clear();
        foreach (SocialDirectMessage message in state.Messages.OrderBy(item => item.CreatedAtUtc))
            MessagesPanel.Children.Add(CreateMessageCard(message));
        ApplyPresence(state.Presence);
        if (shouldScroll) MessagesScrollViewer.ScrollToEnd();
    }

    private void ApplyPresence(string presence)
    {
        _canSend = true;
        MessageInputTextBox.IsEnabled = true;
        SendButton.IsEnabled = true;
        SetInputToolTip(presence switch
        {
            "InGame" => "This message will be delivered as an in-game whisper.",
            "LauncherOnline" => "This message will be delivered to the launcher.",
            _ => "This friend is offline. The message will be queued for later delivery."
        });
    }

    private void RebuildConversationTabs()
    {
        ConversationTabsPanel.Children.Clear();
        foreach (ConversationState state in _conversations.Values.OrderBy(item => item.PlayerName, StringComparer.OrdinalIgnoreCase))
            ConversationTabsPanel.Children.Add(CreateConversationTab(state));
        UpdateSidebarLayout();
    }

    private Border CreateConversationTab(ConversationState state)
    {
        bool selected = string.Equals(state.AccountId, _activeAccountId, StringComparison.OrdinalIgnoreCase);
        Grid grid = new();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Border status = new()
        {
            Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Margin = new Thickness(1, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(state.Presence switch
            {
                "InGame" => Color.FromRgb(56, 189, 248),
                "LauncherOnline" => Color.FromRgb(74, 222, 128),
                _ => Color.FromRgb(107, 114, 128)
            })
        };
        TextBlock mutedIcon = new()
        {
            Text = "🔕", FontFamily = new FontFamily("Segoe UI Emoji"), FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(155, 167, 186)), VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Visibility = _mutedAccountIds.Contains(state.AccountId) ? Visibility.Visible : Visibility.Collapsed,
            ToolTip = "Conversation notifications are muted"
        };
        TextBlock name = new() { Text = state.PlayerName, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
        Border unread = new()
        {
            MinWidth = 20, Height = 20, CornerRadius = new CornerRadius(10), Margin = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(5, 1, 5, 1), Background = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = state.UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed,
            Child = new TextBlock { Text = state.UnreadCount > 99 ? "99+" : state.UnreadCount.ToString(), Foreground = Brushes.White, FontSize = 9, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        Button close = new()
        {
            Content = "×", Width = 22, Height = 22, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(0),
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = new SolidColorBrush(Color.FromRgb(151, 161, 177)),
            Tag = state.AccountId, ToolTip = "Close conversation"
        };
        close.Click += CloseConversationTab_Click;
        Grid.SetColumn(mutedIcon, 1); Grid.SetColumn(name, 2); Grid.SetColumn(unread, 3); Grid.SetColumn(close, 4);
        grid.Children.Add(status); grid.Children.Add(mutedIcon); grid.Children.Add(name); grid.Children.Add(unread); grid.Children.Add(close);

        Border tab = new()
        {
            Child = grid, Padding = new Thickness(9, 8, 7, 8), Margin = new Thickness(0, 0, 0, 4), CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(selected ? Color.FromRgb(30, 54, 72) : Color.FromRgb(20, 25, 34)),
            BorderBrush = new SolidColorBrush(selected ? Color.FromRgb(56, 189, 248) : Color.FromRgb(42, 48, 60)),
            BorderThickness = new Thickness(1), Cursor = Cursors.Hand, Tag = state.AccountId
        };
        ContextMenu contextMenu = new();
        if (TryFindResource("DarkContextMenuStyle") is Style contextMenuStyle)
            contextMenu.Style = contextMenuStyle;
        MenuItem muteItem = new()
        {
            Header = _mutedAccountIds.Contains(state.AccountId) ? "Unmute Conversation" : "Mute Conversation",
            Tag = state.AccountId
        };
        if (TryFindResource("DarkMenuItemStyle") is Style menuItemStyle)
            muteItem.Style = menuItemStyle;
        muteItem.Click += ConversationMuteMenuItem_Click;
        contextMenu.Items.Add(muteItem);
        tab.ContextMenu = contextMenu;
        tab.MouseLeftButtonUp += ConversationTab_MouseLeftButtonUp;
        return tab;
    }


    private void ConversationMuteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string accountId }) return;
        ConversationMuteRequested?.Invoke(this, new ConversationMuteRequestedEventArgs(accountId, !_mutedAccountIds.Contains(accountId)));
    }

    private void ConversationTab_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && IsInsideButton(source)) return;
        if (sender is Border { Tag: string accountId }) SelectConversation(accountId, true);
    }

    private void CloseConversationTab_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { Tag: string accountId }) return;
        bool wasActive = string.Equals(_activeAccountId, accountId, StringComparison.OrdinalIgnoreCase);
        _conversations.Remove(accountId);
        if (_conversations.Count == 0) { Close(); return; }
        if (wasActive) _activeAccountId = _conversations.Values.OrderBy(item => item.PlayerName, StringComparer.OrdinalIgnoreCase).First().AccountId;
        RenderActiveConversation(false);
        RebuildConversationTabs();
        ConversationActivated?.Invoke(this, new ConversationActivatedEventArgs(_activeAccountId));
    }

    private void UpdateSidebarLayout()
    {
        bool shouldExpand = _conversations.Count > 1;
        if (!shouldExpand)
        {
            ConversationSidebar.Visibility = Visibility.Collapsed;
            ConversationSidebarColumn.Width = new GridLength(0);
            if (_sidebarExpanded && WindowState == WindowState.Normal)
                Width = Math.Max(MinWidth, Width - _currentSidebarWidth);
            _sidebarExpanded = false;
            _currentSidebarWidth = 0;
            return;
        }

        double sidebarWidth = CalculateSidebarWidth();
        ConversationSidebar.Width = sidebarWidth;
        ConversationSidebarColumn.Width = new GridLength(sidebarWidth);
        ConversationSidebar.Visibility = Visibility.Visible;
        if (WindowState == WindowState.Normal)
        {
            double widthAdjustment = _sidebarExpanded ? sidebarWidth - _currentSidebarWidth : sidebarWidth;
            Width = Math.Min(SystemParameters.WorkArea.Width - 40, Math.Max(ActualWidth, SingleConversationWidth) + widthAdjustment);
        }
        _sidebarExpanded = true;
        _currentSidebarWidth = sidebarWidth;
    }

    private double CalculateSidebarWidth()
    {
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double longest = _conversations.Values.Select(state => new FormattedText(
            state.PlayerName, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 12, Brushes.White, pixelsPerDip).WidthIncludingTrailingWhitespace).DefaultIfEmpty(0).Max();
        return Math.Clamp(longest + 8 + 8 + 22 + 6 + 34 + 22 + 6 + 34, MinimumSidebarWidth, MaximumSidebarWidth);
    }

    private static void AddMessagesToState(ConversationState state, IEnumerable<SocialDirectMessage> messages)
    {
        foreach (SocialDirectMessage message in messages.OrderBy(item => item.CreatedAtUtc))
        {
            if (!state.MessageIds.Add(message.MessageId)) continue;
            state.Messages.Add(message);
        }
    }

    private void SetInputToolTip(string message)
    {
        ToolTip toolTip = new() { Content = new TextBlock { Text = message, Foreground = new SolidColorBrush(Color.FromRgb(232, 238, 247)) } };
        if (TryFindResource("DarkToolTipStyle") is Style style) toolTip.Style = style;
        MessageInputTextBox.ToolTip = toolTip;
    }

    private static Border CreateMessageCard(SocialDirectMessage message)
    {
        DateTimeOffset time = DateTimeOffset.FromUnixTimeSeconds(message.CreatedAtUtc).ToLocalTime();
        StackPanel stack = new();
        stack.Children.Add(new TextBlock { Text = message.IsOutgoing ? "You" : message.SenderPlayerName, Foreground = new SolidColorBrush(message.IsOutgoing ? Color.FromRgb(56, 189, 248) : Color.FromRgb(74, 222, 128)), FontWeight = FontWeights.Bold, FontSize = 10 });
        stack.Children.Add(new TextBlock { Text = message.MessageBody, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
        stack.Children.Add(new TextBlock { Text = time.ToString("MMM d, h:mm tt"), Foreground = new SolidColorBrush(Color.FromRgb(128, 138, 156)), FontSize = 9, Margin = new Thickness(0, 5, 0, 0) });
        return new Border { Background = new SolidColorBrush(message.IsOutgoing ? Color.FromRgb(18, 40, 58) : Color.FromRgb(17, 22, 30)), BorderBrush = new SolidColorBrush(Color.FromRgb(42, 48, 60)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(11), Margin = new Thickness(message.IsOutgoing ? 42 : 0, 0, message.IsOutgoing ? 0 : 42, 8), Child = stack };
    }

    private void SendCurrentMessage()
    {
        if (!_canSend || !_conversations.TryGetValue(_activeAccountId, out ConversationState? state)) return;
        string body = MessageInputTextBox.Text.Trim();
        if (body.Length == 0) return;
        MessageSendRequested?.Invoke(this, new ConversationMessageSendEventArgs(state.AccountId, state.PlayerName, body));
    }

    private void SendButton_Click(object sender, RoutedEventArgs e) => SendCurrentMessage();
    private void MessageInputTextBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) { e.Handled = true; SendCurrentMessage(); } }
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton != MouseButton.Left || IsInsideButton(e.OriginalSource as DependencyObject)) return; if (e.ClickCount == 2 && ResizeMode != ResizeMode.NoResize) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; else DragMove(); }
    private static bool IsInsideButton(DependencyObject? source) { while (source is not null) { if (source is Button) return true; source = VisualTreeHelper.GetParent(source); } return false; }
    private void CloseWindowButton_Click(object sender, RoutedEventArgs e) => Close();

    private sealed class ConversationState(string accountId, string playerName)
    {
        public string AccountId { get; } = accountId;
        public string PlayerName { get; set; } = playerName;
        public string Presence { get; set; } = "Offline";
        public int UnreadCount { get; set; }
        public HashSet<string> MessageIds { get; } = [];
        public List<SocialDirectMessage> Messages { get; } = [];
    }
}

public sealed class ConversationMessageSendEventArgs(string accountId, string playerName, string messageBody) : EventArgs
{
    public string AccountId { get; } = accountId;
    public string PlayerName { get; } = playerName;
    public string MessageBody { get; } = messageBody;
}

public sealed class ConversationActivatedEventArgs(string accountId) : EventArgs
{
    public string AccountId { get; } = accountId;
}

public sealed class ConversationMuteRequestedEventArgs(string accountId, bool mute) : EventArgs
{
    public string AccountId { get; } = accountId;
    public bool Mute { get; } = mute;
}
