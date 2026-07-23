using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MHRebornLauncher.Models;

namespace MHRebornLauncher.Services;

public sealed class SocialPresenceService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public async Task<bool> UpdateAsync(LauncherSettings settings, string accessToken, bool online)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return false;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.SocialPresenceUrl)
            {
                Content = JsonContent.Create(new { Online = online })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using HttpResponseMessage response = await _http.SendAsync(request).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            // Presence is optional and must never interrupt launcher use.
            return false;
        }
    }
    public bool UpdateBeforeShutdown(LauncherSettings settings, string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return false;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.SocialPresenceUrl)
            {
                Content = JsonContent.Create(new { Online = false })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using HttpResponseMessage response = _http.Send(request, timeout.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

}
