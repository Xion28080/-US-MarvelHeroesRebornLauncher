using System.Net.Http;
using System.Net.Http.Json;
using MHRebornLauncher.Models;

namespace MHRebornLauncher.Services;

public sealed class PortalService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<PortalTokenResponse> CreatePortalLoginAsync(LauncherSettings settings, string emailAddress, string password)
    {
        try
        {
            using HttpResponseMessage response = await _http.PostAsJsonAsync(
                settings.PortalTokenApiUrl,
                new PortalTokenRequest { EmailAddress = emailAddress, Password = password });

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
