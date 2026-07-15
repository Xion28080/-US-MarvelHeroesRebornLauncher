using System.Windows;
using MHRebornLauncher.Models;
using MHRebornLauncher.Services;

namespace MHRebornLauncher;

public partial class OptionsWindow : Window
{
    private readonly LauncherSettings _settings;
    private readonly SettingsService _settingsService;

    public OptionsWindow(LauncherSettings settings, SettingsService settingsService)
    {
        InitializeComponent();
        _settings = settings;
        _settingsService = settingsService;

        SkipLaunchSplashCheckBox.IsChecked = settings.SkipLaunchSplash;
        SkipStartupMoviesCheckBox.IsChecked = settings.SkipStartupMovies;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.SkipLaunchSplash = SkipLaunchSplashCheckBox.IsChecked == true;
        _settings.SkipStartupMovies = SkipStartupMoviesCheckBox.IsChecked == true;
        _settingsService.Save(_settings);
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
