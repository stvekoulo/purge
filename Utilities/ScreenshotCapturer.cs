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
        s.CancelScan();
        await Task.Delay(400);
        s.SetLanguage("fr");
        s.SetAppearance("dark");
        Theme.ThemeBrushes.ApplyAppearance("dark", window);
        await Task.Delay(300);

        // Scan modules for realistic Overview & Optimization screenshots
        await s.ScanAppCachesAsync();
        await s.ScanDevToolsAsync();
        await s.ScanOneDriveAsync();
        await s.ScanOptimizationAsync();
        await Task.Delay(400);

        // 1. Enriched Overview (Dark)
        s.OverviewPage = OverviewSubPage.Summary;
        s.SelectedTab = AppTab.Overview;
        await Task.Delay(500);
        await CaptureMultiAsync(window, dirs, "screenshot-overview-v13-dark.png");

        // 2. Optimization — Startup Apps (Dark)
        s.SelectedTab = AppTab.Optimization;
        s.OptimizationSubTab = Models.OptimizationSubTab.StartupApps;
        s.ApplyOptimizationFilter();
        await Task.Delay(500);
        await CaptureMultiAsync(window, dirs, "screenshot-optimization-startup-dark.png");

        // 3. Optimization — System Maintenance (Dark)
        s.OptimizationSubTab = Models.OptimizationSubTab.Maintenance;
        await Task.Delay(500);
        await CaptureMultiAsync(window, dirs, "screenshot-optimization-maintenance-dark.png");

        window.Close();
    }

    private static async Task CaptureMultiAsync(Window window, List<string> dirs, string fileName)
    {
        await Task.Delay(600);
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
