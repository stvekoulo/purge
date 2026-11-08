using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PurgeWin.Controls;
using PurgeWin.Theme;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

public sealed partial class DevToolsView : UserControl
{
    private PurgeStore Store => PurgeStore.Shared;

    public DevToolsView()
    {
        InitializeComponent();
        DevListView.ItemsSource = Store.FilteredDevItems;
        Store.PropertyChanged += (_, _) => SyncUi();
        Store.FilteredDevItems.CollectionChanged += (_, _) => SyncUi();
        Loc.LanguageChanged += SyncUi;
        ThemeBrushes.ThemeChanged += SyncUi;
        SyncUi();
    }

    private void SyncUi()
    {
        PageTitle.Text = Loc.T("Outils de dev", "Dev Tools");
        SubtitleText.Text = Store.DevSubtitle;

        ScanBtn.IsBusy = Store.DevPhase == ScanPhase.Scanning;
        ScanBtn.Content = Store.DevPhase == ScanPhase.Scanning
            ? Loc.T("Analyse en cours...", "Scanning...")
            : Loc.T("Analyser", "Scan");
        ScanBtn.IsEnabled = !Store.IsCleaning;

        CleanSelectedBtn.IsBusy = Store.IsCleaning;
        if (Store.IsCleaning)
        {
            CleanSelectedBtn.Content = Loc.T("Nettoyage en cours...", "Cleaning...");
        }
        else
        {
            CleanSelectedBtn.Content = Store.SelectedDevCount > 0
                ? Loc.T($"Nettoyer ({ByteFormatter.Format(Store.SelectedDevBytes)})", $"Clean ({ByteFormatter.Format(Store.SelectedDevBytes)})")
                : Loc.T("Nettoyer la sélection", "Clean Selected");
        }
        CleanSelectedBtn.IsEnabled = Store.SelectedDevCount > 0 && !Store.IsScanning;

        FilterAllBtn.Content = Loc.T("Tout", "All");
        FilterGlobalBtn.Content = Loc.T("Caches globaux", "Global Caches");
        FilterProjectsBtn.Content = Loc.T("Projets de dev", "Dev Projects");
        FilterAvdBtn.Content = Loc.T("Émulateurs Android", "Android Emulators");
        FilterWorktreesBtn.Content = Loc.T("Worktrees IA", "AI Worktrees");

        FilterAllBtn.Variant = Store.DevFilter == DevSectionFilter.All ? PurgeButtonVariant.Secondary : PurgeButtonVariant.Ghost;
        FilterGlobalBtn.Variant = Store.DevFilter == DevSectionFilter.GlobalCache ? PurgeButtonVariant.Secondary : PurgeButtonVariant.Ghost;
        FilterProjectsBtn.Variant = Store.DevFilter == DevSectionFilter.ProjectArtifact ? PurgeButtonVariant.Secondary : PurgeButtonVariant.Ghost;
        FilterAvdBtn.Variant = Store.DevFilter == DevSectionFilter.AndroidAvd ? PurgeButtonVariant.Secondary : PurgeButtonVariant.Ghost;
        FilterWorktreesBtn.Variant = Store.DevFilter == DevSectionFilter.OrphanWorktree ? PurgeButtonVariant.Secondary : PurgeButtonVariant.Ghost;

        SelectAllCheck.Content = Loc.T("Tout sélectionner", "Select All");
        SelectionSummary.Text = Store.SelectedDevSummaryText;
        SortLabel.Text = Loc.T("Trier par :", "Sort by:");
        SortLargestItem.Content = Loc.T("Plus volumineux", "Largest");
        SortNameItem.Content = Loc.T("Nom", "Name");
        SortDateItem.Content = Loc.T("Date de modification", "Date Modified");

        EmptyTitle.Text = Loc.T("Aucun cache ou artefact de développement", "No developer caches or build artifacts found");
        EmptySubtitle.Text = Loc.T(
            "Lancez une analyse pour inspecter npm, NuGet, Cargo, Gradle et vos projets sur les disques sélectionnés.",
            "Run a scan to inspect npm, NuGet, Cargo, Gradle, and your projects on the selected drives.");

        EmptyState.Visibility = Store.FilteredDevItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DevListView.Visibility = Store.FilteredDevItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnScanClick(object sender, RoutedEventArgs e) => _ = Store.ScanDevToolsAsync();

    private async void OnCleanSelectedClick(object sender, RoutedEventArgs e)
    {
        var selected = Store.DevItems.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0) return;
        var candidates = selected.Select(i =>
            new DeletionConfirmDialog.Candidate(i.Title, i.Subtitle, i.Bytes, i.Level)).ToList();
        if (await DeletionConfirmDialog.ConfirmAsync(XamlRoot, candidates))
            await Store.CleanSelectedDevAsync();
    }

    private void OnFilterAllClick(object sender, RoutedEventArgs e) { Store.DevFilter = DevSectionFilter.All; Store.ApplyDevFilter(); }
    private void OnFilterGlobalClick(object sender, RoutedEventArgs e) { Store.DevFilter = DevSectionFilter.GlobalCache; Store.ApplyDevFilter(); }
    private void OnFilterProjectsClick(object sender, RoutedEventArgs e) { Store.DevFilter = DevSectionFilter.ProjectArtifact; Store.ApplyDevFilter(); }
    private void OnFilterAvdClick(object sender, RoutedEventArgs e) { Store.DevFilter = DevSectionFilter.AndroidAvd; Store.ApplyDevFilter(); }
    private void OnFilterWorktreesClick(object sender, RoutedEventArgs e) { Store.DevFilter = DevSectionFilter.OrphanWorktree; Store.ApplyDevFilter(); }

    private void OnSelectAllClick(object sender, RoutedEventArgs e) =>
        Store.SelectAllDev(SelectAllCheck.IsChecked == true);

    private void OnSortChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortCombo is null) return;
        Store.DevSort = SortCombo.SelectedIndex switch
        {
            1 => SortOrder.Name,
            2 => SortOrder.DateModified,
            _ => SortOrder.Largest,
        };
        Store.ApplyDevFilter();
    }

    private void OnRevealItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string p } && !string.IsNullOrEmpty(p))
            ExplorerReveal.Show(p);
    }

    private void OnExcludeItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string p } && !string.IsNullOrEmpty(p))
            Store.ExcludePath(p);
    }
}
