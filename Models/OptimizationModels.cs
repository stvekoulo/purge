using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PurgeWin.Theme;
using PurgeWin.Utilities;

namespace PurgeWin.Models;

public enum OptimizationSubTab { StartupApps, Maintenance }

public enum StartupSourceKind
{
    CurrentUserRun,
    LocalMachineRun,
    LocalMachineRun32,
    UserStartupFolder,
    CommonStartupFolder,
}

/// <summary>Represents a Windows startup application managed natively via
/// Explorer\StartupApproved (the same mechanism used by Windows Task Manager).</summary>
public sealed partial class StartupAppItem : ObservableObject
{
    public required string Id { get; init; }
    public required string ValueName { get; init; }
    public required string DisplayName { get; init; }
    public required string Publisher { get; init; }
    public required string Command { get; init; }
    public string? ExecutablePath { get; init; }
    public string? IconPath { get; init; }
    public required StartupSourceKind SourceKind { get; init; }
    public required bool CanToggle { get; init; }

    [ObservableProperty] private bool _isEnabled;

    private ImageSource? _cachedIcon;
    public ImageSource? IconSource
    {
        get
        {
            if (_cachedIcon == null && !string.IsNullOrEmpty(IconPath) && File.Exists(IconPath))
            {
                try { _cachedIcon = new BitmapImage(new Uri(IconPath)); } catch { }
            }
            return _cachedIcon;
        }
    }

    public bool HasIcon => !string.IsNullOrEmpty(IconPath) && File.Exists(IconPath);
    public Visibility IconVisibility => HasIcon ? Visibility.Visible : Visibility.Collapsed;
    public Visibility FallbackIconVisibility => HasIcon ? Visibility.Collapsed : Visibility.Visible;

    public string SourceBadgeText => SourceKind switch
    {
        StartupSourceKind.CurrentUserRun => Loc.T("Registre (Utilisateur)", "Registry (User)"),
        StartupSourceKind.UserStartupFolder => Loc.T("Dossier Démarrage", "Startup Folder"),
        StartupSourceKind.CommonStartupFolder => Loc.T("Dossier Système", "System Folder"),
        _ => Loc.T("Registre (Système)", "Registry (System)"),
    };

    public string StatusBadgeText => IsEnabled
        ? Loc.T("Actif au démarrage", "Enabled at login")
        : Loc.T("Désactivé", "Disabled");

    public Brush StatusBgBrush => ThemeBrushes.Get(IsEnabled ? "StatusCheckFill" : "FillSecondary");
    public Brush StatusFgBrush => ThemeBrushes.Get(IsEnabled ? "StatusCheckText" : "TextTertiary");

    public string Subtitle
    {
        get
        {
            var pub = string.IsNullOrWhiteSpace(Publisher)
                ? SourceBadgeText
                : $"{Publisher} · {SourceBadgeText}";
            return string.IsNullOrWhiteSpace(ExecutablePath) ? pub : $"{pub} · {ExecutablePath}";
        }
    }

    public string ToggleTooltip => CanToggle
        ? Loc.T("Activer ou désactiver au démarrage de Windows (réversible, rien n'est supprimé)",
                "Enable or disable at Windows login (reversible; nothing is deleted)")
        : Loc.T("Entrée système globale : relancez Purge en administrateur pour la modifier",
                "System-wide entry: run Purge as administrator to change this toggle");

    partial void OnIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusBadgeText));
        OnPropertyChanged(nameof(StatusBgBrush));
        OnPropertyChanged(nameof(StatusFgBrush));
    }

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(SourceBadgeText));
        OnPropertyChanged(nameof(StatusBadgeText));
        OnPropertyChanged(nameof(StatusBgBrush));
        OnPropertyChanged(nameof(StatusFgBrush));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ToggleTooltip));
    }
}

/// <summary>Safe, non-destructive 1-click system maintenance operation.</summary>
public sealed partial class MaintenanceTaskItem : ObservableObject
{
    public required string Id { get; init; }
    public required string Glyph { get; init; }
    public required string TitleFr { get; init; }
    public required string TitleEn { get; init; }
    public required string DescriptionFr { get; init; }
    public required string DescriptionEn { get; init; }

    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _lastSucceeded = true;
    [ObservableProperty] private string _lastResultFr = "";
    [ObservableProperty] private string _lastResultEn = "";

    public string Title => Loc.T(TitleFr, TitleEn);
    public string Description => Loc.T(DescriptionFr, DescriptionEn);
    public string StatusText => Loc.T(LastResultFr, LastResultEn);
    public Visibility StatusVisibility => string.IsNullOrEmpty(StatusText) ? Visibility.Collapsed : Visibility.Visible;
    public Brush StatusBgBrush => ThemeBrushes.Get(LastSucceeded ? "StatusSafeFill" : "StatusCheckFill");
    public Brush StatusFgBrush => ThemeBrushes.Get(LastSucceeded ? "StatusSafeText" : "StatusCheckText");
    public string RunButtonText => IsRunning
        ? Loc.T("En cours…", "Running…")
        : Loc.T("Exécuter", "Run");
    public bool CanRun => !IsRunning;

    public Brush IconBgBrush => ThemeBrushes.Get("StatusSafeFill");
    public Brush IconFgBrush => ThemeBrushes.Get("StatusSafeText");

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(RunButtonText));
        OnPropertyChanged(nameof(CanRun));
    }

    partial void OnLastSucceededChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusBgBrush));
        OnPropertyChanged(nameof(StatusFgBrush));
    }

    partial void OnLastResultFrChanged(string value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusVisibility));
    }

    partial void OnLastResultEnChanged(string value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusVisibility));
    }

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusVisibility));
        OnPropertyChanged(nameof(StatusBgBrush));
        OnPropertyChanged(nameof(StatusFgBrush));
        OnPropertyChanged(nameof(RunButtonText));
        OnPropertyChanged(nameof(IconBgBrush));
        OnPropertyChanged(nameof(IconFgBrush));
    }
}
