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
        Store.PropertyChanged += (_, _) => UpdateUI();
        Store.Drives.CollectionChanged += (_, _) => RebuildDriveButtons();
        Loc.LanguageChanged += () => { RebuildDriveButtons(); UpdateUI(); };
        Theme.ThemeBrushes.ThemeChanged += () => { RebuildDriveButtons(); UpdateUI(); };
        Loaded += (_, _) => { RebuildDriveButtons(); UpdateUI(); };
    }

    private void RebuildDriveButtons()
    {
        if (DriveButtonsPanel is null) return;
        DriveButtonsPanel.Children.Clear();
        foreach (var d in Store.Drives)
        {
            var btn = new PurgeButton
            {
                Content = d.IsAllDrives
                    ? $"{d.DisplayTitle} ({ByteFormatter.Format(d.FreeBytes)} {Loc.T("libres", "free")})"
                    : $"{d.DisplayTitle} · {ByteFormatter.Format(d.FreeBytes)} / {ByteFormatter.Format(d.TotalBytes)}",
                Size = PurgeButtonSize.Compact,
                Variant = d.IsActive ? PurgeButtonVariant.Secondary : PurgeButtonVariant.Ghost,
                Tag = d.Key,
            };
            btn.Click += (s, _) =>
            {
                if (s is PurgeButton { Tag: string key })
                {
                    Store.SetDrive(key);
                    RebuildDriveButtons();
                    UpdateUI();
                }
            };
            DriveButtonsPanel.Children.Add(btn);
        }
    }

    private void UpdateUI()
    {
        TxtPageTitle.Text = Loc.T("Vue d'ensemble", "Overview");
        TxtLastScan.Text = Store.LastScanText;
        TxtDrivesLabel.Text = Loc.T("Disques :", "Drives:");

        BtnScanAll.IsBusy = Store.IsScanning;
        BtnScanAll.Content = Store.IsScanning
            ? Loc.T("Analyse en cours...", "Scanning...")
            : Loc.T("Tout analyser", "Scan Everything");
        BtnScanAll.IsEnabled = !Store.IsCleaning;

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
        BtnCleanSafe.IsEnabled = Store.SafeBytes > 0 && !Store.IsScanning;

        TxtCachesTitle.Text = Loc.T("Caches d'applications", "App Caches");
        TxtDevTitle.Text = Loc.T("Outils de développement", "Dev Tools");
        TxtLargeTitle.Text = Loc.T("Fichiers volumineux", "Large Files");
        TxtUninstallTitle.Text = Loc.T("Applications & Restes orphelins", "Installed Apps & Leftovers");
        TxtEverythingTitle.Text = Loc.T("Tout le reste", "Everything else");
        TxtEverythingSubtitle.Text = Loc.T(
            "Windows, bibliothèques d'applications et dossiers personnels (cliquez pour voir la carte)",
            "Windows, app libraries, and personal folders (click to inspect treemap)");

        if (Store.DiskTotal > 0)
        {
            TxtDiskUsed.Text = Loc.T($"{ByteFormatter.Format(Store.DiskUsed)} utilisés", $"{ByteFormatter.Format(Store.DiskUsed)} used");
            TxtDiskTotal.Text = Loc.T($"sur {ByteFormatter.Format(Store.DiskTotal)}", $"of {ByteFormatter.Format(Store.DiskTotal)}");
            TxtDiskFree.Text = Loc.T($"{ByteFormatter.Format(Store.DiskFree)} libres", $"{ByteFormatter.Format(Store.DiskFree)} free");

            double total = Math.Max(1.0, Store.DiskTotal);
            var cachesW = Math.Max(0.0001, Store.CacheBytes / total);
            var devW = Math.Max(0.0001, Store.DevBytes / total);
            var largeW = Math.Max(0.0001, Store.LargeFileBytes / total);
            var appsW = Math.Max(0.0001, Store.InstalledAppBytes / total);
            var leftW = Math.Max(0.0001, Store.LeftoverBytes / total);
            var freeW = Math.Max(0.001, Store.DiskFree / total);
            var categorized = Store.CacheBytes + Store.DevBytes + Store.LargeFileBytes + Store.InstalledAppBytes + Store.LeftoverBytes;
            var restBytes = Math.Max(0, Store.DiskUsed - categorized);
            var restW = Math.Max(0.001, restBytes / total);

            ColCaches.Width = new GridLength(cachesW, GridUnitType.Star);
            ColDev.Width = new GridLength(devW, GridUnitType.Star);
            ColLarge.Width = new GridLength(largeW, GridUnitType.Star);
            ColApps.Width = new GridLength(appsW, GridUnitType.Star);
            ColLeftovers.Width = new GridLength(leftW, GridUnitType.Star);
            ColRest.Width = new GridLength(restW, GridUnitType.Star);
            ColFree.Width = new GridLength(freeW, GridUnitType.Star);

            TxtCachesPercent.Text = ByteFormatter.Percent(Store.CacheBytes, Store.DiskTotal);
            TxtDevPercent.Text = ByteFormatter.Percent(Store.DevBytes, Store.DiskTotal);
            TxtLargePercent.Text = ByteFormatter.Percent(Store.LargeFileBytes, Store.DiskTotal);
            TxtUninstallPercent.Text = ByteFormatter.Percent(Store.InstalledAppBytes + Store.LeftoverBytes, Store.DiskTotal);
            TxtRestOfDiskSize.Text = ByteFormatter.Format(restBytes);
        }

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
