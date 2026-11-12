using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PurgeWin.Theme;
using PurgeWin.Utilities;

namespace PurgeWin.Models;

public enum OneDriveSyncState
{
    Local,
    CloudOnly,
    Pinned,
}

public enum OneDriveFilter
{
    All,
    Local,
    CloudOnly,
    Pinned,
}

public sealed partial class OneDriveFileItem : ObservableObject
{
    public required string FullPath { get; init; }
    public required string Name { get; init; }
    private string _folderFr = "";
    public required string FolderDisplay { get => Loc.T(_folderFr, FolderDisplayEn ?? _folderFr); init => _folderFr = value; }
    public string? FolderDisplayEn { get; init; }
    public required long Bytes { get; init; }
    public required DateTime LastModified { get; init; }
    public required LargeFileCategory Category { get; init; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private OneDriveSyncState _syncState;

    partial void OnSyncStateChanged(OneDriveSyncState value)
    {
        OnPropertyChanged(nameof(IsLocalOnDisk));
        OnPropertyChanged(nameof(CanEvictLocalCopy));
        OnPropertyChanged(nameof(BadgeText));
        OnPropertyChanged(nameof(BadgeGlyph));
        OnPropertyChanged(nameof(BadgeBgBrush));
        OnPropertyChanged(nameof(BadgeFgBrush));
        OnPropertyChanged(nameof(IconBgBrush));
        OnPropertyChanged(nameof(IconFgBrush));
        OnPropertyChanged(nameof(EvictButtonVisibility));
        OnPropertyChanged(nameof(SizeNoteText));
        OnPropertyChanged(nameof(SizeFgBrush));
    }

    public bool IsLocalOnDisk => SyncState is OneDriveSyncState.Local or OneDriveSyncState.Pinned;
    public bool CanEvictLocalCopy => IsLocalOnDisk;
    public Visibility EvictButtonVisibility => CanEvictLocalCopy ? Visibility.Visible : Visibility.Collapsed;

    public string SizeText => ByteFormatter.Format(Bytes);
    public string SizeNoteText => SyncState == OneDriveSyncState.CloudOnly
        ? Loc.T("Cloud · 0 o local", "Cloud · 0 B local")
        : Loc.T("Stocké sur ce PC", "Stored on PC");
    public Brush SizeFgBrush => ThemeBrushes.Get(SyncState == OneDriveSyncState.CloudOnly ? "TextSecondary" : "TextPrimary");

    public string AgeText => Loc.T("Modifié ", "Modified ") + RelativeDate.Format(LastModified);
    public string Subtitle => $"{FolderDisplay} · {AgeText}";
    public string EvictButtonLabel => Loc.T("Libérer", "Free up");
    public string EvictTooltip => Loc.T(
        "Passer en ligne uniquement (libère l'espace sur ce PC sans supprimer de OneDrive)",
        "Make online-only (frees disk space on this PC without deleting from OneDrive)");
    public string RevealTooltip => Loc.T("Afficher dans l'Explorateur Windows", "Show in File Explorer");

    public string BadgeText => SyncState switch
    {
        OneDriveSyncState.CloudOnly => Loc.T("En ligne", "Online-only"),
        OneDriveSyncState.Pinned => Loc.T("Épinglé", "Pinned"),
        _ => Loc.T("Sur ce PC", "On this PC"),
    };

    public string BadgeGlyph => SyncState switch
    {
        OneDriveSyncState.CloudOnly => "\uE753",
        OneDriveSyncState.Pinned => "\uE718",
        _ => "\uEDA2",
    };

    public Brush BadgeBgBrush => ThemeBrushes.Get(SyncState switch
    {
        OneDriveSyncState.CloudOnly => "FillSecondary",
        OneDriveSyncState.Pinned => "StatusCheckFill",
        _ => "StatusSafeFill",
    });

    public Brush BadgeFgBrush => ThemeBrushes.Get(SyncState switch
    {
        OneDriveSyncState.CloudOnly => "TextSecondary",
        OneDriveSyncState.Pinned => "StatusCheckText",
        _ => "StatusSafeText",
    });

    public Brush IconBgBrush => ThemeBrushes.Get(SyncState switch
    {
        OneDriveSyncState.Local => "StatusSafeFill",
        OneDriveSyncState.Pinned => "StatusCheckFill",
        _ => "FillSecondary",
    });

    public Brush IconFgBrush => ThemeBrushes.Get(SyncState switch
    {
        OneDriveSyncState.Local => "StatusSafeText",
        OneDriveSyncState.Pinned => "StatusCheckText",
        _ => "TextSecondary",
    });

    public string Glyph => Category switch
    {
        LargeFileCategory.Video => "\uE714",
        LargeFileCategory.Audio => "\uE8D6",
        LargeFileCategory.Image => "\uEB9F",
        LargeFileCategory.Archive => "\uF012",
        LargeFileCategory.Document => "\uE8A5",
        LargeFileCategory.DiskImage => "\uEDA2",
        LargeFileCategory.VirtualMachine => "\uE7F4",
        LargeFileCategory.AiModel => "\uE945",
        _ => "\uE7C3",
    };

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(FolderDisplay));
        OnPropertyChanged(nameof(AgeText));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(BadgeText));
        OnPropertyChanged(nameof(BadgeGlyph));
        OnPropertyChanged(nameof(BadgeBgBrush));
        OnPropertyChanged(nameof(BadgeFgBrush));
        OnPropertyChanged(nameof(IconBgBrush));
        OnPropertyChanged(nameof(IconFgBrush));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(SizeNoteText));
        OnPropertyChanged(nameof(SizeFgBrush));
        OnPropertyChanged(nameof(EvictButtonLabel));
        OnPropertyChanged(nameof(EvictTooltip));
        OnPropertyChanged(nameof(RevealTooltip));
    }
}
