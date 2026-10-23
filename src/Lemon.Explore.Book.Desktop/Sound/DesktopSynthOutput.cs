using Lemon.Explore.Book.Chapters.Sound;

namespace Lemon.Explore.Book.Desktop.Sound;

/// <summary>The desktop's sound device for the Sound chapter's synthesizer (see <see cref="SoundEngine.OpenOutput"/>).</summary>
internal static class DesktopSynthOutput
{
    public static IDisposable? Open(SoundSynth synth)
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsSynthOutput.TryOpen(synth);
        }

        if (OperatingSystem.IsMacOS())
        {
            return MacSynthOutput.TryOpen(synth);
        }

        return null;
    }
}
