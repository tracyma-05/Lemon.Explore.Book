namespace Lemon.Explore.Book.Chapters.Seasons;

/// <summary>What the seasons page shows.</summary>
public enum SeasonsMode
{
    /// <summary>Earth's year round the Sun with its tilted axis.</summary>
    Tilt,

    /// <summary>The Sun's path across the sky at one place.</summary>
    SunPath,

    /// <summary>The myth that seasons come from the distance to the Sun.</summary>
    Distance,
}

public enum Season
{
    Spring,
    Summer,
    Autumn,
    Winter,
}

/// <summary>The four points of Earth's year where the seasons turn.</summary>
public enum SeasonEvent
{
    MarchEquinox,
    JuneSolstice,
    SeptemberEquinox,
    DecemberSolstice,
}

/// <summary>
/// The Sun's position through the year and at one place on Earth, for an Earth with any axial tilt.
/// </summary>
/// <remarks>
/// A day is counted from 1 January 00:00 (UT) of a typical year (2026), so dates line up with the calendar to within a
/// day. The Sun's ecliptic longitude and distance use the low-precision formulas of the Astronomical Almanac (good to
/// about 0.01°). Altitudes are geometric: the Sun's centre, without atmospheric refraction, so days come out a few
/// minutes shorter than published sunrise and sunset times. Angles are in degrees unless the name says otherwise.
/// </remarks>
public static class SeasonsGeometry
{
    /// <summary>Earth's axial tilt (obliquity of the ecliptic) today.</summary>
    public const double EarthTilt = 23.44;

    /// <summary>Tropical year in days.</summary>
    public const double Year = 365.2422;

    public const double AstronomicalUnitKm = 149_597_870.7;

    /// <summary>Days from J2000.0 (2000-01-01 12:00) to 2026-01-01 00:00.</summary>
    private const double YearStart = 9496.5;

    private static readonly DateTime CalendarYear = new(2026, 1, 1);

    /// <summary>The calendar date of <paramref name="day"/> (only the month and day are meaningful).</summary>
    public static DateTime DateOf(double day) => CalendarYear.AddDays(Wrap(day));

    /// <summary>Noon of today's date, as a day of the model year.</summary>
    public static double Today() => DateTime.Today.DayOfYear - 0.5;

    public static double Wrap(double day) => day - (Year * Math.Floor(day / Year));

    /// <summary>The Sun's mean anomaly: 0 at perihelion, when Earth is closest to the Sun.</summary>
    public static double MeanAnomaly(double day) => Normalize360(357.529 + (0.98560028 * (day + YearStart)));

    /// <summary>The Sun's ecliptic longitude seen from Earth, 0…360: 0 at the March equinox, 90 at the June solstice.</summary>
    public static double SunLongitude(double day)
    {
        var g = ToRadians(MeanAnomaly(day));
        var meanLongitude = 280.459 + (0.98564736 * (day + YearStart));
        return Normalize360(meanLongitude + (1.915 * Math.Sin(g)) + (0.020 * Math.Sin(2 * g)));
    }

    /// <summary>Distance from Earth to the Sun in astronomical units.</summary>
    public static double SunDistance(double day)
    {
        var g = ToRadians(MeanAnomaly(day));
        return 1.00014 - (0.01671 * Math.Cos(g)) - (0.00014 * Math.Cos(2 * g));
    }

    /// <summary>The day (within the model year) the Sun reaches ecliptic longitude <paramref name="longitude"/>.</summary>
    public static double DayOfLongitude(double longitude)
    {
        // The Sun moves about 360° a year; a few Newton steps take out the orbit's eccentricity.
        var day = Wrap((Normalize360(longitude) - SunLongitude(0)) / 360 * Year);
        for (var i = 0; i < 4; i++)
        {
            day -= Normalize180(SunLongitude(day) - longitude) / 360 * Year;
        }

        return Wrap(day);
    }

    public static double EventLongitude(SeasonEvent e) => 90 * (int)e;

    public static double EventDay(SeasonEvent e) => DayOfLongitude(EventLongitude(e));

    /// <summary>The day Earth passes perihelion, closest to the Sun (early January).</summary>
    public static double PerihelionDay() => Wrap((360 - MeanAnomaly(0)) / 0.98560028);

    /// <summary>The day Earth passes aphelion, farthest from the Sun (early July).</summary>
    public static double AphelionDay() => Wrap(PerihelionDay() + (Year / 2));

    /// <summary>The Sun's declination: the latitude where it stands overhead at noon.</summary>
    public static double Declination(double day, double tilt) =>
        ToDegrees(Math.Asin(Math.Sin(ToRadians(tilt)) * Math.Sin(ToRadians(SunLongitude(day)))));

    /// <summary>The Sun's altitude at local noon (negative when it stays below the horizon all day).</summary>
    public static double NoonAltitude(double latitude, double declination) => 90 - Math.Abs(latitude - declination);

    /// <summary>The half-day arc: the hour angle of sunset in degrees, 0 in polar night and 180 in polar day.</summary>
    public static double SunsetHourAngle(double latitude, double declination)
    {
        var (sinLat, cosLat) = Math.SinCos(ToRadians(latitude));
        var (sinDec, cosDec) = Math.SinCos(ToRadians(declination));
        var numerator = sinLat * sinDec;
        var denominator = cosLat * cosDec;

        // At a pole (or with the Sun at a pole) the Sun is either up all day or down all day.
        if (Math.Abs(denominator) < 1e-9)
        {
            return numerator > 1e-9 ? 180 : numerator < -1e-9 ? 0 : 90;
        }

        return ToDegrees(Math.Acos(Math.Clamp(-numerator / denominator, -1, 1)));
    }

    /// <summary>Hours from sunrise to sunset, 0…24.</summary>
    public static double DayLength(double latitude, double declination) => SunsetHourAngle(latitude, declination) / 7.5;

    /// <summary>
    /// Sunlight a level patch of ground receives over one day (top of the atmosphere), relative to the equator at an
    /// equinox; <paramref name="distance"/> in AU dims or brightens it by the inverse square law.
    /// </summary>
    public static double DailySunlight(double latitude, double declination, double distance = 1)
    {
        var h0 = ToRadians(SunsetHourAngle(latitude, declination));
        var (sinLat, cosLat) = Math.SinCos(ToRadians(latitude));
        var (sinDec, cosDec) = Math.SinCos(ToRadians(declination));
        var sunlight = (h0 * sinLat * sinDec) + (cosLat * cosDec * Math.Sin(h0));
        return Math.Max(0, sunlight) / (distance * distance);
    }

    /// <summary>
    /// Direction to the Sun from a place at <paramref name="latitude"/>, as (east, north, up), at hour angle
    /// <paramref name="hourAngle"/> (0 at local noon, positive in the afternoon).
    /// </summary>
    public static (double East, double North, double Up) SunDirection(double latitude, double declination, double hourAngle)
    {
        var (sinLat, cosLat) = Math.SinCos(ToRadians(latitude));
        var (sinDec, cosDec) = Math.SinCos(ToRadians(declination));
        var (sinH, cosH) = Math.SinCos(ToRadians(hourAngle));
        return (-cosDec * sinH, (sinDec * cosLat) - (cosDec * cosH * sinLat), (sinDec * sinLat) + (cosDec * cosH * cosLat));
    }

    /// <summary>The Sun's altitude above the horizon at hour angle <paramref name="hourAngle"/>.</summary>
    public static double Altitude(double latitude, double declination, double hourAngle) =>
        ToDegrees(Math.Asin(Math.Clamp(SunDirection(latitude, declination, hourAngle).Up, -1, 1)));

    /// <summary>Hour angle of local solar time <paramref name="hours"/> (12 = noon).</summary>
    public static double HourAngle(double hours) => 15 * (hours - 12);

    /// <summary>The astronomical season of one hemisphere: the equinoxes and solstices start them.</summary>
    public static Season SeasonAt(double sunLongitude, bool north)
    {
        var quarter = (int)(Normalize360(sunLongitude) / 90) % 4;
        if (!north) quarter = (quarter + 2) % 4;
        return (Season)quarter;
    }

    public static double Normalize360(double degrees) => degrees - (360 * Math.Floor(degrees / 360));

    /// <summary>An angle in −180…180.</summary>
    public static double Normalize180(double degrees) => Normalize360(degrees + 180) - 180;

    public static double ToRadians(double degrees) => degrees * Math.PI / 180;

    public static double ToDegrees(double radians) => radians * 180 / Math.PI;
}
