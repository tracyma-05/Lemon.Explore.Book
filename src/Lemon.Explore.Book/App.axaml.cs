using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Settings;
using Lemon.Explore.Book.ViewModels;
using Lemon.Explore.Book.Views;

namespace Lemon.Explore.Book;

public partial class App : Application
{
    /// <summary>
    /// Where reader preferences are kept. Each head sets its own store before starting the app; tests and
    /// the designer get one that forgets everything.
    /// </summary>
    public static ISettingsStore Settings { get; set; } = new MemorySettingsStore();

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

        var book = new MainViewModel(Settings);
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
