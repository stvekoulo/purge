using PurgeWin.Models;
using PurgeWin.Utilities;

namespace PurgeWin.Services;

/// Purge for Windows: with multi-drive & bilingual support.
public static class EverythingElseScanner
{
    public static Task<List<EverythingElseFolder>> MeasureAsync(
        long diskUsedBytes, long purgeCategorizedBytes, long trashBytes,
        IReadOnlyList<string> activeDrives, CancellationToken token = default) =>
        Task.Run(() => Measure(diskUsedBytes, purgeCategorizedBytes, trashBytes, activeDrives, token), token);

    private static List<EverythingElseFolder> Measure(
        long diskUsedBytes, long purgeCategorizedBytes, long trashBytes,
        IReadOnlyList<string> activeDrives, CancellationToken token)
    {
        var items = new List<EverythingElseFolder>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingApp = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var sysRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        bool includeSys = activeDrives.Count == 0 || activeDrives.Any(d => string.Equals(d, sysRoot, StringComparison.OrdinalIgnoreCase));

        // 1. ManagedInApps (Blue)
        if (trashBytes > 1024 * 1024)
        {
            items.Add(new("app:trash",
                Loc.T("Corbeille Windows", "Windows Recycle Bin"), "shell:RecycleBinFolder",
                Loc.T("Fichiers déjà envoyés à la Corbeille.", "Files already moved to the Recycle Bin."),
                Loc.T("Videz la Corbeille depuis l'Explorateur Windows quand vous êtes prêt.", "Empty the Recycle Bin from File Explorer when ready."),
                EverythingElseBucket.ManagedInApps, trashBytes));
        }

        if (includeSys)
        {
            string[] steamPaths = [@"C:\Program Files (x86)\Steam\steamapps", @"C:\Program Files\Steam\steamapps", Path.Combine(localApp, "Steam")];
            foreach (var sp in steamPaths.Where(Directory.Exists))
            {
                var b = QuickDirSize(sp, 2, token);
                if (b > 50L * 1024 * 1024)
                {
                    items.Add(new($"app:steam:{sp}",
                        Loc.T("Bibliothèque Steam", "Steam Library"), sp,
                        Loc.T("Jeux et contenus gérés par Steam.", "Games and content managed by Steam."),
                        Loc.T("Désinstallez les jeux inutilisés depuis Steam > Paramètres > Stockage.", "Uninstall unused games in Steam > Settings > Storage."),
                        EverythingElseBucket.ManagedInApps, b));
                    break;
                }
            }

            var dockerWsl = Path.Combine(localApp, "Docker", "wsl");
            if (Directory.Exists(dockerWsl))
            {
                var b = QuickDirSize(dockerWsl, 2, token);
                if (b > 50L * 1024 * 1024)
                {
                    items.Add(new("app:docker",
                        Loc.T("Disque virtuel Docker Desktop", "Docker Desktop Virtual Disk"), dockerWsl,
                        Loc.T("Images, conteneurs et volumes WSL2 de Docker Desktop.", "WSL2 images, containers, and volumes for Docker Desktop."),
                        Loc.T("Utilisez Docker Desktop ou 'docker system prune' pour réduire cette image.", "Use Docker Desktop or 'docker system prune' to shrink this image."),
                        EverythingElseBucket.ManagedInApps, b));
                }
            }

            // 2. YourFiles on System Drive (Amber)
            (string Path, string NameFr, string NameEn, string DescFr, string DescEn)[] userFolders =
            [
                (Path.Combine(home, "Downloads"), "Téléchargements", "Downloads", "Vos téléchargements personnels.", "Your personal downloads."),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Documents", "Documents", "Vos documents et projets.", "Your documents and projects."),
                (Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Bureau", "Desktop", "Fichiers posés sur votre Bureau.", "Files on your Desktop."),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Vidéos", "Videos", "Vos vidéos et enregistrements.", "Your videos and recordings."),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Images", "Pictures", "Vos photos et captures d'écran.", "Your photos and screenshots."),
                (Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Musique", "Music", "Votre bibliothèque audio.", "Your audio library."),
            ];

            foreach (var u in userFolders.DistinctBy(x => x.Path, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(u.Path) || !Directory.Exists(u.Path)) continue;
                var b = QuickDirSize(u.Path, 2, token);
                if (b > 10L * 1024 * 1024)
                {
                    items.Add(new($"user:{u.NameEn}", Loc.T(u.NameFr, u.NameEn), u.Path, Loc.T(u.DescFr, u.DescEn),
                        Loc.T("Parcourez l'onglet Fichiers volumineux ou ouvrez dans l'Explorateur.", "Browse the Large Files tab or reveal in File Explorer."),
                        EverythingElseBucket.YourFiles, b));
                }
            }

            // 3. LeftAlone (Coral)
            var leftLocal = Math.Max(0, QuickDirSize(localApp, 1, token) - purgeCategorizedBytes / 2);
            if (leftLocal > 50L * 1024 * 1024)
            {
                items.Add(new("left:localappdata",
                    Loc.T("Données d'applications (AppData\\Local)", "App Data (AppData\\Local)"), localApp,
                    Loc.T("Bases de données, profils et sessions que Purge ne touche jamais par sécurité.", "Databases, profiles, and sessions Purge never touches for safety."),
                    Loc.T("Conservé volontairement pour préserver vos comptes et réglages.", "Deliberately kept to preserve your accounts and settings."),
                    EverythingElseBucket.LeftAlone, leftLocal));
            }

            var leftRoaming = QuickDirSize(roamingApp, 1, token);
            if (leftRoaming > 50L * 1024 * 1024)
            {
                items.Add(new("left:roaming",
                    Loc.T("Préférences et profils (AppData\\Roaming)", "Preferences & Profiles (AppData\\Roaming)"), roamingApp,
                    Loc.T("Configurations, extensions et profils utilisateur de vos logiciels.", "Configurations, extensions, and user profiles for your apps."),
                    Loc.T("Conservé volontairement par Purge.", "Deliberately kept by Purge."),
                    EverythingElseBucket.LeftAlone, leftRoaming));
            }
        }

        // Secondary Drives top-level folders (YourFiles / LeftAlone)
        foreach (var drive in activeDrives.Where(d => !string.Equals(d, sysRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(d)))
        {
            try
            {
                foreach (var sub in Directory.EnumerateDirectories(drive))
                {
                    var name = Path.GetFileName(sub);
                    if (name.StartsWith('$') || name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)) continue;
                    var b = QuickDirSize(sub, 2, token);
                    if (b < 25L * 1024 * 1024) continue;
                    items.Add(new($"drive:{sub}", $"{drive.TrimEnd('\\')}\\{name}", sub,
                        Loc.T($"Dossier sur le disque {drive.TrimEnd('\\')}.", $"Folder on drive {drive.TrimEnd('\\')}."),
                        Loc.T("Ouvrez dans l'Explorateur ou inspectez via Outils de dev / Fichiers volumineux.", "Open in File Explorer or inspect via Dev Tools / Large Files."),
                        EverythingElseBucket.YourFiles, b));
                }
            }
            catch { }
        }

        // 4. ManagedByWindows (Grey)
        var accounted = items.Sum(i => i.Bytes) + purgeCategorizedBytes;
        var remaining = Math.Max(2L * 1024 * 1024 * 1024, diskUsedBytes - accounted);
        if (includeSys)
        {
            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var progDir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            items.Add(new("win:os",
                Loc.T("Windows & Composants système (C:\\Windows)", "Windows & System Components (C:\\Windows)"), winDir,
                Loc.T("Système d'exploitation, WinSxS, pilotes, mémoire virtuelle et clichés.", "Operating system, WinSxS, drivers, pagefile, and shadow copies."),
                Loc.T("Géré automatiquement par Windows.", "Managed automatically by Windows."),
                EverythingElseBucket.ManagedByWindows, (long)(remaining * 0.62)));

            items.Add(new("win:programfiles",
                Loc.T("Programmes système & Runtime (Program Files)", "System Programs & Runtimes (Program Files)"), progDir,
                Loc.T("Binaires d'applications, runtimes partagés et bibliothèques système.", "Application binaries, shared runtimes, and system libraries."),
                Loc.T("Géré par les installeurs Windows et le Désinstalleur.", "Managed by Windows installers and the Uninstaller tab."),
                EverythingElseBucket.ManagedByWindows, (long)(remaining * 0.38)));
        }
        else if (remaining > 0)
        {
            var firstDrive = activeDrives.FirstOrDefault() ?? sysRoot;
            items.Add(new("win:vol",
                Loc.T($"Système de fichiers & métadonnées ({firstDrive.TrimEnd('\\')})", $"Filesystem & Metadata ({firstDrive.TrimEnd('\\')})"), firstDrive,
                Loc.T("Index NTFS, clichés instantanés et dossiers profonds.", "NTFS MFT, shadow copies, and deep directory trees."),
                Loc.T("Géré par le système de fichiers Windows.", "Managed by the Windows filesystem."),
                EverythingElseBucket.ManagedByWindows, remaining));
        }

        return items.OrderByDescending(i => i.Bytes).ToList();
    }

    private static long QuickDirSize(string root, int maxDepth, CancellationToken token)
    {
        long total = 0;
        void Walk(string dir, int depth)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var di = new DirectoryInfo(dir);
                foreach (var f in di.EnumerateFiles("*", new EnumerationOptions { IgnoreInaccessible = true }))
                    total += f.Length;
                if (depth >= maxDepth) return;
                foreach (var sub in di.EnumerateDirectories("*", new EnumerationOptions
                {
                    IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint,
                }))
                {
                    Walk(sub.FullName, depth + 1);
                }
            }
            catch { }
        }
        Walk(root, 0);
        return total;
    }
}
