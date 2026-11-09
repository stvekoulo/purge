using System.Text.Json;
using PurgeWin.Models;

namespace PurgeWin.Services;

/// Loads the allowlist catalog shipped next to the app. Port of
/// ExplanationDatabase + CacheDiscoveryPaths: nothing outside it is scanned.
public static class CacheCatalog
{
    private static IReadOnlyList<CatalogEntry>? _entries;

    public static IReadOnlyList<CatalogEntry> Entries => _entries ??= Load();

    private static List<CatalogEntry> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "windows-cache-catalog.json");
        if (!File.Exists(path)) return [];
        var entries = JsonSerializer.Deserialize<List<CatalogEntry>>(File.ReadAllText(path)) ?? [];
        // A catalog entry with no risk tier never reaches the UI (canSurfaceInScanResults).
        return entries.Where(e => e.Level.CanSurfaceInScanResults() && e.Paths.Count > 0).ToList();
    }
}
