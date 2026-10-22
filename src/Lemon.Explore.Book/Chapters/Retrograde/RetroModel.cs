using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Retrograde;

/// <summary>The three views of the retrograde page.</summary>
public enum RetroMode
{
    /// <summary>The planet's path among the stars along the ecliptic: drifting east, stopping, going back west, stopping again.</summary>
    Sky,

    /// <summary>Seen from above the Sun: Earth on the inside lane overtakes the planet, and the line of sight swings back.</summary>
    Above,

    /// <summary>The same motion from Earth's centre (Ptolemy's deferent and epicycle) and from the Sun's (Copernicus).</summary>
    Epicycle,
}

/// <summary>The planets whose retrograde loops the page shows.</summary>
public enum RetroPlanet
{
    Mercury,
    Venus,
    Mars,
    Jupiter,
    Saturn,
}

/// <summary>A position in space, astronomical units, in J2000 ecliptic coordinates (x towards the March equinox, z to the north ecliptic pole).</summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public double Length => Math.Sqrt((X * X) + (Y * Y) + (Z * Z));

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vec3 operator *(Vec3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);
}

/// <summary>Where a planet appears from Earth: ecliptic longitude and latitude (degrees) and its distance (AU).</summary>
public readonly record struct GeoPosition(double Longitude, double Latitude, double Distance);

/// <summary>
/// One retrograde period of a planet, as days since J2000 (see <see cref="RetroModel.DayOf"/>): when it stops and turns
/// west, when it stops and turns east again, and the middle (opposition for an outer planet, inferior conjunction for
/// Mercury and Venus), when it goes back fastest.
/// </summary>
public sealed record RetroPeriod(double Start, double End, double Middle)
{
    public double Duration => End - Start;

    public bool Contains(double day) => day >= Start && day <= End;
}

/// <summary>
/// The geometry of the retrograde page: heliocentric positions of Earth and five planets from JPL's approximate Keplerian
/// elements (the same table the solar-system page uses, <see cref="SolarSystemCatalog"/>), where each appears from
/// Earth, how fast its ecliptic longitude changes, and the retrograde periods found from that rate.
/// </summary>
/// <remarks>
/// Positions are geometric (no light time, aberration or precession), which moves the stations by well under a day.
/// The elements are fitted to 1800–2050; the page stays within 1950–2060.
/// </remarks>
public static class RetroModel
{
    private const double Deg = Math.PI / 180;

    /// <summary>The tilt of Earth's axis to its orbit at J2000, degrees.</summary>
    public const double Obliquity = 23.4392911;

    /// <summary>Kilometres in an astronomical unit.</summary>
    public const double KmPerAu = 149_597_870.7;

    /// <summary>2000 January 1, 12:00 UT: day 0.</summary>
    public static readonly DateTime J2000 = new(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>The first and last days the page goes to.</summary>
    public static double MinDay { get; } = DayOf(new DateTime(1950, 1, 1));

    public static double MaxDay { get; } = DayOf(new DateTime(2060, 12, 31));

    private static readonly IReadOnlyDictionary<string, KeplerOrbit> Orbits = new[] { "Mercury", "Venus", "Earth", "Mars", "Jupiter", "Saturn" }
        .ToDictionary(id => id, id => SolarSystemCatalog.Bodies.First(b => b.Id == id).Orbit!);

    private static readonly Dictionary<RetroPlanet, IReadOnlyList<RetroPeriod>> PeriodCache = [];
    private static readonly Lock CacheLock = new();

    #region dates

    /// <summary>Days since J2000 of a calendar date (its midnight, as a UTC date).</summary>
    public static double DayOf(DateTime date) => (DateTime.SpecifyKind(date, DateTimeKind.Utc) - J2000).TotalDays;

    /// <summary>The moment <paramref name="day"/> days after J2000.</summary>
    public static DateTime DateOf(double day) => J2000.AddDays(Math.Clamp(day, -200_000, 200_000));

    /// <summary>Today's local date as a day number.</summary>
    public static double Today => DayOf(DateTime.Today);

    #endregion

    #region planets

    /// <summary>The planet's id in the solar-system catalogue (also its resource names).</summary>
    public static string Id(RetroPlanet planet) => planet.ToString();

    /// <summary>Mercury and Venus go round inside Earth's orbit.</summary>
    public static bool IsInner(RetroPlanet planet) => planet is RetroPlanet.Mercury or RetroPlanet.Venus;

    /// <summary>The planet's semi-major axis at J2000, AU.</summary>
    public static double SemiMajorAxis(RetroPlanet planet) => Orbits[Id(planet)].SemiMajorAxis;

    /// <summary>The planet's year, days (Kepler's third law).</summary>
    public static double OrbitDays(RetroPlanet planet) => OrbitDays(Id(planet));

    private static double OrbitDays(string id) => 365.256363 * Math.Pow(Orbits[id].SemiMajorAxis, 1.5);

    /// <summary>The synodic period: from one opposition (or inferior conjunction) to the next, days.</summary>
    public static double SynodicDays(RetroPlanet planet) => 1 / Math.Abs((1 / OrbitDays(planet)) - (1 / OrbitDays("Earth")));

    /// <summary>
    /// Half the span of days the page shows round one retrograde loop: between 1.5 and 3 times half the loop's length, so
    /// the path just crosses back over the loop, and of seven equal steps through the span the middle three go backwards.
    /// </summary>
    public static double HalfWindow(RetroPlanet planet) => planet switch
    {
        RetroPlanet.Mercury => 26,
        RetroPlanet.Venus => 50,
        RetroPlanet.Mars => 90,
        RetroPlanet.Jupiter => 130,
        _ => 140,
    };

    /// <summary>Earth's position from the Sun on <paramref name="day"/>.</summary>
    public static Vec3 Earth(double day) => Helio("Earth", day);

    /// <summary>The planet's position from the Sun on <paramref name="day"/>.</summary>
    public static Vec3 Helio(RetroPlanet planet, double day) => Helio(Id(planet), day);

    /// <summary>The planet's position from Earth on <paramref name="day"/>.</summary>
    public static Vec3 FromEarth(RetroPlanet planet, double day) => Helio(planet, day) - Earth(day);

    /// <summary>Where the planet appears from Earth: ecliptic longitude, latitude and distance.</summary>
    public static GeoPosition Geo(RetroPlanet planet, double day) => ToSpherical(FromEarth(planet, day));

    /// <summary>The Sun's ecliptic longitude seen from Earth, degrees.</summary>
    public static double SunLongitude(double day)
    {
        var earth = Earth(day);
        return Wrap360(Math.Atan2(-earth.Y, -earth.X) / Deg);
    }

    /// <summary>How far the planet appears east (+) or west (−) of the Sun, degrees.</summary>
    public static double Elongation(RetroPlanet planet, double day) => Wrap180(Geo(planet, day).Longitude - SunLongitude(day));

    /// <summary>How fast the planet's ecliptic longitude changes, degrees a day: east is positive, retrograde negative.</summary>
    public static double Rate(RetroPlanet planet, double day)
    {
        const double h = 0.25;
        return Wrap180(Geo(planet, day + h).Longitude - Geo(planet, day - h).Longitude) / (2 * h);
    }

    /// <summary>A body's orbital speed at its distance on <paramref name="day"/>, km/s (vis-viva).</summary>
    public static double SpeedKmS(RetroPlanet? planet, double day)
    {
        var id = planet is { } p ? Id(p) : "Earth";
        var a = Orbits[id].SemiMajorAxis;
        var r = Helio(id, day).Length;
        return 29.7847 * Math.Sqrt(Math.Max(0, (2 / r) - (1 / a)));
    }

    /// <summary><paramref name="count"/> points round a body's orbit (its position through one of its years from <paramref name="day"/>).</summary>
    public static IReadOnlyList<Vec3> OrbitPoints(RetroPlanet? planet, double day, int count = 180)
    {
        var id = planet is { } p ? Id(p) : "Earth";
        var period = OrbitDays(id);
        return Enumerable.Range(0, count).Select(i => Helio(id, day + (period * i / count))).ToList();
    }

    /// <summary>
    /// A body's position from the Sun: Kepler's equation solved for the elements at the date, turned into the ecliptic
    /// frame (Standish's recipe, keeping z).
    /// </summary>
    internal static Vec3 Helio(string id, double day)
    {
        var o = Orbits[id];
        var t = day / 36525.0;
        var a = o.SemiMajorAxis + (o.SemiMajorAxisRate * t);
        var e = o.Eccentricity + (o.EccentricityRate * t);
        var i = (o.Inclination + (o.InclinationRate * t)) * Deg;
        var l = o.MeanLongitude + (o.MeanLongitudeRate * t);
        var varpi = o.PerihelionLongitude + (o.PerihelionLongitudeRate * t);
        var node = (o.NodeLongitude + (o.NodeLongitudeRate * t)) * Deg;
        var w = (varpi * Deg) - node;
        var m = Wrap180(l - varpi) * Deg;

        var ecc = m + (e * Math.Sin(m));
        for (var k = 0; k < 8; k++)
        {
            var delta = (ecc - (e * Math.Sin(ecc)) - m) / (1 - (e * Math.Cos(ecc)));
            ecc -= delta;
            if (Math.Abs(delta) < 1e-12) break;
        }

        var xp = a * (Math.Cos(ecc) - e);
        var yp = a * Math.Sqrt(1 - (e * e)) * Math.Sin(ecc);
        var (sw, cw) = Math.SinCos(w);
        var (so, co) = Math.SinCos(node);
        var (si, ci) = Math.SinCos(i);
        return new Vec3(
            (((cw * co) - (sw * so * ci)) * xp) + (((-sw * co) - (cw * so * ci)) * yp),
            (((cw * so) + (sw * co * ci)) * xp) + (((-sw * so) + (cw * co * ci)) * yp),
            (sw * si * xp) + (cw * si * yp));
    }

    /// <summary>A vector as ecliptic longitude, latitude (degrees) and length.</summary>
    public static GeoPosition ToSpherical(Vec3 v)
    {
        var r = v.Length;
        return new GeoPosition(Wrap360(Math.Atan2(v.Y, v.X) / Deg), r > 0 ? Math.Asin(Math.Clamp(v.Z / r, -1, 1)) / Deg : 0, r);
    }

    #endregion

    #region retrograde periods

    /// <summary>Every retrograde period of the planet between 1950 and 2060, in order (worked out once per planet).</summary>
    public static IReadOnlyList<RetroPeriod> Periods(RetroPlanet planet)
    {
        lock (CacheLock)
        {
            if (!PeriodCache.TryGetValue(planet, out var periods))
            {
                periods = FindPeriods(planet, MinDay, MaxDay);
                PeriodCache[planet] = periods;
            }

            return periods;
        }
    }

    /// <summary>The retrograde period whose loop the page shows on <paramref name="day"/>: the one whose window holds it, else the nearest.</summary>
    public static RetroPeriod Apparition(RetroPlanet planet, double day)
    {
        var periods = Periods(planet);
        var half = HalfWindow(planet);
        return periods.FirstOrDefault(p => Math.Abs(day - p.Middle) <= half) ?? periods.MinBy(p => Math.Abs(day - p.Middle))!;
    }

    /// <summary>The period going on on <paramref name="day"/>, or null.</summary>
    public static RetroPeriod? During(RetroPlanet planet, double day) => Periods(planet).FirstOrDefault(p => p.Contains(day));

    /// <summary>The next periods that have not ended by <paramref name="day"/>.</summary>
    public static IReadOnlyList<RetroPeriod> Upcoming(RetroPlanet planet, double day, int count) =>
        Periods(planet).Where(p => p.End >= day).Take(count).ToList();

    /// <summary>
    /// Scans the longitude rate a day at a time and narrows each change of sign down by halving: a change from east to west
    /// is the station where the loop begins, west to east where it ends.
    /// </summary>
    internal static IReadOnlyList<RetroPeriod> FindPeriods(RetroPlanet planet, double from, double to)
    {
        var periods = new List<RetroPeriod>();
        double? start = null;
        var previous = Rate(planet, from);
        for (var day = from + 1; day <= to; day++)
        {
            var rate = Rate(planet, day);
            if (previous > 0 && rate <= 0)
            {
                start = Station(planet, day - 1, day);
            }
            else if (previous < 0 && rate >= 0 && start is { } s)
            {
                var end = Station(planet, day - 1, day);
                periods.Add(new RetroPeriod(s, end, Middle(planet, s, end)));
                start = null;
            }

            previous = rate;
        }

        return periods;
    }

    /// <summary>Where the rate changes sign between two days.</summary>
    private static double Station(RetroPlanet planet, double a, double b)
    {
        var fa = Rate(planet, a);
        for (var i = 0; i < 24; i++)
        {
            var mid = (a + b) / 2;
            var fm = Rate(planet, mid);
            if (Math.Sign(fm) == Math.Sign(fa))
            {
                a = mid;
                fa = fm;
            }
            else
            {
                b = mid;
            }
        }

        return (a + b) / 2;
    }

    /// <summary>Opposition (outer planets) or inferior conjunction (inner) inside a period, by halving.</summary>
    private static double Middle(RetroPlanet planet, double start, double end)
    {
        var target = IsInner(planet) ? 0.0 : 180.0;
        double Miss(double day) => Wrap180(Elongation(planet, day) - target);
        double a = start, b = end;
        var fa = Miss(a);
        if (Math.Sign(fa) == Math.Sign(Miss(b))) return (start + end) / 2;
        for (var i = 0; i < 40; i++)
        {
            var mid = (a + b) / 2;
            var fm = Miss(mid);
            if (Math.Sign(fm) == Math.Sign(fa))
            {
                a = mid;
                fa = fm;
            }
            else
            {
                b = mid;
            }
        }

        return (a + b) / 2;
    }

    #endregion

    #region sky coordinates

    /// <summary>A star's J2000 right ascension (hours) and declination (degrees) as ecliptic longitude and latitude.</summary>
    public static (double Longitude, double Latitude) Ecliptic(double raHours, double decDegrees)
    {
        var ra = raHours * 15 * Deg;
        var dec = decDegrees * Deg;
        var (se, ce) = Math.SinCos(Obliquity * Deg);
        var lon = Math.Atan2((Math.Sin(ra) * ce) + (Math.Tan(dec) * se), Math.Cos(ra));
        var lat = Math.Asin(Math.Clamp((Math.Sin(dec) * ce) - (Math.Cos(dec) * se * Math.Sin(ra)), -1, 1));
        return (Wrap360(lon / Deg), lat / Deg);
    }

    public static double Wrap360(double degrees) => ((degrees % 360) + 360) % 360;

    public static double Wrap180(double degrees) => Wrap360(degrees + 180) - 180;

    #endregion
}
