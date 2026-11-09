using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PurgeWin.Services;

/// Resolves, extracts, and caches high-fidelity Windows application icons.
public static class AppIconService
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Purge", "AppIcons");

    private static readonly Dictionary<string, string> BrandAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = "vscode", ["visualstudiocode"] = "vscode",
        ["githubdesktop"] = "github", ["git"] = "git",
        ["edge"] = "edge", ["msedge"] = "edge",
        ["chrome"] = "googlechrome", ["googlechrome"] = "googlechrome",
        ["firefox"] = "firefox", ["brave"] = "brave", ["opera"] = "opera",
        ["spotify"] = "spotify", ["discord"] = "discord",
        ["telegram"] = "telegram", ["slack"] = "slack",
        ["notion"] = "notion", ["figma"] = "figma",
        ["docker"] = "docker", ["python"] = "python",
        ["androidstudio"] = "androidstudio", ["zoom"] = "zoom",
        ["vlc"] = "vlc", ["obsidian"] = "obsidian",
        ["postman"] = "postman", ["insomnia"] = "insomnia"
    };

    static AppIconService()
    {
        try { Directory.CreateDirectory(CacheDir); } catch { }
    }

    public static string? GetAppIconPath(string appKey, string appName, string? displayIcon, string? installLoc)
    {
        string hash = ComputeHash(appKey + "|" + appName);
        string cachedPng = Path.Combine(CacheDir, $"{hash}.png");
        if (File.Exists(cachedPng)) return cachedPng;

        // 1. Extract from DisplayIcon registry entry
        if (!string.IsNullOrWhiteSpace(displayIcon))
        {
            var iconPath = CleanIconPath(displayIcon);
            if (TryExtractAndSave(iconPath, cachedPng)) return cachedPng;
        }

        // 2. Extract from InstallLocation executable
        if (!string.IsNullOrWhiteSpace(installLoc) && Directory.Exists(installLoc))
        {
            var exe = FindMainExe(installLoc, appName);
            if (exe != null && TryExtractAndSave(exe, cachedPng)) return cachedPng;
        }

        // 3. Extract from Start Menu shortcut (.lnk)
        var lnk = FindStartMenuShortcut(appName);
        if (lnk != null && TryExtractAndSave(lnk, cachedPng)) return cachedPng;

        // 4. Fallback: Check BrandIcons catalogue
        string? brand = FindBrandIcon(appName);
        if (brand != null && File.Exists(brand)) return brand;

        return null;
    }

    private static string? FindBrandIcon(string appName)
    {
        string clean = Regex.Replace(appName.ToLowerInvariant(), @"[^a-z0-9]", "");
        foreach (var (alias, target) in BrandAliases)
        {
            if (clean.Contains(alias, StringComparison.OrdinalIgnoreCase))
            {
                var file = BrandIconResolver.Resolve(target);
                if (file != null) return file;
            }
        }
        return null;
    }

    private static string CleanIconPath(string raw)
    {
        var cleaned = raw.Trim('"', ' ');
        int comma = cleaned.LastIndexOf(',');
        if (comma > 2) cleaned = cleaned[..comma].Trim('"', ' ');
        return Environment.ExpandEnvironmentVariables(cleaned);
    }

    private static bool TryExtractAndSave(string sourcePath, string targetPng)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return false;
        try
        {
            var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (ext is ".png")
            {
                File.Copy(sourcePath, targetPng, true);
                return true;
            }
            if (ext is ".ico")
            {
                using var ico = new Icon(sourcePath, 64, 64);
                using var bmp = ico.ToBitmap();
                bmp.Save(targetPng, ImageFormat.Png);
                return true;
            }

            using var extracted = Icon.ExtractAssociatedIcon(sourcePath);
            if (extracted != null)
            {
                using var bmp = extracted.ToBitmap();
                bmp.Save(targetPng, ImageFormat.Png);
                return true;
            }
        }
        catch { }
        return false;
    }

    private static string? FindMainExe(string dir, string appName)
    {
        try
        {
            string cleanName = Regex.Replace(appName, @"[^a-zA-Z0-9]", "");
            var exes = Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly);
            var best = exes.FirstOrDefault(e => Path.GetFileNameWithoutExtension(e)
                .Equals(cleanName, StringComparison.OrdinalIgnoreCase))
                ?? exes.FirstOrDefault(e => appName.Contains(Path.GetFileNameWithoutExtension(e), StringComparison.OrdinalIgnoreCase))
                ?? exes.OrderByDescending(e => new FileInfo(e).Length).FirstOrDefault();
            return best;
        }
        catch { return null; }
    }

    private static string? FindStartMenuShortcut(string appName)
    {
        string[] searchDirs =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs)
        ];
        string cleanName = Regex.Replace(appName, @"[^a-zA-Z0-9]", "");
        foreach (var d in searchDirs)
        {
            if (!Directory.Exists(d)) continue;
            try
            {
                var lnks = Directory.GetFiles(d, "*.lnk", SearchOption.AllDirectories);
                var match = lnks.FirstOrDefault(l =>
                    Path.GetFileNameWithoutExtension(l).Contains(cleanName, StringComparison.OrdinalIgnoreCase)
                    || appName.Contains(Path.GetFileNameWithoutExtension(l), StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }
            catch { }
        }
        return null;
    }

    private static string ComputeHash(string text)
    {
        using var md5 = MD5.Create();
        var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
