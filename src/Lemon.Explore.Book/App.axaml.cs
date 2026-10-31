using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lemon.Book.Shell;
using Lemon.Book.Shell.Explore.Narration;
using Lemon.Book.Shell.Infrastructures.Localization;
using Lemon.Book.Shell.Infrastructures.Settings;
using Lemon.Book.Shell.Infrastructures.Theming;
using Lemon.Explore.Book.Contents;
using Lemon.Explore.Book.ViewModels;
using Lemon.Explore.Book.Views;

namespace Lemon.Explore.Book;

public partial class App : Application
{
    static App() => ConfigureShell();

    /// <summary>
    /// What the shell (Lemon.Book.Shell) needs to know of this book: its assembly (strings, narration, build
    /// metadata), its page count and its languages, English first. Done before anything else. The book has no
    /// sign-in, so its sync name is never used.
    /// </summary>
    public static void ConfigureShell() =>
        BookShell.Configure(new BookShellOptions(typeof(App).Assembly, "explore-book", () => BookContents.PageCount)
        {
            Cultures = ["en", "zh-CN"],
        });

    /// <summary>
    /// Where reader preferences are kept. Each head sets its own store before starting the app; tests and
    /// the designer get one that forgets everything.
    /// </summary>
    public static ISettingsStore Settings { get; set; } = new MemorySettingsStore();

    /// <summary>Plays narration clips. Each head sets its own player; without one, narration is timed subtitles.</summary>
    public static INarrationAudio NarrationAudio { get; set; } = SilentNarrationAudio.Instance;

#if DEBUG
    // Developer tools attach once per process; headless tests start a fresh Application for every test.
    private static bool s_developerToolsAttached;
#endif

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        if (!s_developerToolsAttached)
        {
            s_developerToolsAttached = true;
            this.AttachDeveloperTools();
        }
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var localization = LocalizationService.Instance;
        localization.SetCulture(localization.ResolveSupportedCulture(Settings.Get(SettingKeys.Culture)));
        BookTheme.Apply(this, Settings);

        var book = new MainViewModel(BookServices.Create(Settings, NarrationManifest.Bundled, NarrationAudio));
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = book };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new MainView { DataContext = book };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
