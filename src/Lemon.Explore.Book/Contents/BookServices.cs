using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Explore.Places;
using Lemon.Explore.Book.Infrastructures.Settings;

namespace Lemon.Explore.Book.Contents;

/// <summary>
/// What the book shares: the reader's settings, the one narration channel, the observer location, the bookmarks and
/// the reading progress.
/// </summary>
public sealed record BookServices(
    ISettingsStore Settings, INarrationService Narration, IExploreLocationService Location, Bookmarks Bookmarks, ReadingProgress Progress)
{
    /// <summary>
    /// Services over <paramref name="settings"/>. Without audio (none ships yet, and tests have none) narration runs
    /// as timed subtitles.
    /// </summary>
    public static BookServices Create(ISettingsStore settings, NarrationManifest? manifest = null, INarrationAudio? audio = null) =>
        new(settings,
            new NarrationService(settings, manifest ?? NarrationManifest.Empty, audio ?? SilentNarrationAudio.Instance),
            new ExploreLocationService(settings),
            new Bookmarks(settings),
            new ReadingProgress(settings));
}
