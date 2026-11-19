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
        if (DispatcherQueue is { } q && !q.HasThreadAccess) { q.TryEnqueue(UpdateUI); return; }
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
        TxtOneDriveTitle.Text = Loc.T("OneDrive (espace local)", "OneDrive (local space)");
        TxtUninstallTitle.Text = Loc.T("Applications & Restes orphelins", "Installed Apps & Leftovers");
        TxtOptTitle.Text = Loc.T("Optimisation & Démarrage", "Optimization & Startup");
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

        bool hasOneDrive = Store.IsOneDriveAvailable;
        RowOneDrive.Visibility = SepOneDrive.Visibility = hasOneDrive ? Visibility.Visible : Visibility.Collapsed;
        if (hasOneDrive)
        {
            TxtOneDriveSubtitle.Text = Store.OneDriveSubtitle;
            TxtOneDriveSize.Text = ByteFormatter.Format(Store.OneDriveLocalBytes);
            TxtOneDrivePercent.Text = ByteFormatter.Percent(Store.OneDriveLocalBytes, Store.DiskTotal);
        }

        TxtUninstallSubtitle.Text = Store.UninstallSubtitle;
        TxtUninstallSize.Text = ByteFormatter.Format(Store.InstalledAppBytes + Store.LeftoverBytes);

        TxtOptSubtitle.Text = Store.OptimizationSubtitle;
        TxtOptBadge.Text = Store.StartupItems.Count > 0
            ? Loc.T($"{Store.EnabledStartupCount} actives", $"{Store.EnabledStartupCount} active")
            : Loc.T("Maintenance", "Maintenance");
    }

    private async void BtnScanAll_Click(object sender, RoutedEventArgs e) =>
        await Store.ScanEverythingAsync();

    private async void BtnCleanSafe_Click(object sender, RoutedEventArgs e)
    {
        var keys = Store.CacheItems.Where(i => i.IsSafe).Select(i => i.Entry.Key)
            .Concat(Store.DevItems.Where(i => i.IsSafe && i.Section == Models.DevSectionKind.GlobalCache).Select(i => i.Id));
        var running = Services.RunningAppDetector.FindRunning(keys);
        if (running.Count > 0 && XamlRoot is not null)
        {
            var names = string.Join(", ", running.Select(r => r.AppName));
            var dlg = new ContentDialog
            {
                XamlRoot = XamlRoot,
                RequestedTheme = Theme.ThemeBrushes.IsDarkTheme() ? ElementTheme.Dark : ElementTheme.Light,
                Title = Loc.T("Applications ouvertes détectées", "Running applications detected"),
                Content = new TextBlock
                {
                    Text = Loc.T(
                        $"{names} sont en cours d'exécution. Les fermer proprement permet de nettoyer la totalité de leur cache sans fichier verrouillé.",
                        $"{names} are currently running. Closing them gracefully allows Purge to clean 100% of their cache without locked files."),
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 440,
                },
                PrimaryButtonText = Loc.T("Fermer et nettoyer", "Close & clean"),
                SecondaryButtonText = Loc.T("Nettoyer sans fermer", "Clean without closing"),
                CloseButtonText = Loc.T("Annuler", "Cancel"),
                DefaultButton = ContentDialogButton.Primary,
            };
            var res = await dlg.ShowAsync();
            if (res == ContentDialogResult.None) return;
            if (res == ContentDialogResult.Primary)
                await Services.RunningAppDetector.CloseGracefullyAsync(running);
        }
        await Store.CleanSafeItemsAsync();
    }

    private void RowAppCaches_Click(object sender, RoutedEventArgs e) =>
        Store.SelectedTab = AppTab.AppCaches;

    private void RowDevTools_Click(object sender, RoutedEventArgs e) =>
        Store.SelectedTab = AppTab.DevTools;

    private void RowLargeFiles_Click(object sender, RoutedEventArgs e) =>
        Store.SelectedTab = AppTab.LargeFiles;

    private void RowOneDrive_Click(object sender, RoutedEventArgs e) =>
        Store.SelectedTab = AppTab.OneDrive;

    private void RowUninstaller_Click(object sender, RoutedEventArgs e) =>
        Store.SelectedTab = AppTab.Uninstaller;

    private void RowOptimization_Click(object sender, RoutedEventArgs e) =>
        Store.SelectedTab = AppTab.Optimization;

    private void RowEverythingElse_Click(object sender, RoutedEventArgs e) =>
        Store.OverviewPage = OverviewSubPage.EverythingElse;
}
