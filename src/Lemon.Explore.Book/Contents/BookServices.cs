using Lemon.Explore.Book.Explore.Narration;
using Lemon.Explore.Book.Infrastructures.Settings;

namespace Lemon.Explore.Book.Contents;

/// <summary>What the chapters share: the reader's settings and the one narration channel.</summary>
public sealed record BookServices(ISettingsStore Settings, INarrationService Narration)
{
    /// <summary>
    /// Services over <paramref name="settings"/>. Without audio (none ships yet, and tests have none) narration runs
    /// as timed subtitles.
    /// </summary>
    public static BookServices Create(ISettingsStore settings, NarrationManifest? manifest = null, INarrationAudio? audio = null) =>
        new(settings, new NarrationService(settings, manifest ?? NarrationManifest.Empty, audio ?? SilentNarrationAudio.Instance));
}
