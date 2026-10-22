using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Waves;

/// <summary>What the waves page shows.</summary>
public enum WaveMode
{
    /// <summary>Two dippers in a ripple tank: crests meeting crests and crests meeting troughs.</summary>
    Ripple,

    /// <summary>Light through two narrow slits: the same overlap, as bright and dark stripes on a screen.</summary>
    Slits,

    /// <summary>A moving source of sound: its waves bunched up ahead and stretched out behind.</summary>
    Doppler,
}

/// <summary>How the waves from the two sources meet at a point.</summary>
public enum Meeting
{
    /// <summary>Crest on crest and trough on trough: they add up.</summary>
    InStep,

    /// <summary>Neither quite in step nor quite out of it.</summary>
    Between,

    /// <summary>Crest on trough: they cancel.</summary>
    OutOfStep,
}

/// <summary>What the listener of the Doppler view hears.</summary>
public enum Hearing
{
    /// <summary>The source stands still: its own pitch.</summary>
    Still,

    /// <summary>The source comes closer: a higher pitch.</summary>
    Approaching,

    /// <summary>The source goes past: about its own pitch.</summary>
    Passing,

    /// <summary>The source goes away: a lower pitch.</summary>
    Receding,

    /// <summary>Faster than sound and not here yet: nothing at all.</summary>
    Silent,

    /// <summary>The cone of piled-up sound passes: a bang.</summary>
    Boom,

    /// <summary>Faster than sound, after the bang: the sound it made coming and going, both at once.</summary>
    Two,
}

/// <summary>A sound reaching the listener: when the source made it (seconds) and the pitch it arrives at (Hz).</summary>
public readonly record struct HeardSound(double Emitted, double Frequency, double CosAngle);

/// <summary>
/// The physics of the waves page. Ripple tank: two dippers in step (or opposite), each sending out rings that fade as
/// 1/√r, in centimetres with the midpoint between the dippers at the origin, A on the left and B on the right. Light:
/// Young's two slits, a screen metres away, positions on it in millimetres. Sound: a source moving along the x axis at a
/// Mach number, a listener anywhere, in metres and real seconds.
/// </summary>
public static class WavePhysics
{
    #region ripple tank

    /// <summary>Speed of ripples in a shallow tank (a few millimetres of water), cm/s.</summary>
    public const double RippleSpeed = 22;

    /// <summary>How many times slower than real the ripple tank and the probe chart run.</summary>
    public const double RippleSlowdown = 10;

    /// <summary>Ripple frequency for a wavelength in cm, Hz.</summary>
    public static double RippleFrequency(double wavelength) => RippleSpeed / wavelength;

    public static Point SourceA(double separation) => new(-separation / 2, 0);

    public static Point SourceB(double separation) => new(separation / 2, 0);

    /// <summary>A ring's height at <paramref name="distance"/> from its dipper, 1 a wavelength away: it spreads as 1/√r.</summary>
    public static double RippleAmplitude(double distance, double wavelength) =>
        Math.Sqrt(wavelength / Math.Max(distance, 0.4 * wavelength));

    /// <summary>How much farther <paramref name="point"/> is from A than from B, cm.</summary>
    public static double PathDifference(Point point, double separation) =>
        ((Vector)(point - SourceA(separation))).Length - ((Vector)(point - SourceB(separation))).Length;

    /// <summary>
    /// How far apart in their rise and fall the two waves arrive at a point, radians in (−π, π]: 0 in step, π opposite.
    /// </summary>
    public static double PhaseGap(double pathDifference, double wavelength, bool opposite)
    {
        var gap = (2 * Math.PI * pathDifference / wavelength) + (opposite ? Math.PI : 0);
        gap = Math.IEEERemainder(gap, 2 * Math.PI);
        return gap <= -Math.PI ? gap + (2 * Math.PI) : gap;
    }

    /// <summary>In step within 40°, opposite within 40°, else between.</summary>
    public static Meeting MeetingOf(double phaseGap)
    {
        var cos = Math.Cos(phaseGap);
        return cos > 0.766 ? Meeting.InStep : cos < -0.766 ? Meeting.OutOfStep : Meeting.Between;
    }

    /// <summary>
    /// The swing of each wave at a point and of the two together, and the together as a share of both added (1: fully in
    /// step, 0: cancelled). Without B, only A's wave.
    /// </summary>
    public static (double A, double B, double Together, double Share) Combined(Point point, double separation, double wavelength,
        bool opposite, bool second)
    {
        var rA = ((Vector)(point - SourceA(separation))).Length;
        var rB = ((Vector)(point - SourceB(separation))).Length;
        var a = RippleAmplitude(rA, wavelength);
        if (!second) return (a, 0, a, 1);

        var b = RippleAmplitude(rB, wavelength);
        var gap = PhaseGap(rA - rB, wavelength, opposite);
        var together = Math.Sqrt((a * a) + (b * b) + (2 * a * b * Math.Cos(gap)));
        return (a, b, together, together / (a + b));
    }

    /// <summary>
    /// The height of each wave at a point at time <paramref name="seconds"/> (real seconds): A's, B's and the two together.
    /// </summary>
    public static (double A, double B, double Sum) RippleHeights(Point point, double separation, double wavelength, bool opposite,
        bool second, double seconds)
    {
        var k = 2 * Math.PI / wavelength;
        var omega = k * RippleSpeed;
        var rA = ((Vector)(point - SourceA(separation))).Length;
        var rB = ((Vector)(point - SourceB(separation))).Length;
        var a = RippleAmplitude(rA, wavelength) * Math.Sin((omega * seconds) - (k * rA));
        var b = second ? RippleAmplitude(rB, wavelength) * Math.Sin((omega * seconds) + (opposite ? Math.PI : 0) - (k * rB)) : 0;
        return (a, b, a + b);
    }

    /// <summary>The path differences of the lines of still water: odd half wavelengths (whole ones when opposite), |Δ| &lt; d.</summary>
    public static IReadOnlyList<double> QuietDifferences(double separation, double wavelength, bool opposite) =>
        Differences(separation, wavelength, opposite ? 0 : 0.5);

    /// <summary>The path differences of the lines where the water heaves most: whole wavelengths (odd halves when opposite).</summary>
    public static IReadOnlyList<double> LoudDifferences(double separation, double wavelength, bool opposite) =>
        Differences(separation, wavelength, opposite ? 0.5 : 0);

    private static List<double> Differences(double separation, double wavelength, double offset)
    {
        var list = new List<double>();
        for (var n = 0; (n + offset) * wavelength < separation - 1e-9; n++)
        {
            var delta = (n + offset) * wavelength;
            list.Add(delta);
            if (delta > 0) list.Insert(0, -delta);
        }

        return list;
    }

    /// <summary>
    /// The line where A is farther than B by <paramref name="delta"/> (a branch of a hyperbola with A and B as its foci),
    /// from y = −<paramref name="yMax"/> to +<paramref name="yMax"/>.
    /// </summary>
    public static IEnumerable<Point> Hyperbola(double delta, double separation, double yMax, int steps = 80)
    {
        var c = separation / 2;
        var a = Math.Abs(delta) / 2;
        if (a < 1e-9)
        {
            yield return new Point(0, -yMax);
            yield return new Point(0, yMax);
            yield break;
        }

        var b = Math.Sqrt(Math.Max(0, (c * c) - (a * a)));
        if (b < 1e-9) yield break;

        var side = Math.Sign(delta);
        var uMax = Math.Asinh(yMax / b);
        for (var i = 0; i <= steps; i++)
        {
            var u = -uMax + (2 * uMax * i / steps);
            yield return new Point(side * a * Math.Cosh(u), b * Math.Sinh(u));
        }
    }

    #endregion

    #region light through two slits

    /// <summary>Width of each slit, mm.</summary>
    public const double SlitWidth = 0.04;

    public const double SpeedOfLight = 299_792_458;

    /// <summary>The light's frequency for a wavelength in nm, Hz.</summary>
    public static double LightFrequency(double nanometres) => SpeedOfLight / (nanometres * 1e-9);

    /// <summary>The distance between neighbouring bright stripes, λL/d, mm (λ in nm, L in m, d in mm).</summary>
    public static double FringeSpacing(double nanometres, double screenDistance, double slitSpacing) =>
        nanometres * 1e-9 * screenDistance / (slitSpacing * 1e-3) * 1e3;

    /// <summary>
    /// Brightness at <paramref name="y"/> mm from the middle of the screen: cos² stripes from the two slits under the
    /// broad glow (sinc²) of each slit on its own. 1 in the middle with both slits; a quarter of that with one.
    /// </summary>
    public static double ScreenIntensity(double y, double nanometres, double screenDistance, double slitSpacing, bool twoSlits)
    {
        var lambda = nanometres * 1e-6;
        var sin = y / Math.Sqrt((y * y) + Square(screenDistance * 1e3));
        var beta = Math.PI * SlitWidth * sin / lambda;
        var glow = Math.Abs(beta) < 1e-9 ? 1 : Square(Math.Sin(beta) / beta);
        if (!twoSlits) return glow / 4;

        return glow * Square(Math.Cos(Math.PI * slitSpacing * sin / lambda));
    }

    /// <summary>Bright stripes inside the middle glow: orders n with |n| &lt; d/a.</summary>
    public static int StripesInMiddle(double slitSpacing)
    {
        var ratio = slitSpacing / SlitWidth;
        var largest = (int)Math.Ceiling(ratio) - 1;
        return (2 * largest) + 1;
    }

    #endregion

    #region a moving source of sound

    public const double SpeedOfSound = 343;

    /// <summary>The source's own pitch: concert A.</summary>
    public const double SourcePitch = 440;

    /// <summary>How many times slower than real the Doppler view runs.</summary>
    public const double DopplerSlowdown = 5;

    /// <summary>Waves per ring drawn: a ring for every 22nd wave, one each 1/20 s.</summary>
    public const int WavesPerRing = 22;

    /// <summary>Seconds between the rings drawn.</summary>
    public const double RingInterval = WavesPerRing / SourcePitch;

    /// <summary>How far the source goes in one pass, metres: it is halfway at mid-pass, at x = 0.</summary>
    public const double PassLength = 300;

    public const double MinPass = 3;

    public const double MaxPass = 40;

    /// <summary>Seconds the source takes to go <see cref="PassLength"/> at a Mach number (3 standing still, at most 40).</summary>
    public static double PassDuration(double mach) =>
        mach < 1e-6 ? MinPass : Math.Min(PassLength / (mach * SpeedOfSound), MaxPass);

    /// <summary>
    /// Seconds before the Doppler view starts the pass again: the pass, and faster than sound long enough for the bang to
    /// reach the listener too.
    /// </summary>
    public static double LoopDuration(double mach, Point listener)
    {
        var pass = PassDuration(mach);
        var boom = ConeArrival(listener, mach);
        return double.IsNaN(boom) ? pass : Math.Clamp(boom + 0.35, pass, MaxPass);
    }

    /// <summary>Where the source is at <paramref name="seconds"/> into the pass: x = 0 halfway through, and always before and after.</summary>
    public static double SourceX(double seconds, double mach) => mach * SpeedOfSound * (seconds - (PassDuration(mach) / 2));

    /// <summary>Pitch heard from a source moving at <paramref name="mach"/>, at an angle whose cosine is between its way and the listener.</summary>
    public static double HeardFrequency(double cosAngle, double mach) => SourcePitch / Math.Abs(1 - (mach * cosAngle));

    /// <summary>Pitch heard straight ahead of the source; infinite at and above the speed of sound.</summary>
    public static double Ahead(double mach) => mach < 1 ? SourcePitch / (1 - mach) : double.PositiveInfinity;

    /// <summary>Pitch heard straight behind it.</summary>
    public static double Behind(double mach) => SourcePitch / (1 + mach);

    /// <summary>Half the angle of the cone of piled-up sound behind a source faster than sound, radians; NaN below.</summary>
    public static double ConeAngle(double mach) => mach > 1 ? Math.Asin(1 / mach) : double.NaN;

    /// <summary>When the cone reaches a listener (seconds into the pass); NaN below the speed of sound.</summary>
    public static double ConeArrival(Point listener, double mach)
    {
        if (mach <= 1) return double.NaN;
        var alpha = ConeAngle(mach);
        var behind = Math.Abs(listener.Y) / Math.Tan(alpha);
        return (PassDuration(mach) / 2) + ((listener.X + behind) / (mach * SpeedOfSound));
    }

    /// <summary>
    /// The sounds reaching <paramref name="listener"/> at <paramref name="seconds"/>: when each was made and its pitch.
    /// Slower than sound there is always exactly one; faster, none until the cone arrives and two after it.
    /// </summary>
    /// <remarks>
    /// A sound made at time e arrives when c(t − e) = |L − S(e)|. As S moves in a straight line the right-hand side is
    /// convex in e, so g(e) = c(t − e) − |L − S(e)| is concave: find its top by golden section, then each side's root by
    /// bisection.
    /// </remarks>
    public static IReadOnlyList<HeardSound> Heard(Point listener, double seconds, double mach)
    {
        var c = SpeedOfSound;
        var v = mach * c;
        var pass = PassDuration(mach);
        double Gap(double e)
        {
            var dx = listener.X - (v * (e - (pass / 2)));
            return (c * (seconds - e)) - Math.Sqrt((dx * dx) + (listener.Y * listener.Y));
        }

        var reach = Math.Sqrt(Square(listener.X - SourceX(seconds, mach)) + Square(listener.Y));
        var lookBack = mach < 0.98 ? ((reach + 10) / (c - v)) + 1 : 120;
        var low = seconds - lookBack;

        // The top of g.
        const double golden = 0.6180339887498949;
        double left = low, right = seconds;
        for (var i = 0; i < 90 && right - left > 1e-10; i++)
        {
            var m1 = right - (golden * (right - left));
            var m2 = left + (golden * (right - left));
            if (Gap(m1) < Gap(m2)) left = m1;
            else right = m2;
        }

        var top = (left + right) / 2;
        if (Gap(top) <= 0) return [];

        var sounds = new List<HeardSound>(2);
        if (Gap(low) < 0) sounds.Add(Sound(Root(Gap, low, top)));
        sounds.Add(Sound(Root(Gap, top, seconds)));
        return sounds;

        HeardSound Sound(double emitted)
        {
            var dx = listener.X - (v * (emitted - (pass / 2)));
            var distance = Math.Sqrt((dx * dx) + (listener.Y * listener.Y));
            var cos = distance < 1e-9 ? 1 : dx / distance;
            return new HeardSound(emitted, HeardFrequency(cos, mach), cos);
        }
    }

    /// <summary>Where <paramref name="f"/> crosses zero between a point where it is positive and one where it is not.</summary>
    private static double Root(Func<double, double> f, double a, double b)
    {
        var fa = f(a);
        for (var i = 0; i < 100 && b - a > 1e-11; i++)
        {
            var m = (a + b) / 2;
            var fm = f(m);
            if ((fm > 0) == (fa > 0))
            {
                a = m;
                fa = fm;
            }
            else
            {
                b = m;
            }
        }

        return (a + b) / 2;
    }

    /// <summary>What the listener hears now, from <see cref="Heard"/>.</summary>
    public static Hearing HearingOf(IReadOnlyList<HeardSound> sounds, double mach, double seconds, double coneArrival)
    {
        if (mach > 1 && !double.IsNaN(coneArrival) && seconds >= coneArrival && seconds - coneArrival < 0.3) return Hearing.Boom;
        if (sounds.Count == 0) return Hearing.Silent;
        if (sounds.Count > 1) return Hearing.Two;
        if (mach < 1e-6) return Hearing.Still;

        var cos = sounds[0].CosAngle;
        return cos > 0.25 ? Hearing.Approaching : cos < -0.25 ? Hearing.Receding : Hearing.Passing;
    }

    private static readonly string[] NoteNames = ["C", "C♯", "D", "D♯", "E", "F", "F♯", "G", "G♯", "A", "A♯", "B"];

    /// <summary>The nearest note of the piano for a pitch, e.g. 440 Hz → "A4", 262 Hz → "C4".</summary>
    public static string NoteName(double hertz)
    {
        if (!(hertz > 0) || double.IsInfinity(hertz)) return "—";
        var midi = (int)Math.Round(69 + (12 * Math.Log2(hertz / 440)));
        var octave = (int)Math.Floor(midi / 12.0) - 1;
        return $"{NoteNames[((midi % 12) + 12) % 12]}{octave}";
    }

    #endregion

    private static double Square(double x) => x * x;
}
