using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.MoonPhases;

/// <summary>What the moon-phases page shows.</summary>
public enum MoonPhasesMode
{
    /// <summary>The Moon's month round Earth seen from above, always half lit, next to the phase seen from Earth.</summary>
    Phases,

    /// <summary>Where the Moon is in your sky at each time of day, and so when each phase can be seen.</summary>
    Sky,

    /// <summary>Why a month of phases is 29.5 days while the Moon goes round in 27.3.</summary>
    Month,
}

/// <summary>The eight named phases, in order through the month.</summary>
public enum MoonPhase
{
    New,
    WaxingCrescent,
    FirstQuarter,
    WaxingGibbous,
    Full,
    WaningGibbous,
    LastQuarter,
    WaningCrescent,
}

/// <summary>
/// The Moon's phase over a month, where it stands in the sky at each time of day, and the real dates of the coming
/// new moons, quarters and full moons.
/// </summary>
/// <remarks>
/// The animated model uses the mean month: the age (days since new moon) turns the Moon's elongation (its angle from
/// the Sun) evenly through 360° in a synodic month. For the sky, the Sun and the Moon both move along the celestial
/// equator, as at an equinox, so rise and set times come out as round numbers (the full moon rises at 18:00). The real
/// dates use the series of Meeus, <i>Astronomical Algorithms</i> ch. 49, with its main terms: good to a few minutes.
/// Angles are in degrees unless the name says otherwise.
/// </remarks>
public static class MoonPhaseGeometry
{
    /// <summary>New moon to new moon, in days.</summary>
    public const double SynodicMonth = 29.530589;

    /// <summary>Once round Earth measured against the stars, in days.</summary>
    public const double SiderealMonth = 27.321662;

    /// <summary>Earth once round the Sun measured against the stars, in days.</summary>
    public const double SiderealYear = 365.256363;

    private const double MeeusEpoch = 2451550.09766;
    private const double MeeusMonth = 29.530588861;
    private const double UnixEpochJulianDay = 2440587.5;

    public static double Wrap(double age) => age - (SynodicMonth * Math.Floor(age / SynodicMonth));

    /// <summary>The Moon's angle east of the Sun, 0…360: 0 at new moon, 90 at first quarter, 180 at full moon.</summary>
    public static double Elongation(double age) => 360 * Wrap(age) / SynodicMonth;

    /// <summary>Share of the Moon's disc that is lit, as seen from Earth, 0…1.</summary>
    public static double Illumination(double age) => (1 - Math.Cos(SeasonsGeometry.ToRadians(Elongation(age)))) / 2;

    public static bool IsWaxing(double age) => Elongation(age) < 180;

    /// <summary>
    /// The phase's name: new moon, the quarters and full moon within a day of their exact point, and the crescents
    /// and gibbous phases in between.
    /// </summary>
    public static MoonPhase PhaseOf(double age)
    {
        var elongation = Elongation(age);
        var quarter = (int)Math.Round(elongation / 90) % 4;
        if (Math.Abs(SeasonsGeometry.Normalize180(elongation - (90 * quarter))) <= 360 / SynodicMonth)
        {
            return (MoonPhase)(2 * quarter);
        }

        return (MoonPhase)((2 * (int)Math.Floor(elongation / 90)) + 1);
    }

    /// <summary>The age at the exact point of <paramref name="phase"/> (the middle of its eighth).</summary>
    public static double AgeOf(MoonPhase phase) => (int)phase * SynodicMonth / 8;

    /// <summary>The principal phases: new moon, first quarter, full moon and last quarter.</summary>
    public static bool IsPrincipal(MoonPhase phase) => (int)phase % 2 == 0;

    #region in the sky

    /// <summary>Local solar time (0…24) the Moon rises: 06:00 at new moon, 50 minutes later each day.</summary>
    public static double MoonriseHour(double age) => WrapHours(6 + (Elongation(age) / 15));

    /// <summary>Local solar time the Moon is highest, due south (north in the southern hemisphere).</summary>
    public static double TransitHour(double age) => WrapHours(12 + (Elongation(age) / 15));

    public static double MoonsetHour(double age) => WrapHours(18 + (Elongation(age) / 15));

    /// <summary>The Moon's hour angle in degrees at local solar time <paramref name="hours"/>: it lags the Sun by its elongation.</summary>
    public static double MoonHourAngle(double age, double hours) => SeasonsGeometry.HourAngle(hours) - Elongation(age);

    /// <summary>Direction to the Moon as (east, north, up) at <paramref name="latitude"/>.</summary>
    public static (double East, double North, double Up) MoonDirection(double latitude, double age, double hours) =>
        SeasonsGeometry.SunDirection(latitude, 0, MoonHourAngle(age, hours));

    /// <summary>Direction to the Sun as (east, north, up) at <paramref name="latitude"/>, on the celestial equator.</summary>
    public static (double East, double North, double Up) SunDirection(double latitude, double hours) =>
        SeasonsGeometry.SunDirection(latitude, 0, SeasonsGeometry.HourAngle(hours));

    /// <summary>Altitude in degrees of a direction given as (east, north, up).</summary>
    public static double Altitude((double East, double North, double Up) direction) =>
        SeasonsGeometry.ToDegrees(Math.Asin(Math.Clamp(direction.Up, -1, 1)));

    /// <summary>Azimuth in degrees from north through east, 0…360.</summary>
    public static double Azimuth((double East, double North, double Up) direction) =>
        SeasonsGeometry.Normalize360(SeasonsGeometry.ToDegrees(Math.Atan2(direction.East, direction.North)));

    public static double WrapHours(double hours) => hours - (24 * Math.Floor(hours / 24));

    #endregion

    #region the month against the stars

    /// <summary>How far the Moon has turned round Earth against the stars, <paramref name="age"/> days after new moon.</summary>
    public static double SiderealAngle(double age) => 360 * age / SiderealMonth;

    /// <summary>How far Earth has gone round the Sun in <paramref name="age"/> days.</summary>
    public static double EarthOrbitAngle(double age) => 360 * age / SiderealYear;

    #endregion

    #region real dates

    /// <summary>
    /// Time (UTC) of a principal phase in lunation <paramref name="lunation"/>, counted from the new moon of
    /// 6 January 2000.
    /// </summary>
    public static DateTime PhaseTime(long lunation, MoonPhase phase)
    {
        if (!IsPrincipal(phase)) throw new ArgumentOutOfRangeException(nameof(phase), phase, "Only new moon, the quarters and full moon have a time.");

        var k = lunation + ((int)phase / 8.0);
        var t = k / 1236.85;
        var jde = MeeusEpoch + (MeeusMonth * k) + (0.00015437 * t * t);

        var e = 1 - (0.002516 * t) - (0.0000074 * t * t);
        var m = Radians(2.5534 + (29.10535670 * k));
        var mp = Radians(201.5643 + (385.81693528 * k));
        var f = Radians(160.7108 + (390.67050284 * k));
        var omega = Radians(124.7746 - (1.56375588 * k));

        double correction;
        switch (phase)
        {
            case MoonPhase.New:
                correction = (-0.40720 * Math.Sin(mp)) + (0.17241 * e * Math.Sin(m)) + (0.01608 * Math.Sin(2 * mp))
                             + (0.01039 * Math.Sin(2 * f)) + (0.00739 * e * Math.Sin(mp - m)) - (0.00514 * e * Math.Sin(mp + m))
                             + (0.00208 * e * e * Math.Sin(2 * m)) - (0.00111 * Math.Sin(mp - (2 * f))) - (0.00057 * Math.Sin(mp + (2 * f)))
                             + (0.00056 * e * Math.Sin((2 * mp) + m)) - (0.00042 * Math.Sin(3 * mp)) + (0.00042 * e * Math.Sin(m + (2 * f)))
                             + (0.00038 * e * Math.Sin(m - (2 * f))) - (0.00024 * e * Math.Sin((2 * mp) - m)) - (0.00017 * Math.Sin(omega));
                break;

            case MoonPhase.Full:
                correction = (-0.40614 * Math.Sin(mp)) + (0.17302 * e * Math.Sin(m)) + (0.01614 * Math.Sin(2 * mp))
                             + (0.01043 * Math.Sin(2 * f)) + (0.00734 * e * Math.Sin(mp - m)) - (0.00515 * e * Math.Sin(mp + m))
                             + (0.00209 * e * e * Math.Sin(2 * m)) - (0.00111 * Math.Sin(mp - (2 * f))) - (0.00057 * Math.Sin(mp + (2 * f)))
                             + (0.00056 * e * Math.Sin((2 * mp) + m)) - (0.00042 * Math.Sin(3 * mp)) + (0.00042 * e * Math.Sin(m + (2 * f)))
                             + (0.00038 * e * Math.Sin(m - (2 * f))) - (0.00024 * e * Math.Sin((2 * mp) - m)) - (0.00017 * Math.Sin(omega));
                break;

            default:
                correction = (-0.62801 * Math.Sin(mp)) + (0.17172 * e * Math.Sin(m)) - (0.01183 * e * Math.Sin(mp + m))
                             + (0.00862 * Math.Sin(2 * mp)) + (0.00804 * Math.Sin(2 * f)) + (0.00454 * e * Math.Sin(mp - m))
                             + (0.00204 * e * e * Math.Sin(2 * m)) - (0.00180 * Math.Sin(mp - (2 * f))) - (0.00070 * Math.Sin(mp + (2 * f)))
                             - (0.00040 * Math.Sin(3 * mp)) - (0.00034 * e * Math.Sin((2 * mp) - m)) + (0.00032 * e * Math.Sin(m + (2 * f)))
                             + (0.00032 * e * Math.Sin(m - (2 * f))) - (0.00028 * e * e * Math.Sin(mp + (2 * m))) - (0.00017 * Math.Sin(omega));
                var w = 0.00306 - (0.00038 * e * Math.Cos(m)) + (0.00026 * Math.Cos(mp)) - (0.00002 * Math.Cos(mp - m))
                        + (0.00002 * Math.Cos(mp + m)) + (0.00002 * Math.Cos(2 * f));
                correction += phase == MoonPhase.FirstQuarter ? w : -w;
                break;
        }

        // Terrestrial time; the minute or so to UTC does not matter at this precision.
        return FromJulianDay(jde + correction);
    }

    /// <summary>The first time after <paramref name="utc"/> the Moon reaches <paramref name="phase"/>.</summary>
    public static DateTime NextPhase(DateTime utc, MoonPhase phase)
    {
        var lunation = (long)Math.Floor((ToJulianDay(utc) - MeeusEpoch) / MeeusMonth) - 1;
        while (true)
        {
            var time = PhaseTime(lunation++, phase);
            if (time > utc) return time;
        }
    }

    /// <summary>The last new moon at or before <paramref name="utc"/>.</summary>
    public static DateTime PreviousNewMoon(DateTime utc)
    {
        var lunation = (long)Math.Floor((ToJulianDay(utc) - MeeusEpoch) / MeeusMonth) + 1;
        while (true)
        {
            var time = PhaseTime(lunation--, MoonPhase.New);
            if (time <= utc) return time;
        }
    }

    /// <summary>Days since the last new moon at <paramref name="utc"/>, within one mean month.</summary>
    public static double AgeAt(DateTime utc) =>
        Math.Clamp((utc - PreviousNewMoon(utc)).TotalDays, 0, SynodicMonth - 1e-6);

    public static double ToJulianDay(DateTime utc) =>
        UnixEpochJulianDay + (DateTime.SpecifyKind(utc, DateTimeKind.Utc) - DateTime.UnixEpoch).TotalDays;

    public static DateTime FromJulianDay(double julianDay) =>
        DateTime.UnixEpoch.AddDays(julianDay - UnixEpochJulianDay);

    #endregion

    private static double Radians(double degrees) => SeasonsGeometry.ToRadians(SeasonsGeometry.Normalize360(degrees));
}
