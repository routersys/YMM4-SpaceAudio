using SpaceAudio.Interfaces;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace SpaceAudio.Services;

public sealed class UpdateService : IUpdateService
{
    private static readonly HttpClient SharedClient = CreateClient();

    private const string PrimaryUrl = "https://api.github.com/repos/routersys/YMM4-SpaceAudio/releases/latest";
    private const string FallbackUrl = "https://manjubox.net/api/ymm4plugins/github/detail/routersys/YMM4-SpaceAudio";

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "SpaceAudio");
        return client;
    }

    public async Task<(bool HasUpdate, string UpdateUrl)> CheckForUpdatesAsync()
    {
        try
        {
            var json = await FetchLatestReleaseJsonAsync().ConfigureAwait(false);
            if (json is null) return (false, string.Empty);
            return ParseRelease(json);
        }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }
        catch (JsonException) { }
        return (false, string.Empty);
    }

    private static async Task<string?> FetchLatestReleaseJsonAsync()
    {
        var res = await SharedClient.GetAsync(PrimaryUrl).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
            res = await SharedClient.GetAsync(FallbackUrl).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    private static (bool HasUpdate, string UpdateUrl) ParseRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("tag_name", out var tagProp) ||
            !doc.RootElement.TryGetProperty("html_url", out var urlProp))
            return (false, string.Empty);

        string tagName = tagProp.GetString() ?? string.Empty;
        string htmlUrl = urlProp.GetString() ?? string.Empty;

        if (!tagName.StartsWith('v')) return (false, string.Empty);
        string remoteStr = tagName[1..];
        if (!Version.TryParse(remoteStr, out var remoteVer)) return (false, string.Empty);
        if (Assembly.GetExecutingAssembly().GetName().Version is not { } currentVer) return (false, string.Empty);
        return remoteVer > currentVer ? (true, htmlUrl) : (false, string.Empty);
    }
}
