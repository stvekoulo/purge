using PurgeWin.Models;
using PurgeWin.Services;
using PurgeWin.Theme;
using PurgeWin.Utilities;

namespace PurgeWin.ViewModels;

/// Multi-drive discovery, active drive switching, language & theme switching, and path exclusions.
public sealed partial class PurgeStore
{
    public IReadOnlyList<string> ActiveDriveRoots
    {
        get
        {
            var realDrives = Drives.Where(d => !d.IsAllDrives).Select(d => d.Key).ToList();
            if (Settings.SelectedDrive == DriveVolumeItem.AllDrivesKey
                || !realDrives.Contains(Settings.SelectedDrive, StringComparer.OrdinalIgnoreCase))
            {
                return realDrives;
            }
            return [Settings.SelectedDrive];
        }
    }

    public void SetLanguage(string langCode)
    {
        Settings.Language = langCode;
        SaveSettings();
        Loc.SetLanguage(langCode);
        RefreshDrives();
        RefreshAllFilteredViews();
        NotifyTotals();
    }

    public void SetAppearance(string appearance)
    {
        Settings.Appearance = appearance;
        SaveSettings();
        ThemeBrushes.ApplyAppearance(appearance);
        RefreshAllFilteredViews();
    }

    public void RefreshAllFilteredViews()
    {
        OnUi(() =>
        {
            ApplyFilter();
            ApplyDevFilter();
            ApplyLargeFilter();
            ApplyUninstallFilter();
        });
    }

    public void SetDrive(string driveKey)
    {
        Settings.SelectedDrive = driveKey;
        SaveSettings();
        RefreshDisk();
    }

    public void RefreshDrives() => RefreshDisk();

    public void RefreshDisk()
    {
        var readyDrives = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType is DriveType.Fixed or DriveType.Removable)
            .ToList();

        long allTotal = 0, allFree = 0;
        var items = new List<DriveVolumeItem>();
        foreach (var d in readyDrives)
        {
            long t = 0, f = 0;
            string label = "";
            try { t = d.TotalSize; f = d.AvailableFreeSpace; label = d.VolumeLabel; } catch { }
            allTotal += t;
            allFree += f;
            items.Add(new DriveVolumeItem
            {
                Key = d.RootDirectory.FullName,
                Name = d.Name,
                VolumeLabel = label,
                TotalBytes = t,
                FreeBytes = f,
                IsAllDrives = false,
                IsActive = string.Equals(Settings.SelectedDrive, d.RootDirectory.FullName, StringComparison.OrdinalIgnoreCase),
            });
        }

        bool isAll = Settings.SelectedDrive == DriveVolumeItem.AllDrivesKey || !items.Any(i => i.IsActive);
        if (isAll) Settings.SelectedDrive = DriveVolumeItem.AllDrivesKey;

        items.Insert(0, new DriveVolumeItem
        {
            Key = DriveVolumeItem.AllDrivesKey,
            Name = Loc.T("Tous les disques", "All drives"),
            VolumeLabel = $"{readyDrives.Count}",
            TotalBytes = allTotal,
            FreeBytes = allFree,
            IsAllDrives = true,
            IsActive = isAll,
        });

        OnUi(() =>
        {
            Drives.Clear();
            foreach (var it in items) Drives.Add(it);
            var active = items.FirstOrDefault(i => i.IsActive) ?? items[0];
            DiskTotal = active.TotalBytes;
            DiskFree = active.FreeBytes;
            OnPropertyChanged(nameof(DiskUsed));
        });

        _ = Task.Run(RecycleBin.Size).ContinueWith(t => OnUi(() => TrashBytes = t.Result));
    }

    public void SaveSettings() => AppData.Save("settings.json", Settings);

    public void ExcludePath(string path)
    {
        if (!Settings.ExcludedPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            Settings.ExcludedPaths.Add(path);
            SaveSettings();
        }
        foreach (var c in CacheItems.Where(i => i.Paths.Contains(path, StringComparer.OrdinalIgnoreCase)).ToList()) CacheItems.Remove(c);
        foreach (var d in DevItems.Where(i => i.Paths.Contains(path, StringComparer.OrdinalIgnoreCase)).ToList()) DevItems.Remove(d);
        foreach (var l in LargeFileItems.Where(i => string.Equals(i.FullPath, path, StringComparison.OrdinalIgnoreCase)).ToList()) LargeFileItems.Remove(l);
        RefreshAllFilteredViews();
        NotifyTotals();
    }
}
