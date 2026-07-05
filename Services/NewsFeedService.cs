using System.Net.Http;
using System.Net.Http.Json;
using MHRebornLauncher.Models;

namespace MHRebornLauncher.Services;

public sealed class NewsFeedService
{
    private readonly HttpClient _http = new();

    public async Task<List<NewsPost>> GetNewsAsync(string newsFeedUrl)
    {
        try
        {
            List<NewsPost>? posts = await _http.GetFromJsonAsync<List<NewsPost>>(newsFeedUrl);
            return posts ?? [];
        }
        catch
        {
            return
            [
                new NewsPost
                {
                    Title = "News feed unavailable",
                    Date = DateTime.Now.ToString("yyyy-MM-dd"),
                    Category = "Launcher",
                    Summary = "The launcher could not load the website news feed. Check that /launcher/news-feed.php exists on the server.",
                    Url = "https://play.omeganode.org/updates.php"
                }
            ];
        }
    }
}
