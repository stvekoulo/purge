using System.Runtime.InteropServices;

namespace PurgeWin.Services;

internal static class FatalError
{
    private static int _reporting;

    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr owner, string text, string caption, uint flags);

    public static void Exit(string source, Exception exception, bool showDialog = true)
    {
        App.LogCrash(source, exception);
        bool headless = Environment.GetCommandLineArgs().Any(a =>
            a.Equals("--scheduled-clean", StringComparison.OrdinalIgnoreCase)
            || a.Equals("--uninstall-cleanup", StringComparison.OrdinalIgnoreCase));
        if (showDialog && !headless && Interlocked.Exchange(ref _reporting, 1) == 0)
        {
            try
            {
                // Native UI remains usable even when WinUI/XAML cannot load.
                MessageBox(IntPtr.Zero,
                    "Purge ne peut pas afficher son interface. / Purge cannot display its interface.\n\n"
                    + exception.Message + "\n\n"
                    + "Journal / Log : %LOCALAPPDATA%\\Purge\\crash.log\n\n"
                    + "Réinstallez la dernière version complète de Purge. / Reinstall the latest full version of Purge.",
                    "Purge — Erreur / Error", 0x10 | 0x10000); // MB_ICONERROR | MB_SETFOREGROUND
            }
            catch { /* Logging and termination must still work without a desktop. */ }
        }
        Environment.Exit(1);
    }
}
