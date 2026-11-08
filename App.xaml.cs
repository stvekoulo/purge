using Microsoft.UI.Xaml;
using PurgeWin.Services;
using PurgeWin.Views;

namespace PurgeWin;

public partial class App : Application
{
    public static Window? CurrentWindow { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogCrash("AppDomain", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogCrash("TaskScheduler", e.Exception);
            e.SetObserved();
        };
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var cliArgs = Environment.GetCommandLineArgs();
        if (cliArgs.Any(a => string.Equals(a, "--uninstall-cleanup", StringComparison.OrdinalIgnoreCase)))
        {
            SystemIntegration.UninstallPurge(removeAppData: false);
            Environment.Exit(0);
            return;
        }
        if (cliArgs.Any(a => string.Equals(a, "--scheduled-clean", StringComparison.OrdinalIgnoreCase)))
        {
            await SystemIntegration.RunHeadlessScheduledCleanAsync();
            Environment.Exit(0);
            return;
        }

        CurrentWindow = new MainWindow();
        CurrentWindow.Activate();
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        LogCrash($"XamlUnhandled: {e.Message}", e.Exception);
        e.Handled = true;
    }

    public static void LogCrash(string source, Exception? ex)
    {
        try
        {
            var logFile = Path.Combine(AppData.Folder, "crash.log");
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {ex?.GetType().FullName}: {ex?.Message}");
            sb.AppendLine(ex?.StackTrace);
            var inner = ex?.InnerException;
            while (inner != null)
            {
                sb.AppendLine($"--- INNER: {inner.GetType().FullName}: {inner.Message}");
                sb.AppendLine(inner.StackTrace);
                inner = inner.InnerException;
            }
            sb.AppendLine();
            File.AppendAllText(logFile, sb.ToString());
        }
        catch { }
    }
}
