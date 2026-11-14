using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PurgeWin.Models;
using PurgeWin.Utilities;

namespace PurgeWin.ViewModels;

public enum SafetyFilter { All, Safe, CheckFirst }
public enum SortOrder { Largest, Name, DateModified }
public enum DevSectionFilter { All, GlobalCache, AndroidAvd, ProjectArtifact, OrphanWorktree }

public sealed partial class PurgeStore
{
    [ObservableProperty] private SafetyFilter _currentFilter = SafetyFilter.All;
    [ObservableProperty] private SortOrder _currentSort = SortOrder.Largest;
    [ObservableProperty] private bool? _isAllSelected = false;

    [ObservableProperty] private DevSectionFilter _devFilter = DevSectionFilter.All;
    [ObservableProperty] private SortOrder _devSort = SortOrder.Largest;

    [ObservableProperty] private LargeFileCategory _largeCategory = LargeFileCategory.All;
    [ObservableProperty] private long _largeMinBytes = LargeFileScannerThreshold;
    [ObservableProperty] private int _largeMinAgeDays = 0;
    [ObservableProperty] private string _largeSearchQuery = "";

    [ObservableProperty] private UninstallerSubTab _uninstallSubTab = UninstallerSubTab.InstalledApps;
    [ObservableProperty] private string _uninstallSearchQuery = "";
    [ObservableProperty] private AppSortOption _uninstallSortOption = AppSortOption.Largest;
    [ObservableProperty] private bool _isUninstallGridView = true;

    private const long LargeFileScannerThreshold = 5L * 1024 * 1024;

    public ObservableCollection<CacheItem> FilteredCacheItems { get; } = [];
    public ObservableCollection<DevItem> FilteredDevItems { get; } = [];
    public ObservableCollection<LargeFileItem> FilteredLargeFiles { get; } = [];
    public ObservableCollection<InstalledAppItem> FilteredInstalledApps { get; } = [];
    public ObservableCollection<OrphanLeftoverItem> FilteredOrphanLeftovers { get; } = [];
    public ObservableCollection<CliToolItem> FilteredCliTools { get; } = [];

    public int AllCount => CacheItems.Count;
    public int CheckFirstCount => CacheItems.Count(i => i.Level == SafetyLevel.Medium);
    public int SelectedCount => CacheItems.Count(i => i.IsSelected);
    public long SelectedBytes => CacheItems.Where(i => i.IsSelected).Sum(i => i.Bytes);

    public int SelectedDevCount => DevItems.Count(i => i.IsSelected);
    public long SelectedDevBytes => DevItems.Where(i => i.IsSelected).Sum(i => i.Bytes);

    public int SelectedLargeCount => LargeFileItems.Count(i => i.IsSelected);
    public long SelectedLargeBytes => LargeFileItems.Where(i => i.IsSelected).Sum(i => i.Bytes);

    public int SelectedUninstallCount => UninstallSubTab switch
    {
        UninstallerSubTab.InstalledApps => InstalledApps.Count(i => i.IsSelected),
        UninstallerSubTab.Leftovers => OrphanLeftovers.Count(i => i.IsSelected),
        _ => CliTools.Count(i => i.IsSelected),
    };

    public long SelectedUninstallBytes => UninstallSubTab switch
    {
        UninstallerSubTab.InstalledApps => InstalledApps.Where(i => i.IsSelected)
            .Sum(i => i.TotalBytes),
        UninstallerSubTab.Leftovers => OrphanLeftovers.Where(i => i.IsSelected).Sum(i => i.Bytes),
        _ => CliTools.Where(i => i.IsSelected).Sum(i => i.Bytes),
    };

    public string CacheSubtitle => CacheItems.Count == 0
        ? EmptyScanText(CachePhase)
        : Loc.T($"{CacheItems.Count} éléments · {ByteFormatter.Format(CacheBytes)} récupérables",
                $"{CacheItems.Count} items · {ByteFormatter.Format(CacheBytes)} reclaimable");

    public string DevSubtitle => DevItems.Count == 0
        ? EmptyScanText(DevPhase)
        : Loc.T($"{DevItems.Count} éléments · {ByteFormatter.Format(DevBytes)} récupérables",
                $"{DevItems.Count} items · {ByteFormatter.Format(DevBytes)} reclaimable");

    public string LargeSubtitle => LargeFileItems.Count == 0
        ? EmptyScanText(LargePhase)
        : Loc.T($"{LargeFileItems.Count} fichiers · {ByteFormatter.Format(LargeFileBytes)} au total",
                $"{LargeFileItems.Count} files · {ByteFormatter.Format(LargeFileBytes)} total");

    public string UninstallSubtitle => (InstalledApps.Count + OrphanLeftovers.Count + CliTools.Count) == 0
        ? EmptyScanText(UninstallPhase)
        : Loc.T($"{InstalledApps.Count} apps · {OrphanLeftovers.Count} restes possibles · {CliTools.Count} outils CLI",
                $"{InstalledApps.Count} apps · {OrphanLeftovers.Count} possible leftovers · {CliTools.Count} CLI tools");

    private static string EmptyScanText(ScanPhase phase) => phase switch
    {
        ScanPhase.Scanning => Loc.T("Analyse de ce périmètre en cours…", "Scanning this scope…"),
        ScanPhase.Done => Loc.T("Aucun élément détecté sur ce périmètre", "No items found in this scope"),
        ScanPhase.Failed => Loc.T("Analyse incomplète — actualisez pour réessayer", "Incomplete scan — refresh to retry"),
        _ => Loc.T("Pas encore analysé sur ce périmètre", "This scope has not been scanned yet")
    };

    public string SelectedSummaryText => FormatSelected(SelectedCount, SelectedBytes);
    public string SelectedDevSummaryText => FormatSelected(SelectedDevCount, SelectedDevBytes);
    public string SelectedLargeSummaryText => FormatSelected(SelectedLargeCount, SelectedLargeBytes);
    public string SelectedUninstallSummaryText => FormatSelected(SelectedUninstallCount, SelectedUninstallBytes);

    private static string FormatSelected(int count, long bytes) => count == 0
        ? Loc.T("Aucun élément sélectionné", "No items selected")
        : Loc.T($"{count} sélectionné{(count > 1 ? "s" : "")} ({ByteFormatter.Format(bytes)})",
                $"{count} selected ({ByteFormatter.Format(bytes)})");

    public void SetFilter(SafetyFilter filter) { CurrentFilter = filter; ApplyFilter(); }
    public void SetSort(SortOrder sort) { CurrentSort = sort; ApplyFilter(); }

    public void ApplyFilter()
    {
        var items = CacheItems.AsEnumerable();
        items = CurrentFilter switch
        {
            SafetyFilter.Safe => items.Where(i => i.IsSafe),
            SafetyFilter.CheckFirst => items.Where(i => i.Level == SafetyLevel.Medium),
            _ => items
        };
        items = CurrentSort switch
        {
            SortOrder.Name => items.OrderBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase),
            SortOrder.DateModified => items.OrderByDescending(i => i.LastModified),
            _ => items.OrderByDescending(i => i.Bytes)
        };
        FilteredCacheItems.Clear();
        foreach (var item in items) FilteredCacheItems.Add(item);
        UpdateSelectAllState();
        NotifySelection();
    }

    public void ApplyDevFilter()
    {
        var items = DevItems.AsEnumerable();
        items = DevFilter switch
        {
            DevSectionFilter.GlobalCache => items.Where(i => i.Section == DevSectionKind.GlobalCache),
            DevSectionFilter.AndroidAvd => items.Where(i => i.Section == DevSectionKind.AndroidAvd),
            DevSectionFilter.ProjectArtifact => items.Where(i => i.Section == DevSectionKind.ProjectArtifact),
            DevSectionFilter.OrphanWorktree => items.Where(i => i.Section == DevSectionKind.OrphanWorktree),
            _ => items
        };
        items = DevSort switch
        {
            SortOrder.Name => items.OrderBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase),
            SortOrder.DateModified => items.OrderByDescending(i => i.LastModified),
            _ => items.OrderByDescending(i => i.Bytes)
        };
        FilteredDevItems.Clear();
        foreach (var item in items) FilteredDevItems.Add(item);
        NotifyDevSelection();
    }

    public void ApplyLargeFilter()
    {
        var cutoff = LargeMinAgeDays <= 0 ? DateTime.MaxValue : DateTime.Now.AddDays(-LargeMinAgeDays);
        var q = LargeSearchQuery.Trim();
        var items = LargeFileItems.Where(i => i.Bytes >= LargeMinBytes && (LargeMinAgeDays <= 0 || i.LastModified <= cutoff));
        if (!string.IsNullOrEmpty(q))
            items = items.Where(i => i.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || i.FolderDisplay.Contains(q, StringComparison.OrdinalIgnoreCase));
        items = LargeCategory switch
        {
            LargeFileCategory.All => items,
            LargeFileCategory.Duplicate => items.Where(i => i.IsDuplicate),
            LargeFileCategory.OneDrive => items.Where(i => i.IsOneDriveSynced),
            _ => items.Where(i => i.Category == LargeCategory)
        };
        FilteredLargeFiles.Clear();
        foreach (var item in items.OrderByDescending(i => i.Bytes)) FilteredLargeFiles.Add(item);
        NotifyLargeSelection();
    }

    public void KeepOneOfEachDuplicate()
    {
        foreach (var item in LargeFileItems.Where(i => i.IsDuplicate))
            item.IsSelected = !item.IsDuplicateKeeper;
        NotifyLargeSelection();
    }

    public void ApplyUninstallFilter()
    {
        var q = UninstallSearchQuery.Trim();
        var apps = InstalledApps.Where(a => string.IsNullOrEmpty(q) || a.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase));
        apps = UninstallSortOption switch
        {
            AppSortOption.Smallest => apps.OrderBy(a => a.TotalBytes),
            AppSortOption.NameAZ => apps.OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase),
            _ => apps.OrderByDescending(a => a.TotalBytes)
        };
        FilteredInstalledApps.Clear();
        foreach (var a in apps) FilteredInstalledApps.Add(a);

        FilteredOrphanLeftovers.Clear();
        foreach (var o in OrphanLeftovers.Where(o => string.IsNullOrEmpty(q) || o.FolderName.Contains(q, StringComparison.OrdinalIgnoreCase)))
            FilteredOrphanLeftovers.Add(o);

        FilteredCliTools.Clear();
        foreach (var c in CliTools.Where(c => string.IsNullOrEmpty(q) || c.Name.Contains(q, StringComparison.OrdinalIgnoreCase)))
            FilteredCliTools.Add(c);

        NotifyUninstallSelection();
    }

    public void SelectAll(bool select)
    {
        foreach (var item in FilteredCacheItems) item.IsSelected = select;
        UpdateSelectAllState();
        NotifySelection();
    }

    public void SelectAllDev(bool select)
    {
        foreach (var item in FilteredDevItems) item.IsSelected = select && item.CanSelect;
        NotifyDevSelection();
    }

    public void NotifySelection()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedBytes));
        OnPropertyChanged(nameof(SelectedSummaryText));
        UpdateSelectAllState();
    }

    public void NotifyDevSelection()
    {
        OnPropertyChanged(nameof(SelectedDevCount));
        OnPropertyChanged(nameof(SelectedDevBytes));
        OnPropertyChanged(nameof(SelectedDevSummaryText));
    }

    public void NotifyLargeSelection()
    {
        OnPropertyChanged(nameof(SelectedLargeCount));
        OnPropertyChanged(nameof(SelectedLargeBytes));
        OnPropertyChanged(nameof(SelectedLargeSummaryText));
    }

    public void NotifyUninstallSelection()
    {
        OnPropertyChanged(nameof(SelectedUninstallCount));
        OnPropertyChanged(nameof(SelectedUninstallBytes));
        OnPropertyChanged(nameof(SelectedUninstallSummaryText));
        OnPropertyChanged(nameof(IsAllUninstallSelected));
    }

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(CacheBytes));
        OnPropertyChanged(nameof(DevBytes));
        OnPropertyChanged(nameof(LargeFileBytes));
        OnPropertyChanged(nameof(InstalledAppBytes));
        OnPropertyChanged(nameof(LeftoverBytes));
        OnPropertyChanged(nameof(TotalReclaimableBytes));
        OnPropertyChanged(nameof(SafeBytes));
        OnPropertyChanged(nameof(SafeCount));
        OnPropertyChanged(nameof(AllCount));
        OnPropertyChanged(nameof(CheckFirstCount));
        OnPropertyChanged(nameof(CacheSubtitle));
        OnPropertyChanged(nameof(DevSubtitle));
        OnPropertyChanged(nameof(LargeSubtitle));
        OnPropertyChanged(nameof(UninstallSubtitle));
        NotifySelection();
        NotifyDevSelection();
        NotifyLargeSelection();
        NotifyOneDriveSelection();
        NotifyUninstallSelection();
    }

    private void UpdateSelectAllState()
    {
        if (FilteredCacheItems.Count == 0) _isAllSelected = false;
        else
        {
            var sel = FilteredCacheItems.Count(i => i.IsSelected);
            _isAllSelected = sel == FilteredCacheItems.Count ? true : sel == 0 ? false : null;
        }
        OnPropertyChanged(nameof(IsAllSelected));
    }
}
