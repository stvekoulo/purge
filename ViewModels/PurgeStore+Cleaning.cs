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

    public Task<CleanOutcome?> CleanSelectedDevAsync() => RunCleanAsync([], DevItems.Where(i => i.IsSelected)
        .SelectMany(i => i.Paths.Select(path => (i.Title, path))).ToList(), "manual");

    public Task<CleanOutcome?> CleanSelectedLargeFilesAsync() => RunCleanAsync([], LargeFileItems.Where(i => i.IsSelected)
        .Select(i => (i.Name, i.FullPath)).ToList(), "manual");

    public Task<CleanOutcome?> CleanSelectedUninstallAsync()
    {
        var targets = new List<(string, string)>();
        if (UninstallSubTab == UninstallerSubTab.InstalledApps)
            targets.AddRange(InstalledApps.Where(i => i.IsSelected).SelectMany(i => i.Leftovers.Select(l => (i.DisplayName, l.Path))));
        else if (UninstallSubTab == UninstallerSubTab.Leftovers)
            targets.AddRange(OrphanLeftovers.Where(i => i.IsSelected).Select(i => (i.FolderName, i.FullPath)));
        else targets.AddRange(CliTools.Where(i => i.IsSelected).Select(i => (i.Name, i.Path)));
        return RunCleanAsync([], targets, "manual");
    }

    private async Task<CleanOutcome?> RunCleanAsync(List<CacheItem> caches, List<(string Title, string Path)> paths, string trigger)
    {
        if (!CanClean || caches.Count + paths.Count == 0) return null;
        var scope = CreateScope();
        var context = _scopeSession.Current;
        IsCleaning = true;
        NotifyScope();
        var progress = new Progress<string>(name => CleaningLabel = Loc.T($"Nettoyage de {name}…", $"Cleaning {name}…"));
        CleanOutcome outcome;
        try
        {
            var cached = caches.Count == 0 ? new CleanOutcome(0, 0, [], []) : await FileDeleter.CleanAsync(caches, progress, scope);
            var direct = paths.Count == 0 ? new CleanOutcome(0, 0, [], []) : await FileDeleter.CleanPathsAsync(paths, progress, scope);
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
        }
        finally { IsCleaning = false; CleaningLabel = ""; NotifyScope(); }
        if (_scopeSession.IsCurrent(context)) await ScanEverythingAsync();
        return outcome;
    }

    public void LaunchAppUninstaller(InstalledAppItem app)
    {
        if (!app.CanUninstall || !CanClean || string.IsNullOrWhiteSpace(app.UninstallString)) return;
        try
        {
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c {app.UninstallString}") { CreateNoWindow = true, UseShellExecute = false });
            _scopeCache.Clear();
        }
        catch (Exception ex) { App.LogCrash("Launch uninstaller", ex); }
    }

    public bool EvictOneDriveFile(LargeFileItem item)
    {
        if (!CanClean || !CreateScope().CanDelete(item.FullPath)) return false;
        var ok = LargeFileScanner.EvictOneDriveLocalCopy(item.FullPath);
        if (ok)
        {
            _scopeCache.Clear(); LargeFileItems.Remove(item); ApplyLargeFilter(); RefreshDisk(); NotifyTotals();
        }
        return ok;
    }

    private void RecordOutcome(CleanOutcome outcome, string trigger)
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
