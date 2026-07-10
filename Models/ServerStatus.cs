namespace MHRebornLauncher.Models;

public sealed class ServerStatus
{
    public long StartupTime { get; set; }
    public long CurrentTime { get; set; }
    public int PlayerManagerPlayers { get; set; }
    public int GisPlayers { get; set; }

    public int PlayerCount => Math.Max(PlayerManagerPlayers, GisPlayers);
}
