using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PurgeWin.Models;
using PurgeWin.Theme;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

public sealed partial class AppCachesView : UserControl
{
    private bool _isUpdatingSelectAll;

    public AppCachesView()
    {
        InitializeComponent();
        ItemsList.ItemsSource = PurgeStore.Shared.FilteredCacheItems;
        PurgeStore.Shared.PropertyChanged += OnStoreChanged;
        Loc.LanguageChanged += UpdateUI;
        ThemeBrushes.ThemeChanged += UpdateUI;
        Loaded += (_, _) => UpdateUI();
    }

    private void OnStoreChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        UpdateUI();

    private void UpdateUI()
    {
        var store = PurgeStore.Shared;

        TxtPageTitle.Text = Loc.T("Caches d'applications", "App Caches");
        TxtSubtitle.Text = store.CacheSubtitle;
        TxtFilterAll.Text = $"{Loc.T("Tout", "All")} ({store.AllCount})";
        TxtFilterSafe.Text = $"{Loc.T("Sûr à nettoyer", "Safe to clean")} ({store.SafeCount})";
        TxtFilterCheck.Text = $"{Loc.T("À vérifier", "Check first")} ({store.CheckFirstCount})";

        BtnScan.IsBusy = store.CachePhase == ScanPhase.Scanning;
        BtnScan.Content = store.CachePhase == ScanPhase.Scanning
            ? Loc.T("Analyse en cours...", "Scanning...")
            : Loc.T("Analyser", "Scan");
        BtnScan.IsEnabled = !store.IsCleaning;

        BtnCleanSelected.IsBusy = store.IsCleaning;
        if (store.IsCleaning)
        {
            BtnCleanSelected.Content = Loc.T("Nettoyage en cours...", "Cleaning...");
        }
        else
        {
            BtnCleanSelected.Content = store.SelectedBytes > 0
                ? Loc.T($"Nettoyer ({ByteFormatter.Format(store.SelectedBytes)})", $"Clean ({ByteFormatter.Format(store.SelectedBytes)})")
                : Loc.T("Nettoyer la sélection", "Clean Selected");
        }
        BtnCleanSelected.IsEnabled = store.SelectedCount > 0 && !store.IsScanning;

        ChkSelectAll.Content = Loc.T("Tout sélectionner", "Select All");
        SortLargestItem.Content = Loc.T("Plus volumineux", "Largest");
        SortNameItem.Content = Loc.T("Nom", "Name");
        SortDateItem.Content = Loc.T("Date de modification", "Date Modified");

        _isUpdatingSelectAll = true;
        ChkSelectAll.IsChecked = store.IsAllSelected;
        _isUpdatingSelectAll = false;

        UpdateChipStyles();
    }

    private void UpdateChipStyles()
    {
        var filter = PurgeStore.Shared.CurrentFilter;
        var activeBrush = ThemeBrushes.Get(this, "FillSecondary");
        var inactiveBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);

        ChipAll.Background = filter == SafetyFilter.All ? activeBrush : inactiveBrush;
        ChipSafe.Background = filter == SafetyFilter.Safe ? activeBrush : inactiveBrush;
        ChipCheck.Background = filter == SafetyFilter.CheckFirst ? activeBrush : inactiveBrush;
    }

    private async void BtnScan_Click(object sender, RoutedEventArgs e) =>
        await PurgeStore.Shared.ScanAppCachesAsync();

    private async void BtnCleanSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = PurgeStore.Shared.CacheItems.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0) return;
        var candidates = selected.Select(i =>
            new DeletionConfirmDialog.Candidate(i.Title, i.PathSummary, i.Bytes, i.Level)).ToList();
        if (await DeletionConfirmDialog.ConfirmAsync(XamlRoot, candidates))
            await PurgeStore.Shared.CleanSelectedAsync();
    }

    private void ChipAll_Click(object sender, RoutedEventArgs e) =>
        PurgeStore.Shared.SetFilter(SafetyFilter.All);

    private void ChipSafe_Click(object sender, RoutedEventArgs e) =>
        PurgeStore.Shared.SetFilter(SafetyFilter.Safe);

    private void ChipCheck_Click(object sender, RoutedEventArgs e) =>
        PurgeStore.Shared.SetFilter(SafetyFilter.CheckFirst);

    private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSelectAll) return;
        PurgeStore.Shared.SelectAll(ChkSelectAll.IsChecked == true);
    }

    private void CmbSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbSort is null) return;
        var sort = CmbSort.SelectedIndex switch
        {
            1 => SortOrder.Name,
            2 => SortOrder.DateModified,
            _ => SortOrder.Largest
        };
        PurgeStore.Shared.SetSort(sort);
    }

    private void OnRevealClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: CacheItem item } && item.Paths.Count > 0)
            ExplorerReveal.Show(item.Paths[0]);
    }

    private void OnExcludeClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: CacheItem item } && item.Paths.Count > 0)
            PurgeStore.Shared.ExcludePath(item.Paths[0]);
    }
}
