namespace MHRebornLauncher.Models;

public sealed class NewsPost
{
    public string Title { get; set; } = "";
    public string Date { get; set; } = "";
    public string Category { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Body { get; set; } = "";
    public string Url { get; set; } = "";

    public string PreviewText => !string.IsNullOrWhiteSpace(Summary) ? Summary : Body;
    public bool HasUrl => !string.IsNullOrWhiteSpace(Url);
}
