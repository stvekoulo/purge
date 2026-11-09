using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PurgeWin.Services;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

public sealed partial class MainWindow
{
    private void OnTrashMenuOpening(object? sender, object e)
    {
        var store = PurgeStore.Shared;
        store.RefreshDisk();
        TrashScopeLabel.Text = Loc.T("Corbeille · ", "Recycle Bin · ") + store.ScopeTitle;
        OpenTrashItem.Text = Loc.T("Ouvrir dans Windows", "Open in Windows");
        RestoreTrashItem.Text = Loc.T("Restaurer les éléments…", "Restore items…");
        EmptyTrashItem.Text = Loc.T("Vider la Corbeille…", "Empty Recycle Bin…");
        AutoEmptyTrashItem.Text = Loc.T("Vider après nettoyage…", "Empty after cleaning…");
        AutoEmptyTrashItem.IsChecked = store.AutoEmptyRecycleBin;
        RestoreTrashItem.IsEnabled = EmptyTrashItem.IsEnabled = store.HasActiveDrives && !store.IsCleaning && !store.IsScanning;
    }

    private void OnOpenTrashInWindows(object sender, RoutedEventArgs e) => ExplorerReveal.Show("shell:RecycleBinFolder");

    private async void OnEmptyTrash(object sender, RoutedEventArgs e)
    {
        var store = PurgeStore.Shared;
        var context = store.CurrentScanContext;
        var dialog = TrashDialog(Loc.T($"Vider la Corbeille · {store.ScopeTitle} ?", $"Empty Recycle Bin · {store.ScopeTitle}?"),
            Loc.T("Les éléments de ce périmètre seront supprimés définitivement, y compris ceux déjà présents avant le dernier nettoyage.",
                "Items in this scope will be permanently deleted, including those present before the last cleanup."),
            Loc.T("Vider définitivement", "Permanently empty"));
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && ReferenceEquals(context, store.CurrentScanContext))
            await ShowTrashResult(await store.ActOnTrashAsync(false));
    }

    private async void OnRestoreTrash(object sender, RoutedEventArgs e)
    {
        var store = PurgeStore.Shared;
        var context = store.CurrentScanContext;
        var dialog = TrashDialog(Loc.T($"Restaurer · {store.ScopeTitle} ?", $"Restore · {store.ScopeTitle}?"),
            Loc.T("Windows remettra les éléments de ce périmètre à leur emplacement d'origine. Les conflits éventuels seront à confirmer dans Windows.",
                "Windows will return items in this scope to their original locations. Confirm any file conflicts in Windows."),
            Loc.T("Restaurer ce périmètre", "Restore this scope"));
        dialog.SecondaryButtonText = Loc.T("Choisir dans Windows", "Choose in Windows");
        var choice = await dialog.ShowAsync();
        if (choice == ContentDialogResult.Secondary) ExplorerReveal.Show("shell:RecycleBinFolder");
        if (choice == ContentDialogResult.Primary && ReferenceEquals(context, store.CurrentScanContext))
            await ShowTrashResult(await store.ActOnTrashAsync(true));
    }

    private async void OnAutoEmptyTrash(object sender, RoutedEventArgs e)
    {
        var store = PurgeStore.Shared;
        if (!AutoEmptyTrashItem.IsChecked) { store.SetAutoEmptyRecycleBin(false); return; }
        var dialog = TrashDialog(Loc.T("Vider automatiquement après nettoyage ?", "Empty automatically after cleaning?"),
            Loc.T("Après un nettoyage manuel ou planifié, toute la Corbeille des disques concernés sera vidée définitivement, y compris les anciens éléments. Cette option est désactivée par défaut.",
                "After a manual or scheduled cleanup, the entire Recycle Bin on the affected drives will be permanently emptied, including older items. This option is off by default."),
            Loc.T("Activer l'option", "Enable option"));
        bool enable = await dialog.ShowAsync() == ContentDialogResult.Primary;
        store.SetAutoEmptyRecycleBin(enable);
        AutoEmptyTrashItem.IsChecked = enable;
    }

    private ContentDialog TrashDialog(string title, string content, string action) => new()
    {
        XamlRoot = RootGrid.XamlRoot, Title = title,
        Content = new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap, MaxWidth = 440 },
        PrimaryButtonText = action, CloseButtonText = Loc.T("Annuler", "Cancel"), DefaultButton = ContentDialogButton.Close
    };

    private async Task ShowTrashResult(TrashActionResult? result)
    {
        if (result == null) return;
        await new ContentDialog { XamlRoot = RootGrid.XamlRoot,
            Title = Loc.T("Corbeille", "Recycle Bin"),
            Content = new TextBlock { Text = PurgeStore.Shared.TrashActionMessage, TextWrapping = TextWrapping.Wrap, MaxWidth = 440 },
            CloseButtonText = "OK" }.ShowAsync();
    }
}
