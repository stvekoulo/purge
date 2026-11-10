namespace PurgeWin.Services;

/// Port of FolderSizing: logical bytes and newest change under a folder,
/// skipping reparse points so a junction can never inflate or redirect a size.
public static class FolderSizing
{
    private static readonly EnumerationOptions FlatOptions = new()
    {
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false,
    };

    public static (long Bytes, DateTime LastModified) Measure(string path, CancellationToken token = default)
    {
        long total = 0;
        var newest = DateTime.MinValue;
        try
        {
            var root = new DirectoryInfo(path);
            if (!root.Exists) return (0, newest);
            newest = root.LastWriteTime;
            var stack = new Stack<DirectoryInfo>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var current = stack.Pop();
                IEnumerable<FileSystemInfo> entries;
                try { entries = current.EnumerateFileSystemInfos("*", FlatOptions); }
                catch { continue; }
                foreach (var entry in entries)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        if (PathExpander.IsReparseLink(entry)) continue;
                        if (entry is FileInfo file)
                        {
                            total += file.Length;
                            if (file.LastWriteTime > newest) newest = file.LastWriteTime;
                        }
                        else if (entry is DirectoryInfo sub) stack.Push(sub);
                    }
                    catch { }
                }
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
            }).ToList();
        }
        catch { yield break; }
        foreach (var child in children)
        {
            if (PathExpander.IsReparseLink(child)) continue;
            DateTime modified;
            try { modified = child.LastWriteTime; }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }
            if (minAgeHours <= 0 || modified < cutoff) yield return child;
        }
    }
}
