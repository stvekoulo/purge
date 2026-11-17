using System.Diagnostics;
using PurgeWin.Models;
using PurgeWin.Utilities;

namespace PurgeWin.Services;

/// <summary>Executes safe, reversible Windows maintenance tasks (DNS flush,
/// Explorer thumbnail cache refresh, Microsoft Store cache, stale app updaters).</summary>
public static class MaintenanceRunner
{
    public static List<MaintenanceTaskItem> CreateDefaultTasks() =>
    [
        new()
        {
            Id = "flush-dns",
            Glyph = "\uE774",
            TitleFr = "Vider le cache DNS Windows",
            TitleEn = "Flush Windows DNS Cache",
            DescriptionFr = "Efface les résolutions de domaines obsolètes (ipconfig /flushdns) pour corriger les lenteurs ou erreurs réseau.",
            DescriptionEn = "Clears stale DNS resolver entries (ipconfig /flushdns) to fix slow connections or domain lookup issues.",
        },
        new()
        {
            Id = "refresh-thumbnails",
            Glyph = "\uEB9F",
            TitleFr = "Régénérer le cache de miniatures de l'Explorateur",
            TitleEn = "Refresh Explorer Thumbnail Cache",
            DescriptionFr = "Envoie à la Corbeille les bases thumbcache non verrouillées pour corriger les icônes ou aperçus corrompus.",
            DescriptionEn = "Moves unlocked thumbcache databases to the Recycle Bin so Windows rebuilds crisp file previews.",
        },
        new()
        {
            Id = "store-cache",
            Glyph = "\uE719",
            TitleFr = "Nettoyer le cache local du Microsoft Store",
            TitleEn = "Clean Microsoft Store Local Cache",
            DescriptionFr = "Libère les fichiers temporaires du Microsoft Store (LocalCache) sans toucher à vos applications installées.",
            DescriptionEn = "Clears temporary Microsoft Store LocalCache files without affecting installed apps.",
        },
        new()
        {
            Id = "stale-updaters",
            Glyph = "\uE896",
            TitleFr = "Purger les installeurs de mises à jour obsolètes",
            TitleEn = "Purge Stale App Update Packages",
            DescriptionFr = "Supprime les paquets d'installation déjà appliqués (mises à jour Purge et caches *-updater).",
            DescriptionEn = "Removes already-applied update installers (Purge updates and pending *-updater packages).",
        },
    ];

    public static async Task<(long FreedBytes, bool Success)> RunTaskAsync(MaintenanceTaskItem task)
    {
        task.IsRunning = true;
        try
        {
            return await Task.Run(() => task.Id switch
            {
                "flush-dns" => RunFlushDns(task),
                "refresh-thumbnails" => RunThumbnailRefresh(task),
                "store-cache" => RunStoreCacheClean(task),
                "stale-updaters" => RunStaleUpdatersClean(task),
                _ => (0L, false),
            });
        }
        finally
        {
            task.IsRunning = false;
        }
    }

    private static (long Freed, bool Ok) RunFlushDns(MaintenanceTaskItem task)
    {
        try
        {
            var ipconfig = Path.Combine(Environment.SystemDirectory, "ipconfig.exe");
            var psi = new ProcessStartInfo(ipconfig) { CreateNoWindow = true, UseShellExecute = false };
            psi.ArgumentList.Add("/flushdns");
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
            bool ok = proc?.ExitCode == 0;
            task.LastResultFr = ok ? "✓ Cache DNS vidé avec succès" : "Échec de la commande ipconfig";
            task.LastResultEn = ok ? "✓ DNS cache flushed successfully" : "ipconfig command failed";
            return (0, ok);
        }
        catch
        {
            task.LastResultFr = "Impossible d'exécuter ipconfig";
            task.LastResultEn = "Could not run ipconfig";
            return (0, false);
        }
    }

    private static (long Freed, bool Ok) RunThumbnailRefresh(MaintenanceTaskItem task)
    {
        var explorerDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Explorer");
        if (!Directory.Exists(explorerDir))
        {
            task.LastResultFr = "✓ Aucun cache de miniatures superflu";
            task.LastResultEn = "✓ No stale thumbnail cache found";
            return (0, true);
        }

        long freed = 0;
        int moved = 0;
        foreach (var file in Directory.EnumerateFiles(explorerDir, "thumbcache_*.db"))
        {
            try
            {
                var len = new FileInfo(file).Length;
                if (len == 0) continue;
                using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                if (RecycleBin.MoveToRecycleBin(file) is null) { freed += len; moved++; }
            }
            catch { /* active thumbcache locked by Explorer is safely kept */ }
        }
        task.LastResultFr = moved > 0
            ? $"✓ {moved} fichier(s) nettoyé(s) ({ByteFormatter.Format(freed)})"
            : "✓ Miniatures actives déjà propres (fichiers ouverts conservés)";
        task.LastResultEn = moved > 0
            ? $"✓ {moved} file(s) cleaned ({ByteFormatter.Format(freed)})"
            : "✓ Active thumbnails already clean (in-use files kept)";
        return (freed, true);
    }

    private static (long Freed, bool Ok) RunStoreCacheClean(MaintenanceTaskItem task)
    {
        var storeCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages", "Microsoft.WindowsStore_8wekyb3d8bbwe", "LocalCache");
        if (!Directory.Exists(storeCache))
        {
            task.LastResultFr = "✓ Cache Microsoft Store déjà vide";
            task.LastResultEn = "✓ Microsoft Store cache already empty";
            return (0, true);
        }

        long freed = 0;
        int moved = 0;
        foreach (var entry in new DirectoryInfo(storeCache).EnumerateFileSystemInfos())
        {
            try
            {
                if (PathExpander.IsReparseLink(entry)) continue;
                var size = FolderSizing.MeasureEntry(entry);
                if (RecycleBin.MoveToRecycleBin(entry.FullName) is null) { freed += size; moved++; }
            }
            catch { }
        }
        task.LastResultFr = moved > 0
            ? $"✓ Cache nettoyé ({ByteFormatter.Format(freed)})"
            : "✓ Cache Microsoft Store déjà propre";
        task.LastResultEn = moved > 0
            ? $"✓ Cache cleaned ({ByteFormatter.Format(freed)})"
            : "✓ Microsoft Store cache already clean";
        return (freed, true);
    }

    private static (long Freed, bool Ok) RunStaleUpdatersClean(MaintenanceTaskItem task)
    {
        long freed = UpdateService.PurgeStaleInstallers(
            Path.Combine(AppData.Folder, "Updates"), UpdateService.CurrentVersion);

        foreach (var dir in PathExpander.Expand("%LOCALAPPDATA%\\*-updater\\pending"))
        {
            try
            {
                if (!Directory.Exists(dir) || DeletionSafetyPolicy.IsProtected(dir)) continue;
                var size = FolderSizing.Measure(dir).Bytes;
                if (size > 0 && RecycleBin.MoveToRecycleBin(dir) is null) freed += size;
            }
            catch { }
        }

        task.LastResultFr = freed > 0
            ? $"✓ {ByteFormatter.Format(freed)} d'installeurs obsolètes purgés"
            : "✓ Aucun installeur obsolète en attente";
        task.LastResultEn = freed > 0
            ? $"✓ {ByteFormatter.Format(freed)} of stale installers purged"
            : "✓ No stale update installers found";
        return (freed, true);
    }
}
