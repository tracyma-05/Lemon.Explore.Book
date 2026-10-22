using Avalonia;
using Avalonia.Browser;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Lemon.Explore.Book;
using Lemon.Explore.Book.Browser;
using Lemon.Explore.Book.Controls.Space;

App.Settings = new LocalStorageSettingsStore();

// The browser runs the planet texture generator on its only thread; a smaller map keeps the first frames responsive.
PlanetTexture.PlanetWidth = 512;

await AppBuilder.Configure<App>()
    .ConfigureFonts(fonts => fonts.AddFontCollection(new EmbeddedFontCollection(
        new Uri("fonts:Book", UriKind.Absolute),
        new Uri("avares://Lemon.Explore.Book.Browser/Assets/Fonts", UriKind.Absolute))))
    .With(new FontManagerOptions { DefaultFamilyName = "fonts:Book#Noto Sans SC" })
    .StartBrowserAppAsync("out");
