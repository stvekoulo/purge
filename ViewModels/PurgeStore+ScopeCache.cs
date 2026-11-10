using PurgeWin.Models;
using PurgeWin.Services;

namespace PurgeWin.ViewModels;

public sealed partial class PurgeStore
{
    public sealed class ScanCacheRecord
    {
        public int SchemaVersion { get; set; }
        public DateTime? LastScan { get; set; }
        public string ScopeKey { get; set; } = "";
        public List<LargeFileCacheDto> LargeFiles { get; set; } = [];
    }

    public sealed record LargeFileCacheDto(string FullPath, string Name, string FolderDisplay,
        long Bytes, DateTime LastModified, LargeFileCategory Category, string? SpecialLabel,
        string? SpecialExplanation, bool IsOneDriveSynced, string? NameEn = null,
        string? FolderDisplayEn = null, string? SpecialLabelEn = null, string? SpecialExplanationEn = null,
        string? DuplicateGroupId = null, bool IsDuplicateKeeper = false, int DuplicateGroupCount = 0);

    private sealed record ScopeSnapshot(List<CacheItem> Caches, List<DevItem> Dev,
        List<LargeFileItem> Large, List<OneDriveFileItem> OneDrive, List<InstalledAppItem> Apps, List<OrphanLeftoverItem> Orphans,
        List<CliToolItem> Cli, List<EverythingElseFolder> Folders, ScanPhase[] Phases, DateTime? LastScan);
    private readonly Dictionary<string, ScopeSnapshot> _scopeCache = new(StringComparer.OrdinalIgnoreCase);

    public void SaveScanCache()
    {
        try
        {
            if (LargeFileItems.Count == 0 && LastScan == null) return;
            var record = new ScanCacheRecord
            {
                LastScan = LastScan,
                SchemaVersion = 2,
                ScopeKey = _scopeSession.Current.Key,
                LargeFiles = LargeFileItems.Select(l => new LargeFileCacheDto(l.FullPath, l.NameFr,
                    l.FolderDisplayFr, l.Bytes, l.LastModified, l.Category, l.SpecialLabelFr,
                    l.SpecialExplanationFr, l.IsOneDriveSynced, l.NameEn, l.FolderDisplayEn,
                    l.SpecialLabelEn, l.SpecialExplanationEn, l.DuplicateGroupId,
                    l.IsDuplicateKeeper, l.DuplicateGroupCount)).ToList()
            };
            AppData.Save("scan-cache.json", record);
        }
        catch { }
    }

    public void LoadScanCache()
    {
        try
        {
            var cache = AppData.Load<ScanCacheRecord>("scan-cache.json");
            if (HasActiveDrives && cache?.SchemaVersion == 2 && cache.LastScan != null
                && string.Equals(cache.ScopeKey, _scopeSession.Current.Key, StringComparison.OrdinalIgnoreCase)
                && (DateTime.Now - cache.LastScan.Value) < TimeSpan.FromHours(24)
                && cache.LargeFiles.Count > 0)
            {
                var scope = CreateScope();
                // Cached entries may have been deleted, excluded or moved since
                // the last session. Never expose entries from another scope.
                var items = cache.LargeFiles.Where(d => scope.CanDelete(d.FullPath)
                        && (File.Exists(d.FullPath) || Directory.Exists(d.FullPath)))
                    .Select(d => new LargeFileItem
                {
                    FullPath = d.FullPath,
                    Name = d.Name,
                    NameEn = d.NameEn,
                    FolderDisplay = d.FolderDisplay,
                    FolderDisplayEn = d.FolderDisplayEn,
                    Bytes = d.Bytes,
                    LastModified = d.LastModified,
                    Category = d.Category,
                    SpecialLabel = d.SpecialLabel,
                    SpecialLabelEn = d.SpecialLabelEn,
                    SpecialExplanation = d.SpecialExplanation,
                    SpecialExplanationEn = d.SpecialExplanationEn,
                    IsOneDriveSynced = d.IsOneDriveSynced,
                    DuplicateGroupId = d.DuplicateGroupId,
                    IsDuplicateKeeper = d.IsDuplicateKeeper,
                    DuplicateGroupCount = d.DuplicateGroupCount
                }).ToList();
                DuplicateAndAiScanner.NormalizeDuplicateGroups(items);
                if (items.Count == 0) return;
                LastScan = cache.LastScan;
                Replace(LargeFileItems, items);
                LargePhase = ScanPhase.Done;
                RefreshAllFilteredViews();
                NotifyTotals();
                NotifyScanState();
            }
        }
        catch { }
    }

    private void SaveScopeSnapshot()
    {
        if (_scopeSession.Current.Generation == 0) return;
        _scopeCache[_scopeSession.Current.Key] = new(CacheItems.ToList(), DevItems.ToList(), LargeFileItems.ToList(),
            OneDriveItems.ToList(), InstalledApps.ToList(), OrphanLeftovers.ToList(), CliTools.ToList(), EverythingElseFolders.ToList(),
            [CachePhase, DevPhase, LargePhase, OneDrivePhase, UninstallPhase, EverythingElsePhase], LastScan);
    }

    private void RestoreScopeSnapshot(string key)
    {
        ClearSelections();
        CacheItems.Clear(); DevItems.Clear(); LargeFileItems.Clear(); OneDriveItems.Clear(); InstalledApps.Clear();
        OrphanLeftovers.Clear(); CliTools.Clear(); EverythingElseFolders.Clear();
        CachePhase = DevPhase = LargePhase = OneDrivePhase = UninstallPhase = EverythingElsePhase = ScanPhase.NotScanned;
        LastScan = null;
        ScanLabel = "";
        SetScanNotice(ScanNotice.None);
        if (HasActiveDrives && _scopeCache.TryGetValue(key, out var saved))
        {
            static ScanPhase Done(ScanPhase phase) => phase == ScanPhase.Scanning ? ScanPhase.NotScanned : phase;
            CachePhase = Done(saved.Phases[0]); DevPhase = Done(saved.Phases[1]); LargePhase = Done(saved.Phases[2]);
            OneDrivePhase = Done(saved.Phases[3]); UninstallPhase = Done(saved.Phases[4]); EverythingElsePhase = Done(saved.Phases[5]);
            Replace(CacheItems, saved.Caches); Replace(DevItems, saved.Dev); Replace(LargeFileItems, saved.Large);
            Replace(OneDriveItems, saved.OneDrive); Replace(InstalledApps, saved.Apps); Replace(OrphanLeftovers, saved.Orphans); Replace(CliTools, saved.Cli);
            Replace(EverythingElseFolders, saved.Folders);
            LastScan = saved.LastScan;
            ClearSelections();
        }
        RefreshAllFilteredViews();
        NotifyTotals();
        NotifyScanState();
    }

    private void ClearSelections()
    {
        foreach (var item in CacheItems) item.IsSelected = false;
        foreach (var item in DevItems) item.IsSelected = false;
        foreach (var item in LargeFileItems) item.IsSelected = false;
        foreach (var item in OneDriveItems) item.IsSelected = false;
        foreach (var item in InstalledApps)
        {
            item.IsSelected = false;
            foreach (var leftover in item.Leftovers) leftover.IsSelected = false;
        }
        foreach (var item in OrphanLeftovers) item.IsSelected = false;
        foreach (var item in CliTools) item.IsSelected = false;
    }
}
