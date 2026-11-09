using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using PurgeWin.ViewModels;
using Windows.Graphics.Imaging;

namespace PurgeWin.Utilities;

/// Captures real application screenshots using WinUI 3 RenderTargetBitmap (--screenshots).
public static class ScreenshotCapturer
{
    public static async Task RunAsync(Window window)
    {
        var dirs = new List<string>
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Assets", "readme")),
            @"C:\Users\steve\.gemini\antigravity\brain\194b66a5-9218-4c1f-8aee-d60de4357e8c"
        };
        foreach (var d in dirs) Directory.CreateDirectory(d);

        var s = PurgeStore.Shared;
        s.SetLanguage("fr");
        s.SetAppearance("dark");
        s.CancelScan();
        await Task.Delay(300);

        // Run uninstaller scan directly
        await s.ScanUninstallerAsync();
        int attempts = 0;
        while (s.UninstallPhase == ScanPhase.Scanning && attempts++ < 60)
            await Task.Delay(250);

        // 1. Uninstaller Grid Mode (Real extracted app icons + cards)
        s.SelectedTab = AppTab.Uninstaller;
        s.IsUninstallGridView = true;
        s.ApplyUninstallFilter();
        await Task.Delay(500);
        await CaptureMultiAsync(window, dirs, "screenshot-uninstaller-grid-dark.png");

        // 2. Uninstaller List Mode
        s.IsUninstallGridView = false;
        s.ApplyUninstallFilter();
        await Task.Delay(500);
        await CaptureMultiAsync(window, dirs, "screenshot-uninstaller-list-dark.png");

        // 3. French Overview
        s.OverviewPage = OverviewSubPage.Summary;
        s.SelectedTab = AppTab.Overview;
        await Task.Delay(400);
        await CaptureMultiAsync(window, dirs, "screenshot-overview-dark.png");

        // 4. Light Mode App Caches & Dev Tools (to verify light mode icons & no Ctrl+R badge)
        s.SetAppearance("light");
        s.SelectedTab = AppTab.AppCaches;
        await s.ScanAppCachesAsync();
        await Task.Delay(600);
        await CaptureMultiAsync(window, dirs, "screenshot-appcaches-light.png");

        s.SelectedTab = AppTab.DevTools;
        await s.ScanDevToolsAsync();
        await Task.Delay(600);
        await CaptureMultiAsync(window, dirs, "screenshot-devtools-light.png");

        window.Close();
    }

    private static async Task CaptureMultiAsync(Window window, List<string> dirs, string fileName)
    {
        await Task.Delay(650);
        if (window.Content is not UIElement root) return;

        var rtb = new RenderTargetBitmap();
        await rtb.RenderAsync(root);
        var pixels = await rtb.GetPixelsAsync();
        byte[] bytes = pixels.ToArray();

        foreach (var dir in dirs)
        {
            var filePath = Path.Combine(dir, fileName);
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            using var ras = fs.AsRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ras);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                (uint)rtb.PixelWidth,
                (uint)rtb.PixelHeight,
                96, 96,
                bytes);
            await encoder.FlushAsync();
        }
    }
}
