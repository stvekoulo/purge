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
    private string _nameFr = "";
    private string _folderFr = "";
    private string? _specialLabelFr;
    private string? _specialExplanationFr;
    public required string Name { get => Loc.T(_nameFr, NameEn ?? _nameFr); init => _nameFr = value; }
    public string NameFr => _nameFr;
    public string? NameEn { get; init; }
    public required string FolderDisplay { get => Loc.T(_folderFr, FolderDisplayEn ?? _folderFr); init => _folderFr = value; }
    public string FolderDisplayFr => _folderFr;
    public string? FolderDisplayEn { get; init; }
    public required long Bytes { get; init; }
    public required DateTime LastModified { get; init; }
    public required LargeFileCategory Category { get; init; }
    public string? SpecialLabel { get => _specialLabelFr is null ? null : Loc.T(_specialLabelFr, SpecialLabelEn ?? _specialLabelFr); init => _specialLabelFr = value; }
    public string? SpecialLabelFr => _specialLabelFr;
    public string? SpecialLabelEn { get; init; }
    public string? SpecialExplanation { get => _specialExplanationFr is null ? null : Loc.T(_specialExplanationFr, SpecialExplanationEn ?? _specialExplanationFr); init => _specialExplanationFr = value; }
    public string? SpecialExplanationFr => _specialExplanationFr;
    public string? SpecialExplanationEn { get; init; }
    public bool IsOneDriveSynced { get; init; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string? _duplicateGroupId;
    [ObservableProperty] private bool _isDuplicateKeeper;
    [ObservableProperty] private int _duplicateGroupCount;

    partial void OnDuplicateGroupIdChanged(string? value) => RefreshDuplicateBadge();
    partial void OnIsDuplicateKeeperChanged(bool value) => RefreshDuplicateBadge();
    partial void OnDuplicateGroupCountChanged(int value) => RefreshDuplicateBadge();

    private void RefreshDuplicateBadge()
    {
        OnPropertyChanged(nameof(IsDuplicate));
        OnPropertyChanged(nameof(BadgeText));
        OnPropertyChanged(nameof(BadgeBgBrush));
        OnPropertyChanged(nameof(BadgeFgBrush));
    }

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

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(FolderDisplay));
        OnPropertyChanged(nameof(SpecialLabel));
        OnPropertyChanged(nameof(SpecialExplanation));
        OnPropertyChanged(nameof(AgeText));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(BadgeText));
        OnPropertyChanged(nameof(SizeText));
    }

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
