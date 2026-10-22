namespace Lemon.Explore.Book.Controls.Space;

/// <summary>
/// Keplerian elements at J2000 plus their rates per Julian century, from JPL's "Approximate Positions of the
/// Planets" (E. M. Standish, table 1, valid 1800–2050 AD; usable well beyond for a visual model).
/// </summary>
/// <remarks>
/// The referenced cnblogs article integrates Newton's gravity step by step. That works for two bodies, but, as
/// the article itself shows, a many-body Euler integration drifts and collapses after a few orbits. Solving
/// Kepler's equation instead is exact for each planet around the Sun, stays stable at any playback speed and
/// lets the page show where the planets really are on a given date.
/// </remarks>
public sealed record KeplerOrbit(
    double SemiMajorAxis, double SemiMajorAxisRate,
    double Eccentricity, double EccentricityRate,
    double Inclination, double InclinationRate,
    double MeanLongitude, double MeanLongitudeRate,
    double PerihelionLongitude, double PerihelionLongitudeRate,
    double NodeLongitude, double NodeLongitudeRate)
{
    private const double J2000 = 2451545.0;
    private const double Deg = Math.PI / 180;

    public static double JulianDay(DateTime utc) =>
        2440587.5 + (utc.ToUniversalTime() - DateTime.UnixEpoch).TotalDays;

    /// <summary>Orbit shape and orientation at a date, ready to evaluate positions along the orbit.</summary>
    public OrbitState At(double julianDay)
    {
        var t = (julianDay - J2000) / 36525.0;
        var a = SemiMajorAxis + (SemiMajorAxisRate * t);
        var e = Eccentricity + (EccentricityRate * t);
        var i = (Inclination + (InclinationRate * t)) * Deg;
        var l = MeanLongitude + (MeanLongitudeRate * t);
        var varpi = PerihelionLongitude + (PerihelionLongitudeRate * t);
        var node = (NodeLongitude + (NodeLongitudeRate * t)) * Deg;

        var argPeri = (varpi * Deg) - node;
        var meanAnomaly = NormalizeAngle((l - varpi) * Deg);

        return new OrbitState(a, e, i, argPeri, node, meanAnomaly);
    }

    public static double NormalizeAngle(double radians)
    {
        radians %= 2 * Math.PI;
        if (radians > Math.PI) radians -= 2 * Math.PI;
        if (radians < -Math.PI) radians += 2 * Math.PI;
        return radians;
    }
}

/// <summary>An orbit frozen at one date. Positions are heliocentric ecliptic coordinates in AU.</summary>
public readonly struct OrbitState
{
    private readonly double _a, _e, _b;
    private readonly double _m11, _m12, _m21, _m22;

    public OrbitState(double a, double e, double inclination, double argPeri, double node, double meanAnomaly)
    {
        _a = a;
        _e = e;
        _b = a * Math.Sqrt(1 - (e * e));
        MeanAnomaly = meanAnomaly;

        var (sw, cw) = Math.SinCos(argPeri);
        var (so, co) = Math.SinCos(node);
        var ci = Math.Cos(inclination);
        _m11 = (cw * co) - (sw * so * ci);
        _m12 = (-sw * co) - (cw * so * ci);
        _m21 = (cw * so) + (sw * co * ci);
        _m22 = (-sw * so) + (cw * co * ci);
    }

    public double MeanAnomaly { get; }

    /// <summary>Eccentric anomaly of the body at this date (Newton iteration on Kepler's equation).</summary>
    public double EccentricAnomaly
    {
        get
        {
            var m = MeanAnomaly;
            var ecc = m + (_e * Math.Sin(m));
            for (var k = 0; k < 6; k++)
            {
                var delta = (ecc - (_e * Math.Sin(ecc)) - m) / (1 - (_e * Math.Cos(ecc)));
                ecc -= delta;
                if (Math.Abs(delta) < 1e-10) break;
            }

            return ecc;
        }
    }

    /// <summary>Ecliptic x/y in AU for an eccentric anomaly (z is dropped: the view looks down on the ecliptic).</summary>
    public (double X, double Y) PositionAt(double eccentricAnomaly)
    {
        var (sinE, cosE) = Math.SinCos(eccentricAnomaly);
        var xp = _a * (cosE - _e);
        var yp = _b * sinE;
        return ((_m11 * xp) + (_m12 * yp), (_m21 * xp) + (_m22 * yp));
    }
}
