namespace Lemon.Explore.Book.Chapters.Sound;

/// <summary>
/// A note to sound: its pitch, the amplitude of each harmonic (the fundamental first), how long each rings when it dies
/// away by itself (0 for held), the envelope and a loudness from 0 to 1.
/// </summary>
public sealed record SoundNote(double Frequency, IReadOnlyList<double> Amplitudes, IReadOnlyList<double> DecayTimes, NoteEnvelope Envelope, double Gain);

/// <summary>
/// The sound page's synthesizer: a steady tone (the loudspeaker's) and up to <see cref="MaxVoices"/> notes, each the sum
/// of its harmonics (additive synthesis from a sine table). <see cref="Render"/> runs on the audio thread; everything
/// else may be called from the UI thread. Pitch and loudness changes glide, so moving a slider never clicks.
/// </summary>
public sealed class SoundSynth
{
    public const int MaxVoices = 8;

    private const int TableSize = 4096;
    private static readonly float[] Sine = BuildSine();

    private readonly object _gate = new();
    private readonly List<Voice> _voices = [];
    private readonly double _rate;

    private double _toneTargetHz = 440;
    private double _toneTargetGain;
    private double _toneHz = 440;
    private double _toneGain;
    private double _tonePhase;

    private double _duckTarget = 1;
    private double _duck = 1;

    public SoundSynth(int sampleRate = 44_100)
    {
        SampleRate = sampleRate;
        _rate = sampleRate;
    }

    public int SampleRate { get; }

    /// <summary>The page's own loudness, 0–1 (the speaker icon mutes with 0).</summary>
    public double Volume { get; set; } = 1;

    /// <summary>True while anything is sounding or fading out.</summary>
    public bool IsSounding
    {
        get
        {
            lock (_gate) return _voices.Count > 0 || _toneGain > 1e-4 || _toneTargetGain > 0;
        }
    }

    /// <summary>The steady tone: its pitch and loudness (0 stops it, fading).</summary>
    public void SetTone(double hertz, double gain)
    {
        lock (_gate)
        {
            _toneTargetHz = Math.Clamp(hertz, 10, 22_000);
            _toneTargetGain = Math.Clamp(gain, 0, 1);
            if (_toneGain < 1e-4) _toneHz = _toneTargetHz;
        }
    }

    /// <summary>Quieter while the narration speaks (true), back up after.</summary>
    public void Duck(bool ducked)
    {
        lock (_gate) _duckTarget = ducked ? 0.22 : 1;
    }

    /// <summary>Starts a note; the oldest one makes way when too many sound.</summary>
    public void Play(SoundNote note)
    {
        var voice = new Voice(note, _rate);
        lock (_gate)
        {
            if (_voices.Count >= MaxVoices) _voices.RemoveAt(0);
            _voices.Add(voice);
        }
    }

    /// <summary>Lets every note go (a quick fade) and stops the tone.</summary>
    public void Release()
    {
        lock (_gate)
        {
            foreach (var voice in _voices) voice.ReleaseSoon();
            _toneTargetGain = 0;
        }
    }

    /// <summary>Fills <paramref name="buffer"/> with the next samples (−1…1).</summary>
    public void Render(Span<float> buffer)
    {
        buffer.Clear();
        lock (_gate)
        {
            var glide = 1 - Math.Exp(-1 / (0.03 * _rate));
            var fade = 1 - Math.Exp(-1 / (0.012 * _rate));
            if (_toneGain > 1e-5 || _toneTargetGain > 0)
            {
                for (var i = 0; i < buffer.Length; i++)
                {
                    _toneHz *= Math.Exp(Math.Log(_toneTargetHz / _toneHz) * glide);
                    _toneGain += (_toneTargetGain - _toneGain) * fade;
                    _tonePhase += _toneHz / _rate;
                    _tonePhase -= Math.Floor(_tonePhase);
                    buffer[i] = (float)(_toneGain * Lookup(_tonePhase));
                }
            }
            else
            {
                _toneGain = 0;
                _toneHz = _toneTargetHz;
            }

            for (var v = _voices.Count - 1; v >= 0; v--)
            {
                var voice = _voices[v];
                voice.Add(buffer);
                if (voice.IsDone) _voices.RemoveAt(v);
            }

            // The page's volume and the narration's ducking, then a soft limit so loud chords never clip harshly.
            var duckFade = 1 - Math.Exp(-1 / (0.12 * _rate));
            var volume = (float)Math.Clamp(Volume, 0, 1);
            for (var i = 0; i < buffer.Length; i++)
            {
                _duck += (_duckTarget - _duck) * duckFade;
                var x = buffer[i] * (float)_duck * volume;
                buffer[i] = MathF.Abs(x) < 0.6f ? x : MathF.Sign(x) * (0.6f + (0.4f * MathF.Tanh((MathF.Abs(x) - 0.6f) / 0.4f)));
            }
        }
    }

    /// <summary>The sine of a phase given in turns (0–1), from the table with linear interpolation.</summary>
    private static double Lookup(double turns)
    {
        var position = turns * TableSize;
        var index = (int)position;
        var frac = position - index;
        index &= TableSize - 1;
        return Sine[index] + ((Sine[(index + 1) & (TableSize - 1)] - Sine[index]) * frac);
    }

    private static float[] BuildSine()
    {
        var table = new float[TableSize];
        for (var i = 0; i < TableSize; i++) table[i] = (float)Math.Sin(2 * Math.PI * i / TableSize);
        return table;
    }

    /// <summary>One note being played.</summary>
    private sealed class Voice
    {
        private readonly double _rate;
        private readonly double _hz;
        private readonly double[] _amplitudes;
        private readonly double[] _decays;
        private readonly double[] _shrink;
        private readonly int _count;
        private readonly NoteEnvelope _envelope;
        private readonly double _gain;
        private readonly double _end;
        private double _phase;
        private double _t;
        private double _releaseAt = double.PositiveInfinity;

        public Voice(SoundNote note, double rate)
        {
            _rate = rate;
            _hz = note.Frequency;
            _envelope = note.Envelope;
            _gain = note.Gain;

            // Only the harmonics below the top of hearing (and the sample rate's limit) are worth adding.
            var limit = Math.Min(19_000, rate * 0.45);
            _count = 0;
            while (_count < note.Amplitudes.Count && (_count + 1) * _hz < limit) _count++;
            _amplitudes = new double[_count];
            _decays = new double[_count];
            var total = 0.0;
            for (var n = 0; n < _count; n++)
            {
                _amplitudes[n] = note.Amplitudes[n];
                _decays[n] = n < note.DecayTimes.Count ? note.DecayTimes[n] : 0;
                total += Math.Abs(_amplitudes[n]);
            }

            // Scaled so a note of many harmonics is not much louder than a pure one.
            var scale = total > 0 ? 1 / Math.Max(1, Math.Sqrt(total) * 1.2) : 0;
            for (var n = 0; n < _count; n++) _amplitudes[n] *= scale;

            // Each harmonic's amplitude shrinks by a fixed factor every sample (exp(−t/τ) without an exp per sample).
            _shrink = _decays.Select(tau => tau > 0 ? Math.Exp(-1 / (tau * rate)) : 1).ToArray();
            var longest = _decays.Length > 0 ? _decays.Max() : 0;
            _end = longest > 0 && !_decays.Contains(0) ? Math.Min(_envelope.Length, longest * 7) : _envelope.Length;
        }

        public bool IsDone => _t >= _end || _t >= _releaseAt + 0.05;

        public void ReleaseSoon() => _releaseAt = Math.Min(_releaseAt, _t);

        public void Add(Span<float> buffer)
        {
            var dt = 1 / _rate;
            for (var i = 0; i < buffer.Length && !IsDone; i++)
            {
                var level = _envelope.Attack > 0 ? Math.Min(1, _t / _envelope.Attack) : 1;
                if (_t > _envelope.Hold) level *= Math.Max(0, 1 - ((_t - _envelope.Hold) / Math.Max(1e-3, _envelope.Release)));
                if (_t > _releaseAt) level *= Math.Max(0, 1 - ((_t - _releaseAt) / 0.05));
                var vibrato = _envelope.Vibrato > 0 ? 1 + (_envelope.Vibrato * Math.Sin(2 * Math.PI * 5.5 * _t) * Math.Min(1, _t / 0.4)) : 1;
                _phase += _hz * vibrato * dt;
                _phase -= Math.Floor(_phase);

                var sample = 0.0;
                for (var n = 0; n < _count; n++)
                {
                    var a = _amplitudes[n];
                    if (a == 0) continue;
                    _amplitudes[n] = a * _shrink[n];
                    var turns = (n + 1) * _phase;
                    sample += a * Lookup(turns - Math.Floor(turns));
                }

                buffer[i] += (float)(sample * level * _gain);
                _t += dt;
            }
        }
    }
}
