using System.Net.Http;
using System.Net.Http.Json;
using MHRebornLauncher.Models;

namespace MHRebornLauncher.Services;

public sealed class EventStatusService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public async Task<EventStatusResponse?> GetStatusAsync(string url)
    {
        try
        {
            EventStatusResponse? result = await _http.GetFromJsonAsync<EventStatusResponse>(url);
            return result is { Ok: true } ? result : null;
        }
        catch
        {
            return null;
        }
    }
}
