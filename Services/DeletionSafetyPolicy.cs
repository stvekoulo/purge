using PurgeWin.Models;
using PurgeWin.Utilities;

namespace PurgeWin.Services;

public enum DeletionDecision { Allowed, NotInAllowlist, ProtectedLocation, ReparsePoint, Missing, TooYoung }

/// Port of DeletionSafetyPolicy.evaluate. Every App Caches removal goes
/// through Evaluate, which re-reads the allowlist from disk at delete time:
/// a path is allowed only when the catalog still expands to it (Folder mode)
/// or to its parent (Contents mode), and it is never a protected location.
public static class DeletionSafetyPolicy
{
    private static readonly Lazy<HashSet<string>> Protected = new(BuildProtected);
    private static readonly HashSet<string> SystemRootNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "Program Files", "Program Files (x86)", "ProgramData",
        "$Recycle.Bin", "System Volume Information", "Recovery", "PerfLogs",
    };

    public static DeletionDecision Evaluate(string path, CatalogEntry entry)
    {
        string full;
        try { full = Path.GetFullPath(path).TrimEnd('\\'); }
        catch { return DeletionDecision.NotInAllowlist; }

        if (!File.Exists(full) && !Directory.Exists(full)) return DeletionDecision.Missing;
        if (IsProtected(full)) return DeletionDecision.ProtectedLocation;
        if (PathExpander.IsReparsePoint(full)) return DeletionDecision.ReparsePoint;
        if (full.Count(c => c == '\\') < 3) return DeletionDecision.ProtectedLocation;

        var roots = entry.Paths.SelectMany(PathExpander.Expand)
            .Select(r => r.TrimEnd('\\'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        switch (entry.CleanMode)
        {
            case CleanMode.Folder:
                return roots.Contains(full) ? DeletionDecision.Allowed : DeletionDecision.NotInAllowlist;
            case CleanMode.Contents:
                var parent = Path.GetDirectoryName(full)?.TrimEnd('\\');
                if (parent is null || !roots.Contains(parent)) return DeletionDecision.NotInAllowlist;
                if (entry.MinAgeHours > 0)
                {
                    var changed = Directory.Exists(full) ? Directory.GetLastWriteTime(full) : File.GetLastWriteTime(full);
                    if (changed > DateTime.Now.AddHours(-entry.MinAgeHours)) return DeletionDecision.TooYoung;
                }
                return DeletionDecision.Allowed;
        }
        return DeletionDecision.NotInAllowlist;
    }

    public static bool IsProtected(string fullPath)
    {
        var normalized = PathScope.Normalize(fullPath);
        if (normalized is null) return true;
        if (Protected.Value.Contains(normalized)) return true;
        // A removable drive can appear after the lazy protected-root snapshot.
        var root = Path.GetPathRoot(normalized);
        if (string.IsNullOrEmpty(root)) return true;
        return normalized.Equals(Path.TrimEndingDirectorySeparator(root), StringComparison.OrdinalIgnoreCase)
            || SystemRootNames.Contains(Path.GetRelativePath(root, normalized));
    }

    /// <summary>Direct user-selected cleanup must never delete an operating
    /// system directory or its children. Catalog-approved cache locations
    /// have a separate exact-match policy in Evaluate.</summary>
    public static bool IsSystemPath(string fullPath)
    {
        var normalized = PathScope.Normalize(fullPath);
        if (normalized is null) return true;
        var root = Path.GetPathRoot(normalized);
        if (string.IsNullOrEmpty(root)) return true;
        var relative = Path.GetRelativePath(root, normalized);
        var first = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        return SystemRootNames.Contains(first);
    }

    /// Never-delete roots across ALL drives: drive roots, Windows/Program Files/
    /// Recycle.Bin/System Volume Information on every drive, and user profile roots.
    private static HashSet<string> BuildProtected()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] perDriveProtected =
        [
            "Windows", "Program Files", "Program Files (x86)", "ProgramData",
            "$Recycle.Bin", "System Volume Information", "Recovery", "PerfLogs",
            "DEVS", "dev", "Projects", "Repos", "Source", "Users",
        ];
        foreach (var d in DriveInfo.GetDrives())
        {
            var root = d.RootDirectory.FullName.TrimEnd('\\');
            set.Add(root);
            foreach (var sub in perDriveProtected)
                set.Add(root + "\\" + sub);
        }
        Environment.SpecialFolder[] folders =
        [
            Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.CommonApplicationData,
            Environment.SpecialFolder.Windows, Environment.SpecialFolder.System, Environment.SpecialFolder.SystemX86,
            Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86,
            Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.Desktop,
            Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyMusic, Environment.SpecialFolder.MyVideos,
        ];
        foreach (var f in folders)
        {
            var p = Environment.GetFolderPath(f);
            if (!string.IsNullOrEmpty(p)) set.Add(p.TrimEnd('\\'));
        }
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        set.Add(Path.Combine(home, "Downloads"));
        set.Add(Path.Combine(home, "AppData"));
        set.Add(Path.GetTempPath().TrimEnd('\\'));
        var oneDrive = Environment.GetEnvironmentVariable("OneDrive");
        if (!string.IsNullOrEmpty(oneDrive)) set.Add(oneDrive.TrimEnd('\\'));
        return set;
    }

    public static string Describe(DeletionDecision decision) => decision switch
    {
        DeletionDecision.Allowed => Loc.T("Autorisé", "Allowed"),
        DeletionDecision.NotInAllowlist => Loc.T("Ignoré par sécurité : hors de la liste autorisée", "Skipped for safety: not in allowlist"),
        DeletionDecision.ProtectedLocation => Loc.T("Ignoré par sécurité : emplacement protégé", "Skipped for safety: protected location"),
        DeletionDecision.ReparsePoint => Loc.T("Ignoré par sécurité : lien ou jonction", "Skipped for safety: symlink or junction"),
        DeletionDecision.Missing => Loc.T("Déjà supprimé", "Already removed"),
        DeletionDecision.TooYoung => Loc.T("Modifié récemment, conservé", "Recently modified, kept"),
        _ => Loc.T("Refusé par sécurité", "Refused for safety"),
    };
}
