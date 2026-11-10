using PurgeWin.Services;
using PurgeWin.Utilities;

namespace PurgeWin.ViewModels;

public sealed partial class PurgeStore
{
    public bool AutoEmptyRecycleBin => Settings.EmptyRecycleBinAfterCleaning;
    private string _trashActionMessageFr = "", _trashActionMessageEn = "";
    public string TrashActionMessage => Loc.T(_trashActionMessageFr, _trashActionMessageEn);

    private void SetTrashActionMessage(string fr, string en)
    {
        _trashActionMessageFr = fr;
        _trashActionMessageEn = en;
        OnPropertyChanged(nameof(TrashActionMessage));
    }

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
            if (result.Failures.Count > 0)
                SetTrashActionMessage("Opération partielle. Certains éléments nécessitent une action dans Windows.",
                    "Partial operation. Some items need attention in Windows.");
            else if (restore)
                SetTrashActionMessage($"Restauration de {result.Requested} éléments demandée à Windows.",
                    $"Restore requested for {result.Requested} items in Windows.");
            else if (result.Requested == 0)
                SetTrashActionMessage("Aucun élément à vider dans ce périmètre.", "No items to empty in this scope.");
            else SetTrashActionMessage("La Corbeille du périmètre a été vidée.",
                    "The Recycle Bin for this scope has been emptied.");
            return result;
        }
        finally { IsCleaning = false; CleaningLabel = ""; RefreshDisk(); NotifyScope(); }
    }
}
