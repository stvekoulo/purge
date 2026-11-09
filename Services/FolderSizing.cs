namespace PurgeWin.Services;

/// Port of FolderSizing: logical bytes and newest change under a folder,
/// skipping reparse points so a junction can never inflate or redirect a size.
public static class FolderSizing
{
    private static readonly EnumerationOptions Options = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        ReturnSpecialDirectories = false,
    };

    public static (long Bytes, DateTime LastModified) Measure(string path, CancellationToken token = default)
    {
        long total = 0;
        var newest = DateTime.MinValue;
        try
        {
            var dir = new DirectoryInfo(path);
            newest = dir.LastWriteTime;
            foreach (var file in dir.EnumerateFiles("*", Options))
            {
                token.ThrowIfCancellationRequested();
                total += file.Length;
                if (file.LastWriteTime > newest) newest = file.LastWriteTime;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* unreadable folders measure what was read */ }
        return (total, newest);
    }

    public static long MeasureEntry(FileSystemInfo info)
    {
        if (info is FileInfo f) return f.Length;
        return Measure(info.FullName).Bytes;
    }

    /// Children of a "contents" location older than the entry's minimum age.
    public static IEnumerable<FileSystemInfo> AgedChildren(string root, int minAgeHours)
    {
        var cutoff = DateTime.Now.AddHours(-minAgeHours);
        IEnumerable<FileSystemInfo> children;
        try
        {
            children = new DirectoryInfo(root).EnumerateFileSystemInfos("*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            }).ToList();
        }
        catch { yield break; }
        foreach (var child in children)
        {
            DateTime modified;
            try { modified = child.LastWriteTime; }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }
            if (minAgeHours <= 0 || modified < cutoff) yield return child;
        }
    }
}
