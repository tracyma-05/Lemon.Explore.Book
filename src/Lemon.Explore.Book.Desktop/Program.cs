using Avalonia;
using Lemon.Explore.Book.Infrastructures.Settings;

namespace Lemon.Explore.Book.Desktop;

internal static class Program
{
    // Avalonia, third-party APIs and anything relying on a SynchronizationContext are not ready until
    // AppMain runs; keep Main to configuration.
    [STAThread]
    public static void Main(string[] args)
    {
        App.Settings = new JsonFileSettingsStore(JsonFileSettingsStore.DefaultPath());
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
