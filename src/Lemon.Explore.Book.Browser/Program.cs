using System.Runtime.InteropServices.JavaScript;
using Avalonia;
using Avalonia.Browser;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Lemon.Book.Shell.Browser;
using Lemon.Explore.Book;
using Lemon.Explore.Book.Browser;
using Lemon.Explore.Book.Chapters.Sound;
using Lemon.Explore.Book.Controls.Space;

// The prefix the book has always saved under, so a reader's progress is still there.
App.Settings = new LocalStorageSettingsStore("explore-book:");
await JSHost.ImportAsync(WebNarrationAudio.ModuleName, WebNarrationAudio.ModuleUrl);
App.NarrationAudio = new WebNarrationAudio();
await JSHost.ImportAsync(WebSynthOutput.ModuleName, WebSynthOutput.ModuleUrl);
SoundEngine.OpenOutput = WebSynthOutput.TryOpen;

// The browser runs the planet texture generator on its only thread; a smaller map keeps the first frames responsive.
PlanetTexture.PlanetWidth = 512;

await AppBuilder.Configure<App>()
    .ConfigureFonts(fonts => fonts.AddFontCollection(new EmbeddedFontCollection(
        new Uri("fonts:Book", UriKind.Absolute),
        new Uri("avares://Lemon.Explore.Book.Browser/Assets/Fonts", UriKind.Absolute))))
    .With(new FontManagerOptions { DefaultFamilyName = "fonts:Book#Noto Sans SC" })
    .StartBrowserAppAsync("out");
