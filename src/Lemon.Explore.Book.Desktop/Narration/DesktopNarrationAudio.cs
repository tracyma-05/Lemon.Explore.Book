using Lemon.Explore.Book.Explore.Narration;

namespace Lemon.Explore.Book.Desktop.Narration;

/// <summary>
/// Narration clips from a <c>narration</c> folder installed with the program (in a macOS bundle, under
/// <c>Contents/Resources</c>), played by the operating system: no audio library to ship.
/// </summary>
internal abstract class DesktopNarrationAudio : INarrationAudio
{
    private readonly string _folder;

    protected DesktopNarrationAudio(string folder)
    {
        _folder = folder;
    }

    /// <summary>The player for this system, or silence where there is none or no clips are installed.</summary>
    public static INarrationAudio Create()
    {
        var folder = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "narration"),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources", "narration")),
            }
            .FirstOrDefault(Directory.Exists);
        if (folder is null)
        {
            return SilentNarrationAudio.Instance;
        }

        if (OperatingSystem.IsWindows())
        {
            return new WindowsNarrationAudio(folder);
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacNarrationAudio(folder);
        }

        return SilentNarrationAudio.Instance;
    }

    public bool Has(string clip) => File.Exists(FullPath(clip));

    public abstract bool Play(string clip, double volume);

    public abstract void Pause();

    public abstract void Resume();

    public abstract void Stop();

    public abstract double Volume { set; }

    public abstract double Position { get; }

    public abstract bool IsEnded { get; }

    // Both players report a file they cannot open when Play is called; neither tells us about a failure part-way.
    public event EventHandler? Failed
    {
        add { }
        remove { }
    }

    protected string FullPath(string clip) => Path.Combine(_folder, clip);
}
