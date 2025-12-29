using System.Net;
using VoiceBoard.Models;
using HtmlDoc = HtmlAgilityPack.HtmlDocument;

namespace VoiceBoard.Services;

public class MyInstantsService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://www.myinstants.com";

    public MyInstantsService()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = true };
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en;q=0.8");
    }

    public async Task<List<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var results = new List<SearchResult>();

        try
        {
            // New URL structure: /en/search/?name=query
            var searchUrl = $"{BaseUrl}/en/search/?name={WebUtility.UrlEncode(query)}";
            var html = await _httpClient.GetStringAsync(searchUrl, cancellationToken);

            // Use regex to find all sound URLs directly from HTML
            var matches = System.Text.RegularExpressions.Regex.Matches(
                html,
                @"<div[^>]*class=""[^""]*small-button[^""]*""[^>]*onmousedown=""play\('([^']+)'\)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Also find titles - they're in anchor tags with class containing "instant-link"
            var titleMatches = System.Text.RegularExpressions.Regex.Matches(
                html,
                @"<a[^>]*class=""[^""]*instant-link[^""]*""[^>]*href=""([^""]+)""[^>]*>([^<]+)</a>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var titles = new List<(string href, string title)>();
            foreach (System.Text.RegularExpressions.Match m in titleMatches)
            {
                titles.Add((m.Groups[1].Value, WebUtility.HtmlDecode(m.Groups[2].Value.Trim())));
            }

            int index = 0;
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var soundPath = match.Groups[1].Value;
                var soundUrl = soundPath.StartsWith("http") ? soundPath : BaseUrl + soundPath;

                var title = index < titles.Count ? titles[index].title : $"Sound {index + 1}";
                var pageUrl = index < titles.Count ? BaseUrl + titles[index].href : "";

                results.Add(new SearchResult
                {
                    Title = title,
                    PageUrl = pageUrl,
                    SoundUrl = soundUrl
                });
                index++;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
        }

        return results;
    }

    public async Task<List<SearchResult>> GetTrendingAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<SearchResult>();

        try
        {
            // New URL structure: /en/index/tr/ for Turkish trending
            var html = await _httpClient.GetStringAsync($"{BaseUrl}/en/index/tr/", cancellationToken);

            // Use regex to find all sound URLs directly from HTML
            var matches = System.Text.RegularExpressions.Regex.Matches(
                html,
                @"<div[^>]*class=""[^""]*small-button[^""]*""[^>]*onmousedown=""play\('([^']+)'\)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Also find titles
            var titleMatches = System.Text.RegularExpressions.Regex.Matches(
                html,
                @"<a[^>]*class=""[^""]*instant-link[^""]*""[^>]*href=""([^""]+)""[^>]*>([^<]+)</a>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var titles = new List<(string href, string title)>();
            foreach (System.Text.RegularExpressions.Match m in titleMatches)
            {
                titles.Add((m.Groups[1].Value, WebUtility.HtmlDecode(m.Groups[2].Value.Trim())));
            }

            int index = 0;
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (index >= 20) break; // Limit to 20

                var soundPath = match.Groups[1].Value;
                var soundUrl = soundPath.StartsWith("http") ? soundPath : BaseUrl + soundPath;

                var title = index < titles.Count ? titles[index].title : $"Sound {index + 1}";
                var pageUrl = index < titles.Count ? BaseUrl + titles[index].href : "";

                results.Add(new SearchResult
                {
                    Title = title,
                    PageUrl = pageUrl,
                    SoundUrl = soundUrl
                });
                index++;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Trending error: {ex.Message}");
        }

        return results;
    }

    private string ExtractSoundUrl(string onClickAttr)
    {
        // onclick="play('/media/sounds/sample.mp3')"
        var match = System.Text.RegularExpressions.Regex.Match(onClickAttr, @"play\('([^']+)'\)");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    public async Task<string> DownloadSoundAsync(string url, string savePath,
        IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var buffer = new byte[8192];
        var totalRead = 0L;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        while (true)
        {
            var read = await contentStream.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;

            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            totalRead += read;

            if (totalBytes > 0)
            {
                progress?.Report((int)(totalRead * 100 / totalBytes));
            }
        }

        return savePath;
    }
}
