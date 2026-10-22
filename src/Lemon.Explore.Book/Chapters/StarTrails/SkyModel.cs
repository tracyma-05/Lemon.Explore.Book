namespace Lemon.Explore.Book.Chapters.StarTrails;

/// <summary>The three views of the star-trails page.</summary>
public enum TrailsMode
{
    /// <summary>A camera's long exposure of the night sky from the viewer's latitude: the stars drawn out into arcs.</summary>
    Camera,

    /// <summary>The celestial sphere round the observer: the stars' daily circles, tilted by the latitude.</summary>
    Sphere,

    /// <summary>A year: why the evening sky changes with the seasons, and the sidereal day.</summary>
    Year,
}

/// <summary>Which way a star goes through a day at a latitude.</summary>
public enum StarPath
{
    /// <summary>Always above the horizon: it circles the pole and never sets.</summary>
    Circumpolar,

    /// <summary>Rises and sets.</summary>
    RisesAndSets,

    /// <summary>Always below the horizon: never seen from here.</summary>
    NeverRises,
}

/// <summary>A direction in the observer's sky: altitude above the horizon and azimuth from north through east, degrees.</summary>
public readonly record struct Horizontal(double Altitude, double Azimuth);

/// <summary>A point of a projected picture (X right, Y up), or none when the point is behind the camera.</summary>
public readonly record struct Projected(double X, double Y, bool Visible);

/// <summary>
/// The sky's geometry: sidereal time, where a star stands at a moment, which stars never set, how long a star is up,
/// and the stereographic camera that keeps the circles of the stars' paths circles in the picture. Times are local mean
/// solar time; days are days of the year (1 = 1 January).
/// </summary>
public static class SkyModel
{
    /// <summary>A sidereal day in solar hours: the stars come back this much sooner each day.</summary>
    public const double SiderealDay = 23.9344696;

    /// <summary>Sidereal hours per solar hour.</summary>
    public const double SiderealRate = 24 / SiderealDay;

    /// <summary>Degrees the sky turns in a solar hour.</summary>
    public const double DegreesPerHour = 15 * SiderealRate;

    /// <summary>The tilt of Earth's axis to its orbit, degrees.</summary>
    public const double Obliquity = 23.44;

    public static double Wrap24(double hours) => ((hours % 24) + 24) % 24;

    public static double Wrap360(double degrees) => ((degrees % 360) + 360) % 360;

    /// <summary>
    /// The local sidereal time, hours, at <paramref name="hour"/> local mean time on day <paramref name="day"/>: about
    /// 6.7 h at midnight on 1 January, gaining 3 min 56 s a day.
    /// </summary>
    public static double SiderealTime(double day, double hour) => Wrap24(6.70 + (0.0657098 * (day - 1)) + (SiderealRate * hour));

    /// <summary>Where a star at <paramref name="ra"/> h, <paramref name="dec"/>° stands at sidereal time <paramref name="lst"/> h from latitude <paramref name="latitude"/>°.</summary>
    public static Horizontal ToHorizontal(double ra, double dec, double lst, double latitude)
    {
        var h = (lst - ra) * 15 * Math.PI / 180;
        var d = dec * Math.PI / 180;
        var phi = latitude * Math.PI / 180;
        var sinAlt = (Math.Sin(d) * Math.Sin(phi)) + (Math.Cos(d) * Math.Cos(phi) * Math.Cos(h));
        var alt = Math.Asin(Math.Clamp(sinAlt, -1, 1));
        var y = -Math.Cos(d) * Math.Sin(h);
        var x = (Math.Sin(d) * Math.Cos(phi)) - (Math.Cos(d) * Math.Sin(phi) * Math.Cos(h));
        var az = Math.Atan2(y, x);
        return new Horizontal(alt * 180 / Math.PI, Wrap360(az * 180 / Math.PI));
    }

    /// <summary>A direction as a unit vector: x east, y north, z up.</summary>
    public static (double X, double Y, double Z) Vector(Horizontal p)
    {
        var alt = p.Altitude * Math.PI / 180;
        var az = p.Azimuth * Math.PI / 180;
        return (Math.Cos(alt) * Math.Sin(az), Math.Cos(alt) * Math.Cos(az), Math.Sin(alt));
    }

    /// <summary>The highest a star gets (crossing the meridian), degrees.</summary>
    public static double HighestAltitude(double dec, double latitude) => 90 - Math.Abs(latitude - dec);

    /// <summary>The lowest a star gets (crossing the meridian under the pole), degrees.</summary>
    public static double LowestAltitude(double dec, double latitude) => Math.Abs(latitude + dec) - 90;

    public static StarPath PathOf(double dec, double latitude) =>
        LowestAltitude(dec, latitude) > 0 ? StarPath.Circumpolar
        : HighestAltitude(dec, latitude) < 0 ? StarPath.NeverRises
        : StarPath.RisesAndSets;

    /// <summary>How many hours of a sidereal day the star spends above the horizon (no refraction).</summary>
    public static double HoursAbove(double dec, double latitude)
    {
        var product = -Math.Tan(latitude * Math.PI / 180) * Math.Tan(dec * Math.PI / 180);
        if (double.IsNaN(product)) return 12;
        if (product <= -1) return SiderealDay;
        if (product >= 1) return 0;
        return 2 * Math.Acos(product) * 180 / Math.PI / 15 / SiderealRate;
    }

    /// <summary>The share of the whole sky that can ever be seen from <paramref name="latitude"/>: half at a pole, all of it at the equator.</summary>
    public static double VisibleShare(double latitude) => (1 + Math.Cos(latitude * Math.PI / 180)) / 2;

    /// <summary>The Sun's right ascension, hours, on day <paramref name="day"/> (a circular orbit, good to a few minutes).</summary>
    public static double SunRa(double day)
    {
        var longitude = (280.46 + (0.9856474 * (day - 1))) * Math.PI / 180;
        var eps = Obliquity * Math.PI / 180;
        return Wrap24(Math.Atan2(Math.Cos(eps) * Math.Sin(longitude), Math.Cos(longitude)) * 180 / Math.PI / 15);
    }

    /// <summary>The Sun's declination, degrees, on day <paramref name="day"/>.</summary>
    public static double SunDec(double day)
    {
        var longitude = (280.46 + (0.9856474 * (day - 1))) * Math.PI / 180;
        return Math.Asin(Math.Sin(Obliquity * Math.PI / 180) * Math.Sin(longitude)) * 180 / Math.PI;
    }

    /// <summary>
    /// The local time a star crosses the meridian (stands highest) on day <paramref name="day"/>, hours 0…24; it comes 3 min
    /// 56 s earlier every day.
    /// </summary>
    public static double TransitTime(double ra, double day) => Wrap24((ra - SiderealTime(day, 0)) / SiderealRate);

    /// <summary>
    /// A stereographic camera looking towards <paramref name="center"/>: a point straight ahead goes to (0, 0), one at an
    /// angle θ from it to radius 2·tan(θ/2). Circles on the sky (the stars' paths) stay circles in the picture.
    /// </summary>
    public static Projected Stereographic(Horizontal point, Horizontal center)
    {
        var (px, py, pz) = Vector(point);
        var (fx, fy, fz) = Vector(center);

        // Right: horizontal and to the right of the view; up: completes the frame.
        var az = center.Azimuth * Math.PI / 180;
        var (rx, ry, rz) = (Math.Cos(az), -Math.Sin(az), 0.0);
        if (center.Altitude > 89.9)
        {
            // Straight up: north at the top and east on the left, as on an all-sky chart held overhead.
            (rx, ry, rz) = (-1, 0, 0);
        }

        var (ux, uy, uz) = ((ry * fz) - (rz * fy), (rz * fx) - (rx * fz), (rx * fy) - (ry * fx));
        var x = (px * rx) + (py * ry) + (pz * rz);
        var y = (px * ux) + (py * uy) + (pz * uz);
        var z = (px * fx) + (py * fy) + (pz * fz);
        if (z < -0.9) return new Projected(0, 0, false);
        var k = 2 / (1 + z);
        return new Projected(k * x, k * y, true);
    }

    /// <summary>The picture's half-width in stereographic units for a horizontal field of view of <paramref name="fov"/>°.</summary>
    public static double HalfWidth(double fov) => 2 * Math.Tan(fov / 4 * Math.PI / 180);

    /// <summary>The altitude of the celestial pole above the horizon (the north pole north of the equator), degrees.</summary>
    public static double PoleAltitude(double latitude) => Math.Abs(latitude);
}
