using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MHRebornLauncher.Models;

namespace MHRebornLauncher.Services;

public sealed class SocialFriendsService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<SocialFriendsResponse> GetAsync(LauncherSettings settings, string accessToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, settings.SocialFriendsUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using HttpResponseMessage response = await _http.SendAsync(request);
            SocialFriendsResponse? payload = await response.Content.ReadFromJsonAsync<SocialFriendsResponse>();
            return payload ?? new SocialFriendsResponse { Error = "The Friends service returned an empty response." };
        }
        catch (Exception ex) { return new SocialFriendsResponse { Error = ex.Message }; }
    }

    public async Task<SocialFriendActionResponse> SubmitActionAsync(LauncherSettings settings, string accessToken, string playerName, string operation)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.SocialFriendActionUrl)
            {
                Content = JsonContent.Create(new { PlayerName = playerName, Operation = operation })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using HttpResponseMessage response = await _http.SendAsync(request);
            SocialFriendActionResponse? payload = await response.Content.ReadFromJsonAsync<SocialFriendActionResponse>();
            return payload ?? new SocialFriendActionResponse { Error = "The friend service returned an empty response." };
        }
        catch (Exception ex) { return new SocialFriendActionResponse { Error = ex.Message }; }
    }

    public async Task<SocialFriendActionResponse> GetActionStatusAsync(LauncherSettings settings, string accessToken, string commandId)
    {
        try
        {
            string url = settings.SocialFriendActionStatusUrl + "?id=" + Uri.EscapeDataString(commandId);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using HttpResponseMessage response = await _http.SendAsync(request);
            SocialFriendActionResponse? payload = await response.Content.ReadFromJsonAsync<SocialFriendActionResponse>();
            return payload ?? new SocialFriendActionResponse { Error = "The friend service returned an empty response." };
        }
        catch (Exception ex) { return new SocialFriendActionResponse { Error = ex.Message }; }
    }
}
