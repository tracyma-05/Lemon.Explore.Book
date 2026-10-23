using Avalonia.Threading;

namespace Lemon.Explore.Book.Chapters.Sound;

/// <summary>
/// The sound page's audio: a <see cref="SoundSynth"/> and the device it plays on. The device is opened only when
/// something is played and closed again after a few quiet seconds; while the narration speaks, the page's own sound is
/// turned down (<paramref name="isNarrating"/>). Nothing here throws when there is no sound device: it stays silent.
/// </summary>
public sealed class SoundEngine : IDisposable
{
    /// <summary>
    /// Opens this platform's sound device for a synthesizer, or returns null when there is none. Each head sets its own
    /// (waveOut on Windows, an audio queue on macOS, Web Audio in the browser); tests leave the default, so they are silent.
    /// </summary>
    public static Func<SoundSynth, IDisposable?> OpenOutput { get; set; } = _ => null;

    /// <summary>Quiet seconds before the device is let go.</summary>
    private const double IdleSeconds = 8;

    private readonly SoundSynth _synth = new();
    private readonly Func<bool> _isNarrating;
    private readonly DispatcherTimer _timer;
    private IDisposable? _output;
    private bool _unavailable;
    private double _idle;
    private bool _enabled = true;

    public SoundEngine(Func<bool> isNarrating)
    {
        _isNarrating = isNarrating;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += OnTick;
    }

    /// <summary>Sound on (the speaker icon); off silences everything at once.</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            _synth.Volume = value ? 1 : 0;
            if (!value) _synth.Release();
        }
    }

    /// <summary>A device could not be opened: the page plays nothing.</summary>
    public bool IsUnavailable => _unavailable;

    /// <summary>Plays a note (if sound is on).</summary>
    public void Play(SoundNote note)
    {
        if (!_enabled || !Open()) return;
        _synth.Play(note);
    }

    /// <summary>The loudspeaker's steady tone; a gain of 0 stops it.</summary>
    public void SetTone(double hertz, double gain)
    {
        if (gain > 0 && (!_enabled || !Open())) return;
        _synth.SetTone(hertz, gain);
    }

    /// <summary>Lets everything fade out.</summary>
    public void Stop() => _synth.Release();

    private bool Open()
    {
        _idle = 0;
        if (_output is not null) return true;
        if (_unavailable) return false;
        _output = OpenOutput(_synth);
        _unavailable = _output is null;
        if (_output is not null) _timer.Start();
        return _output is not null;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _synth.Duck(_isNarrating());
        if (_synth.IsSounding)
        {
            _idle = 0;
            return;
        }

        _idle += _timer.Interval.TotalSeconds;
        if (_idle < IdleSeconds) return;
        _timer.Stop();
        _output?.Dispose();
        _output = null;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        _output?.Dispose();
        _output = null;
    }
}
