using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Eclipses;

/// <summary>What the eclipse page demonstrates.</summary>
public enum EclipseMode
{
    Solar,
    Lunar,

    /// <summary>Why eclipses do not happen every month: the Moon's tilted orbit and the eclipse seasons.</summary>
    Seasons,
}

public enum EclipseStage
{
    None,
    SolarPartial,
    SolarTotal,
    SolarAnnular,
    LunarPenumbral,
    LunarPartial,
    LunarTotal,
}

/// <summary>How far from the centre of Earth's shadow the Moon passes (set by its tilted orbit).</summary>
public enum LunarPath
{
    Central,
    OffCentre,
    Grazing,
}

/// <summary>The Sun as an observer on Earth sees it, with the Moon in front of it.</summary>
/// <param name="SunRadius">Apparent radius of the Sun in tangent-plane units (about radians).</param>
/// <param name="MoonRadius">Apparent radius of the Moon, same units.</param>
/// <param name="MoonX">Moon centre relative to the Sun's, to the right (the Moon moves right to left).</param>
/// <param name="MoonY">Moon centre relative to the Sun's, up (north).</param>
/// <param name="MoonInFront">The Moon is on the observer's side of the sky at all.</param>
/// <param name="Coverage">Fraction of the Sun's disc the Moon covers.</param>
public sealed record SolarSky(
    double SunRadius, double MoonRadius, double MoonX, double MoonY, bool MoonInFront, double Coverage, EclipseStage Stage);

/// <summary>The Moon and Earth's shadow where the Moon is, seen from Earth's night side.</summary>
/// <param name="MoonX">Moon centre relative to the shadow's axis in diagram units, to the right (it moves right to left).</param>
/// <param name="MoonY">Moon centre relative to the shadow's axis, up (north).</param>
/// <param name="BehindEarth">The Moon is on the night side, where Earth's shadow is.</param>
/// <param name="UmbraCoverage">Fraction of the Moon's disc inside the umbra.</param>
/// <param name="Light">Share of the Sun's light reaching the Moon's centre: 1 in full sunlight, 0 in the umbra.</param>
public sealed record LunarSky(
    double MoonRadius, double MoonX, double MoonY, double UmbraRadius, double PenumbraRadius, bool BehindEarth,
    double UmbraCoverage, double Light, EclipseStage Stage);

/// <summary>Where the Moon's shadow falls on Earth's day side, seen from the Sun (diagram units, Earth's centre at 0).</summary>
/// <param name="CentreY">Shadow axis, sideways in the orbital plane (seen from the Sun, +Y is to the left).</param>
/// <param name="CentreZ">Shadow axis, north of the orbital plane.</param>
/// <param name="Umbra">Umbra radius at Earth; negative when the umbra ends short of Earth (the antumbra, radius |Umbra|).</param>
/// <param name="Penumbra">Penumbra radius at Earth.</param>
/// <param name="Reaches">The shadow is cast towards Earth's day side at all.</param>
public sealed record ShadowOnEarth(double CentreY, double CentreZ, double Umbra, double Penumbra, bool Reaches);

/// <summary>The shadow cone of a sphere lit by the Sun.</summary>
/// <param name="Radius">Radius of the sphere casting the shadow.</param>
/// <param name="SunDistance">Distance between its centre and the Sun's.</param>
public readonly record struct ShadowCone(double Radius, double SunDistance)
{
    /// <summary>Umbra radius at distance <paramref name="x"/> behind the sphere; negative past the tip (the antumbra).</summary>
    public double Umbra(double x) => Radius - (x * (EclipseGeometry.SunRadius - Radius) / SunDistance);

    public double Penumbra(double x) => Radius + (x * (EclipseGeometry.SunRadius + Radius) / SunDistance);

    /// <summary>Distance from the sphere to the tip of the umbra.</summary>
    public double UmbraLength => SunDistance * Radius / (EclipseGeometry.SunRadius - Radius);
}

/// <summary>
/// The Sun, Earth and Moon of the solar and lunar eclipse diagrams: the orbital plane seen from the north, Earth at the
/// origin, the Sun on the −X axis and Z pointing north out of the plane.
/// </summary>
/// <remarks>
/// Not to scale (the real Moon is 110 Earth radii from Earth's 60 and the Sun 23 000 away), but chosen so the shadows
/// behave like the real ones: from the near orbit the Moon's umbra just reaches Earth's surface (a narrow path of
/// totality), from the far orbit it ends short of it (annular), the Sun and the Moon look almost the same size from
/// Earth, and the Moon fits inside Earth's umbra about twice over.
/// </remarks>
public static class EclipseGeometry
{
    public const double SunDistance = 750;
    public const double SunRadius = 90;
    public const double EarthRadius = 50;

    /// <summary>The Moon's orbit near perigee: its umbra reaches Earth, so the eclipse is total.</summary>
    public const double MoonOrbitNear = 190;

    /// <summary>The Moon's orbit near apogee: its umbra ends short of Earth, so the eclipse is annular.</summary>
    public const double MoonOrbitFar = 222;

    /// <summary>How deep the Moon's umbra reaches into Earth from the near orbit at new moon.</summary>
    private const double UmbraOvershoot = 14;

    /// <summary>The Moon's radius, sized so its umbra from the near orbit reaches <see cref="UmbraOvershoot"/> into Earth.</summary>
    public const double MoonRadius =
        SunRadius * (MoonOrbitNear - EarthRadius + UmbraOvershoot) / (SunDistance - EarthRadius + UmbraOvershoot);

    /// <summary>Farthest an observer is placed north or south of the orbital plane, in Earth radii.</summary>
    public const double MaxObserverOffset = 0.85;

    internal static readonly V3 Sun = new(-SunDistance, 0, 0);

    public static ShadowCone EarthShadow => new(EarthRadius, SunDistance);

    public static double MoonOrbitRadius(bool near) => near ? MoonOrbitNear : MoonOrbitFar;

    /// <summary>The Moon's height above the orbital plane at full moon for each path through Earth's shadow.</summary>
    public static double LunarPathHeight(LunarPath path) => path switch
    {
        LunarPath.OffCentre => 38,
        LunarPath.Grazing => 64,
        _ => 0,
    };

    /// <summary>
    /// The Moon's centre at <paramref name="angle"/> radians along its orbit (0 = new moon, between the Sun and Earth;
    /// π = full moon), going round counter-clockwise seen from the north.
    /// </summary>
    internal static V3 MoonPosition(double angle, double orbitRadius, double height) =>
        new(-orbitRadius * Math.Cos(angle), -orbitRadius * Math.Sin(angle), height);

    /// <summary>
    /// An observer on the meridian facing the Sun (where it is noon), <paramref name="offset"/> (−1…1) of the way to
    /// <see cref="MaxObserverOffset"/> north (+) or south (−) of the orbital plane.
    /// </summary>
    internal static V3 Observer(double offset)
    {
        var height = Math.Clamp(offset, -1, 1) * MaxObserverOffset * EarthRadius;
        return new V3(-Math.Sqrt((EarthRadius * EarthRadius) - (height * height)), 0, height);
    }

    /// <summary>What the observer sees of the Sun and the Moon with the Moon at <paramref name="angle"/> radians.</summary>
    public static SolarSky Solar(double angle, bool moonNear, double observerOffset)
    {
        var observer = Observer(observerOffset);
        var moon = MoonPosition(angle, MoonOrbitRadius(moonNear), 0);
        var toSun = Sun - observer;
        var toMoon = moon - observer;

        // Sky axes for an observer facing the Sun with north up; the Moon then moves from right to left.
        var forward = toSun.Normalized();
        var north = new V3(0, 0, 1);
        var up = (north - (forward * V3.Dot(north, forward))).Normalized();
        var right = V3.Cross(forward, up);

        var sunRadius = ApparentRadius(SunRadius, toSun.Length);
        var depth = V3.Dot(toMoon, forward);
        if (depth <= MoonRadius)
        {
            return new SolarSky(sunRadius, 0, 0, 0, false, 0, EclipseStage.None);
        }

        var moonRadius = ApparentRadius(MoonRadius, toMoon.Length);
        var x = V3.Dot(toMoon, right) / depth;
        var y = V3.Dot(toMoon, up) / depth;
        var separation = Math.Sqrt((x * x) + (y * y));
        var coverage = OverlapArea(sunRadius, moonRadius, separation) / (Math.PI * sunRadius * sunRadius);

        EclipseStage stage;
        if (separation >= sunRadius + moonRadius)
        {
            stage = EclipseStage.None;
        }
        else if (moonRadius >= sunRadius)
        {
            stage = separation <= moonRadius - sunRadius ? EclipseStage.SolarTotal : EclipseStage.SolarPartial;
        }
        else
        {
            stage = separation <= sunRadius - moonRadius ? EclipseStage.SolarAnnular : EclipseStage.SolarPartial;
        }

        return new SolarSky(sunRadius, moonRadius, x, y, true, Math.Clamp(coverage, 0, 1), stage);
    }

    /// <summary>The Moon in Earth's shadow, <paramref name="height"/> above the orbital plane, at <paramref name="angle"/> radians.</summary>
    public static LunarSky Lunar(double angle, double height)
    {
        var moon = MoonPosition(angle, MoonOrbitNear, height);
        var behind = moon.X;
        var cone = EarthShadow;
        var umbra = cone.Umbra(Math.Max(behind, 0));
        var penumbra = cone.Penumbra(Math.Max(behind, 0));

        // Seen from the night side looking away from the Sun with north up, +Y is to the left.
        var x = -moon.Y;
        var y = moon.Z;
        if (behind <= 0)
        {
            return new LunarSky(MoonRadius, x, y, umbra, penumbra, false, 0, 1, EclipseStage.None);
        }

        var distance = Math.Sqrt((x * x) + (y * y));
        var inUmbra = Math.Clamp(OverlapArea(MoonRadius, umbra, distance) / (Math.PI * MoonRadius * MoonRadius), 0, 1);
        var light = Math.Clamp((distance - umbra) / (penumbra - umbra), 0, 1);

        var stage = distance + MoonRadius <= umbra ? EclipseStage.LunarTotal
            : distance - MoonRadius < umbra ? EclipseStage.LunarPartial
            : distance - MoonRadius < penumbra ? EclipseStage.LunarPenumbral
            : EclipseStage.None;
        return new LunarSky(MoonRadius, x, y, umbra, penumbra, true, inUmbra, light, stage);
    }

    /// <summary>The Moon's shadow where it meets Earth's day side, with the Moon at <paramref name="angle"/> radians.</summary>
    public static ShadowOnEarth MoonShadowOnEarth(double angle, bool moonNear)
    {
        var moon = MoonPosition(angle, MoonOrbitRadius(moonNear), 0);
        var axis = moon - Sun;

        // A plane through the day side, close to the surface facing the Sun.
        const double planeX = -EarthRadius * 0.9;
        var t = (planeX - Sun.X) / axis.X;
        if (axis.X <= 0 || t <= 1)
        {
            return new ShadowOnEarth(0, 0, 0, 0, false);
        }

        var hit = Sun + (axis * t);
        var cone = new ShadowCone(MoonRadius, axis.Length);
        var distance = (hit - moon).Length;
        return new ShadowOnEarth(hit.Y, hit.Z, cone.Umbra(distance), cone.Penumbra(distance), true);
    }

    /// <summary>Area shared by two discs of radii <paramref name="r1"/> and <paramref name="r2"/> whose centres are <paramref name="d"/> apart.</summary>
    public static double OverlapArea(double r1, double r2, double d)
    {
        if (r1 <= 0 || r2 <= 0 || d >= r1 + r2) return 0;

        if (d <= Math.Abs(r1 - r2))
        {
            var r = Math.Min(r1, r2);
            return Math.PI * r * r;
        }

        var a1 = Math.Acos(Math.Clamp(((d * d) + (r1 * r1) - (r2 * r2)) / (2 * d * r1), -1, 1));
        var a2 = Math.Acos(Math.Clamp(((d * d) + (r2 * r2) - (r1 * r1)) / (2 * d * r2), -1, 1));
        var kite = Math.Sqrt(Math.Max(0, (-d + r1 + r2) * (d + r1 - r2) * (d - r1 + r2) * (d + r1 + r2)));
        return (r1 * r1 * a1) + (r2 * r2 * a2) - (0.5 * kite);
    }

    /// <summary>
    /// Playback speed in degrees of the Moon's orbit per second at <paramref name="angleDegrees"/>: a month passes in
    /// about twelve seconds, slowing right down through the eclipse so its phases (and the brief totality) can be seen.
    /// </summary>
    public static double PlaybackRate(EclipseMode mode, double angleDegrees)
    {
        const double cruise = 30;
        switch (mode)
        {
            case EclipseMode.Solar:
            {
                var d = Math.Abs(NormalizeDegrees(angleDegrees));
                return 0.35 + (2.65 * Noise.SmoothStep(0.5, 2, d)) + ((cruise - 3) * Noise.SmoothStep(24, 40, d));
            }

            case EclipseMode.Lunar:
            {
                var d = Math.Abs(NormalizeDegrees(angleDegrees - 180));
                return 4.5 + ((cruise - 4.5) * Noise.SmoothStep(36, 52, d));
            }

            default:
                return cruise;
        }
    }

    /// <summary>The angle in [−180, 180).</summary>
    public static double NormalizeDegrees(double degrees) => degrees - (360 * Math.Floor((degrees + 180) / 360));

    /// <summary>Tangent of the angle a sphere of <paramref name="radius"/> at <paramref name="distance"/> subtends.</summary>
    private static double ApparentRadius(double radius, double distance) =>
        Math.Tan(Math.Asin(Math.Min(1, radius / distance)));
}

public enum SeasonEventKind
{
    None,
    SolarEclipse,
    LunarEclipse,
    PenumbralLunarEclipse,
}

/// <summary>A new or full moon, the Moon's ecliptic latitude then and the eclipse it brings, if any.</summary>
public readonly record struct SeasonEvent(double Day, bool IsNewMoon, double Latitude, SeasonEventKind Kind);

/// <summary>
/// The Moon's latitude over the year, for the "why not every month" view: its orbit is tilted 5.1° to the ecliptic,
/// so at most new and full moons it passes above or below the Sun or Earth's shadow. Only when the Sun is near one
/// of the nodes (an eclipse season, twice a year) is the Moon close enough to the ecliptic for an eclipse.
/// </summary>
/// <remarks>
/// Mean motions only, and the line of nodes is held still (it really turns once in 18.6 years, which makes the
/// seasons come about 19 days earlier each year). Day 0 is an arbitrary start, not a calendar date. The limits are
/// approximate: a solar eclipse is seen somewhere on Earth within about 1.5° of latitude, a lunar eclipse touches the
/// umbra within about 1°, and the penumbra within about 1.6°.
/// </remarks>
public static class EclipseSeasons
{
    public const double Year = 365.25;
    public const double SynodicMonth = 29.530589;
    public const double Inclination = 5.145;
    public const double NodeLongitude = 95;
    public const double FirstNewMoon = 10;
    public const double SolarLimit = 1.5;
    public const double LunarLimit = 1.0;
    public const double PenumbralLimit = 1.6;

    /// <summary>How far (in degrees of the Sun's longitude) either side of a node an eclipse season reaches.</summary>
    public static double SeasonHalfWidth => Math.Asin(SolarLimit / Inclination) * 180 / Math.PI;

    /// <summary>The Sun's longitude as seen from Earth, in degrees.</summary>
    public static double SunLongitude(double day) => 360 * day / Year;

    /// <summary>The Moon's longitude in degrees; equal to the Sun's at new moon.</summary>
    public static double MoonLongitude(double day) => SunLongitude(day) + (360 * (day - FirstNewMoon) / SynodicMonth);

    /// <summary>The Moon's ecliptic latitude in degrees (north positive).</summary>
    public static double MoonLatitude(double day) => Inclination * Math.Sin(ToRadians(MoonLongitude(day) - NodeLongitude));

    /// <summary>The Sun is close enough to a node for the new moon to bring a solar eclipse.</summary>
    public static bool InSeason(double day) =>
        Math.Abs(Inclination * Math.Sin(ToRadians(SunLongitude(day) - NodeLongitude))) < SolarLimit;

    /// <summary>New and full moons between <paramref name="from"/> and <paramref name="to"/> (days), in order.</summary>
    public static IEnumerable<SeasonEvent> Events(double from, double to)
    {
        var halfMonth = SynodicMonth / 2;
        for (var k = (long)Math.Ceiling((from - FirstNewMoon) / halfMonth); ; k++)
        {
            var day = FirstNewMoon + (k * halfMonth);
            if (day > to) yield break;

            var isNewMoon = k % 2 == 0;
            var latitude = MoonLatitude(day);
            yield return new SeasonEvent(day, isNewMoon, latitude, Classify(isNewMoon, latitude));
        }
    }

    public static SeasonEventKind Classify(bool isNewMoon, double latitude)
    {
        var distance = Math.Abs(latitude);
        if (isNewMoon) return distance < SolarLimit ? SeasonEventKind.SolarEclipse : SeasonEventKind.None;

        return distance < LunarLimit ? SeasonEventKind.LunarEclipse
            : distance < PenumbralLimit ? SeasonEventKind.PenumbralLunarEclipse
            : SeasonEventKind.None;
    }

    /// <summary>The first day after <paramref name="day"/> on which an eclipse season begins.</summary>
    public static double NextSeasonStart(double day)
    {
        var inSeason = InSeason(day);
        for (var d = day + 0.25; d < day + Year; d += 0.25)
        {
            var now = InSeason(d);
            if (now && !inSeason) return d;
            inSeason = now;
        }

        return day;
    }

    /// <summary>Playback speed in days per second: a year in about half a minute, slower inside an eclipse season.</summary>
    public static double PlaybackRate(double day)
    {
        var sunLatitude = Math.Abs(Inclination * Math.Sin(ToRadians(SunLongitude(day) - NodeLongitude)));
        return 5 + (7 * Noise.SmoothStep(SolarLimit, SolarLimit + 1.2, sunLatitude));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
