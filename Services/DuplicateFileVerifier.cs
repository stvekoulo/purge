namespace PurgeWin.Services;

/// <summary>Recheck duplicate candidates immediately before recycling one.
/// A failed comparison is never permission to delete.</summary>
public static class DuplicateFileVerifier
{
    public static bool AreIdentical(string candidate, string keeper)
    {
        try
        {
            if (PathScope.Normalize(candidate)?.Equals(PathScope.Normalize(keeper), StringComparison.OrdinalIgnoreCase) != false)
                return false;
            using var first = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var second = new FileStream(keeper, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (first.Length != second.Length) return false;

            var firstBuffer = new byte[65536];
            var secondBuffer = new byte[65536];
            int read;
            while ((read = first.Read(firstBuffer, 0, firstBuffer.Length)) != 0)
            {
                second.ReadExactly(secondBuffer.AsSpan(0, read));
                if (!firstBuffer.AsSpan(0, read).SequenceEqual(secondBuffer.AsSpan(0, read))) return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>Builds the map of selected duplicate paths to an unselected keeper path
    /// when at least one unselected member remains in the duplicate group on disk.</summary>
    public static Dictionary<string, string> ResolveKeepers(
        IEnumerable<(string Path, string? GroupId, bool IsSelected)> items)
    {
        var all = items.Where(i => !string.IsNullOrEmpty(i.GroupId) && File.Exists(i.Path)).ToList();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in all.GroupBy(i => i.GroupId!, StringComparer.OrdinalIgnoreCase))
        {
            var members = group.ToList();
            if (members.Count < 2) continue;
            var unselected = members.FirstOrDefault(m => !m.IsSelected);
            if (unselected.Path is null) continue;
            foreach (var selected in members.Where(m => m.IsSelected))
                result[selected.Path] = unselected.Path;
        }
        return result;
    }
}
