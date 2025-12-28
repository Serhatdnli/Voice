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
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<List<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var results = new List<SearchResult>();

        try
        {
            var searchUrl = $"{BaseUrl}/tr/search/?name={WebUtility.UrlEncode(query)}";
            var html = await _httpClient.GetStringAsync(searchUrl, cancellationToken);

            var doc = new HtmlDoc();
            doc.LoadHtml(html);

            var instantNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'instant')]");

            if (instantNodes == null) return results;

            foreach (var node in instantNodes)
            {
                var titleNode = node.SelectSingleNode(".//a[@class='instant-link']");
                var buttonNode = node.SelectSingleNode(".//button[contains(@class, 'small-button')]");

                if (titleNode == null || buttonNode == null) continue;

                var onClickAttr = buttonNode.GetAttributeValue("onclick", "");
                var soundUrl = ExtractSoundUrl(onClickAttr);

                if (string.IsNullOrEmpty(soundUrl)) continue;

                results.Add(new SearchResult
                {
                    Title = WebUtility.HtmlDecode(titleNode.InnerText.Trim()),
                    PageUrl = BaseUrl + titleNode.GetAttributeValue("href", ""),
                    SoundUrl = soundUrl.StartsWith("http") ? soundUrl : BaseUrl + soundUrl
                });
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
            var html = await _httpClient.GetStringAsync($"{BaseUrl}/tr/", cancellationToken);

            var doc = new HtmlDoc();
            doc.LoadHtml(html);

            var instantNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'instant')]");

            if (instantNodes == null) return results;

            foreach (var node in instantNodes.Take(20))
            {
                var titleNode = node.SelectSingleNode(".//a[@class='instant-link']");
                var buttonNode = node.SelectSingleNode(".//button[contains(@class, 'small-button')]");

                if (titleNode == null || buttonNode == null) continue;

                var onClickAttr = buttonNode.GetAttributeValue("onclick", "");
                var soundUrl = ExtractSoundUrl(onClickAttr);

                if (string.IsNullOrEmpty(soundUrl)) continue;

                results.Add(new SearchResult
                {
                    Title = WebUtility.HtmlDecode(titleNode.InnerText.Trim()),
                    PageUrl = BaseUrl + titleNode.GetAttributeValue("href", ""),
                    SoundUrl = soundUrl.StartsWith("http") ? soundUrl : BaseUrl + soundUrl
                });
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
