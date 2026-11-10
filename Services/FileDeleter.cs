using PurgeWin.Models;
using PurgeWin.Utilities;

namespace PurgeWin.Services;

/// Port of FileDeleter: every cache target passes DeletionSafetyPolicy.Evaluate,
/// and user-selected items (Dev Tools, Large Files, Uninstaller leftovers) pass
/// protected-root and reparse-point checks right before moving to Recycle Bin.
public static class FileDeleter
{
    public static Task<CleanOutcome> CleanAsync(IEnumerable<CacheItem> items, IProgress<string>? progress = null, PathScope? scope = null) =>
        Task.Run(() => CleanCaches(items.ToList(), progress, scope));

    public static Task<CleanOutcome> CleanPathsAsync(IEnumerable<(string Title, string Path)> targets, IProgress<string>? progress = null,
        PathScope? scope = null, IReadOnlyDictionary<string, string>? duplicateKeepers = null,
        IReadOnlySet<string>? gitWorktrees = null) =>
        Task.Run(() => CleanDirectPaths(targets.ToList(), progress, scope, duplicateKeepers, gitWorktrees));

    private static CleanOutcome CleanCaches(List<CacheItem> items, IProgress<string>? progress, PathScope? scope)
    {
        var moved = new List<string>();
        var failures = new List<CleanFailure>();
        long freed = 0;

        foreach (var item in items)
        {
            progress?.Report(item.Title);
            foreach (var target in Targets(item))
            {
                if (scope?.CanDelete(target.FullName) == false)
                {
                    failures.Add(new(target.FullName, Loc.T("Hors du disque sélectionné ou chemin exclu", "Outside selected disk or excluded")));
                    continue;
                }
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

    private static CleanOutcome CleanDirectPaths(List<(string Title, string Path)> targets, IProgress<string>? progress,
        PathScope? scope, IReadOnlyDictionary<string, string>? duplicateKeepers, IReadOnlySet<string>? gitWorktrees)
    {
        var moved = new List<string>();
        var failures = new List<CleanFailure>();
        long freed = 0;

        foreach (var (title, rawPath) in targets)
        {
            progress?.Report(title);
            string full;
            try { full = Path.GetFullPath(rawPath).TrimEnd('\\'); }
            catch
            {
                failures.Add(new CleanFailure(rawPath, Loc.T("Chemin invalide", "Invalid path")));
                continue;
            }

            if (!File.Exists(full) && !Directory.Exists(full)) continue;
            if (scope?.CanDelete(full) == false)
            {
                failures.Add(new(full, Loc.T("Hors du disque sélectionné ou chemin exclu", "Outside selected disk or excluded")));
                continue;
            }
            if (DeletionSafetyPolicy.IsProtected(full) || DeletionSafetyPolicy.IsSystemPath(full)
                || full.Count(c => c == '\\') < 2)
            {
                failures.Add(new CleanFailure(full, Loc.T("Ignoré par sécurité : emplacement protégé", "Skipped for safety: protected location")));
                continue;
            }
            if (PathExpander.IsReparsePoint(full) || PathExpander.HasReparsePointAncestor(full))
            {
                failures.Add(new CleanFailure(full, Loc.T("Ignoré par sécurité : lien ou jonction", "Skipped for safety: symlink or junction")));
                continue;
            }
            if (duplicateKeepers?.TryGetValue(full, out var keeper) == true
                && (string.IsNullOrEmpty(keeper) || !DuplicateFileVerifier.AreIdentical(full, keeper)))
            {
                failures.Add(new CleanFailure(full, Loc.T("Doublon non confirmé : copie à conserver absente ou modifiée", "Duplicate not verified: keeper missing or changed")));
                continue;
            }
            if (gitWorktrees?.Contains(full) == true && GitWorktreeInspector.Inspect(full) != GitWorktreeState.Clean)
            {
                failures.Add(new CleanFailure(full, Loc.T("Worktree Git modifié ou non vérifiable", "Modified or unverifiable Git worktree")));
                continue;
            }

            FileSystemInfo info = Directory.Exists(full) ? new DirectoryInfo(full) : new FileInfo(full);
            MoveOne(info, ref freed, moved, failures);
        }
        return new CleanOutcome(freed, moved.Count, moved, failures);
    }

    private static void MoveOne(FileSystemInfo target, ref long freed, List<string> moved, List<CleanFailure> failures)
    {
        try
        {
            var size = FolderSizing.MeasureEntry(target);
            var error = RecycleBin.MoveToRecycleBin(target.FullName);
            if (error is null)
            {
                freed += size;
                moved.Add(target.FullName);
            }
            else failures.Add(new CleanFailure(target.FullName, error));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            failures.Add(new CleanFailure(target.FullName, ex.Message));
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

}
