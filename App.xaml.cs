using System.Windows;
using System.Windows.Threading;
using MHRebornLauncher.Services;

namespace MHRebornLauncher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LogService.Info("Launcher starting.");
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogService.Error("Unhandled application exception", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => { LogService.Error("Unobserved task exception", args.Exception); args.SetObserved(); };

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var settings = new SettingsService().LoadOrCreate();
        var login = new LoginWindow(settings);
        bool? result = login.ShowDialog();
        if (result != true || login.AuthenticatedAccount is null)
        {
            LogService.Info("Launcher closed before authentication completed.");
            Shutdown();
            return;
        }

        LogService.Info("Authentication succeeded; opening main window.");
        var main = new MainWindow(settings, login.AuthenticatedEmail, login.AuthenticatedPassword, login.AuthenticatedAccount);
        MainWindow = main;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        main.Show();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogService.Error("Unexpected launcher error", e.Exception);
        MessageBox.Show("The launcher encountered an unexpected error. A log was saved in the launcher Support folder.", "Marvel Heroes Reborn Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
