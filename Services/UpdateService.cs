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
        return new(CreateHttpClient(), Path.Combine(AppData.Folder, "Updates"), CurrentVersion,
            RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());
#endif
    }

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"PurgeWin/{CurrentVersion}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }
}
