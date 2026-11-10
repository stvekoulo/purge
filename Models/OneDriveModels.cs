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
        OnPropertyChanged(nameof(BadgeBgBrush));
        OnPropertyChanged(nameof(BadgeFgBrush));
        OnPropertyChanged(nameof(EvictButtonVisibility));
        OnPropertyChanged(nameof(SizeNoteText));
    }

    public bool IsLocalOnDisk => SyncState is OneDriveSyncState.Local or OneDriveSyncState.Pinned;
    public bool CanEvictLocalCopy => IsLocalOnDisk;
    public Visibility EvictButtonVisibility => CanEvictLocalCopy ? Visibility.Visible : Visibility.Collapsed;

    public string SizeText => ByteFormatter.Format(Bytes);
    public string SizeNoteText => SyncState == OneDriveSyncState.CloudOnly
        ? Loc.T("0 o sur ce PC", "0 B on PC")
        : Loc.T("Sur le disque", "On disk");
    public string AgeText => Loc.T("Modifié ", "Modified ") + RelativeDate.Format(LastModified);
    public string Subtitle => $"{FolderDisplay} · {AgeText}";
    public string EvictButtonLabel => Loc.T("Libérer l'espace", "Free up space");

    public string BadgeText => SyncState switch
    {
        OneDriveSyncState.CloudOnly => Loc.T("En ligne uniquement", "Online-only"),
        OneDriveSyncState.Pinned => Loc.T("Toujours gardé sur ce PC", "Always kept on PC"),
        _ => Loc.T("Sur ce PC (Local)", "On this PC (Local)"),
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
        _ => "\uE753",
    };

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(FolderDisplay));
        OnPropertyChanged(nameof(AgeText));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(BadgeText));
        OnPropertyChanged(nameof(BadgeBgBrush));
        OnPropertyChanged(nameof(BadgeFgBrush));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(SizeNoteText));
        OnPropertyChanged(nameof(EvictButtonLabel));
    }
}
