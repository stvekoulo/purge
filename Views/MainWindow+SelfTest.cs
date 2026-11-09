using Microsoft.UI.Xaml.Input;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

public sealed partial class MainWindow
{
    private void OnScanCurrentShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var s = PurgeStore.Shared;
        _ = s.SelectedTab switch
        {
            AppTab.Overview when s.OverviewPage == OverviewSubPage.EverythingElse => _everythingElseView?.RefreshCurrentFolderAsync() ?? s.ScanEverythingElseAsync(),
            AppTab.AppCaches => s.ScanAppCachesAsync(),
            AppTab.DevTools => s.ScanDevToolsAsync(),
            AppTab.LargeFiles => s.ScanLargeFilesAsync(),
            AppTab.Uninstaller => s.ScanUninstallerAsync(),
            _ => s.ScanEverythingAsync(),
        };
        args.Handled = true;
    }

    private void OnScanAllShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _ = PurgeStore.Shared.ScanEverythingAsync();
        args.Handled = true;
    }

    private void OnTab1Shortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        PurgeStore.Shared.SelectedTab = AppTab.Overview;
        args.Handled = true;
    }

    private void OnTab2Shortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        PurgeStore.Shared.SelectedTab = AppTab.AppCaches;
        args.Handled = true;
    }

    private void OnTab3Shortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        PurgeStore.Shared.SelectedTab = AppTab.DevTools;
        args.Handled = true;
    }

    private void OnTab4Shortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        PurgeStore.Shared.SelectedTab = AppTab.LargeFiles;
        args.Handled = true;
    }

    private void OnTab5Shortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        PurgeStore.Shared.SelectedTab = AppTab.Uninstaller;
        args.Handled = true;
    }

    private async Task RunSelfTestAsync()
    {
        var s = PurgeStore.Shared;
        await Task.Delay(300);
        await s.ScanAppCachesAsync();
        await s.ScanDevToolsAsync();
        await s.ScanUninstallerAsync();
        await s.ScanEverythingElseAsync();

        foreach (var tab in Enum.GetValues<AppTab>())
        {
            s.SelectedTab = tab;
            await Task.Delay(120);
        }

        s.SelectedTab = AppTab.Overview;
        s.OverviewPage = OverviewSubPage.EverythingElse;
        await Task.Delay(150);
        s.OverviewPage = OverviewSubPage.Summary;
        s.SetAppearance("light");
        await Task.Delay(200);
        s.SetAppearance("dark");
        await Task.Delay(200);
        s.SetLanguage("en");
        await Task.Delay(150);
        s.SetLanguage("fr");
        await Task.Delay(150);
        Close();
    }
}
