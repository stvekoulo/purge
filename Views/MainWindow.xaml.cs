using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PurgeWin.Services;
using PurgeWin.Theme;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

public sealed partial class MainWindow : Window
{
    private bool _ready;
    private OverviewView? _overviewView;
    private EverythingElseView? _everythingElseView;
    private AppCachesView? _appCachesView;
    private DevToolsView? _devToolsView;
    private LargeFilesView? _largeFilesView;
    private UninstallerView? _uninstallerView;
    private SettingsView? _settingsView;
    private AboutView? _aboutView;
    private AppWindow? _appWindow;
    private TrayIconService? _trayIcon;

    public MainWindow()
    {
        var store = PurgeStore.Shared;
        store.Attach(DispatcherQueue);
        InitializeComponent();
        ConfigureWindow();

        store.RefreshDisk();
        store.PropertyChanged += OnStoreChanged;
        Loc.LanguageChanged += SyncSidebarTexts;
        ThemeBrushes.ThemeChanged += OnThemeUpdated;
        RootGrid.ActualThemeChanged += (_, _) => ThemeBrushes.UpdateWindowTitleBar(this);
        SettingsView.ReplayOnboardingRequested += () => OnboardingControl.ShowWelcome();

        ThemeBrushes.ApplyAppearance(store.Settings.Appearance, this);
        InitLogoAnimation();
        SyncSidebarTexts();
        UpdateActiveTabUI();
        UpdateTrashSummary();
        UpdateBadges();
        InitializeUpdateDetection();
        _ready = true;

#if PURGE_UX_TEST
        if (Environment.GetCommandLineArgs().Contains("--scope-probe"))
        {
            _ = ScopeIntegrationProbe.RunAsync(this);
            return;
        }
#endif

#if PURGE_UPDATE_PREVIEW
        _ = UpdateService.Shared.CheckAsync();
#else

        bool isSelfTest = Environment.GetCommandLineArgs().Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase));
        bool isScreenshots = Environment.GetCommandLineArgs().Any(a => string.Equals(a, "--screenshots", StringComparison.OrdinalIgnoreCase));
        if (!store.Settings.HasCompletedOnboarding && !isSelfTest && !isScreenshots)
            OnboardingControl.ShowWelcome();

        if (isSelfTest)
            _ = RunSelfTestAsync();
        else if (isScreenshots)
            _ = ScreenshotCapturer.RunAsync(this);
        else
            _ = store.ScanEverythingAsync();
#endif
    }

    private void ConfigureWindow()
    {
        Title = "Purge";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);

        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        if (appWindow is not null)
        {
            _appWindow = appWindow;
            _trayIcon = new TrayIconService(hWnd, appWindow);
            appWindow.Closing += OnAppWindowClosing;
            Closed += (_, _) => _trayIcon?.Dispose();

            var icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(icoPath)) appWindow.SetIcon(icoPath);
            if (appWindow.TitleBar is { } tb && AppWindowTitleBar.IsCustomizationSupported())
            {
                tb.ExtendsContentIntoTitleBar = true;
                tb.PreferredHeightOption = TitleBarHeightOption.Standard;
                int h = tb.Height > 0 ? tb.Height : 38;
                TitleBarRow.Height = new GridLength(Math.Max(38, h));
                TitleBarDragRegion.Height = Math.Max(38, h);
            }
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = 860;
                presenter.PreferredMinimumHeight = 560;
            }
            appWindow.Resize(new Windows.Graphics.SizeInt32(1040, 720));
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            if (displayArea is not null)
            {
                var pos = appWindow.Position;
                pos.X = (displayArea.WorkArea.Width - 1040) / 2;
                pos.Y = (displayArea.WorkArea.Height - 720) / 2;
                appWindow.Move(pos);
            }
        }
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_trayIcon is { IsExiting: true }) return;
        var cmdArgs = Environment.GetCommandLineArgs();
        if (cmdArgs.Any(a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase)
            || a.Equals("--screenshots", StringComparison.OrdinalIgnoreCase)))
            return;

        args.Cancel = true;
        _appWindow?.Hide();
    }

    private void OnThemeUpdated()
    {
        bool dark = ThemeBrushes.IsDarkTheme(RootGrid);
        IconThemeToggle.Glyph = dark ? "\uE706" : "\uE708"; // Sun in dark mode, Moon in light mode
        UpdateActiveTabUI();
    }

    private void SyncSidebarTexts()
    {
        TxtLangBadge.Text = Loc.IsEn ? "EN" : "FR";
        TxtNavOverview.Text = Loc.T("Vue d'ensemble", "Overview");
        TxtSecClean.Text = Loc.T("Nettoyer", "Clean");
        TxtNavCaches.Text = Loc.T("Caches d'apps", "App Caches");
        TxtNavDev.Text = Loc.T("Outils de dev", "Dev Tools");
        TxtSecReview.Text = Loc.T("Vérifier", "Review");
        TxtNavLarge.Text = Loc.T("Fichiers volumineux", "Large Files");
        TxtNavUninstall.Text = Loc.T("Désinstalleur", "Uninstaller");
        TxtNavSettings.Text = Loc.T("Réglages", "Settings");
        TxtNavAbout.Text = Loc.T("À propos", "About");
        UpdateTrashSummary();
        UpdateBadges();
        UpdateLogoCleaningState();
        OnThemeUpdated();
    }

    private void OnStoreChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (DispatcherQueue is { } q && !q.HasThreadAccess)
        {
            q.TryEnqueue(() => OnStoreChanged(sender, e));
            return;
        }

        if (e.PropertyName is nameof(PurgeStore.SelectedTab) or nameof(PurgeStore.OverviewPage))
            UpdateActiveTabUI();
        else if (e.PropertyName is nameof(PurgeStore.TrashBytes) or nameof(PurgeStore.TrashItemCount))
            UpdateTrashSummary();
        else if (e.PropertyName is nameof(PurgeStore.CacheBytes) or nameof(PurgeStore.DevBytes)
                 or nameof(PurgeStore.LargeFileBytes) or nameof(PurgeStore.LeftoverBytes) or nameof(PurgeStore.InstalledAppBytes))
            UpdateBadges();
        else if (e.PropertyName is nameof(PurgeStore.IsCleaning) or nameof(PurgeStore.CleaningLabel))
        {
            UpdateLogoCleaningState();
            RefreshUpdateUI();
        }
    }

    private void UpdateTrashSummary()
    {
        var bytes = PurgeStore.Shared.TrashBytes;
        TxtTrashSummary.Text = bytes > 0
            ? Loc.T($"Corbeille · {ByteFormatter.Format(bytes)}", $"Recycle Bin · {ByteFormatter.Format(bytes)}")
            : PurgeStore.Shared.TrashItemCount > 0 ? Loc.T($"Corbeille · {PurgeStore.Shared.TrashItemCount} éléments", $"Recycle Bin · {PurgeStore.Shared.TrashItemCount} items")
            : Loc.T("Corbeille · vide", "Recycle Bin · empty");
    }

    private void UpdateBadges()
    {
        var s = PurgeStore.Shared;
        TxtAppCachesBadge.Text = s.CacheBytes > 0 ? ByteFormatter.Format(s.CacheBytes) : "";
        TxtDevToolsBadge.Text = s.DevBytes > 0 ? ByteFormatter.Format(s.DevBytes) : "";
        TxtLargeFilesBadge.Text = s.LargeFileBytes > 0 ? ByteFormatter.Format(s.LargeFileBytes) : "";
        long uninstTotal = s.InstalledAppBytes + s.LeftoverBytes;
        TxtUninstallBadge.Text = uninstTotal > 0 ? ByteFormatter.Format(uninstTotal) : "";
    }

    private void UpdateActiveTabUI()
    {
        var store = PurgeStore.Shared;
        var tab = store.SelectedTab;
        UIElement target = tab switch
        {
            AppTab.Overview when store.OverviewPage == OverviewSubPage.EverythingElse => _everythingElseView ??= new(),
            AppTab.AppCaches => _appCachesView ??= new(),
            AppTab.DevTools => _devToolsView ??= new(),
            AppTab.LargeFiles => _largeFilesView ??= new(),
            AppTab.Uninstaller => _uninstallerView ??= new(),
            AppTab.Settings => _settingsView ??= new(),
            AppTab.About => _aboutView ??= new(),
            _ => _overviewView ??= new()
        };
        if (DetailHost.Children.Count != 1 || !ReferenceEquals(DetailHost.Children[0], target))
        {
            DetailHost.Children.Clear();
            DetailHost.Children.Add(target);
        }

        SetNavButtonActive(NavOverview, tab == AppTab.Overview);
        SetNavButtonActive(NavAppCaches, tab == AppTab.AppCaches);
        SetNavButtonActive(NavDevTools, tab == AppTab.DevTools);
        SetNavButtonActive(NavLargeFiles, tab == AppTab.LargeFiles);
        SetNavButtonActive(NavUninstaller, tab == AppTab.Uninstaller);
        SetNavButtonActive(NavSettings, tab == AppTab.Settings);
        SetNavButtonActive(NavAbout, tab == AppTab.About);
        if (_ready && tab is not (AppTab.Settings or AppTab.About)) _ = store.ScanEverythingAsync(force: false);
    }

    private void SetNavButtonActive(Button btn, bool isActive)
    {
        var activeBrush = ThemeBrushes.Get(RootGrid, "FillSecondary");
        var transparent = new SolidColorBrush(Colors.Transparent);
        btn.Background = isActive ? activeBrush : transparent;
    }

    private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        bool currentlyDark = ThemeBrushes.IsDarkTheme(RootGrid);
        PurgeStore.Shared.SetAppearance(currentlyDark ? "light" : "dark");
    }

    private void BtnLangToggle_Click(object sender, RoutedEventArgs e) =>
        PurgeStore.Shared.SetLanguage(Loc.IsEn ? "fr" : "en");

    private void BtnOpenRecycleBin_Click(object sender, RoutedEventArgs e)
    {
        // The native flyout exposes open, restore and empty actions.
    }

    private void NavOverview_Click(object sender, RoutedEventArgs e) { PurgeStore.Shared.OverviewPage = OverviewSubPage.Summary; PurgeStore.Shared.SelectedTab = AppTab.Overview; }
    private void NavAppCaches_Click(object sender, RoutedEventArgs e) => PurgeStore.Shared.SelectedTab = AppTab.AppCaches;
    private void NavDevTools_Click(object sender, RoutedEventArgs e) => PurgeStore.Shared.SelectedTab = AppTab.DevTools;
    private void NavLargeFiles_Click(object sender, RoutedEventArgs e) => PurgeStore.Shared.SelectedTab = AppTab.LargeFiles;
    private void NavUninstaller_Click(object sender, RoutedEventArgs e) => PurgeStore.Shared.SelectedTab = AppTab.Uninstaller;
    private void NavSettings_Click(object sender, RoutedEventArgs e) => PurgeStore.Shared.SelectedTab = AppTab.Settings;
    private void NavAbout_Click(object sender, RoutedEventArgs e) => PurgeStore.Shared.SelectedTab = AppTab.About;
}
