using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ImageForge.Core;

public sealed record ToolRelease(string Tag, Version Version, int? Preview) : IComparable<ToolRelease>
{
    public Uri Page => new("https://github.com/sethcroft/ImageForge/releases/tag/" + Tag);
    public int CompareTo(ToolRelease? other)
    {
        if (other is null) return 1;
        var version = Version.CompareTo(other.Version);
        return version != 0 ? version : Preview is null ? (other.Preview is null ? 0 : 1) : other.Preview is null ? -1 : Preview.Value.CompareTo(other.Preview.Value);
    }
    public static ToolRelease? Parse(string? tag)
    {
        var match = Regex.Match(tag ?? "", @"^v?(\d{1,5})\.(\d{1,5})\.(\d{1,5})(?:-preview\.(\d{1,5}))?$", RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        return new(tag!, new(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value)), match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : null);
    }
}

public sealed record ReleaseCatalog(ToolRelease? Stable, ToolRelease? Preview)
{
    public static ReleaseCatalog Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        ToolRelease? stable = null, preview = null;
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("draft", out var draft) || draft.ValueKind != JsonValueKind.False ||
                !item.TryGetProperty("tag_name", out var tag) || tag.ValueKind != JsonValueKind.String || ToolRelease.Parse(tag.GetString()) is not { } release ||
                !item.TryGetProperty("prerelease", out var pre) || pre.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) continue;
            // Accept only the version/tag scheme used by ImageForge; previews must be marked as such.
            if ((release.Preview is not null) != pre.GetBoolean()) continue;
            if (release.Preview is null && release.CompareTo(stable) > 0) stable = release;
            if (release.Preview is not null && release.CompareTo(preview) > 0) preview = release;
        }
        return new(stable, preview);
    }
    public static async Task<ReleaseCatalog> FetchAsync(CancellationToken token = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = 1024 * 1024 };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ImageForge/0.5");
        var json = await client.GetStringAsync("https://api.github.com/repos/sethcroft/ImageForge/releases?per_page=30", token);
        return Parse(json);
    }
}
