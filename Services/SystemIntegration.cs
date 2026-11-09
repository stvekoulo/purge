using System.Diagnostics;
using Microsoft.Win32;
using PurgeWin.Models;
using PurgeWin.Utilities;

namespace PurgeWin.Services;

/// Purge for Windows:
/// 1) Windows Startup registration (HKCU\...\Run\Purge)
/// 2) Windows Scheduled Task registration (schtasks.exe /Create /TN "PurgeSafeCleanup")
/// 3) Headless "--scheduled-clean" runner
/// 4) Self-uninstaller (removes startup, scheduled task, and optionally %LOCALAPPDATA%\Purge)
public static class SystemIntegration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "Purge";
    private const string ScheduledTaskName = "PurgeSafeCleanup";

    public static bool IsLaunchAtStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return !string.IsNullOrEmpty(key?.GetValue(RunValueName) as string);
        }
        catch { return false; }
    }

    public static bool SetLaunchAtStartup(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null) return false;
            if (enabled)
            {
                var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Purge.exe");
                key.SetValue(RunValueName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
            return true;
        }
        catch { return false; }
    }

    public static bool ApplyScheduledCleaning(string frequency)
    {
        try
        {
            if (string.IsNullOrEmpty(frequency) || frequency == "off")
            {
                return RunSchtasks($"/Delete /TN \"{ScheduledTaskName}\" /F", allowMissing: true);
            }

            var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Purge.exe");
            var scheduleArgs = frequency switch
            {
                "weekly" => "/SC WEEKLY /D MON /ST 12:00",
                "quarterly" => "/SC MONTHLY /MO 3 /D 1 /ST 12:00",
                _ => "/SC MONTHLY /D 1 /ST 12:00",
            };
            return RunSchtasks($"/Create /TN \"{ScheduledTaskName}\" /TR \"\\\"{exe}\\\" --scheduled-clean\" {scheduleArgs} /F");
        }
        catch { return false; }
    }

    public static async Task RunHeadlessScheduledCleanAsync()
    {
        var settings = AppData.Load<AppSettings>("settings.json");
        var history = AppData.Load<HistoryLog>("history.json");
        var excluded = settings.ExcludedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var roots = PathScope.AvailableRoots().Where(root => settings.SelectedDrive == "ALL"
            || root.Equals(settings.SelectedDrive, StringComparison.OrdinalIgnoreCase)).ToArray();
        var scope = new PathScope(roots, excluded);

        var items = await CacheScanner.ScanAsync(null, excluded, CancellationToken.None, roots);
        var safeItems = items.Where(i => i.IsSafe).ToList();
        var devItems = await DevScanner.ScanAsync(null, excluded, settings.StaleProjectDays,
            settings.CustomProjectRoots, roots, CancellationToken.None, globalCachesOnly: true);
        var devPaths = devItems.Where(i => i.IsSafe && i.Section == DevSectionKind.GlobalCache)
            .SelectMany(i => i.Paths.Select(path => (i.Title, path))).ToList();
        if (safeItems.Count == 0 && devPaths.Count == 0) return;

        var caches = await FileDeleter.CleanAsync(safeItems, scope: scope);
        var dev = await FileDeleter.CleanPathsAsync(devPaths, scope: scope);
        var outcome = new CleanOutcome(caches.FreedBytes + dev.FreedBytes, caches.MovedCount + dev.MovedCount,
            caches.MovedPaths.Concat(dev.MovedPaths).ToList(), caches.Failures.Concat(dev.Failures).ToList());
        if (settings.EmptyRecycleBinAfterCleaning && outcome.MovedCount > 0)
        {
            var empty = await RecycleBin.EmptyAsync(roots);
            outcome = outcome with
            {
                RecycleBinEmptied = empty.Failures.Count == 0,
                RecycleBinEmptyRequested = true,
                Failures = outcome.Failures.Concat(empty.Failures.Select(f => new CleanFailure("Corbeille / Recycle Bin", f))).ToList(),
            };
        }
        if (outcome.MovedCount > 0 || outcome.Failures.Count > 0)
        {
            history.Entries.Insert(0, new HistoryEntry
            {
                Date = DateTime.Now,
                Trigger = "scheduled",
                FreedBytes = outcome.FreedBytes,
                ItemCount = outcome.MovedCount,
                RecycleBinEmptied = outcome.RecycleBinEmptied,
                RecycleBinEmptyRequested = outcome.RecycleBinEmptyRequested,
                Moved = outcome.MovedPaths.ToList(),
                Skipped = outcome.Failures.Select(f => $"{f.Path}: {f.Reason}").ToList(),
            });
            AppData.Save("history.json", history);
        }
    }

    public static void UninstallPurge(bool removeAppData)
    {
        try
        {
            SetLaunchAtStartup(false);
            RunSchtasks($"/Delete /TN \"{ScheduledTaskName}\" /F");
            if (removeAppData && Directory.Exists(AppData.Folder))
            {
                RecycleBin.MoveToRecycleBin(AppData.Folder);
            }
        }
        catch { }
    }

    private static bool RunSchtasks(string arguments, bool allowMissing = false)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            if (p is null || !p.WaitForExit(4000)) return false;
            // schtasks reports a missing task with 1 when disabling an already
            // disabled schedule. Treat that case as a successful no-op.
            return p.ExitCode == 0 || (allowMissing && p.ExitCode == 1);
        }
        catch { return false; }
    }
}
