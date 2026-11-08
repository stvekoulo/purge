using System;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using PurgeWin.Utilities;

namespace PurgeWin.Services;

public record UpdateInfo(
    string LatestVersion,
    string ReleaseTitle,
    string ReleaseNotes,
    string ReleaseUrl,
    string? InstallerDownloadUrl,
    bool IsNewer
);

public sealed class UpdateCheckResult
{
    public bool Success { get; init; }
    public bool UpdateAvailable { get; init; }
    public UpdateInfo? Info { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Service de vérification et notification des mises à jour de Purge for Windows via GitHub Releases.
/// </summary>
public static class UpdateService
{
    public const string CurrentVersion = "1.0.2";
    private const string ApiEndpoint = "https://api.github.com/repos/stvekoulo/purge/releases/latest";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    static UpdateService()
    {
        try
        {
            Http.DefaultRequestHeaders.UserAgent.ParseAdd($"PurgeWin/{CurrentVersion}");
            Http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
        }
        catch { }
    }

    public static event Action<UpdateInfo>? UpdateAvailable;
    public static UpdateInfo? CachedUpdate { get; private set; }

    /// <summary>
    /// Vérifie si une nouvelle version est disponible sur GitHub Releases.
    /// </summary>
    public static async Task<UpdateCheckResult> CheckForUpdatesAsync(bool silent = false)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ApiEndpoint);
            using var response = await Http.SendAsync(request).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult
                {
                    Success = false,
                    ErrorMessage = silent ? null : Loc.T($"Serveur GitHub indisponible ({response.StatusCode})", $"GitHub server unavailable ({response.StatusCode})")
                };
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string tagName = root.TryGetProperty("tag_name", out var tEl) ? tEl.GetString() ?? "" : "";
            string htmlUrl = root.TryGetProperty("html_url", out var uEl) ? uEl.GetString() ?? "" : "";
            string name = root.TryGetProperty("name", out var nEl) ? nEl.GetString() ?? "" : "";
            string body = root.TryGetProperty("body", out var bEl) ? bEl.GetString() ?? "" : "";

            string remoteVerStr = tagName.TrimStart('v', 'V').Trim();
            bool isNewer = CompareVersions(remoteVerStr, CurrentVersion) > 0;
            string? installerUrl = FindInstallerUrl(root);

            var info = new UpdateInfo(
                LatestVersion: string.IsNullOrEmpty(remoteVerStr) ? tagName : remoteVerStr,
                ReleaseTitle: name,
                ReleaseNotes: body,
                ReleaseUrl: string.IsNullOrWhiteSpace(htmlUrl) ? "https://github.com/stvekoulo/purge/releases" : htmlUrl,
                InstallerDownloadUrl: installerUrl,
                IsNewer: isNewer
            );

            if (isNewer)
            {
                CachedUpdate = info;
                UpdateAvailable?.Invoke(info);
            }

            return new UpdateCheckResult
            {
                Success = true,
                UpdateAvailable = isNewer,
                Info = info
            };
        }
        catch (Exception)
        {
            return new UpdateCheckResult
            {
                Success = false,
                ErrorMessage = silent ? null : Loc.T("Impossible de vérifier les mises à jour (hors-ligne ou réseau indisponible).", "Unable to check for updates (offline or network unavailable).")
            };
        }
    }

    private static string? FindInstallerUrl(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;

        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            _ => "x64"
        };

        string suffix = $"-win-{arch}-Setup.exe";

        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.TryGetProperty("name", out var n) && asset.TryGetProperty("browser_download_url", out var u))
            {
                string assetName = n.GetString() ?? "";
                if (assetName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return u.GetString();
                }
            }
        }
        return null;
    }

    public static int CompareVersions(string v1, string v2)
    {
        if (Version.TryParse(v1, out var p1) && Version.TryParse(v2, out var p2))
        {
            return p1.CompareTo(p2);
        }
        return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase);
    }

    public static void OpenDownloadOrRelease(UpdateInfo? info = null)
    {
        try
        {
            var target = info?.InstallerDownloadUrl ?? info?.ReleaseUrl
                ?? CachedUpdate?.InstallerDownloadUrl ?? CachedUpdate?.ReleaseUrl
                ?? "https://github.com/stvekoulo/purge/releases";

            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch { }
    }
}
