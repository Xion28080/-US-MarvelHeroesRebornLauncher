using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MHRebornLauncher.Models;

namespace MHRebornLauncher.Services;

public sealed class PlayerDashboardService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<PlayerDashboardResponse> GetAsync(LauncherSettings settings, string accessToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.PlayerDashboardUrl)
            {
                Content = JsonContent.Create(new
                {
                    PreviewMode = settings.DashboardPreviewMode
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using HttpResponseMessage response = await _http.SendAsync(request);
            PlayerDashboardResponse? payload = await response.Content.ReadFromJsonAsync<PlayerDashboardResponse>();
            return payload ?? new PlayerDashboardResponse { Error = "The dashboard returned an empty response." };
        }
        catch (Exception ex)
        {
            return new PlayerDashboardResponse { Error = ex.Message };
        }
    }
}
