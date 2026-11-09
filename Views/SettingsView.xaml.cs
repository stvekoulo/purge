using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PurgeWin.Services;
using PurgeWin.Theme;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;
using Windows.Storage.Pickers;

namespace PurgeWin.Views;

public sealed partial class SettingsView : UserControl
{
    public static event Action? ReplayOnboardingRequested;
    private bool _loading = true;

    public SettingsView()
    {
        InitializeComponent();
        Loc.LanguageChanged += SyncTexts;
        ThemeBrushes.ThemeChanged += UpdateThemeCardBorders;
        Loaded += (_, _) => LoadSettings();
    }

    private void LoadSettings()
    {
        _loading = true;
        var settings = PurgeStore.Shared.Settings;
        var history = PurgeStore.Shared.History;

        CmbLang.SelectedIndex = Loc.IsEn ? 1 : 0;
        TglStartup.IsOn = SystemIntegration.IsLaunchAtStartupEnabled();
        CmbSchedule.SelectedIndex = settings.ScheduledFrequency switch
        {
            "weekly" => 1, "monthly" => 2, "quarterly" => 3, _ => 0
        };
        CmbStale.SelectedIndex = settings.StaleProjectDays switch
        {
            30 => 1, 90 => 2, 180 => 3, _ => 0
        };
        TglAutoUpdate.IsOn = settings.AutoCheckUpdates;
        ChkAutoEmptyTrash.IsChecked = settings.EmptyRecycleBinAfterCleaning;
        _loading = false;

        SyncTexts();
        UpdateThemeCardBorders();

        long lifetime = history.Entries.Sum(e => e.FreedBytes);
        var cmp = SizeComparisonCatalog.Describe(lifetime);
        TxtLifetimeTotal.Text = lifetime > 0
            ? $"{Loc.T("Total libéré :", "Total freed:")} {ByteFormatter.Format(lifetime)}" + (cmp is { } c ? $" ({c.Label})" : "")
            : "";

        TxtHistoryEmpty.Visibility = history.Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryList.Visibility = history.Entries.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        HistoryList.ItemsSource = null;
        HistoryList.ItemsSource = history.Entries;

        RefreshFolderList(CustomRootsPanel, settings.CustomProjectRoots, OnRemoveCustomRootClick);
        RefreshExclusions();
    }

    private void UpdateThemeCardBorders()
    {
        var mode = PurgeStore.Shared.Settings.Appearance;
        Button[] cards = [BtnThemeAuto, BtnThemeLight, BtnThemeDark];
        foreach (var card in cards)
        {
            bool active = string.Equals(card.Tag as string, mode, StringComparison.OrdinalIgnoreCase);
            card.BorderBrush = ThemeBrushes.Get(this, active ? "ActionPrimary" : "BorderSubtle");
            card.Background = ThemeBrushes.Get(this, active ? "FillSecondary" : "SurfaceCard");
        }
    }

    private void RefreshExclusions()
    {
        var excluded = PurgeStore.Shared.Settings.ExcludedPaths;
        TxtNoExclusions.Visibility = excluded.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ExclusionsPanel.Visibility = excluded.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        RefreshFolderList(ExclusionsPanel, excluded, OnRemoveExclusionClick);
    }

    private void RefreshFolderList(StackPanel panel, List<string> paths, RoutedEventHandler onRemove)
    {
        if (panel is null) return;
        panel.Children.Clear();
        foreach (var path in paths.ToList())
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tb = new TextBlock
            {
                Text = path, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                Foreground = ThemeBrushes.Get(this, "TextSecondary"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var btn = new Button
            {
                Tag = path,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 2, 6, 2),
                Content = new FontIcon { Glyph = "\uE711", FontSize = 11 },
            };
            btn.Click += onRemove;
            Grid.SetColumn(btn, 1);
            grid.Children.Add(tb);
            grid.Children.Add(btn);

            panel.Children.Add(new Border
            {
                Background = ThemeBrushes.Get(this, "FillSecondary"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6, 10, 6),
                Child = grid,
            });
        }
    }

    private void SyncTexts()
    {
        TxtPageTitle.Text = Loc.T("Réglages", "Settings");
        TxtPageSub.Text = Loc.T("Apparence, langue, automatisation et historique", "Appearance, language, automation, and history");
        BtnReplayOnboarding.Content = Loc.T("Visite d'accueil", "Welcome Tour");
        TxtSecAppearance.Text = Loc.T("Apparence & Langue", "Appearance & Language");
        TxtThemeAuto.Text = Loc.T("Système", "System");
        TxtThemeLight.Text = Loc.T("Clair", "Light");
        TxtThemeDark.Text = Loc.T("Sombre", "Dark");
        TxtLangTitle.Text = Loc.T("Langue de l'interface", "Interface Language");
        TxtLangSub.Text = Loc.T("Basculer instantanément entre le français et l'anglais", "Switch immediately between French and English");

        TxtSecAutomation.Text = Loc.T("Automatisation & Projets", "Automation & Projects");
        TxtStartupTitle.Text = Loc.T("Lancer au démarrage de Windows", "Launch at Windows Startup");
        TxtStartupSub.Text = Loc.T("Démarre Purge automatiquement à l'ouverture de session", "Starts Purge automatically when you sign in");
        TxtScheduleTitle.Text = Loc.T("Nettoyage automatique sécurisé", "Scheduled Safe Cleaning");
        TxtScheduleSub.Text = Loc.T("Nettoie uniquement les caches marqués « Sûr » vers la Corbeille", "Cleans only items marked 'Safe' into the Recycle Bin");
        SchedOffItem.Content = Loc.T("Désactivé", "Off");
        SchedWeeklyItem.Content = Loc.T("Chaque semaine", "Weekly");
        SchedMonthlyItem.Content = Loc.T("Chaque mois", "Monthly");
        SchedQuarterlyItem.Content = Loc.T("Chaque trimestre", "Quarterly");
        RefreshComboSelection(CmbSchedule);

        TxtStaleTitle.Text = Loc.T("Ancienneté des artefacts de projets", "Project Build Artifact Age");
        TxtStaleSub.Text = Loc.T("Filtrer node_modules, target et .next selon leur dernière modification", "Filter node_modules, target, and .next by last modified date");
        StaleAllItem.Content = Loc.T("Tous les projets", "All projects");
        RefreshComboSelection(CmbStale);
        TxtAutoUpdateTitle.Text = Loc.T("Vérifier les mises à jour au démarrage", "Check for updates on startup");
        TxtAutoUpdateSub.Text = Loc.T("Recherche discrètement si une nouvelle version est disponible sur GitHub", "Silently checks if a newer version is available on GitHub");
        ChkAutoEmptyTrash.Content = Loc.T("Vider la Corbeille après nettoyage", "Empty Recycle Bin after cleaning");
        TxtAutoEmptyHint.Text = Loc.T("Désactivé par défaut. Après un nettoyage manuel ou planifié, supprime définitivement toute la Corbeille des disques concernés, anciens éléments compris.",
            "Off by default. After manual or scheduled cleaning, permanently empties the entire Recycle Bin on the affected drives, including older items.");
        TxtCustomRootsTitle.Text = Loc.T("Dossiers de projets personnalisés", "Custom Project Folders");
        TxtCustomRootsSub.Text = Loc.T("Dossiers supplémentaires inspectés par Outils de dev", "Extra folders scanned by Dev Tools");
        BtnAddProjectFolder.Content = Loc.T("+ Ajouter", "+ Add Folder");

        TxtSecHistory.Text = Loc.T("Historique des nettoyages", "Cleaning History");
        TxtHistoryEmpty.Text = Loc.T("Aucun nettoyage effectué pour le moment.", "No cleanups recorded yet.");
        TxtSecExclusions.Text = Loc.T("Dossiers exclus & Maintenance", "Excluded Folders & Maintenance");
        TxtExclusionsSub.Text = Loc.T("Les dossiers que vous avez demandé à Purge d'ignorer lors des analyses.", "Folders you asked Purge to skip during scans.");
        TxtNoExclusions.Text = Loc.T("Aucun dossier exclu.", "No excluded folders.");
        TxtSelfUninstallDesc.Text = Loc.T("Retirer la tâche planifiée, le démarrage automatique et les réglages locaux.", "Remove scheduled task, startup entry, and local settings.");
        BtnSelfUninstall.Content = Loc.T("Désinstaller Purge...", "Uninstall Purge...");
    }

    private void RefreshComboSelection(ComboBox cmb)
    {
        if (cmb is null) return;
        bool prev = _loading;
        _loading = true;
        int idx = cmb.SelectedIndex;
        cmb.SelectedIndex = -1;
        cmb.SelectedIndex = idx;
        _loading = prev;
    }

    private void OnThemeCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string mode })
        {
            PurgeStore.Shared.SetAppearance(mode);
            UpdateThemeCardBorders();
        }
    }

    private void CmbLang_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || CmbLang is null) return;
        PurgeStore.Shared.SetLanguage(CmbLang.SelectedIndex == 1 ? "en" : "fr");
    }

    private void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        SystemIntegration.SetLaunchAtStartup(TglStartup.IsOn);
    }

    private void CmbSchedule_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || CmbSchedule is null) return;
        var freq = CmbSchedule.SelectedIndex switch { 1 => "weekly", 2 => "monthly", 3 => "quarterly", _ => "off" };
        PurgeStore.Shared.Settings.ScheduledFrequency = freq;
        PurgeStore.Shared.SaveSettings();
        SystemIntegration.ApplyScheduledCleaning(freq);
    }

    private void CmbStale_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || CmbStale is null) return;
        PurgeStore.Shared.Settings.StaleProjectDays = CmbStale.SelectedIndex switch { 1 => 30, 2 => 90, 3 => 180, _ => 0 };
        PurgeStore.Shared.SaveSettings();
        PurgeStore.Shared.InvalidateAnalysis();
    }

    private void OnAutoUpdateToggled(object sender, RoutedEventArgs e)
    {
        if (_loading || TglAutoUpdate is null) return;
        PurgeStore.Shared.Settings.AutoCheckUpdates = TglAutoUpdate.IsOn;
        PurgeStore.Shared.SaveSettings();
    }

    private void OnAutoEmptyChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading && ChkAutoEmptyTrash != null) PurgeStore.Shared.SetAutoEmptyRecycleBin(ChkAutoEmptyTrash.IsChecked == true);
    }

    private async void OnAddProjectFolderClick(object sender, RoutedEventArgs e)
    {
        if (App.CurrentWindow is null) return;
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null && !PurgeStore.Shared.Settings.CustomProjectRoots.Contains(folder.Path, StringComparer.OrdinalIgnoreCase))
        {
            PurgeStore.Shared.Settings.CustomProjectRoots.Add(folder.Path);
            PurgeStore.Shared.SaveSettings();
            PurgeStore.Shared.InvalidateAnalysis();
            RefreshFolderList(CustomRootsPanel, PurgeStore.Shared.Settings.CustomProjectRoots, OnRemoveCustomRootClick);
        }
    }

    private void OnRemoveCustomRootClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
        {
            PurgeStore.Shared.Settings.CustomProjectRoots.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            PurgeStore.Shared.SaveSettings();
            PurgeStore.Shared.InvalidateAnalysis();
            RefreshFolderList(CustomRootsPanel, PurgeStore.Shared.Settings.CustomProjectRoots, OnRemoveCustomRootClick);
        }
    }

    private void OnRemoveExclusionClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
        {
            PurgeStore.Shared.Settings.ExcludedPaths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            PurgeStore.Shared.SaveSettings();
            PurgeStore.Shared.InvalidateAnalysis();
            RefreshExclusions();
        }
    }

    private void OnReplayOnboardingClick(object sender, RoutedEventArgs e) =>
        ReplayOnboardingRequested?.Invoke();

    private async void OnSelfUninstallClick(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is null) return;
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            RequestedTheme = ThemeBrushes.IsDarkTheme() ? ElementTheme.Dark : ElementTheme.Light,
            Title = Loc.T("Désinstaller Purge ?", "Uninstall Purge?"),
            Content = Loc.T(
                "Cela supprimera le démarrage automatique, la tâche planifiée et enverra les données locales (%LOCALAPPDATA%\\Purge) dans la Corbeille.",
                "This removes the startup entry, scheduled task, and moves local app data (%LOCALAPPDATA%\\Purge) to the Recycle Bin."),
            PrimaryButtonText = Loc.T("Désinstaller et quitter", "Uninstall & Quit"),
            CloseButtonText = Loc.T("Annuler", "Cancel"),
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            SystemIntegration.UninstallPurge(removeAppData: true);
            App.CurrentWindow?.Close();
        }
    }
}
