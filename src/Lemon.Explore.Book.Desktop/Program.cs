using Avalonia;
using Lemon.Explore.Book.Infrastructures.Settings;
using Lemon.Explore.Book.Updates;

namespace Lemon.Explore.Book.Desktop;

internal static class Program
{
    // Avalonia, third-party APIs and anything relying on a SynchronizationContext are not ready until
    // AppMain runs; keep Main to configuration.
    [STAThread]
    public static int Main(string[] args)
    {
        // Started by the previous version to finish an update: swap the installation, start it, and exit.
        if (UpdateApplier.TryRun(args, out var exitCode))
        {
            return exitCode;
        }

        AppInstallation.MoveWorkingDirectoryOut();
        App.Settings = new JsonFileSettingsStore(JsonFileSettingsStore.DefaultPath());
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
