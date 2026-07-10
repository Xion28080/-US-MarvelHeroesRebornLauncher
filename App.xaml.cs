using System.Windows;
using MHRebornLauncher.Services;

namespace MHRebornLauncher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var settings = new SettingsService().LoadOrCreate();
        var login = new LoginWindow(settings);
        bool? result = login.ShowDialog();

        if (result != true || login.AuthenticatedAccount is null)
        {
            Shutdown();
            return;
        }

        var main = new MainWindow(settings, login.AuthenticatedEmail, login.AuthenticatedPassword, login.AuthenticatedAccount);
        MainWindow = main;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        main.Show();
    }
}
