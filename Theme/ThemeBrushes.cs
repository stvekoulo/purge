using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace PurgeWin.Theme;

/// Purge for Windows:. Provides fail-safe brush
/// lookup in C# and manages Light/Dark/System theme switching + TitleBar styling.
public static class ThemeBrushes
{
    public static event Action? ThemeChanged;

    private static readonly Dictionary<string, (uint Light, uint Dark)> Palette = new()
    {
        ["TextPrimary"] = (0x2E2D2B, 0xECEAE6),
        ["TextSecondary"] = (0x64625E, 0xA6A39D),
        ["TextTertiary"] = (0x85827C, 0x807D78),
        ["SurfaceBase"] = (0xF6F5F3, 0x171615),
        ["SurfacePrimary"] = (0xF6F5F3, 0x171615),
        ["SurfaceCard"] = (0xFFFFFF, 0x1E1D1C),
        ["SurfaceElevated"] = (0xFFFFFF, 0x1E1D1C),
        ["SurfaceRaised"] = (0xFFFFFF, 0x2B2A28),
        ["SurfaceCardHover"] = (0xF4F2EF, 0x2A2927),
        ["FillSecondary"] = (0xEFEDEA, 0x282725),
        ["FillSecondaryHover"] = (0xE8E6E2, 0x2F2E2C),
        ["FillSecondaryPressed"] = (0xDFDCD8, 0x373533),
        ["BorderSubtle"] = (0xE6E3DF, 0x302F2D),
        ["Hairline"] = (0xE6E3DF, 0x302F2D),
        ["BorderStrong"] = (0xD5D2CD, 0x3D3B38),
        ["ActionPrimary"] = (0x353431, 0xECEAE6),
        ["ActionPrimaryHover"] = (0x42403D, 0xF6F4F1),
        ["ActionPrimaryPressed"] = (0x2A2927, 0xD8D5D0),
        ["OnActionPrimary"] = (0xFFFFFF, 0x171615),
        ["ActionDestructive"] = (0xD1312A, 0xD9372D),
        ["ActionDestructiveHover"] = (0xBC2B25, 0xE2453B),
        ["ActionDestructivePressed"] = (0xA92620, 0xC4302A),
        ["StatusSafeText"] = (0x1F7A35, 0x5FD36B),
        ["StatusSafeFill"] = (0xE6F4EA, 0x1B2E22),
        ["StatusCheckText"] = (0x8A5300, 0xF2B84B),
        ["StatusCheckFill"] = (0xFAEEDA, 0x332910),
        ["StatusDangerText"] = (0xB4302A, 0xF47468),
        ["StatusDangerFill"] = (0xFBE8E5, 0x321B19),
        ["StatusUnsureText"] = (0x5A5853, 0xA9A6A0),
        ["StatusUnsureFill"] = (0xEFEDEA, 0x282725),
        ["AccentCelebrate"] = (0xD18F00, 0xFFC70D),
        ["ChartAppCaches"] = (0x56B4E9, 0x6CC3F0),
        ["ChartDevTools"] = (0x0072B2, 0x3A94D0),
        ["ChartLargeFiles"] = (0xE69F00, 0xF0B020),
        ["ChartApps"] = (0xD55E00, 0xE8772A),
        ["ChartInstalledApps"] = (0xD55E00, 0xE8772A),
        ["ChartLeftovers"] = (0xCC79A7, 0xDA93BA),
        ["ChartAppLeftovers"] = (0xCC79A7, 0xDA93BA),
        ["ChartAppCachesTile"] = (0x3399D3, 0x3A9BD5),
        ["ChartDevToolsTile"] = (0x0072B2, 0x1E78BA),
        ["ChartLeftoversTile"] = (0xB35A8D, 0xBE6C9C),
        ["ChartEverythingElse"] = (0x8C8983, 0xA8A49D),
        ["ChartFreeSpace"] = (0xDAD7D2, 0x4E4B47),
        ["ManagedInApps"] = (0x3E8FD3, 0x5AA9E6),
        ["YourFiles"] = (0xD39B2F, 0xE3B04B),
        ["LeftAlone"] = (0xCC6655, 0xD9786A),
        ["ManagedByWindows"] = (0x8A8781, 0x6E6B66),
        ["BucketLabel"] = (0x171615, 0x171615),
        ["ManagedByWindowsLabel"] = (0x171615, 0xF4F2EE),
    };

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private static volatile bool _cachedIsDark = true;

    public static bool IsDarkTheme(FrameworkElement? element = null)
    {
        try
        {
            if (App.CurrentWindow?.DispatcherQueue?.HasThreadAccess == true)
            {
                if (element is not null && element.ActualTheme != ElementTheme.Default)
                    return _cachedIsDark = (element.ActualTheme == ElementTheme.Dark);
                if (App.CurrentWindow?.Content is FrameworkElement root && root.ActualTheme != ElementTheme.Default)
                    return _cachedIsDark = (root.ActualTheme == ElementTheme.Dark);
                if (Application.Current is not null)
                    return _cachedIsDark = (Application.Current.RequestedTheme == ApplicationTheme.Dark);
            }
        }
        catch { }
        return _cachedIsDark;
    }

    public static void ApplyAppearance(string? appearance, Window? window = null)
    {
        var win = window ?? App.CurrentWindow;
        if (win?.Content is FrameworkElement root)
        {
            root.RequestedTheme = appearance switch
            {
                "light" => ElementTheme.Light,
                "dark" => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
        }
        UpdateWindowTitleBar(win);
        ThemeChanged?.Invoke();
    }

    public static void UpdateWindowTitleBar(Window? window = null)
    {
        var win = window ?? App.CurrentWindow;
        if (win is null) return;
        try
        {
            bool dark = IsDarkTheme(win.Content as FrameworkElement);
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(win);
            int useDark = dark ? 1 : 0;
            _ = DwmSetWindowAttribute(hWnd, 20, ref useDark, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE

            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            if (appWindow?.TitleBar is { } tb && AppWindowTitleBar.IsCustomizationSupported())
            {
                var bg = Colors.Transparent;
                var fg = FromHex(dark ? 0xECEAE6u : 0x2E2D2Bu);
                var hoverBg = FromHex(dark ? 0x2F2E2Cu : 0xE8E6E2u);
                var pressedBg = FromHex(dark ? 0x373533u : 0xDFDCD8u);
                var mutedFg = FromHex(dark ? 0x807D78u : 0x85827Cu);

                tb.BackgroundColor = bg;
                tb.ForegroundColor = fg;
                tb.InactiveBackgroundColor = bg;
                tb.InactiveForegroundColor = mutedFg;
                tb.ButtonBackgroundColor = bg;
                tb.ButtonForegroundColor = fg;
                tb.ButtonInactiveBackgroundColor = bg;
                tb.ButtonInactiveForegroundColor = mutedFg;
                tb.ButtonHoverBackgroundColor = hoverBg;
                tb.ButtonHoverForegroundColor = fg;
                tb.ButtonPressedBackgroundColor = pressedBg;
                tb.ButtonPressedForegroundColor = fg;
            }
        }
        catch { }
    }

    public static SolidColorBrush Get(FrameworkElement? element, string key) =>
        Get(key, IsDarkTheme(element));

    public static SolidColorBrush Get(string key, bool? isDark = null)
    {
        var dark = isDark ?? IsDarkTheme();
        if (Palette.TryGetValue(key, out var pair))
        {
            var hex = dark ? pair.Dark : pair.Light;
            return new SolidColorBrush(FromHex(hex));
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public static Color FromHex(uint hex, byte alpha = 255) =>
        Color.FromArgb(
            alpha,
            (byte)((hex >> 16) & 0xFF),
            (byte)((hex >> 8) & 0xFF),
            (byte)(hex & 0xFF));
}
