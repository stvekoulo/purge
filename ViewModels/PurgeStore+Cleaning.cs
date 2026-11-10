using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using PurgeWin.Models;
using PurgeWin.Services;
using PurgeWin.Utilities;

namespace PurgeWin.ViewModels;

public sealed partial class PurgeStore
{
    [ObservableProperty] private CleanOutcome? _lastCleanOutcome;
    [ObservableProperty] private bool _showCelebration;
    private bool CanClean => !IsScanning && !IsCleaning && HasActiveDrives;

    public Task<CleanOutcome?> CleanSelectedAsync() => RunCleanAsync(CacheItems.Where(i => i.IsSelected).ToList(), [], "manual");

    public Task<CleanOutcome?> CleanSafeItemsAsync() => RunCleanAsync(CacheItems.Where(i => i.IsSafe).ToList(),
        DevItems.Where(i => i.IsSafe && i.Section == DevSectionKind.GlobalCache)
            .SelectMany(i => i.Paths.Select(path => (i.Title, path))).ToList(), "safe");

    public Task<CleanOutcome?> CleanSelectedDevAsync()
    {
        var selected = DevItems.Where(i => i.IsSelected).ToList();
        var worktrees = selected.Where(i => i.Section == DevSectionKind.OrphanWorktree)
            .SelectMany(i => i.Paths).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return RunCleanAsync([], selected.SelectMany(i => i.Paths.Select(path => (i.Title, path))).ToList(),
            "manual", gitWorktrees: worktrees);
    }

    public Task<CleanOutcome?> CleanSelectedLargeFilesAsync()
    {
        var selected = LargeFileItems.Where(i => i.IsSelected).ToList();
        var selectedPaths = selected.Select(i => i.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicateKeepers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in selected.Where(i => i.IsDuplicate))
        {
            var keeper = LargeFileItems.FirstOrDefault(i => i.DuplicateGroupId == item.DuplicateGroupId && i.IsDuplicateKeeper);
            // If the keeper is selected, missing, or was itself selected,
            // refuse the whole affected duplicate group rather than lose all copies.
            duplicateKeepers[item.FullPath] = keeper is null || selectedPaths.Contains(keeper.FullPath)
                ? "" : keeper.FullPath;
        }
        return RunCleanAsync([], selected.Select(i => (i.Name, i.FullPath)).ToList(), "manual", duplicateKeepers);
    }

    public Task<CleanOutcome?> CleanSelectedUninstallAsync()
    {
        var targets = new List<(string, string)>();
        if (UninstallSubTab == UninstallerSubTab.InstalledApps)
            targets.AddRange(InstalledApps.Where(i => i.IsSelected).SelectMany(i => i.Leftovers
                .Where(l => l.IsExactMatch).Select(l => (i.DisplayName, l.Path))));
        else if (UninstallSubTab == UninstallerSubTab.Leftovers)
            targets.AddRange(OrphanLeftovers.Where(i => i.IsSelected).Select(i => (i.FolderName, i.FullPath)));
        else targets.AddRange(CliTools.Where(i => i.IsSelected).Select(i => (i.Name, i.Path)));
        return RunCleanAsync([], targets, "manual");
    }

    private async Task<CleanOutcome?> RunCleanAsync(List<CacheItem> caches, List<(string Title, string Path)> paths,
        string trigger, IReadOnlyDictionary<string, string>? duplicateKeepers = null,
        IReadOnlySet<string>? gitWorktrees = null)
    {
        if (!CanClean || caches.Count + paths.Count == 0) return null;
        var scope = CreateScope();
        var context = _scopeSession.Current;
        var selectedLargePaths = LargeFileItems.Where(i => i.IsSelected)
            .Select(i => i.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        IsCleaning = true;
        NotifyScope();
        var progress = new Progress<string>(name => CleaningLabel = Loc.T($"Nettoyage de {name}…", $"Cleaning {name}…"));
        CleanOutcome outcome;
        try
        {
            var cached = caches.Count == 0 ? new CleanOutcome(0, 0, [], []) : await FileDeleter.CleanAsync(caches, progress, scope);
            var direct = paths.Count == 0 ? new CleanOutcome(0, 0, [], [])
                : await FileDeleter.CleanPathsAsync(paths, progress, scope, duplicateKeepers, gitWorktrees);
            outcome = new(cached.FreedBytes + direct.FreedBytes, cached.MovedCount + direct.MovedCount,
                cached.MovedPaths.Concat(direct.MovedPaths).ToList(), cached.Failures.Concat(direct.Failures).ToList());
            if (Settings.EmptyRecycleBinAfterCleaning && outcome.MovedCount > 0)
            {
                outcome = outcome with { RecycleBinEmptyRequested = true };
                CleaningLabel = Loc.T("Vidage automatique de la Corbeille…", "Emptying Recycle Bin automatically…");
                var emptied = await RecycleBin.EmptyAsync(scope.Roots);
                if (emptied.Failures.Count > 0)
                {
                    TrashActionMessage = Loc.T("Le nettoyage est terminé, mais la Corbeille n'a pas pu être entièrement vidée.",
                        "Cleaning finished, but the Recycle Bin could not be emptied completely.");
                    OnPropertyChanged(nameof(TrashActionMessage));
                    outcome = outcome with { Failures = outcome.Failures.Concat(emptied.Failures.Select(f => new CleanFailure("Corbeille / Recycle Bin", f))).ToList() };
                }
                else outcome = outcome with { RecycleBinEmptied = true };
                RefreshDisk();
            }
            RecordOutcome(outcome, trigger);
            ClearSelections();
            // Keep failed large-file selections available for an explicit retry.
            var movedPaths = outcome.MovedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var item in LargeFileItems.Where(i => selectedLargePaths.Contains(i.FullPath) && !movedPaths.Contains(i.FullPath)))
                item.IsSelected = true;
        }
        finally { IsCleaning = false; CleaningLabel = ""; NotifyScope(); }
        if (_scopeSession.IsCurrent(context))
        {
            if (caches.Count > 0) await ScanAppCachesAsync();
            if (paths.Count > 0)
            {
                if (DevItems.Any(d => d.Paths.Any(dp => paths.Any(p => string.Equals(p.Path, dp, StringComparison.OrdinalIgnoreCase)))))
                    await ScanDevToolsAsync();

                // An attempted move is not a successful one. Failed files must
                // remain in the current view until a later scan or retry.
                var removed = outcome.MovedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
                OnUi(() =>
                {
                    var remaining = LargeFileItems.Where(l => !removed.Contains(l.FullPath)).ToList();
                    if (remaining.Count != LargeFileItems.Count)
                    {
                        Replace(LargeFileItems, remaining);
                        RefreshAllFilteredViews();
                        NotifyTotals();
                    }
                });
            }
            RefreshDisk();
            SaveScopeSnapshot();
            SaveScanCache();
        }
        return outcome;
    }

    public bool LaunchAppUninstaller(InstalledAppItem app)
    {
        if (!app.CanUninstall || !CanClean || string.IsNullOrWhiteSpace(app.UninstallString)) return false;
        try
        {
            // The registered uninstaller owns removal of files, services and
            // registry keys. Never trash an installed application's directory.
            Process.Start(UninstallCommand.CreateStartInfo(app.UninstallString));
            _scopeCache.Clear();
            return true;
        }
        catch (Exception ex) { App.LogCrash("Launch uninstaller", ex); return false; }
    }

    public bool EvictOneDriveFile(LargeFileItem item)
    {
        if (!CanClean || !item.IsOneDriveSynced || !CreateScope().CanDelete(item.FullPath)) return false;
        var ok = LargeFileScanner.EvictOneDriveLocalCopy(item.FullPath);
        if (ok)
        {
            // attrib +U -P requests dehydration; it does not prove that
            // Windows has already freed the local bytes. Keep the entry in
            // the result list until the next scan confirms its cloud state.
            _scopeCache.Clear(); RefreshDisk(); NotifyTotals();
        }
        return ok;
    }

    public void RecordOutcome(CleanOutcome outcome, string trigger)
    {
        _scopeCache.Clear();
        History.Entries.Insert(0, new() { Date = DateTime.Now, Trigger = trigger, FreedBytes = outcome.FreedBytes,
            RecycleBinEmptied = outcome.RecycleBinEmptied,
            RecycleBinEmptyRequested = outcome.RecycleBinEmptyRequested,
            ItemCount = outcome.MovedCount, Moved = outcome.MovedPaths.ToList(),
            Skipped = outcome.Failures.Select(f => $"{f.Path}: {f.Reason}").ToList() });
        AppData.Save("history.json", History);
        LastCleanOutcome = outcome;
        if (outcome.FreedBytes > 0) ShowCelebration = true;
        RefreshDisk();
    }

    public void DismissCelebration() => ShowCelebration = false;
}
