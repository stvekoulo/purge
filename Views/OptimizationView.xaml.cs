using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PurgeWin.Controls;
using PurgeWin.Models;
using PurgeWin.Theme;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

public sealed partial class OptimizationView : UserControl
{
    private PurgeStore Store => PurgeStore.Shared;

    public OptimizationView()
    {
        InitializeComponent();
        StartupListView.ItemsSource = Store.FilteredStartupItems;
        MaintenanceListView.ItemsSource = Store.MaintenanceTasks;
        foreach (var task in Store.MaintenanceTasks)
            task.PropertyChanged += (_, _) => SyncUi();
        Store.PropertyChanged += (_, _) => SyncUi();
        Store.FilteredStartupItems.CollectionChanged += (_, _) => SyncUi();
        Loc.LanguageChanged += () => { Store.RefreshOptimizationLocalization(); SyncUi(); };
        ThemeBrushes.ThemeChanged += () => { Store.RefreshOptimizationLocalization(); SyncUi(); };
        if (Store.StartupItems.Count == 0 && Store.OptimizationPhase == ScanPhase.NotScanned)
            _ = Store.ScanOptimizationAsync();
        SyncUi();
    }

    private void SyncUi()
    {
        PageTitle.Text = Loc.T("Optimisation", "Optimization");
        SubtitleText.Text = Store.OptimizationSubtitle;

        ScanBtn.IsBusy = Store.OptimizationPhase == ScanPhase.Scanning;
        ScanBtn.Content = Store.OptimizationPhase == ScanPhase.Scanning
            ? Loc.T("Analyse en cours…", "Scanning…")
            : Loc.T("Actualiser", "Refresh");
        ScanBtn.IsEnabled = !Store.IsCleaning && !Store.IsScanning;

        int selTasks = Store.SelectedMaintenanceCount;
        RunMaintenanceBtn.IsBusy = Store.IsCleaning;
        RunMaintenanceBtn.Content = Store.IsCleaning
            ? (!string.IsNullOrWhiteSpace(Store.CleaningLabel) ? Store.CleaningLabel : Loc.T("Maintenance en cours…", "Running maintenance…"))
            : selTasks > 0
                ? Loc.T($"Exécuter la maintenance ({selTasks})", $"Run maintenance ({selTasks})")
                : Loc.T("Exécuter la maintenance", "Run maintenance");
        RunMaintenanceBtn.IsEnabled = !Store.IsCleaning && !Store.IsScanning && selTasks > 0;

        bool isStartup = Store.OptimizationSubTab == OptimizationSubTab.StartupApps;
        TabStartupBtn.Content = $"{Loc.T("Applications au démarrage", "Startup Apps")} ({Store.StartupItems.Count})";
        TabMaintenanceBtn.Content = $"{Loc.T("Maintenance système", "System Maintenance")} ({Store.MaintenanceTasks.Count})";
        TabStartupBtn.Variant = isStartup ? PurgeButtonVariant.Secondary : PurgeButtonVariant.Ghost;
        TabMaintenanceBtn.Variant = !isStartup ? PurgeButtonVariant.Secondary : PurgeButtonVariant.Ghost;

        SearchBox.Visibility = isStartup ? Visibility.Visible : Visibility.Collapsed;
        SearchBox.PlaceholderText = Loc.T("Rechercher une application…", "Search startup apps…");

        ToolbarHint.Text = isStartup
            ? Loc.T(
                "Désactivez les programmes inutiles au démarrage (synchronisé avec Windows, réversible).",
                "Disable unneeded login programs (synced with Task Manager; reversible).")
            : Loc.T(
                "Actions sûres et réversibles pour accélérer Windows et rafraîchir les caches système.",
                "Safe, non-destructive tasks to speed up Windows and refresh system caches.");

        StartupListView.Visibility = isStartup && Store.FilteredStartupItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        MaintenanceListView.Visibility = !isStartup ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = isStartup && Store.FilteredStartupItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyTitle.Text = Store.OptimizationPhase == ScanPhase.Scanning
            ? Loc.T("Inspection du démarrage Windows…", "Inspecting Windows startup…")
            : Loc.T("Aucune application au démarrage", "No startup applications found");
        EmptySubtitle.Text = Loc.T(
            "Aucune entrée correspondante dans le registre ou le dossier Démarrage.",
            "No matching entries in Windows Registry or Startup folder.");
    }

    private void OnScanClick(object sender, RoutedEventArgs e) => _ = Store.ScanOptimizationAsync();

    private async void OnRunSelectedMaintenanceClick(object sender, RoutedEventArgs e)
    {
        if (Store.OptimizationSubTab != OptimizationSubTab.Maintenance)
        {
            Store.OptimizationSubTab = OptimizationSubTab.Maintenance;
            SyncUi();
        }
        await Store.RunSelectedMaintenanceAsync();
    }

    private void OnTabStartupClick(object sender, RoutedEventArgs e)
    {
        Store.OptimizationSubTab = OptimizationSubTab.StartupApps;
        SyncUi();
    }

    private void OnTabMaintenanceClick(object sender, RoutedEventArgs e)
    {
        Store.OptimizationSubTab = OptimizationSubTab.Maintenance;
        SyncUi();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        Store.StartupSearchQuery = SearchBox.Text ?? "";
        Store.ApplyOptimizationFilter();
    }

    private void OnStartupItemToggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle && toggle.DataContext is StartupAppItem item)
        {
            if (item.IsEnabled == toggle.IsOn) return;
            if (!Store.ToggleStartupApp(item, toggle.IsOn))
                toggle.IsOn = item.IsEnabled;
        }
    }

    private async void OnRunSingleTaskClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MaintenanceTaskItem task })
            await Store.RunSingleMaintenanceAsync(task);
    }

    private void OnRevealClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path } && !string.IsNullOrWhiteSpace(path))
            ExplorerReveal.Show(path);
    }

    private void OnRowPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
            border.BorderBrush = ThemeBrushes.Get("ActionPrimary");
    }

    private void OnRowPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
            border.BorderBrush = ThemeBrushes.Get("Hairline");
    }
}
