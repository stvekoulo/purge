using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PurgeWin.Controls;
using PurgeWin.Models;
using PurgeWin.Theme;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

public sealed partial class LargeFilesView : UserControl
{
    private PurgeStore Store => PurgeStore.Shared;

    public LargeFilesView()
    {
        InitializeComponent();
        FilesListView.ItemsSource = Store.FilteredLargeFiles;
        Store.PropertyChanged += (_, _) => SyncUi();
        Store.FilteredLargeFiles.CollectionChanged += (_, _) => SyncUi();
        Loc.LanguageChanged += SyncUi;
        ThemeBrushes.ThemeChanged += SyncUi;
        SyncUi();
    }

    private void SyncUi()
    {
        PageTitle.Text = Loc.T("Fichiers volumineux", "Large Files");
        SubtitleText.Text = Store.LargeSubtitle;
        ScanBtn.Content = Store.LargePhase == ScanPhase.Scanning
            ? Loc.T("Analyse...", "Scanning...")
            : Loc.T("Analyser", "Scan");
        ScanBtn.IsEnabled = !Store.IsScanning && !Store.IsCleaning;

        KeepOneBtn.Content = Loc.T("Garder un exemplaire de chaque", "Keep One of Each");
        KeepOneBtn.Visibility = Store.LargeCategory == LargeFileCategory.Duplicate ? Visibility.Visible : Visibility.Collapsed;

        CleanSelectedBtn.Content = Store.SelectedLargeCount > 0
            ? Loc.T($"Corbeille ({ByteFormatter.Format(Store.SelectedLargeBytes)})", $"Move to Trash ({ByteFormatter.Format(Store.SelectedLargeBytes)})")
            : Loc.T("Envoyer à la Corbeille", "Move to Recycle Bin");
        CleanSelectedBtn.IsEnabled = Store.SelectedLargeCount > 0 && !Store.IsCleaning && !Store.IsScanning;

        ChipAll.Content = Loc.T("Tout", "All");
        ChipVideo.Content = Loc.T("Vidéos", "Videos");
        ChipArchive.Content = "Archives";
        ChipDisk.Content = Loc.T("Images disque", "Disk Images");
        ChipVm.Content = Loc.T("Machines virtuelles", "Virtual Machines");
        ChipAi.Content = Loc.T("Modèles IA", "AI Models");
        ChipDup.Content = Loc.T("Doublons", "Duplicates");
        ChipCloud.Content = "OneDrive";
        ChipDoc.Content = "Documents";
        ChipImage.Content = "Images";
        ChipAudio.Content = "Audio";

        PurgeButton[] chips = [ChipAll, ChipVideo, ChipArchive, ChipDisk, ChipVm, ChipAi, ChipDup, ChipCloud, ChipDoc, ChipImage, ChipAudio];
        foreach (var chip in chips)
        {
            var active = Enum.TryParse<LargeFileCategory>(chip.Tag as string, out var cat) && cat == Store.LargeCategory;
            chip.Variant = active ? PurgeButtonVariant.Secondary : PurgeButtonVariant.Ghost;
        }

        SearchBox.PlaceholderText = Loc.T("Rechercher un fichier...", "Search files...");
        SelectionSummary.Text = Store.SelectedLargeSummaryText;
        AgeAllItem.Content = Loc.T("Tout âge", "Any age");
        Age30Item.Content = Loc.T("> 30 jours", "> 30 days");
        Age90Item.Content = Loc.T("> 90 jours", "> 90 days");
        Age365Item.Content = Loc.T("> 1 an", "> 1 year");

        EmptyTitle.Text = Loc.T("Aucun fichier volumineux à afficher", "No large files to display");
        EmptySubtitle.Text = Loc.T(
            "Lancez une analyse pour repérer vidéos, archives, ISO, VMs, modèles IA et doublons sur vos disques.",
            "Run a scan to find videos, archives, ISOs, VMs, AI models, and duplicates across your drives.");

        EmptyState.Visibility = Store.FilteredLargeFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FilesListView.Visibility = Store.FilteredLargeFiles.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnScanClick(object sender, RoutedEventArgs e) => _ = Store.ScanLargeFilesAsync();
    private void OnKeepOneClick(object sender, RoutedEventArgs e) => Store.KeepOneOfEachDuplicate();

    private async void OnCleanSelectedClick(object sender, RoutedEventArgs e)
    {
        var selected = Store.LargeFileItems.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0) return;
        var candidates = selected.Select(i =>
            new DeletionConfirmDialog.Candidate(i.Name, i.FullPath, i.Bytes, SafetyLevel.Medium)).ToList();
        if (await DeletionConfirmDialog.ConfirmAsync(XamlRoot, candidates))
            await Store.CleanSelectedLargeFilesAsync();
    }

    private void OnChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is PurgeButton { Tag: string tag } && Enum.TryParse<LargeFileCategory>(tag, out var cat))
        {
            Store.LargeCategory = cat;
            Store.ApplyLargeFilter();
        }
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        Store.LargeSearchQuery = SearchBox.Text ?? "";
        Store.ApplyLargeFilter();
    }

    private void OnSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SizeCombo is null) return;
        Store.LargeMinBytes = SizeCombo.SelectedIndex switch
        {
            1 => 50L * 1024 * 1024,
            2 => 250L * 1024 * 1024,
            3 => 1024L * 1024 * 1024,
            _ => 5L * 1024 * 1024,
        };
        Store.ApplyLargeFilter();
    }

    private void OnAgeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AgeCombo is null) return;
        Store.LargeMinAgeDays = AgeCombo.SelectedIndex switch
        {
            1 => 30,
            2 => 90,
            3 => 365,
            _ => 0,
        };
        Store.ApplyLargeFilter();
    }

    private void OnEvictCloudClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: LargeFileItem item })
            Store.EvictOneDriveFile(item);
    }

    private void OnRevealClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string p } && !string.IsNullOrEmpty(p))
            ExplorerReveal.Show(p);
    }
}
