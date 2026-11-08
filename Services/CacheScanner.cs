using PurgeWin.Models;

namespace PurgeWin.Services;

/// Port of CacheScanner: walks only the catalog, measures each match and
/// merges the locations of one entry into one row. Results stream in through
/// the callback as each entry is measured.
public static class CacheScanner
{
    public static async Task<List<CacheItem>> ScanAsync(
        Action<CacheItem>? onItem, ISet<string> excluded, CancellationToken token)
    {
        var results = new List<CacheItem>();
        await Task.Run(() =>
        {
            Parallel.ForEach(CacheCatalog.Entries, new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount / 2),
                CancellationToken = token,
            }, entry =>
            {
                var item = Measure(entry, excluded, token);
                if (item is null) return;
                lock (results) results.Add(item);
                onItem?.Invoke(item);
            });
        }, token);
        return results.OrderByDescending(i => i.Bytes).ToList();
    }

    private static CacheItem? Measure(CatalogEntry entry, ISet<string> excluded, CancellationToken token)
    {
        var paths = entry.Paths.SelectMany(PathExpander.Expand)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            // A Folder-mode match that is a protected root is never offered;
            // Contents roots (like %TEMP%) stay, only their children move.
            .Where(p => entry.CleanMode == CleanMode.Contents || !DeletionSafetyPolicy.IsProtected(p))
            .Where(p => !excluded.Contains(p))
            .ToList();
        if (paths.Count == 0) return null;

        long bytes = 0;
        var newest = DateTime.MinValue;
        foreach (var path in paths)
        {
            token.ThrowIfCancellationRequested();
            if (entry.CleanMode == CleanMode.Contents)
            {
                foreach (var child in FolderSizing.AgedChildren(path, entry.MinAgeHours))
                {
                    bytes += FolderSizing.MeasureEntry(child);
                    if (child.LastWriteTime > newest) newest = child.LastWriteTime;
                }
            }
            else
            {
                var (b, m) = FolderSizing.Measure(path, token);
                bytes += b;
                if (m > newest) newest = m;
            }
        }
        // Empty locations are not worth a row.
        if (bytes < 64 * 1024) return null;
        return new CacheItem { Entry = entry, Paths = paths, Bytes = bytes, LastModified = newest };
    }
}
