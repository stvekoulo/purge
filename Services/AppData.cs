using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using PurgeWin.Models;
using PurgeWin.Utilities;

namespace PurgeWin.Services;

/// Port of AppSupportFolder: everything Purge keeps lives in
/// %LOCALAPPDATA%\Purge. Nothing is uploaded.
public static class AppData
{
    public static string Folder
    {
        get
        {
#if PURGE_UX_TEST
            var testRoot = Environment.GetEnvironmentVariable("PURGE_TEST_DATA_ROOT");
            if (!string.IsNullOrWhiteSpace(testRoot) && Path.IsPathFullyQualified(testRoot))
            {
                Directory.CreateDirectory(testRoot!);
                return testRoot!;
            }
#endif
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Purge");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly object IoLock = new();
    private const int MaxHistoryEntries = 100;
    private const int MaxPathsPerHistoryEntry = 50;

    public static T Load<T>(string name) where T : new()
    {
        lock (IoLock)
        {
            try
            {
                var path = Path.Combine(Folder, name);
                return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? new T() : new T();
            }
            catch { return new T(); }
        }
    }

    public static void Save<T>(string name, T value)
    {
        lock (IoLock)
        {
            var tmp = Path.Combine(Folder, $"{name}.tmp-{Guid.NewGuid():N}");
            try
            {
                if (value is HistoryLog log)
                {
                    if (log.Entries.Count > MaxHistoryEntries)
                        log.Entries.RemoveRange(MaxHistoryEntries, log.Entries.Count - MaxHistoryEntries);
                    foreach (var entry in log.Entries)
                    {
                        if (entry.Moved.Count > MaxPathsPerHistoryEntry)
                            entry.Moved.RemoveRange(MaxPathsPerHistoryEntry, entry.Moved.Count - MaxPathsPerHistoryEntry);
                        if (entry.Skipped.Count > MaxPathsPerHistoryEntry)
                            entry.Skipped.RemoveRange(MaxPathsPerHistoryEntry, entry.Skipped.Count - MaxPathsPerHistoryEntry);
                    }
                }
                var path = Path.Combine(Folder, name);
                File.WriteAllText(tmp, JsonSerializer.Serialize(value, Json));
                File.Move(tmp, path, overwrite: true);
            }
            catch { /* ignore disk full or lock */ }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }
    }
}

public sealed class AppSettings
{
    /// "system", "light" or "dark" (AppearanceMode).
    public string Appearance { get; set; } = "system";
    /// "fr" or "en".
    public string Language { get; set; } = "fr";
    /// "ALL" or a drive root like "C:\\", "D:\\", "E:\\".
    public string SelectedDrive { get; set; } = DriveVolumeItem.AllDrivesKey;
    public List<string> ExcludedPaths { get; set; } = [];
    public List<string> CustomProjectRoots { get; set; } = [];
    /// "off", "weekly", "monthly", "quarterly"
    public string ScheduledFrequency { get; set; } = "off";
    /// Minimum days untouched for project build folders (0 = all, 30, 90, 180, 365).
    public int StaleProjectDays { get; set; } = 0;
    public bool AutoCheckUpdates { get; set; } = true;
    public bool EmptyRecycleBinAfterCleaning { get; set; }
    public bool HasCompletedOnboarding { get; set; }
}

/// Port of CleanupHistoryEntry.
public sealed class HistoryEntry
{
    public DateTime Date { get; set; }
    public string Trigger { get; set; } = "manual"; // manual | safe | scheduled
    public long FreedBytes { get; set; }
    public int ItemCount { get; set; }
    public bool RecycleBinEmptied { get; set; }
    public bool RecycleBinEmptyRequested { get; set; }
    public List<string> Moved { get; set; } = [];
    public List<string> Skipped { get; set; } = [];

    [JsonIgnore]
    public string DateText => Loc.IsEn
        ? Date.ToString("MMM d, yyyy HH:mm", CultureInfo.GetCultureInfo("en-US"))
        : Date.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("fr-FR"));

    [JsonIgnore]
    public string FreedText => ByteFormatter.Format(FreedBytes);

    [JsonIgnore]
    public string SummaryText
    {
        get
        {
            var mode = Trigger switch
            {
                "safe" => Loc.T("Nettoyage rapide sécurisé", "Quick safe clean"),
                "scheduled" => Loc.T("Nettoyage planifié", "Scheduled clean"),
                "uninstall" => Loc.T("Désinstallation & nettoyage", "Uninstall & clean"),
                "optimization" => Loc.T("Maintenance système", "System maintenance"),
                _ => Loc.T("Nettoyage manuel", "Manual clean")
            };
            if (RecycleBinEmptied) return Loc.T($"{ItemCount} éléments nettoyés · Corbeille vidée définitivement · {mode}",
                $"{ItemCount} items cleaned · Recycle Bin permanently emptied · {mode}");
            if (RecycleBinEmptyRequested) return Loc.T($"{ItemCount} éléments nettoyés · Vidage partiel de la Corbeille · {mode}",
                $"{ItemCount} items cleaned · Recycle Bin partially emptied · {mode}");
            return Loc.T(
                $"{ItemCount} élément{(ItemCount > 1 ? "s" : "")} envoyé{(ItemCount > 1 ? "s" : "")} à la Corbeille · {mode}",
                $"{ItemCount} item{(ItemCount > 1 ? "s" : "")} moved to Recycle Bin · {mode}");
        }
    }
}

public sealed class HistoryLog
{
    public List<HistoryEntry> Entries { get; set; } = [];
}
