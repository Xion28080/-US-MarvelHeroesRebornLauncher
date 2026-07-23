using System.Net.Http;
using System.Net.Http.Headers;
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

        return await PostAsync(settings.LoginApiUrl, new LoginRequest
        {
            EmailAddress = emailAddress,
            Password = password
        });
    }

    public async Task<LoginResponse> RefreshAsync(LauncherSettings settings, string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return new LoginResponse { Success = false, Error = "No saved launcher session is available." };

        return await PostAsync(settings.SessionRefreshApiUrl, new SessionRefreshRequest
        {
            RefreshToken = refreshToken
        });
    }

    public async Task RevokeAsync(LauncherSettings settings, string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.SessionRevokeApiUrl)
            {
                Content = JsonContent.Create(new { })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using HttpResponseMessage _ = await _http.SendAsync(request);
        }
        catch
        {
            // Local sign-out must still complete if the server cannot be reached.
        }
    }

    private async Task<LoginResponse> PostAsync<T>(string url, T payload)
    {
        try
        {
            using HttpResponseMessage response = await _http.PostAsJsonAsync(url, payload);
            LoginResponse? result = await response.Content.ReadFromJsonAsync<LoginResponse>();

            if (result != null)
                return result;

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
