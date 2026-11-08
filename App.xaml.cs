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
        if (Environment.GetCommandLineArgs().Any(a => string.Equals(a, "--scheduled-clean", StringComparison.OrdinalIgnoreCase)))
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
        LogCrash("XamlUnhandled", e.Exception);
        e.Handled = true;
    }

    public static void LogCrash(string source, Exception? ex)
    {
        try
        {
            var logFile = Path.Combine(AppData.Folder, "crash.log");
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {ex?.GetType().FullName}: {ex?.Message}\r\n{ex?.StackTrace}\r\n\r\n";
            File.AppendAllText(logFile, entry);
        }
        catch { }
    }
}
