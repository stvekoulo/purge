using PurgeWin.Services;
using PurgeWin.Utilities;

namespace PurgeWin.ViewModels;

public sealed partial class PurgeStore
{
    public bool AutoEmptyRecycleBin => Settings.EmptyRecycleBinAfterCleaning;
    public string TrashActionMessage { get; private set; } = "";

    public void SetAutoEmptyRecycleBin(bool enabled)
    {
        Settings.EmptyRecycleBinAfterCleaning = enabled;
        SaveSettings();
        OnPropertyChanged(nameof(AutoEmptyRecycleBin));
    }

    public async Task<TrashActionResult?> ActOnTrashAsync(bool restore)
    {
        if (IsCleaning || IsScanning || !HasActiveDrives) return null;
        var roots = ActiveDriveRoots;
        IsCleaning = true;
        CleaningLabel = restore ? Loc.T("Restauration demandée à Windows…", "Requesting restore from Windows…")
            : Loc.T("Vidage de la Corbeille…", "Emptying Recycle Bin…");
        NotifyScope();
        try
        {
            var result = restore ? await RecycleBin.RestoreAsync(roots) : await RecycleBin.EmptyAsync(roots);
            _scopeCache.Clear();
            TrashActionMessage = result.Failures.Count > 0
                ? Loc.T("Opération partielle. Certains éléments nécessitent une action dans Windows.", "Partial operation. Some items need attention in Windows.")
                : restore ? Loc.T($"Restauration de {result.Requested} éléments demandée à Windows.", $"Restore requested for {result.Requested} items in Windows.")
                : result.Requested == 0 ? Loc.T("Aucun élément à vider dans ce périmètre.", "No items to empty in this scope.")
                : Loc.T("La Corbeille du périmètre a été vidée.", "The Recycle Bin for this scope has been emptied.");
            OnPropertyChanged(nameof(TrashActionMessage));
            return result;
        }
        finally { IsCleaning = false; CleaningLabel = ""; RefreshDisk(); NotifyScope(); }
    }
}
