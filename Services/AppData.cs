using System.Text.Json;
using System.Text.Json.Serialization;
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
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Purge");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static T Load<T>(string name) where T : new()
    {
        try
        {
            var path = Path.Combine(Folder, name);
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? new T() : new T();
        }
        catch { return new T(); }
    }

    public static void Save<T>(string name, T value)
    {
        try
        {
            var path = Path.Combine(Folder, name);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(value, Json));
            File.Move(tmp, path, overwrite: true);
        }
        catch { /* ignore disk full or lock */ }
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
    public bool HasCompletedOnboarding { get; set; }
}

/// Port of CleanupHistoryEntry.
public sealed class HistoryEntry
{
    public DateTime Date { get; set; }
    public string Trigger { get; set; } = "manual"; // manual | safe | scheduled
    public long FreedBytes { get; set; }
    public int ItemCount { get; set; }
    public List<string> Moved { get; set; } = [];
    public List<string> Skipped { get; set; } = [];

    [JsonIgnore]
    public string DateText => Date.ToString("dd/MM/yyyy HH:mm");

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
                _ => Loc.T("Nettoyage manuel", "Manual clean")
            };
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
