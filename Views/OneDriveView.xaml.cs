using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PurgeWin.Controls;
using PurgeWin.Models;
using PurgeWin.Theme;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

public sealed partial class OneDriveView : UserControl
{
    private PurgeStore Store => PurgeStore.Shared;

    public OneDriveView()
    {
        InitializeComponent();
        FilesListView.ItemsSource = Store.FilteredOneDriveItems;
        Store.PropertyChanged += (_, _) => SyncUi();
        Store.FilteredOneDriveItems.CollectionChanged += (_, _) => SyncUi();
        Loc.LanguageChanged += SyncUi;
        ThemeBrushes.ThemeChanged += SyncUi;
        SyncUi();
    }

    private void SyncUi()
    {
        PageTitle.Text = "OneDrive";
        SubtitleText.Text = Store.OneDriveSubtitle;

        ScanBtn.IsBusy = Store.OneDrivePhase == ScanPhase.Scanning;
        ScanBtn.Content = Store.OneDrivePhase == ScanPhase.Scanning
            ? Loc.T("Analyse en cours...", "Scanning...")
            : Loc.T("Analyser", "Scan");
        ScanBtn.IsEnabled = !Store.IsCleaning && !Store.IsScanning && Store.HasActiveDrives;

        EvictSelectedBtn.Content = Store.SelectedOneDriveLocalCount > 0
            ? Loc.T($"Libérer l'espace ({ByteFormatter.Format(Store.SelectedOneDriveLocalBytes)})",
                    $"Free up space ({ByteFormatter.Format(Store.SelectedOneDriveLocalBytes)})")
            : Loc.T("Libérer l'espace local", "Free up local space");
        EvictSelectedBtn.IsEnabled = Store.SelectedOneDriveLocalCount > 0 && !Store.IsScanning && !Store.IsCleaning;

        CleanSelectedBtn.IsBusy = Store.IsCleaning;
        CleanSelectedBtn.Content = Store.IsCleaning
            ? Loc.T("Traitement...", "Processing...")
            : Store.SelectedOneDriveCount > 0
                ? Loc.T($"Corbeille ({ByteFormatter.Format(Store.SelectedOneDriveBytes)})",
                        $"Move to Trash ({ByteFormatter.Format(Store.SelectedOneDriveBytes)})")
                : Loc.T("Envoyer à la Corbeille", "Move to Recycle Bin");
        CleanSelectedBtn.IsEnabled = Store.SelectedOneDriveCount > 0 && !Store.IsScanning && !Store.IsCleaning;

        TxtLocalCardLabel.Text = Loc.T("Sur ce PC (Local)", "On this PC (Local)");
        TxtLocalCardBytes.Text = ByteFormatter.Format(Store.OneDriveLocalBytes);
        TxtLocalCardSub.Text = Loc.T(
            $"{Store.OneDriveLocalCount} fichier{(Store.OneDriveLocalCount > 1 ? "s" : "")} · libérable sans supprimer du Cloud",
            $"{Store.OneDriveLocalCount} file{(Store.OneDriveLocalCount > 1 ? "s" : "")} · can be freed without deleting from Cloud");

        TxtCloudCardLabel.Text = Loc.T("En ligne uniquement (Cloud)", "Online-only (Cloud)");
        TxtCloudCardBytes.Text = ByteFormatter.Format(Store.OneDriveCloudOnlyBytes);
        TxtCloudCardSub.Text = Loc.T(
            $"{Store.OneDriveCloudOnlyCount} fichier{(Store.OneDriveCloudOnlyCount > 1 ? "s" : "")} · 0 o occupé sur ce PC",
            $"{Store.OneDriveCloudOnlyCount} file{(Store.OneDriveCloudOnlyCount > 1 ? "s" : "")} · 0 B used on this PC");

        TxtPinnedCardLabel.Text = Loc.T("Toujours gardés (Épinglés)", "Always kept (Pinned)");
        TxtPinnedCardBytes.Text = ByteFormatter.Format(Store.OneDrivePinnedBytes);
        TxtPinnedCardSub.Text = Loc.T(
            $"{Store.OneDrivePinnedCount} fichier{(Store.OneDrivePinnedCount > 1 ? "s" : "")} conservé{(Store.OneDrivePinnedCount > 1 ? "s" : "")} hors ligne",
            $"{Store.OneDrivePinnedCount} file{(Store.OneDrivePinnedCount > 1 ? "s" : "")} kept offline");

        ChipAll.Content = Loc.T($"Tout ({Store.OneDriveItems.Count})", $"All ({Store.OneDriveItems.Count})");
        ChipLocal.Content = Loc.T($"Sur ce PC ({Store.OneDriveLocalCount})", $"On this PC ({Store.OneDriveLocalCount})");
        ChipCloud.Content = Loc.T($"En ligne uniquement ({Store.OneDriveCloudOnlyCount})", $"Online-only ({Store.OneDriveCloudOnlyCount})");
        ChipPinned.Content = Loc.T($"Toujours gardés ({Store.OneDrivePinnedCount})", $"Always kept ({Store.OneDrivePinnedCount})");

        PurgeButton[] chips = [ChipAll, ChipLocal, ChipCloud, ChipPinned];
        foreach (var chip in chips)
        {
            var active = Enum.TryParse<OneDriveFilter>(chip.Tag as string, out var f) && f == Store.OneDriveStateFilter;
            chip.Variant = active ? PurgeButtonVariant.Secondary : PurgeButtonVariant.Ghost;
        }

        SelectAllCheck.Content = Loc.T("Tout sélectionner", "Select all");
        SelectAllCheck.IsChecked = Store.IsAllOneDriveSelected;
        SearchBox.PlaceholderText = Loc.T("Rechercher dans OneDrive...", "Search in OneDrive...");
        SelectionSummary.Text = Store.SelectedOneDriveSummaryText;

        SortSizeItem.Content = Loc.T("Taille (décroissante)", "Size (largest)");
        SortDateItem.Content = Loc.T("Date de modification", "Date modified");
        SortNameItem.Content = Loc.T("Nom (A–Z)", "Name (A–Z)");

        EmptyTitle.Text = Loc.T("Aucun fichier OneDrive à afficher", "No OneDrive files to display");
        EmptySubtitle.Text = Loc.T(
            "Lancez une analyse pour voir vos fichiers OneDrive stockés sur ce PC ou en ligne uniquement (≥ 1 Mo).",
            "Run a scan to inspect your OneDrive files stored locally on this PC or online-only (≥ 1 MB).");

        EmptyState.Visibility = Store.FilteredOneDriveItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FilesListView.Visibility = Store.FilteredOneDriveItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnScanClick(object sender, RoutedEventArgs e) => _ = Store.ScanOneDriveAsync();

    private async void OnEvictSelectedClick(object sender, RoutedEventArgs e) =>
        await Store.EvictSelectedOneDriveAsync();

    private async void OnCleanSelectedClick(object sender, RoutedEventArgs e)
    {
        var selected = Store.OneDriveItems.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0) return;
        var candidates = selected.Select(i =>
            new DeletionConfirmDialog.Candidate(i.Name, i.FullPath, i.Bytes, SafetyLevel.Medium)).ToList();
        if (await DeletionConfirmDialog.ConfirmAsync(XamlRoot, candidates))
            await Store.CleanSelectedOneDriveAsync();
    }

    private void OnChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is PurgeButton { Tag: string tag } && Enum.TryParse<OneDriveFilter>(tag, out var filter))
        {
            Store.OneDriveStateFilter = filter;
            Store.ApplyOneDriveFilter();
        }
    }

    private void OnSelectAllClick(object sender, RoutedEventArgs e) =>
        Store.SelectAllOneDrive(SelectAllCheck.IsChecked == true);

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        Store.OneDriveSearchQuery = SearchBox.Text ?? "";
        Store.ApplyOneDriveFilter();
    }

    private void OnSortChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortCombo is null) return;
        Store.OneDriveSort = SortCombo.SelectedIndex switch
        {
            1 => SortOrder.DateModified,
            2 => SortOrder.Name,
            _ => SortOrder.Largest,
        };
        Store.ApplyOneDriveFilter();
    }

    private void OnEvictItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: OneDriveFileItem item })
            Store.EvictOneDriveItem(item);
    }

    private void OnRevealClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string p } && !string.IsNullOrEmpty(p))
            ExplorerReveal.Show(p);
    }
}
