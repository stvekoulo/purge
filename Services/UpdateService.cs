using System.Reflection;
using System.Runtime.InteropServices;

namespace PurgeWin.Services;

public static class UpdateService
{
    public static string CurrentVersion { get; } =
        GetCurrentVersion();

    public static UpdateCoordinator Shared { get; } = CreateCoordinator();

    public static int CompareVersions(string left, string right) => UpdateRelease.CompareVersions(left, right);

    private static string GetCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString(version.Revision > 0 ? 4 : 3) ?? "0.0.0";
    }

    private static UpdateCoordinator CreateCoordinator()
    {
#if PURGE_UPDATE_PREVIEW
        return UpdatePreview.Create();
#else
        var folder = Path.Combine(AppData.Folder, "Updates");
        PurgeStaleInstallers(folder, CurrentVersion);
        return new(CreateHttpClient(), folder, CurrentVersion,
            RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());
#endif
    }

    public static long PurgeStaleInstallers(string updatesFolder, string currentVersion)
    {
        long freed = 0;
        try
        {
            if (!Directory.Exists(updatesFolder)) return 0;
            foreach (var file in Directory.EnumerateFiles(updatesFolder))
            {
                var name = Path.GetFileName(file);
                bool stalePartial = name.Contains(".partial-", StringComparison.OrdinalIgnoreCase);
                bool installedSetup = false;
                if (name.StartsWith("Purge-", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = name.Split('-');
                    if (parts.Length >= 2)
                    {
                        try { installedSetup = CompareVersions(parts[1], currentVersion) <= 0; }
                        catch { }
                    }
                }
                if (!stalePartial && !installedSetup) continue;
                try
                {
                    var len = new FileInfo(file).Length;
                    File.Delete(file);
                    freed += len;
                }
                catch { }
            }
        }
        catch { }
        return freed;
    }

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"PurgeWin/{CurrentVersion}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }
}
