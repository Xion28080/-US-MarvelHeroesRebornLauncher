using System.IO;

namespace MHRebornLauncher.Services;

public sealed class GamePathService
{
    private static readonly string[] ExecutableNames =
    [
        "MarvelHeroesOmega.exe",
        "MarvelHeroes2016.exe",
        "MarvelHeroes2015.exe",
        "MarvelGame.exe"
    ];

    public string LauncherDirectory { get; }
    public string GameRootDirectory { get; }
    public string? GameExecutablePath { get; }
    public bool IsGameFound => !string.IsNullOrWhiteSpace(GameExecutablePath) && File.Exists(GameExecutablePath);

    public GamePathService()
    {
        LauncherDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        GameRootDirectory = ResolveGameRoot(LauncherDirectory);
        GameExecutablePath = FindExecutable(GameRootDirectory);
    }

    private static string ResolveGameRoot(string startDirectory)
    {
        if (Directory.Exists(Path.Combine(startDirectory, "UnrealEngine3")))
            return startDirectory;

        // Supports testing from UnrealEngine3\Binaries\Win64 or Win32 too.
        if (ContainsKnownExecutable(startDirectory))
            return Path.GetFullPath(Path.Combine(startDirectory, "..", "..", ".."));

        // Supports launcher placed one folder below the root, but root placement is recommended.
        string parent = Path.GetFullPath(Path.Combine(startDirectory, ".."));
        if (Directory.Exists(Path.Combine(parent, "UnrealEngine3")))
            return parent;

        return startDirectory;
    }

    private static string? FindExecutable(string gameRoot)
    {
        string win64Dir = Path.Combine(gameRoot, "UnrealEngine3", "Binaries", "Win64");
        string win32Dir = Path.Combine(gameRoot, "UnrealEngine3", "Binaries", "Win32");

        string? win64 = FindExecutableInDirectory(win64Dir);
        if (win64 != null)
            return win64;

        return FindExecutableInDirectory(win32Dir);
    }

    private static string? FindExecutableInDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            return null;

        foreach (string executableName in ExecutableNames)
        {
            string path = Path.Combine(directory, executableName);
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static bool ContainsKnownExecutable(string directory)
    {
        if (!Directory.Exists(directory))
            return false;

        return ExecutableNames.Any(name => File.Exists(Path.Combine(directory, name)));
    }
}
