using Avalonia;
using Avalonia.Browser;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Lemon.Explore.Book;
using Lemon.Explore.Book.Browser;

App.Settings = new LocalStorageSettingsStore();

await AppBuilder.Configure<App>()
    .ConfigureFonts(fonts => fonts.AddFontCollection(new EmbeddedFontCollection(
        new Uri("fonts:Book", UriKind.Absolute),
        new Uri("avares://Lemon.Explore.Book.Browser/Assets/Fonts", UriKind.Absolute))))
    .With(new FontManagerOptions { DefaultFamilyName = "fonts:Book#Noto Sans SC" })
    .StartBrowserAppAsync("out");
