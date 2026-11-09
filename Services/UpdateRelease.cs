using System.Text.Json;

namespace PurgeWin.Services;

public static class UpdateRelease
{
    public const string Endpoint = "https://api.github.com/repos/stvekoulo/purge/releases/latest";
    private const string DownloadPrefix = "https://github.com/stvekoulo/purge/releases/download/";

    public static Version ParseVersion(string value)
    {
        var clean = value.Trim().TrimStart('v', 'V').Split('+')[0];
        if (!Version.TryParse(clean, out var version)) throw new UpdateException(UpdateError.Release);
        return new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
    }

    public static int CompareVersions(string left, string right) => ParseVersion(left).CompareTo(ParseVersion(right));

    public static UpdateInfo? Parse(string json, string currentVersion, string architecture)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()
            || root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var version = ParseVersion(tag);
        if (version <= ParseVersion(currentVersion)) return null;
        var displayVersion = version.Revision == 0 ? version.ToString(3) : version.ToString(4);
        string expected = $"Purge-{displayVersion}-win-{architecture}-Setup.exe";
        UpdateAsset? installer = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() != expected) continue;
                string url = asset.GetProperty("browser_download_url").GetString() ?? "";
                string digest = asset.TryGetProperty("digest", out var d) ? d.GetString() ?? "" : "";
                long size = asset.GetProperty("size").GetInt64();
                if (!IsTrustedUrl(url) || !digest.StartsWith("sha256:", StringComparison.Ordinal)
                    || digest.Length != 71 || !digest[7..].All(Uri.IsHexDigit)
                    || size <= 0 || size > 512L * 1024 * 1024)
                    throw new UpdateException(UpdateError.Integrity);
                installer = new(expected, url, size, digest[7..]);
                break;
            }
        }
        return new(displayVersion, root.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
            root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
            "https://github.com/stvekoulo/purge/releases", installer);
    }

    public static bool IsTrustedUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo)
        && url.StartsWith(DownloadPrefix, StringComparison.Ordinal)
        && uri.AbsolutePath.StartsWith("/stvekoulo/purge/releases/download/", StringComparison.Ordinal)
        && uri.AbsolutePath.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase);
}
