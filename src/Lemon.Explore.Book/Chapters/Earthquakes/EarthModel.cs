namespace Lemon.Explore.Book.Chapters.Earthquakes;

/// <summary>A body wave: P pushes and pulls (through solid and liquid), S shakes sideways (through solid only).</summary>
public enum WaveKind
{
    P,
    S,
}

/// <summary>The path a ray took, named as seismologists do.</summary>
public enum Phase
{
    /// <summary>Through the mantle only.</summary>
    P,

    /// <summary>Through the liquid outer core and back.</summary>
    PKP,

    /// <summary>Through the outer and the inner core.</summary>
    PKIKP,

    /// <summary>Bounced off the inner core's surface.</summary>
    PKiKP,

    /// <summary>A shear wave through the mantle.</summary>
    S,

    /// <summary>A shear wave that ran into the liquid outer core and went no further.</summary>
    SStopped,
}

/// <summary>A ray through the Earth from a shallow quake: points (radius km, angle from the quake rad, time s) in order.</summary>
public sealed class SeismicRay
{
    internal SeismicRay(WaveKind kind, double takeoff, double rayParameter, float[] radius, float[] angle, float[] time, Phase phase)
    {
        Kind = kind;
        Takeoff = takeoff;
        RayParameter = rayParameter;
        Radius = radius;
        Angle = angle;
        Time = time;
        Phase = phase;
    }

    public WaveKind Kind { get; }

    /// <summary>The angle it leaves the quake at, ° from straight down.</summary>
    public double Takeoff { get; }

    /// <summary>r·sin(i)/v, s per radian: the same all along the ray (Snell's law in a round Earth).</summary>
    public double RayParameter { get; }

    public float[] Radius { get; }

    public float[] Angle { get; }

    public float[] Time { get; }

    public Phase Phase { get; }

    /// <summary>It came back up to the surface (an S ray stopped at the core does not).</summary>
    public bool Surfaced => Phase != Phase.SStopped;

    /// <summary>How far round the Earth it came up, °; for a stopped ray, how far it got.</summary>
    public double Distance => Angle[^1] * 180 / Math.PI;

    public double TravelTime => Time[^1];

    /// <summary>Where the ray is at time <paramref name="t"/>: radius and angle, or null before it starts or after it ends.</summary>
    public (double Radius, double Angle)? At(double t)
    {
        if (t < 0 || t > Time[^1]) return null;
        var hi = Array.BinarySearch(Time, (float)t);
        if (hi >= 0) return (Radius[hi], Angle[hi]);
        hi = ~hi;
        if (hi == 0) return (Radius[0], Angle[0]);
        var lo = hi - 1;
        var span = Time[hi] - Time[lo];
        var f = span <= 0 ? 0 : (t - Time[lo]) / span;
        return (Radius[lo] + ((Radius[hi] - Radius[lo]) * f), Angle[lo] + ((Angle[hi] - Angle[lo]) * f));
    }

    /// <summary>The deepest point it reached, km below the surface.</summary>
    public double DeepestDepth => EarthModel.Radius - Radius.Min();
}

/// <summary>A wave reaching a station: which, and when (s after the quake).</summary>
public readonly record struct Arrival(Phase Phase, double Time);

/// <summary>
/// The inside of the Earth for the earthquake page: a layered model of how fast P and S waves go (close to IASP91, with
/// the textbook's round depths: the Moho at 33 km, the core at 2900 km, the inner core at 5150 km), and rays traced
/// through it shell by shell. In each 10–25 km shell the speed is taken as constant, so a ray is a straight chord there;
/// r·sin(i)/v stays the same along it, which bends it as the speed changes and sends it back up where it can go no
/// deeper. S waves stop at the liquid outer core. The travel times come within a few per cent of the standard tables.
/// </summary>
public static class EarthModel
{
    /// <summary>Earth's mean radius, km.</summary>
    public const double Radius = 6371;

    /// <summary>The Moho: the bottom of the crust (average under continents), km.</summary>
    public const double Moho = 33;

    /// <summary>The Gutenberg discontinuity: the top of the liquid outer core, km.</summary>
    public const double Gutenberg = 2900;

    /// <summary>The top of the solid inner core, km.</summary>
    public const double InnerCore = 5150;

    /// <summary>The bottom of the upper mantle, km.</summary>
    public const double UpperMantle = 660;

    /// <summary>The asthenosphere, the soft layer the plates ride on, km.</summary>
    public const double AsthenosphereTop = 80;

    public const double AsthenosphereBottom = 400;

    /// <summary>How deep the quakes start, km.</summary>
    public const double SourceDepth = 10;

    /// <summary>Surface (Rayleigh) waves run along the surface at about this speed, km/s.</summary>
    public const double SurfaceWaveSpeed = 3.7;

    /// <summary>Depth (km), P and S speed (km/s); a depth listed twice is a jump (the first row above it, the second below).</summary>
    private static readonly (double Depth, double Vp, double Vs)[] Table =
    [
        (0, 5.80, 3.36),
        (20, 5.80, 3.36),
        (20, 6.50, 3.75),
        (33, 6.50, 3.75),
        (33, 8.04, 4.47),
        (120, 8.05, 4.50),
        (210, 8.30, 4.52),
        (410, 9.03, 4.87),
        (410, 9.36, 5.07),
        (660, 10.20, 5.60),
        (660, 10.79, 5.96),
        (760, 11.07, 6.24),
        (1000, 11.40, 6.35),
        (1500, 11.95, 6.60),
        (2000, 12.45, 6.85),
        (2500, 13.05, 7.10),
        (2740, 13.65, 7.28),
        (2900, 13.69, 7.30),
        (2900, 8.00, 0),
        (3500, 8.90, 0),
        (4000, 9.50, 0),
        (4500, 9.95, 0),
        (5150, 10.29, 0),
        (5150, 11.04, 3.50),
        (6371, 11.24, 3.67),
    ];

    /// <summary>
    /// The speed of a wave at a depth, km/s; 0 for S in the outer core. At a jump, <paramref name="below"/> picks the
    /// value just under it.
    /// </summary>
    public static double Velocity(WaveKind kind, double depth, bool below = false)
    {
        depth = Math.Clamp(depth, 0, Radius);
        for (var i = 0; i + 1 < Table.Length; i++)
        {
            var a = Table[i];
            var b = Table[i + 1];
            if (a.Depth == b.Depth)
            {
                if (depth == a.Depth) return Pick(below ? b : a, kind);
                continue;
            }

            if (depth < a.Depth || depth > b.Depth) continue;
            if (depth == b.Depth && below && i + 2 < Table.Length && Table[i + 2].Depth == b.Depth) continue;
            var f = (depth - a.Depth) / (b.Depth - a.Depth);
            return Pick(a, kind) + ((Pick(b, kind) - Pick(a, kind)) * f);
        }

        return Pick(Table[^1], kind);
    }

    private static double Pick((double Depth, double Vp, double Vs) row, WaveKind kind) => kind == WaveKind.P ? row.Vp : row.Vs;

    /// <summary>The depths where the speeds jump: the shells are cut there exactly.</summary>
    private static readonly double[] Jumps = [20, Moho, 410, UpperMantle, Gutenberg, InnerCore];

    /// <summary>
    /// A shell between two radii (km) and its speeds at its top and bottom (just inside it, so a jump at its edge is not
    /// smeared). Inside, the speed is taken as v = a·rᵇ, which runs through both values and lets a ray be followed exactly.
    /// </summary>
    private readonly record struct Shell(double Out, double In, double VpTop, double VpBottom, double VsTop, double VsBottom)
    {
        public double Top(WaveKind kind) => kind == WaveKind.P ? VpTop : VsTop;

        public double Bottom(WaveKind kind) => kind == WaveKind.P ? VpBottom : VsBottom;
    }

    private static readonly Lazy<Shell[]> Shells = new(BuildShells);

    private static Shell[] BuildShells()
    {
        var depths = new SortedSet<double>(Jumps);
        for (var d = 0.0; d <= 800; d += 10) depths.Add(d);
        for (var d = 800.0; d < Radius; d += 25) depths.Add(d);
        depths.Add(SourceDepth);
        depths.Add(Radius);

        var list = depths.ToList();
        var shells = new Shell[list.Count - 1];
        for (var i = 0; i + 1 < list.Count; i++)
        {
            var top = list[i];
            var bottom = list[i + 1];
            shells[i] = new Shell(
                Radius - top,
                Math.Max(0.5, Radius - bottom),
                Velocity(WaveKind.P, top, below: true),
                Velocity(WaveKind.P, bottom),
                Velocity(WaveKind.S, top, below: true),
                Velocity(WaveKind.S, bottom));
        }

        return shells;
    }

    /// <summary>
    /// Traces a ray leaving a quake <see cref="SourceDepth"/> km down at <paramref name="takeoff"/>° from straight down
    /// until it comes back up to the surface (or, for S, meets the outer core).
    /// </summary>
    /// <remarks>
    /// With η = r/v, a ray keeps p = η·sin(i). In a shell where v = a·rᵇ, η goes as r^(1−b), and with
    /// κ = 1/(1−b) = ln(r₂/r₁)/ln(η₂/η₁) the angle travelled is κ·[acos(p/η)] and the time κ·[√(η²−p²)] between the
    /// radii; a ray turns where η = p. Points are added every half degree or so inside a shell, so the drawn path bends smoothly.
    /// </remarks>
    public static SeismicRay Trace(WaveKind kind, double takeoff)
    {
        var shells = Shells.Value;
        var source = 0;
        while (shells[source].Out > Radius - SourceDepth + 1e-6) source++;

        var p = shells[source].Out * Math.Sin(takeoff * Math.PI / 180) / shells[source].Top(kind);
        p = Math.Max(p, 1e-6);
        var path = new RayPath(kind, p, shells[source].Out);
        var k = source;
        var down = true;
        var reflected = false;
        var stopped = false;

        for (var guard = 0; guard < 20_000; guard++)
        {
            if (down)
            {
                if (k >= shells.Length)
                {
                    down = false;
                    k--;
                    continue;
                }

                var shell = shells[k];
                var vTop = shell.Top(kind);
                if (vTop <= 0)
                {
                    stopped = true;
                    break;
                }

                var etaTop = shell.Out / vTop;
                var etaBottom = shell.In / shell.Bottom(kind);
                if (p >= etaTop * (1 - 1e-12))
                {
                    // It cannot get into this shell: it turns (at a jump in speed, bounces) at the shell's top.
                    if (k > 0 && vTop - shells[k - 1].Bottom(kind) > 0.3) reflected = true;
                    down = false;
                    k--;
                    continue;
                }

                var phiTop = Math.Acos(Math.Min(1, p / etaTop));
                if (p >= etaBottom)
                {
                    // It turns inside this shell: down to where η = p, and back up.
                    path.Walk(shell, etaTop, etaBottom, phiTop, 0);
                    path.Walk(shell, etaTop, etaBottom, 0, phiTop);
                    down = false;
                    k--;
                    continue;
                }

                path.Walk(shell, etaTop, etaBottom, phiTop, Math.Acos(Math.Min(1, p / etaBottom)));
                k++;
            }
            else
            {
                if (k < 0) break;
                var shell = shells[k];
                var etaTop = shell.Out / shell.Top(kind);
                var etaBottom = shell.In / shell.Bottom(kind);
                if (p >= etaBottom * (1 - 1e-12))
                {
                    // Coming up into faster rock at too low an angle: it bounces back down.
                    down = true;
                    k++;
                    continue;
                }

                path.Walk(shell, etaTop, etaBottom, Math.Acos(Math.Min(1, p / etaBottom)), Math.Acos(Math.Min(1, p / etaTop)));
                k--;
            }
        }

        Phase phase;
        if (stopped) phase = Phase.SStopped;
        else if (kind == WaveKind.S) phase = Phase.S;
        else if (path.Deepest > InnerCore + 1) phase = Phase.PKIKP;
        else if (reflected && path.Deepest > InnerCore - 1) phase = Phase.PKiKP;
        else if (path.Deepest > Gutenberg + 1) phase = Phase.PKP;
        else phase = Phase.P;

        return new SeismicRay(kind, takeoff, p, path.Radius.ToArray(), path.Angle.ToArray(), path.Time.ToArray(), phase);
    }

    /// <summary>A ray's points as it is traced, and how to follow it through a shell.</summary>
    private sealed class RayPath
    {
        private readonly WaveKind _kind;
        private readonly double _p;
        private double _theta;
        private double _t;

        public RayPath(WaveKind kind, double p, double r0)
        {
            _kind = kind;
            _p = p;
            Add(r0);
        }

        public List<float> Radius { get; } = new(900);

        public List<float> Angle { get; } = new(900);

        public List<float> Time { get; } = new(900);

        public double Deepest { get; private set; }

        private void Add(double r)
        {
            Radius.Add((float)r);
            Angle.Add((float)_theta);
            Time.Add((float)_t);
            Deepest = Math.Max(Deepest, EarthModel.Radius - r);
        }

        /// <summary>
        /// Through part of a shell, from φ₀ to φ₁ (φ = acos(p/η), 0 at the turning point), a point every half degree or so:
        /// angle κ·Δφ, time κ·p·Δtan φ, radius from η = p/cos φ.
        /// </summary>
        public void Walk(Shell shell, double etaTop, double etaBottom, double phi0, double phi1)
        {
            var ratio = Math.Log(etaTop / etaBottom);
            var kappa = Math.Abs(ratio) < 1e-12 ? 1e9 : Math.Log(shell.Out / shell.In) / ratio;
            var steps = Math.Clamp((int)Math.Ceiling(Math.Abs(kappa * (phi1 - phi0)) / 0.009), 1, 40);
            var theta0 = _theta;
            var t0 = _t;
            var tan0 = Math.Tan(phi0);
            for (var i = 1; i <= steps; i++)
            {
                var phi = phi0 + ((phi1 - phi0) * i / steps);
                _theta = theta0 + Math.Abs(kappa * (phi - phi0));
                _t = t0 + Math.Abs(kappa * _p * (Math.Tan(phi) - tan0));
                var eta = _p / Math.Cos(phi);
                Add(Math.Clamp(shell.Out * Math.Pow(eta / etaTop, kappa), shell.In, shell.Out));
            }
        }
    }

    private static readonly Lazy<IReadOnlyList<SeismicRay>> PFan = new(() => MakeFan(WaveKind.P));
    private static readonly Lazy<IReadOnlyList<SeismicRay>> SFan = new(() => MakeFan(WaveKind.S));

    /// <summary>Rays leaving the quake from straight down to nearly flat (every 0.15°, finer where they reach the core), made once.</summary>
    public static IReadOnlyList<SeismicRay> Fan(WaveKind kind) => kind == WaveKind.P ? PFan.Value : SFan.Value;

    private static IReadOnlyList<SeismicRay> MakeFan(WaveKind kind)
    {
        var rays = new List<SeismicRay>();
        // P rays steep enough to reach the core come closer together: the ones grazing or bouncing off the inner core
        // cover a wide stretch of the surface from a very narrow range of angles.
        var takeoff = 0.05;
        while (takeoff < 89.5)
        {
            rays.Add(Trace(kind, takeoff));
            takeoff += kind == WaveKind.P && takeoff < 14 ? 0.02 : 0.15;
        }
        return rays;
    }

    /// <summary>
    /// The waves reaching a station <paramref name="distance"/>° round the Earth from the quake: the first of each
    /// phase, by time. Found where two neighbouring rays of the fan come up on either side of the station.
    /// </summary>
    public static IReadOnlyList<Arrival> Arrivals(double distance)
    {
        var best = new Dictionary<Phase, double>();
        foreach (var kind in new[] { WaveKind.P, WaveKind.S })
        {
            var fan = Fan(kind);
            for (var i = 0; i + 1 < fan.Count; i++)
            {
                var a = fan[i];
                var b = fan[i + 1];
                if (!a.Surfaced || !b.Surfaced || a.Phase != b.Phase) continue;
                var da = a.Distance;
                var db = b.Distance;
                if ((distance - da) * (distance - db) > 0 || Math.Abs(db - da) > 8) continue;
                var f = Math.Abs(db - da) < 1e-9 ? 0 : (distance - da) / (db - da);
                var time = a.TravelTime + ((b.TravelTime - a.TravelTime) * f);
                if (!best.TryGetValue(a.Phase, out var known) || time < known) best[a.Phase] = time;
            }
        }

        return best.Select(pair => new Arrival(pair.Key, pair.Value)).OrderBy(a => a.Time).ToList();
    }

    /// <summary>When the surface waves arrive, s: they run round the surface.</summary>
    public static double SurfaceWaveTime(double distance) => distance * Math.PI / 180 * Radius / SurfaceWaveSpeed;

    /// <summary>The first P-type and the first S arrival at a distance, s, or null where none comes (the shadow zones).</summary>
    public static (double? P, double? S) FirstArrivals(double distance, bool countInnerCore = true)
    {
        double? p = null;
        double? s = null;
        foreach (var arrival in Arrivals(distance))
        {
            if (arrival.Phase == Phase.S) s ??= arrival.Time;
            else if (arrival.Phase is Phase.P or Phase.PKP || (countInnerCore && arrival.Phase == Phase.PKIKP)) p ??= arrival.Time;
        }

        return (p, s);
    }

    /// <summary>Which layer a depth is in.</summary>
    public static EarthLayer LayerAt(double depth) => depth switch
    {
        < Moho => EarthLayer.Crust,
        < UpperMantle => EarthLayer.UpperMantle,
        < Gutenberg => EarthLayer.LowerMantle,
        < InnerCore => EarthLayer.OuterCore,
        _ => EarthLayer.InnerCore,
    };
}

public enum EarthLayer
{
    Crust,
    UpperMantle,
    LowerMantle,
    OuterCore,
    InnerCore,
}
