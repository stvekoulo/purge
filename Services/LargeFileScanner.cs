using System.Diagnostics;
using PurgeWin.Models;
using PurgeWin.Utilities;

namespace PurgeWin.Services;

/// Port of LargeFileScanner + ICloudDownloadPolicy (adapted to OneDrive & multi-drive).
public static class LargeFileScanner
{
    public const long MinimumBytes = 5L * 1024 * 1024; // 5 MB floor minimum threshold

    private static readonly HashSet<string> SkippedDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", ".hg", ".svn", "target", "venv", ".venv",
        "__pycache__", ".next", ".nuxt", ".gradle", "Pods", "DerivedData", "bin", "obj",
        "Windows", "Program Files", "Program Files (x86)", "ProgramData", "AppData",
        "$Recycle.Bin", "System Volume Information", "Recovery", "PerfLogs", "Config.Msi",
    };

    public static async Task<List<LargeFileItem>> ScanAsync(
        Action<LargeFileItem>? onItem, ISet<string> excluded, IReadOnlyList<string> activeDrives, CancellationToken token)
    {
        var results = new List<LargeFileItem>();
        var scope = new PathScope(activeDrives, excluded);
        await Task.Run(() =>
        {
            bool OnActiveDrive(string p) =>
                scope.CanDelete(p);

            var roots = GetScanRoots(activeDrives);
            var oneDriveRoot = Environment.GetEnvironmentVariable("OneDrive");
            var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (rootPath, labelFr, labelEn, maxDepth) in roots)
            {
                if (!Directory.Exists(rootPath) || !scope.Allows(rootPath)) continue;
                WalkFolder(rootPath, labelFr, labelEn, oneDriveRoot, excluded, 0, maxDepth, seenFiles, item =>
                {
                    if (!scope.CanDelete(item.FullPath)) return;
                    lock (results) results.Add(item);
                    onItem?.Invoke(item);
                }, token);
            }

            foreach (var aiItem in DuplicateAndAiScanner.ScanAiModels(excluded, token, scope))
            {
                if (!OnActiveDrive(aiItem.FullPath) || !seenFiles.Add(aiItem.FullPath)) continue;
                lock (results) results.Add(aiItem);
                onItem?.Invoke(aiItem);
            }

            DuplicateAndAiScanner.MarkDuplicates(results, token);
        }, token);

        return results.OrderByDescending(i => i.Bytes).ToList();
    }

    private static List<(string Path, string LabelFr, string LabelEn, int MaxDepth)> GetScanRoots(IReadOnlyList<string> activeDrives)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var list = new List<(string Path, string LabelFr, string LabelEn, int MaxDepth)>
        {
            (Path.Combine(home, "Downloads"), "Téléchargements", "Downloads", 5),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Documents", "Documents", 5),
            (Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Bureau", "Desktop", 5),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Vidéos", "Videos", 5),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Musique", "Music", 5),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Images", "Pictures", 5),
        };

        var sysRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        foreach (var drive in activeDrives)
        {
            if (string.Equals(drive, sysRoot, StringComparison.OrdinalIgnoreCase)) continue;
            if (!Directory.Exists(drive)) continue;
            try
            {
                foreach (var sub in Directory.EnumerateDirectories(drive))
                {
                    var name = Path.GetFileName(sub);
                    if (name.StartsWith('$') || name.StartsWith('.') || SkippedDirs.Contains(name)) continue;
                    var label = $"{drive.TrimEnd('\\')}\\{name}";
                    list.Add((sub, label, label, 3));
                }
            }
            catch { }
        }

        return list.Where(r => !string.IsNullOrEmpty(r.Path) &&
                activeDrives.Any(d => PathScope.IsWithin(r.Path, d)))
            .DistinctBy(r => r.Path.TrimEnd('\\'), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void WalkFolder(
        string folder, string rootLabelFr, string rootLabelEn, string? oneDriveRoot, ISet<string> excluded,
        int depth, int maxDepth, ISet<string> seenFiles, Action<LargeFileItem> emit, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (depth > maxDepth) return;
        IEnumerable<FileSystemInfo> entries;
        try
        {
            entries = new DirectoryInfo(folder).EnumerateFileSystemInfos("*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System,
            }).ToList();
        }
        catch { return; }

        foreach (var entry in entries)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (excluded.Contains(entry.FullName) || PathExpander.IsReparseLink(entry)) continue;

                if (entry is DirectoryInfo subDir)
                {
                    if (subDir.Name.StartsWith('.') || SkippedDirs.Contains(subDir.Name)) continue;
                    WalkFolder(subDir.FullName, rootLabelFr, rootLabelEn, oneDriveRoot, excluded, depth + 1, maxDepth, seenFiles, emit, token);
                }
                else if (entry is FileInfo file)
                {
                    if (file.Length < MinimumBytes || !seenFiles.Add(file.FullName)) continue;
                    var ext = file.Extension;
                    var category = OneDriveScanner.CategorizeExtension(ext);
                    var (specialLabelFr, specialLabelEn, specialExplainFr, specialExplainEn) = InspectSpecialFile(ext, file.FullName, file.LastWriteTime);
                    var isOneDrive = !string.IsNullOrEmpty(oneDriveRoot)
                        && PathScope.IsWithin(file.FullName, oneDriveRoot)
                        && ((int)file.Attributes & 0x00400000) == 0;

                    emit(new LargeFileItem
                    {
                        FullPath = file.FullName,
                        Name = file.Name,
                        FolderDisplay = $"{rootLabelFr} ({Path.GetDirectoryName(file.FullName)})",
                        FolderDisplayEn = $"{rootLabelEn} ({Path.GetDirectoryName(file.FullName)})",
                        Bytes = file.Length,
                        LastModified = file.LastWriteTime,
                        Category = category,
                        SpecialLabel = specialLabelFr,
                        SpecialLabelEn = specialLabelEn,
                        SpecialExplanation = specialExplainFr,
                        SpecialExplanationEn = specialExplainEn,
                        IsOneDriveSynced = isOneDrive,
                    });
                }
            }
            catch (IOException) { /* File changed or was removed during the scan. */ }
            catch (UnauthorizedAccessException) { /* Skip an inaccessible entry. */ }
        }
    }

    private static (string? LabelFr, string? LabelEn, string? ExplainFr, string? ExplainEn) InspectSpecialFile(
        string ext, string fullPath, DateTime lastModified) =>
        ext.ToLowerInvariant() switch
        {
            ".crdownload" or ".part" or ".partial" => (
                "Téléchargement inachevé", "Unfinished download",
                "Fichier d'un téléchargement interrompu par le navigateur.", "Partial file from an interrupted browser download."),
            ".iso" or ".wim" or ".img" => (
                "Image d'installation", "Installer disk image",
                "Image disque volumineuse, souvent inutile après l'installation.", "Large disk image, often unneeded after installation."),
            ".vhdx" or ".vhd" or ".vmdk" or ".vdi" => (
                "Disque de machine virtuelle", "Virtual machine disk",
                "Disque virtuel Hyper-V, VMware ou VirtualBox.", "Virtual disk for Hyper-V, VMware, or VirtualBox."),
            ".exe" or ".msi" or ".msix" when fullPath.Contains("\\Downloads\\", StringComparison.OrdinalIgnoreCase)
                && lastModified < DateTime.Now.AddDays(-30) => (
                "Ancien installeur", "Old installer",
                "Programme d'installation téléchargé il y a plus de 30 jours.", "Installer package downloaded over 30 days ago."),
            _ => (null, null, null, null),
        };

    public static bool EvictOneDriveLocalCopy(string path) => OneDriveScanner.EvictLocalCopy(path);
}
