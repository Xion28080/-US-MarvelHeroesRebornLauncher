using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using MHRebornLauncher.Models;

namespace MHRebornLauncher.Services;

public sealed class UpdateService
{
    private readonly HttpClient _http = new();

    public UpdateService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MHRebornLauncher-Updater/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public string CurrentVersion
    {
        get
        {
            string? informational = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
                return informational.Split('+')[0].Trim();

            return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        }
    }

    public async Task<LauncherUpdateInfo?> CheckForUpdateAsync(LauncherSettings settings)
    {
        if (!settings.EnableLauncherUpdates)
            return null;

        string latestReleaseUrl = $"https://api.github.com/repos/{settings.GitHubOwner}/{settings.GitHubRepository}/releases/latest";

        using HttpResponseMessage response = await _http.GetAsync(latestReleaseUrl);
        if (!response.IsSuccessStatusCode)
            return null;

        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument doc = await JsonDocument.ParseAsync(stream);
        JsonElement root = doc.RootElement;

        string tagName = root.TryGetProperty("tag_name", out JsonElement tagElement) ? tagElement.GetString() ?? "" : "";
        string latestVersion = NormalizeVersion(tagName);
        string currentVersion = NormalizeVersion(CurrentVersion);

        if (!IsNewerVersion(latestVersion, currentVersion))
            return null;

        if (!root.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        JsonElement? selectedAsset = null;
        foreach (JsonElement asset in assets.EnumerateArray())
        {
            string assetName = asset.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() ?? "" : "";
            if (IsPreferredAsset(assetName, settings.ReleaseAssetName, settings.ReleaseAssetPrefix))
            {
                selectedAsset = asset;
                break;
            }
        }

        if (selectedAsset == null)
            return null;

        JsonElement selected = selectedAsset.Value;
        string downloadUrl = selected.TryGetProperty("browser_download_url", out JsonElement downloadElement)
            ? downloadElement.GetString() ?? ""
            : "";

        if (string.IsNullOrWhiteSpace(downloadUrl))
            return null;

        return new LauncherUpdateInfo
        {
            CurrentVersion = currentVersion,
            LatestVersion = latestVersion,
            ReleaseName = root.TryGetProperty("name", out JsonElement releaseNameElement) ? releaseNameElement.GetString() ?? tagName : tagName,
            ReleaseNotes = root.TryGetProperty("body", out JsonElement bodyElement) ? bodyElement.GetString() ?? "" : "",
            AssetName = selected.TryGetProperty("name", out JsonElement assetNameElement) ? assetNameElement.GetString() ?? "" : "",
            DownloadUrl = downloadUrl,
            HtmlUrl = root.TryGetProperty("html_url", out JsonElement htmlElement) ? htmlElement.GetString() ?? "" : ""
        };
    }

    public async Task<string> DownloadUpdateAsync(LauncherUpdateInfo updateInfo, IProgress<double>? progress = null)
    {
        string tempFolder = Path.Combine(Path.GetTempPath(), "MHRebornLauncherUpdate");
        Directory.CreateDirectory(tempFolder);

        string extension = Path.GetExtension(updateInfo.AssetName);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".zip";

        string packagePath = Path.Combine(tempFolder, "MHRebornLauncherUpdate" + extension);
        if (File.Exists(packagePath))
            File.Delete(packagePath);

        using HttpResponseMessage response = await _http.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;
        await using Stream input = await response.Content.ReadAsStreamAsync();
        await using FileStream output = File.Create(packagePath);

        byte[] buffer = new byte[81920];
        long totalRead = 0;
        int read;

        while ((read = await input.ReadAsync(buffer)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read));
            totalRead += read;

            if (totalBytes.HasValue && totalBytes.Value > 0)
            {
                progress?.Report((double)totalRead / totalBytes.Value);
            }
        }

        FileInfo info = new(packagePath);
        if (!info.Exists || info.Length < 1024)
            throw new InvalidOperationException("The downloaded update package appears to be invalid.");

        return packagePath;
    }

    public void StartUpdaterAndExit(string packagePath)
    {
        string installDir = AppContext.BaseDirectory;
        string updaterPath = Path.Combine(installDir, "MHRebornLauncher.Updater.exe");
        string launcherPath = Environment.ProcessPath ?? Path.Combine(installDir, "MHRebornLauncher.exe");
        int processId = Environment.ProcessId;

        if (!File.Exists(updaterPath))
        {
            throw new FileNotFoundException("The updater helper was not found. Reinstall the launcher package and try again.", updaterPath);
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = updaterPath,
            UseShellExecute = true,
            WorkingDirectory = installDir
        };

        startInfo.ArgumentList.Add("--package");
        startInfo.ArgumentList.Add(packagePath);
        startInfo.ArgumentList.Add("--install-dir");
        startInfo.ArgumentList.Add(installDir);
        startInfo.ArgumentList.Add("--launcher");
        startInfo.ArgumentList.Add(launcherPath);
        startInfo.ArgumentList.Add("--pid");
        startInfo.ArgumentList.Add(processId.ToString());
        startInfo.ArgumentList.Add("--restart");

        Process.Start(startInfo);
        Application.Current.Shutdown();
    }

    private static bool IsPreferredAsset(string assetName, string exactName, string prefix)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return false;

        if (!string.IsNullOrWhiteSpace(exactName) && assetName.Equals(exactName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(prefix)
            && assetName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return assetName.Equals("MHRebornLauncher.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeVersion(string version)
    {
        version = version.Trim();
        if (version.StartsWith('v') || version.StartsWith('V'))
            version = version[1..];

        int plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
            version = version[..plusIndex];

        int dashIndex = version.IndexOf('-');
        if (dashIndex >= 0)
            version = version[..dashIndex];

        return version.Trim();
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        if (!Version.TryParse(latest, out Version? latestVersion))
            return false;

        if (!Version.TryParse(current, out Version? currentVersion))
            return false;

        return latestVersion > currentVersion;
    }
}
