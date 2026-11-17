using System.Diagnostics;

namespace PurgeWin.Services;

/// <summary>Detects running user-session applications that hold open locks on
/// selected cache folders, and offers graceful WM_CLOSE shutdown before cleaning
/// (never force-killing unsaved user work).</summary>
public static class RunningAppDetector
{
    private static readonly Dictionary<string, (string AppName, string[] Processes)> AppMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chrome"] = ("Google Chrome", ["chrome"]),
        ["edge"] = ("Microsoft Edge", ["msedge"]),
        ["brave"] = ("Brave", ["brave"]),
        ["opera"] = ("Opera", ["opera"]),
        ["vivaldi"] = ("Vivaldi", ["vivaldi"]),
        ["arc"] = ("Arc", ["Arc"]),
        ["zen"] = ("Zen Browser", ["zen"]),
        ["firefox"] = ("Firefox", ["firefox"]),
        ["discord"] = ("Discord", ["Discord"]),
        ["slack"] = ("Slack", ["slack"]),
        ["teams"] = ("Microsoft Teams", ["ms-teams", "Teams"]),
        ["notion"] = ("Notion", ["Notion"]),
        ["figma"] = ("Figma", ["Figma"]),
        ["spotify"] = ("Spotify", ["Spotify"]),
        ["steam-htmlcache"] = ("Steam", ["steam", "steamwebhelper"]),
        ["zoom"] = ("Zoom", ["Zoom"]),
        ["capcut"] = ("CapCut", ["CapCut"]),
        ["davinci-resolve"] = ("DaVinci Resolve", ["Resolve"]),
        ["canva"] = ("Canva", ["Canva"]),
        ["epic-games"] = ("Epic Games Launcher", ["EpicGamesLauncher"]),
        ["battlenet"] = ("Battle.net", ["Battle.net"]),
        ["ubisoft"] = ("Ubisoft Connect", ["upc"]),
        ["telegram-temp"] = ("Telegram", ["Telegram"]),
        ["postman"] = ("Postman", ["Postman"]),
        ["insomnia"] = ("Insomnia", ["Insomnia"]),
        ["dev-vscode"] = ("Visual Studio Code", ["Code"]),
        ["dev-cursor"] = ("Cursor", ["Cursor"]),
    };

    public sealed record RunningAppInfo(string Key, string AppName, string[] ProcessNames);

    public static List<RunningAppInfo> FindRunning(IEnumerable<string> keys)
    {
        var requested = keys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (requested.Count == 0) return [];

        int mySession;
        try { mySession = Process.GetCurrentProcess().SessionId; }
        catch { return []; }

        HashSet<string> activeProcessNames;
        try
        {
            activeProcessNames = Process.GetProcesses()
                .Where(p =>
                {
                    try { return p.SessionId == mySession; }
                    catch { return false; }
                    finally { p.Dispose(); }
                })
                .Select(p => p.ProcessName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch { return []; }

        var found = new List<RunningAppInfo>();
        foreach (var key in requested)
        {
            if (!AppMap.TryGetValue(key, out var spec)) continue;
            if (spec.Processes.Any(activeProcessNames.Contains))
                found.Add(new RunningAppInfo(key, spec.AppName, spec.Processes));
        }
        return found.DistinctBy(r => r.AppName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static async Task CloseGracefullyAsync(IEnumerable<RunningAppInfo> runningApps)
    {
        var procNames = runningApps.SelectMany(a => a.ProcessNames).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (procNames.Count == 0) return;

        await Task.Run(async () =>
        {
            int mySession;
            try { mySession = Process.GetCurrentProcess().SessionId; }
            catch { return; }

            var tracked = new List<Process>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.SessionId != mySession || !procNames.Contains(p.ProcessName))
                    {
                        p.Dispose();
                        continue;
                    }
                    if (p.MainWindowHandle != IntPtr.Zero)
                        p.CloseMainWindow();
                    tracked.Add(p);
                }
                catch { p.Dispose(); }
            }

            try
            {
                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 3500 && tracked.Any(p => { try { return !p.HasExited; } catch { return false; } }))
                    await Task.Delay(250);
            }
            finally
            {
                foreach (var p in tracked) p.Dispose();
            }
        });
    }
}
