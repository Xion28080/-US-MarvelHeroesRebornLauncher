using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using MHRebornLauncher.Models;

namespace MHRebornLauncher.Services;

public sealed class DiagnosticPackageService
{
    public string Create(GamePathService paths, LauncherSettings settings, IReadOnlyList<DiagnosticResult> results)
    {
        Directory.CreateDirectory(LauncherPaths.DiagnosticsDirectory);
        string work = Path.Combine(LauncherPaths.DiagnosticsDirectory, $"support-{DateTime.Now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(work);

        StringBuilder summary = new();
        summary.AppendLine("Marvel Heroes Reborn Launcher Support Summary");
        summary.AppendLine($"Created: {DateTime.Now:O}");
        summary.AppendLine($"Launcher version: {Assembly.GetExecutingAssembly().GetName().Version}");
        summary.AppendLine($"Windows: {Environment.OSVersion}");
        summary.AppendLine($"64-bit OS/process: {Environment.Is64BitOperatingSystem}/{Environment.Is64BitProcess}");
        summary.AppendLine($"Launcher path: {paths.LauncherDirectory}");
        summary.AppendLine($"Game root: {paths.GameRootDirectory}");
        summary.AppendLine($"Game executable: {paths.GameExecutablePath ?? "Not found"}");
        summary.AppendLine($"Skip splash: {settings.SkipLaunchSplash}");
        summary.AppendLine($"Skip movies: {settings.SkipStartupMovies}");
        summary.AppendLine($"Minimize after launch: {settings.MinimizeAfterLaunch}");
        summary.AppendLine($"Restore after exit: {settings.RestoreAfterGameExit}");
        summary.AppendLine();
        summary.AppendLine("Checks:");
        foreach (DiagnosticResult result in results) summary.AppendLine($"[{result.Severity}] {result.Name}: {result.Details}");
        File.WriteAllText(Path.Combine(work, "support-summary.txt"), summary.ToString(), Encoding.UTF8);

        if (Directory.Exists(LauncherPaths.LogsDirectory))
            foreach (string file in Directory.GetFiles(LauncherPaths.LogsDirectory, "*.log").OrderByDescending(File.GetLastWriteTime).Take(5))
                File.Copy(file, Path.Combine(work, Path.GetFileName(file)), true);

        foreach (string candidate in GetGameLogDirectories(paths.GameRootDirectory))
        {
            if (!Directory.Exists(candidate)) continue;
            foreach (string file in Directory.GetFiles(candidate, "*.log").OrderByDescending(File.GetLastWriteTime).Take(3))
                File.Copy(file, Path.Combine(work, "game-" + Path.GetFileName(file)), true);
            break;
        }

        string zip = work + ".zip";
        ZipFile.CreateFromDirectory(work, zip, CompressionLevel.Optimal, false);
        Directory.Delete(work, true);
        return zip;
    }

    public static IEnumerable<string> GetGameLogDirectories(string root)
    {
        yield return Path.Combine(root, "UnrealEngine3", "MarvelGame", "Logs");
        yield return Path.Combine(root, "UnrealEngine3", "MarvelGame", "Saved", "Logs");
        yield return Path.Combine(root, "UnrealEngine3", "Logs");
    }
}
