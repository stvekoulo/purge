using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;
using PurgeWin.Models;

namespace PurgeWin.Services;

/// <summary>Enumerates and toggles Windows startup applications via Explorer\StartupApproved,
/// matching Windows 11 Settings &gt; Apps &gt; Startup and Task Manager without deleting entries.</summary>
public static class StartupManagerService
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedRunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ApprovedRun32SubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
    private const string ApprovedFolderSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    public static bool IsElevated
    {
        get
        {
            try
            {
                using var id = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }
    }

    public static Task<List<StartupAppItem>> ScanAsync(CancellationToken token = default) =>
        Task.Run(() =>
        {
            var items = new List<StartupAppItem>();
            bool admin = IsElevated;

            ReadRegistryRun(items, RegistryHive.CurrentUser, RegistryView.Default,
                ApprovedRunSubKey, StartupSourceKind.CurrentUserRun, canToggle: true, token);
            ReadRegistryRun(items, RegistryHive.LocalMachine, RegistryView.Registry64,
                ApprovedRunSubKey, StartupSourceKind.LocalMachineRun, canToggle: admin, token);
            ReadRegistryRun(items, RegistryHive.LocalMachine, RegistryView.Registry32,
                ApprovedRun32SubKey, StartupSourceKind.LocalMachineRun32, canToggle: admin, token);

            ReadStartupFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                RegistryHive.CurrentUser, StartupSourceKind.UserStartupFolder, canToggle: true, token);
            ReadStartupFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                RegistryHive.LocalMachine, StartupSourceKind.CommonStartupFolder, canToggle: admin, token);

            return items
                .OrderByDescending(i => i.IsEnabled)
                .ThenBy(i => i.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }, token);

    public static bool SetStartupEnabled(StartupAppItem item, bool enabled)
    {
        if (!item.CanToggle) return false;
        var (hive, view, subKey) = item.SourceKind switch
        {
            StartupSourceKind.CurrentUserRun => (RegistryHive.CurrentUser, RegistryView.Default, ApprovedRunSubKey),
            StartupSourceKind.UserStartupFolder => (RegistryHive.CurrentUser, RegistryView.Default, ApprovedFolderSubKey),
            StartupSourceKind.LocalMachineRun => (RegistryHive.LocalMachine, RegistryView.Registry64, ApprovedRunSubKey),
            StartupSourceKind.LocalMachineRun32 => (RegistryHive.LocalMachine, RegistryView.Registry64, ApprovedRun32SubKey),
            StartupSourceKind.CommonStartupFolder => (RegistryHive.LocalMachine, RegistryView.Registry64, ApprovedFolderSubKey),
            _ => (RegistryHive.CurrentUser, RegistryView.Default, ApprovedRunSubKey),
        };

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var approvedKey = baseKey.CreateSubKey(subKey, writable: true);
            if (approvedKey is null) return false;

            var payload = new byte[12];
            if (enabled)
            {
                payload[0] = 0x02;
            }
            else
            {
                payload[0] = 0x03;
                var fileTime = BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc());
                Buffer.BlockCopy(fileTime, 0, payload, 4, 8);
            }
            approvedKey.SetValue(item.ValueName, payload, RegistryValueKind.Binary);
            item.IsEnabled = enabled;
            return true;
        }
        catch { return false; }
    }

    private static void ReadRegistryRun(
        List<StartupAppItem> list, RegistryHive hive, RegistryView view,
        string approvedSubKey, StartupSourceKind kind, bool canToggle, CancellationToken token)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var runKey = baseKey.OpenSubKey(RunSubKey);
            if (runKey is null) return;
            using var approvedBase = RegistryKey.OpenBaseKey(hive, view == RegistryView.Registry32 ? RegistryView.Registry64 : view);
            using var approvedKey = approvedBase.OpenSubKey(approvedSubKey);

            foreach (var valueName in runKey.GetValueNames())
            {
                token.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(valueName)) continue;
                var cmd = (runKey.GetValue(valueName) as string)?.Trim();
                if (string.IsNullOrWhiteSpace(cmd)) continue;
                if (valueName.Equals("SecurityHealth", StringComparison.OrdinalIgnoreCase)) continue;

                var exePath = ExtractExecutablePath(cmd);
                var (friendlyName, publisher) = ReadFileMetadata(exePath, valueName);
                var iconPath = !string.IsNullOrEmpty(exePath) && File.Exists(exePath)
                    ? AppIconService.GetAppIconPath($"startup_{kind}_{valueName}", friendlyName, exePath, Path.GetDirectoryName(exePath))
                    : null;
                bool isEnabled = IsStartupApproved(approvedKey, valueName);

                list.Add(new StartupAppItem
                {
                    Id = $"{kind}:{valueName}",
                    ValueName = valueName,
                    DisplayName = friendlyName,
                    Publisher = publisher,
                    Command = cmd,
                    ExecutablePath = exePath,
                    IconPath = iconPath,
                    SourceKind = kind,
                    CanToggle = canToggle,
                    IsEnabled = isEnabled,
                });
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { }
    }

    private static void ReadStartupFolder(
        List<StartupAppItem> list, string folder, RegistryHive approvedHive,
        StartupSourceKind kind, bool canToggle, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(approvedHive, RegistryView.Registry64);
            using var approvedKey = baseKey.OpenSubKey(ApprovedFolderSubKey);

            foreach (var file in Directory.EnumerateFiles(folder))
            {
                token.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(file);
                if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;

                var displayName = Path.GetFileNameWithoutExtension(file);
                var (_, publisher) = ReadFileMetadata(file, displayName);
                var iconPath = AppIconService.GetAppIconPath($"startup_{kind}_{fileName}", displayName, file, folder);
                bool isEnabled = IsStartupApproved(approvedKey, fileName);

                list.Add(new StartupAppItem
                {
                    Id = $"{kind}:{fileName}",
                    ValueName = fileName,
                    DisplayName = displayName,
                    Publisher = publisher,
                    Command = file,
                    ExecutablePath = file,
                    IconPath = iconPath,
                    SourceKind = kind,
                    CanToggle = canToggle,
                    IsEnabled = isEnabled,
                });
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { }
    }

    public static bool IsStartupApproved(RegistryKey? approvedKey, string valueName)
    {
        try
        {
            if (approvedKey?.GetValue(valueName) is byte[] data && data.Length > 0)
                return (data[0] & 1) == 0;
        }
        catch { }
        return true;
    }

    public static string? ExtractExecutablePath(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var expanded = Environment.ExpandEnvironmentVariables(command.Trim());
        if (expanded.StartsWith('"'))
        {
            int end = expanded.IndexOf('"', 1);
            return end > 1 ? expanded[1..end] : null;
        }
        int exeIdx = expanded.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIdx > 0) return expanded[..(exeIdx + 4)];
        int space = expanded.IndexOf(' ');
        return space > 0 ? expanded[..space] : expanded;
    }

    private static (string Name, string Publisher) ReadFileMetadata(string? exePath, string fallbackName)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            return (fallbackName, "");
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            var desc = !string.IsNullOrWhiteSpace(info.FileDescription) ? info.FileDescription.Trim()
                : !string.IsNullOrWhiteSpace(info.ProductName) ? info.ProductName.Trim()
                : fallbackName;
            var company = info.CompanyName?.Trim() ?? "";
            return (desc, company);
        }
        catch { return (fallbackName, ""); }
    }
}
