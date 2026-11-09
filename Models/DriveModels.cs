using CommunityToolkit.Mvvm.ComponentModel;
using PurgeWin.Utilities;

namespace PurgeWin.Models;

/// Represents a mounted local drive volume (e.g. C:\, D:\, E:\) or "All Drives".
public sealed partial class DriveVolumeItem : ObservableObject
{
    public const string AllDrivesKey = "ALL";

    public required string Key { get; init; } // "ALL" or "C:\\", "D:\\", "E:\\"
    public required string Name { get; init; }
    public required string VolumeLabel { get; init; }
    public required long TotalBytes { get; init; }
    public required long FreeBytes { get; init; }
    public required bool IsAllDrives { get; init; }
    public bool IsAvailable { get; init; } = true;
    public double UsedPercent => UsedFraction * 100;
    public string PickerTitle => IsAllDrives ? DisplayTitle : $"{DisplayTitle} · {ByteFormatter.Format(FreeBytes)} {Loc.T("libres", "free")}";

    [ObservableProperty] private bool _isActive;

    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);
    public double UsedFraction => TotalBytes > 0 ? Math.Clamp((double)UsedBytes / TotalBytes, 0, 1) : 0;

    public string DisplayTitle => IsAllDrives
        ? Loc.T("Tous les disques", "All drives")
        : string.IsNullOrWhiteSpace(VolumeLabel)
            ? Name.TrimEnd('\\')
            : $"{Name.TrimEnd('\\')} ({VolumeLabel})";

    public string CapacitySummary =>
        Loc.T(
            $"{ByteFormatter.Format(FreeBytes)} libres sur {ByteFormatter.Format(TotalBytes)}",
            $"{ByteFormatter.Format(FreeBytes)} free of {ByteFormatter.Format(TotalBytes)}");
}
