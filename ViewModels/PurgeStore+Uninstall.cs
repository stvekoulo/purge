using System.Diagnostics;
using PurgeWin.Models;
using PurgeWin.Services;
using PurgeWin.Utilities;

namespace PurgeWin.ViewModels;

public sealed partial class PurgeStore
{
    public bool? IsAllUninstallSelected
    {
        get
        {
            var (total, selected) = UninstallSubTab switch
            {
                UninstallerSubTab.InstalledApps => (FilteredInstalledApps.Count, FilteredInstalledApps.Count(a => a.IsSelected)),
                UninstallerSubTab.Leftovers => (FilteredOrphanLeftovers.Count, FilteredOrphanLeftovers.Count(o => o.IsSelected)),
                _ => (FilteredCliTools.Count, FilteredCliTools.Count(c => c.IsSelected)),
            };
            if (total == 0 || selected == 0) return false;
            return selected == total ? true : null;
        }
    }

    public void SelectAllUninstall(bool select)
    {
        switch (UninstallSubTab)
        {
            case UninstallerSubTab.InstalledApps:
                foreach (var app in FilteredInstalledApps) app.IsSelected = select;
                break;
            case UninstallerSubTab.Leftovers:
                foreach (var orphan in FilteredOrphanLeftovers) orphan.IsSelected = select;
                break;
            default:
                foreach (var cli in FilteredCliTools) cli.IsSelected = select;
                break;
        }
        NotifyUninstallSelection();
        OnPropertyChanged(nameof(IsAllUninstallSelected));
    }

    public async Task<CleanOutcome?> UninstallAppsAndCleanAsync(
        IReadOnlyList<InstalledAppItem> apps, bool cleanLeftovers = true, bool preferUnattended = true)
    {
        if (!CanClean || apps.Count == 0) return null;
        var scope = CreateScope();
        var context = _scopeSession.Current;
        IsCleaning = true;
        NotifyScope();

        long freedAppBytes = 0;
        int uninstalledApps = 0;
        var movedPaths = new List<string>();
        var failures = new List<CleanFailure>();
        var leftoverTargets = new List<(string Title, string Path)>();
        var matchingCaches = new List<CacheItem>();
        var uninstalledKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            for (int i = 0; i < apps.Count; i++)
            {
                var app = apps[i];
                CleaningLabel = Loc.T(
                    $"Désinstallation de {app.DisplayName} ({i + 1}/{apps.Count})…",
                    $"Uninstalling {app.DisplayName} ({i + 1}/{apps.Count})…");

                bool appRemoved = false;
                if (app.CanUninstall && (!string.IsNullOrWhiteSpace(app.UninstallString) || !string.IsNullOrWhiteSpace(app.QuietUninstallString)))
                {
                    appRemoved = await Task.Run(() => RunSingleUninstallAsync(app, preferUnattended));
                }

                if (appRemoved || !app.CanUninstall)
                {
                    if (appRemoved)
                    {
                        uninstalledApps++;
                        freedAppBytes += app.AppBytes;
                        movedPaths.Add(app.DisplayName);
                        uninstalledKeys.Add(app.Key);
                    }

                    if (cleanLeftovers)
                    {
                        foreach (var leftover in app.Leftovers)
                        {
                            if (Directory.Exists(leftover.Path) || File.Exists(leftover.Path))
                                leftoverTargets.Add((app.DisplayName, leftover.Path));
                        }

                        if (appRemoved && !string.IsNullOrWhiteSpace(app.InstallLocation)
                            && Directory.Exists(app.InstallLocation)
                            && scope.CanDelete(app.InstallLocation)
                            && !DeletionSafetyPolicy.IsProtected(app.InstallLocation)
                            && !DeletionSafetyPolicy.IsSystemPath(app.InstallLocation))
                        {
                            leftoverTargets.Add((app.DisplayName, app.InstallLocation));
                        }

                        var normApp = new string(app.DisplayName.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
                        if (normApp.Length >= 4)
                        {
                            foreach (var cache in CacheItems)
                            {
                                var normCache = new string(cache.Title.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
                                if (normCache.Contains(normApp, StringComparison.OrdinalIgnoreCase) && !matchingCaches.Contains(cache))
                                    matchingCaches.Add(cache);
                            }
                        }
                    }
                }
                else
                {
                    failures.Add(new CleanFailure(app.DisplayName,
                        Loc.T("Désinstallation annulée ou non terminée", "Uninstallation cancelled or incomplete")));
                }
            }

            long cleanedBytes = 0;
            int cleanedCount = 0;
            if (cleanLeftovers && (leftoverTargets.Count > 0 || matchingCaches.Count > 0))
            {
                CleaningLabel = Loc.T(
                    "Nettoyage automatique des caches et données résiduelles…",
                    "Automatically cleaning caches and leftover data…");
                var progress = new Progress<string>(name =>
                    CleaningLabel = Loc.T($"Nettoyage de {name}…", $"Cleaning {name}…"));

                var uniqueTargets = leftoverTargets
                    .GroupBy(t => t.Path, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .Where(t => Directory.Exists(t.Path) || File.Exists(t.Path))
                    .ToList();

                var cachedOutcome = matchingCaches.Count == 0
                    ? new CleanOutcome(0, 0, [], [])
                    : await FileDeleter.CleanAsync(matchingCaches, progress, scope);
                var directOutcome = uniqueTargets.Count == 0
                    ? new CleanOutcome(0, 0, [], [])
                    : await FileDeleter.CleanPathsAsync(uniqueTargets, progress, scope);

                cleanedBytes = cachedOutcome.FreedBytes + directOutcome.FreedBytes;
                cleanedCount = cachedOutcome.MovedCount + directOutcome.MovedCount;
                movedPaths.AddRange(cachedOutcome.MovedPaths);
                movedPaths.AddRange(directOutcome.MovedPaths);
                failures.AddRange(cachedOutcome.Failures);
                failures.AddRange(directOutcome.Failures);
            }

            var outcome = new CleanOutcome(
                freedAppBytes + cleanedBytes,
                uninstalledApps + cleanedCount,
                movedPaths,
                failures);

            if (Settings.EmptyRecycleBinAfterCleaning && cleanedCount > 0)
            {
                outcome = outcome with { RecycleBinEmptyRequested = true };
                CleaningLabel = Loc.T("Vidage automatique de la Corbeille…", "Emptying Recycle Bin automatically…");
                var emptied = await RecycleBin.EmptyAsync(scope.Roots);
                if (emptied.Failures.Count == 0)
                    outcome = outcome with { RecycleBinEmptied = true };
            }

            RecordOutcome(outcome, "uninstall");
            OnUi(() =>
            {
                foreach (var a in InstalledApps) a.IsSelected = false;
                if (uninstalledKeys.Count > 0)
                {
                    var remaining = InstalledApps.Where(a => !uninstalledKeys.Contains(a.Key)).ToList();
                    Replace(InstalledApps, remaining);
                    ApplyUninstallFilter();
                }
                NotifyTotals();
            });

            if (_scopeSession.IsCurrent(context))
            {
                await ScanUninstallerAsync();
                if (matchingCaches.Count > 0) await ScanAppCachesAsync();
                RefreshDisk();
                SaveScopeSnapshot();
                SaveScanCache();
            }

            return outcome;
        }
        finally
        {
            IsCleaning = false;
            CleaningLabel = "";
            NotifyScope();
        }
    }

    private static async Task<bool> RunSingleUninstallAsync(InstalledAppItem app, bool preferUnattended)
    {
        Process? proc = null;
        try
        {
            var psi = UninstallCommand.CreateUnattendedStartInfo(
                app.UninstallString, app.QuietUninstallString, preferUnattended, app.IsWindowsInstaller);
            proc = Process.Start(psi);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(app.UninstallString))
            {
                try { proc = Process.Start(UninstallCommand.CreateStartInfo(app.UninstallString)); }
                catch (Exception ex) { App.LogCrash("Fallback uninstaller start", ex); return false; }
            }
            else return false;
        }

        if (proc is null) return false;
        var sw = Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try { await proc.WaitForExitAsync(cts.Token); } catch { }

        bool EntryGone() => !UninstallScanner.IsRegistryEntryPresent(app.Key)
            || (!string.IsNullOrWhiteSpace(app.InstallLocation) && !Directory.Exists(app.InstallLocation));

        if (EntryGone()) return true;

        // Many Windows uninstallers (NSIS, Squirrel, Electron) spawn a child
        // process in %TEMP% and exit the initial process within 1-2 seconds.
        // Poll until the registry entry disappears or up to 60 seconds.
        int maxPolls = sw.Elapsed < TimeSpan.FromSeconds(4) ? 120 : 20;
        for (int p = 0; p < maxPolls; p++)
        {
            await Task.Delay(500);
            if (EntryGone()) return true;
        }

        return EntryGone();
    }
}
