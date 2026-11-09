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

    private static readonly Dictionary<string, LargeFileCategory> ExtMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"] = LargeFileCategory.Video, [".mkv"] = LargeFileCategory.Video, [".mov"] = LargeFileCategory.Video,
        [".avi"] = LargeFileCategory.Video, [".webm"] = LargeFileCategory.Video, [".wmv"] = LargeFileCategory.Video,
        [".mp3"] = LargeFileCategory.Audio, [".wav"] = LargeFileCategory.Audio, [".flac"] = LargeFileCategory.Audio,
        [".m4a"] = LargeFileCategory.Audio, [".aac"] = LargeFileCategory.Audio, [".ogg"] = LargeFileCategory.Audio,
        [".png"] = LargeFileCategory.Image, [".jpg"] = LargeFileCategory.Image, [".jpeg"] = LargeFileCategory.Image,
        [".webp"] = LargeFileCategory.Image, [".tiff"] = LargeFileCategory.Image, [".psd"] = LargeFileCategory.Image,
        [".raw"] = LargeFileCategory.Image, [".heic"] = LargeFileCategory.Image,
        [".zip"] = LargeFileCategory.Archive, [".rar"] = LargeFileCategory.Archive, [".7z"] = LargeFileCategory.Archive,
        [".tar"] = LargeFileCategory.Archive, [".gz"] = LargeFileCategory.Archive, [".tgz"] = LargeFileCategory.Archive,
        [".pdf"] = LargeFileCategory.Document, [".docx"] = LargeFileCategory.Document, [".pptx"] = LargeFileCategory.Document,
        [".xlsx"] = LargeFileCategory.Document,
        [".iso"] = LargeFileCategory.DiskImage, [".img"] = LargeFileCategory.DiskImage, [".wim"] = LargeFileCategory.DiskImage,
        [".vhdx"] = LargeFileCategory.VirtualMachine, [".vhd"] = LargeFileCategory.VirtualMachine,
        [".vmdk"] = LargeFileCategory.VirtualMachine, [".vdi"] = LargeFileCategory.VirtualMachine,
        [".ova"] = LargeFileCategory.VirtualMachine,
        [".gguf"] = LargeFileCategory.AiModel, [".safetensors"] = LargeFileCategory.AiModel,
        [".onnx"] = LargeFileCategory.AiModel, [".pt"] = LargeFileCategory.AiModel, [".pth"] = LargeFileCategory.AiModel,
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

            foreach (var (rootPath, label, maxDepth) in roots)
            {
                if (!Directory.Exists(rootPath) || !scope.Allows(rootPath)) continue;
                WalkFolder(rootPath, label, oneDriveRoot, excluded, 0, maxDepth, seenFiles, item =>
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

    private static List<(string Path, string Label, int MaxDepth)> GetScanRoots(IReadOnlyList<string> activeDrives)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var list = new List<(string Path, string Label, int MaxDepth)>
        {
            (Path.Combine(home, "Downloads"), Loc.T("Téléchargements", "Downloads"), 5),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Documents", 5),
            (Environment.GetFolderPath(Environment.SpecialFolder.Desktop), Loc.T("Bureau", "Desktop"), 5),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), Loc.T("Vidéos", "Videos"), 5),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), Loc.T("Musique", "Music"), 5),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Loc.T("Images", "Pictures"), 5),
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
                    list.Add((sub, $"{drive.TrimEnd('\\')}\\{name}", 3));
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
        string folder, string rootLabel, string? oneDriveRoot, ISet<string> excluded,
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
                AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
            });
        }
        catch { return; }

        foreach (var entry in entries)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (excluded.Contains(entry.FullName)) continue;

                if (entry is DirectoryInfo subDir)
                {
                    if (subDir.Name.StartsWith('.') || SkippedDirs.Contains(subDir.Name)) continue;
                    WalkFolder(subDir.FullName, rootLabel, oneDriveRoot, excluded, depth + 1, maxDepth, seenFiles, emit, token);
                }
                else if (entry is FileInfo file)
                {
                    if (file.Length < MinimumBytes || !seenFiles.Add(file.FullName)) continue;
                    var ext = file.Extension;
                    var category = ExtMap.GetValueOrDefault(ext, LargeFileCategory.Other);
                    var (specialLabel, specialExplain) = InspectSpecialFile(ext);
                    var isOneDrive = !string.IsNullOrEmpty(oneDriveRoot)
                        && PathScope.IsWithin(file.FullName, oneDriveRoot)
                        && ((int)file.Attributes & 0x00400000) == 0;

                    emit(new LargeFileItem
                    {
                        FullPath = file.FullName,
                        Name = file.Name,
                        FolderDisplay = $"{rootLabel} ({Path.GetDirectoryName(file.FullName)})",
                        Bytes = file.Length,
                        LastModified = file.LastWriteTime,
                        Category = category,
                        SpecialLabel = specialLabel,
                        SpecialExplanation = specialExplain,
                        IsOneDriveSynced = isOneDrive,
                    });
                }
            }
            catch (IOException) { /* File changed or was removed during the scan. */ }
            catch (UnauthorizedAccessException) { /* Skip an inaccessible entry. */ }
        }
    }

    private static (string? Label, string? Explain) InspectSpecialFile(string ext) =>
        ext.ToLowerInvariant() switch
        {
            ".crdownload" or ".part" or ".partial" => (
                Loc.T("Téléchargement inachevé", "Unfinished download"),
                Loc.T("Fichier d'un téléchargement interrompu par le navigateur.", "Partial file from an interrupted browser download.")),
            ".iso" or ".wim" or ".img" => (
                Loc.T("Image d'installation", "Installer disk image"),
                Loc.T("Image disque volumineuse, souvent inutile après l'installation.", "Large disk image, often unneeded after installation.")),
            ".vhdx" or ".vhd" or ".vmdk" or ".vdi" => (
                Loc.T("Disque de machine virtuelle", "Virtual machine disk"),
                Loc.T("Disque virtuel Hyper-V, VMware ou VirtualBox.", "Virtual disk for Hyper-V, VMware, or VirtualBox.")),
            _ => (null, null),
        };

    public static bool EvictOneDriveLocalCopy(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            using var proc = Process.Start(new ProcessStartInfo("attrib.exe", $"+U -P \"{path}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            proc?.WaitForExit(4000);
            return proc?.ExitCode == 0;
        }
        catch { return false; }
    }
}
