using System.Diagnostics;
using System.IO.Compression;

namespace MHRebornLauncher.Updater;

internal static class Program
{
    private const string LauncherFileName = "MHRebornLauncher.exe";

    private static int Main(string[] args)
    {
        try
        {
            Dictionary<string, string> options = ParseArgs(args);

            string packagePath = GetRequired(options, "package");
            string installDir = GetRequired(options, "install-dir");
            string launcherPath = GetRequired(options, "launcher");
            bool restart = options.ContainsKey("restart");

            if (options.TryGetValue("pid", out string? pidText) && int.TryParse(pidText, out int pid))
            {
                WaitForProcessToExit(pid, TimeSpan.FromSeconds(30));
            }

            Thread.Sleep(800);

            if (!Directory.Exists(installDir))
                throw new DirectoryNotFoundException("Install directory was not found: " + installDir);

            if (!File.Exists(packagePath))
                throw new FileNotFoundException("Update package was not found.", packagePath);

            string tempExtractDir = Path.Combine(Path.GetTempPath(), "MHRebornLauncherUpdate", "extract-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempExtractDir);

            try
            {
                string sourceDir;

                if (packagePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    ZipFile.ExtractToDirectory(packagePath, tempExtractDir, true);
                    string? extractedLauncher = Directory
                        .EnumerateFiles(tempExtractDir, LauncherFileName, SearchOption.AllDirectories)
                        .FirstOrDefault();

                    if (string.IsNullOrWhiteSpace(extractedLauncher))
                        throw new FileNotFoundException("The update package did not contain " + LauncherFileName + ".");

                    sourceDir = Path.GetDirectoryName(extractedLauncher) ?? tempExtractDir;
                }
                else if (Path.GetFileName(packagePath).Equals(LauncherFileName, StringComparison.OrdinalIgnoreCase)
                    || packagePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    sourceDir = tempExtractDir;
                    File.Copy(packagePath, Path.Combine(sourceDir, LauncherFileName), true);
                }
                else
                {
                    throw new InvalidOperationException("Unsupported update package type: " + Path.GetExtension(packagePath));
                }

                CopyUpdateFiles(sourceDir, installDir);
            }
            finally
            {
                TryDeleteDirectory(tempExtractDir);
                TryDeleteFile(packagePath);
            }

            if (restart && File.Exists(launcherPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = launcherPath,
                    WorkingDirectory = installDir,
                    UseShellExecute = true
                });
            }

            return 0;
        }
        catch (Exception ex)
        {
            string logPath = Path.Combine(Path.GetTempPath(), "MHRebornLauncherUpdate", "updater-error.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllText(logPath, ex.ToString());
            return 1;
        }
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
                continue;

            string key = arg[2..];
            if (key.Equals("restart", StringComparison.OrdinalIgnoreCase))
            {
                options[key] = "true";
                continue;
            }

            if (i + 1 >= args.Length)
                throw new ArgumentException("Missing value for argument " + arg);

            options[key] = args[++i];
        }

        return options;
    }

    private static string GetRequired(Dictionary<string, string> options, string key)
    {
        if (!options.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Missing required argument --" + key);

        return value;
    }

    private static void WaitForProcessToExit(int pid, TimeSpan timeout)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            process.WaitForExit((int)timeout.TotalMilliseconds);
        }
        catch
        {
            // The launcher is already closed or the process could not be accessed.
        }
    }

    private static void CopyUpdateFiles(string sourceDir, string installDir)
    {
        string currentUpdaterPath = Environment.ProcessPath ?? "";
        string currentUpdaterName = Path.GetFileName(currentUpdaterPath);

        foreach (string sourceFile in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDir, sourceFile);
            string targetFile = Path.Combine(installDir, relativePath);
            string targetName = Path.GetFileName(targetFile);

            // Do not try to overwrite the updater while it is running.
            if (targetName.Equals(currentUpdaterName, StringComparison.OrdinalIgnoreCase))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);

            string backupFile = targetFile + ".bak";
            TryDeleteFile(backupFile);

            bool backupCreated = false;

            if (File.Exists(targetFile))
            {
                try
                {
                    File.Copy(targetFile, backupFile, true);
                    backupCreated = true;
                }
                catch
                {
                    // Backup is best-effort only.
                }
            }

            File.Copy(sourceFile, targetFile, true);

            if (backupCreated)
            {
                TryDeleteFile(backupFile);
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
