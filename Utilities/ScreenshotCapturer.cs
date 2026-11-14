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

        // Scan OneDrive & capture Dark + Light views
        await s.ScanOneDriveAsync();
        int attempts = 0;
        while (s.OneDrivePhase == ScanPhase.Scanning && attempts++ < 80)
            await Task.Delay(250);

        // 1. OneDrive Dark Mode (Tout OneDrive)
        s.SelectedTab = AppTab.OneDrive;
        s.OneDriveStateFilter = Models.OneDriveFilter.All;
        s.ApplyOneDriveFilter();
        await Task.Delay(600);
        await CaptureMultiAsync(window, dirs, "screenshot-onedrive-dark.png");

        // 2. OneDrive Dark Mode ("Sur ce PC" filter selected)
        s.OneDriveStateFilter = Models.OneDriveFilter.Local;
        s.ApplyOneDriveFilter();
        await Task.Delay(500);
        await CaptureMultiAsync(window, dirs, "screenshot-onedrive-local-dark.png");

        // 3. OneDrive Light Mode (Tout OneDrive)
        s.OneDriveStateFilter = Models.OneDriveFilter.All;
        s.ApplyOneDriveFilter();
        s.SetAppearance("light");
        await Task.Delay(600);
        await CaptureMultiAsync(window, dirs, "screenshot-onedrive-light.png");

        // 4. Uninstaller Dark Mode with multiple apps selected for batch uninstall & clean
        s.SetAppearance("dark");
        Theme.ThemeBrushes.ApplyAppearance("dark", window);
        await s.ScanUninstallerAsync();
        attempts = 0;
        while (s.UninstallPhase == ScanPhase.Scanning && attempts++ < 80)
            await Task.Delay(250);
        s.SelectedTab = AppTab.Uninstaller;
        s.UninstallSubTab = Models.UninstallerSubTab.InstalledApps;
        s.IsUninstallGridView = true;
        s.ApplyUninstallFilter();
        foreach (var app in s.FilteredInstalledApps.Take(3))
            app.IsSelected = true;
        s.NotifyUninstallSelection();
        await Task.Delay(600);
        await CaptureMultiAsync(window, dirs, "screenshot-uninstaller-batch-dark.png");

        foreach (var app in s.InstalledApps) app.IsSelected = false;
        s.NotifyUninstallSelection();
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
