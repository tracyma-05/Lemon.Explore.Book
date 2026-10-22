using Lemon.Explore.Book.Chapters.DayNight;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Aurora;

/// <summary>The views of the aurora page.</summary>
public enum AuroraMode
{
    /// <summary>Side view of the magnetosphere in the solar wind, with the Dungey cycle.</summary>
    Shield,

    /// <summary>A charged particle trapped in Earth's dipole field: gyration, bounce, drift, the loss cone.</summary>
    Particles,

    /// <summary>The auroral oval on the globe, a slice through the curtain and the sky seen from the shared location.</summary>
    Sky,
}

/// <summary>The trapped particle: an electron or a proton.</summary>
public enum AuroraSpecies
{
    Electron,
    Proton,
}

/// <summary>Where a particle's numbers come out: gyration, bounce and drift periods, speed and mirror latitude.</summary>
public readonly record struct ParticleMotion(double Speed, double GyroPeriod, double BouncePeriod, double DriftPeriod, double MirrorLatitude, double LossCone);

/// <summary>Light given off at one height by the falling electrons: oxygen green, oxygen red, nitrogen violet (relative).</summary>
public readonly record struct AuroraLight(double Green, double Red, double Violet)
{
    public double Total => Green + Red + Violet;
}

/// <summary>
/// The physics of the aurora page, in plain numbers:
/// <list type="bullet">
/// <item>The solar wind's push and the magnetopause and bow shock it gives (Shue et al. 1998; Spreiter's stand-off rule).</item>
/// <item>A rough Kp from the solar wind (Newell et al. 2007 coupling function).</item>
/// <item>Earth's field as a centred dipole (IGRF 2025: 29 800 nT at the equator, pole near 80.8° N 72.7° W): field lines,
/// footpoints, mirror points, the loss cone, and the gyration, bounce and drift periods (Schulz and Lanzerotti 1974).</item>
/// <item>Geomagnetic coordinates and magnetic local time, and a rule-of-thumb auroral oval by Kp (after Feldstein and Starkov).</item>
/// <item>How far an aurora at a height can be seen, and what colour it is at each height: where electrons of an energy stop,
/// and how much oxygen green (557.7 nm, lifetime 0.7 s) and red (630 nm, lifetime 110 s) survive collisions there.</item>
/// </list>
/// </summary>
internal static class AuroraModel
{
    public const double EarthRadiusKm = 6371.2;

    /// <summary>The dipole field at the magnetic equator on the ground, nT (IGRF 2025, rounded).</summary>
    public const double EquatorialField = 29_800;

    /// <summary>The geomagnetic north pole (the dipole axis), 2025: latitude and longitude.</summary>
    public const double PoleLatitude = 80.8;

    public const double PoleLongitude = -72.7;

    /// <summary>The part of the interplanetary field across the Sun–Earth line, nT, taken as fixed (only Bz is a slider).</summary>
    public const double ImfBy = 3;

    /// <summary>Height of the bottom of the atmosphere as far as trapped particles go, km: below it they are lost.</summary>
    public const double AtmosphereKm = 100;

    #region solar wind and the magnetopause

    /// <summary>The solar wind's dynamic pressure, nPa, from its density (protons per cm³) and speed (km/s).</summary>
    public static double DynamicPressure(double density, double speed) => 1.6726e-6 * density * speed * speed;

    /// <summary>The magnetopause's distance at the nose, Earth radii (Shue et al. 1998).</summary>
    public static double Standoff(double pressure, double bz) =>
        (10.22 + (1.29 * Math.Tanh(0.184 * (bz + 8.14)))) * Math.Pow(Math.Max(0.05, pressure), -1 / 6.6);

    /// <summary>How the magnetopause flares out towards the tail (Shue et al. 1998).</summary>
    public static double Flaring(double pressure, double bz) =>
        (0.58 - (0.007 * bz)) * (1 + (0.024 * Math.Log(Math.Max(0.05, pressure))));

    /// <summary>
    /// The distance of a surface of the Shue shape at an angle from the Sun–Earth line, Earth radii: <paramref name="nose"/>
    /// straight towards the Sun, wider as the angle grows.
    /// </summary>
    public static double ShapeRadius(double angleDegrees, double nose, double flaring)
    {
        var c = Math.Cos(angleDegrees * Math.PI / 180);
        return nose * Math.Pow(2 / Math.Max(1e-6, 1 + c), flaring);
    }

    /// <summary>The fast magnetosonic Mach number of the solar wind (a 50 km/s sound speed and an IMF of Bz and By).</summary>
    public static double MachNumber(double density, double speed, double bz)
    {
        var field = Math.Sqrt((bz * bz) + (ImfBy * ImfBy));
        var alfven = 21.8 * field / Math.Sqrt(Math.Max(0.1, density));
        return speed / Math.Sqrt((alfven * alfven) + (50 * 50));
    }

    /// <summary>The bow shock's distance at the nose: the magnetopause's plus Spreiter's 1.1 × the density ratio across the shock.</summary>
    public static double BowShockStandoff(double nose, double mach)
    {
        const double gamma = 5.0 / 3;
        var m2 = Math.Max(1.2, mach) * Math.Max(1.2, mach);
        var compression = (gamma + 1) * m2 / (((gamma - 1) * m2) + 2);
        return nose * (1 + (1.1 / compression));
    }

    /// <summary>
    /// A rough Kp (0–9) from the solar wind, with Newell et al.'s (2007) fit to the coupling function
    /// dΦ/dt = v^4/3 B^2/3 sin^8/3(θ/2) and the wind's pressure.
    /// </summary>
    public static double EstimatedKp(double density, double speed, double bz)
    {
        var field = Math.Sqrt((bz * bz) + (ImfBy * ImfBy));
        var clock = Math.Atan2(ImfBy, bz);
        var coupling = Math.Pow(speed, 4.0 / 3) * Math.Pow(field, 2.0 / 3) * Math.Pow(Math.Abs(Math.Sin(clock / 2)), 8.0 / 3);
        var kp = 0.05 + (2.244e-4 * coupling) + (2.844e-6 * Math.Sqrt(density) * speed * speed);
        return Math.Clamp(kp, 0, 9);
    }

    /// <summary>Geostationary orbit, Earth radii: a magnetopause pushed inside it leaves those satellites in the solar wind.</summary>
    public const double Geostationary = 6.62;

    #endregion

    #region the dipole

    /// <summary>The latitude where the field line crossing the equator at <paramref name="shell"/> Earth radii meets the ground.</summary>
    public static double FootLatitude(double shell, double heightKm = 0)
    {
        var r = 1 + (heightKm / EarthRadiusKm);
        return Math.Acos(Math.Sqrt(Math.Clamp(r / shell, 0, 1))) * 180 / Math.PI;
    }

    /// <summary>The field's strength along a field line at a magnetic latitude, as a multiple of the strength at its equator.</summary>
    public static double FieldRatio(double latitudeDegrees)
    {
        var (s, c) = Math.SinCos(latitudeDegrees * Math.PI / 180);
        return Math.Sqrt(1 + (3 * s * s)) / Math.Pow(c, 6);
    }

    /// <summary>
    /// The latitude where a particle crossing the equator at pitch angle <paramref name="pitchDegrees"/> turns back: where
    /// the field is 1/sin² of the pitch angle times its equatorial strength (its magnetic moment is kept).
    /// </summary>
    public static double MirrorLatitude(double pitchDegrees)
    {
        var s = Math.Sin(Math.Clamp(pitchDegrees, 0.01, 90) * Math.PI / 180);
        var target = 1 / (s * s);
        double lo = 0, hi = 89.99;
        for (var i = 0; i < 60; i++)
        {
            var mid = (lo + hi) / 2;
            if (FieldRatio(mid) < target) lo = mid;
            else hi = mid;
        }

        return (lo + hi) / 2;
    }

    /// <summary>
    /// The loss cone at the equator of shell <paramref name="shell"/>, degrees: particles with a smaller pitch angle would
    /// turn back only below <see cref="AtmosphereKm"/>, so they hit the air instead.
    /// </summary>
    public static double LossCone(double shell)
    {
        // The field at the foot 100 km up, as a multiple of the line's equatorial field, is all that counts.
        var footField = FieldRatio(FootLatitude(shell, AtmosphereKm));
        return Math.Asin(Math.Sqrt(1 / footField)) * 180 / Math.PI;
    }

    /// <summary>The bounce integral's T(y) and D(y) of Schulz and Lanzerotti (1974), y = sin of the equatorial pitch angle.</summary>
    public static (double T, double D) BounceIntegrals(double pitchDegrees)
    {
        var y = Math.Sin(Math.Clamp(pitchDegrees, 0, 90) * Math.PI / 180);
        var y34 = Math.Pow(y, 0.75);
        var t = 1.380173 - (0.639693 * y34);
        var d = (5.520692 - (2.357194 * y) + (1.279385 * y34)) / 12;
        return (t, d);
    }

    /// <summary>
    /// The motion of an electron or proton of kinetic energy <paramref name="energyKeV"/> on shell <paramref name="shell"/> at
    /// equatorial pitch angle <paramref name="pitchDegrees"/>: its speed (km/s), the gyration period at the equator, the
    /// bounce period (pole to pole and back) and the drift period round Earth, in seconds; relativistic.
    /// </summary>
    public static ParticleMotion Motion(AuroraSpecies species, double energyKeV, double shell, double pitchDegrees)
    {
        const double c = 299_792_458;
        const double charge = 1.602176634e-19;
        var mass = species == AuroraSpecies.Electron ? 9.1093837e-31 : 1.67262192e-27;
        var restKeV = mass * c * c / charge / 1000;
        var gamma = 1 + (energyKeV / restKeV);
        var v = c * Math.Sqrt(1 - (1 / (gamma * gamma)));
        var re = EarthRadiusKm * 1000;
        var b0 = EquatorialField * 1e-9;
        var bEq = b0 / (shell * shell * shell);

        var gyro = 2 * Math.PI * gamma * mass / (charge * bEq);
        var (t, d) = BounceIntegrals(pitchDegrees);
        var bounce = 4 * shell * re * t / v;
        var pv = gamma * mass * v * v;
        var drift = 2 * Math.PI * charge * b0 * re * re / (3 * shell * pv) * (t / d);
        return new ParticleMotion(v / 1000, gyro, bounce, drift, MirrorLatitude(pitchDegrees), LossCone(shell));
    }

    #endregion

    #region geomagnetic coordinates

    private static readonly Vec3 PoleAxis = Vec3.FromGeo(PoleLatitude, PoleLongitude, 0);

    /// <summary>The frame of geomagnetic coordinates: x along the meridian through the geographic north pole, z the axis.</summary>
    private static readonly (Vec3 X, Vec3 Y, Vec3 Z) Frame = MakeFrame();

    /// <summary>The geomagnetic frame's axes in geographic coordinates (x at latitude 0, longitude 0; z the North Pole).</summary>
    public static (Vec3 X, Vec3 Y, Vec3 Z) MagneticFrame => Frame;

    private static (Vec3, Vec3, Vec3) MakeFrame()
    {
        var z = PoleAxis.Normalized();
        var north = new Vec3(0, 0, 1);
        var x = (north - (z * north.Dot(z))).Normalized();
        return (x, z.Cross(x), z);
    }

    /// <summary>Magnetic latitude and longitude (centred dipole) of a geographic point, degrees.</summary>
    public static (double Latitude, double Longitude) ToMagnetic(double latitude, double longitude)
    {
        var p = Vec3.FromGeo(latitude, longitude, 0);
        var mlat = Math.Asin(Math.Clamp(p.Dot(Frame.Z), -1, 1)) * 180 / Math.PI;
        var mlon = Math.Atan2(p.Dot(Frame.Y), p.Dot(Frame.X)) * 180 / Math.PI;
        return (mlat, mlon);
    }

    /// <summary>The geographic point at a magnetic latitude and longitude, degrees.</summary>
    public static GeoPoint FromMagnetic(double mlat, double mlon)
    {
        var (sl, cl) = Math.SinCos(mlat * Math.PI / 180);
        var (so, co) = Math.SinCos(mlon * Math.PI / 180);
        var p = (Frame.X * (cl * co)) + (Frame.Y * (cl * so)) + (Frame.Z * sl);
        return new GeoPoint(Math.Asin(Math.Clamp(p.Z, -1, 1)) * 180 / Math.PI, Math.Atan2(p.Y, p.X) * 180 / Math.PI);
    }

    /// <summary>The magnetic longitude of the point under the Sun at an instant.</summary>
    public static double SubsolarMagneticLongitude(DateTime utc)
    {
        var sun = DayNightGeometry.Sun(utc);
        return ToMagnetic(sun.Declination, sun.SubsolarLongitude).Longitude;
    }

    /// <summary>Magnetic local time, hours 0–24: 12 under the Sun, 0 at magnetic midnight.</summary>
    public static double MagneticLocalTime(DateTime utc, double latitude, double longitude)
    {
        var mlon = ToMagnetic(latitude, longitude).Longitude;
        return Wrap24(12 + ((mlon - SubsolarMagneticLongitude(utc)) / 15));
    }

    /// <summary>The magnetic longitude where it is <paramref name="mlt"/> o'clock magnetic local time at an instant.</summary>
    public static double MagneticLongitudeAt(double subsolarMlon, double mlt) => subsolarMlon + ((mlt - 12) * 15);

    public static double Wrap24(double hours) => ((hours % 24) + 24) % 24;

    #endregion

    #region the auroral oval

    /// <summary>
    /// The oval's equatorward edge, magnetic latitude, at a magnetic local time: about 66.5° at midnight in quiet times and
    /// 2° lower per step of Kp; the day side sits higher and moves less. A rounded rule of thumb, not a fitted model.
    /// </summary>
    public static double OvalEquatorward(double kp, double mlt) => Blend(66.5 - (2.0 * kp), 76 - (0.9 * kp), mlt);

    /// <summary>The oval's poleward edge, magnetic latitude.</summary>
    public static double OvalPoleward(double kp, double mlt) => Blend(72 - (0.7 * kp), 79.5 - (0.4 * kp), mlt);

    /// <summary>How bright the oval is at a magnetic local time, 0–1: brightest a little before magnetic midnight, and with Kp.</summary>
    public static double OvalBrightness(double kp, double mlt)
    {
        var night = 0.5 + (0.5 * Math.Cos((mlt - 23) * Math.PI / 12));
        return Math.Clamp((0.25 + (0.75 * Math.Clamp(kp, 0, 9) / 9)) * (0.3 + (0.7 * night)), 0, 1);
    }

    /// <summary>Midnight value at 0 h, noon value at 12 h, a cosine in between.</summary>
    private static double Blend(double midnight, double noon, double mlt) =>
        ((midnight + noon) / 2) - ((noon - midnight) / 2 * Math.Cos(mlt * Math.PI / 12));

    #endregion

    #region seeing it

    /// <summary>
    /// The elevation above the horizon, degrees, of a light <paramref name="heightKm"/> up and <paramref name="groundKm"/>
    /// away along the ground (Earth's curve included); negative when it is below the horizon.
    /// </summary>
    public static double Elevation(double heightKm, double groundKm)
    {
        var gamma = Math.Abs(groundKm) / EarthRadiusKm;
        var e = Math.Atan2(Math.Cos(gamma) - (EarthRadiusKm / (EarthRadiusKm + heightKm)), Math.Sin(gamma));
        return e * 180 / Math.PI;
    }

    /// <summary>
    /// The height, km, where a line of sight at <paramref name="elevationDegrees"/> passes over the point
    /// <paramref name="groundKm"/> away; infinite if it never gets there.
    /// </summary>
    public static double HeightOnSight(double elevationDegrees, double groundKm)
    {
        var gamma = Math.Abs(groundKm) / EarthRadiusKm;
        var e = elevationDegrees * Math.PI / 180;
        var c = Math.Cos(gamma + e);
        return c <= 1e-9 ? double.PositiveInfinity : (EarthRadiusKm * Math.Cos(e) / c) - EarthRadiusKm;
    }

    /// <summary>How far along the ground a light <paramref name="heightKm"/> up still stands <paramref name="minElevation"/>° above the horizon, km.</summary>
    public static double SightDistance(double heightKm, double minElevation = 3)
    {
        var e = minElevation * Math.PI / 180;
        var gamma = Math.Acos(EarthRadiusKm * Math.Cos(e) / (EarthRadiusKm + heightKm)) - e;
        return gamma * EarthRadiusKm;
    }

    /// <summary>Kilometres along the ground per degree of (magnetic) latitude.</summary>
    public const double KmPerDegree = EarthRadiusKm * Math.PI / 180;

    /// <summary>
    /// The Kp at which the top of the aurora (<paramref name="topKm"/> up) would rise at least 3° over the horizon at
    /// magnetic latitude <paramref name="mlat"/> around magnetic midnight; above 9 when only a storm beyond the scale would do.
    /// </summary>
    public static double KpToSee(double mlat, double topKm = 400)
    {
        var reach = SightDistance(topKm) / KmPerDegree;
        var needed = Math.Abs(mlat) + reach;
        // Invert the midnight edge 66.5 − 2 Kp (extrapolated past 9).
        return Math.Max(0, (66.5 - needed) / 2.0);
    }

    #endregion

    #region the light

    /// <summary>Number density of the air, molecules per cm³, by height (US Standard Atmosphere 1976, rounded).</summary>
    private static readonly (double Height, double Log10)[] Air =
    [
        (60, Math.Log10(6.4e15)), (70, Math.Log10(1.7e15)), (80, Math.Log10(4.2e14)), (90, Math.Log10(7.1e13)),
        (100, Math.Log10(1.2e13)), (110, Math.Log10(2.1e12)), (120, Math.Log10(5.1e11)), (150, Math.Log10(5.2e10)),
        (200, Math.Log10(7.3e9)), (250, Math.Log10(1.9e9)), (300, Math.Log10(6.4e8)), (400, Math.Log10(1.05e8)),
        (500, Math.Log10(2.2e7)), (700, Math.Log10(2.0e6)),
    ];

    public static double Density(double heightKm)
    {
        if (heightKm <= Air[0].Height) return Math.Pow(10, Air[0].Log10);
        for (var i = 1; i < Air.Length; i++)
        {
            if (heightKm <= Air[i].Height)
            {
                var (h0, l0) = Air[i - 1];
                var (h1, l1) = Air[i];
                return Math.Pow(10, l0 + ((l1 - l0) * (heightKm - h0) / (h1 - h0)));
            }
        }

        return Math.Pow(10, Air[^1].Log10);
    }

    /// <summary>
    /// The share of excited O(¹S) atoms that give off their green 557.7 nm light before a collision takes the energy: it
    /// lives 0.74 s, so the green fades below about 100 km.
    /// </summary>
    public static double GreenSurvives(double heightKm) => 1.35 / (1.35 + (2.5e-13 * Density(heightKm)));

    /// <summary>
    /// The share of excited O(¹D) atoms that give off their red 630 nm light: it lives about 110 s, so collisions put it out
    /// below about 200 km and the red shows only high up.
    /// </summary>
    public static double RedSurvives(double heightKm) => 0.0091 / (0.0091 + (1.5e-11 * Density(heightKm)));

    /// <summary>Where electrons of an energy put most of it into the air, km (approximate, after Rees 1963 and Fang et al. 2010).</summary>
    private static readonly (double LogKeV, double Height)[] Stopping =
    [
        (Math.Log10(0.2), 260), (Math.Log10(0.5), 205), (Math.Log10(1), 172), (Math.Log10(2), 145), (Math.Log10(5), 120),
        (Math.Log10(10), 108), (Math.Log10(20), 100), (Math.Log10(50), 90), (Math.Log10(100), 83),
    ];

    public static double PeakHeight(double energyKeV)
    {
        var x = Math.Log10(Math.Clamp(energyKeV, 0.2, 100));
        for (var i = 1; i < Stopping.Length; i++)
        {
            if (x <= Stopping[i].LogKeV)
            {
                var (x0, h0) = Stopping[i - 1];
                var (x1, h1) = Stopping[i];
                return h0 + ((h1 - h0) * (x - x0) / (x1 - x0));
            }
        }

        return Stopping[^1].Height;
    }

    /// <summary>
    /// The energy the electrons leave at a height, relative to the most (1 at the peak): a sharp bottom where they stop and
    /// a long tail upwards.
    /// </summary>
    public static double Deposition(double heightKm, double energyKeV)
    {
        var peak = PeakHeight(energyKeV);
        if (heightKm <= peak)
        {
            var width = 4 + (0.08 * (peak - 90));
            var z = (heightKm - peak) / width;
            return Math.Exp(1 - z - Math.Exp(-z));
        }

        var up = 22 + (0.35 * Math.Max(0, peak - 100));
        return Math.Exp(-(heightKm - peak) / up);
    }

    /// <summary>
    /// The highest the curtain still glows noticeably for electrons of an energy, km: the top height where its light is at
    /// least a twenty-fifth of the brightest. Soft electrons give a tall, red-topped curtain that can be seen from farther.
    /// </summary>
    public static double VisibleTop(double energyKeV)
    {
        var most = 0.0;
        var top = 100.0;
        for (var h = 80.0; h <= 600; h += 2) most = Math.Max(most, Light(h, energyKeV).Total);
        for (var h = 80.0; h <= 600; h += 2)
        {
            if (Light(h, energyKeV).Total >= most / 25) top = h;
        }

        return top;
    }

    /// <summary>The light at a height from electrons of an energy: green, red and violet, before the eye's response.</summary>
    public static AuroraLight Light(double heightKm, double energyKeV)
    {
        var q = Deposition(heightKm, energyKeV);
        // Slow secondary electrons high up excite the red (1.97 eV) far more than the green (4.19 eV).
        var redYield = 0.5 + (1.5 * Math.Clamp((heightKm - 150) / 150, 0, 1));
        return new AuroraLight(q * GreenSurvives(heightKm), q * RedSurvives(heightKm) * redYield, q * 0.28);
    }

    #endregion
}
