using System.Diagnostics;

namespace PurgeWin.Services;

public enum GitWorktreeState { Clean, Dirty, Registered, Unknown }

/// <summary>A missing Git executable, timeout or failed status check is never
/// interpreted as a clean worktree.</summary>
public static class GitWorktreeInspector
{
    public static GitWorktreeState Inspect(string directory)
    {
        if (!Directory.Exists(directory) ||
            (!File.Exists(Path.Combine(directory, ".git")) && !Directory.Exists(Path.Combine(directory, ".git"))))
            return GitWorktreeState.Unknown;

        try
        {
            var status = RunGit(directory, "status", "--porcelain=v1", "--untracked-files=all");
            if (status is null) return GitWorktreeState.Unknown;
            if (!string.IsNullOrWhiteSpace(status)) return GitWorktreeState.Dirty;

            // A clean worktree can still be registered and actively used by
            // an editor. Moving it straight to the Recycle Bin would leave
            // Git's linked-worktree metadata broken. Only unregistered
            // leftovers may be considered for direct cleanup.
            var registered = RunGit(directory, "worktree", "list", "--porcelain");
            if (registered is null) return GitWorktreeState.Unknown;
            var paths = registered.Split('\n')
                .Where(line => line.StartsWith("worktree ", StringComparison.Ordinal))
                .Select(line => line[9..].TrimEnd('\r').Replace('/', '\\'))
                .ToList();
            if (paths.Count == 0) return GitWorktreeState.Unknown;
            var current = Path.GetFullPath(directory).TrimEnd('\\');
            return paths.Any(p => string.Equals(p.TrimEnd('\\'), current, StringComparison.OrdinalIgnoreCase))
                ? GitWorktreeState.Registered : GitWorktreeState.Clean;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
            or System.ComponentModel.Win32Exception or ArgumentException)
        {
            return GitWorktreeState.Unknown;
        }
    }

    private static string? RunGit(string directory, params string[] args)
    {
        try
        {
            var start = new ProcessStartInfo("git")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            start.ArgumentList.Add("-C");
            start.ArgumentList.Add(directory);
            foreach (var arg in args) start.ArgumentList.Add(arg);

            using var process = Process.Start(start);
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(3000))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }
            if (process.ExitCode != 0) return null;
            _ = errors.GetAwaiter().GetResult();
            return output.GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
