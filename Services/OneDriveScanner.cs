using System.Diagnostics;
using PurgeWin.Models;

namespace PurgeWin.Services;

/// <summary>Dedicated OneDrive scanner and Cloud Files state manager.
/// Inspects Windows Cloud Filter flags to distinguish local vs online-only vs pinned files.</summary>
public static class OneDriveScanner
{
    public const long MinimumBytes = 1L * 1024 * 1024; // 1 MB threshold for dedicated OneDrive view

    private const int FILE_ATTRIBUTE_RECALL_ON_OPEN = 0x00040000;
    private const int FILE_ATTRIBUTE_PINNED = 0x00080000;
    private const int FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS = 0x00400000;

    private static readonly HashSet<string> SkippedDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", ".hg", ".svn", "target", "venv", ".venv",
        "__pycache__", ".next", ".nuxt", "bin", "obj", ".vault",
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
        [".xlsx"] = LargeFileCategory.Document, [".doc"] = LargeFileCategory.Document, [".xls"] = LargeFileCategory.Document,
        [".iso"] = LargeFileCategory.DiskImage, [".img"] = LargeFileCategory.DiskImage, [".wim"] = LargeFileCategory.DiskImage,
        [".vhdx"] = LargeFileCategory.VirtualMachine, [".vhd"] = LargeFileCategory.VirtualMachine,
        [".vmdk"] = LargeFileCategory.VirtualMachine, [".vdi"] = LargeFileCategory.VirtualMachine,
    };

    public static IReadOnlyList<string> GetOneDriveRoots()
    {
        var candidates = new List<string?>
        {
            Environment.GetEnvironmentVariable("OneDrive"),
            Environment.GetEnvironmentVariable("OneDriveConsumer"),
            Environment.GetEnvironmentVariable("OneDriveCommercial"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive"),
        };
        return candidates
            .Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
            .Select(p => Path.GetFullPath(p!).TrimEnd('\\'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool IsOneDriveAvailable => GetOneDriveRoots().Count > 0;

    public static OneDriveSyncState InspectSyncState(FileAttributes attrs)
    {
        int raw = (int)attrs;
        if ((raw & (FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS | FILE_ATTRIBUTE_RECALL_ON_OPEN | (int)FileAttributes.Offline | (int)FileAttributes.SparseFile)) != 0
            && (raw & (FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS | FILE_ATTRIBUTE_RECALL_ON_OPEN | (int)FileAttributes.Offline)) != 0)
        {
            return OneDriveSyncState.CloudOnly;
        }
        if ((raw & FILE_ATTRIBUTE_PINNED) != 0)
        {
            return OneDriveSyncState.Pinned;
        }
        return OneDriveSyncState.Local;
    }

    public static async Task<List<OneDriveFileItem>> ScanAsync(
        ISet<string> excluded, IReadOnlyList<string> activeDrives, CancellationToken token)
    {
        var results = new List<OneDriveFileItem>();
        var scope = new PathScope(activeDrives, excluded);
        var roots = GetOneDriveRoots().Where(r => scope.Allows(r)).ToList();
        if (roots.Count == 0) return results;

        await Task.Run(() =>
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in roots)
            {
                WalkRoot(root, root, excluded, scope, 0, 7, seen, results, token);
            }
        }, token);

        return results.OrderByDescending(i => i.Bytes).ToList();
    }

    private static void WalkRoot(
        string currentDir, string oneDriveRoot, ISet<string> excluded, PathScope scope,
        int depth, int maxDepth, ISet<string> seen, List<OneDriveFileItem> results, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (depth > maxDepth) return;

        IEnumerable<FileSystemInfo> entries;
        try
        {
            entries = new DirectoryInfo(currentDir).EnumerateFileSystemInfos("*", new EnumerationOptions
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
                    WalkRoot(subDir.FullName, oneDriveRoot, excluded, scope, depth + 1, maxDepth, seen, results, token);
                }
                else if (entry is FileInfo file)
                {
                    if (file.Length < MinimumBytes || !scope.CanDelete(file.FullName) || !seen.Add(file.FullName))
                        continue;

                    var relDir = Path.GetRelativePath(oneDriveRoot, Path.GetDirectoryName(file.FullName) ?? oneDriveRoot);
                    var displayDir = relDir == "."
                        ? "OneDrive"
                        : "OneDrive › " + relDir.Replace('\\', '/').Replace("/", " › ");
                    var category = ExtMap.GetValueOrDefault(file.Extension, LargeFileCategory.Other);
                    var syncState = InspectSyncState(file.Attributes);

                    results.Add(new OneDriveFileItem
                    {
                        FullPath = file.FullName,
                        Name = file.Name,
                        FolderDisplay = displayDir,
                        FolderDisplayEn = displayDir,
                        Bytes = file.Length,
                        LastModified = file.LastWriteTime,
                        Category = category,
                        SyncState = syncState,
                    });
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public static bool EvictLocalCopy(string path)
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
