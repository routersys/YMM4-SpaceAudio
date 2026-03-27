using SpaceAudio.Interfaces;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace SpaceAudio.Services;

public sealed class UpdateService : IUpdateService
{
    public async Task<(bool HasUpdate, string UpdateUrl)> CheckForUpdatesAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "SpaceAudio");
            var res = await client.GetAsync("https://api.github.com/repos/routersys/YMM4-SpaceAudio/releases/latest");
            if (!res.IsSuccessStatusCode)
            {
                res = await client.GetAsync("https://manjubox.net/api/ymm4plugins/github/detail/routersys/YMM4-SpaceAudio");
            }

            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("tag_name", out var tagProp) &&
                    doc.RootElement.TryGetProperty("html_url", out var urlProp))
                {
                    string tagName = tagProp.GetString() ?? string.Empty;
                    string htmlUrl = urlProp.GetString() ?? string.Empty;
                    if (tagName.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                    {
                        var remoteStr = tagName.Substring(1);
                        if (Version.TryParse(remoteStr, out var remoteVer) &&
                            Assembly.GetExecutingAssembly().GetName().Version is Version currentVer)
                        {
                            if (remoteVer > currentVer)
                            {
                                return (true, htmlUrl);
                            }
                        }
                    }
                }
            }
        }
        catch
        {
        }
        return (false, string.Empty);
    }
}
