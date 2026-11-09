using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PurgeWin.Theme;
using PurgeWin.Utilities;

namespace PurgeWin.Models;

public enum DevSectionKind { GlobalCache, AndroidAvd, ProjectArtifact, OrphanWorktree }

/// One row in Dev Tools (global cache, Android emulator, project artifact, or
/// orphaned AI worktree). Represents a developer cache, build artifact, Android AVD, or AI worktree.
public sealed partial class DevItem : ObservableObject
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string Explanation { get; init; }
    public required IReadOnlyList<string> Paths { get; init; }
    public required long Bytes { get; init; }
    public required DateTime LastModified { get; init; }
    public required SafetyLevel Level { get; init; }
    public required DevSectionKind Section { get; init; }
    public string? Icon { get; init; }
    public string? ProjectRoot { get; init; }
    public string? ReinstallHint { get; init; }
    public bool HasUncommittedGit { get; init; }

    [ObservableProperty] private bool _isSelected;

    public bool IsSafe => Level == SafetyLevel.Safe;
    public string SizeText => ByteFormatter.Format(Bytes);
    public string SafetyText => HasUncommittedGit ? Loc.T("Modifs Git", "Git dirty") : Level.DisplayName();

    public string SectionBadge => Section switch
    {
        DevSectionKind.GlobalCache => Loc.T("Cache global", "Global cache"),
        DevSectionKind.AndroidAvd => Loc.T("Émulateur Android", "Android Emulator"),
        DevSectionKind.ProjectArtifact => Loc.T("Projet de dev", "Dev project"),
        DevSectionKind.OrphanWorktree => Loc.T("Worktree Git orphelin", "Orphan Git worktree"),
        _ => Loc.T("Outil de dev", "Dev tool"),
    };

    private string? ResolvedIconFile => Services.BrandIconResolver.Resolve(Icon);

    public bool HasIcon => ResolvedIconFile is not null;
    public ImageSource? IconSource => ResolvedIconFile is { } f ? new BitmapImage(new Uri(f)) : null;
    public Visibility IconVisibility => HasIcon ? Visibility.Visible : Visibility.Collapsed;
    public Visibility FallbackIconVisibility => HasIcon ? Visibility.Collapsed : Visibility.Visible;
    public Brush BadgeBgBrush => ThemeBrushes.Get(HasUncommittedGit ? "StatusDangerFill" : IsSafe ? "StatusSafeFill" : "StatusCheckFill");
    public Brush BadgeFgBrush => ThemeBrushes.Get(HasUncommittedGit ? "StatusDangerText" : IsSafe ? "StatusSafeText" : "StatusCheckText");
    public string PrimaryPath => Paths.Count > 0 ? Paths[0] : "";
}
