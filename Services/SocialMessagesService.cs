using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MHRebornLauncher.Models;

namespace MHRebornLauncher.Services;

public sealed class SocialMessagesService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<SocialMessagesResponse> GetAsync(LauncherSettings settings, string accessToken, long since)
    {
        try
        {
            string separator = settings.SocialMessagesUrl.Contains('?') ? "&" : "?";
            string url = settings.SocialMessagesUrl + separator + "since=" + Math.Max(0, since);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using HttpResponseMessage response = await _http.SendAsync(request);
            SocialMessagesResponse? payload = await response.Content.ReadFromJsonAsync<SocialMessagesResponse>();
            return payload ?? new SocialMessagesResponse { Error = "The message service returned an empty response." };
        }
        catch (Exception ex)
        {
            return new SocialMessagesResponse { Error = ex.Message };
        }
    }

    public async Task<SocialMessageSendResponse> SendAsync(LauncherSettings settings, string accessToken, string recipientAccountId, string recipientPlayerName, string messageBody)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.SocialMessageSendUrl)
            {
                Content = JsonContent.Create(new
                {
                    RecipientAccountId = recipientAccountId,
                    RecipientPlayerName = recipientPlayerName,
                    MessageBody = messageBody
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using HttpResponseMessage response = await _http.SendAsync(request);
            SocialMessageSendResponse? payload = await response.Content.ReadFromJsonAsync<SocialMessageSendResponse>();
            return payload ?? new SocialMessageSendResponse { Error = "The message service returned an empty response." };
        }
        catch (Exception ex)
        {
            return new SocialMessageSendResponse { Error = ex.Message };
        }
    }

    public async Task<bool> MarkReadAsync(LauncherSettings settings, string accessToken, string otherAccountId)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.SocialMessageReadUrl)
            {
                Content = JsonContent.Create(new { OtherAccountId = otherAccountId })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using HttpResponseMessage response = await _http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<SocialMessageSendResponse> GetSendStatusAsync(LauncherSettings settings, string accessToken, string commandId)
    {
        try
        {
            string url = settings.SocialMessageStatusUrl + "?id=" + Uri.EscapeDataString(commandId);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using HttpResponseMessage response = await _http.SendAsync(request);
            SocialMessageSendResponse? payload = await response.Content.ReadFromJsonAsync<SocialMessageSendResponse>();
            return payload ?? new SocialMessageSendResponse { Error = "The message status service returned an empty response." };
        }
        catch (Exception ex)
        {
            return new SocialMessageSendResponse { Error = ex.Message };
        }
    }
}
