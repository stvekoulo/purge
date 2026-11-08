using PurgeWin.Models;

namespace PurgeWin.Services;

/// Port of FileDeleter: every cache target passes DeletionSafetyPolicy.Evaluate,
/// and user-selected items (Dev Tools, Large Files, Uninstaller leftovers) pass
/// protected-root and reparse-point checks right before moving to Recycle Bin.
public static class FileDeleter
{
    public static Task<CleanOutcome> CleanAsync(IEnumerable<CacheItem> items, IProgress<string>? progress = null) =>
        Task.Run(() => CleanCaches(items.ToList(), progress));

    public static Task<CleanOutcome> CleanPathsAsync(IEnumerable<(string Title, string Path)> targets, IProgress<string>? progress = null) =>
        Task.Run(() => CleanDirectPaths(targets.ToList(), progress));

    private static CleanOutcome CleanCaches(List<CacheItem> items, IProgress<string>? progress)
    {
        var moved = new List<string>();
        var failures = new List<CleanFailure>();
        long freed = 0;

        foreach (var item in items)
        {
            progress?.Report(item.Title);
            foreach (var target in Targets(item))
            {
                var decision = DeletionSafetyPolicy.Evaluate(target.FullName, item.Entry);
                if (decision == DeletionDecision.Missing) continue;
                if (decision != DeletionDecision.Allowed)
                {
                    if (decision != DeletionDecision.TooYoung)
                        failures.Add(new CleanFailure(target.FullName, DeletionSafetyPolicy.Describe(decision)));
                    continue;
                }
                MoveOne(target, ref freed, moved, failures);
            }
        }
        return new CleanOutcome(freed, moved.Count, moved, failures);
    }

    private static CleanOutcome CleanDirectPaths(List<(string Title, string Path)> targets, IProgress<string>? progress)
    {
        var moved = new List<string>();
        var failures = new List<CleanFailure>();
        long freed = 0;

        foreach (var (title, rawPath) in targets)
        {
            progress?.Report(title);
            string full;
            try { full = Path.GetFullPath(rawPath).TrimEnd('\\'); }
            catch { continue; }

            if (!File.Exists(full) && !Directory.Exists(full)) continue;
            if (DeletionSafetyPolicy.IsProtected(full) || full.Count(c => c == '\\') < 2)
            {
                failures.Add(new CleanFailure(full, "Ignoré par sécurité : emplacement protégé"));
                continue;
            }
            if (PathExpander.IsReparsePoint(full))
            {
                failures.Add(new CleanFailure(full, "Ignoré par sécurité : lien ou jonction"));
                continue;
            }

            FileSystemInfo info = Directory.Exists(full) ? new DirectoryInfo(full) : new FileInfo(full);
            MoveOne(info, ref freed, moved, failures);
        }
        return new CleanOutcome(freed, moved.Count, moved, failures);
    }

    private static void MoveOne(FileSystemInfo target, ref long freed, List<string> moved, List<CleanFailure> failures)
    {
        var size = FolderSizing.MeasureEntry(target);
        var error = RecycleBin.MoveToRecycleBin(target.FullName);
        if (error is null)
        {
            freed += size;
            moved.Add(target.FullName);
        }
        else
        {
            var left = File.Exists(target.FullName) || Directory.Exists(target.FullName)
                ? FolderSizing.MeasureEntry(Refresh(target)) : 0;
            freed += Math.Max(0, size - left);
            failures.Add(new CleanFailure(target.FullName, error));
        }
    }

    private static IEnumerable<FileSystemInfo> Targets(CacheItem item)
    {
        foreach (var path in item.Paths)
        {
            if (item.Entry.CleanMode == CleanMode.Contents)
            {
                foreach (var child in FolderSizing.AgedChildren(path, item.Entry.MinAgeHours)) yield return child;
            }
            else if (Directory.Exists(path))
            {
                yield return new DirectoryInfo(path);
            }
        }
    }

    private static FileSystemInfo Refresh(FileSystemInfo info) =>
        Directory.Exists(info.FullName) ? new DirectoryInfo(info.FullName) : new FileInfo(info.FullName);
}
