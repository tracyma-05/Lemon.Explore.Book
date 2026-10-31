using Avalonia;
using Lemon.Book.Shell.Desktop.Narration;
using Lemon.Book.Shell.Infrastructures.Settings;
using Lemon.Book.Shell.Updates;
using Lemon.Explore.Book.Chapters.Sound;
using Lemon.Explore.Book.Desktop.Sound;

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
        // The folder the book has always kept its settings in, so a reader's progress is still there.
        App.Settings = new JsonFileSettingsStore(JsonFileSettingsStore.DefaultPath("Lemon.Explore.Book"));
        App.NarrationAudio = DesktopNarrationAudio.Create();
        SoundEngine.OpenOutput = DesktopSynthOutput.Open;
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
