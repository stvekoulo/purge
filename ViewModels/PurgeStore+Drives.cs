using PurgeWin.Models;
using PurgeWin.Services;
using PurgeWin.Theme;
using PurgeWin.Utilities;

namespace PurgeWin.ViewModels;

public sealed partial class PurgeStore
{
    public IReadOnlyList<string> ActiveDriveRoots => Drives.Where(d => !d.IsAllDrives && d.IsAvailable
        && (IsAllDrives || d.Key.Equals(Settings.SelectedDrive, StringComparison.OrdinalIgnoreCase))).Select(d => d.Key).ToArray();
    public bool IsAllDrives => Settings.SelectedDrive == DriveVolumeItem.AllDrivesKey;
    public bool HasActiveDrives => ActiveDriveRoots.Count > 0;
    public string ScopeTitle => Drives.FirstOrDefault(d => d.Key == Settings.SelectedDrive)?.DisplayTitle
        ?? Settings.SelectedDrive.TrimEnd('\\');
    public string ScopeStatus => !HasActiveDrives ? Loc.T("Disque indisponible. Reconnectez-le puis actualisez.", "Drive unavailable. Reconnect it and refresh.")
        : IsCleaning ? Loc.T("Nettoyage en cours — le disque reste verrouillé.", "Cleaning — drive selection is locked.")
        : IsScanning ? Loc.T($"Analyse de {ScopeTitle} · {ScanLabel}…", $"Scanning {ScopeTitle} · {ScanLabel}…")
        : new[] { CachePhase, DevPhase, LargePhase, UninstallPhase, EverythingElsePhase }.Contains(ScanPhase.Failed)
            ? Loc.T("Certaines analyses ont échoué. Actualisez pour réessayer.", "Some scans failed. Refresh to try again.")
        : !string.IsNullOrEmpty(ScanError) ? ScanError
        : Loc.T($"{LastScanText} · Toutes les vues suivent ce choix.", $"{LastScanText} · All views follow this selection.");
    public int DriveCatalogVersion { get; private set; }
    public ScanContext CurrentScanContext => _scopeSession.Current;
    public long ScopeRevision => _scopeSession.Current.Generation;
    private PathScope CreateScope() => new(ActiveDriveRoots, Settings.ExcludedPaths ?? []);

    public void SetLanguage(string language)
    {
        Settings.Language = language;
        SaveSettings();
        Loc.SetLanguage(language);
        foreach (var item in CacheItems) item.RefreshLocalizedText();
        foreach (var item in DevItems) item.RefreshLocalizedText();
        foreach (var item in LargeFileItems) item.RefreshLocalizedText();
        foreach (var item in InstalledApps) item.RefreshLocalizedText();
        foreach (var item in OrphanLeftovers) item.RefreshLocalizedText();
        RefreshDisk();
        RefreshAllFilteredViews();
        NotifyTotals();
        NotifyScope();
    }

    public void SetAppearance(string appearance)
    {
        Settings.Appearance = appearance;
        SaveSettings();
        ThemeBrushes.ApplyAppearance(appearance);
        RefreshAllFilteredViews();
    }

    public void RefreshAllFilteredViews() => OnUi(() =>
    {
        ApplyFilter(); ApplyDevFilter(); ApplyLargeFilter(); ApplyUninstallFilter();
    });

    public void SetDrive(string key)
    {
        if (IsCleaning || string.Equals(key, Settings.SelectedDrive, StringComparison.OrdinalIgnoreCase)) return;
        if (!Drives.Any(d => d.Key.Equals(key, StringComparison.OrdinalIgnoreCase))) return;
        SaveScopeSnapshot();
        ClearSelections();
        Settings.SelectedDrive = key;
        _scopeSession.Change(key, CreateScope());
        RestoreScopeSnapshot(key);
        RefreshDisk();
        SaveSettings();
        NotifyScope();
        _ = ScanEverythingAsync(force: false);
    }

    public void RefreshDrives() => RefreshDisk();

    public void RefreshDisk()
    {
        var items = new List<DriveVolumeItem>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
                items.Add(new() { Key = drive.RootDirectory.FullName, Name = drive.Name,
                    VolumeLabel = drive.VolumeLabel, TotalBytes = drive.TotalSize, FreeBytes = drive.AvailableFreeSpace,
                    IsAllDrives = false, IsActive = drive.Name.Equals(Settings.SelectedDrive, StringComparison.OrdinalIgnoreCase) });
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        if (!IsAllDrives && !items.Any(d => d.IsActive))
            items.Add(new() { Key = Settings.SelectedDrive, Name = Settings.SelectedDrive, VolumeLabel = Loc.T("Indisponible", "Unavailable"),
                TotalBytes = 0, FreeBytes = 0, IsAllDrives = false, IsActive = true, IsAvailable = false });
        items.Insert(0, new() { Key = DriveVolumeItem.AllDrivesKey, Name = Loc.T("Tous les disques", "All drives"),
            VolumeLabel = "", TotalBytes = items.Sum(d => d.TotalBytes), FreeBytes = items.Sum(d => d.FreeBytes),
            IsAllDrives = true, IsActive = IsAllDrives });
        Replace(Drives, items);
        var active = items.First(d => d.IsActive);
        DiskTotal = active.TotalBytes;
        DiskFree = active.FreeBytes;
        OnPropertyChanged(nameof(DiskUsed));
        var roots = ActiveDriveRoots;
        if (_scopeSession.Current.Generation > 0 && !_scopeSession.Current.Scope.Roots.SequenceEqual(roots, StringComparer.OrdinalIgnoreCase))
        {
            _scopeCache.Clear();
            _scopeSession.Change(Settings.SelectedDrive, CreateScope());
            RestoreScopeSnapshot(Settings.SelectedDrive);
            _ = ScanEverythingAsync(force: false);
        }
        DriveCatalogVersion++;
        OnPropertyChanged(nameof(DriveCatalogVersion));
        NotifyScope();
        var context = _scopeSession.Current;
        _ = Task.Run(() => (Bytes: roots.Sum(root => RecycleBin.Size(root)), Count: roots.Sum(root => RecycleBin.Count(root)))).ContinueWith(task => OnUi(() =>
        {
            if (task.IsCompletedSuccessfully && ReferenceEquals(context, _scopeSession.Current))
            { TrashBytes = task.Result.Bytes; TrashItemCount = task.Result.Count; }
        }));
    }

    public async Task RefreshCurrentScopeAsync()
    {
        if (IsCleaning) return;
        _scopeCache.Remove(Settings.SelectedDrive);
        _scopeSession.Change(Settings.SelectedDrive, CreateScope());
        RestoreScopeSnapshot(Settings.SelectedDrive);
        RefreshDisk();
        await ScanEverythingAsync();
    }

    public void CancelScan()
    {
        _scopeSession.Cancel();
        if (CachePhase == ScanPhase.Scanning) CachePhase = ScanPhase.NotScanned;
        if (DevPhase == ScanPhase.Scanning) DevPhase = ScanPhase.NotScanned;
        if (LargePhase == ScanPhase.Scanning) LargePhase = ScanPhase.NotScanned;
        if (UninstallPhase == ScanPhase.Scanning) UninstallPhase = ScanPhase.NotScanned;
        if (EverythingElsePhase == ScanPhase.Scanning) EverythingElsePhase = ScanPhase.NotScanned;
        ScanError = Loc.T("Analyse arrêtée. Actualisez pour continuer.", "Scan stopped. Refresh to continue.");
        ScanLabel = "";
        NotifyScanState();
    }

    private void EnsureScanContext()
    {
        if (_scopeSession.Current.Token.IsCancellationRequested)
            _scopeSession.Change(Settings.SelectedDrive, CreateScope());
    }

    public void InvalidateAnalysis()
    {
        if (IsCleaning) return;
        _scopeCache.Clear();
        _scopeSession.Change(Settings.SelectedDrive, CreateScope());
        RestoreScopeSnapshot(Settings.SelectedDrive);
        NotifyScope();
    }

    private void NotifyScope()
    {
        OnPropertyChanged(nameof(ScopeTitle)); OnPropertyChanged(nameof(ScopeStatus));
        OnPropertyChanged(nameof(HasActiveDrives)); OnPropertyChanged(nameof(IsAllDrives));
        OnPropertyChanged(nameof(ScopeRevision));
    }

    public void SaveSettings() => AppData.Save("settings.json", Settings);
    public void ExcludePath(string path)
    {
        var full = PathScope.Normalize(path);
        if (full == null || IsCleaning) return;
        if (!Settings.ExcludedPaths.Contains(full, StringComparer.OrdinalIgnoreCase)) Settings.ExcludedPaths.Add(full);
        SaveSettings();
        _scopeCache.Clear();
        _ = RefreshCurrentScopeAsync();
    }
}
