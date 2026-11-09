using PurgeWin.Utilities;

namespace PurgeWin.Models;

public enum EverythingElseBucket { ManagedInApps, YourFiles, LeftAlone, ManagedByWindows, Unmeasured }

public sealed record EverythingElseFolder(string Id, string Name, string FullPath,
    string Description, string ActionHint, EverythingElseBucket Bucket, long Bytes)
{
    public bool IsDirectory { get; init; } = true;
    public bool IsEstimate { get; init; } = true;
    public string DisplayName => Name;
    public string ActionAdvice => ActionHint;
    public string SizeText => (IsEstimate ? "≈ " : "") + ByteFormatter.Format(Bytes);
    public string LocationText => string.IsNullOrEmpty(FullPath) ? Description : FullPath;
}
