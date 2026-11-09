using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using PurgeWin.Models;
using PurgeWin.Services;
using PurgeWin.Utilities;

namespace PurgeWin.ViewModels;

public enum AppTab { Overview, AppCaches, DevTools, LargeFiles, Uninstaller, Settings, About }
public enum OverviewSubPage { Summary, EverythingElse }
public enum ScanPhase { NotScanned, Scanning, Done, Failed }

public sealed partial class PurgeStore : ObservableObject
{
    public static PurgeStore Shared { get; } = new();
    private DispatcherQueue? _ui;
    private readonly ScopeSession _scopeSession = new();
    public AppSettings Settings { get; }
    public HistoryLog History { get; }
    public ObservableCollection<DriveVolumeItem> Drives { get; } = new BulkObservableCollection<DriveVolumeItem>();
    public ObservableCollection<CacheItem> CacheItems { get; } = new BulkObservableCollection<CacheItem>();
    public ObservableCollection<DevItem> DevItems { get; } = new BulkObservableCollection<DevItem>();
    public ObservableCollection<LargeFileItem> LargeFileItems { get; } = new BulkObservableCollection<LargeFileItem>();
    public ObservableCollection<InstalledAppItem> InstalledApps { get; } = new BulkObservableCollection<InstalledAppItem>();
    public ObservableCollection<OrphanLeftoverItem> OrphanLeftovers { get; } = new BulkObservableCollection<OrphanLeftoverItem>();
    public ObservableCollection<CliToolItem> CliTools { get; } = new BulkObservableCollection<CliToolItem>();
    public ObservableCollection<EverythingElseFolder> EverythingElseFolders { get; } = new BulkObservableCollection<EverythingElseFolder>();

    [ObservableProperty] private AppTab _selectedTab = AppTab.Overview;
    [ObservableProperty] private OverviewSubPage _overviewPage;
    [ObservableProperty] private ScanPhase _cachePhase;
    [ObservableProperty] private ScanPhase _devPhase;
    [ObservableProperty] private ScanPhase _largePhase;
    [ObservableProperty] private ScanPhase _uninstallPhase;
    [ObservableProperty] private ScanPhase _everythingElsePhase;
    [ObservableProperty] private DateTime? _lastScan;
    [ObservableProperty] private long _diskTotal;
    [ObservableProperty] private long _diskFree;
    [ObservableProperty] private long _trashBytes;
    [ObservableProperty] private long _trashItemCount;
    [ObservableProperty] private bool _isCleaning;
    [ObservableProperty] private string _cleaningLabel = "";
    [ObservableProperty] private string _scanLabel = "";
    [ObservableProperty] private string _scanError = "";

    private PurgeStore()
    {
        Settings = AppData.Load<AppSettings>("settings.json");
        History = AppData.Load<HistoryLog>("history.json");
        Settings.ExcludedPaths ??= [];
        Settings.CustomProjectRoots ??= [];
        Settings.SelectedDrive = PathScope.Normalize(Settings.SelectedDrive) is { } selected
            ? Path.GetPathRoot(selected)! : DriveVolumeItem.AllDrivesKey;
        History.Entries ??= [];
        Loc.SetLanguage(Settings.Language);
        RefreshDisk();
        _scopeSession.Change(Settings.SelectedDrive, CreateScope());
        LoadScanCache();
    }

    private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _pendingUi = new();

    public void Attach(DispatcherQueue queue)
    {
        _ui = queue;
        while (_pendingUi.TryDequeue(out var action))
        {
            _ui.TryEnqueue(() => action());
        }
    }

    private void OnUi(Action action)
    {
        if (_ui != null)
        {
            if (_ui.HasThreadAccess) action();
            else _ui.TryEnqueue(() => action());
        }
        else
        {
            if (!Thread.CurrentThread.IsThreadPoolThread && !Thread.CurrentThread.IsBackground)
                action();
            else
                _pendingUi.Enqueue(action);
        }
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
    public bool IsScanning => new[] { CachePhase, DevPhase, LargePhase, UninstallPhase, EverythingElsePhase }
        .Contains(ScanPhase.Scanning);
    public string LastScanText => IsScanning ? Loc.T("Analyse en cours…", "Scanning…")
        : LastScan == null ? Loc.T("Pas encore analysé", "Not scanned yet")
        : Loc.T("Analysé ", "Scanned ") + RelativeDate.Format(LastScan.Value);

    public bool IsCacheFresh(TimeSpan? maxAge = null)
    {
        var age = maxAge ?? TimeSpan.FromHours(24);
        return LastScan.HasValue && (DateTime.Now - LastScan.Value) < age && HasActiveDrives;
    }

    public async Task ScanEverythingAsync(bool force = true)
    {
        if (IsScanning || IsCleaning || !HasActiveDrives) return;
        if (!force && IsCacheFresh()) return;
        EnsureScanContext();
        var context = _scopeSession.Current;
        (Func<ScanPhase> Phase, Func<Task> Scan)[] scans =
        [
            (() => CachePhase, ScanAppCachesAsync), (() => DevPhase, ScanDevToolsAsync),
            (() => LargePhase, ScanLargeFilesAsync), (() => UninstallPhase, ScanUninstallerAsync),
            (() => EverythingElsePhase, ScanEverythingElseAsync)
        ];
        foreach (var scan in scans)
        {
            if (!_scopeSession.IsCurrent(context)) return;
            if (force || scan.Phase() != ScanPhase.Done) await scan.Scan();
        }
    }

    private async Task RunScanAsync<T>(string label, Action<ScanPhase> phase,
        Func<ScanContext, Task<T>> scan, Action<T> apply)
    {
        if (IsScanning || IsCleaning || !HasActiveDrives) return;
        EnsureScanContext();
        var context = _scopeSession.Current;
        ScanError = "";
        ScanLabel = label;
        phase(ScanPhase.Scanning);
        NotifyScanState();
        ScanPhase finalPhase = ScanPhase.NotScanned;
        try
        {
            var result = await scan(context);
            context.Token.ThrowIfCancellationRequested();
            OnUi(() => { if (_scopeSession.IsCurrent(context)) apply(result); });
            finalPhase = ScanPhase.Done;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            App.LogCrash($"Scan: {label}", ex);
            finalPhase = ScanPhase.Failed;
        }
        finally
        {
            OnUi(() =>
            {
                if (!_scopeSession.IsCurrent(context)) return;
                phase(finalPhase);
                ScanLabel = "";
                if (finalPhase == ScanPhase.Done)
                {
                    LastScan = DateTime.Now;
                    SaveScopeSnapshot();
                    SaveScanCache();
                }
                if (finalPhase == ScanPhase.Failed)
                    ScanError = Loc.T("Analyse incomplète. Actualisez pour réessayer.", "Incomplete scan. Refresh to try again.");
                RefreshAllFilteredViews();
                NotifyTotals();
                NotifyScanState();
            });
        }
    }

    public Task ScanAppCachesAsync() => RunScanAsync(Loc.T("Caches d'apps", "App caches"), p => CachePhase = p,
        c => CacheScanner.ScanAsync(null, c.Scope.Excluded.ToHashSet(StringComparer.OrdinalIgnoreCase), c.Token, c.Scope.Roots),
        items => Replace(CacheItems, items, item => item.PropertyChanged += (_, e) =>
        { if (e.PropertyName == nameof(CacheItem.IsSelected)) NotifySelection(); }));

    public Task ScanDevToolsAsync() => RunScanAsync(Loc.T("Outils de dev", "Dev tools"), p => DevPhase = p,
        c => DevScanner.ScanAsync(null, c.Scope.Excluded.ToHashSet(StringComparer.OrdinalIgnoreCase),
            Settings.StaleProjectDays, Settings.CustomProjectRoots.ToArray(), c.Scope.Roots, c.Token),
        items => Replace(DevItems, items, item => item.PropertyChanged += (_, e) =>
        { if (e.PropertyName == nameof(DevItem.IsSelected)) NotifyDevSelection(); }));

    public Task ScanLargeFilesAsync() => RunScanAsync(Loc.T("Fichiers volumineux", "Large files"), p => LargePhase = p,
        c => LargeFileScanner.ScanAsync(null, c.Scope.Excluded.ToHashSet(StringComparer.OrdinalIgnoreCase), c.Scope.Roots, c.Token),
        items => Replace(LargeFileItems, items, item => item.PropertyChanged += (_, e) =>
        { if (e.PropertyName == nameof(LargeFileItem.IsSelected)) NotifyLargeSelection(); }));

    public Task ScanUninstallerAsync() => RunScanAsync(Loc.T("Applications et restes", "Apps and leftovers"), p => UninstallPhase = p,
        c => UninstallScanner.ScanAllAsync(c.Scope.Excluded.ToHashSet(StringComparer.OrdinalIgnoreCase), c.Token, c.Scope.Roots,
            c.Key == DriveVolumeItem.AllDrivesKey),
        result =>
        {
            Replace(InstalledApps, result.Apps, item => item.PropertyChanged += (_, _) => NotifyUninstallSelection());
            Replace(OrphanLeftovers, result.Orphans, item => item.PropertyChanged += (_, _) => NotifyUninstallSelection());
            Replace(CliTools, result.CliTools, item => item.PropertyChanged += (_, _) => NotifyUninstallSelection());
        });

    public Task ScanEverythingElseAsync() => RunScanAsync(Loc.T("Carte des dossiers", "Folder map"), p => EverythingElsePhase = p,
        c => EverythingElseScanner.MeasureAsync(DiskUsed, 0, TrashBytes, c.Scope.Roots, c.Token, c.Scope.Excluded),
        items => Replace(EverythingElseFolders, items));

    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> items, Action<T>? attach = null)
    {
        var snapshot = items.ToArray();
        foreach (var item in snapshot) attach?.Invoke(item);
        if (collection is BulkObservableCollection<T> bulk) bulk.ReplaceAll(snapshot);
        else { collection.Clear(); foreach (var item in snapshot) collection.Add(item); }
    }

    private void NotifyScanState()
    {
        OnPropertyChanged(nameof(IsScanning));
        OnPropertyChanged(nameof(LastScanText));
        OnPropertyChanged(nameof(ScopeStatus));
    }
}
