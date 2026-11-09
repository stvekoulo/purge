using System.Diagnostics;
using Microsoft.Win32;

namespace PurgeWin.Services;

public static class UpdateInstaller
{
    public static ProcessStartInfo BuildStartInfo(string installer, string installationFolder,
        int processId, bool allUsers)
    {
        if (!Path.IsPathFullyQualified(installer) || !Path.IsPathFullyQualified(installationFolder) || processId <= 0)
            throw new ArgumentException("Invalid update installation target.");
        var start = new ProcessStartInfo(installer) { UseShellExecute = true };
        foreach (var argument in new[] { "/SP-", "/SILENT", "/NORESTART", "/NOCLOSEAPPLICATIONS",
            "/NORESTARTAPPLICATIONS", "/PURGEUPDATE=1", $"/PURGEPID={processId}",
            $"/DIR={Path.GetFullPath(installationFolder).TrimEnd(Path.DirectorySeparatorChar)}",
            allUsers ? "/ALLUSERS" : "/CURRENTUSER", "/LOG" })
            start.ArgumentList.Add(argument);
        return start;
    }

    public static void Start(string installer, string installationFolder, int processId)
    {
        using var process = Process.Start(BuildStartInfo(installer, installationFolder, processId,
            IsMachineInstallation(installationFolder)));
        if (process == null) throw new UpdateException(UpdateError.Install);
    }

    private static bool IsMachineInstallation(string folder)
    {
        const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{8E6C34A2-7F19-4C8B-A540-9D31E20B1045}_is1";
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = machine.OpenSubKey(keyPath);
            if (key?.GetValue("InstallLocation") is string location
                && Path.GetFullPath(location).TrimEnd('\\').Equals(Path.GetFullPath(folder).TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
