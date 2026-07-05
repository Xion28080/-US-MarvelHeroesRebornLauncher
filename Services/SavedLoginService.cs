using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MHRebornLauncher.Models;

namespace MHRebornLauncher.Services;

public sealed class SavedLoginService
{
    private readonly string _folderPath;
    private readonly string _filePath;

    public SavedLoginService()
    {
        _folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Marvel Heroes Reborn Launcher");
        _filePath = Path.Combine(_folderPath, "saved-login.dat");
    }

    public void Save(SavedLogin login)
    {
        Directory.CreateDirectory(_folderPath);

        string json = JsonSerializer.Serialize(login);
        byte[] plainBytes = Encoding.UTF8.GetBytes(json);
        byte[] protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

        File.WriteAllBytes(_filePath, protectedBytes);
    }

    public SavedLogin? Load()
    {
        if (!File.Exists(_filePath))
            return null;

        try
        {
            byte[] protectedBytes = File.ReadAllBytes(_filePath);
            byte[] plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            string json = Encoding.UTF8.GetString(plainBytes);
            return JsonSerializer.Deserialize<SavedLogin>(json);
        }
        catch
        {
            Clear();
            return null;
        }
    }

    public void Clear()
    {
        if (File.Exists(_filePath))
            File.Delete(_filePath);
    }
}
