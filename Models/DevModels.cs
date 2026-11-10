using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PurgeWin.Theme;
using PurgeWin.Utilities;
using PurgeWin.Services;

namespace PurgeWin.Models;

public enum DevSectionKind { GlobalCache, AndroidAvd, ProjectArtifact, OrphanWorktree }

/// One row in Dev Tools (global cache, Android emulator, project artifact, or
/// orphaned AI worktree). Represents a developer cache, build artifact, Android AVD, or AI worktree.
public sealed partial class DevItem : ObservableObject
{
    public required string Id { get; init; }
    private string _titleFr = "";
    private string _explanationFr = "";
    public required string Title { get => Loc.T(_titleFr, TitleEn ?? _titleFr); init => _titleFr = value; }
    public string? TitleEn { get; init; }
    public required string Subtitle { get; init; }
    public required string Explanation { get => Loc.T(_explanationFr, ExplanationEn ?? _explanationFr); init => _explanationFr = value; }
    public string? ExplanationEn { get; init; }
    public required IReadOnlyList<string> Paths { get; init; }
    public required long Bytes { get; init; }
    public required DateTime LastModified { get; init; }
    public required SafetyLevel Level { get; init; }
    public required DevSectionKind Section { get; init; }
    public string? Icon { get; init; }
    public string? ProjectRoot { get; init; }
    public string? ReinstallHint { get; init; }
    public GitWorktreeState GitState { get; init; } = GitWorktreeState.Clean;

    [ObservableProperty] private bool _isSelected;

    public bool IsSafe => Level == SafetyLevel.Safe;
    public bool CanSelect => Section != DevSectionKind.OrphanWorktree || GitState == GitWorktreeState.Clean;
    public string SizeText => ByteFormatter.Format(Bytes);
    public string SafetyText => GitState switch
    {
        GitWorktreeState.Registered => Loc.T("Worktree Git actif · protégé", "Registered Git worktree · protected"),
        GitWorktreeState.Dirty => Loc.T("Modifications Git · protégé", "Git changes · protected"),
        GitWorktreeState.Unknown => Loc.T("État Git inconnu · protégé", "Unknown Git state · protected"),
        _ => Level.DisplayName(),
    };

    public string SectionBadge => Section switch
    {
        DevSectionKind.GlobalCache => Loc.T("Cache global", "Global cache"),
        DevSectionKind.AndroidAvd => Loc.T("Émulateur Android", "Android Emulator"),
        DevSectionKind.ProjectArtifact => Loc.T("Projet de dev", "Dev project"),
        DevSectionKind.OrphanWorktree => Loc.T("Worktree IA à vérifier", "AI worktree to review"),
        _ => Loc.T("Outil de dev", "Dev tool"),
    };
    public string RevealHint => Loc.T("Afficher dans l'Explorateur", "Show in File Explorer");
    public string ExcludeHint => Loc.T("Exclure des analyses", "Exclude from scans");

    private string? ResolvedIconFile => Services.BrandIconResolver.Resolve(Icon);

    public bool HasIcon => ResolvedIconFile is not null;
    public ImageSource? IconSource => ResolvedIconFile is { } f ? new BitmapImage(new Uri(f)) : null;
    public Visibility IconVisibility => HasIcon ? Visibility.Visible : Visibility.Collapsed;
    public Visibility FallbackIconVisibility => HasIcon ? Visibility.Collapsed : Visibility.Visible;
    public Brush BadgeBgBrush => ThemeBrushes.Get(!CanSelect ? "StatusDangerFill" : IsSafe ? "StatusSafeFill" : "StatusCheckFill");
    public Brush BadgeFgBrush => ThemeBrushes.Get(!CanSelect ? "StatusDangerText" : IsSafe ? "StatusSafeText" : "StatusCheckText");
    public string PrimaryPath => Paths.Count > 0 ? Paths[0] : "";

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Explanation));
        OnPropertyChanged(nameof(SafetyText));
        OnPropertyChanged(nameof(SectionBadge));
        OnPropertyChanged(nameof(RevealHint));
        OnPropertyChanged(nameof(ExcludeHint));
        OnPropertyChanged(nameof(SizeText));
    }
}
