using System.Diagnostics;
using PurgeWin.Models;
using PurgeWin.Utilities;

namespace PurgeWin.Services;

public static class FolderExplorer
{
    private static readonly EnumerationOptions Options = new()
    {
        IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint,
        ReturnSpecialDirectories = false
    };

    public static Task<List<EverythingElseFolder>> ReadAsync(string directory, PathScope scope, CancellationToken token) =>
        Task.Run(() => Read(directory, scope, token), token);

    private static List<EverythingElseFolder> Read(string directory, PathScope scope, CancellationToken token)
    {
        if (!scope.Allows(directory) || PathExpander.IsReparsePoint(directory)) return [];
        var info = new DirectoryInfo(directory);
        if (!info.Exists) throw new DirectoryNotFoundException(directory);
        var result = new List<EverythingElseFolder>();
        foreach (var entry in info.EnumerateFileSystemInfos("*", Options).Take(2000))
        {
            token.ThrowIfCancellationRequested();
            if (!scope.Allows(entry.FullName)) continue;
            try
            {
                bool folder = entry is DirectoryInfo;
                var size = folder ? Sample(entry.FullName, scope, token) : (((FileInfo)entry).Length, false);
                result.Add(new(entry.FullName, entry.Name, entry.FullName,
                    folder ? Loc.T("Dossier · lecture seule", "Folder · read only") : Loc.T("Fichier · lecture seule", "File · read only"),
                    Loc.T("Explorez le dossier ou affichez-le dans l'Explorateur Windows.", "Explore the folder or reveal it in File Explorer."),
                    Classify(entry.FullName), size.Item1) { IsDirectory = folder, IsEstimate = size.Item2 });
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return result.OrderByDescending(i => i.Bytes).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static (long, bool) Sample(string directory, PathScope scope, CancellationToken token)
    {
        long bytes = 0;
        int count = 0;
        bool partial = false;
        var timer = Stopwatch.StartNew();
        void Walk(string path, int depth)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                foreach (var item in new DirectoryInfo(path).EnumerateFileSystemInfos("*", Options))
                {
                    token.ThrowIfCancellationRequested();
                    if (++count > 4000 || timer.ElapsedMilliseconds > 250) { partial = true; return; }
                    if (!scope.Allows(item.FullName)) continue;
                    if (item is FileInfo file) bytes += file.Length;
                    else if (depth < 2) Walk(item.FullName, depth + 1);
                    else partial = true;
                }
            }
            catch (IOException) { partial = true; }
            catch (UnauthorizedAccessException) { partial = true; }
        }
        Walk(directory, 0);
        return (bytes, partial);
    }

    private static EverythingElseBucket Classify(string path)
    {
        if (PathScope.IsWithin(path, Environment.GetFolderPath(Environment.SpecialFolder.Windows))
            || new[] { "System Volume Information", "$Recycle.Bin", "Recovery" }
                .Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            return EverythingElseBucket.ManagedByWindows;
        if (PathScope.IsWithin(path, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles))
            || PathScope.IsWithin(path, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)))
            return EverythingElseBucket.ManagedInApps;
        if (PathScope.IsWithin(path, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
            || PathScope.IsWithin(path, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)))
            return EverythingElseBucket.LeftAlone;
        return EverythingElseBucket.YourFiles;
    }
}
