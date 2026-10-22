namespace Lemon.Explore.Book.Chapters.Expansion;

/// <summary>The views of the expanding-universe page.</summary>
public enum ExpansionMode
{
    /// <summary>A patch of galaxies drifting apart, seen from any of them: Hubble's law and no centre.</summary>
    Hubble,

    /// <summary>Light crossing stretching space from a distant galaxy to us, and its spectrum shifting red.</summary>
    Redshift,

    /// <summary>The universe's size through time, from the hot start to the far future.</summary>
    History,
}

/// <summary>The universes the history view can compare; all expand at today's rate now.</summary>
public enum UniverseKind
{
    /// <summary>Ours: matter, radiation and dark energy as Planck measured them (2018).</summary>
    Ours,

    /// <summary>Only matter, just enough to be flat (no dark energy): slows down for ever.</summary>
    MatterOnly,

    /// <summary>Five times more matter than flat and no dark energy: stops, falls back and ends in a crunch.</summary>
    Closed,
}

/// <summary>How the history view spreads time along its axis.</summary>
public enum TimeAxis
{
    /// <summary>Billions of years, evenly: the true shape, the early universe squeezed into the left edge.</summary>
    Linear,

    /// <summary>Powers of ten, from a hundred-thousandth of a second: the first minutes get as much room as the last billions of years.</summary>
    Log,
}

/// <summary>
/// A Friedmann universe: how its scale factor <c>a</c> (its size compared with today) grows with time, the light-travel
/// distances and redshifts that follow, and its expansion rate. Built once per universe from a table over ln a.
/// </summary>
/// <remarks>
/// Units: time in billions of years (Gyr), distances in megaparsecs (Mpc), the Hubble rate in km/s per Mpc. The table
/// integrates dt = d(ln a) / H(a) and the comoving light path dχ = c d(ln a) / (a H(a)) with Simpson's rule; a universe
/// that turns round (<see cref="UniverseKind.Closed"/>) is finished near its top with the substitution a = a_max − u²,
/// which removes the 1/√(a_max − a) of H → 0 there, and falls back as the mirror image of its rise.
/// </remarks>
public sealed class Universe
{
    /// <summary>The speed of light, km/s.</summary>
    public const double C = 299_792.458;

    /// <summary>1 / (1 km/s/Mpc) in billions of years.</summary>
    public const double GyrPerInverseH = 977.7922;

    /// <summary>Billions of light-years in a megaparsec.</summary>
    public const double GlyPerMpc = 3.261564e-3;

    public const double SecondsPerGyr = 3.15576e16;

    /// <summary>The microwave background's temperature today, K.</summary>
    public const double CmbToday = 2.7255;

    /// <summary>The redshift of the microwave background: when the hot gas turned clear, 380 000 years after the start.</summary>
    public const double CmbRedshift = 1090;

    private const double MinLnA = -32.0;
    private const double Step = 0.004;

    private readonly double[] _lnA;
    private readonly double[] _time;

    /// <summary>The comoving light path from the start to each a, Mpc (χ to a source at a = path(1) − path(a)).</summary>
    private readonly double[] _path;

    private Universe(UniverseKind kind, double h0, double omegaM, double omegaR, double omegaL)
    {
        Kind = kind;
        H0 = h0;
        OmegaM = omegaM;
        OmegaR = omegaR;
        OmegaL = omegaL;
        MaxScale = FindTurnaround();
        (_lnA, _time, _path) = Build();
        Age = TimeAt(1);
        if (MaxScale is not null)
        {
            TurnaroundTime = _time[^1];
            CrunchTime = 2 * _time[^1];
        }
    }

    public static Universe Ours { get; } = new(UniverseKind.Ours, 67.4, 0.315, 9.2e-5, 1 - 0.315 - 9.2e-5);

    public static Universe MatterOnly { get; } = new(UniverseKind.MatterOnly, 67.4, 1 - 9.2e-5, 9.2e-5, 0);

    public static Universe Closed { get; } = new(UniverseKind.Closed, 67.4, 5, 9.2e-5, 0);

    public static Universe Of(UniverseKind kind) => kind switch
    {
        UniverseKind.MatterOnly => MatterOnly,
        UniverseKind.Closed => Closed,
        _ => Ours,
    };

    public UniverseKind Kind { get; }

    /// <summary>Today's expansion rate, km/s per Mpc.</summary>
    public double H0 { get; }

    public double OmegaM { get; }

    public double OmegaR { get; }

    public double OmegaL { get; }

    /// <summary>Curvature: 0 flat, negative closed.</summary>
    public double OmegaK => 1 - OmegaM - OmegaR - OmegaL;

    /// <summary>Its age today (a = 1), Gyr.</summary>
    public double Age { get; }

    /// <summary>How big it gets before falling back (a closed universe), else null.</summary>
    public double? MaxScale { get; }

    public double? TurnaroundTime { get; }

    /// <summary>When a closed universe ends, Gyr after its start.</summary>
    public double? CrunchTime { get; }

    /// <summary>1 / H0 in Gyr (the "Hubble time").</summary>
    public double HubbleTime => GyrPerInverseH / H0;

    /// <summary>(a E)² = Ωr/a² + Ωm/a + Ωk + ΩΛ a²: zero where the expansion stops.</summary>
    private double F(double a) => (OmegaR / (a * a)) + (OmegaM / a) + OmegaK + (OmegaL * a * a);

    /// <summary>H(a) / H0 (zero past a turnaround's top).</summary>
    public double E(double a)
    {
        var f = F(a);
        return f <= 0 ? 0 : Math.Sqrt(f) / a;
    }

    /// <summary>The expansion rate at scale factor a, km/s per Mpc.</summary>
    public double Hubble(double a) => H0 * E(a);

    /// <summary>The deceleration q = −ä a / ȧ²: positive while gravity slows the expansion, negative once it speeds up.</summary>
    public double Deceleration(double a)
    {
        var e2 = Math.Max(1e-12, F(a) / (a * a));
        return ((2 * OmegaR / Math.Pow(a, 4)) + (OmegaM / Math.Pow(a, 3)) - (2 * OmegaL)) / (2 * e2);
    }

    private double? _speedup;
    private bool _speedupFound;

    /// <summary>The scale factor where the expansion stops slowing and starts speeding up, or null if it never does.</summary>
    public double? AccelerationStart()
    {
        if (!_speedupFound)
        {
            _speedup = FindAccelerationStart();
            _speedupFound = true;
        }

        return _speedup;
    }

    private double? FindAccelerationStart()
    {
        if (Deceleration(1e3) >= 0) return null;
        double lo = 1e-3, hi = 1e3;
        if (Deceleration(lo) <= 0) return null;
        for (var i = 0; i < 100; i++)
        {
            var mid = Math.Sqrt(lo * hi);
            if (Deceleration(mid) > 0) lo = mid;
            else hi = mid;
        }

        return Math.Sqrt(lo * hi);
    }

    /// <summary>Where F(a) = 0 above today, by bisection; null if the universe expands for ever.</summary>
    private double? FindTurnaround()
    {
        double lo = 1, hi = 1;
        for (var i = 0; i < 60 && F(hi) > 0; i++) hi *= 1.5;
        if (F(hi) > 0) return null;
        for (var i = 0; i < 200; i++)
        {
            var mid = 0.5 * (lo + hi);
            if (F(mid) > 0) lo = mid;
            else hi = mid;
        }

        return lo;
    }

    /// <summary>The table: ln a, time and light path from a tiny a up to a = 60 or a turnaround's top.</summary>
    private (double[] LnA, double[] Time, double[] Path) Build()
    {
        var h = H0 / GyrPerInverseH; // per Gyr
        var c = C / H0;              // the Hubble distance, Mpc
        var lnA = new List<double>();
        var time = new List<double>();
        var path = new List<double>();

        // Deep in the radiation era a ∝ √t: t = a² / (2 H0 √Ωr) and the light path c t / a = c a / (2 √Ωr) (in H0 units).
        var a0 = Math.Exp(MinLnA);
        var rootR = Math.Sqrt(OmegaR);
        lnA.Add(MinLnA);
        time.Add(a0 * a0 / (2 * h * rootR));
        path.Add(c * a0 / rootR);

        double DtDlnA(double ln) => 1 / (h * E(Math.Exp(ln)));
        double DpDlnA(double ln)
        {
            var a = Math.Exp(ln);
            return c / (a * E(a));
        }

        var top = MaxScale is { } max ? Math.Log(max * 0.98) : Math.Log(60);
        var ln0 = MinLnA;
        while (ln0 < top - 1e-12)
        {
            var ln1 = Math.Min(top, ln0 + Step);
            var mid = 0.5 * (ln0 + ln1);
            var w = (ln1 - ln0) / 6;
            time.Add(time[^1] + (w * (DtDlnA(ln0) + (4 * DtDlnA(mid)) + DtDlnA(ln1))));
            path.Add(path[^1] + (w * (DpDlnA(ln0) + (4 * DpDlnA(mid)) + DpDlnA(ln1))));
            lnA.Add(ln1);
            ln0 = ln1;
        }

        if (MaxScale is { } aMax)
        {
            // Near the top dt = da / (H0 √F) blows up as 1/√(aMax − a); with a = aMax − u² it becomes 2u du / (H0 √F), finite.
            var slope = Math.Abs((F(aMax) - F(aMax * (1 - 1e-6))) / (aMax * 1e-6));
            double DtDu(double u)
            {
                var a = aMax - (u * u);
                var f = F(a);
                return u < 1e-9 || f <= 0 ? 2 / (h * Math.Sqrt(slope)) : 2 * u / (h * Math.Sqrt(f));
            }

            double DpDu(double u)
            {
                var a = aMax - (u * u);
                var f = F(a);
                return (u < 1e-9 || f <= 0 ? 2 / Math.Sqrt(slope) : 2 * u / Math.Sqrt(f)) * c / a;
            }

            var u0 = Math.Sqrt(aMax - Math.Exp(top));
            const int steps = 400;
            for (var i = 0; i < steps; i++)
            {
                var ua = u0 * (1 - ((double)i / steps));
                var ub = u0 * (1 - ((double)(i + 1) / steps));
                var um = 0.5 * (ua + ub);
                var w = (ua - ub) / 6;
                time.Add(time[^1] + (w * (DtDu(ua) + (4 * DtDu(um)) + DtDu(ub))));
                path.Add(path[^1] + (w * (DpDu(ua) + (4 * DpDu(um)) + DpDu(ub))));
                lnA.Add(Math.Log(aMax - (ub * ub)));
            }
        }

        return (lnA.ToArray(), time.ToArray(), path.ToArray());
    }

    /// <summary>The time (Gyr after the start) when the scale factor first was <paramref name="a"/>.</summary>
    public double TimeAt(double a)
    {
        if (a <= 0) return 0;
        var ln = Math.Log(a);
        if (ln <= _lnA[0])
        {
            var h = H0 / GyrPerInverseH;
            return a * a / (2 * h * Math.Sqrt(OmegaR));
        }

        return Interpolate(_lnA, _time, ln);
    }

    /// <summary>The scale factor at a time (Gyr after the start); falls back after a closed universe's top, 0 at its crunch.</summary>
    public double ScaleAt(double time)
    {
        if (time <= 0) return 0;
        if (TurnaroundTime is { } turn && time > turn)
        {
            if (time >= CrunchTime) return 0;
            time = (2 * turn) - time;
        }

        if (time <= _time[0])
        {
            var h = H0 / GyrPerInverseH;
            return Math.Sqrt(2 * h * Math.Sqrt(OmegaR) * time);
        }

        if (time >= _time[^1]) return Math.Exp(_lnA[^1]);
        return Math.Exp(Interpolate(_time, _lnA, time));
    }

    /// <summary>Whether a closed universe is falling back at this time.</summary>
    public bool IsContracting(double time) => TurnaroundTime is { } turn && time > turn;

    /// <summary>The expansion rate at a time, km/s per Mpc (negative while falling back).</summary>
    public double HubbleAtTime(double time)
    {
        var a = ScaleAt(time);
        if (a <= 0) return double.PositiveInfinity;
        var rate = Hubble(a);
        return IsContracting(time) ? -rate : rate;
    }

    /// <summary>
    /// The comoving distance to light that left at scale factor <paramref name="a"/> and reaches us today, Mpc. It is
    /// also how far that source is now; it was <c>a</c> times that when the light left.
    /// </summary>
    public double ComovingDistance(double a)
    {
        if (a >= 1) return 0;
        var now = Interpolate(_lnA, _path, 0);
        if (a <= 0) return now;
        var ln = Math.Log(a);
        var then = ln <= _lnA[0] ? _path[0] * a / Math.Exp(_lnA[0]) : Interpolate(_lnA, _path, ln);
        return now - then;
    }

    /// <summary>How far light has come from the start until today: the edge of what we can see, Mpc.</summary>
    public double Horizon => ComovingDistance(0);

    /// <summary>How long ago light left at scale factor <paramref name="a"/>, Gyr.</summary>
    public double LookbackTime(double a) => Age - TimeAt(a);

    /// <summary>How fast a source whose light left at <paramref name="a"/> moves away from us now (H0 × its distance), km/s.</summary>
    public double RecessionSpeedNow(double a) => H0 * ComovingDistance(a);

    /// <summary>The microwave background's temperature at scale factor a, K.</summary>
    public static double Temperature(double a) => CmbToday / a;

    public static double ScaleForRedshift(double z) => 1 / (1 + z);

    public static double RedshiftForScale(double a) => (1 / a) - 1;

    /// <summary>Linear interpolation of <paramref name="ys"/> at <paramref name="x"/> in the increasing <paramref name="xs"/>.</summary>
    private static double Interpolate(double[] xs, double[] ys, double x)
    {
        if (x <= xs[0]) return ys[0];
        if (x >= xs[^1]) return ys[^1];
        int lo = 0, hi = xs.Length - 1;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) >> 1;
            if (xs[mid] <= x) lo = mid;
            else hi = mid;
        }

        var t = (x - xs[lo]) / (xs[hi] - xs[lo]);
        return ys[lo] + ((ys[hi] - ys[lo]) * t);
    }
}

/// <summary>A light source of the redshift view: a real galaxy (or quasar) and how much its light is stretched.</summary>
public sealed record LightSource(string Id, double Redshift)
{
    /// <summary>The galaxies to pick, nearest first. Redshifts from published spectra, rounded.</summary>
    public static IReadOnlyList<LightSource> All { get; } =
    [
        new("Virgo", 0.0036),
        new("Coma", 0.0231),
        new("Quasar", 0.158),
        new("Halfway", 1.0),
        new("GnZ11", 10.6),
        new("Jades", 14.18),
    ];

    public static LightSource? Find(string? id) => All.FirstOrDefault(s => s.Id == id);
}

/// <summary>A spectral line the redshift view follows: hydrogen's strongest lines and doubly ionised oxygen's.</summary>
public sealed record SpectralLine(string Id, string Name, double Nanometres)
{
    public static IReadOnlyList<SpectralLine> All { get; } =
    [
        new("lya", "Lyα", 121.6),
        new("hd", "Hδ", 410.2),
        new("hg", "Hγ", 434.0),
        new("hb", "Hβ", 486.1),
        new("o3", "[O III]", 500.7),
        new("ha", "Hα", 656.3),
    ];

    /// <summary>The line the travelling wave stands for (blue-green Hβ, red by z ≈ 0.35, infrared by z ≈ 0.55).</summary>
    public static SpectralLine Wave => All[3];
}

/// <summary>
/// A moment in cosmic history for the history view, set either by the scale factor (so it moves with the universe:
/// the microwave background is released at the same temperature in every universe) or by the time from today.
/// </summary>
public sealed record CosmicEvent(string Id, double? Scale = null, double? FromNow = null, bool OursOnly = false)
{
    /// <summary>The moments, in order. Scale factors from the temperatures (2.7255 K / T) or redshifts they happen at.</summary>
    public static IReadOnlyList<CosmicEvent> All { get; } =
    [
        new("Protons", Scale: Universe.CmbToday / 1e12),
        new("Nuclei", Scale: Universe.CmbToday / 1e9),
        new("Cmb", Scale: 1 / (1 + Universe.CmbRedshift)),
        new("FirstStars", Scale: 1 / 21.0, OursOnly: true),
        new("Jades", Scale: 1 / 15.18, OursOnly: true),
        new("Noon", Scale: 1 / 3.0, OursOnly: true),
        new("Speedup", OursOnly: true),
        new("Sun", FromNow: -4.6, OursOnly: true),
        new("Now", Scale: 1),
        new("Andromeda", FromNow: 4.5, OursOnly: true),
    ];

    /// <summary>When this happens in <paramref name="universe"/>, Gyr after its start, or null if it does not happen there.</summary>
    public double? TimeIn(Universe universe)
    {
        if (OursOnly && universe.Kind != UniverseKind.Ours) return null;
        if (Id == "Speedup") return universe.AccelerationStart() is { } a ? universe.TimeAt(a) : null;
        if (FromNow is { } dt) return universe.Age + dt;
        return Scale is { } s ? universe.TimeAt(s) : null;
    }
}

/// <summary>The stages of cosmic history the history view names, from the hot start to the far future.</summary>
public enum CosmicEra
{
    /// <summary>Hotter than a billion kelvin: particles, no atomic nuclei yet.</summary>
    Start,

    /// <summary>A glowing fog of nuclei and electrons that light cannot cross.</summary>
    Plasma,

    /// <summary>Clear but dark: gas, no stars yet.</summary>
    DarkAges,

    /// <summary>The first stars and small galaxies light up.</summary>
    Dawn,

    /// <summary>Galaxies grow; gravity slows the expansion.</summary>
    Galaxies,

    /// <summary>Dark energy has taken over: the expansion speeds up.</summary>
    Speeding,

    Now,

    /// <summary>Our universe's future: speeding up for ever, distant galaxies fading from view.</summary>
    FutureFast,

    /// <summary>A universe without dark energy, after today: still growing, ever more slowly.</summary>
    FutureSlowing,

    /// <summary>A closed universe after its top: falling back together.</summary>
    Contracting,

    /// <summary>The last stage of a crunch: hot and dense again.</summary>
    Crunch,

    /// <summary>After a crunch: nothing left.</summary>
    Ended,
}

/// <summary>The history view's axis and the stage of history at a time.</summary>
public static class CosmicHistory
{
    /// <summary>The axis ends this many Gyr after the start.</summary>
    public const double End = 30;

    /// <summary>The log axis starts this many seconds after the start.</summary>
    public const double LogStartSeconds = 1e-5;

    private static double LogStart => Math.Log10(LogStartSeconds);

    private static double LogEnd => Math.Log10(End * Universe.SecondsPerGyr);

    /// <summary>
    /// The earliest moment the history view goes back to: a few seconds after the start on the even axis (still at its
    /// left edge, and still hot), the axis's own start on the log one.
    /// </summary>
    public static double Earliest(TimeAxis axis) => axis == TimeAxis.Linear ? 1e-16 : Time(axis, 0.002);

    /// <summary>Where a time (Gyr) falls along the axis, 0 to 1.</summary>
    public static double Fraction(TimeAxis axis, double time)
    {
        if (axis == TimeAxis.Linear) return Math.Clamp(time / End, 0, 1);
        if (time <= 0) return 0;
        var s = Math.Log10(time * Universe.SecondsPerGyr);
        return Math.Clamp((s - LogStart) / (LogEnd - LogStart), 0, 1);
    }

    /// <summary>The time (Gyr) at a place along the axis, 0 to 1.</summary>
    public static double Time(TimeAxis axis, double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        if (axis == TimeAxis.Linear) return fraction * End;
        return Math.Pow(10, LogStart + (fraction * (LogEnd - LogStart))) / Universe.SecondsPerGyr;
    }

    /// <summary>The stage of history at a time in a universe.</summary>
    public static CosmicEra EraAt(Universe universe, double time)
    {
        if (universe.CrunchTime is { } crunch && time >= crunch) return CosmicEra.Ended;
        var a = universe.ScaleAt(time);
        if (universe.IsContracting(time)) return a < 1 / 6.0 ? CosmicEra.Crunch : CosmicEra.Contracting;
        if (a < Universe.CmbToday / 1e9) return CosmicEra.Start;
        if (a < 1 / (1 + Universe.CmbRedshift)) return CosmicEra.Plasma;
        if (a < 1 / 21.0) return CosmicEra.DarkAges;
        if (a < 1 / 6.0) return CosmicEra.Dawn;
        if (Math.Abs(time - universe.Age) < 0.3) return CosmicEra.Now;
        if (time > universe.Age)
        {
            // A closed universe is still slowing towards its top, not slowing for ever.
            return universe.Kind switch
            {
                UniverseKind.Ours => CosmicEra.FutureFast,
                UniverseKind.MatterOnly => CosmicEra.FutureSlowing,
                _ => CosmicEra.Galaxies,
            };
        }

        return universe.AccelerationStart() is { } speedup && a > speedup ? CosmicEra.Speeding : CosmicEra.Galaxies;
    }
}
