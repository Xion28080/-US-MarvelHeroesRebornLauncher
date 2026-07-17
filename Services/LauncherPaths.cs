using System.IO;

namespace MHRebornLauncher.Services;

public static class LauncherPaths
{
    public static string DataRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OmegaNode", "MHRebornLauncher");
    public static string LogsDirectory => Path.Combine(DataRoot, "Logs");
    public static string DiagnosticsDirectory => Path.Combine(DataRoot, "Diagnostics");
    public static string CacheDirectory => Path.Combine(DataRoot, "WebView2");
}
