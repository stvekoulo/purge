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

    public async Task<CleanOutcome?> CleanSelectedAsync()
    {
        var selected = CacheItems.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0 || IsCleaning) return null;
        return await ExecuteCacheCleanAsync(selected, "manual");
    }

    public async Task<CleanOutcome?> CleanSafeItemsAsync()
    {
        var safeCaches = CacheItems.Where(i => i.IsSafe).ToList();
        var safeDev = DevItems.Where(i => i.IsSafe && i.Section == DevSectionKind.GlobalCache).ToList();
        if ((safeCaches.Count == 0 && safeDev.Count == 0) || IsCleaning) return null;

        CleanOutcome? outcome = null;
        if (safeCaches.Count > 0)
            outcome = await ExecuteCacheCleanAsync(safeCaches, "safe");

        if (safeDev.Count > 0)
        {
            var devTargets = safeDev.SelectMany(d => d.Paths.Select(p => (d.Title, p))).ToList();
            var devOut = await ExecuteDirectCleanAsync(devTargets, "safe");
            if (outcome is null) outcome = devOut;
            else if (devOut is not null)
                outcome = new CleanOutcome(
                    outcome.FreedBytes + devOut.FreedBytes,
                    outcome.MovedCount + devOut.MovedCount,
                    outcome.MovedPaths.Concat(devOut.MovedPaths).ToList(),
                    outcome.Failures.Concat(devOut.Failures).ToList());
            await ScanDevToolsAsync();
        }
        return outcome;
    }

    public async Task<CleanOutcome?> CleanSelectedDevAsync()
    {
        var selected = DevItems.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0 || IsCleaning) return null;
        var targets = selected.SelectMany(d => d.Paths.Select(p => (d.Title, p))).ToList();
        var outcome = await ExecuteDirectCleanAsync(targets, "manual");
        await ScanDevToolsAsync();
        return outcome;
    }

    public async Task<CleanOutcome?> CleanSelectedLargeFilesAsync()
    {
        var selected = LargeFileItems.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0 || IsCleaning) return null;
        var targets = selected.Select(f => (f.Name, f.FullPath)).ToList();
        var outcome = await ExecuteDirectCleanAsync(targets, "manual");
        await ScanLargeFilesAsync();
        return outcome;
    }

    public async Task<CleanOutcome?> CleanSelectedUninstallAsync()
    {
        if (IsCleaning) return null;
        var targets = new List<(string Title, string Path)>();
        if (UninstallSubTab == UninstallerSubTab.InstalledApps)
        {
            foreach (var app in InstalledApps.Where(a => a.IsSelected))
            {
                foreach (var lo in app.Leftovers)
                    targets.Add(($"{app.DisplayName} ({lo.ScopeLabel})", lo.Path));
            }
        }
        else if (UninstallSubTab == UninstallerSubTab.Leftovers)
        {
            foreach (var lo in OrphanLeftovers.Where(o => o.IsSelected))
                targets.Add((lo.FolderName, lo.FullPath));
        }
        else
        {
            foreach (var cli in CliTools.Where(c => c.IsSelected))
                targets.Add((cli.Name, cli.Path));
        }

        if (targets.Count == 0) return null;
        var outcome = await ExecuteDirectCleanAsync(targets, "manual");
        await ScanUninstallerAsync();
        return outcome;
    }

    public void LaunchAppUninstaller(InstalledAppItem app)
    {
        if (string.IsNullOrWhiteSpace(app.UninstallString)) return;
        try
        {
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c {app.UninstallString}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
        }
        catch { }
    }

    public bool EvictOneDriveFile(LargeFileItem item)
    {
        var ok = LargeFileScanner.EvictOneDriveLocalCopy(item.FullPath);
        if (ok)
        {
            LargeFileItems.Remove(item);
            ApplyLargeFilter();
            RefreshDisk();
            NotifyTotals();
        }
        return ok;
    }

    private async Task<CleanOutcome?> ExecuteCacheCleanAsync(List<CacheItem> items, string trigger)
    {
        IsCleaning = true;
        CleaningLabel = Loc.T("Nettoyage en cours...", "Cleaning...");
        var progress = new Progress<string>(title =>
            CleaningLabel = Loc.T($"Nettoyage de {title}...", $"Cleaning {title}..."));

        CleanOutcome outcome;
        try { outcome = await FileDeleter.CleanAsync(items, progress); }
        finally { IsCleaning = false; CleaningLabel = ""; }

        RecordOutcome(outcome, trigger);
        await ScanAppCachesAsync();
        return outcome;
    }

    private async Task<CleanOutcome?> ExecuteDirectCleanAsync(List<(string Title, string Path)> targets, string trigger)
    {
        IsCleaning = true;
        CleaningLabel = Loc.T("Envoi à la Corbeille...", "Moving to Recycle Bin...");
        var progress = new Progress<string>(title =>
            CleaningLabel = Loc.T($"Nettoyage de {title}...", $"Cleaning {title}..."));

        CleanOutcome outcome;
        try { outcome = await FileDeleter.CleanPathsAsync(targets, progress); }
        finally { IsCleaning = false; CleaningLabel = ""; }

        RecordOutcome(outcome, trigger);
        return outcome;
    }

    private void RecordOutcome(CleanOutcome outcome, string trigger)
    {
        var historyEntry = new HistoryEntry
        {
            Date = DateTime.Now,
            Trigger = trigger,
            FreedBytes = outcome.FreedBytes,
            ItemCount = outcome.MovedCount,
            Moved = outcome.MovedPaths.ToList(),
            Skipped = outcome.Failures.Select(f => $"{f.Path}: {f.Reason}").ToList()
        };
        History.Entries.Insert(0, historyEntry);
        AppData.Save("history.json", History);
        LastCleanOutcome = outcome;
        if (outcome.FreedBytes > 0) ShowCelebration = true;
        RefreshDisk();
    }

    public void DismissCelebration() => ShowCelebration = false;
}
