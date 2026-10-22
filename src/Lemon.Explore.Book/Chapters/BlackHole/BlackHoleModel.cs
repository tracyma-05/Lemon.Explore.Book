namespace Lemon.Explore.Book.Chapters.BlackHole;

/// <summary>The three views of the black-hole page.</summary>
public enum BlackHoleMode
{
    /// <summary>The black hole up close: its shadow, the photon ring and a glowing disk of gas bent round it.</summary>
    Shadow,

    /// <summary>Seen from above: light passing at different distances, and a probe in orbit beside Newton's.</summary>
    Orbits,

    /// <summary>Far away: a black hole in front of a galaxy, a star or a grid, bending their light into arcs and rings.</summary>
    Lensing,
}

/// <summary>What the lensing view puts behind the black hole.</summary>
public enum LensSource
{
    Galaxy,
    Star,
    Grid,
}

/// <summary>The probe launches the orbits view has ready.</summary>
public enum ProbeLaunch
{
    /// <summary>Exactly the circular speed at 10 M: it goes round and round.</summary>
    Circle,

    /// <summary>An ellipse between 18 M and 10 M that turns each lap (the rosette).</summary>
    Rosette,

    /// <summary>The innermost stable circle at 6 M, nudged: it soon spirals in.</summary>
    Isco,

    /// <summary>Too little sideways speed at 12 M: it falls in.</summary>
    Plunge,

    /// <summary>Fast enough at 10 M to get away.</summary>
    Escape,

    /// <summary>Set by dragging the probe's arrow.</summary>
    Custom,
}

/// <summary>
/// Light and matter near a non-spinning (Schwarzschild) black hole, in units where G = c = M = 1: the event horizon is at
/// r = 2, light can circle at r = 3 (the photon sphere), and the innermost stable circular orbit is at r = 6. Distances
/// are in units of GM/c² (1.5 km for each mass of the Sun).
/// </summary>
public static class BlackHoleModel
{
    /// <summary>The event horizon (Schwarzschild radius), 2 M.</summary>
    public const double Horizon = 2;

    /// <summary>Where light can go round in a circle, 3 M.</summary>
    public const double PhotonSphere = 3;

    /// <summary>The innermost stable circular orbit, 6 M: the inner edge of a thin disk.</summary>
    public const double Isco = 6;

    /// <summary>Light passing closer than this (3√3 M ≈ 5.2 M) falls in: the radius of the shadow.</summary>
    public static readonly double CriticalImpact = 3 * Math.Sqrt(3);

    /// <summary>GM/c² for one mass of the Sun, km.</summary>
    public const double SunMassKm = 1.476625;

    /// <summary>The Schwarzschild radius (2GM/c²) of a mass given in masses of the Sun, km.</summary>
    public static double SchwarzschildKm(double sunMasses) => 2 * SunMassKm * sunMasses;

    #region light

    /// <summary>
    /// The light ray's equation in the plane it travels in: u = 1/r against the angle φ round the hole, u'' = 3u² − u
    /// (Newton's straight line would be u'' = −u). Returns (u', u'') for the state (u, u').
    /// </summary>
    public static (double Du, double Ddu) LightSlope(double u, double du) => (du, (3 * u * u) - u);

    /// <summary>
    /// The angle a ray passing with impact parameter <paramref name="b"/> (from far away to far away) is turned through,
    /// radians, or null if it falls in. For large b this is close to 4/b (Einstein's light bending).
    /// </summary>
    public static double? Deflection(double b, double step = 0.002)
    {
        var path = TraceLight(b, 0, step);
        return path.Captured ? null : path.EndAngle - Math.PI;
    }

    /// <summary>
    /// A light ray followed from a point at radius <paramref name="r0"/> (0 for far away) heading inwards with impact
    /// parameter <paramref name="b"/>: u = 1/r sampled every <paramref name="step"/> radians of φ, until it falls through
    /// the horizon or gets back out to infinity. At most <paramref name="maxAngle"/>.
    /// </summary>
    public static LightPath TraceLight(double b, double r0, double step = 0.01, double maxAngle = 5 * Math.PI)
    {
        var u0 = r0 > 0 ? 1 / r0 : 0;
        var slope2 = (1 / (b * b)) - (u0 * u0 * (1 - (2 * u0)));
        var u = u0;
        var du = Math.Sqrt(Math.Max(0, slope2));
        var samples = new List<float> { (float)u };
        var phi = 0.0;
        var turned = false;
        while (phi < maxAngle)
        {
            var (nu, ndu) = Rk4(u, du, step, LightSlope);
            if (nu >= 0.5)
            {
                // Through the horizon: find where u reached 1/2 on this step.
                var f = (0.5 - u) / (nu - u);
                samples.Add(0.5f);
                return new LightPath(b, [.. samples], step, phi + (f * step), true);
            }

            if (ndu < 0) turned = true;
            if (turned && nu <= 0)
            {
                // Back out to infinity: φ there is the direction the light came from.
                var f = u / Math.Max(1e-12, u - nu);
                samples.Add(0f);
                return new LightPath(b, [.. samples], step, phi + (f * step), false);
            }

            u = nu;
            du = ndu;
            phi += step;
            samples.Add((float)u);
        }

        // Still circling (b is within a hair of the critical value): count it as caught.
        return new LightPath(b, [.. samples], step, phi, true);
    }

    #endregion

    #region matter

    /// <summary>The speed (dφ/dτ times r, in c) and angular momentum of a circular orbit at radius r (r > 3).</summary>
    public static double CircularAngularMomentum(double r) => r / Math.Sqrt(r - 3);

    /// <summary>The energy per unit mass (E, 1 at rest far away) of a circular orbit at radius r.</summary>
    public static double CircularEnergy(double r) => (1 - (2 / r)) / Math.Sqrt(1 - (3 / r));

    /// <summary>The effective potential E² of a particle with angular momentum L at radius r: (1 − 2/r)(1 + L²/r²).</summary>
    public static double EffectivePotential(double r, double l) => (1 - (2 / r)) * (1 + (l * l / (r * r)));

    /// <summary>Newton's effective potential on the same scale: 1 − 2/r + L²/r² (1 + twice the energy per mass).</summary>
    public static double NewtonPotential(double r, double l) => 1 - (2 / r) + (l * l / (r * r));

    /// <summary>The energy E of a particle at r moving out at dr/dτ = <paramref name="rDot"/> with angular momentum L.</summary>
    public static double Energy(double r, double rDot, double l) => Math.Sqrt((rDot * rDot) + EffectivePotential(r, l));

    /// <summary>
    /// The angular momentum of a bound orbit turning at <paramref name="far"/> and <paramref name="near"/>: the L for which
    /// the effective potential is the same at both radii.
    /// </summary>
    public static double AngularMomentumForTurns(double far, double near)
    {
        var k = (1 / (far * far)) - (2 / (far * far * far)) - (1 / (near * near)) + (2 / (near * near * near));
        return Math.Sqrt(((2 / far) - (2 / near)) / k);
    }

    /// <summary>The radii of the top of the potential barrier and the bottom of the well for angular momentum L, if any (L > √12).</summary>
    public static (double Barrier, double Well)? PotentialTurns(double l)
    {
        var d = (l * l) - 12;
        if (d < 0) return null;
        var s = Math.Sqrt(d);
        return ((l * (l - s)) / 2, (l * (l + s)) / 2);
    }

    /// <summary>
    /// How fast the probe's own clock runs compared with a clock far away, dτ/dt = (1 − 2/r)/E. 0 at the horizon: seen
    /// from far away, the probe's clock (and the probe) seems to stop there.
    /// </summary>
    public static double ClockRate(double r, double energy) => Math.Max(0, (1 - (2 / r)) / energy);

    /// <summary>How much slower a clock held still at radius r runs than one far away: √(1 − 2/r).</summary>
    public static double StaticClockRate(double r) => r <= Horizon ? 0 : Math.Sqrt(1 - (2 / r));

    #endregion

    #region the disk

    /// <summary>The disk's orbital angular speed at r (Kepler's, which is exact here in coordinate time): r^−3/2.</summary>
    public static double DiskAngularSpeed(double r) => Math.Pow(r, -1.5);

    /// <summary>
    /// The factor g = (seen frequency) / (sent frequency) for light from the disk at radius r reaching a far viewer with
    /// angular momentum <paramref name="lz"/> about the disk's axis: √(1 − 3/r) / (1 − Ω·Lz). Gas coming towards the
    /// viewer (Lz along the orbit) is blueshifted and brightened; the √(1 − 3/r) is the pull of gravity and the gas's time
    /// dilation together.
    /// </summary>
    public static double DiskShift(double r, double lz, bool doppler = true) =>
        Math.Sqrt(1 - (3 / r)) / (doppler ? 1 - (DiskAngularSpeed(r) * lz) : 1);

    /// <summary>
    /// How strongly a thin disk glows at r (r ≥ 6), the shape of Page and Thorne's flux simplified to r⁻³(1 − √(6/r)),
    /// scaled so its peak is 1.
    /// </summary>
    public static double DiskGlow(double r)
    {
        if (r <= Isco) return 0;
        var f = (1 - Math.Sqrt(Isco / r)) / (r * r * r);
        return f / DiskGlowPeak;
    }

    /// <summary>The peak of r⁻³(1 − √(6/r)): at r = 49/6 · … found by setting the slope to zero (r = 8.167).</summary>
    private static readonly double DiskGlowPeak = PeakGlow();

    private static double PeakGlow()
    {
        var r = 49.0 / 6;
        return (1 - Math.Sqrt(Isco / r)) / (r * r * r);
    }

    /// <summary>The glowing gas's temperature relative to its hottest ring: glow^¼.</summary>
    public static double DiskTemperature(double r) => Math.Pow(DiskGlow(r), 0.25);

    #endregion

    #region lensing

    /// <summary>
    /// Where a point of the sky at <paramref name="image"/> (relative to the lens, in units of the Einstein radius) really
    /// is: β = θ − θ/|θ|² (the lens equation of a point mass).
    /// </summary>
    public static (double X, double Y) LensSourceOf(double x, double y)
    {
        var d2 = (x * x) + (y * y);
        if (d2 < 1e-12) return (double.PositiveInfinity, double.PositiveInfinity);
        return (x - (x / d2), y - (y / d2));
    }

    /// <summary>
    /// The two images of a point source at distance <paramref name="u"/> (in Einstein radii) from the lens: their
    /// distances from the lens, on the source's side (+) and the opposite side (−), and how much each is brightened.
    /// </summary>
    public static (double Outer, double Inner, double OuterGain, double InnerGain) LensImages(double u)
    {
        var s = Math.Sqrt((u * u) + 4);
        var outer = (u + s) / 2;
        var inner = (u - s) / 2;
        var total = Magnification(u);
        return (outer, inner, (total + 1) / 2, (total - 1) / 2);
    }

    /// <summary>How much brighter a point source looks in total behind a point lens: (u² + 2)/(u√(u² + 4)).</summary>
    public static double Magnification(double u)
    {
        u = Math.Max(u, 1e-6);
        return ((u * u) + 2) / (u * Math.Sqrt((u * u) + 4));
    }

    /// <summary>
    /// The Einstein radius, arcseconds, of a lens of <paramref name="sunMasses"/> at <paramref name="lensDistance"/> in
    /// front of a source at <paramref name="sourceDistance"/> (both in the same unit of <paramref name="metresPerUnit"/>
    /// metres): θE = √(4GM/c² · Dls / (Dl·Ds)), with Dls = Ds − Dl (flat space, fine within our galaxy and a rough guide
    /// further out).
    /// </summary>
    public static double EinsteinRadiusArcsec(double sunMasses, double lensDistance, double sourceDistance, double metresPerUnit)
    {
        var gm = SunMassKm * 1000 * sunMasses;
        var dl = lensDistance * metresPerUnit;
        var ds = sourceDistance * metresPerUnit;
        var theta = Math.Sqrt(4 * gm * (ds - dl) / (dl * ds));
        return theta * 180 / Math.PI * 3600;
    }

    #endregion

    /// <summary>One classic fourth-order Runge–Kutta step of a second-order equation y'' = f(y, y').</summary>
    internal static (double Y, double Dy) Rk4(double y, double dy, double h, Func<double, double, (double, double)> f)
    {
        var (k1y, k1v) = f(y, dy);
        var (k2y, k2v) = f(y + (h / 2 * k1y), dy + (h / 2 * k1v));
        var (k3y, k3v) = f(y + (h / 2 * k2y), dy + (h / 2 * k2v));
        var (k4y, k4v) = f(y + (h * k3y), dy + (h * k3v));
        return (y + (h / 6 * (k1y + (2 * k2y) + (2 * k3y) + k4y)), dy + (h / 6 * (k1v + (2 * k2v) + (2 * k3v) + k4v)));
    }
}

/// <summary>
/// A light ray in its own plane: u = 1/r every <see cref="Step"/> radians of φ from where it was followed from, and the
/// angle where it ended (through the horizon if <see cref="Captured"/>, else back out).
/// </summary>
public sealed record LightPath(double Impact, float[] U, double Step, double EndAngle, bool Captured)
{
    /// <summary>u at angle φ (between samples, straight in between), 0 past the end of an escaping ray.</summary>
    public double UAt(double phi)
    {
        var x = phi / Step;
        var i = (int)x;
        if (i < 0) return U[0];
        if (i >= U.Length - 1) return U[^1];
        var f = x - i;
        return U[i] + ((U[i + 1] - U[i]) * f);
    }

    /// <summary>r at angle φ.</summary>
    public double RAt(double phi)
    {
        var u = UAt(phi);
        return u <= 1e-9 ? double.PositiveInfinity : 1 / u;
    }

    /// <summary>The ray's closest approach to the hole (the horizon for a ray that falls in).</summary>
    public double Closest => 1 / U.Max();
}

/// <summary>
/// A probe (or anything with mass) moving freely round the hole, followed in its own time τ with general relativity's
/// equation of motion, d²r/dτ² = −1/r² + L²/r³ − 3L²/r⁴, or Newton's (no last term) for comparison. Also keeps the time
/// a clock far away shows.
/// </summary>
public sealed class Probe
{
    public Probe(double r, double phi, double rDot, double l, bool newton)
    {
        R = r;
        Phi = phi;
        RDot = rDot;
        L = l;
        IsNewton = newton;
        Energy = newton ? 0 : BlackHoleModel.Energy(r, rDot, l);
    }

    public double R { get; private set; }

    public double Phi { get; private set; }

    /// <summary>dr/dτ.</summary>
    public double RDot { get; private set; }

    /// <summary>Angular momentum per unit mass, r²·dφ/dτ.</summary>
    public double L { get; }

    /// <summary>Energy per unit mass (relativity only; 1 for a probe at rest far away).</summary>
    public double Energy { get; }

    public bool IsNewton { get; }

    /// <summary>The probe's own time so far.</summary>
    public double ProperTime { get; private set; }

    /// <summary>A far-away clock's time so far (infinite once the probe has crossed the horizon).</summary>
    public double FarTime { get; private set; }

    /// <summary>The probe has crossed the horizon (relativity) or come within it (Newton, which has no horizon but is stopped there too).</summary>
    public bool IsLost { get; private set; }

    /// <summary>The probe has gone beyond <see cref="EscapeRadius"/> heading out.</summary>
    public bool HasEscaped => R > EscapeRadius && RDot > 0;

    public const double EscapeRadius = 60;

    public double X => R * Math.Cos(Phi);

    public double Y => R * Math.Sin(Phi);

    /// <summary>Moves the probe on by <paramref name="dtau"/> of its own time, in small steps (smaller close in).</summary>
    public void Advance(double dtau)
    {
        if (IsLost) return;
        var left = dtau;
        while (left > 1e-12 && !IsLost)
        {
            var h = Math.Min(left, Math.Clamp(0.02 * R * Math.Sqrt(R), 0.002, 0.5));
            Step(h);
            left -= h;
        }
    }

    private void Step(double h)
    {
        (double, double) F(double r, double v) => (v, Accel(r));
        var r0 = R;
        var (r, v) = BlackHoleModel.Rk4(R, RDot, h, F);

        // φ and the far clock by the trapezium rule on the step.
        var phiRate = (L / (r0 * r0) + (L / (r * r))) / 2;
        Phi += phiRate * h;
        if (!IsNewton)
        {
            var lapse0 = 1 - (2 / r0);
            var lapse1 = 1 - (2 / r);
            if (lapse1 <= 0.002 || r <= BlackHoleModel.Horizon)
            {
                FarTime = double.PositiveInfinity;
            }
            else
            {
                FarTime += Energy * ((1 / lapse0) + (1 / lapse1)) / 2 * h;
            }
        }
        else
        {
            FarTime += h;
        }

        R = r;
        RDot = v;
        ProperTime += h;
        if (R <= BlackHoleModel.Horizon)
        {
            R = BlackHoleModel.Horizon;
            IsLost = true;
        }
    }

    private double Accel(double r)
    {
        var l2 = L * L;
        var a = (-1 / (r * r)) + (l2 / (r * r * r));
        return IsNewton ? a : a - (3 * l2 / (r * r * r * r));
    }

    /// <summary>The starting state of one of the ready launches: r, dr/dτ and L.</summary>
    public static (double R, double RDot, double L) Launch(ProbeLaunch launch) => launch switch
    {
        ProbeLaunch.Rosette => (18, 0, BlackHoleModel.AngularMomentumForTurns(18, 10)),
        ProbeLaunch.Isco => (6.05, -0.01, BlackHoleModel.CircularAngularMomentum(6)),
        ProbeLaunch.Plunge => (12, 0, 3.4),
        ProbeLaunch.Escape => (10, 0.2, 5.6),
        _ => (10, 0, BlackHoleModel.CircularAngularMomentum(10)),
    };
}

/// <summary>Real black holes the up-close view can stand for (its picture is the same for any mass, only its size changes).</summary>
public sealed record BlackHoleExample(string Id, double SunMasses, double DistanceLightYears)
{
    /// <summary>Cygnus X-1, a black hole of about 21 Suns orbiting a giant star, 7200 light years away.</summary>
    public static readonly BlackHoleExample CygnusX1 = new("CygX1", 21, 7200);

    /// <summary>Sagittarius A*, the black hole at the centre of our galaxy: 4.3 million Suns, 26,700 light years away.</summary>
    public static readonly BlackHoleExample SgrA = new("SgrA", 4.3e6, 26700);

    /// <summary>M87*, the first black hole photographed (2019): 6.5 billion Suns, 55 million light years away.</summary>
    public static readonly BlackHoleExample M87 = new("M87", 6.5e9, 55e6);

    public static IReadOnlyList<BlackHoleExample> All { get; } = [CygnusX1, SgrA, M87];

    public static BlackHoleExample? Find(string? id) => All.FirstOrDefault(e => e.Id == id);

    /// <summary>The event horizon's radius, km.</summary>
    public double HorizonKm => BlackHoleModel.SchwarzschildKm(SunMasses);

    /// <summary>The shadow's diameter on the sky seen from Earth, micro-arcseconds: 2·3√3·GM/c² over the distance.</summary>
    public double ShadowMicroArcsec
    {
        get
        {
            const double kmPerLightYear = 9.4607e12;
            var diameterKm = 2 * BlackHoleModel.CriticalImpact * BlackHoleModel.SunMassKm * SunMasses;
            return diameterKm / (DistanceLightYears * kmPerLightYear) * 180 / Math.PI * 3600 * 1e6;
        }
    }

    /// <summary>How long light takes to go once round the photon sphere (2π·3 GM/c³), seconds.</summary>
    public double PhotonLapSeconds => 2 * Math.PI * BlackHoleModel.PhotonSphere * BlackHoleModel.SunMassKm * SunMasses / 299792.458;

    /// <summary>The gas's orbit time at the inner edge of the disk (2π·6^1.5 GM/c³), seconds.</summary>
    public double IscoOrbitSeconds => 2 * Math.PI * Math.Pow(BlackHoleModel.Isco, 1.5) * BlackHoleModel.SunMassKm * SunMasses / 299792.458;
}

/// <summary>The lenses the lensing view can stand for: their mass and distances set the true Einstein radius.</summary>
public sealed record LensExample(string Id, double SunMasses, double LensDistance, double SourceDistance, double MetresPerUnit)
{
    private const double Kiloparsec = 3.0857e19;
    private const double Megaparsec = 3.0857e22;

    /// <summary>A star-sized black hole halfway to the stars of our galaxy's centre (microlensing, OGLE-2011-BLG-0462).</summary>
    public static readonly LensExample StellarHole = new("Stellar", 7, 1.6, 8, Kiloparsec);

    /// <summary>A big galaxy (its central black hole and all its stars and dark matter) 1 billion light years away, lensing a galaxy twice as far.</summary>
    public static readonly LensExample Galaxy = new("Galaxy", 1e12, 300, 600, Megaparsec);

    /// <summary>A galaxy cluster like Abell 1689 (the mass inside its ring), 2 billion light years away, lensing galaxies far behind it.</summary>
    public static readonly LensExample Cluster = new("Cluster", 2e14, 600, 2000, Megaparsec);

    public static IReadOnlyList<LensExample> All { get; } = [StellarHole, Galaxy, Cluster];

    public static LensExample? Find(string? id) => All.FirstOrDefault(e => e.Id == id);

    /// <summary>The Einstein radius of this lens with its mass times <paramref name="massFactor"/>, arcseconds.</summary>
    public double EinsteinArcsec(double massFactor = 1) =>
        BlackHoleModel.EinsteinRadiusArcsec(SunMasses * massFactor, LensDistance, SourceDistance, MetresPerUnit);
}
