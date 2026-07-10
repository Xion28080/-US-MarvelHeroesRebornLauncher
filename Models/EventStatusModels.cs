using System.Text.Json.Serialization;

namespace MHRebornLauncher.Models;

public sealed class EventStatusResponse
{
    public bool Ok { get; set; }
    public List<LiveEvent> ActiveEvents { get; set; } = [];
}

public sealed class LiveEvent
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "Event";
    public string Detail { get; set; } = "";
    public string? EndsAt { get; set; }
    public string? EndsAtHuman { get; set; }
}
