namespace Lemon.Explore.Book.Chapters.Sound;

/// <summary>The views of the sound page.</summary>
public enum SoundMode
{
    /// <summary>A loudspeaker pushing the air: compressions and rarefactions travelling to an ear.</summary>
    Wave,

    /// <summary>A plucked guitar string and an air column in a pipe: standing waves and their harmonics.</summary>
    Strings,

    /// <summary>A keyboard and a few instruments: pitch, the octave, overtones and timbre.</summary>
    Timbre,
}

/// <summary>What the sound travels through.</summary>
public enum SoundMedium
{
    Air,
    Water,
    Steel,

    /// <summary>Nothing at all: the speaker moves, but no sound leaves it.</summary>
    Vacuum,
}

/// <summary>What vibrates in the strings view.</summary>
public enum Resonator
{
    String,

    /// <summary>An air column open at both ends (a flute).</summary>
    OpenPipe,

    /// <summary>An air column closed at the far end (a clarinet, a bottle).</summary>
    ClosedPipe,
}

/// <summary>The instruments of the timbre view, each a set of overtones and a way of starting and ending a note.</summary>
public enum Instrument
{
    Fork,
    Flute,
    Clarinet,
    Violin,
    Trumpet,
    Piano,

    /// <summary>The overtones dragged by hand.</summary>
    Custom,
}

/// <summary>A second note sounding with the first in the timbre view.</summary>
public enum SecondNote
{
    None,

    /// <summary>The same note a few hertz off: the two beat.</summary>
    Beats,

    /// <summary>Twelve semitones up: twice the frequency.</summary>
    Octave,

    /// <summary>Seven semitones up: about one and a half times the frequency.</summary>
    Fifth,
}

/// <summary>
/// How a note of an instrument starts and ends: the attack (seconds to full), how long it is held, the release (seconds
/// to silence), the decay time of its fundamental when it dies away by itself (0 for a held note), how much faster the
/// overtones die (τₙ = τ₁ / n^<see cref="OvertoneDecay"/>), and the vibrato's depth (a fraction of the pitch, at 5.5 Hz).
/// </summary>
public readonly record struct NoteEnvelope(double Attack, double Hold, double Release, double DecayTime, double OvertoneDecay, double Vibrato)
{
    /// <summary>The note's loudness (0–1) at <paramref name="t"/> seconds, the fundamental's decay included.</summary>
    public double At(double t)
    {
        if (t < 0) return 0;
        var level = Attack > 0 ? Math.Min(1, t / Attack) : 1;
        if (DecayTime > 0) level *= Math.Exp(-t / DecayTime);
        if (t > Hold) level *= Math.Max(0, 1 - ((t - Hold) / Math.Max(1e-3, Release)));
        return level;
    }

    /// <summary>When the note has died away.</summary>
    public double Length => Hold + Release;
}

/// <summary>
/// The physics of the sound page, in plain numbers:
/// <list type="bullet">
/// <item>Sound speed in dry air from the temperature (331.3 m/s at 0 °C, ∝ √T), and in water and steel; air density from
/// the ideal gas law at sea-level pressure.</item>
/// <item>Sound pressure level against the 20 µPa reference: pressure, the air's back-and-forth movement and intensity.</item>
/// <item>The threshold of hearing (Terhardt 1979's fit to the quiet-threshold curve).</item>
/// <item>Equal temperament on A4 = 440 Hz.</item>
/// <item>A plucked string (fixed ends; harmonic amplitudes of a triangle released from rest), its frequency from length,
/// tension and mass per metre, and the open and closed pipe's resonances with the open-end correction.</item>
/// <item>Rough overtone tables and envelopes of a few instruments (rounded, for illustration: real ones change with the
/// note, the player and the instrument).</item>
/// </list>
/// </summary>
public static class SoundModel
{
    /// <summary>The reference sound pressure for 0 dB, Pa (RMS).</summary>
    public const double ReferencePressure = 20e-6;

    /// <summary>Sea-level air pressure, Pa.</summary>
    public const double AirPressure = 101_325;

    /// <summary>Where sound starts to hurt, dB.</summary>
    public const double PainThreshold = 130;

    /// <summary>The lowest and highest pitch a young ear hears, Hz.</summary>
    public const double LowestHeard = 20;
    public const double HighestHeard = 20_000;

    /// <summary>The pitch every orchestra tunes to: A4, Hz.</summary>
    public const double ConcertA = 440;

    /// <summary>A guitar's scale length (nut to saddle), m.</summary>
    public const double GuitarScale = 0.648;

    /// <summary>The inner radius of the pipes shown, m (a flute's bore is about 19 mm across).</summary>
    public const double PipeRadius = 0.0095;

    #region the medium

    /// <summary>The speed of sound in dry air at <paramref name="celsius"/>, m/s.</summary>
    public static double SpeedInAir(double celsius) => 331.3 * Math.Sqrt(1 + (celsius / 273.15));

    /// <summary>Dry air's density at sea-level pressure, kg/m³ (1.204 at 20 °C).</summary>
    public static double AirDensity(double celsius) => AirPressure / (287.05 * (celsius + 273.15));

    /// <summary>The speed of sound in <paramref name="medium"/>, m/s (0 in a vacuum).</summary>
    public static double Speed(SoundMedium medium, double celsius) => medium switch
    {
        SoundMedium.Air => SpeedInAir(celsius),
        SoundMedium.Water => 1482,
        SoundMedium.Steel => 5960,
        _ => 0,
    };

    /// <summary>The density of <paramref name="medium"/>, kg/m³.</summary>
    public static double Density(SoundMedium medium, double celsius) => medium switch
    {
        SoundMedium.Air => AirDensity(celsius),
        SoundMedium.Water => 998,
        SoundMedium.Steel => 7850,
        _ => 0,
    };

    /// <summary>Wavelength, m: speed over frequency.</summary>
    public static double Wavelength(double hertz, double speed) => speed / hertz;

    #endregion

    #region loudness

    /// <summary>The RMS sound pressure of a level, Pa.</summary>
    public static double PressureRms(double decibels) => ReferencePressure * Math.Pow(10, decibels / 20);

    /// <summary>The peak sound pressure of a steady tone at a level, Pa.</summary>
    public static double PressureAmplitude(double decibels) => Math.Sqrt(2) * PressureRms(decibels);

    /// <summary>How far each bit of the medium moves either way in a tone, m: p / (ρ c ω).</summary>
    public static double DisplacementAmplitude(double decibels, double hertz, SoundMedium medium, double celsius)
    {
        var impedance = Density(medium, celsius) * Speed(medium, celsius);
        return impedance > 0 ? PressureAmplitude(decibels) / (impedance * 2 * Math.PI * hertz) : 0;
    }

    /// <summary>The fastest the medium moves in a tone, m/s: p / (ρ c).</summary>
    public static double VelocityAmplitude(double decibels, SoundMedium medium, double celsius)
    {
        var impedance = Density(medium, celsius) * Speed(medium, celsius);
        return impedance > 0 ? PressureAmplitude(decibels) / impedance : 0;
    }

    /// <summary>The power the sound carries through a square metre, W/m²: p²rms / (ρ c).</summary>
    public static double Intensity(double decibels, SoundMedium medium, double celsius)
    {
        var impedance = Density(medium, celsius) * Speed(medium, celsius);
        return impedance > 0 ? Math.Pow(PressureRms(decibels), 2) / impedance : 0;
    }

    /// <summary>
    /// The quietest tone heard at <paramref name="hertz"/>, dB (Terhardt 1979): high at the low end, lowest near 3–4 kHz
    /// where the ear canal resonates, rising steeply above 10 kHz.
    /// </summary>
    public static double HearingThreshold(double hertz)
    {
        var k = hertz / 1000;
        return (3.64 * Math.Pow(k, -0.8)) - (6.5 * Math.Exp(-0.6 * Math.Pow(k - 3.3, 2))) + (1e-3 * Math.Pow(k, 4));
    }

    /// <summary>A young listener would hear the tone.</summary>
    public static bool IsAudible(double hertz, double decibels) =>
        hertz >= LowestHeard && hertz <= HighestHeard && decibels >= HearingThreshold(hertz);

    /// <summary>A familiar sound about as loud as <paramref name="decibels"/>: the id of its words (Sound_Loud_*).</summary>
    public static string LoudnessExample(double decibels) => decibels switch
    {
        < 15 => "Breath",
        < 35 => "Whisper",
        < 50 => "Library",
        < 70 => "Talk",
        < 85 => "Traffic",
        < 100 => "Drill",
        < 115 => "Concert",
        < 125 => "Thunder",
        _ => "Jet",
    };

    #endregion

    #region notes

    private static readonly string[] NoteNames = ["C", "C♯", "D", "D♯", "E", "F", "F♯", "G", "G♯", "A", "A♯", "B"];

    /// <summary>The frequency of a piano key by its MIDI number (A4 = 69 = 440 Hz), equal temperament.</summary>
    public static double NoteFrequency(double midi) => ConcertA * Math.Pow(2, (midi - 69) / 12);

    /// <summary>A frequency as a (fractional) MIDI number.</summary>
    public static double Midi(double hertz) => 69 + (12 * Math.Log2(hertz / ConcertA));

    /// <summary>The name of a key, e.g. 60 → "C4", 69 → "A4".</summary>
    public static string NoteName(int midi) => $"{NoteNames[((midi % 12) + 12) % 12]}{(int)Math.Floor(midi / 12.0) - 1}";

    /// <summary>The nearest key to a frequency, e.g. 262 Hz → "C4".</summary>
    public static string NoteName(double hertz) => hertz > 0 && !double.IsInfinity(hertz) ? NoteName((int)Math.Round(Midi(hertz))) : "—";

    /// <summary>Whether a frequency falls on a piano's 88 keys (A0 to C8, about 27.5 Hz to 4.2 kHz).</summary>
    public static bool IsOnPiano(double hertz) => hertz > 0 && Math.Round(Midi(hertz)) is >= 21 and <= 108;

    /// <summary>How far a frequency is from its nearest key, in cents (hundredths of a semitone), −50 to 50.</summary>
    public static double Cents(double hertz)
    {
        var midi = Midi(hertz);
        return (midi - Math.Round(midi)) * 100;
    }

    /// <summary>A black key (C♯, D♯, F♯, G♯, A♯).</summary>
    public static bool IsBlack(int midi) => (((midi % 12) + 12) % 12) is 1 or 3 or 6 or 8 or 10;

    /// <summary>The frequency of the second note against a first note at <paramref name="hertz"/>.</summary>
    public static double SecondFrequency(double hertz, SecondNote second, double detune) => second switch
    {
        SecondNote.Beats => hertz + detune,
        SecondNote.Octave => hertz * 2,
        SecondNote.Fifth => hertz * Math.Pow(2, 7 / 12.0),
        _ => 0,
    };

    /// <summary>Semitones from the first note to the second (the beating note sits on the same key).</summary>
    public static int SecondSemitones(SecondNote second) => second switch
    {
        SecondNote.Octave => 12,
        SecondNote.Fifth => 7,
        _ => 0,
    };

    #endregion

    #region strings

    /// <summary>The open strings of a guitar in standard tuning, low to high: MIDI numbers (E2 A2 D3 G3 B3 E4).</summary>
    public static readonly IReadOnlyList<int> GuitarStrings = [40, 45, 50, 55, 59, 64];

    /// <summary>A typical tension of each guitar string (light steel strings), N.</summary>
    public static readonly IReadOnlyList<double> GuitarTensions = [71, 76, 78, 71, 58, 62];

    /// <summary>The fundamental of a string fixed at both ends, Hz: f₁ = √(T/μ) / 2L.</summary>
    public static double StringFrequency(double length, double tension, double massPerMetre) =>
        Math.Sqrt(tension / massPerMetre) / (2 * length);

    /// <summary>The mass per metre that tunes a string to <paramref name="hertz"/>, kg/m.</summary>
    public static double MassPerMetre(double hertz, double length, double tension) => tension / Math.Pow(2 * length * hertz, 2);

    /// <summary>How fast a ripple runs along the string, m/s: √(T/μ).</summary>
    public static double StringWaveSpeed(double tension, double massPerMetre) => Math.Sqrt(tension / massPerMetre);

    /// <summary>The length left vibrating with a finger behind fret <paramref name="fret"/>: each fret a semitone.</summary>
    public static double FrettedLength(double length, double fret) => length * Math.Pow(2, -fret / 12);

    /// <summary>
    /// The amplitude of harmonic <paramref name="n"/> of a string pulled aside by 1 at <paramref name="pluck"/> (a fraction
    /// of its length from the nut) and let go: 2 sin(nπβ) / (n²π² β(1 − β)). Zero for every harmonic with a node at the
    /// plucking point.
    /// </summary>
    public static double PluckAmplitude(int n, double pluck)
    {
        var beta = Math.Clamp(pluck, 1e-3, 1 - 1e-3);
        return 2 * Math.Sin(n * Math.PI * beta) / (n * n * Math.PI * Math.PI * beta * (1 - beta));
    }

    /// <summary>Whether harmonic <paramref name="n"/> is missing because it has a node near the plucking point.</summary>
    public static bool IsSilenced(int n, double pluck) => Math.Abs(Math.Sin(n * Math.PI * pluck)) < 0.08;

    /// <summary>How long harmonic <paramref name="n"/> of a guitar string rings (to 1/e), s: higher ones die sooner.</summary>
    public static double StringDecay(int n) => 2.4 / Math.Pow(n, 0.75);

    /// <summary>
    /// The string's shape at <paramref name="x"/> (0 at the nut, 1 at the bridge) after <paramref name="periods"/>
    /// periods of its fundamental: the sum of its modes, each fading as <paramref name="fade"/>(n) says.
    /// </summary>
    public static double StringShape(double x, double periods, double pluck, int modes, Func<int, double> fade)
    {
        var y = 0.0;
        for (var n = 1; n <= modes; n++)
        {
            var a = PluckAmplitude(n, pluck) * fade(n);
            if (a == 0) continue;
            y += a * Math.Sin(n * Math.PI * x) * Math.Cos(2 * Math.PI * n * periods);
        }

        return y;
    }

    #endregion

    #region pipes

    /// <summary>The harmonic number of a pipe's <paramref name="mode"/>th resonance: every one open, odd ones closed.</summary>
    public static int PipeHarmonic(int mode, bool closed) => closed ? (2 * mode) - 1 : mode;

    /// <summary>The pipe's acoustic length, m: each open end reaches 0.61 radii past the tube.</summary>
    public static double EffectiveLength(double length, bool closed) => length + (0.61 * PipeRadius * (closed ? 1 : 2));

    /// <summary>The <paramref name="mode"/>th resonance of a pipe, Hz: n v / 2L open, (2n − 1) v / 4L closed.</summary>
    public static double PipeFrequency(int mode, double length, double speed, bool closed)
    {
        var harmonic = PipeHarmonic(mode, closed);
        return harmonic * speed / ((closed ? 4 : 2) * EffectiveLength(length, closed));
    }

    /// <summary>
    /// How far the air moves in the pipe's mode (−1…1) at <paramref name="x"/> (0 at the mouth, 1 at the far end): open
    /// ends are where it moves most, a closed end holds it still.
    /// </summary>
    public static double PipeDisplacement(int mode, double x, bool closed) => closed
        ? Math.Cos(((2 * mode) - 1) * Math.PI * x / 2)
        : Math.Cos(mode * Math.PI * x);

    /// <summary>How much the pressure swings in the pipe's mode (−1…1): the other way round, most at a closed end.</summary>
    public static double PipePressure(int mode, double x, bool closed) => closed
        ? Math.Sin(((2 * mode) - 1) * Math.PI * x / 2)
        : Math.Sin(mode * Math.PI * x);

    #endregion

    #region timbre

    /// <summary>The number of overtones each instrument has here (the fundamental first).</summary>
    public const int HarmonicCount = 12;

    /// <summary>
    /// Relative amplitudes of an instrument's first <see cref="HarmonicCount"/> harmonics (rounded, a middle note, for
    /// illustration). A fork is almost pure; a clarinet's low notes are mostly odd harmonics; a violin's bowed string is
    /// close to a sawtooth; a trumpet's are strongest a few harmonics up.
    /// </summary>
    public static double[] Harmonics(Instrument instrument) => instrument switch
    {
        Instrument.Fork => [1, 0.02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        Instrument.Flute => [1, 0.5, 0.22, 0.1, 0.05, 0.03, 0.015, 0.01, 0, 0, 0, 0],
        Instrument.Clarinet => [1, 0.04, 0.65, 0.03, 0.42, 0.03, 0.25, 0.02, 0.14, 0.01, 0.07, 0.01],
        Instrument.Violin => [1, 0.62, 0.5, 0.38, 0.33, 0.26, 0.2, 0.17, 0.13, 0.1, 0.08, 0.06],
        Instrument.Trumpet => [0.55, 0.85, 1, 0.9, 0.7, 0.52, 0.38, 0.27, 0.18, 0.12, 0.08, 0.05],
        Instrument.Piano => [1, 0.55, 0.35, 0.22, 0.16, 0.1, 0.07, 0.05, 0.03, 0.02, 0.015, 0.01],
        _ => [1, 0.5, 0.33, 0.25, 0.2, 0.17, 0.14, 0.12, 0.11, 0.1, 0.09, 0.08],
    };

    /// <summary>How a note of the instrument starts, lasts and ends.</summary>
    public static NoteEnvelope Envelope(Instrument instrument) => instrument switch
    {
        Instrument.Fork => new(0.004, 3.2, 0.4, 2.2, 0, 0),
        Instrument.Flute => new(0.09, 1.3, 0.15, 0, 0, 0.004),
        Instrument.Clarinet => new(0.05, 1.3, 0.12, 0, 0, 0),
        Instrument.Violin => new(0.16, 1.3, 0.25, 0, 0, 0.006),
        Instrument.Trumpet => new(0.06, 1.3, 0.12, 0, 0, 0.002),
        Instrument.Piano => new(0.004, 2.6, 0.35, 1.3, 0.6, 0),
        _ => new(0.04, 1.3, 0.15, 0, 0, 0),
    };

    /// <summary>
    /// One period of a wave made of <paramref name="harmonics"/> at <paramref name="phase"/> (0–1 a period), all sines in
    /// step: the shape a microphone would draw (the ear does not hear the phases, only the amplitudes).
    /// </summary>
    public static double Waveform(IReadOnlyList<double> harmonics, double phase)
    {
        var y = 0.0;
        for (var n = 1; n <= harmonics.Count; n++) y += harmonics[n - 1] * Math.Sin(2 * Math.PI * n * phase);
        return y;
    }

    /// <summary>The largest swing of <see cref="Waveform"/> over a period, to scale it into a box.</summary>
    public static double WaveformPeak(IReadOnlyList<double> harmonics)
    {
        var peak = 1e-6;
        for (var i = 0; i < 512; i++) peak = Math.Max(peak, Math.Abs(Waveform(harmonics, i / 512.0)));
        return peak;
    }

    #endregion
}
