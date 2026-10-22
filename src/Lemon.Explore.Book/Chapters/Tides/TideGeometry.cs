using Lemon.Explore.Book.Chapters.MoonPhases;
using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Tides;

/// <summary>What the tides page shows.</summary>
public enum TidesMode
{
    /// <summary>Earth from above with the Moon: the tidal force, the two bulges, and Earth turning under them.</summary>
    Bulges,

    /// <summary>The Sun's bulges adding to the Moon's at new and full moon, and working against them at the quarters.</summary>
    SpringNeap,

    /// <summary>A harbour seen from the side, the water rising and falling through the day.</summary>
    Coast,
}

/// <summary>Where the water is in its rise and fall.</summary>
public enum TideStage
{
    High,
    Falling,
    Low,
    Rising,
}

/// <summary>A high or low tide: when (days since the new moon), how high, and which.</summary>
public readonly record struct TideTurn(double Day, double Height, bool IsHigh);

/// <summary>
/// The tide model of the tides page, and the physics behind it: the Moon's and the Sun's tide-raising forces and the
/// equilibrium tide they would raise on an ocean covering the whole Earth.
/// </summary>
/// <remarks>
/// Time is a <c>day</c>: days since a new moon that falls at 00:00, so its fraction is the observer's local solar time.
/// The observer stands on the equator and the Moon and the Sun move in the equator's plane, so both tides are
/// semidiurnal and the two highs of a day are equally high (no diurnal inequality). Heights are relative to the Moon's
/// share: <c>cos 2H</c>, H the Moon's hour angle, plus <see cref="SolarRatio"/> times the same for the Sun. Real coasts
/// add a delay of hours (the lunitidal interval) and amplify or damp the range; the model leaves both out.
/// Angles are in degrees unless the name says otherwise.
/// </remarks>
public static class TideGeometry
{
    public const double GravitationalConstant = 6.674e-11;
    public const double EarthMassKg = 5.972e24;
    public const double MoonMassKg = 7.342e22;
    public const double SunMassKg = 1.989e30;
    public const double EarthRadiusKm = 6371;
    public const double MoonDistanceKm = 384_400;
    public const double SunDistanceKm = 149_597_871;

    /// <summary>
    /// The Sun's tide-raising force as a share of the Moon's, about 0.46: 27 million times the mass, but 389 times as
    /// far, and the tidal force falls off with the cube of the distance.
    /// </summary>
    public static readonly double SolarRatio = SunMassKg / MoonMassKg * Math.Pow(MoonDistanceKm / SunDistanceKm, 3);

    /// <summary>Hours from one passage of the Moon overhead to the next: 24 h 50 min, as Earth turns after the moving Moon.</summary>
    public static double LunarDayHours => 24 * MoonPhaseGeometry.SynodicMonth / (MoonPhaseGeometry.SynodicMonth - 1);

    /// <summary>Hours from one high tide to the next, on average: half a lunar day, 12 h 25 min.</summary>
    public static double TidePeriodHours => LunarDayHours / 2;

    /// <summary>Metres per unit of the model at the coast: the Moon alone raises and lowers the water by a metre.</summary>
    public const double CoastMetres = 1.0;

    #region forces

    /// <summary>A body's pull at <paramref name="distanceKm"/> from its centre, in m/s².</summary>
    public static double Gravity(double massKg, double distanceKm) => GravitationalConstant * massKg / Math.Pow(distanceKm * 1000, 2);

    /// <summary>
    /// The tidal acceleration on Earth's side nearest a body, in m/s²: the body's pull there minus its pull on Earth's
    /// centre. <paramref name="farSide"/> gives the side away from the body, where the pull is weaker than at the centre.
    /// </summary>
    public static double TidalAcceleration(double massKg, double distanceKm, bool farSide = false) => farSide
        ? Gravity(massKg, distanceKm) - Gravity(massKg, distanceKm + EarthRadiusKm)
        : Gravity(massKg, distanceKm - EarthRadiusKm) - Gravity(massKg, distanceKm);

    /// <summary>The usual approximation of <see cref="TidalAcceleration"/>: 2GMR/d³.</summary>
    public static double TidalAccelerationApprox(double massKg, double distanceKm) =>
        2 * GravitationalConstant * massKg * EarthRadiusKm * 1000 / Math.Pow(distanceKm * 1000, 3);

    /// <summary>
    /// The tidal force at a point of Earth's surface <paramref name="angle"/>° from the direction of the body, in units
    /// of GMR/d³: outwards (radial) and along the surface (tangential, positive away from the body's direction). Twice
    /// as strong outwards under the body and opposite it as it is inwards halfway between.
    /// </summary>
    public static (double Radial, double Tangential) TidalForce(double angle)
    {
        var (sin, cos) = Math.SinCos(SeasonsGeometry.ToRadians(angle));
        return ((3 * cos * cos) - 1, -3 * sin * cos);
    }

    /// <summary>
    /// The difference between high and low water, in metres, of the equilibrium tide a body raises on an ocean covering
    /// all of Earth: (3/2)(M/Mₑ)(R/d)³R. About 0.54 m for the Moon and 0.25 m for the Sun.
    /// </summary>
    public static double EquilibriumRange(double massKg, double distanceKm) =>
        1.5 * massKg / EarthMassKg * Math.Pow(EarthRadiusKm / distanceKm, 3) * EarthRadiusKm * 1000;

    #endregion

    #region the model

    /// <summary>Local solar time in hours, 0…24.</summary>
    public static double HourOfDay(double day) => MoonPhaseGeometry.WrapHours(day * 24);

    /// <summary>
    /// Where the observer is on Earth seen from above the North Pole: degrees counter-clockwise from the direction of the
    /// Sun, so 0 at noon, 90 at 18:00, 180 at midnight.
    /// </summary>
    public static double ObserverAngle(double day) => SeasonsGeometry.Normalize360(15 * (HourOfDay(day) - 12));

    /// <summary>The Moon's angle from the Sun, the same way round: 0 at new moon, 180 at full.</summary>
    public static double MoonAngle(double day) => MoonPhaseGeometry.Elongation(day);

    /// <summary>The Moon's hour angle, −180…180: 0 when it is highest in the sky, ±180 when it is right underfoot.</summary>
    public static double MoonHourAngle(double day) => SeasonsGeometry.Normalize180(ObserverAngle(day) - MoonAngle(day));

    /// <summary>
    /// The height of the water at <paramref name="angle"/>° round Earth (counter-clockwise from the Sun's direction), in
    /// units of the Moon's tide: highest under the Moon and opposite it, lowest halfway between.
    /// </summary>
    public static double WaterAt(double angle, double moonAngle, bool includeSun = true)
    {
        var lunar = Math.Cos(2 * SeasonsGeometry.ToRadians(angle - moonAngle));
        return includeSun ? lunar + (SolarRatio * Math.Cos(2 * SeasonsGeometry.ToRadians(angle))) : lunar;
    }

    /// <summary>The height of the water at the observer, in units of the Moon's tide, −(1 + r)…1 + r.</summary>
    public static double Height(double day, bool includeSun = true) => WaterAt(ObserverAngle(day), MoonAngle(day), includeSun);

    /// <summary>The rate of change of <see cref="Height"/> in units per day.</summary>
    public static double Rate(double day, bool includeSun = true)
    {
        var moonRate = 2 * Math.PI * 2 * (1 - (1 / MoonPhaseGeometry.SynodicMonth));
        var rate = -moonRate * Math.Sin(2 * SeasonsGeometry.ToRadians(MoonHourAngle(day)));
        if (includeSun) rate -= 2 * Math.PI * 2 * SolarRatio * Math.Sin(2 * SeasonsGeometry.ToRadians(ObserverAngle(day)));
        return rate;
    }

    /// <summary>The height at the coast in metres above the lowest possible low water (chart datum), 0…2(1 + r).</summary>
    public static double CoastHeight(double day) => (Height(day) + 1 + SolarRatio) * CoastMetres;

    /// <summary>Converts a height of the model (with the Sun) to metres above chart datum.</summary>
    public static double ToCoastMetres(double height) => (height + 1 + SolarRatio) * CoastMetres;

    /// <summary>
    /// The day's range between high and low water, in units of the Moon's tide: 2(1 + r) at new and full moon (spring
    /// tides), 2(1 − r) at the quarters (neap tides).
    /// </summary>
    public static double Range(double day)
    {
        var r = SolarRatio;
        return 2 * Math.Sqrt((1 + (r * r)) + (2 * r * Math.Cos(2 * SeasonsGeometry.ToRadians(MoonAngle(day)))));
    }

    public static double SpringRange => 2 * (1 + SolarRatio);

    public static double NeapRange => 2 * (1 - SolarRatio);

    /// <summary>Where the range is between neap (0) and spring (1).</summary>
    public static double SpringFraction(double day) => (Range(day) - NeapRange) / (SpringRange - NeapRange);

    /// <summary>The range is growing towards the next spring tide (from a quarter towards new or full moon).</summary>
    public static bool IsRangeGrowing(double day) => Math.Sin(2 * SeasonsGeometry.ToRadians(MoonAngle(day))) < 0;

    /// <summary>
    /// The high and low tides between two days, in order. Found where <see cref="Rate"/> changes sign on a ten-minute
    /// grid, then refined by bisection to well under a second.
    /// </summary>
    public static IReadOnlyList<TideTurn> Turns(double fromDay, double toDay, bool includeSun = true)
    {
        const double step = 10.0 / 1440;
        var turns = new List<TideTurn>();
        var a = fromDay;
        var rateA = Rate(a, includeSun);
        while (a < toDay)
        {
            var b = Math.Min(a + step, toDay);
            var rateB = Rate(b, includeSun);
            if (rateA != 0 && Math.Sign(rateA) != Math.Sign(rateB))
            {
                var (lo, hi, rateLo) = (a, b, rateA);
                for (var i = 0; i < 40; i++)
                {
                    var mid = (lo + hi) / 2;
                    var rateMid = Rate(mid, includeSun);
                    if (Math.Sign(rateMid) == Math.Sign(rateLo)) (lo, rateLo) = (mid, rateMid);
                    else hi = mid;
                }

                var day = (lo + hi) / 2;
                turns.Add(new TideTurn(day, Height(day, includeSun), rateA > 0));
            }

            (a, rateA) = (b, rateB);
        }

        return turns;
    }

    /// <summary>The next high (or low) tide after <paramref name="day"/>.</summary>
    public static TideTurn Next(double day, bool high, bool includeSun = true) =>
        Turns(day, day + 1, includeSun).First(t => t.IsHigh == high);

    /// <summary>High or low water within half an hour of a turn, else rising or falling.</summary>
    public static TideStage StageAt(double day, bool includeSun = true)
    {
        const double near = 0.5 / 24;
        foreach (var turn in Turns(day - near, day + near, includeSun))
        {
            return turn.IsHigh ? TideStage.High : TideStage.Low;
        }

        return Rate(day, includeSun) > 0 ? TideStage.Rising : TideStage.Falling;
    }

    #endregion
}
