using System.Net.Http;
using System.Net.Http.Json;
using MHRebornLauncher.Models;

namespace MHRebornLauncher.Services;

public sealed class PlayerDashboardService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<PlayerDashboardResponse> GetAsync(LauncherSettings settings, string email, string password)
    {
        try
        {
            using HttpResponseMessage response = await _http.PostAsJsonAsync(settings.PlayerDashboardUrl, new
            {
                EmailAddress = email,
                Password = password,
                PreviewMode = settings.DashboardPreviewMode
            });

            PlayerDashboardResponse? payload = await response.Content.ReadFromJsonAsync<PlayerDashboardResponse>();
            return payload ?? new PlayerDashboardResponse { Error = "The dashboard returned an empty response." };
        }
        catch (Exception ex)
        {
            return new PlayerDashboardResponse { Error = ex.Message };
        }
    }
}
