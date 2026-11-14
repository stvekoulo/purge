using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PurgeWin.Theme;
using PurgeWin.Utilities;

namespace PurgeWin.Models;

public enum UninstallerSubTab { InstalledApps, Leftovers, CliTools }
public enum AppSortOption { Largest, Smallest, NameAZ }

/// One associated leftover folder for an installed app (AppData\Local,
/// AppData\Roaming, ProgramData, Temp). Exact matches arrive checked; looser
/// name-only matches arrive unchecked for the user to confirm.
public sealed partial class AppLeftoverItem : ObservableObject
{
    public required string Path { get; init; }
    public required string ScopeLabel { get; init; }
    public required long Bytes { get; init; }
    public required bool IsExactMatch { get; init; }

    [ObservableProperty] private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(CardBorderBrush));
    }

    public Brush CardBorderBrush => IsSelected
        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 124, 58, 237))
        : ThemeBrushes.Get("Hairline");

    public string SizeText => ByteFormatter.Format(Bytes);
    public string MatchBadge => IsExactMatch
        ? Loc.T("Correspondance exacte", "Exact match")
        : Loc.T("À vérifier", "Check first");

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(MatchBadge));
        OnPropertyChanged(nameof(SizeText));
    }
}

/// Purge for Windows:.
public sealed partial class InstalledAppItem : ObservableObject
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string Publisher { get; init; }
    public required string DisplayVersion { get; init; }
    public string? InstallLocation { get; init; }
    public string? UninstallString { get; init; }
    public string? QuietUninstallString { get; init; }
    public bool IsWindowsInstaller { get; init; }
    public string? DisplayIcon { get; init; }
    public string? IconPath { get; set; }
    private ImageSource? _cachedIconSource;
    public ImageSource? IconSource
    {
        get
        {
            if (_cachedIconSource == null && !string.IsNullOrEmpty(IconPath) && System.IO.File.Exists(IconPath))
            {
                try { _cachedIconSource = new BitmapImage(new Uri(IconPath)); } catch { }
            }
            return _cachedIconSource;
        }
    }
    public bool HasIcon => !string.IsNullOrEmpty(IconPath) && System.IO.File.Exists(IconPath);
    public Visibility IconVisibility => HasIcon ? Visibility.Visible : Visibility.Collapsed;
    public Visibility FallbackIconVisibility => HasIcon ? Visibility.Collapsed : Visibility.Visible;
    public bool CanUninstall { get; init; } = true;
    public string UninstallHint => CanUninstall ? Loc.T("Ouvrir le désinstalleur de l'application", "Open the application's uninstaller")
        : Loc.T("Choisissez le disque d'installation, ou Tous les disques, pour désinstaller l'application.",
            "Choose the installation drive, or All drives, to uninstall this application.");
    public string UninstallActionText => Loc.T("Désinstaller", "Uninstall");
    public required long AppBytes { get; init; }
    public required List<AppLeftoverItem> Leftovers { get; init; }

    [ObservableProperty] private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(CardBorderBrush));
    }

    public Brush CardBorderBrush => IsSelected
        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 124, 58, 237))
        : ThemeBrushes.Get("Hairline");

    public long LeftoverBytes => Leftovers.Sum(l => l.Bytes);
    public long TotalBytes => AppBytes + LeftoverBytes;
    public string TotalSizeText => ByteFormatter.Format(TotalBytes);

    public string Subtitle
    {
        get
        {
            var pub = string.IsNullOrWhiteSpace(Publisher)
                ? Loc.T("Application installée", "Installed app")
                : Publisher;
            return Leftovers.Count > 0
                ? Loc.T(
                    $"{pub} · {Leftovers.Count} dossier{(Leftovers.Count > 1 ? "s" : "")} de données ({ByteFormatter.Format(LeftoverBytes)})",
                    $"{pub} · {Leftovers.Count} data folder{(Leftovers.Count > 1 ? "s" : "")} ({ByteFormatter.Format(LeftoverBytes)})")
                : pub;
        }
    }

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(UninstallHint));
        OnPropertyChanged(nameof(UninstallActionText));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(TotalSizeText));
        foreach (var leftover in Leftovers) leftover.RefreshLocalizedText();
    }
}

/// Port of OrphanLeftoverScanner findings: folders in AppData\Local or
/// AppData\Roaming that no longer match any installed app.
public sealed partial class OrphanLeftoverItem : ObservableObject
{
    public required string FolderName { get; init; }
    public required string FullPath { get; init; }
    public required string LocationKind { get; init; }
    public required long Bytes { get; init; }
    public required DateTime LastModified { get; init; }

    [ObservableProperty] private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(CardBorderBrush));
    }

    public Brush CardBorderBrush => IsSelected
        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 124, 58, 237))
        : ThemeBrushes.Get("Hairline");

    public string SizeText => ByteFormatter.Format(Bytes);
    public string Subtitle => $"{LocationKind} · {Loc.T("Modifié", "Modified")} {RelativeDate.Format(LastModified)} · {FullPath}";
    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(SizeText));
    }
    public Brush BadgeBgBrush => ThemeBrushes.Get("StatusCheckFill");
    public Brush BadgeFgBrush => ThemeBrushes.Get("StatusCheckText");
}

/// Purge for Windows:.
public sealed partial class CliToolItem : ObservableObject
{
    public required string Name { get; init; }
    public required string Manager { get; init; }
    public required string Path { get; init; }
    public required long Bytes { get; init; }
    public string? UninstallCommand { get; init; }

    [ObservableProperty] private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(CardBorderBrush));
    }

    public Brush CardBorderBrush => IsSelected
        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 124, 58, 237))
        : ThemeBrushes.Get("Hairline");

    public string SizeText => ByteFormatter.Format(Bytes);
    public string Subtitle => $"{Manager} · {Path}";
    public void RefreshLocalizedText() => OnPropertyChanged(nameof(SizeText));
}
