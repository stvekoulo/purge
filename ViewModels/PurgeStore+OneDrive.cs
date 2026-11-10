using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PurgeWin.Models;
using PurgeWin.Services;
using PurgeWin.Utilities;

namespace PurgeWin.ViewModels;

public sealed partial class PurgeStore
{
    public ObservableCollection<OneDriveFileItem> OneDriveItems { get; } = new BulkObservableCollection<OneDriveFileItem>();
    public ObservableCollection<OneDriveFileItem> FilteredOneDriveItems { get; } = [];

    [ObservableProperty] private ScanPhase _oneDrivePhase;
    [ObservableProperty] private OneDriveFilter _oneDriveStateFilter = OneDriveFilter.All;
    [ObservableProperty] private string _oneDriveSearchQuery = "";
    [ObservableProperty] private SortOrder _oneDriveSort = SortOrder.Largest;
    [ObservableProperty] private bool? _isAllOneDriveSelected = false;

    public bool IsOneDriveAvailable => OneDriveScanner.IsOneDriveAvailable;
    public long OneDriveLocalBytes => OneDriveItems.Where(i => i.IsLocalOnDisk).Sum(i => i.Bytes);
    public long OneDriveCloudOnlyBytes => OneDriveItems.Where(i => i.SyncState == OneDriveSyncState.CloudOnly).Sum(i => i.Bytes);
    public long OneDrivePinnedBytes => OneDriveItems.Where(i => i.SyncState == OneDriveSyncState.Pinned).Sum(i => i.Bytes);
    public long OneDriveTotalBytes => OneDriveItems.Sum(i => i.Bytes);

    public int OneDriveLocalCount => OneDriveItems.Count(i => i.IsLocalOnDisk);
    public int OneDriveCloudOnlyCount => OneDriveItems.Count(i => i.SyncState == OneDriveSyncState.CloudOnly);
    public int OneDrivePinnedCount => OneDriveItems.Count(i => i.SyncState == OneDriveSyncState.Pinned);

    public int SelectedOneDriveCount => OneDriveItems.Count(i => i.IsSelected);
    public long SelectedOneDriveBytes => OneDriveItems.Where(i => i.IsSelected).Sum(i => i.Bytes);
    public int SelectedOneDriveLocalCount => OneDriveItems.Count(i => i.IsSelected && i.CanEvictLocalCopy);
    public long SelectedOneDriveLocalBytes => OneDriveItems.Where(i => i.IsSelected && i.CanEvictLocalCopy).Sum(i => i.Bytes);

    public string OneDriveSubtitle => OneDriveItems.Count == 0
        ? EmptyScanText(OneDrivePhase)
        : Loc.T(
            $"{OneDriveItems.Count} fichiers · {ByteFormatter.Format(OneDriveLocalBytes)} sur ce PC · {ByteFormatter.Format(OneDriveCloudOnlyBytes)} en ligne",
            $"{OneDriveItems.Count} files · {ByteFormatter.Format(OneDriveLocalBytes)} on this PC · {ByteFormatter.Format(OneDriveCloudOnlyBytes)} online-only");

    public string SelectedOneDriveSummaryText => SelectedOneDriveCount == 0
        ? Loc.T("Aucun élément sélectionné", "No items selected")
        : Loc.T(
            $"{SelectedOneDriveCount} sélectionné{(SelectedOneDriveCount > 1 ? "s" : "")} ({ByteFormatter.Format(SelectedOneDriveBytes)})",
            $"{SelectedOneDriveCount} selected ({ByteFormatter.Format(SelectedOneDriveBytes)})");

    public Task ScanOneDriveAsync() => RunScanAsync("OneDrive", p => OneDrivePhase = p,
        c => OneDriveScanner.ScanAsync(c.Scope.Excluded.ToHashSet(StringComparer.OrdinalIgnoreCase), c.Scope.Roots, c.Token),
        items => Replace(OneDriveItems, items, item => item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(OneDriveFileItem.IsSelected) or nameof(OneDriveFileItem.SyncState))
                NotifyOneDriveSelection();
        }));

    public void ApplyOneDriveFilter()
    {
        var q = OneDriveSearchQuery.Trim();
        var items = OneDriveItems.AsEnumerable();
        if (!string.IsNullOrEmpty(q))
        {
            items = items.Where(i => i.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || i.FolderDisplay.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        items = OneDriveStateFilter switch
        {
            OneDriveFilter.Local => items.Where(i => i.IsLocalOnDisk),
            OneDriveFilter.CloudOnly => items.Where(i => i.SyncState == OneDriveSyncState.CloudOnly),
            OneDriveFilter.Pinned => items.Where(i => i.SyncState == OneDriveSyncState.Pinned),
            _ => items,
        };
        items = OneDriveSort switch
        {
            SortOrder.Name => items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
            SortOrder.DateModified => items.OrderByDescending(i => i.LastModified),
            _ => items.OrderByDescending(i => i.Bytes),
        };
        FilteredOneDriveItems.Clear();
        foreach (var item in items) FilteredOneDriveItems.Add(item);
        NotifyOneDriveSelection();
    }

    public void SelectAllOneDrive(bool select)
    {
        foreach (var item in FilteredOneDriveItems) item.IsSelected = select;
        NotifyOneDriveSelection();
    }

    public void NotifyOneDriveSelection()
    {
        if (FilteredOneDriveItems.Count == 0) _isAllOneDriveSelected = false;
        else
        {
            var sel = FilteredOneDriveItems.Count(i => i.IsSelected);
            _isAllOneDriveSelected = sel == FilteredOneDriveItems.Count ? true : sel == 0 ? false : null;
        }
        OnPropertyChanged(nameof(IsAllOneDriveSelected));
        OnPropertyChanged(nameof(SelectedOneDriveCount));
        OnPropertyChanged(nameof(SelectedOneDriveBytes));
        OnPropertyChanged(nameof(SelectedOneDriveLocalCount));
        OnPropertyChanged(nameof(SelectedOneDriveLocalBytes));
        OnPropertyChanged(nameof(SelectedOneDriveSummaryText));
        OnPropertyChanged(nameof(OneDriveLocalBytes));
        OnPropertyChanged(nameof(OneDriveCloudOnlyBytes));
        OnPropertyChanged(nameof(OneDrivePinnedBytes));
        OnPropertyChanged(nameof(OneDriveTotalBytes));
        OnPropertyChanged(nameof(OneDriveLocalCount));
        OnPropertyChanged(nameof(OneDriveCloudOnlyCount));
        OnPropertyChanged(nameof(OneDrivePinnedCount));
        OnPropertyChanged(nameof(OneDriveSubtitle));
    }

    public bool EvictOneDriveItem(OneDriveFileItem item)
    {
        if (!CanClean || !item.CanEvictLocalCopy || !CreateScope().CanDelete(item.FullPath)) return false;
        var ok = OneDriveScanner.EvictLocalCopy(item.FullPath);
        if (ok)
        {
            item.SyncState = OneDriveSyncState.CloudOnly;
            RefreshDisk();
            ApplyOneDriveFilter();
            NotifyTotals();
        }
        return ok;
    }

    public async Task<int> EvictSelectedOneDriveAsync()
    {
        if (!CanClean) return 0;
        var scope = CreateScope();
        var targets = OneDriveItems.Where(i => i.IsSelected && i.CanEvictLocalCopy && scope.CanDelete(i.FullPath)).ToList();
        if (targets.Count == 0) return 0;

        IsCleaning = true;
        CleaningLabel = Loc.T("Libération de l'espace local OneDrive…", "Freeing local OneDrive space…");
        NotifyScope();
        var evicted = new List<OneDriveFileItem>();
        try
        {
            await Task.Run(() =>
            {
                foreach (var item in targets)
                {
                    if (OneDriveScanner.EvictLocalCopy(item.FullPath))
                        evicted.Add(item);
                }
            });
            OnUi(() =>
            {
                foreach (var item in evicted)
                {
                    item.SyncState = OneDriveSyncState.CloudOnly;
                    item.IsSelected = false;
                }
                ApplyOneDriveFilter();
                NotifyTotals();
            });
            RefreshDisk();
        }
        finally
        {
            IsCleaning = false;
            CleaningLabel = "";
            NotifyScope();
        }
        return evicted.Count;
    }

    public async Task<CleanOutcome?> CleanSelectedOneDriveAsync()
    {
        var selected = OneDriveItems.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0) return null;
        var outcome = await RunCleanAsync([], selected.Select(i => (i.Name, i.FullPath)).ToList(), "manual");
        if (outcome is not null)
        {
            var removed = outcome.MovedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            OnUi(() =>
            {
                var remaining = OneDriveItems
                    .Where(i => !removed.Contains(i.FullPath) && File.Exists(i.FullPath))
                    .ToList();
                Replace(OneDriveItems, remaining, item => item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is nameof(OneDriveFileItem.IsSelected) or nameof(OneDriveFileItem.SyncState))
                        NotifyOneDriveSelection();
                });
                ApplyOneDriveFilter();
                NotifyTotals();
            });
        }
        return outcome;
    }
}
