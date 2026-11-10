using PurgeWin.Utilities;

namespace PurgeWin.Models;

public enum EverythingElseBucket { ManagedInApps, YourFiles, LeftAlone, ManagedByWindows, Unmeasured }

public sealed record EverythingElseFolder(string Id, string Name, string FullPath,
    string Description, string ActionHint, EverythingElseBucket Bucket, long Bytes)
{
    public string? NameEn { get; init; }
    public string? DescriptionEn { get; init; }
    public string? ActionHintEn { get; init; }
    public bool IsDirectory { get; init; } = true;
    public bool IsEstimate { get; init; } = true;
    public string DisplayName => Loc.T(Name, NameEn ?? Name);
    public string ActionAdvice => Loc.T(ActionHint, ActionHintEn ?? ActionHint);
    public string SizeText => (IsEstimate ? "≈ " : "") + ByteFormatter.Format(Bytes);
    public string LocationText => string.IsNullOrEmpty(FullPath) ? Loc.T(Description, DescriptionEn ?? Description) : FullPath;
}
