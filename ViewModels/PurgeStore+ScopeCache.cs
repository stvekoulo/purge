using PurgeWin.Models;

namespace PurgeWin.ViewModels;

public sealed partial class PurgeStore
{
    private sealed record ScopeSnapshot(List<CacheItem> Caches, List<DevItem> Dev,
        List<LargeFileItem> Large, List<InstalledAppItem> Apps, List<OrphanLeftoverItem> Orphans,
        List<CliToolItem> Cli, List<EverythingElseFolder> Folders, ScanPhase[] Phases, DateTime? LastScan);
    private readonly Dictionary<string, ScopeSnapshot> _scopeCache = new(StringComparer.OrdinalIgnoreCase);

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
