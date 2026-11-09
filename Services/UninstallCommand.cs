using System.Diagnostics;

namespace PurgeWin.Services;

/// <summary>Starts an application's registered uninstall command directly,
/// without introducing an extra cmd.exe /c shell interpretation.</summary>
public static class UninstallCommand
{
    public static ProcessStartInfo CreateStartInfo(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) throw new ArgumentException("No registered uninstaller.", nameof(command));
        var input = command.Trim();
        string file;
        string args;

        if (input[0] == '"')
        {
            var end = input.IndexOf('"', 1);
            if (end < 2) throw new ArgumentException("Invalid uninstall command.", nameof(command));
            file = input[1..end];
            args = input[(end + 1)..].TrimStart();
        }
        else
        {
            // Some registry entries contain unquoted paths with spaces.
            // Prefer the first executable suffix at a token boundary.
            var end = -1;
            foreach (var suffix in new[] { ".exe", ".cmd", ".bat" })
            {
                var pos = input.IndexOf(suffix, StringComparison.OrdinalIgnoreCase);
                if (pos >= 0 && (pos + suffix.Length == input.Length || char.IsWhiteSpace(input[pos + suffix.Length])))
                {
                    end = pos + suffix.Length;
                    break;
                }
            }
            if (end < 0)
            {
                end = input.IndexOf(' ');
                if (end < 0) end = input.Length;
            }
            file = input[..end];
            args = input[end..].TrimStart();
        }

        if (string.IsNullOrWhiteSpace(file)) throw new ArgumentException("Missing uninstall executable.", nameof(command));
        return new ProcessStartInfo(file, args) { UseShellExecute = true };
    }
}
