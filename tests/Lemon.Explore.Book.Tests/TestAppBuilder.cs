using Avalonia;
using Avalonia.Headless;
using Lemon.Explore.Book;
using Lemon.Explore.Book.Tests;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

// Sequential on purpose: the headless platform does not like parallel start-up, and several tests change
// process-wide state (the current culture, LocalizationService.Instance).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Lemon.Explore.Book.Tests;

/// <summary>
/// Headless host for <c>[AvaloniaFact]</c> tests. Uses the real <see cref="App"/>, so the book's styles load;
/// the headless platform has no desktop lifetime, so App opens no window of its own. Skia rendering (rather
/// than headless drawing) lets a test capture a frame.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
