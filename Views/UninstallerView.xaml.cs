using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PurgeWin.Controls;
using PurgeWin.Models;
using PurgeWin.Theme;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

public sealed partial class UninstallerView : UserControl
{
    private PurgeStore Store => PurgeStore.Shared;

    public UninstallerView()
    {
        InitializeComponent();
        AppsGridView.ItemsSource = Store.FilteredInstalledApps;
        AppsListView.ItemsSource = Store.FilteredInstalledApps;
        LeftoversListView.ItemsSource = Store.FilteredOrphanLeftovers;
        CliListView.ItemsSource = Store.FilteredCliTools;
        Store.PropertyChanged += (_, _) => SyncUi();
        Store.FilteredInstalledApps.CollectionChanged += (_, _) => SyncUi();
        Store.FilteredOrphanLeftovers.CollectionChanged += (_, _) => SyncUi();
        Store.FilteredCliTools.CollectionChanged += (_, _) => SyncUi();
        Loc.LanguageChanged += SyncUi;
        ThemeBrushes.ThemeChanged += SyncUi;
        SyncUi();
    }

    private void SyncUi()
    {
        PageTitle.Text = Loc.T("Désinstalleur", "Uninstaller");
        SubtitleText.Text = Store.UninstallSubtitle;

        ScanBtn.IsBusy = Store.UninstallPhase == ScanPhase.Scanning;
        ScanBtn.Content = Store.UninstallPhase == ScanPhase.Scanning
            ? Loc.T("Analyse en cours...", "Scanning...")
            : Loc.T("Analyser", "Scan");
        ScanBtn.IsEnabled = !Store.IsCleaning && !Store.IsScanning && Store.HasActiveDrives;

        CleanSelectedBtn.IsBusy = Store.IsCleaning;
        if (Store.IsCleaning)
        {
            CleanSelectedBtn.Content = Loc.T("Nettoyage en cours...", "Cleaning...");
        }
        else
        {
            CleanSelectedBtn.Content = Store.SelectedUninstallCount > 0
                ? Loc.T($"Nettoyer ({ByteFormatter.Format(Store.SelectedUninstallBytes)})", $"Clean ({ByteFormatter.Format(Store.SelectedUninstallBytes)})")
                : Loc.T("Nettoyer la sélection", "Clean Selected");
        }
        CleanSelectedBtn.IsEnabled = Store.SelectedUninstallCount > 0 && !Store.IsScanning;

        TabAppsBtn.Content = $"{Loc.T("Applications installées", "Installed Apps")} ({Store.InstalledApps.Count})";
        TabLeftoversBtn.Content = $"{Loc.T("Restes orphelins", "Removed App Leftovers")} ({Store.OrphanLeftovers.Count})";
        TabCliBtn.Content = $"{Loc.T("Outils CLI", "CLI Tools")} ({Store.CliTools.Count})";

        TabAppsBtn.Variant = Store.UninstallSubTab == UninstallerSubTab.InstalledApps ? PurgeButtonVariant.Secondary : PurgeButtonVariant.Ghost;
        TabLeftoversBtn.Variant = Store.UninstallSubTab == UninstallerSubTab.Leftovers ? PurgeButtonVariant.Secondary : PurgeButtonVariant.Ghost;
        TabCliBtn.Variant = Store.UninstallSubTab == UninstallerSubTab.CliTools ? PurgeButtonVariant.Secondary : PurgeButtonVariant.Ghost;

        SearchBox.PlaceholderText = Loc.T("Rechercher...", "Search...");
        SelectionSummary.Text = Store.SelectedUninstallSummaryText;

        bool isApps = Store.UninstallSubTab == UninstallerSubTab.InstalledApps;
        SortCombo.Visibility = isApps ? Visibility.Visible : Visibility.Collapsed;
        ViewSwitcher.Visibility = isApps ? Visibility.Visible : Visibility.Collapsed;

        int activeCount = Store.UninstallSubTab switch
        {
            UninstallerSubTab.InstalledApps => Store.FilteredInstalledApps.Count,
            UninstallerSubTab.Leftovers => Store.FilteredOrphanLeftovers.Count,
            _ => Store.FilteredCliTools.Count,
        };

        if (isApps)
        {
            AppsGridView.Visibility = Store.IsUninstallGridView && activeCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            AppsListView.Visibility = !Store.IsUninstallGridView && activeCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            LeftoversListView.Visibility = Visibility.Collapsed;
            CliListView.Visibility = Visibility.Collapsed;

            ViewGridBtn.Background = Store.IsUninstallGridView ? ThemeBrushes.Get("SurfaceRaised") : new SolidColorBrush(Colors.Transparent);
            ViewListBtn.Background = !Store.IsUninstallGridView ? ThemeBrushes.Get("SurfaceRaised") : new SolidColorBrush(Colors.Transparent);
            if (ViewGridBtn.Content is FontIcon gi) gi.Foreground = ThemeBrushes.Get(Store.IsUninstallGridView ? "TextPrimary" : "TextSecondary");
            if (ViewListBtn.Content is FontIcon li) li.Foreground = ThemeBrushes.Get(!Store.IsUninstallGridView ? "TextPrimary" : "TextSecondary");
        }
        else
        {
            AppsGridView.Visibility = Visibility.Collapsed;
            AppsListView.Visibility = Visibility.Collapsed;
            LeftoversListView.Visibility = Store.UninstallSubTab == UninstallerSubTab.Leftovers && activeCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            CliListView.Visibility = Store.UninstallSubTab == UninstallerSubTab.CliTools && activeCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        EmptyState.Visibility = activeCount == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyTitle.Text = Loc.T("Aucun élément à afficher", "No items to display");
        EmptySubtitle.Text = Loc.T(
            "Lancez une analyse pour inspecter vos applications, restes orphelins et outils CLI.",
            "Run a scan to inspect your installed apps, orphan leftovers, and CLI tools.");
    }

    private void OnScanClick(object sender, RoutedEventArgs e) => _ = Store.ScanUninstallerAsync();

    private async void OnCleanSelectedClick(object sender, RoutedEventArgs e)
    {
        if (Store.UninstallSubTab == UninstallerSubTab.InstalledApps)
        {
            var picked = Store.InstalledApps.Where(a => a.IsSelected).ToList();
            if (picked.Count == 1)
            {
                await UninstallReviewDialog.ShowAsync(XamlRoot, picked[0], Store);
                return;
            }
        }

        var candidates = Store.UninstallSubTab switch
        {
            UninstallerSubTab.InstalledApps => Store.InstalledApps.Where(a => a.IsSelected && a.LeftoverBytes > 0)
                .Select(a => new DeletionConfirmDialog.Candidate(a.DisplayName, a.Subtitle, a.LeftoverBytes, SafetyLevel.Medium)).ToList(),
            UninstallerSubTab.Leftovers => Store.OrphanLeftovers.Where(o => o.IsSelected)
                .Select(o => new DeletionConfirmDialog.Candidate(o.FolderName, o.FullPath, o.Bytes, SafetyLevel.Medium)).ToList(),
            _ => Store.CliTools.Where(c => c.IsSelected)
                .Select(c => new DeletionConfirmDialog.Candidate(c.Name, c.Path, c.Bytes, SafetyLevel.Medium)).ToList(),
        };
        if (candidates.Count == 0) return;
        if (await DeletionConfirmDialog.ConfirmAsync(XamlRoot, candidates))
            await Store.CleanSelectedUninstallAsync();
    }

    private void OnTabAppsClick(object sender, RoutedEventArgs e) { Store.UninstallSubTab = UninstallerSubTab.InstalledApps; SyncUi(); }
    private void OnTabLeftoversClick(object sender, RoutedEventArgs e) { Store.UninstallSubTab = UninstallerSubTab.Leftovers; SyncUi(); }
    private void OnTabCliClick(object sender, RoutedEventArgs e) { Store.UninstallSubTab = UninstallerSubTab.CliTools; SyncUi(); }

    private void OnViewGridClick(object sender, RoutedEventArgs e) { Store.IsUninstallGridView = true; SyncUi(); }
    private void OnViewListClick(object sender, RoutedEventArgs e) { Store.IsUninstallGridView = false; SyncUi(); }

    private void OnSortChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortCombo?.SelectedItem is ComboBoxItem item && Enum.TryParse<AppSortOption>(item.Tag?.ToString(), out var sort))
        {
            Store.UninstallSortOption = sort;
            Store.ApplyUninstallFilter();
        }
    }

    private void OnAppGridItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is InstalledAppItem app)
        {
            app.IsSelected = !app.IsSelected;
            Store.NotifyUninstallSelection();
        }
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        Store.UninstallSearchQuery = SearchBox.Text ?? "";
        Store.ApplyUninstallFilter();
    }

    private async void OnLaunchUninstallClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: InstalledAppItem app })
            await UninstallReviewDialog.ShowAsync(XamlRoot, app, Store);
    }

    private void OnRevealClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string p } && !string.IsNullOrEmpty(p))
            ExplorerReveal.Show(p);
    }
}
