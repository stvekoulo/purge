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

/// Expands catalog path templates: environment variables, then "*" segments
/// matched against real directories. Reparse points are never followed.
public static class PathExpander
{
    public static IEnumerable<string> Expand(string template)
    {
        var expanded = Environment.ExpandEnvironmentVariables(template);
        if (expanded.Contains('%')) yield break; // unknown variable: never guess
        var parts = expanded.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) yield break;
        var roots = new List<string> { parts[0] + "\\" };
        foreach (var part in parts.Skip(1))
        {
            var next = new List<string>();
            foreach (var root in roots)
            {
                if (part.Contains('*'))
                {
                    foreach (var dir in SafeDirectories(root, part)) next.Add(dir);
                }
                else
                {
                    var candidate = Path.Combine(root, part);
                    if (Directory.Exists(candidate) && !IsReparsePoint(candidate)) next.Add(candidate);
                }
            }
            roots = next;
            if (roots.Count == 0) yield break;
        }
        foreach (var r in roots) yield return Path.GetFullPath(r);
    }

    public static bool IsReparsePoint(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch { return true; } // unreadable counts as unsafe
    }

    private static IEnumerable<string> SafeDirectories(string root, string pattern)
    {
        try
        {
            return Directory.EnumerateDirectories(root, pattern, new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            }).ToList();
        }
        catch { return []; }
    }
}
