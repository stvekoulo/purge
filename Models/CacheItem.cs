using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PurgeWin.Theme;
using PurgeWin.Utilities;

namespace PurgeWin.Models;

/// Port of CacheItem: one row in App Caches. Duplicate locations for the same
/// catalog entry (several browser profiles, for instance) merge into one row.
public sealed partial class CacheItem : ObservableObject
{
    public required CatalogEntry Entry { get; init; }
    public required IReadOnlyList<string> Paths { get; init; }
    public required long Bytes { get; init; }
    public required DateTime LastModified { get; init; }

    [ObservableProperty] private bool _isSelected;

    public string Id => Entry.Key;
    public string Title => Entry.DisplayName;
    public string Explanation => Entry.Explanation;
    public SafetyLevel Level => Entry.Level;
    public bool IsSafe => Level == SafetyLevel.Safe;
    public string SizeText => ByteFormatter.Format(Bytes);
    public string SafetyText => Level.DisplayName();
    public string PathSummary => Paths.Count == 1 ? Paths[0] : $"{Paths[0]}  (+{Paths.Count - 1})";

    private string? ResolvedIconFile
    {
        get
        {
            if (string.IsNullOrEmpty(Entry.Icon)) return null;
            var file = Path.Combine(AppContext.BaseDirectory, "Assets", "BrandIcons", $"{Entry.Icon}.png");
            return File.Exists(file) ? file : null;
        }
    }

    public bool HasIcon => ResolvedIconFile is not null;

    public ImageSource? IconSource =>
        ResolvedIconFile is { } file ? new BitmapImage(new Uri(file)) : null;

    public Visibility IconVisibility => HasIcon ? Visibility.Visible : Visibility.Collapsed;
    public Visibility FallbackIconVisibility => HasIcon ? Visibility.Collapsed : Visibility.Visible;
    public Brush BadgeBgBrush => ThemeBrushes.Get(IsSafe ? "StatusSafeFill" : "StatusCheckFill");
    public Brush BadgeFgBrush => ThemeBrushes.Get(IsSafe ? "StatusSafeText" : "StatusCheckText");
}

/// What one clean did, for the completion dialog and Cleaning History.
public sealed record CleanOutcome(
    long FreedBytes,
    int MovedCount,
    IReadOnlyList<string> MovedPaths,
    IReadOnlyList<CleanFailure> Failures,
    bool RecycleBinEmptied = false,
    bool RecycleBinEmptyRequested = false);

public sealed record CleanFailure(string Path, string Reason);
