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
            using var first = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var second = new FileStream(keeper, FileMode.Open, FileAccess.Read, FileShare.Read);
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
}
