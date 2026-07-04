using System.IO;
using System.Text.Json;
using MHRebornLauncher.Models;

namespace MHRebornLauncher.Services;

public sealed class SettingsService
{
    private readonly string _folderPath;
    private readonly string _filePath;

    public SettingsService()
    {
        _folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Marvel Heroes Reborn Launcher");
        _filePath = Path.Combine(_folderPath, "launcher-settings.json");
    }

    public LauncherSettings LoadOrCreate()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                string json = File.ReadAllText(_filePath);
                LauncherSettings? settings = JsonSerializer.Deserialize<LauncherSettings>(json);
                if (settings != null)
                    return settings;
            }
        }
        catch
        {
            // Fall through and recreate defaults.
        }

        LauncherSettings defaultSettings = new();
        Save(defaultSettings);
        return defaultSettings;
    }

    public void Save(LauncherSettings settings)
    {
        Directory.CreateDirectory(_folderPath);
        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
