using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.DayNight;

/// <summary>What the day-and-night page shows.</summary>
public enum DayNightMode
{
    /// <summary>The world map with the lit half, twilight and the line between day and night.</summary>
    Map,

    /// <summary>The same map divided into time zones, with the clock and the date in each.</summary>
    TimeZones,

    /// <summary>Earth as a globe lit from one side: the line between day and night is a great circle.</summary>
    Globe,
}

/// <summary>Where the Sun is in the sky at one place, from day through twilight to night.</summary>
public enum DayPhase
{
    Day,

    /// <summary>The Sun low in the east: from sunrise until it is 6° up.</summary>
    Sunrise,

    /// <summary>The Sun low in the west: from 6° up until sunset.</summary>
    Sunset,

    /// <summary>Civil twilight before sunrise (Sun 0.8°–6° below the horizon).</summary>
    Dawn,

    /// <summary>Civil twilight after sunset.</summary>
    Dusk,

    /// <summary>Sun 6°–12° below the horizon.</summary>
    NauticalTwilight,

    /// <summary>Sun 12°–18° below the horizon.</summary>
    AstronomicalTwilight,

    Night,
}

/// <summary>A point on Earth in degrees, north and east positive.</summary>
public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>
/// Where the Sun is at one instant: its declination (the latitude where it is overhead), the longitude where it is
/// overhead, and the equation of time (how far a sundial runs ahead of the clock, in minutes).
/// </summary>
public readonly record struct SunPosition(double Declination, double SubsolarLongitude, double EquationOfTime, double EclipticLongitude)
{
    public GeoPoint Subsolar => new(Declination, SubsolarLongitude);
}

/// <summary>Sunrise and sunset in one window of time (usually a local calendar day), in UTC.</summary>
public readonly record struct SunEvents(DateTime? Rise, DateTime? Set, TimeSpan Daylight, bool AlwaysUp, bool AlwaysDown);

/// <summary>
/// The Sun's position over a real, spinning Earth at a real instant, and what it means at one place: its altitude,
/// sunrise and sunset, local solar time, and the circle between day and night.
/// </summary>
/// <remarks>
/// The Sun's position uses the low-precision formulas of the Astronomical Almanac (good to about 0.01°, a minute or
/// so in time) with UT taken for TT. Sunrise and sunset are for the top of the Sun's disc with standard refraction
/// (−0.833°), as published tables give them. Angles are in degrees unless the name says otherwise.
/// </remarks>
public static class DayNightGeometry
{
    /// <summary>The Sun's centre at sunrise and sunset: its radius and the bending of light near the horizon.</summary>
    public const double SunriseAltitude = -0.833;

    public const double CivilTwilight = -6;
    public const double NauticalTwilight = -12;
    public const double AstronomicalTwilight = -18;

    private static readonly DateTime J2000 = new(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    public static SunPosition Sun(DateTime utc)
    {
        var n = (utc - J2000).TotalDays;
        var meanLongitude = Normalize360(280.460 + (0.9856474 * n));
        var g = ToRadians(357.528 + (0.9856003 * n));
        var lambda = ToRadians(meanLongitude + (1.915 * Math.Sin(g)) + (0.020 * Math.Sin(2 * g)));
        var epsilon = ToRadians(23.439 - (0.0000004 * n));

        var rightAscension = Normalize360(ToDegrees(Math.Atan2(Math.Cos(epsilon) * Math.Sin(lambda), Math.Cos(lambda))));
        var declination = ToDegrees(Math.Asin(Math.Sin(epsilon) * Math.Sin(lambda)));

        // Greenwich sidereal time: how far Earth has turned. The Sun is overhead where the local sidereal time equals
        // its right ascension.
        var siderealTime = Normalize360(280.46061837 + (360.98564736629 * n));
        var subsolarLongitude = Normalize180(rightAscension - siderealTime);
        var equationOfTime = Normalize180(meanLongitude - rightAscension) * 4;
        return new SunPosition(declination, subsolarLongitude, equationOfTime, Normalize360(ToDegrees(lambda)));
    }

    /// <summary>
    /// Hour angle of the Sun at <paramref name="longitude"/>, −180…180: negative in the morning, 0 at local solar noon.
    /// </summary>
    public static double HourAngle(SunPosition sun, double longitude) => Normalize180(longitude - sun.SubsolarLongitude);

    /// <summary>The Sun's altitude above the horizon (geometric, without refraction).</summary>
    public static double Altitude(SunPosition sun, double latitude, double longitude)
    {
        var (sinLat, cosLat) = Math.SinCos(ToRadians(latitude));
        var (sinDec, cosDec) = Math.SinCos(ToRadians(sun.Declination));
        var cosH = Math.Cos(ToRadians(longitude - sun.SubsolarLongitude));
        return ToDegrees(Math.Asin(Math.Clamp((sinLat * sinDec) + (cosLat * cosDec * cosH), -1, 1)));
    }

    public static double Altitude(DateTime utc, double latitude, double longitude) => Altitude(Sun(utc), latitude, longitude);

    /// <summary>Local apparent solar time in hours, 0…24: what a sundial at <paramref name="longitude"/> shows.</summary>
    public static double SolarTime(SunPosition sun, double longitude) => Wrap24(12 + (HourAngle(sun, longitude) / 15));

    /// <summary>
    /// The circle on Earth where the Sun stands at <paramref name="altitude"/>: at 0° the line between day and night,
    /// a great circle 90° from the point under the Sun; at −6°, −12° and −18° the ends of the three twilights.
    /// </summary>
    public static GeoPoint[] Circle(SunPosition sun, double altitude, int count)
    {
        var radius = ToRadians(90 - altitude);
        var (sinDec, cosDec) = Math.SinCos(ToRadians(sun.Declination));
        var (sinLon, cosLon) = Math.SinCos(ToRadians(sun.SubsolarLongitude));

        // The subsolar point and two directions at right angles to it: east along the equator, and north.
        var s = (X: cosDec * cosLon, Y: cosDec * sinLon, Z: sinDec);
        var east = (X: -sinLon, Y: cosLon, Z: 0.0);
        var north = (X: -sinDec * cosLon, Y: -sinDec * sinLon, Z: cosDec);
        var (sinR, cosR) = Math.SinCos(radius);

        var points = new GeoPoint[count];
        for (var i = 0; i < count; i++)
        {
            var (sinT, cosT) = Math.SinCos(2 * Math.PI * i / count);
            var x = (cosR * s.X) + (sinR * ((cosT * north.X) + (sinT * east.X)));
            var y = (cosR * s.Y) + (sinR * ((cosT * north.Y) + (sinT * east.Y)));
            var z = (cosR * s.Z) + (sinR * ((cosT * north.Z) + (sinT * east.Z)));
            points[i] = new GeoPoint(ToDegrees(Math.Asin(Math.Clamp(z, -1, 1))), ToDegrees(Math.Atan2(y, x)));
        }

        return points;
    }

    /// <summary>
    /// When the Sun crosses <paramref name="altitude"/> between <paramref name="startUtc"/> and
    /// <paramref name="endUtc"/>: the first rise, the last set and the time it is above that altitude in total.
    /// </summary>
    public static SunEvents Events(double latitude, double longitude, DateTime startUtc, DateTime endUtc, double altitude = SunriseAltitude)
    {
        const int stepMinutes = 10;
        var span = (endUtc - startUtc).TotalMinutes;
        var steps = Math.Max(1, (int)Math.Ceiling(span / stepMinutes));

        double Above(DateTime t) => Altitude(t, latitude, longitude) - altitude;

        DateTime? rise = null;
        DateTime? set = null;
        var daylight = TimeSpan.Zero;
        var previousTime = startUtc;
        var previous = Above(previousTime);
        var upSince = previous > 0 ? startUtc : (DateTime?)null;
        var everUp = previous > 0;
        var everDown = previous <= 0;

        for (var i = 1; i <= steps; i++)
        {
            var time = i == steps ? endUtc : startUtc.AddMinutes(i * stepMinutes);
            var value = Above(time);
            if ((previous > 0) != (value > 0))
            {
                var crossing = Refine(previousTime, time, previous > 0);
                if (value > 0)
                {
                    rise ??= crossing;
                    upSince = crossing;
                }
                else
                {
                    set = crossing;
                    if (upSince is { } since) daylight += crossing - since;
                    upSince = null;
                }
            }

            everUp |= value > 0;
            everDown |= value <= 0;
            previous = value;
            previousTime = time;
        }

        if (upSince is { } stillUp) daylight += endUtc - stillUp;
        return new SunEvents(rise, set, daylight, everUp && !everDown, everDown && !everUp);

        DateTime Refine(DateTime a, DateTime b, bool aboveAtA)
        {
            for (var k = 0; k < 20; k++)
            {
                var mid = a + ((b - a) / 2);
                if ((Above(mid) > 0) == aboveAtA) a = mid;
                else b = mid;
            }

            return a + ((b - a) / 2);
        }
    }

    /// <summary>Sunrise and sunset on one local calendar day of <paramref name="zone"/>.</summary>
    public static SunEvents EventsOn(DateOnly localDate, TimeZoneInfo zone, double latitude, double longitude, double altitude = SunriseAltitude)
    {
        var start = LocalMidnightUtc(localDate, zone);
        var end = LocalMidnightUtc(localDate.AddDays(1), zone);
        return Events(latitude, longitude, start, end, altitude);
    }

    /// <summary>The UTC instant of local midnight, tolerant of a daylight-saving jump at midnight.</summary>
    public static DateTime LocalMidnightUtc(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var offset = zone.GetUtcOffset(local.AddHours(12));
        return DateTime.SpecifyKind(local - offset, DateTimeKind.Utc);
    }

    /// <summary>The phase of the day for a Sun at <paramref name="altitude"/> and <paramref name="hourAngle"/>.</summary>
    public static DayPhase PhaseOf(double altitude, double hourAngle)
    {
        var morning = Normalize180(hourAngle) < 0;
        return altitude switch
        {
            >= 6 => DayPhase.Day,
            >= SunriseAltitude => morning ? DayPhase.Sunrise : DayPhase.Sunset,
            >= CivilTwilight => morning ? DayPhase.Dawn : DayPhase.Dusk,
            >= NauticalTwilight => DayPhase.NauticalTwilight,
            >= AstronomicalTwilight => DayPhase.AstronomicalTwilight,
            _ => DayPhase.Night,
        };
    }

    /// <summary>The instant of an equinox or solstice in <paramref name="year"/> (to within a few minutes).</summary>
    public static DateTime EventTime(int year, SeasonEvent e)
    {
        var target = 90.0 * (int)e;

        // A first guess from the calendar, then Newton steps on the Sun's ecliptic longitude (about 0.9856° a day).
        var month = e switch
        {
            SeasonEvent.MarchEquinox => 3,
            SeasonEvent.JuneSolstice => 6,
            SeasonEvent.SeptemberEquinox => 9,
            _ => 12,
        };
        var t = new DateTime(year, month, 21, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 6; i++)
        {
            var error = Normalize180(Sun(t).EclipticLongitude - target);
            t = t.AddDays(-error / 0.9856474);
        }

        return t;
    }

    /// <summary>The ideal time zone of a meridian: one hour for every 15° from Greenwich, −12…12.</summary>
    public static int NominalZone(double longitude) => (int)Math.Round(Normalize180(longitude) / 15, MidpointRounding.AwayFromZero) switch
    {
        > 12 => 12,
        < -12 => -12,
        var zone => zone,
    };

    public static double Wrap24(double hours) => hours - (24 * Math.Floor(hours / 24));

    public static double Normalize360(double degrees) => SeasonsGeometry.Normalize360(degrees);

    public static double Normalize180(double degrees) => SeasonsGeometry.Normalize180(degrees);

    public static double ToRadians(double degrees) => degrees * Math.PI / 180;

    public static double ToDegrees(double radians) => radians * 180 / Math.PI;
}
