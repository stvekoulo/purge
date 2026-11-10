using System.Runtime.InteropServices;

namespace PurgeWin.Services;

public static class PathExpander
{
    private const uint IO_REPARSE_TAG_SURROGATE_BIT = 0x20000000u;
    private const uint IO_REPARSE_TAG_CLOUD_MASK = 0xFFFF0FFFu;
    private const uint IO_REPARSE_TAG_CLOUD = 0x9000001Au;
    private const uint IO_REPARSE_TAG_WOF = 0x80000017u;
    private const uint IO_REPARSE_TAG_DEDUP = 0x80000013u;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATAW
    {
        public FileAttributes dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    [DllImport("kernel32.dll", EntryPoint = "FindFirstFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindFirstFileW(string lpFileName, out WIN32_FIND_DATAW lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FindClose(IntPtr hFindFile);

    public static IEnumerable<string> Expand(string template)
    {
        var expanded = Environment.ExpandEnvironmentVariables(template);
        if (expanded.Contains('%')) yield break; // unknown variable: never guess
        var parts = expanded.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) yield break;
        var roots = new List<string> { parts[0] + "\\" };
        foreach (var part in parts.Skip(1))
        {
            var next = new List<string>();
            foreach (var root in roots)
            {
                if (part.Contains('*'))
                {
                    foreach (var dir in SafeDirectories(root, part)) next.Add(dir);
                }
                else
                {
                    var candidate = Path.Combine(root, part);
                    if (Directory.Exists(candidate) && !IsReparsePoint(candidate)) next.Add(candidate);
                }
            }
            roots = next;
            if (roots.Count == 0) yield break;
        }
        foreach (var r in roots) yield return Path.GetFullPath(r);
    }

    /// <summary>Returns true if the reparse tag represents an in-place OS filter
    /// (OneDrive / Cloud Files, WOF compression, or Data Deduplication) rather than
    /// a link or mount point (symlink, junction, volume mount, AppExecLink).</summary>
    public static bool IsBenignReparseTag(uint tag) =>
        (tag & IO_REPARSE_TAG_SURROGATE_BIT) == 0 && (
            (tag & IO_REPARSE_TAG_CLOUD_MASK) == IO_REPARSE_TAG_CLOUD ||
            tag == IO_REPARSE_TAG_WOF ||
            tag == IO_REPARSE_TAG_DEDUP);

    public static bool IsReparseLink(FileSystemInfo info) =>
        (info.Attributes & FileAttributes.ReparsePoint) != 0 && IsReparsePoint(info.FullName);

    public static bool IsReparsePoint(string path)
    {
        try
        {
            var attrs = File.GetAttributes(path);
            if ((attrs & FileAttributes.ReparsePoint) == 0) return false;
            var trimmed = path.TrimEnd('\\', '/');
            if (string.IsNullOrEmpty(trimmed) || trimmed.EndsWith(':')) return false;
            var handle = FindFirstFileW(trimmed, out var data);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1)) return true;
            try { return !IsBenignReparseTag(data.dwReserved0); }
            finally { FindClose(handle); }
        }
        catch { return true; } // unreadable counts as unsafe
    }

    /// <summary>Reject paths whose ancestor passes through a junction or
    /// symlink, even if the final file itself is not a reparse point.</summary>
    public static bool HasReparsePointAncestor(string path)
    {
        var normalized = PathScope.Normalize(path);
        if (normalized is null) return true;
        for (var ancestor = Path.GetDirectoryName(normalized); !string.IsNullOrEmpty(ancestor);
             ancestor = Path.GetDirectoryName(ancestor))
        {
            if (ancestor.Equals(Path.GetPathRoot(ancestor), StringComparison.OrdinalIgnoreCase)) break;
            if (IsReparsePoint(ancestor)) return true;
        }
        return false;
    }

    private static IEnumerable<string> SafeDirectories(string root, string pattern)
    {
        try
        {
            return Directory.EnumerateDirectories(root, pattern, new EnumerationOptions
            {
                IgnoreInaccessible = true,
            }).Where(d => !IsReparsePoint(d)).ToList();
        }
        catch { return []; }
    }
}
