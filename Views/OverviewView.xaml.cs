using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PurgeWin.Controls;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

public sealed partial class OverviewView : UserControl
{
    private PurgeStore Store => PurgeStore.Shared;

    public OverviewView()
    {
        InitializeComponent();
        Store.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PurgeStore.ScopeTitle) or nameof(PurgeStore.IsAllDrives) or nameof(PurgeStore.DriveCatalogVersion) or nameof(PurgeStore.IsCleaning)) RebuildDriveButtons();
            UpdateUI();
        };
        Store.Drives.CollectionChanged += (_, _) => RebuildDriveButtons();
        Loc.LanguageChanged += () => { RebuildDriveButtons(); UpdateUI(); };
        Theme.ThemeBrushes.ThemeChanged += () => { RebuildDriveButtons(); UpdateUI(); };
        Loaded += (_, _) => { RebuildDriveButtons(); UpdateUI(); };
    }

    private void UpdateUI()
    {
        TxtPageTitle.Text = Loc.T("Vue d'ensemble", "Overview") + (Store.IsAllDrives ? "" : " · " + Store.ScopeTitle);
        TxtLastScan.Text = Store.LastScanText;
        TxtDrivesLabel.Text = Loc.T("Disques :", "Drives:");

        BtnScanAll.IsBusy = Store.IsScanning;
        BtnScanAll.Content = Store.IsScanning
            ? Loc.T("Analyse en cours...", "Scanning...")
            : Loc.T("Tout analyser", "Scan Everything");
        BtnScanAll.IsEnabled = !Store.IsCleaning && !Store.IsScanning && Store.HasActiveDrives;

        BtnCleanSafe.IsBusy = Store.IsCleaning;
        if (Store.IsCleaning)
        {
            BtnCleanSafe.Content = Loc.T("Nettoyage en cours...", "Cleaning...");
            CleaningBanner.Visibility = Visibility.Visible;
            TxtCleaningBannerTitle.Text = Loc.T("Nettoyage en cours...", "Cleaning in progress...");
            TxtCleaningBannerSub.Text = string.IsNullOrWhiteSpace(Store.CleaningLabel)
                ? Loc.T("Suppression sécurisée vers la Corbeille...", "Safe removal to Recycle Bin...")
                : Store.CleaningLabel;
        }
        else
        {
            CleaningBanner.Visibility = Visibility.Collapsed;
            BtnCleanSafe.Content = Store.SafeBytes > 0
                ? Loc.T($"Nettoyer ce qui est sûr ({ByteFormatter.Format(Store.SafeBytes)})", $"Clean Safe Items ({ByteFormatter.Format(Store.SafeBytes)})")
                : Loc.T("Nettoyer les éléments sûrs", "Clean Safe Items");
        }
        BtnCleanSafe.IsEnabled = Store.SafeBytes > 0 && !Store.IsScanning && !Store.IsCleaning && Store.HasActiveDrives;
        if (!Store.IsCleaning && Store.AutoEmptyRecycleBin)
            BtnCleanSafe.Content = Loc.T("Nettoyer + vider la Corbeille", "Clean + empty Recycle Bin");

        TxtCachesTitle.Text = Loc.T("Caches d'applications", "App Caches");
        TxtDevTitle.Text = Loc.T("Outils de développement", "Dev Tools");
        TxtLargeTitle.Text = Loc.T("Fichiers volumineux", "Large Files");
        TxtUninstallTitle.Text = Loc.T("Applications & Restes orphelins", "Installed Apps & Leftovers");
        TxtEverythingTitle.Text = Loc.T("Explorer l'espace disque", "Explore disk space");
        TxtEverythingSubtitle.Text = Loc.T(
            "Parcourir, rechercher et comparer les dossiers du périmètre",
            "Browse, search and compare folders in this scope");

        TxtDiskUsed.Text = Loc.T($"{ByteFormatter.Format(Store.DiskUsed)} utilisés", $"{ByteFormatter.Format(Store.DiskUsed)} used");
        TxtDiskTotal.Text = Loc.T($"sur {ByteFormatter.Format(Store.DiskTotal)}", $"of {ByteFormatter.Format(Store.DiskTotal)}");
        TxtDiskFree.Text = Loc.T($"{ByteFormatter.Format(Store.DiskFree)} libres", $"{ByteFormatter.Format(Store.DiskFree)} free");
        if (Store.DiskTotal >= 0)
        {
            TxtDiskUsed.Text = Loc.T($"{ByteFormatter.Format(Store.DiskUsed)} utilisés", $"{ByteFormatter.Format(Store.DiskUsed)} used");
            TxtDiskTotal.Text = Loc.T($"sur {ByteFormatter.Format(Store.DiskTotal)}", $"of {ByteFormatter.Format(Store.DiskTotal)}");
            TxtDiskFree.Text = Loc.T($"{ByteFormatter.Format(Store.DiskFree)} libres", $"{ByteFormatter.Format(Store.DiskFree)} free");

            double total = Math.Max(1.0, Store.DiskTotal);
            // Scans overlap (e.g. developer caches also appear in app data).
            // Adding their estimates double-counts bytes, so the disk bar
            // shows only the accurate used/free split. Category estimates are
            // presented separately below, without false stacked proportions.
            ColCaches.Width = new GridLength(0);
            ColDev.Width = new GridLength(0);
            ColLarge.Width = new GridLength(0);
            ColApps.Width = new GridLength(0);
            ColLeftovers.Width = new GridLength(0);
            ColRest.Width = new GridLength(Math.Max(0, Store.DiskUsed) / total, GridUnitType.Star);
            ColFree.Width = new GridLength(Math.Max(0, Store.DiskFree) / total, GridUnitType.Star);

            TxtCachesPercent.Text = ByteFormatter.Percent(Store.CacheBytes, Store.DiskTotal);
            TxtDevPercent.Text = ByteFormatter.Percent(Store.DevBytes, Store.DiskTotal);
            TxtLargePercent.Text = ByteFormatter.Percent(Store.LargeFileBytes, Store.DiskTotal);
            TxtUninstallPercent.Text = ByteFormatter.Percent(Store.InstalledAppBytes + Store.LeftoverBytes, Store.DiskTotal);
            TxtRestOfDiskSize.Text = ByteFormatter.Format(Store.DiskUsed);
        }

        TxtDiskBarHint.Text = Loc.T("Espace utilisé / libre · Les estimations par catégorie peuvent se recouper.",
            "Used / free space · Category estimates may overlap.");

        TxtCachesSubtitle.Text = Store.CacheSubtitle;
        TxtCachesSize.Text = ByteFormatter.Format(Store.CacheBytes);

        TxtDevSubtitle.Text = Store.DevSubtitle;
        TxtDevSize.Text = ByteFormatter.Format(Store.DevBytes);

        TxtLargeSubtitle.Text = Store.LargeSubtitle;
        TxtLargeSize.Text = ByteFormatter.Format(Store.LargeFileBytes);

        TxtUninstallSubtitle.Text = Store.UninstallSubtitle;
        TxtUninstallSize.Text = ByteFormatter.Format(Store.InstalledAppBytes + Store.LeftoverBytes);
    }

    private async void BtnScanAll_Click(object sender, RoutedEventArgs e) =>
        await Store.ScanEverythingAsync();

    private async void BtnCleanSafe_Click(object sender, RoutedEventArgs e) =>
        await Store.CleanSafeItemsAsync();

    private void RowAppCaches_Click(object sender, RoutedEventArgs e) =>
        Store.SelectedTab = AppTab.AppCaches;

    private void RowDevTools_Click(object sender, RoutedEventArgs e) =>
        Store.SelectedTab = AppTab.DevTools;

    private void RowLargeFiles_Click(object sender, RoutedEventArgs e) =>
        Store.SelectedTab = AppTab.LargeFiles;

    private void RowUninstaller_Click(object sender, RoutedEventArgs e) =>
        Store.SelectedTab = AppTab.Uninstaller;

    private void RowEverythingElse_Click(object sender, RoutedEventArgs e) =>
        Store.OverviewPage = OverviewSubPage.EverythingElse;
}
