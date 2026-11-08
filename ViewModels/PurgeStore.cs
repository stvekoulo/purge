using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using PurgeWin.Models;
using PurgeWin.Services;
using PurgeWin.Utilities;

namespace PurgeWin.ViewModels;

public enum AppTab { Overview, AppCaches, DevTools, LargeFiles, Uninstaller, Settings, About }
public enum OverviewSubPage { Summary, EverythingElse }
public enum ScanPhase { NotScanned, Scanning, Done }

/// Port of PurgeStore (the app's one source of truth), split by concern into
/// partial files (< 300 lines each): scanning here, drives/language in
/// PurgeStore+Drives.cs, filtering in PurgeStore+Filter.cs, cleaning in PurgeStore+Cleaning.cs.
public sealed partial class PurgeStore : ObservableObject
{
    public static PurgeStore Shared { get; } = new();

    private DispatcherQueue? _ui;
    private CancellationTokenSource? _scanCts;

    public AppSettings Settings { get; }
    public HistoryLog History { get; }

    public ObservableCollection<DriveVolumeItem> Drives { get; } = [];
    public ObservableCollection<CacheItem> CacheItems { get; } = [];
    public ObservableCollection<DevItem> DevItems { get; } = [];
    public ObservableCollection<LargeFileItem> LargeFileItems { get; } = [];
    public ObservableCollection<InstalledAppItem> InstalledApps { get; } = [];
    public ObservableCollection<OrphanLeftoverItem> OrphanLeftovers { get; } = [];
    public ObservableCollection<CliToolItem> CliTools { get; } = [];
    public ObservableCollection<EverythingElseFolder> EverythingElseFolders { get; } = [];

    [ObservableProperty] private AppTab _selectedTab = AppTab.Overview;
    [ObservableProperty] private OverviewSubPage _overviewPage = OverviewSubPage.Summary;
    [ObservableProperty] private ScanPhase _cachePhase = ScanPhase.NotScanned;
    [ObservableProperty] private ScanPhase _devPhase = ScanPhase.NotScanned;
    [ObservableProperty] private ScanPhase _largePhase = ScanPhase.NotScanned;
    [ObservableProperty] private ScanPhase _uninstallPhase = ScanPhase.NotScanned;
    [ObservableProperty] private ScanPhase _everythingElsePhase = ScanPhase.NotScanned;
    [ObservableProperty] private DateTime? _lastScan;
    [ObservableProperty] private long _diskTotal;
    [ObservableProperty] private long _diskFree;
    [ObservableProperty] private long _trashBytes;
    [ObservableProperty] private bool _isCleaning;
    [ObservableProperty] private string _cleaningLabel = "";

    private PurgeStore()
    {
        Settings = AppData.Load<AppSettings>("settings.json");
        History = AppData.Load<HistoryLog>("history.json");
        Loc.SetLanguage(Settings.Language);
        RefreshDrives();
    }

    public void Attach(DispatcherQueue queue) => _ui = queue;

    private void OnUi(Action action)
    {
        if (_ui is null || _ui.HasThreadAccess) action();
        else _ui.TryEnqueue(() => action());
    }

    public long DiskUsed => Math.Max(0, DiskTotal - DiskFree);
    public long CacheBytes => CacheItems.Sum(i => i.Bytes);
    public long DevBytes => DevItems.Sum(i => i.Bytes);
    public long LargeFileBytes => LargeFileItems.Sum(i => i.Bytes);
    public long InstalledAppBytes => InstalledApps.Sum(i => i.TotalBytes);
    public long LeftoverBytes => OrphanLeftovers.Sum(i => i.Bytes) + CliTools.Sum(i => i.Bytes);
    public long TotalReclaimableBytes => CacheBytes + DevBytes + LargeFileBytes + LeftoverBytes;
    public long SafeBytes => CacheItems.Where(i => i.IsSafe).Sum(i => i.Bytes)
                           + DevItems.Where(i => i.IsSafe && i.Section == DevSectionKind.GlobalCache).Sum(i => i.Bytes);
    public int SafeCount => CacheItems.Count(i => i.IsSafe)
                          + DevItems.Count(i => i.IsSafe && i.Section == DevSectionKind.GlobalCache);

    public bool IsScanning =>
        CachePhase == ScanPhase.Scanning || DevPhase == ScanPhase.Scanning ||
        LargePhase == ScanPhase.Scanning || UninstallPhase == ScanPhase.Scanning;

    public string LastScanText => LastScan is null
        ? Loc.T("Pas encore analysé", "Not scanned yet")
        : Loc.T("Analysé ", "Scanned ") + RelativeDate.Format(LastScan.Value);

    /// Sequential Scan Everything across all categories.
    public async Task ScanEverythingAsync()
    {
        if (IsScanning) return;
        await ScanAppCachesAsync();
        await ScanDevToolsAsync();
        await ScanLargeFilesAsync();
        await ScanUninstallerAsync();
        await ScanEverythingElseAsync();
    }

    public async Task ScanAppCachesAsync()
    {
        if (CachePhase == ScanPhase.Scanning) return;
        _scanCts = new CancellationTokenSource();
        CachePhase = ScanPhase.Scanning;
        OnPropertyChanged(nameof(IsScanning));
        var excluded = Settings.ExcludedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var activeDrives = ActiveDriveRoots;
        try
        {
            var found = await CacheScanner.ScanAsync(null, excluded, _scanCts.Token);
            var filtered = found.Where(c => c.Paths.Any(p =>
                activeDrives.Count == 0 || activeDrives.Any(d => p.StartsWith(d, StringComparison.OrdinalIgnoreCase)))).ToList();
            OnUi(() =>
            {
                CacheItems.Clear();
                foreach (var item in filtered)
                {
                    item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(CacheItem.IsSelected)) NotifySelection(); };
                    CacheItems.Add(item);
                }
                ApplyFilter();
            });
        }
        catch (OperationCanceledException) { }
        LastScan = DateTime.Now;
        CachePhase = ScanPhase.Done;
        OnPropertyChanged(nameof(IsScanning));
        OnPropertyChanged(nameof(LastScanText));
        RefreshDisk();
        NotifyTotals();
    }

    public async Task ScanDevToolsAsync()
    {
        if (DevPhase == ScanPhase.Scanning) return;
        DevPhase = ScanPhase.Scanning;
        OnPropertyChanged(nameof(IsScanning));
        var excluded = Settings.ExcludedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            var found = await DevScanner.ScanAsync(null, excluded, Settings.StaleProjectDays, Settings.CustomProjectRoots, ActiveDriveRoots, CancellationToken.None);
            OnUi(() =>
            {
                DevItems.Clear();
                foreach (var item in found)
                {
                    item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(DevItem.IsSelected)) NotifyDevSelection(); };
                    DevItems.Add(item);
                }
                ApplyDevFilter();
            });
        }
        catch { }
        DevPhase = ScanPhase.Done;
        LastScan = DateTime.Now;
        OnPropertyChanged(nameof(IsScanning));
        NotifyTotals();
    }

    public async Task ScanLargeFilesAsync()
    {
        if (LargePhase == ScanPhase.Scanning) return;
        LargePhase = ScanPhase.Scanning;
        OnPropertyChanged(nameof(IsScanning));
        var excluded = Settings.ExcludedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            var found = await LargeFileScanner.ScanAsync(null, excluded, ActiveDriveRoots, CancellationToken.None);
            OnUi(() =>
            {
                LargeFileItems.Clear();
                foreach (var item in found)
                {
                    item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(LargeFileItem.IsSelected)) NotifyLargeSelection(); };
                    LargeFileItems.Add(item);
                }
                ApplyLargeFilter();
            });
        }
        catch { }
        LargePhase = ScanPhase.Done;
        LastScan = DateTime.Now;
        OnPropertyChanged(nameof(IsScanning));
        NotifyTotals();
    }

    public async Task ScanUninstallerAsync()
    {
        if (UninstallPhase == ScanPhase.Scanning) return;
        UninstallPhase = ScanPhase.Scanning;
        OnPropertyChanged(nameof(IsScanning));
        var excluded = Settings.ExcludedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            var (apps, orphans, cli) = await UninstallScanner.ScanAllAsync(excluded, CancellationToken.None);
            OnUi(() =>
            {
                InstalledApps.Clear();
                foreach (var a in apps) { a.PropertyChanged += (_, _) => NotifyUninstallSelection(); InstalledApps.Add(a); }
                OrphanLeftovers.Clear();
                foreach (var o in orphans) { o.PropertyChanged += (_, _) => NotifyUninstallSelection(); OrphanLeftovers.Add(o); }
                CliTools.Clear();
                foreach (var c in cli) { c.PropertyChanged += (_, _) => NotifyUninstallSelection(); CliTools.Add(c); }
                ApplyUninstallFilter();
            });
        }
        catch { }
        UninstallPhase = ScanPhase.Done;
        LastScan = DateTime.Now;
        OnPropertyChanged(nameof(IsScanning));
        NotifyTotals();
    }

    public async Task ScanEverythingElseAsync()
    {
        if (EverythingElsePhase == ScanPhase.Scanning) return;
        EverythingElsePhase = ScanPhase.Scanning;
        try
        {
            var folders = await EverythingElseScanner.MeasureAsync(DiskUsed, TotalReclaimableBytes + InstalledAppBytes, TrashBytes, ActiveDriveRoots);
            OnUi(() =>
            {
                EverythingElseFolders.Clear();
                foreach (var f in folders) EverythingElseFolders.Add(f);
            });
        }
        catch { }
        EverythingElsePhase = ScanPhase.Done;
        NotifyTotals();
    }
}
