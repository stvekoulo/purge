using PurgeWin.Models;
using PurgeWin.Utilities;

namespace PurgeWin.Services;

public static class EverythingElseScanner
{
    public static async Task<List<EverythingElseFolder>> MeasureAsync(long diskUsedBytes, long purgeCategorizedBytes,
        long trashBytes, IReadOnlyList<string> activeDrives, CancellationToken token = default,
        IReadOnlyList<string>? excluded = null)
    {
        var scope = new PathScope(activeDrives, excluded);
        var items = new List<EverythingElseFolder>();
        foreach (var root in scope.Roots)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var folders = await FolderExplorer.ReadAsync(root, scope, token);
                items.AddRange(folders.Select(f => scope.Roots.Count > 1 ? f with { Name = root.TrimEnd('\\') + "\\" + f.Name } : f));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        long unmeasured = Math.Max(0, diskUsedBytes - items.Sum(i => i.Bytes));
        if (unmeasured > 0)
            items.Add(new("unmeasured", Loc.T("Espace non détaillé", "Space not yet detailed"), "",
                Loc.T("Dossiers profonds, fichiers protégés ou exclus et métadonnées. Cette taille n'est pas attribuée à Windows.",
                    "Deep folders, protected or excluded files, and metadata. This size is not attributed to Windows."),
                Loc.T("Explorez un dossier pour affiner l'analyse. Les tailles marquées ≈ sont des estimations.",
                    "Explore a folder to refine the scan. Sizes marked ≈ are estimates."),
                EverythingElseBucket.Unmeasured, unmeasured) { IsDirectory = false, IsEstimate = false });
        return items.OrderByDescending(i => i.Bytes).ToList();
    }
}
