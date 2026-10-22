using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Kepler;

/// <summary>What the Kepler's laws page shows.</summary>
public enum KeplerMode
{
    /// <summary>First law: the orbit is an ellipse with the Sun at one focus; the pins-and-string construction.</summary>
    Ellipse,

    /// <summary>Second law: the line from the Sun sweeps equal areas in equal times, so the planet is fastest at perihelion.</summary>
    Areas,

    /// <summary>Third law: the square of the period goes with the cube of the size of the orbit; the planets and your own.</summary>
    Periods,
}

/// <summary>Where a body is on its orbit, as the page names it.</summary>
public enum OrbitStage
{
    /// <summary>Near perihelion: closest to the Sun and fastest.</summary>
    Perihelion,

    /// <summary>Moving away from the Sun and slowing down.</summary>
    Receding,

    /// <summary>Near aphelion: farthest from the Sun and slowest.</summary>
    Aphelion,

    /// <summary>Falling back towards the Sun and speeding up.</summary>
    Approaching,

    /// <summary>A circle (or nearly): the same distance and speed all the way round.</summary>
    Circle,
}

/// <summary>A body whose orbit the ellipse and areas views can show: its size (AU) and eccentricity.</summary>
public sealed record KeplerBody(string Id, double SemiMajorAxisAu, double Eccentricity)
{
    /// <summary>The year of the body in Earth years, from the third law.</summary>
    public double PeriodYears => KeplerGeometry.PeriodYears(SemiMajorAxisAu);

    public double PerihelionAu => SemiMajorAxisAu * (1 - Eccentricity);

    public double AphelionAu => SemiMajorAxisAu * (1 + Eccentricity);
}

/// <summary>A body for the third-law chart: its real size of orbit and its real period.</summary>
public readonly record struct PeriodPoint(string Id, double SemiMajorAxisAu, double PeriodYears)
{
    /// <summary>T² / a³ in years and AU: 1 for anything going round the Sun.</summary>
    public double Ratio => PeriodYears * PeriodYears / Math.Pow(SemiMajorAxisAu, 3);
}

/// <summary>A point of an orbit at one moment, in units of the semi-major axis, the Sun at the origin.</summary>
/// <param name="Phase">Fraction of the period since perihelion, 0…1 (the mean anomaly over 2π).</param>
/// <param name="EccentricAnomaly">Radians.</param>
/// <param name="TrueAnomaly">The angle at the Sun from perihelion, radians, 0…2π.</param>
/// <param name="Radius">Distance from the Sun.</param>
/// <param name="X">Towards perihelion.</param>
/// <param name="Y">90° ahead of perihelion, the way the body goes round.</param>
/// <param name="Speed">In units of the speed on a circle of the same size (<c>√(GM/a)</c>).</param>
/// <param name="DirectionX">Unit vector of the motion.</param>
/// <param name="DirectionY">Unit vector of the motion.</param>
public readonly record struct OrbitPoint(
    double Phase, double EccentricAnomaly, double TrueAnomaly, double Radius,
    double X, double Y, double Speed, double DirectionX, double DirectionY);

/// <summary>
/// Kepler's three laws as numbers: positions on an ellipse in time (Kepler's equation), the area swept, the speed
/// (vis-viva), and the third law relating period and size of orbit, with real orbits for comparison.
/// </summary>
/// <remarks>
/// Orbits of the first two views are in units of the semi-major axis <c>a</c> with the Sun at the origin and perihelion
/// on +x; the body goes round counter-clockwise. Time is a <c>phase</c>, the fraction of the period since perihelion.
/// The third-law view uses AU and Earth years. Angles are in radians unless the name says otherwise.
/// </remarks>
public static class KeplerGeometry
{
    /// <summary>The Sun's gravitational parameter GM, m³/s² (IAU 2015).</summary>
    public const double SunGm = 1.32712440018e20;

    /// <summary>The astronomical unit, metres.</summary>
    public const double AuMetres = 1.495978707e11;

    public const double DaysPerYear = 365.25;

    /// <summary>Below this the page calls the orbit a circle.</summary>
    public const double CircleEccentricity = 0.01;

    /// <summary>Largest eccentricity the page shows; at 1 the ellipse opens into a parabola and never comes back.</summary>
    public const double MaxEccentricity = 0.97;

    /// <summary>The speed on a circular orbit of 1 AU, km/s: about 29.8, Earth's average speed.</summary>
    public static double CircularSpeedAt1Au => Math.Sqrt(SunGm / AuMetres) / 1000;

    /// <summary>
    /// Bodies the first two views offer. Planets come from the Solar System page's orbits (JPL, at J2000); Pluto and
    /// Halley's comet from JPL's small-body database. "Custom" is the example orbit the eccentricity slider changes.
    /// </summary>
    public static IReadOnlyList<KeplerBody> Bodies { get; } =
    [
        new("Custom", 1, 0.5),
        PlanetBody("Earth"),
        PlanetBody("Mars"),
        PlanetBody("Mercury"),
        new("Pluto", 39.48, 0.2488),
        new("Halley", 17.83, 0.967),
    ];

    /// <summary>
    /// Planets, dwarf planets and Halley's comet with their measured periods, for the third law: all fall on T² = a³.
    /// Planets' periods are NASA's fact sheet values (the Solar System page's).
    /// </summary>
    public static IReadOnlyList<PeriodPoint> PeriodPoints { get; } = BuildPeriodPoints();

    public static KeplerBody Body(string id) => Bodies.FirstOrDefault(b => b.Id == id) ?? Bodies[0];

    #region third law

    /// <summary>The period in Earth years of an orbit <paramref name="semiMajorAxisAu"/> AU across (half the long axis): T² = a³.</summary>
    public static double PeriodYears(double semiMajorAxisAu) => Math.Pow(semiMajorAxisAu, 1.5);

    /// <summary>The same from Newton's form T = 2π √(a³ / GM), in days.</summary>
    public static double PeriodDays(double semiMajorAxisAu)
    {
        var a = semiMajorAxisAu * AuMetres;
        return 2 * Math.PI * Math.Sqrt(a * a * a / SunGm) / 86_400;
    }

    /// <summary>The size of orbit for a period: a = T^(2/3).</summary>
    public static double SemiMajorAxisAu(double periodYears) => Math.Pow(periodYears, 2.0 / 3);

    /// <summary>The average speed round a circle <paramref name="semiMajorAxisAu"/> AU from the Sun, km/s.</summary>
    public static double CircularSpeedKmS(double semiMajorAxisAu) => CircularSpeedAt1Au / Math.Sqrt(semiMajorAxisAu);

    #endregion

    #region position in time

    /// <summary>Solves Kepler's equation M = E − e sin E for the eccentric anomaly E (Newton's method).</summary>
    public static double EccentricAnomaly(double meanAnomaly, double eccentricity)
    {
        var m = WrapAngle(meanAnomaly);
        var e = Math.Clamp(eccentricity, 0, 0.999);

        // Starting at π converges for every M when the orbit is very long.
        var ecc = e > 0.8 ? Math.PI : m;
        for (var k = 0; k < 50; k++)
        {
            var delta = (ecc - (e * Math.Sin(ecc)) - m) / (1 - (e * Math.Cos(ecc)));
            ecc -= delta;
            if (Math.Abs(delta) < 1e-13) break;
        }

        return ecc;
    }

    /// <summary>The angle at the Sun from perihelion for an eccentric anomaly, 0…2π.</summary>
    public static double TrueAnomaly(double eccentricAnomaly, double eccentricity)
    {
        var half = eccentricAnomaly / 2;
        var nu = 2 * Math.Atan2(Math.Sqrt(1 + eccentricity) * Math.Sin(half), Math.Sqrt(1 - eccentricity) * Math.Cos(half));
        return WrapAngle(nu);
    }

    /// <summary>The phase (fraction of the period since perihelion) at which the body is at <paramref name="trueAnomaly"/>.</summary>
    public static double PhaseAt(double trueAnomaly, double eccentricity)
    {
        var half = trueAnomaly / 2;
        var ecc = 2 * Math.Atan2(Math.Sqrt(1 - eccentricity) * Math.Sin(half), Math.Sqrt(1 + eccentricity) * Math.Cos(half));
        return WrapAngle(ecc - (eccentricity * Math.Sin(ecc))) / (2 * Math.PI);
    }

    /// <summary>Where the body is <paramref name="phase"/> of a period after perihelion, and how fast it goes.</summary>
    public static OrbitPoint At(double phase, double eccentricity)
    {
        var e = Math.Clamp(eccentricity, 0, 0.999);
        phase = Wrap(phase);
        var ecc = EccentricAnomaly(phase * 2 * Math.PI, e);
        var (sinE, cosE) = Math.SinCos(ecc);
        var b = Math.Sqrt(1 - (e * e));
        var x = cosE - e;
        var y = b * sinE;
        var r = 1 - (e * cosE);

        // The motion is along d(x, y)/dE.
        var dx = -sinE;
        var dy = b * cosE;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        return new OrbitPoint(phase, ecc, TrueAnomaly(ecc, e), r, x, y, VisViva(r), dx / length, dy / length);
    }

    /// <summary>Speed at <paramref name="radius"/> (in units of a) in units of the circular speed at a: v² = GM (2/r − 1/a).</summary>
    public static double VisViva(double radius) => Math.Sqrt(Math.Max(0, (2 / radius) - 1));

    /// <summary>Speed in km/s at <paramref name="radius"/> (units of a) on an orbit <paramref name="semiMajorAxisAu"/> AU across.</summary>
    public static double SpeedKmS(double semiMajorAxisAu, double radius) => CircularSpeedKmS(semiMajorAxisAu) * VisViva(radius);

    /// <summary>A point on the ellipse at an eccentric anomaly, in units of a, the Sun at the origin.</summary>
    public static (double X, double Y) PointAt(double eccentricAnomaly, double eccentricity)
    {
        var (sinE, cosE) = Math.SinCos(eccentricAnomaly);
        return (cosE - eccentricity, Math.Sqrt(1 - (eccentricity * eccentricity)) * sinE);
    }

    #endregion

    #region areas

    /// <summary>The area of the whole ellipse, in units of a²: π a b.</summary>
    public static double EllipseArea(double eccentricity) => Math.PI * Math.Sqrt(1 - (eccentricity * eccentricity));

    /// <summary>
    /// The area the line from the Sun sweeps between two phases (units of a²). The second law: it is the time
    /// between them times a constant, whatever the shape.
    /// </summary>
    public static double SweptArea(double fromPhase, double toPhase, double eccentricity) =>
        EllipseArea(eccentricity) * (toPhase - fromPhase);

    /// <summary>The same area added up from thin triangles at the Sun, which is how the tests check the law.</summary>
    public static double SweptAreaBySlices(double fromPhase, double toPhase, double eccentricity, int slices = 4000)
    {
        var area = 0.0;
        var previous = At(fromPhase, eccentricity);
        for (var i = 1; i <= slices; i++)
        {
            var phase = fromPhase + ((toPhase - fromPhase) * i / slices);
            var next = At(phase, eccentricity);
            area += ((previous.X * next.Y) - (next.X * previous.Y)) / 2;
            previous = next;
        }

        return area;
    }

    #endregion

    /// <summary>Near perihelion or aphelion (within 25° at the Sun), or between them, or a circle.</summary>
    public static OrbitStage StageAt(double phase, double eccentricity)
    {
        if (eccentricity < CircleEccentricity) return OrbitStage.Circle;

        var nu = At(phase, eccentricity).TrueAnomaly * 180 / Math.PI;
        if (nu < 25 || nu > 335) return OrbitStage.Perihelion;
        if (Math.Abs(nu - 180) < 25) return OrbitStage.Aphelion;
        return nu < 180 ? OrbitStage.Receding : OrbitStage.Approaching;
    }

    /// <summary>
    /// The next time after <paramref name="utc"/> a planet of the Solar System page is at perihelion (mean anomaly 0)
    /// or aphelion (180°), from its mean motion. For Earth this is within a day or two of the published dates: those
    /// are for Earth itself, the elements for the Earth–Moon pair.
    /// </summary>
    public static DateTime NextApsis(KeplerOrbit orbit, DateTime utc, bool perihelion)
    {
        var jd = KeplerOrbit.JulianDay(utc);
        var state = orbit.At(jd);
        var target = perihelion ? 0 : Math.PI;
        var ahead = WrapAngle(target - WrapAngle(state.MeanAnomaly));
        if (ahead < 1e-9) ahead += 2 * Math.PI;

        // Mean motion, radians per day: the mean longitude's rate less the perihelion's.
        var perDay = (orbit.MeanLongitudeRate - orbit.PerihelionLongitudeRate) * Math.PI / 180 / 36525;
        return utc.ToUniversalTime().AddDays(ahead / perDay);
    }

    public static double Wrap(double phase)
    {
        phase %= 1;
        return phase < 0 ? phase + 1 : phase;
    }

    /// <summary>An angle in 0…2π.</summary>
    public static double WrapAngle(double radians)
    {
        radians %= 2 * Math.PI;
        return radians < 0 ? radians + (2 * Math.PI) : radians;
    }

    private static KeplerBody PlanetBody(string id)
    {
        var orbit = SolarSystemCatalog.Bodies.First(b => b.Id == id).Orbit!;
        return new KeplerBody(id, orbit.SemiMajorAxis, orbit.Eccentricity);
    }

    private static List<PeriodPoint> BuildPeriodPoints()
    {
        var points = SolarSystemCatalog.Bodies
            .Where(b => b.Orbit is not null && b.OrbitalPeriodDays is not null)
            .Select(b => new PeriodPoint(b.Id, b.Orbit!.SemiMajorAxis, b.OrbitalPeriodDays!.Value / DaysPerYear))
            .ToList();

        // Ceres in the asteroid belt, Pluto, and Halley's comet: measured periods from JPL.
        points.Add(new PeriodPoint("Ceres", 2.766, 4.60));
        points.Add(new PeriodPoint("Pluto", 39.48, 247.94));
        points.Add(new PeriodPoint("Halley", 17.83, 75.32));
        return points.OrderBy(p => p.SemiMajorAxisAu).ToList();
    }
}
