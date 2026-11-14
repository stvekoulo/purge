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

    /// <summary>Builds a ProcessStartInfo that prefers silent/unattended flags when supported
    /// so batch uninstallation does not require manual clicks for each app.</summary>
    public static ProcessStartInfo CreateUnattendedStartInfo(
        string? uninstallString, string? quietUninstallString = null, bool preferUnattended = true, bool isMsi = false)
    {
        if (preferUnattended && !string.IsNullOrWhiteSpace(quietUninstallString))
            return CreateStartInfo(quietUninstallString);

        var raw = !string.IsNullOrWhiteSpace(uninstallString) ? uninstallString : quietUninstallString;
        var psi = CreateStartInfo(raw ?? "");
        if (!preferUnattended) return psi;

        var exeName = Path.GetFileName(psi.FileName);
        var args = psi.Arguments ?? "";

        // 1. MSI packages (msiexec.exe): convert /I to /X and add /passive /norestart
        if (isMsi || exeName.Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase)
                  || exeName.Equals("msiexec", StringComparison.OrdinalIgnoreCase))
        {
            args = System.Text.RegularExpressions.Regex.Replace(args, @"(^|\s)/[iI](\s|\{)", "$1/X$2");
            if (!args.Contains("/q", StringComparison.OrdinalIgnoreCase)
                && !args.Contains("/passive", StringComparison.OrdinalIgnoreCase))
                args = $"{args} /passive /norestart".Trim();
            psi.Arguments = args;
            return psi;
        }

        // 2. Inno Setup uninstallers (unins000.exe, unins001.exe, etc.)
        if (exeName.StartsWith("unins", StringComparison.OrdinalIgnoreCase)
            && exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            if (!args.Contains("/SILENT", StringComparison.OrdinalIgnoreCase)
                && !args.Contains("/VERYSILENT", StringComparison.OrdinalIgnoreCase))
                psi.Arguments = $"{args} /VERYSILENT /SUPPRESSMSGBOXES /NORESTART".Trim();
            return psi;
        }

        // 3. Squirrel / Electron Update.exe --uninstall
        if (exeName.Equals("Update.exe", StringComparison.OrdinalIgnoreCase)
            && args.Contains("--uninstall", StringComparison.OrdinalIgnoreCase))
        {
            if (!args.Contains("-s", StringComparison.OrdinalIgnoreCase)
                && !args.Contains("--silent", StringComparison.OrdinalIgnoreCase))
                psi.Arguments = $"{args} -s".Trim();
            return psi;
        }

        // 4. NSIS / Electron-builder Uninstall*.exe when no silent flag is present
        if ((exeName.StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase)
             || exeName.StartsWith("uninst", StringComparison.OrdinalIgnoreCase))
            && !args.Contains("/S", StringComparison.Ordinal)
            && !args.Contains("/silent", StringComparison.OrdinalIgnoreCase)
            && !args.Contains("/quiet", StringComparison.OrdinalIgnoreCase))
        {
            psi.Arguments = $"{args} /S".Trim();
        }

        return psi;
    }
}
