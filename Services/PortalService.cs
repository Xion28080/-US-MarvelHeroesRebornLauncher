using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MHRebornLauncher.Models;

namespace MHRebornLauncher.Services;

public sealed class PortalService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<PortalTokenResponse> CreatePortalLoginAsync(LauncherSettings settings, string accessToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.PortalTokenApiUrl)
            {
                Content = JsonContent.Create(new { })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using HttpResponseMessage response = await _http.SendAsync(request);
            PortalTokenResponse? result = await response.Content.ReadFromJsonAsync<PortalTokenResponse>();
            return result ?? new PortalTokenResponse { Success = false, Error = "The portal returned an invalid response." };
        }
        catch (TaskCanceledException)
        {
            return new PortalTokenResponse { Success = false, Error = "The portal request timed out." };
        }
        catch
        {
            return new PortalTokenResponse { Success = false, Error = "Unable to contact the Account Portal." };
        }
    }
}
