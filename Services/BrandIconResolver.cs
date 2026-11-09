namespace PurgeWin.Services;

/// Resolves brand icon file paths dynamically based on active Light/Dark theme.
/// In light mode, prefers dark monochrome variants located in Assets/BrandIcons/light/.
public static class BrandIconResolver
{
    public static string? Resolve(string? iconName)
    {
        if (string.IsNullOrWhiteSpace(iconName)) return null;

        try
        {
            bool isDark = Theme.ThemeBrushes.IsDarkTheme();
            if (!isDark)
            {
                var lightPath = Path.Combine(AppContext.BaseDirectory, "Assets", "BrandIcons", "light", $"{iconName}.png");
                if (File.Exists(lightPath)) return lightPath;
            }

            var darkPath = Path.Combine(AppContext.BaseDirectory, "Assets", "BrandIcons", $"{iconName}.png");
            return File.Exists(darkPath) ? darkPath : null;
        }
        catch
        {
            var fallback = Path.Combine(AppContext.BaseDirectory, "Assets", "BrandIcons", $"{iconName}.png");
            return File.Exists(fallback) ? fallback : null;
        }
    }
}
