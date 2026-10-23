using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Avalonia.Threading;
using Lemon.Explore.Book.Chapters.Sound;

namespace Lemon.Explore.Book.Browser;

/// <summary>
/// The Sound chapter's synthesizer through Web Audio (<c>wwwroot/synth.js</c>). There is no audio thread in the
/// browser, so a timer on the UI thread keeps about a fifth of a second rendered ahead: enough to ride over a busy
/// frame, little enough that a pluck still follows the click.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed partial class WebSynthOutput : IDisposable
{
    public const string ModuleName = "synth";

    /// <summary>Relative to <c>_framework/dotnet.js</c>, which imports it.</summary>
    public const string ModuleUrl = "../synth.js";

    private const int ChunkSamples = 2048;
    private const double LeadSeconds = 0.2;

    private readonly SoundSynth _synth;
    private readonly DispatcherTimer _timer;
    private readonly float[] _samples = new float[ChunkSamples];
    private readonly double[] _chunk = new double[ChunkSamples];

    private WebSynthOutput(SoundSynth synth)
    {
        _synth = synth;
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(30) };
        _timer.Tick += (_, _) => Fill();
        Open();
        Fill();
        _timer.Start();
    }

    public static IDisposable? TryOpen(SoundSynth synth) => new WebSynthOutput(synth);

    private void Fill()
    {
        // Bounded, so a long stall (a hidden tab) never queues seconds of sound at once.
        for (var i = 0; i < 8 && Ahead() < LeadSeconds; i++)
        {
            _synth.Render(_samples);
            for (var s = 0; s < ChunkSamples; s++)
            {
                _chunk[s] = _samples[s];
            }

            Push(_chunk, _synth.SampleRate);
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        Close();
    }

    [JSImport("open", ModuleName)]
    private static partial void Open();

    [JSImport("ahead", ModuleName)]
    private static partial double Ahead();

    [JSImport("push", ModuleName)]
    private static partial void Push(double[] samples, int sampleRate);

    [JSImport("close", ModuleName)]
    private static partial void Close();
}
