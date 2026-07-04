using System.Diagnostics;
using System.IO;
using MHRebornLauncher.Models;

namespace MHRebornLauncher.Services;

public sealed class GameLauncherService
{
    public string BuildArguments(LauncherSettings settings, string emailAddress, string password)
    {
        return string.Join(' ', BuildArgumentArray(settings, emailAddress, password).Select(QuoteIfNeeded));
    }

    public void Launch(string gameExecutablePath, LauncherSettings settings, string emailAddress, string password)
    {
        if (!File.Exists(gameExecutablePath))
            throw new FileNotFoundException("Marvel Heroes executable was not found.", gameExecutablePath);

        string[] args = BuildArgumentArray(settings, emailAddress, password);

        var startInfo = new ProcessStartInfo
        {
            FileName = gameExecutablePath,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(gameExecutablePath) ?? AppContext.BaseDirectory
        };

        foreach (string arg in args)
            startInfo.ArgumentList.Add(arg);

        Process.Start(startInfo);
    }

    private static string[] BuildArgumentArray(LauncherSettings settings, string emailAddress, string password)
    {
        return
        [
            "-nosteam",
            "-robocopy",
            $"-siteconfigurl={settings.SiteConfigUrl}",
            $"-emailaddress={emailAddress}",
            $"-password={password}"
        ];
    }

    private static string QuoteIfNeeded(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "\"\"";

        return value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
    }
}
