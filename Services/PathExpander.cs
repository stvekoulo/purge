namespace PurgeWin.Services;

public static class PathExpander
{
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

    public static bool IsReparsePoint(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch { return true; } // unreadable counts as unsafe
    }

    private static IEnumerable<string> SafeDirectories(string root, string pattern)
    {
        try
        {
            return Directory.EnumerateDirectories(root, pattern, new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            }).ToList();
        }
        catch { return []; }
    }
}
