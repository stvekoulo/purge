using System.Text.Json.Serialization;
using PurgeWin.Utilities;

namespace PurgeWin.Models;

/// Port of SafetyLevel. Only .Safe and .Medium may surface in scan results;
/// .Unknown is the fallback for anything unidentified and is dropped.
public enum SafetyLevel { Safe, Medium, Unknown }

public static class SafetyLevelExtensions
{
    public static bool CanSurfaceInScanResults(this SafetyLevel level) =>
        level is SafetyLevel.Safe or SafetyLevel.Medium;

    public static string DisplayName(this SafetyLevel level) => level switch
    {
        SafetyLevel.Safe => Loc.T("Sûr à nettoyer", "Safe to clean"),
        SafetyLevel.Medium => Loc.T("À vérifier", "Check first"),
        _ => Loc.T("Incertain", "Unknown"),
    };

    public static int SortOrder(this SafetyLevel level) => level switch
    {
        SafetyLevel.Safe => 0,
        SafetyLevel.Medium => 1,
        _ => 3,
    };

    public static SafetyLevel Parse(string? tag) => tag switch
    {
        "safe" => SafetyLevel.Safe,
        "medium" => SafetyLevel.Medium,
        _ => SafetyLevel.Unknown,
    };
}

/// How a catalog location is cleaned: the matched folder itself goes to the
/// Recycle Bin, or only what is inside it (for roots like %TEMP% that the
/// system expects to exist).
public enum CleanMode { Folder, Contents }

/// One entry of Resources/windows-cache-catalog.json, the Windows
/// counterpart of explanations.json plus CacheDiscoveryPaths: the allowlist.
public sealed class CatalogEntry
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("tag")] public string Tag { get; set; } = "unknown";
    [JsonPropertyName("explanation")] public string Explanation { get; set; } = "";
    [JsonPropertyName("kind")] public string Kind { get; set; } = "app";
    [JsonPropertyName("icon")] public string? Icon { get; set; }
    [JsonPropertyName("mode")] public string Mode { get; set; } = "folder";
    [JsonPropertyName("min_age_hours")] public int MinAgeHours { get; set; }
    [JsonPropertyName("needs_admin")] public bool NeedsAdmin { get; set; }
    [JsonPropertyName("paths")] public List<string> Paths { get; set; } = [];

    [JsonIgnore] public SafetyLevel Level => SafetyLevelExtensions.Parse(Tag);
    [JsonIgnore] public CleanMode CleanMode => Mode == "contents" ? CleanMode.Contents : CleanMode.Folder;
}
