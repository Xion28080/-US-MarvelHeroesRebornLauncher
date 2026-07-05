using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Media;
using MHRebornLauncher.Models;
using MHRebornLauncher.Services;
using System.Reflection;

namespace MHRebornLauncher;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly SavedLoginService _savedLoginService = new();
    private readonly NewsFeedService _newsFeedService = new();
    private readonly AuthService _authService = new();
    private readonly GameLauncherService _gameLauncherService = new();
    private readonly GamePathService _gamePathService = new();
    private readonly UpdateService _updateService = new();

    private LauncherSettings _settings;
    private string _currentEmail = "";
    private string _currentPassword = "";

    public MainWindow()
    {
        InitializeComponent();

        LauncherVersionText.Text = $"Launcher v{GetLauncherVersion()}";

        _settings = _settingsService.LoadOrCreate();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateGameDetectionUi();
        await LoadNewsAsync();
        await TryAutoLoginAsync();
        _ = CheckForLauncherUpdateAsync();
    }


    private async Task CheckForLauncherUpdateAsync()
    {
        try
        {
            LauncherUpdateInfo? updateInfo = await _updateService.CheckForUpdateAsync(_settings);
            if (updateInfo == null)
                return;

            string message =
                $"A new version of the Marvel Heroes Reborn Launcher is available.\n\n" +
                $"Current version: {updateInfo.CurrentVersion}\n" +
                $"Latest version: {updateInfo.LatestVersion}\n\n" +
                "Would you like to download and install it now?";

            MessageBoxResult result = MessageBox.Show(
                message,
                "Launcher Update Available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes)
                return;

            FooterText.Text = "Downloading launcher update...";

            Progress<double> progress = new(value =>
            {
                int percent = Math.Clamp((int)Math.Round(value * 100), 0, 100);
                FooterText.Text = $"Downloading launcher update... {percent}%";
            });

            string packagePath = await _updateService.DownloadUpdateAsync(updateInfo, progress);

            FooterText.Text = "Installing launcher update...";
            _updateService.StartUpdaterAndExit(packagePath);
        }
        catch (Exception ex)
        {
            FooterText.Text = "Ready to launch.";
            MessageBox.Show(
                "The launcher update could not be completed. You can continue using this version.\n\n" + ex.Message,
                "Update Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task TryAutoLoginAsync()
    {
        SavedLogin? savedLogin = _savedLoginService.Load();

        if (savedLogin == null || string.IsNullOrWhiteSpace(savedLogin.EmailAddress) || string.IsNullOrWhiteSpace(savedLogin.Password))
        {
            ShowLoginScreen();
            return;
        }

        LoginResponse loginResult = await _authService.LoginAsync(_settings, savedLogin.EmailAddress, savedLogin.Password);
        if (!loginResult.Success)
        {
            _savedLoginService.Clear();
            ShowLoginScreen();
            ShowError(loginResult.Error.Length > 0 ? loginResult.Error : "Saved login is no longer valid.");
            return;
        }

        _currentEmail = savedLogin.EmailAddress;
        _currentPassword = savedLogin.Password;
        ShowAccountScreen();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        string email = NormalizeEmail(EmailTextBox.Text);
        string password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(email))
        {
            ShowError("Please enter your email or account name.");
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError("Please enter your password.");
            return;
        }

        LoginButton.IsEnabled = false;
        LoginButton.Content = "CHECKING...";
        ShowError("");

        try
        {
            LoginResponse loginResult = await _authService.LoginAsync(_settings, email, password);
            if (!loginResult.Success)
            {
                ShowError(loginResult.Error.Length > 0 ? loginResult.Error : "Invalid login.");
                return;
            }

            _currentEmail = email;
            _currentPassword = password;

            if (RememberMeCheckBox.IsChecked == true)
            {
                _savedLoginService.Save(new SavedLogin
                {
                    EmailAddress = email,
                    Password = password
                });
            }

            ShowAccountScreen();
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Content = "LOG IN";
        }
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_gamePathService.IsGameFound || string.IsNullOrWhiteSpace(_gamePathService.GameExecutablePath))
        {
            MessageBox.Show(
                "MarvelHeroesOmega.exe was not found. Place the launcher in the root Marvel Heroes folder next to UnrealEngine3.",
                "Game Not Found",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
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

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        _savedLoginService.Clear();
        _currentEmail = "";
        _currentPassword = "";
        EmailTextBox.Text = "";
        PasswordBox.Password = "";
        RememberMeCheckBox.IsChecked = false;
        ShowLoginScreen();
    }

    private async void RefreshNewsButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadNewsAsync();
    }

    private async Task LoadNewsAsync()
    {
        NewsStatusText.Text = "Loading news...";
        List<NewsPost> posts = await _newsFeedService.GetNewsAsync(_settings.NewsFeedUrl);
        NewsItemsControl.ItemsSource = posts;
        NewsStatusText.Text = posts.Count == 1 ? "Latest website update" : $"Latest {posts.Count} website updates";
    }

    private void ViewPostButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
            return;

        string url = element.Tag?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Unable to Open Post", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private string NormalizeEmail(string input)
    {
        input = input.Trim();

        if (input.Length == 0)
            return input;

        if (!input.Contains('@'))
            return $"{input}@{_settings.EmailDomain}";

        return input;
    }

    private void ShowLoginScreen()
    {
        LoginPanel.Visibility = Visibility.Visible;
        AccountPanel.Visibility = Visibility.Collapsed;
        FooterText.Text = _settings.RequireServerLogin
            ? "Server validation enabled. Enter your server account to continue."
            : "Enter your Marvel Heroes Reborn account to launch the game.";
    }

    private void ShowAccountScreen()
    {
        LoginPanel.Visibility = Visibility.Collapsed;
        AccountPanel.Visibility = Visibility.Visible;
        LoggedInEmailText.Text = _currentEmail;

        GamePathText.Text = _gamePathService.GameExecutablePath ?? "Not found";
        LaunchModeText.Text = _settings.RequireServerLogin
            ? "Automatic login enabled. Credentials are verified before launching."
            : "Automatic login enabled. The game server will verify your account when the game starts.";

        FooterText.Text = "Ready to launch.";
    }

    private void ShowError(string message)
    {
        LoginErrorText.Text = message;
    }

    private void CreateAccountLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });

            e.Handled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Unable to Open Link", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UpdateGameDetectionUi()
    {
        if (_gamePathService.IsGameFound)
        {
            GameFoundDot.Fill = new SolidColorBrush(Color.FromRgb(87, 194, 115));
            GameFoundText.Text = "Game found";
        }
        else
        {
            GameFoundDot.Fill = new SolidColorBrush(Color.FromRgb(224, 82, 82));
            GameFoundText.Text = "Game not found";
        }
    }

    private static string GetLauncherVersion()
    {
        string? version = System.Reflection.Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(version))
        {
            version = System.Reflection.Assembly
                .GetExecutingAssembly()
                .GetName()
                .Version?
                .ToString();
        }

        version = (version ?? "Unknown")
            .Split('+')[0]
            .Trim();

        return version;
    }
}
