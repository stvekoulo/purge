using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
            ? Loc.T($"Libérer ({ByteFormatter.Format(Store.SelectedOneDriveLocalBytes)})",
                    $"Free up ({ByteFormatter.Format(Store.SelectedOneDriveLocalBytes)})")
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

        var totalCount = Store.OneDriveItems.Count;
        var localCount = Store.OneDriveLocalCount;
        var cloudCount = Store.OneDriveCloudOnlyCount;
        var pinnedCount = Store.OneDrivePinnedCount;

        TxtAllCardLabel.Text = Loc.T("Tout OneDrive", "All OneDrive");
        TxtAllCardBytes.Text = ByteFormatter.Format(Store.OneDriveTotalBytes);
        TxtAllCardSub.Text = Loc.T(
            $"{totalCount} fichier{(totalCount > 1 ? "s" : "")} au total",
            $"{totalCount} file{(totalCount > 1 ? "s" : "")} total");

        TxtLocalCardLabel.Text = Loc.T("Sur ce PC", "On this PC");
        TxtLocalCardBytes.Text = ByteFormatter.Format(Store.OneDriveLocalBytes);
        TxtLocalCardSub.Text = Loc.T(
            $"{localCount} fichier{(localCount > 1 ? "s" : "")} · libérable{(localCount > 1 ? "s" : "")}",
            $"{localCount} file{(localCount > 1 ? "s" : "")} · can be freed");

        TxtCloudCardLabel.Text = Loc.T("En ligne", "Online-only");
        TxtCloudCardBytes.Text = ByteFormatter.Format(Store.OneDriveCloudOnlyBytes);
        TxtCloudCardSub.Text = Loc.T(
            $"{cloudCount} fichier{(cloudCount > 1 ? "s" : "")} · 0 o sur PC",
            $"{cloudCount} file{(cloudCount > 1 ? "s" : "")} · 0 B on PC");

        TxtPinnedCardLabel.Text = Loc.T("Épinglés", "Pinned");
        TxtPinnedCardBytes.Text = ByteFormatter.Format(Store.OneDrivePinnedBytes);
        TxtPinnedCardSub.Text = Loc.T(
            $"{pinnedCount} fichier{(pinnedCount > 1 ? "s" : "")} · hors ligne",
            $"{pinnedCount} file{(pinnedCount > 1 ? "s" : "")} · kept offline");

        UpdateFilterCardsVisualState();
        UpdateStorageBar();

        SelectAllCheck.Content = Loc.T("Tout sélectionner", "Select all");
        SelectAllCheck.IsChecked = Store.IsAllOneDriveSelected;
        SearchBox.PlaceholderText = Loc.T("Rechercher un fichier...", "Search files...");
        SelectionSummary.Text = Store.SelectedOneDriveSummaryText;

        SortSizeItem.Content = Loc.T("Taille (décroissante)", "Size (largest)");
        SortDateItem.Content = Loc.T("Date de modification", "Date modified");
        SortNameItem.Content = Loc.T("Nom (A–Z)", "Name (A–Z)");

        EmptyTitle.Text = Loc.T("Aucun fichier OneDrive dans cette vue", "No OneDrive files in this view");
        EmptySubtitle.Text = Loc.T(
            "Sélectionnez un autre filtre ci-dessus ou lancez une analyse OneDrive.",
            "Select another filter card above or run a OneDrive scan.");

        EmptyState.Visibility = Store.FilteredOneDriveItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FilesListView.Visibility = Store.FilteredOneDriveItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateFilterCardsVisualState()
    {
        var activeBorder = ThemeBrushes.Get(this, "ActionPrimary");
        var subtleBorder = ThemeBrushes.Get(this, "Hairline");
        var activeBg = ThemeBrushes.Get(this, "FillSecondary");
        var normalBg = ThemeBrushes.Get(this, "SurfaceElevated");

        (Border Card, OneDriveFilter Filter)[] cards =
        [
            (CardAll, OneDriveFilter.All),
            (CardLocal, OneDriveFilter.Local),
            (CardCloud, OneDriveFilter.CloudOnly),
            (CardPinned, OneDriveFilter.Pinned),
        ];

        foreach (var (card, filter) in cards)
        {
            bool active = Store.OneDriveStateFilter == filter;
            card.BorderBrush = active ? activeBorder : subtleBorder;
            card.Background = active ? activeBg : normalBg;
        }
    }

    private void UpdateStorageBar()
    {
        double total = Math.Max(1, Store.OneDriveTotalBytes);
        double localOnly = Math.Max(0, Store.OneDriveLocalBytes - Store.OneDrivePinnedBytes);
        double pinned = Math.Max(0, Store.OneDrivePinnedBytes);
        double cloud = Math.Max(0, Store.OneDriveCloudOnlyBytes);

        if (Store.OneDriveTotalBytes <= 0)
        {
            ColBarLocal.Width = new GridLength(0);
            ColBarPinned.Width = new GridLength(0);
            ColBarCloud.Width = new GridLength(1, GridUnitType.Star);
            return;
        }

        ColBarLocal.Width = new GridLength(localOnly / total, GridUnitType.Star);
        ColBarPinned.Width = new GridLength(pinned / total, GridUnitType.Star);
        ColBarCloud.Width = new GridLength(Math.Max(0.001, cloud / total), GridUnitType.Star);
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

    private void OnFilterCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && Enum.TryParse<OneDriveFilter>(tag, out var filter))
        {
            Store.OneDriveStateFilter = filter;
            Store.ApplyOneDriveFilter();
            SyncUi();
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

    private void OnRowPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
            border.BorderBrush = ThemeBrushes.Get(this, "BorderStrong");
    }

    private void OnRowPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
            border.BorderBrush = ThemeBrushes.Get(this, "Hairline");
    }
}
