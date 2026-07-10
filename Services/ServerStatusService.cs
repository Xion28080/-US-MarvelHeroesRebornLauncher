using System.Net.Http;
using System.Net.Http.Json;
using MHRebornLauncher.Models;

namespace MHRebornLauncher.Services;

public sealed class ServerStatusService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public async Task<ServerStatus?> GetStatusAsync(string serverStatusUrl)
    {
        try
        {
            ServerStatus? status = await _http.GetFromJsonAsync<ServerStatus>(serverStatusUrl);
            if (status == null || status.StartupTime <= 0 || status.CurrentTime <= 0)
                return null;

            return status;
        }
        catch
        {
            return null;
        }
    }
}
