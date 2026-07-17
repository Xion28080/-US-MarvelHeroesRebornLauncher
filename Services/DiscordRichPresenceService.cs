using DiscordRPC;

namespace MHRebornLauncher.Services;

public sealed class DiscordRichPresenceService : IDisposable
{
    private const string ApplicationId = "1527451603173376071";
    private const string LargeImageKey = "omeganode";
    private readonly DateTime _startedAtUtc = DateTime.UtcNow;
    private DiscordRpcClient? _client;
    private bool _enabled;
    private bool _disposed;

    public void SetEnabled(bool enabled)
    {
        if (_disposed) return;
        _enabled = enabled;

        if (!enabled)
        {
            try { _client?.ClearPresence(); } catch { }
            try { _client?.Dispose(); } catch { }
            _client = null;
            return;
        }

        EnsureInitialized();
    }

    public void Update(bool gameRunning, bool serverOnline, string? activeEventName)
    {
        if (_disposed || !_enabled) return;
        EnsureInitialized();
        if (_client is null) return;

        string details = gameRunning ? "In Game" : "Browsing the launcher";
        string state = !string.IsNullOrWhiteSpace(activeEventName)
            ? $"{activeEventName} Active"
            : serverOnline ? "Server Online" : "Server Offline";

        try
        {
            _client.SetPresence(new RichPresence
            {
                Details = details,
                State = state,
                Timestamps = new Timestamps { Start = _startedAtUtc },
                Assets = new Assets
                {
                    LargeImageKey = LargeImageKey,
                    LargeImageText = "[US] Marvel Heroes Reborn [Private Server]"
                },
                Buttons =
                [
                    new Button { Label = "Official Website", Url = "https://play.omeganode.org" },
                    new Button { Label = "Join Discord", Url = "https://discord.gg/yU9yQ3pq7v" }
                ]
            });
        }
        catch (Exception ex)
        {
            LogService.Error("Discord Rich Presence update failed", ex);
        }
    }

    private void EnsureInitialized()
    {
        if (_client is not null || !_enabled || _disposed) return;
        try
        {
            _client = new DiscordRpcClient(ApplicationId)
            {
                SkipIdenticalPresence = true
            };
            _client.Initialize();
            LogService.Info("Discord Rich Presence initialized.");
        }
        catch (Exception ex)
        {
            LogService.Error("Discord Rich Presence initialization failed", ex);
            try { _client?.Dispose(); } catch { }
            _client = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _client?.ClearPresence(); } catch { }
        try { _client?.Dispose(); } catch { }
        _client = null;
    }
}
