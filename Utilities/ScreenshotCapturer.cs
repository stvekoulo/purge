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
        var outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "readme"));
        Directory.CreateDirectory(outDir);

        var s = PurgeStore.Shared;
        s.SetLanguage("en");
        s.SetAppearance("dark");
        await Task.Delay(400);
        await s.ScanEverythingAsync();
        await s.ScanEverythingElseAsync();
        await Task.Delay(500);

        // 1. English Dark Mode — Overview
        s.OverviewPage = OverviewSubPage.Summary;
        s.SelectedTab = AppTab.Overview;
        await CaptureStepAsync(window, Path.Combine(outDir, "screenshot-overview-dark.png"));

        // 2. English Light Mode — Overview
        s.SetAppearance("light");
        await CaptureStepAsync(window, Path.Combine(outDir, "screenshot-overview-light.png"));

        // 3. English Dark Mode — Dev Tools
        s.SetAppearance("dark");
        s.SelectedTab = AppTab.DevTools;
        await CaptureStepAsync(window, Path.Combine(outDir, "screenshot-devtools-dark.png"));

        // 4. English Dark Mode — Everything Else (Treemap)
        s.SelectedTab = AppTab.Overview;
        s.OverviewPage = OverviewSubPage.EverythingElse;
        await CaptureStepAsync(window, Path.Combine(outDir, "screenshot-treemap-dark.png"));

        // 5. English Dark Mode — Settings
        s.OverviewPage = OverviewSubPage.Summary;
        s.SelectedTab = AppTab.Settings;
        await CaptureStepAsync(window, Path.Combine(outDir, "screenshot-settings-dark.png"));

        // 6. French Dark Mode — Overview (for README.fr.md)
        s.SetLanguage("fr");
        await s.ScanEverythingAsync();
        await s.ScanEverythingElseAsync();
        s.SelectedTab = AppTab.Overview;
        await CaptureStepAsync(window, Path.Combine(outDir, "screenshot-overview-fr.png"));

        window.Close();
    }

    private static async Task CaptureStepAsync(Window window, string filePath)
    {
        await Task.Delay(650);
        if (window.Content is not UIElement root) return;

        var rtb = new RenderTargetBitmap();
        await rtb.RenderAsync(root);
        var pixels = await rtb.GetPixelsAsync();
        byte[] bytes = pixels.ToArray();

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
