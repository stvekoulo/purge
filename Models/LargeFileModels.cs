using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PurgeWin.Theme;
using PurgeWin.Utilities;

namespace PurgeWin.Models;

public enum LargeFileCategory
{
    All,
    Video,
    Audio,
    Image,
    Archive,
    Document,
    DiskImage,
    VirtualMachine,
    AiModel,
    Duplicate,
    OneDrive,
    Other,
}

/// Purge for Windows: a personal file, VM bundle, disk image, unfinished
/// download, or local AI model weight found by Large Files.
public sealed partial class LargeFileItem : ObservableObject
{
    public required string FullPath { get; init; }
    public required string Name { get; init; }
    public required string FolderDisplay { get; init; }
    public required long Bytes { get; init; }
    public required DateTime LastModified { get; init; }
    public required LargeFileCategory Category { get; init; }
    public string? SpecialLabel { get; init; }
    public string? SpecialExplanation { get; init; }
    public bool IsOneDriveSynced { get; init; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string? _duplicateGroupId;
    [ObservableProperty] private bool _isDuplicateKeeper;
    [ObservableProperty] private int _duplicateGroupCount;

    public bool IsDuplicate => !string.IsNullOrEmpty(DuplicateGroupId);
    public string SizeText => ByteFormatter.Format(Bytes);
    public string AgeText => Loc.T("Modifié ", "Modified ") + RelativeDate.Format(LastModified);
    public string Subtitle => $"{FolderDisplay} · {AgeText}";

    public string BadgeText
    {
        get
        {
            if (IsDuplicate)
                return IsDuplicateKeeper
                    ? Loc.T("Doublon · À garder", "Duplicate · Keep")
                    : Loc.T($"Doublon ({DuplicateGroupCount} copies)", $"Duplicate ({DuplicateGroupCount} copies)");
            if (IsOneDriveSynced) return Loc.T("Synchronisé OneDrive", "Synced in OneDrive");
            if (!string.IsNullOrEmpty(SpecialLabel)) return SpecialLabel!;
            return CategoryDisplay(Category);
        }
    }

    public Brush BadgeBgBrush => ThemeBrushes.Get(
        IsDuplicate && !IsDuplicateKeeper ? "StatusCheckFill" :
        IsOneDriveSynced ? "StatusSafeFill" : "FillSecondary");

    public Brush BadgeFgBrush => ThemeBrushes.Get(
        IsDuplicate && !IsDuplicateKeeper ? "StatusCheckText" :
        IsOneDriveSynced ? "StatusSafeText" : "TextSecondary");

    public Visibility CloudActionVisibility => IsOneDriveSynced ? Visibility.Visible : Visibility.Collapsed;

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

    public static string CategoryDisplay(LargeFileCategory cat) => cat switch
    {
        LargeFileCategory.Video => Loc.T("Vidéo", "Video"),
        LargeFileCategory.Audio => "Audio",
        LargeFileCategory.Image => "Image",
        LargeFileCategory.Archive => "Archive",
        LargeFileCategory.Document => "Document",
        LargeFileCategory.DiskImage => Loc.T("Image disque", "Disk image"),
        LargeFileCategory.VirtualMachine => Loc.T("Machine virtuelle", "Virtual machine"),
        LargeFileCategory.AiModel => Loc.T("Modèle IA", "AI model"),
        LargeFileCategory.Duplicate => Loc.T("Doublon", "Duplicate"),
        LargeFileCategory.OneDrive => "OneDrive",
        _ => Loc.T("Autre", "Other"),
    };
}
