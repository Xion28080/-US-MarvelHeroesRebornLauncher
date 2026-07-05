using System.Net.Http;
using System.Net.Http.Json;
using MHRebornLauncher.Models;

namespace MHRebornLauncher.Services;

public sealed class AuthService
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public async Task<LoginResponse> LoginAsync(LauncherSettings settings, string emailAddress, string password)
    {
        if (!settings.RequireServerLogin)
        {
            return new LoginResponse
            {
                Success = true,
                PlayerName = emailAddress,
                UserLevel = 0
            };
        }

        try
        {
            var request = new LoginRequest
            {
                EmailAddress = emailAddress,
                Password = password
            };

            using HttpResponseMessage response = await _http.PostAsJsonAsync(settings.LoginApiUrl, request);

            LoginResponse? result = await response.Content.ReadFromJsonAsync<LoginResponse>();

            if (result != null)
            {
                return result;
            }

            return new LoginResponse
            {
                Success = false,
                Error = response.IsSuccessStatusCode
                    ? "The login server returned an invalid response."
                    : "Unable to verify login with the server."
            };
        }
        catch (TaskCanceledException)
        {
            return new LoginResponse
            {
                Success = false,
                Error = "The login server timed out. Please try again."
            };
        }
        catch
        {
            return new LoginResponse
            {
                Success = false,
                Error = "Unable to contact the login server."
            };
        }
    }
}
