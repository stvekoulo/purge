namespace PurgeWin.Services;

/// Immutable disk/exclusion boundaries captured by each scan and cleanup.
public sealed class PathScope
{
    public IReadOnlyList<string> Roots { get; }
    public IReadOnlyList<string> Excluded { get; }

    public PathScope(IEnumerable<string> roots, IEnumerable<string>? excluded = null)
    {
        Roots = roots.Select(Normalize).Where(p => p != null).Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        Excluded = (excluded ?? []).Select(Normalize).Where(p => p != null).Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return null;
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }

    public static bool IsWithin(string? path, string? parent)
    {
        string? full = Normalize(path), root = Normalize(parent);
        return full != null && root != null && (full.Equals(root, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));
    }

    public bool Contains(string? path) => Roots.Any(root => IsWithin(path, root));
    public bool Allows(string? path) => Contains(path) && !Excluded.Any(root => IsWithin(path, root));
    public bool CanDelete(string? path) => Allows(path) && !Excluded.Any(child => IsWithin(child, path));

    public static IReadOnlyList<string> AvailableRoots()
    {
        var roots = new List<string>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try { if (drive.IsReady && drive.DriveType is DriveType.Fixed or DriveType.Removable) roots.Add(drive.RootDirectory.FullName); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return roots;
    }
}
