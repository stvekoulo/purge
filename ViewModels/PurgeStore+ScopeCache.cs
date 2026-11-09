using PurgeWin.Models;
using PurgeWin.Services;

namespace PurgeWin.ViewModels;

public sealed partial class PurgeStore
{
    public sealed class ScanCacheRecord
    {
        public DateTime? LastScan { get; set; }
        public string ScopeKey { get; set; } = "";
        public List<LargeFileCacheDto> LargeFiles { get; set; } = [];
    }

    public sealed record LargeFileCacheDto(string FullPath, string Name, string FolderDisplay,
        long Bytes, DateTime LastModified, LargeFileCategory Category, string? SpecialLabel,
        string? SpecialExplanation, bool IsOneDriveSynced);

    private sealed record ScopeSnapshot(List<CacheItem> Caches, List<DevItem> Dev,
        List<LargeFileItem> Large, List<InstalledAppItem> Apps, List<OrphanLeftoverItem> Orphans,
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
                ScopeKey = _scopeSession.Current.Key,
                LargeFiles = LargeFileItems.Select(l => new LargeFileCacheDto(l.FullPath, l.Name,
                    l.FolderDisplay, l.Bytes, l.LastModified, l.Category, l.SpecialLabel,
                    l.SpecialExplanation, l.IsOneDriveSynced)).ToList()
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
            if (cache?.LastScan != null && (DateTime.Now - cache.LastScan.Value) < TimeSpan.FromHours(24)
                && cache.LargeFiles.Count > 0)
            {
                LastScan = cache.LastScan;
                var items = cache.LargeFiles.Select(d => new LargeFileItem
                {
                    FullPath = d.FullPath,
                    Name = d.Name,
                    FolderDisplay = d.FolderDisplay,
                    Bytes = d.Bytes,
                    LastModified = d.LastModified,
                    Category = d.Category,
                    SpecialLabel = d.SpecialLabel,
                    SpecialExplanation = d.SpecialExplanation,
                    IsOneDriveSynced = d.IsOneDriveSynced
                }).ToList();
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
            InstalledApps.ToList(), OrphanLeftovers.ToList(), CliTools.ToList(), EverythingElseFolders.ToList(),
            [CachePhase, DevPhase, LargePhase, UninstallPhase, EverythingElsePhase], LastScan);
    }

    private void RestoreScopeSnapshot(string key)
    {
        ClearSelections();
        CacheItems.Clear(); DevItems.Clear(); LargeFileItems.Clear(); InstalledApps.Clear();
        OrphanLeftovers.Clear(); CliTools.Clear(); EverythingElseFolders.Clear();
        CachePhase = DevPhase = LargePhase = UninstallPhase = EverythingElsePhase = ScanPhase.NotScanned;
        LastScan = null;
        ScanLabel = ScanError = "";
        if (HasActiveDrives && _scopeCache.TryGetValue(key, out var saved))
        {
            static ScanPhase Done(ScanPhase phase) => phase == ScanPhase.Scanning ? ScanPhase.NotScanned : phase;
            CachePhase = Done(saved.Phases[0]); DevPhase = Done(saved.Phases[1]); LargePhase = Done(saved.Phases[2]);
            UninstallPhase = Done(saved.Phases[3]); EverythingElsePhase = Done(saved.Phases[4]);
            Replace(CacheItems, saved.Caches); Replace(DevItems, saved.Dev); Replace(LargeFileItems, saved.Large);
            Replace(InstalledApps, saved.Apps); Replace(OrphanLeftovers, saved.Orphans); Replace(CliTools, saved.Cli);
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
        foreach (var item in InstalledApps)
        {
            item.IsSelected = false;
            foreach (var leftover in item.Leftovers) leftover.IsSelected = false;
        }
        foreach (var item in OrphanLeftovers) item.IsSelected = false;
        foreach (var item in CliTools) item.IsSelected = false;
    }
}
