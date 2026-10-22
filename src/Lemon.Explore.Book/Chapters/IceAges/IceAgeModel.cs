namespace Lemon.Explore.Book.Chapters.IceAges;

/// <summary>The ice-age page's three views.</summary>
public enum IceAgeMode
{
    /// <summary>Earth's orbit and axis: the stretch of the orbit, the tilt and the wobble, and how they change over time.</summary>
    Orbit,

    /// <summary>Summer sunshine at 65° N over the last 800,000 years and the northern ice sheets it lets grow or melts.</summary>
    Sunshine,

    /// <summary>A slice through the North American ice sheet: snow piling up, melting at the edges, the ground pressed down.</summary>
    Sheet,
}

/// <summary>Which of the three cycles the orbit view shows changing; the others are held at today's values.</summary>
public enum IceAgeCycle
{
    All,
    Eccentricity,
    Tilt,
    Precession,
}

/// <summary>
/// Earth's orbit and axis at one moment: the eccentricity, the tilt (obliquity, degrees), the longitude of perihelion
/// measured from the moving spring equinox (degrees, Berger's convention: perihelion falls at solar longitude ϖ + 180°)
/// and the general precession since J1950 (degrees, the equinox's drift along the ecliptic).
/// </summary>
public readonly record struct OrbitalElements(double Eccentricity, double Obliquity, double Perihelion, double Precession)
{
    /// <summary>The climatic precession index e·sin ϖ: positive when the far point of the orbit falls in northern summer.</summary>
    public double PrecessionIndex => Eccentricity * Math.Sin(IceAgeModel.Rad(Perihelion));

    /// <summary>The solar longitude (0 = northern spring equinox, 90 = northern summer solstice) at which Earth is closest to the Sun.</summary>
    public double PerihelionLongitude => IceAgeModel.Wrap(Perihelion + 180);
}

/// <summary>
/// The astronomy behind the ice ages. Earth's orbital elements come from the main terms of Berger (1978): ten for the
/// obliquity, ten for e·sin/cos Π and three for the general precession, with t in years from 1950. The truncated series
/// is nudged so 1950's values come out as published (e = 0.016726, ε = 23.446°, ϖ = 102.04°). Daily mean sunshine at the
/// top of the air follows from the Sun's declination and Earth's distance (Berger 1978, Milankovitch's quantity).
/// </summary>
public static class IceAgeModel
{
    /// <summary>The Sun's power through a square metre facing it at 1 AU, W/m².</summary>
    public const double SolarConstant = 1361;

    /// <summary>The latitude whose summer sunshine paces the ice ages (Milankovitch): where the big ice sheets grew.</summary>
    public const double KeyLatitude = 65;

    /// <summary>Days in a tropical year.</summary>
    public const double YearDays = 365.2422;

    /// <summary>The northern spring equinox's day of the year (0 = 1 January): 20 March.</summary>
    public const double EquinoxDay = 78.8;

    // Berger 1978, Table 1: obliquity terms (amplitude ″, frequency ″/yr, phase °); ε* = 23.320556°.
    private static readonly (double A, double F, double D)[] ObliquityTerms =
    [
        (-2462.2214, 31.609974, 251.9025),
        (-857.3232, 32.620504, 280.8325),
        (-629.3231, 24.172203, 128.3057),
        (-414.2804, 31.983787, 292.7252),
        (-311.7632, 44.828336, 15.3747),
        (308.9412, 30.973257, 263.7951),
        (-162.5533, 43.668246, 308.4258),
        (-116.1077, 32.246691, 240.0099),
        (101.1189, 30.599444, 222.9725),
        (-67.6856, 42.681324, 268.7809),
    ];

    // Berger 1978, Table 4: e·sin Π and e·cos Π terms (amplitude, frequency ″/yr, phase °).
    private static readonly (double P, double F, double D)[] EccentricityTerms =
    [
        (0.01860798, 4.207205, 28.620089),
        (0.01627522, 7.346091, 193.788772),
        (-0.01300660, 17.857263, 308.307024),
        (0.00988829, 17.220546, 320.199637),
        (-0.00336700, 16.846733, 279.376984),
        (0.00333077, 5.199079, 87.195000),
        (-0.00235400, 18.231076, 349.129677),
        (0.00140015, 26.216758, 128.443387),
        (0.00100700, 6.359169, 154.143880),
        (0.00085700, 16.210016, 291.269597),
    ];

    // Berger 1978, Table 5: general precession terms (amplitude ″, frequency ″/yr, phase °); ψ = 50.439273″/yr · t + 3.392506°.
    private static readonly (double A, double F, double D)[] PrecessionTerms =
    [
        (7391.0225, 31.609974, 251.9025),
        (2555.1526, 32.620504, 280.8325),
        (2022.7629, 24.172203, 128.3057),
    ];

    /// <summary>The 1950 values the truncated series is nudged to.</summary>
    public static readonly OrbitalElements Today1950 = new(0.016726, 23.446, 102.04, 0);

    private static readonly (double Obliquity, double Sin, double Cos, double Perihelion) Nudge = MakeNudge();

    /// <summary>Earth's orbital elements <paramref name="kyr"/> thousand years from 1950 (negative: in the past).</summary>
    public static OrbitalElements Elements(double kyr)
    {
        var raw = Raw(kyr * 1000);
        var sin = raw.Sin + Nudge.Sin;
        var cos = raw.Cos + Nudge.Cos;
        var e = Math.Sqrt((sin * sin) + (cos * cos));
        var pi = Deg(Math.Atan2(sin, cos));
        return new OrbitalElements(e, raw.Obliquity + Nudge.Obliquity, Wrap(pi + raw.Psi + Nudge.Perihelion), raw.Psi - Raw(0).Psi);
    }

    /// <summary>
    /// The elements with only <paramref name="cycle"/> changing: the other two are held at today's values (the eccentricity
    /// and the perihelion together make the wobble's effect, so "Precession" moves the perihelion and keeps today's e).
    /// </summary>
    public static OrbitalElements Elements(double kyr, IceAgeCycle cycle)
    {
        var at = Elements(kyr);
        if (cycle == IceAgeCycle.All) return at;
        var now = Elements(0);
        return cycle switch
        {
            IceAgeCycle.Eccentricity => now with { Eccentricity = at.Eccentricity },
            IceAgeCycle.Tilt => now with { Obliquity = at.Obliquity },
            _ => now with { Perihelion = at.Perihelion, Precession = at.Precession },
        };
    }

    private static (double Obliquity, double Sin, double Cos, double Psi) Raw(double years)
    {
        var obliquity = 23.320556;
        foreach (var (a, f, d) in ObliquityTerms) obliquity += a * Math.Cos(Rad((f * years / 3600) + d)) / 3600;

        double sin = 0, cos = 0;
        foreach (var (p, f, d) in EccentricityTerms)
        {
            var angle = Rad((f * years / 3600) + d);
            sin += p * Math.Sin(angle);
            cos += p * Math.Cos(angle);
        }

        var psi = (50.439273 * years / 3600) + 3.392506;
        foreach (var (a, f, d) in PrecessionTerms) psi += a * Math.Sin(Rad((f * years / 3600) + d)) / 3600;
        return (obliquity, sin, cos, psi);
    }

    private static (double, double, double, double) MakeNudge()
    {
        var raw = Raw(0);
        var target = Today1950;

        // Π today from the published ϖ and the series' ψ; e·sin/cos Π from it and the published e.
        var pi = Rad(target.Perihelion - raw.Psi);
        var sin = (target.Eccentricity * Math.Sin(pi)) - raw.Sin;
        var cos = (target.Eccentricity * Math.Cos(pi)) - raw.Cos;
        return (target.Obliquity - raw.Obliquity, sin, cos, 0);
    }

    #region the Sun's path and sunshine

    /// <summary>The true anomaly (degrees from perihelion) where the Sun stands at solar longitude <paramref name="longitude"/>.</summary>
    public static double TrueAnomaly(OrbitalElements orbit, double longitude) => Wrap(longitude - orbit.PerihelionLongitude);

    /// <summary>Earth's distance from the Sun at true anomaly <paramref name="anomaly"/>, in units of the semi-major axis.</summary>
    public static double Distance(double eccentricity, double anomaly) =>
        (1 - (eccentricity * eccentricity)) / (1 + (eccentricity * Math.Cos(Rad(anomaly))));

    /// <summary>The Sun's declination (degrees) at solar longitude <paramref name="longitude"/>.</summary>
    public static double Declination(OrbitalElements orbit, double longitude) =>
        Deg(Math.Asin(Math.Sin(Rad(orbit.Obliquity)) * Math.Sin(Rad(longitude))));

    /// <summary>
    /// The day's mean sunshine (W/m²) on level ground at the top of the air, at <paramref name="latitude"/> on the day the
    /// Sun stands at solar longitude <paramref name="longitude"/> (90 = the northern summer solstice).
    /// </summary>
    public static double DailyInsolation(OrbitalElements orbit, double latitude, double longitude = 90)
    {
        var declination = Rad(Declination(orbit, longitude));
        var phi = Rad(latitude);
        var x = -Math.Tan(phi) * Math.Tan(declination);
        var hourAngle = x <= -1 ? Math.PI : x >= 1 ? 0 : Math.Acos(x);
        var r = Distance(orbit.Eccentricity, TrueAnomaly(orbit, longitude));
        return SolarConstant / Math.PI / (r * r)
            * ((hourAngle * Math.Sin(phi) * Math.Sin(declination)) + (Math.Cos(phi) * Math.Cos(declination) * Math.Sin(hourAngle)));
    }

    /// <summary>Midsummer (northern summer solstice) daily sunshine at 65° N, <paramref name="kyr"/> thousand years from 1950.</summary>
    public static double SummerSunshine(double kyr, IceAgeCycle cycle = IceAgeCycle.All) => DailyInsolation(Elements(kyr, cycle), KeyLatitude);

    /// <summary>The time since the spring equinox (days) for Earth to reach solar longitude <paramref name="longitude"/> (Kepler's equation).</summary>
    public static double DaysAfterEquinox(OrbitalElements orbit, double longitude)
    {
        var from = MeanAnomaly(orbit.Eccentricity, TrueAnomaly(orbit, 0));
        var to = MeanAnomaly(orbit.Eccentricity, TrueAnomaly(orbit, longitude));
        return Wrap(Deg(to - from)) / 360 * YearDays;
    }

    /// <summary>The day of the year (0 = 1 January) Earth passes perihelion, with the spring equinox kept on 20 March.</summary>
    public static double PerihelionDay(OrbitalElements orbit) =>
        (EquinoxDay + DaysAfterEquinox(orbit, orbit.PerihelionLongitude)) % 365.25;

    /// <summary>The length (days) of the northern summer half-year, spring equinox to autumn equinox.</summary>
    public static double NorthernSummerDays(OrbitalElements orbit) => DaysAfterEquinox(orbit, 180);

    /// <summary>The mean anomaly (radians, 0 to 2π) for a true anomaly in degrees.</summary>
    private static double MeanAnomaly(double e, double trueAnomaly)
    {
        var v = Rad(trueAnomaly);
        var eccentric = 2 * Math.Atan2(Math.Sqrt(1 - e) * Math.Sin(v / 2), Math.Sqrt(1 + e) * Math.Cos(v / 2));
        var mean = eccentric - (e * Math.Sin(eccentric));
        return mean < 0 ? mean + (2 * Math.PI) : mean;
    }

    /// <summary>The true anomaly (degrees) a fraction <paramref name="yearFraction"/> of a year after perihelion (Kepler's equation by Newton).</summary>
    public static double AnomalyAfterPerihelion(double e, double yearFraction)
    {
        var mean = 2 * Math.PI * (yearFraction - Math.Floor(yearFraction));
        var eccentric = mean;
        for (var i = 0; i < 8; i++) eccentric -= (eccentric - (e * Math.Sin(eccentric)) - mean) / (1 - (e * Math.Cos(eccentric)));
        return Wrap(Deg(2 * Math.Atan2(Math.Sqrt(1 + e) * Math.Sin(eccentric / 2), Math.Sqrt(1 - e) * Math.Cos(eccentric / 2))));
    }

    #endregion

    #region the pole star

    /// <summary>The north celestial pole in J2000 ecliptic coordinates (longitude, latitude; degrees) at <paramref name="kyr"/> from 1950.</summary>
    public static (double Longitude, double Latitude) Pole(double kyr)
    {
        var orbit = Elements(kyr);
        return (Wrap(90 - orbit.Precession), 90 - orbit.Obliquity);
    }

    /// <summary>A star's J2000 ecliptic coordinates (degrees) from its right ascension (hours) and declination (degrees).</summary>
    public static (double Longitude, double Latitude) Ecliptic(double raHours, double decDegrees)
    {
        var e = Rad(23.4393);
        var ra = Rad(raHours * 15);
        var dec = Rad(decDegrees);
        var sinBeta = (Math.Sin(dec) * Math.Cos(e)) - (Math.Cos(dec) * Math.Sin(e) * Math.Sin(ra));
        var lambda = Math.Atan2((Math.Sin(ra) * Math.Cos(e)) + (Math.Tan(dec) * Math.Sin(e)), Math.Cos(ra));
        return (Wrap(Deg(lambda)), Deg(Math.Asin(sinBeta)));
    }

    /// <summary>The angle (degrees) between two points given by ecliptic longitude and latitude.</summary>
    public static double Separation((double Longitude, double Latitude) a, (double Longitude, double Latitude) b)
    {
        var cos = (Math.Sin(Rad(a.Latitude)) * Math.Sin(Rad(b.Latitude)))
            + (Math.Cos(Rad(a.Latitude)) * Math.Cos(Rad(b.Latitude)) * Math.Cos(Rad(a.Longitude - b.Longitude)));
        return Deg(Math.Acos(Math.Clamp(cos, -1, 1)));
    }

    /// <summary>Bright stars near the pole's circle: name key, right ascension (h), declination (°), J2000.</summary>
    public static readonly (string Id, double Ra, double Dec)[] PoleStars =
    [
        ("polaris", 2.530, 89.264),
        ("vega", 18.616, 38.784),
        ("thuban", 14.073, 64.376),
        ("deneb", 20.690, 45.280),
        ("kochab", 14.845, 74.156),
        ("errai", 23.656, 77.632),
        ("alderamin", 21.310, 62.586),
    ];

    #endregion

    internal static double Rad(double degrees) => degrees * Math.PI / 180;

    internal static double Deg(double radians) => radians * 180 / Math.PI;

    internal static double Wrap(double degrees)
    {
        var d = degrees % 360;
        return d < 0 ? d + 360 : d;
    }
}
