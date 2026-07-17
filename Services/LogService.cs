using System.IO;
using System.Text;

namespace MHRebornLauncher.Services;

public static class LogService
{
    private static readonly object Sync = new();
    private static string LogPath => Path.Combine(LauncherPaths.LogsDirectory, $"launcher-{DateTime.Now:yyyy-MM-dd}.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", ex is null ? message : $"{message}{Environment.NewLine}{ex}");

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(LauncherPaths.LogsDirectory);
            string safe = Redact(message);
            lock (Sync)
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {safe}{Environment.NewLine}", Encoding.UTF8);
        }
        catch { }
    }

    private static string Redact(string value)
    {
        foreach (string marker in new[] { "password=", "token=", "authorization:" })
        {
            int index;
            while ((index = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                int end = value.IndexOfAny([' ', '&', '\r', '\n'], index + marker.Length);
                if (end < 0) end = value.Length;
                value = value[..(index + marker.Length)] + "[REDACTED]" + value[end..];
            }
        }
        return value;
    }
}
