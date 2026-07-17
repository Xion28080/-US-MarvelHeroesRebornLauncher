using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MHRebornLauncher.Models;
using MHRebornLauncher.Services;

namespace MHRebornLauncher;

public partial class OptionsWindow : Window
{
    private readonly LauncherSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly GamePathService _gamePathService = new();
    private readonly InstallationVerifier _installationVerifier = new();
    private readonly DiagnosticPackageService _diagnosticPackageService = new();
    private List<DiagnosticResult> _lastResults = [];

    public OptionsWindow(LauncherSettings settings, SettingsService settingsService)
    {
        InitializeComponent();
        _settings = settings;
        _settingsService = settingsService;
        SkipLaunchSplashCheckBox.IsChecked = settings.SkipLaunchSplash;
        SkipStartupMoviesCheckBox.IsChecked = settings.SkipStartupMovies;
        MinimizeAfterLaunchCheckBox.IsChecked = settings.MinimizeAfterLaunch;
        RestoreAfterGameExitCheckBox.IsChecked = settings.RestoreAfterGameExit;
        DiscordRichPresenceCheckBox.IsChecked = settings.EnableDiscordRichPresence;
    }

    private void GeneralNavButton_Click(object sender, RoutedEventArgs e) => ShowSection(false);
    private void SupportNavButton_Click(object sender, RoutedEventArgs e) => ShowSection(true);

    private void ShowSection(bool support)
    {
        GeneralPanel.Visibility = support ? Visibility.Collapsed : Visibility.Visible;
        SupportPanel.Visibility = support ? Visibility.Visible : Visibility.Collapsed;
        GeneralNavButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(support ? "#00000000" : "#243142"));
        SupportNavButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(support ? "#243142" : "#00000000"));
        GeneralNavButton.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(support ? "#00000000" : "#148BEB"));
        SupportNavButton.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(support ? "#148BEB" : "#00000000"));
    }

    private async void VerifyInstallationButton_Click(object sender, RoutedEventArgs e)
    {
        SupportStatusText.Text = "Running installation and connectivity checks...";
        DiagnosticResultsList.ItemsSource = null;
        try
        {
            _lastResults = await _installationVerifier.RunAsync(_gamePathService, _settings);
            DiagnosticResultsList.ItemsSource = _lastResults.Select(ToDisplayResult).ToList();
            int failed = _lastResults.Count(x => x.Severity == DiagnosticSeverity.Failed);
            int warning = _lastResults.Count(x => x.Severity == DiagnosticSeverity.Warning);
            SupportStatusText.Text = failed == 0 ? $"Verification complete: {_lastResults.Count - warning} passed, {warning} warning(s)." : $"Verification complete: {failed} failed, {warning} warning(s).";
            LogService.Info($"Installation verification completed. Failed={failed}, Warnings={warning}");
        }
        catch (Exception ex)
        {
            SupportStatusText.Text = "Verification failed: " + ex.Message;
            LogService.Error("Installation verification failed", ex);
        }
    }

    private async void CreateDiagnosticZipButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResults.Count == 0)
            _lastResults = await _installationVerifier.RunAsync(_gamePathService, _settings);
        try
        {
            string path = _diagnosticPackageService.Create(_gamePathService, _settings, _lastResults);
            SupportStatusText.Text = "Diagnostic package created: " + path;
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            LogService.Info("Diagnostic package created.");
        }
        catch (Exception ex)
        {
            SupportStatusText.Text = "Unable to create diagnostic package: " + ex.Message;
            LogService.Error("Diagnostic package creation failed", ex);
        }
    }

    private async void CopySupportSummaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResults.Count == 0)
            _lastResults = await _installationVerifier.RunAsync(_gamePathService, _settings);
        StringBuilder text = new();
        text.AppendLine("Marvel Heroes Reborn Launcher Support Summary");
        text.AppendLine($"Launcher: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
        text.AppendLine($"Game path: {_gamePathService.GameExecutablePath ?? "Not found"}");
        foreach (DiagnosticResult result in _lastResults) text.AppendLine($"[{result.Severity}] {result.Name}: {result.Details}");
        Clipboard.SetText(text.ToString());
        SupportStatusText.Text = "Support summary copied to the clipboard.";
    }

    private void OpenGameFolderButton_Click(object sender, RoutedEventArgs e) => OpenFolder(_gamePathService.GameRootDirectory);
    private void OpenLauncherLogsButton_Click(object sender, RoutedEventArgs e) => OpenFolder(LauncherPaths.LogsDirectory);
    private void OpenDataFolderButton_Click(object sender, RoutedEventArgs e) => OpenFolder(LauncherPaths.DataRoot);
    private void OpenGameLogsButton_Click(object sender, RoutedEventArgs e)
    {
        string? path = DiagnosticPackageService.GetGameLogDirectories(_gamePathService.GameRootDirectory).FirstOrDefault(Directory.Exists);
        if (path is null) { SupportStatusText.Text = "No supported game log folder was found yet."; return; }
        OpenFolder(path);
    }

    private void OpenFolder(string path)
    {
        try { Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true }); }
        catch (Exception ex) { SupportStatusText.Text = "Unable to open folder: " + ex.Message; }
    }

    private static object ToDisplayResult(DiagnosticResult result) => new
    {
        result.Name,
        result.Details,
        result.Severity,
        SeverityBrush = result.Severity switch
        {
            DiagnosticSeverity.Passed => new SolidColorBrush(Color.FromRgb(74, 222, 128)),
            DiagnosticSeverity.Warning => new SolidColorBrush(Color.FromRgb(250, 204, 21)),
            _ => new SolidColorBrush(Color.FromRgb(248, 113, 113))
        }
    };

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.SkipLaunchSplash = SkipLaunchSplashCheckBox.IsChecked == true;
        _settings.SkipStartupMovies = SkipStartupMoviesCheckBox.IsChecked == true;
        _settings.MinimizeAfterLaunch = MinimizeAfterLaunchCheckBox.IsChecked == true;
        _settings.RestoreAfterGameExit = RestoreAfterGameExitCheckBox.IsChecked == true;
        _settings.EnableDiscordRichPresence = DiscordRichPresenceCheckBox.IsChecked == true;
        _settingsService.Save(_settings);
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left || IsInsideButton(e.OriginalSource as DependencyObject)) return;
        if (e.ClickCount == 2 && ResizeMode != ResizeMode.NoResize) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else DragMove();
    }
    private static bool IsInsideButton(DependencyObject? source) { while (source is not null) { if (source is Button) return true; source = VisualTreeHelper.GetParent(source); } return false; }
    private void MinimizeWindowButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeWindowButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseWindowButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void Window_StateChanged(object? sender, EventArgs e) { if (MaximizeWindowButton is not null) MaximizeWindowButton.Content = WindowState == WindowState.Maximized ? "❐" : "□"; }
}
