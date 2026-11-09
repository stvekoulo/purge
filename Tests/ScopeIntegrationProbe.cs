#if PURGE_UX_TEST
using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Xaml;
using PurgeWin.Services;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

internal static class ScopeIntegrationProbe
{
    public static async Task RunAsync(Window window)
    {
        var store = PurgeStore.Shared;
        var results = new Dictionary<string, bool>();
        var metrics = new Dictionary<string, object>();
        try
        {
            await Task.Delay(300);
            await store.ScanEverythingAsync();
            results["All disks complete"] = !store.IsScanning && string.IsNullOrEmpty(store.ScanError);
            metrics["ALL"] = Counts(store);
            var roots = store.ActiveDriveRoots.ToArray();
            foreach (var root in roots.Reverse())
            {
                if (store.LargeFileItems.Count > 0) store.LargeFileItems[0].IsSelected = true;
                store.SetDrive(root);
                await Wait(store);
                var scope = store.CurrentScanContext.Scope;
                results[$"Only {root} results"] = store.CacheItems.All(i => i.Paths.All(scope.CanDelete))
                    && store.DevItems.All(i => i.Paths.All(scope.CanDelete))
                    && store.LargeFileItems.All(i => scope.CanDelete(i.FullPath))
                    && store.OrphanLeftovers.All(i => scope.CanDelete(i.FullPath))
                    && store.CliTools.All(i => scope.CanDelete(i.Path));
                results[$"Selection reset on {root}"] = store.SelectedLargeCount == 0 && store.SelectedCount == 0;
                metrics[root] = Counts(store);
            }
            if (roots.Length > 1)
            {
                var refresh = store.RefreshCurrentScopeAsync();
                store.SetDrive(roots[^1]);
                await refresh;
                await Wait(store);
                await Task.Delay(200);
                results["Late scan cannot replace selected disk"] = store.Settings.SelectedDrive == roots[^1]
                    && store.LargeFileItems.All(i => store.CurrentScanContext.Scope.Contains(i.FullPath));
                store.SetDrive("ALL");
                await Wait(store);
                var timer = Stopwatch.StartNew();
                store.SetDrive(roots[^1]);
                results["Cached scope switches immediately"] = !store.IsScanning && timer.ElapsedMilliseconds < 1500;
                results["Cached selection stays empty"] = store.SelectedLargeCount == 0;
            }
            results["Automatic empty defaults to off"] = !store.AutoEmptyRecycleBin;
        }
        catch (Exception ex) { metrics["error"] = ex.ToString(); results["probe"] = false; }
        await File.WriteAllTextAsync(Path.Combine(AppData.Folder, "scope-probe.json"), JsonSerializer.Serialize(new { results, metrics }, new JsonSerializerOptions { WriteIndented = true }));
        Environment.ExitCode = results.Values.All(v => v) ? 0 : 1;
        window.Close();
    }

    private static object Counts(PurgeStore store) => new { Caches = store.CacheItems.Count, Dev = store.DevItems.Count,
        Large = store.LargeFileItems.Count, Apps = store.InstalledApps.Count, store.CacheBytes, store.DevBytes, store.LargeFileBytes };
    private static async Task Wait(PurgeStore store)
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (store.IsScanning && DateTime.UtcNow < deadline) await Task.Delay(100);
        if (store.IsScanning) throw new TimeoutException("Scope scan timeout");
    }
}
#endif
