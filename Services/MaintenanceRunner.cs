using System.Runtime.InteropServices;
using PurgeWin.Models;
using PurgeWin.Utilities;

namespace PurgeWin.Services;

/// <summary>Executes safe, reversible Windows maintenance tasks (DNS flush,
/// Explorer thumbnail cache refresh, Microsoft Store cache, stale app updaters).
/// Background workers return pure value tuples so ObservableObject properties are
/// only mutated on the UI thread.</summary>
public static class MaintenanceRunner
{
    [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
    private static extern bool DnsFlushResolverCache();

    private sealed record TaskOutcome(long Freed, bool Ok, string ResultFr, string ResultEn);

    public static List<MaintenanceTaskItem> CreateDefaultTasks() =>
    [
        new()
        {
            Id = "flush-dns",
            Glyph = "\uE774",
            TitleFr = "Vider le cache DNS Windows",
            TitleEn = "Flush Windows DNS Cache",
            DescriptionFr = "Efface les résolutions de domaines obsolètes (cache DNS Windows) pour corriger les lenteurs ou erreurs réseau.",
            DescriptionEn = "Clears stale DNS resolver entries (Windows DNS cache) to fix slow connections or domain lookup issues.",
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
        var taskId = task.Id;
        task.IsRunning = true;
        try
        {
            var outcome = await Task.Run(() => taskId switch
            {
                "flush-dns" => RunFlushDns(),
                "refresh-thumbnails" => RunThumbnailRefresh(),
                "store-cache" => RunStoreCacheClean(),
                "stale-updaters" => RunStaleUpdatersClean(),
                _ => new TaskOutcome(0L, false, "", ""),
            });
            task.LastSucceeded = outcome.Ok;
            task.LastResultFr = outcome.ResultFr;
            task.LastResultEn = outcome.ResultEn;
            return (outcome.Freed, outcome.Ok);
        }
        finally
        {
            task.IsRunning = false;
        }
    }

    private static TaskOutcome RunFlushDns()
    {
        try
        {
            bool ok = DnsFlushResolverCache();
            return new(
                0,
                ok,
                ok ? "✓ Cache DNS vidé avec succès" : "Service Client DNS indisponible",
                ok ? "✓ DNS cache flushed successfully" : "DNS Client service unavailable");
        }
        catch
        {
            return new(0, false, "Impossible de vider le cache DNS", "Could not flush DNS cache");
        }
    }

    private static TaskOutcome RunThumbnailRefresh()
    {
        var explorerDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Explorer");
        if (!Directory.Exists(explorerDir))
            return new(0, true, "✓ Aucun cache de miniatures superflu", "✓ No stale thumbnail cache found");

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
        return new(
            freed,
            true,
            moved > 0
                ? $"✓ {moved} fichier(s) nettoyé(s) ({ByteFormatter.Format(freed)})"
                : "✓ Miniatures actives déjà propres (fichiers ouverts conservés)",
            moved > 0
                ? $"✓ {moved} file(s) cleaned ({ByteFormatter.Format(freed)})"
                : "✓ Active thumbnails already clean (in-use files kept)");
    }

    private static TaskOutcome RunStoreCacheClean()
    {
        var storeCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages", "Microsoft.WindowsStore_8wekyb3d8bbwe", "LocalCache");
        if (!Directory.Exists(storeCache))
            return new(0, true, "✓ Cache Microsoft Store déjà vide", "✓ Microsoft Store cache already empty");

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
        return new(
            freed,
            true,
            moved > 0 && freed > 0 ? $"✓ Cache nettoyé ({ByteFormatter.Format(freed)})"
                : moved > 0 ? "✓ Cache nettoyé" : "✓ Cache Microsoft Store déjà propre",
            moved > 0 && freed > 0 ? $"✓ Cache cleaned ({ByteFormatter.Format(freed)})"
                : moved > 0 ? "✓ Cache cleaned" : "✓ Microsoft Store cache already clean");
    }

    private static TaskOutcome RunStaleUpdatersClean()
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

        return new(
            freed,
            true,
            freed > 0 ? $"✓ {ByteFormatter.Format(freed)} d'installeurs obsolètes purgés" : "✓ Aucun installeur obsolète en attente",
            freed > 0 ? $"✓ {ByteFormatter.Format(freed)} of stale installers purged" : "✓ No stale update installers found");
    }
}
