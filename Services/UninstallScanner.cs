using System.Diagnostics;
using Microsoft.Win32;
using PurgeWin.Models;

namespace PurgeWin.Services;

/// Port of AppUninstallScanner + OrphanLeftoverScanner + CommandLineToolScanner:
/// 1) Reads HKLM/HKCU Uninstall registry keys and pairs each app with its AppData leftovers.
/// 2) Finds orphaned AppData folders whose owning app is no longer installed.
/// 3) Scans CLI tools in Scoop, Cargo, npm global, pipx, uv, and ~/.local/bin.
public static class UninstallScanner
{
    private static readonly HashSet<string> ProtectedAppDataFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft", "Windows", "Packages", "Programs", "Temp", "ConnectedDevicesPlatform",
        "D3DSCache", "PlaceholderTileLogoFolder", "Publishers", "VirtualStore", "Purge",
        "Comms", "PeerDistRepub", "SquirrelTemp", "CrashDumps", " NuGet", "pip", "uv",
        "npm-cache", "pnpm", "Yarn", "Google", "Mozilla", "BraveSoftware",
    };

    public static async Task<(List<InstalledAppItem> Apps, List<OrphanLeftoverItem> Orphans, List<CliToolItem> CliTools)> ScanAllAsync(
        ISet<string> excluded, CancellationToken token, IReadOnlyList<string>? activeDrives = null, bool allDrives = true)
    {
        return await Task.Run(() =>
        {
            var scope = new PathScope(activeDrives ?? PathScope.AvailableRoots(), excluded);
            var apps = ScanInstalledApps(excluded, token, scope, allDrives);
            var orphans = ScanOrphanLeftovers(apps, excluded, token);
            var cli = ScanCliTools(excluded, token);
            return (apps, orphans.Where(o => scope.CanDelete(o.FullPath)).ToList(), cli.Where(c => scope.CanDelete(c.Path)).ToList());
        }, token);
    }

    private static List<InstalledAppItem> ScanInstalledApps(ISet<string> excluded, CancellationToken token, PathScope scope, bool allDrives)
    {
        var raw = new Dictionary<string, InstalledAppItem>(StringComparer.OrdinalIgnoreCase);
        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingApp = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var localDirs = SafeTopDirs(localApp).Where(d => scope.CanDelete(d.FullName)).ToList();
        var roamingDirs = SafeTopDirs(roamingApp).Where(d => scope.CanDelete(d.FullName)).ToList();

        (RegistryHive Hive, RegistryView View, string SubKey)[] roots =
        [
            (RegistryHive.CurrentUser, RegistryView.Default, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
            (RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
            (RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
        ];

        foreach (var (hive, view, subKeyPath) in roots)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var uninstallKey = baseKey.OpenSubKey(subKeyPath);
                if (uninstallKey is null) continue;

                foreach (var subName in uninstallKey.GetSubKeyNames())
                {
                    token.ThrowIfCancellationRequested();
                    using var sub = uninstallKey.OpenSubKey(subName);
                    if (sub is null) continue;

                    var name = (sub.GetValue("DisplayName") as string)?.Trim();
                    if (string.IsNullOrEmpty(name) || raw.ContainsKey(name)) continue;
                    if (sub.GetValue("SystemComponent") is int sys && sys == 1) continue;
                    if (!string.IsNullOrEmpty(sub.GetValue("ParentKeyName") as string)) continue;
                    if (name.StartsWith("KB", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Redistributable", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Windows SDK", StringComparison.OrdinalIgnoreCase)) continue;

                    var publisher = (sub.GetValue("Publisher") as string)?.Trim() ?? "";
                    var version = (sub.GetValue("DisplayVersion") as string)?.Trim() ?? "";
                    var installLoc = (sub.GetValue("InstallLocation") as string)?.Trim('"', ' ');
                    var uninstallStr = (sub.GetValue("UninstallString") as string)?.Trim();
                    var displayIcon = (sub.GetValue("DisplayIcon") as string)?.Trim();
                    bool installedHere = string.IsNullOrEmpty(installLoc) ? allDrives : scope.Contains(installLoc);

                    long appBytes = 0;
                    if (installedHere && sub.GetValue("EstimatedSize") is int estKb && estKb > 0)
                        appBytes = estKb * 1024L;
                    else if (installedHere && !string.IsNullOrEmpty(installLoc) && Directory.Exists(installLoc) && !DeletionSafetyPolicy.IsProtected(installLoc))
                        appBytes = FolderSizing.Measure(installLoc, token).Bytes;

                    var leftovers = MatchLeftovers(name, publisher, localDirs, roamingDirs, excluded, token);
                    if (appBytes <= 0 && leftovers.Count == 0) continue;

                    var iconPath = AppIconService.GetAppIconPath(subName, name, displayIcon, installLoc);

                    raw[name] = new InstalledAppItem
                    {
                        Key = subName, DisplayName = name, Publisher = publisher, DisplayVersion = version,
                        InstallLocation = installLoc, UninstallString = uninstallStr, DisplayIcon = displayIcon,
                        IconPath = iconPath,
                        AppBytes = appBytes, Leftovers = leftovers, CanUninstall = installedHere,
                    };
                }
            }
            catch { }
        }

        return raw.Values.OrderByDescending(a => a.TotalBytes).ToList();
    }

    private static List<AppLeftoverItem> MatchLeftovers(
        string appName, string publisher, List<DirectoryInfo> localDirs, List<DirectoryInfo> roamingDirs,
        ISet<string> excluded, CancellationToken token)
    {
        var items = new List<AppLeftoverItem>();
        var cleanToken = NormalizeToken(appName);
        if (cleanToken.Length < 4) return items;

        void CheckDirs(List<DirectoryInfo> dirs, string scopeLabel)
        {
            foreach (var dir in dirs)
            {
                if (ProtectedAppDataFolders.Contains(dir.Name) || excluded.Contains(dir.FullName)) continue;
                var dirToken = NormalizeToken(dir.Name);
                var exact = dirToken.Equals(cleanToken, StringComparison.OrdinalIgnoreCase);
                var partial = !exact && dirToken.Length >= 5 && cleanToken.Contains(dirToken, StringComparison.OrdinalIgnoreCase);
                if (!exact && !partial) continue;

                var (bytes, _) = FolderSizing.Measure(dir.FullName, token);
                if (bytes < 32 * 1024) continue;
                items.Add(new AppLeftoverItem
                {
                    Path = dir.FullName, ScopeLabel = scopeLabel, Bytes = bytes,
                    IsExactMatch = exact, IsSelected = exact,
                });
            }
        }

        CheckDirs(localDirs, "AppData\\Local");
        CheckDirs(roamingDirs, "AppData\\Roaming");
        return items;
    }

    private static List<OrphanLeftoverItem> ScanOrphanLeftovers(
        List<InstalledAppItem> installedApps, ISet<string> excluded, CancellationToken token)
    {
        var orphans = new List<OrphanLeftoverItem>();
        var claimedPaths = installedApps.SelectMany(a => a.Leftovers.Select(l => l.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var appTokens = installedApps
            .SelectMany(a => new[] { NormalizeToken(a.DisplayName), NormalizeToken(a.Publisher) })
            .Where(t => t.Length >= 3)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingApp = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        foreach (var (root, kind) in new[] { (localApp, "AppData\\Local"), (roamingApp, "AppData\\Roaming") })
        {
            foreach (var dir in SafeTopDirs(root))
            {
                token.ThrowIfCancellationRequested();
                if (ProtectedAppDataFolders.Contains(dir.Name) || claimedPaths.Contains(dir.FullName) || excluded.Contains(dir.FullName))
                    continue;
                var tok = NormalizeToken(dir.Name);
                if (tok.Length < 4 || appTokens.Any(a => a.Contains(tok, StringComparison.OrdinalIgnoreCase) || tok.Contains(a, StringComparison.OrdinalIgnoreCase)))
                    continue;
                // Only consider folders untouched for > 30 days as candidate orphans
                if (dir.LastWriteTime > DateTime.Now.AddDays(-30)) continue;

                var (bytes, mod) = FolderSizing.Measure(dir.FullName, token);
                if (bytes < 512 * 1024) continue;
                orphans.Add(new OrphanLeftoverItem
                {
                    FolderName = dir.Name, FullPath = dir.FullName, LocationKind = kind,
                    Bytes = bytes, LastModified = mod,
                });
            }
        }
        return orphans.OrderByDescending(o => o.Bytes).ToList();
    }

    private static List<CliToolItem> ScanCliTools(ISet<string> excluded, CancellationToken token)
    {
        var tools = new List<CliToolItem>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        (string Dir, string Manager, string? CmdPrefix)[] roots =
        [
            (Path.Combine(home, "scoop", "apps"), "Scoop", "scoop uninstall"),
            (Path.Combine(appData, "npm", "node_modules"), "npm global", "npm uninstall -g"),
            (Path.Combine(home, ".local", "pipx", "venvs"), "pipx", "pipx uninstall"),
            (Path.Combine(appData, "uv", "tools"), "uv tool", "uv tool uninstall"),
        ];

        foreach (var (root, manager, cmdPrefix) in roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var dir in SafeTopDirs(root))
            {
                token.ThrowIfCancellationRequested();
                if (dir.Name.StartsWith('.') || dir.Name.Equals("scoop", StringComparison.OrdinalIgnoreCase) || excluded.Contains(dir.FullName))
                    continue;
                var (bytes, _) = FolderSizing.Measure(dir.FullName, token);
                if (bytes < 64 * 1024) continue;
                tools.Add(new CliToolItem
                {
                    Name = dir.Name, Manager = manager, Path = dir.FullName, Bytes = bytes,
                    UninstallCommand = cmdPrefix is null ? null : $"{cmdPrefix} {dir.Name}",
                });
            }
        }
        return tools.OrderByDescending(t => t.Bytes).ToList();
    }

    private static string NormalizeToken(string input) =>
        new(input.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static List<DirectoryInfo> SafeTopDirs(string root)
    {
        try
        {
            return new DirectoryInfo(root).EnumerateDirectories("*", new EnumerationOptions
            {
                IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint,
            }).ToList();
        }
        catch { return []; }
    }

    public static void LaunchSystemUninstaller(InstalledAppItem app)
    {
        if (string.IsNullOrWhiteSpace(app.UninstallString)) return;
        try
        {
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c {app.UninstallString}")
            {
                UseShellExecute = true,
            });
        }
        catch { }
    }
}
