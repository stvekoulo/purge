using System.Diagnostics;

namespace PurgeWin.Utilities;

/// Purge for Windows: opens Windows Explorer with the file or folder
/// selected, or opens a directory directly.
public static class ExplorerReveal
{
    public static void Show(string? path) => Reveal(path);

    public static void Reveal(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var explorerExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            if (path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo(explorerExe, path) { UseShellExecute = true });
                return;
            }
            var full = Path.GetFullPath(path);
            if (File.Exists(full))
            {
                Process.Start(new ProcessStartInfo(explorerExe, $"/select,\"{full}\"") { UseShellExecute = true });
            }
            else if (Directory.Exists(full))
            {
                Process.Start(new ProcessStartInfo(explorerExe, $"\"{full}\"") { UseShellExecute = true });
            }
            else if (Path.GetDirectoryName(full) is { } parent && Directory.Exists(parent))
            {
                Process.Start(new ProcessStartInfo(explorerExe, $"\"{parent}\"") { UseShellExecute = true });
            }
        }
        catch { }
    }

    public static void OpenFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch { }
    }
}
