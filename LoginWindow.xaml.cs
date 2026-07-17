using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Navigation;
using MHRebornLauncher.Models;
using MHRebornLauncher.Services;

namespace MHRebornLauncher;

public partial class LoginWindow : Window
{
    private readonly LauncherSettings _settings;
    private readonly AuthService _authService = new();
    private readonly SavedLoginService _savedLoginService = new();

    public string AuthenticatedEmail { get; private set; } = "";
    public string AuthenticatedPassword { get; private set; } = "";
    public LoginResponse? AuthenticatedAccount { get; private set; }

    public LoginWindow(LauncherSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        VersionText.Text = $"Launcher v{GetVersion()}";
        Loaded += LoginWindow_Loaded;
    }

    private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SavedLogin? saved = _savedLoginService.Load();
        if (saved is null) return;

        EmailTextBox.Text = saved.EmailAddress;
        RememberMeCheckBox.IsChecked = true;
        LoginButton.IsEnabled = false;
        LoginButton.Content = "SIGNING IN...";

        LoginResponse result = await _authService.LoginAsync(_settings, saved.EmailAddress, saved.Password);
        if (result.Success)
        {
            CompleteLogin(saved.EmailAddress, saved.Password, result);
            return;
        }

        _savedLoginService.Clear();
        LoginErrorText.Text = result.Error.Length > 0 ? result.Error : "Your saved sign-in is no longer valid.";
        LoginButton.IsEnabled = true;
        LoginButton.Content = "CONTINUE";
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e) => await AttemptLoginAsync();

    private async void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await AttemptLoginAsync();
    }

    private async Task AttemptLoginAsync()
    {
        string email = NormalizeEmail(EmailTextBox.Text);
        string password = PasswordBox.Password;
        if (email.Length == 0) { LoginErrorText.Text = "Enter your game account."; return; }
        if (password.Length == 0) { LoginErrorText.Text = "Enter your password."; return; }

        LoginButton.IsEnabled = false;
        LoginButton.Content = "SIGNING IN...";
        LoginErrorText.Text = "";

        LoginResponse result = await _authService.LoginAsync(_settings, email, password);
        if (!result.Success)
        {
            LoginErrorText.Text = result.Error.Length > 0 ? result.Error : "Invalid account or password.";
            LoginButton.IsEnabled = true;
            LoginButton.Content = "CONTINUE";
            return;
        }

        if (RememberMeCheckBox.IsChecked == true)
            _savedLoginService.Save(new SavedLogin { EmailAddress = email, Password = password });
        else
            _savedLoginService.Clear();

        CompleteLogin(email, password, result);
    }

    private void CompleteLogin(string email, string password, LoginResponse result)
    {
        AuthenticatedEmail = email;
        AuthenticatedPassword = password;
        AuthenticatedAccount = result;
        DialogResult = true;
        Close();
    }

    private string NormalizeEmail(string input)
    {
        input = input.Trim();
        return input.Length > 0 && !input.Contains('@') ? $"{input}@{_settings.EmailDomain}" : input;
    }

    private void CreateAccountButton_Click(object sender, RoutedEventArgs e) => OpenUrl("https://play.omeganode.org/Dashboard/");
    private void ExternalLink_RequestNavigate(object sender, RequestNavigateEventArgs e) { OpenUrl(e.Uri.AbsoluteUri); e.Handled = true; }
    private static void OpenUrl(string url) => Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });

    private static string GetVersion() => (Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown").Split('+')[0];

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
    private void CloseWindowButton_Click(object sender, RoutedEventArgs e) => Close();

}
