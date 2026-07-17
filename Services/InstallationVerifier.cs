using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Microsoft.Web.WebView2.Core;
using MHRebornLauncher.Models;

namespace MHRebornLauncher.Services;

public sealed class InstallationVerifier
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public async Task<List<DiagnosticResult>> RunAsync(GamePathService paths, LauncherSettings settings)
    {
        List<DiagnosticResult> results = [];
        Add(results, "Launcher location", Directory.Exists(Path.Combine(paths.LauncherDirectory, "UnrealEngine3")), "Launcher is beside UnrealEngine3.", "Place the launcher in the Marvel Heroes root folder beside UnrealEngine3.");
        Add(results, "Updater", File.Exists(Path.Combine(paths.LauncherDirectory, "MHRebornLauncher.Updater.exe")), "Updater found.", "MHRebornLauncher.Updater.exe is missing.");
        Add(results, "Game executable", paths.IsGameFound, paths.GameExecutablePath ?? "Game executable found.", "No supported Marvel Heroes executable was found.");
        Add(results, "UnrealEngine3 folder", Directory.Exists(Path.Combine(paths.GameRootDirectory, "UnrealEngine3")), "Required game folder found.", "UnrealEngine3 is missing.");

        if (paths.IsGameFound)
        {
            string rawVersion = FileVersionInfo.GetVersionInfo(paths.GameExecutablePath!).FileVersion ?? "Unknown";
            string version = NormalizeVersion(rawVersion);
            bool expected = version == "Unknown" || VersionsMatch(version, "1.52.0.1700");
            results.Add(new("Client version", expected ? DiagnosticSeverity.Passed : DiagnosticSeverity.Warning, $"Detected: {version}. Expected client: 1.52.0.1700 (2.16a)."));
        }

        try
        {
            string? webViewVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
            results.Add(new("WebView2 Runtime", string.IsNullOrWhiteSpace(webViewVersion) ? DiagnosticSeverity.Failed : DiagnosticSeverity.Passed, string.IsNullOrWhiteSpace(webViewVersion) ? "Microsoft Edge WebView2 Runtime was not found." : $"Installed: {webViewVersion}"));
        }
        catch (Exception ex) { results.Add(new("WebView2 Runtime", DiagnosticSeverity.Failed, ex.Message)); }

        await TestEndpoint(results, "Website", "https://play.omeganode.org/");
        await TestEndpoint(results, "Login service", settings.LoginApiUrl, allowMethodFailure: true);
        await TestEndpoint(results, "Server status", settings.ServerStatusUrl);
        await TestEndpoint(results, "Dashboard service", settings.PlayerDashboardUrl, allowMethodFailure: true);
        return results;
    }

    private async Task TestEndpoint(List<DiagnosticResult> results, string name, string url, bool allowMethodFailure = false)
    {
        try
        {
            using HttpResponseMessage response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            bool methodRequired = allowMethodFailure && response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed;
            bool reachable = response.IsSuccessStatusCode || methodRequired;
            string message = methodRequired
                ? $"Service reachable; POST required (HTTP {(int)response.StatusCode} {response.ReasonPhrase})."
                : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
            results.Add(new(name, reachable ? DiagnosticSeverity.Passed : DiagnosticSeverity.Failed, message));
        }
        catch (Exception ex) { results.Add(new(name, DiagnosticSeverity.Failed, ex.Message)); }
    }

    private static string NormalizeVersion(string? version)
    {
        return string.IsNullOrWhiteSpace(version)
            ? "Unknown"
            : version.Trim().Replace(',', '.');
    }

    private static bool VersionsMatch(string? detected, string expected)
    {
        string normalizedDetected = NormalizeVersion(detected);
        string normalizedExpected = NormalizeVersion(expected);

        return Version.TryParse(normalizedDetected, out Version? detectedVersion)
            && Version.TryParse(normalizedExpected, out Version? expectedVersion)
            && detectedVersion == expectedVersion;
    }

    private static void Add(List<DiagnosticResult> list, string name, bool passed, string ok, string fail) => list.Add(new(name, passed ? DiagnosticSeverity.Passed : DiagnosticSeverity.Failed, passed ? ok : fail));
}
