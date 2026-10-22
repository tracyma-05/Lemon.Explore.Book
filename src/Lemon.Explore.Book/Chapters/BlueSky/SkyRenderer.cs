namespace Lemon.Explore.Book.Chapters.BlueSky;

/// <summary>
/// The light of the sky in every direction for one altitude of the Sun and one amount of haze: sunlight scattered once
/// by the air on its way to the eye (single scattering), dimmed on the way in from the Sun and on the way out to you,
/// with the Earth's shadow where the Sun's light runs into the ground. Kept as a table over the view's altitude and
/// its azimuth from the Sun, worked out again only when the Sun or the haze changes.
/// </summary>
internal sealed class SkyRenderer
{
    /// <summary>Rows of the table, from the horizon to straight up, closer together near the horizon.</summary>
    public const int AltitudeSteps = 46;

    /// <summary>Columns, 0…180° from the Sun's azimuth (the sky is the same on both sides).</summary>
    public const int AzimuthSteps = 61;

    private const int ViewSteps = 28;

    /// <summary>How much of the light from straight up is added everywhere for light scattered more than once.</summary>
    private const double MultipleScattering = 0.25;
    private const double Deg = Math.PI / 180;

    /// <summary>The air between any point and the Sun, looked up instead of added up for every step of every view ray.</summary>
    private const int LutHeights = 64;

    private const int LutAngles = 96;
    private static readonly Lazy<AirColumn[]> ColumnLut = new(BuildColumnLut);

    private readonly LinearRgb[] _table = new LinearRgb[AltitudeSteps * AzimuthSteps];
    private static readonly double[] RayleighByBand = SkyOptics.Wavelengths.Select(SkyOptics.Rayleigh).ToArray();
    private static readonly double[] OzoneByBand = SkyOptics.Wavelengths.Select(SkyOptics.Ozone).ToArray();

    /// <summary>The sky's brightness on a clear afternoon, which the eye's adjustment is measured from.</summary>
    private static readonly Lazy<double> DaylightLevel = new(() =>
    {
        var renderer = new SkyRenderer();
        renderer.Compute(40, 0.3);
        return renderer.MeanLuminance;
    });

    public double SunAltitude { get; private set; } = double.NaN;

    public double Haze { get; private set; } = double.NaN;

    /// <summary>The average brightness of the sky above 5°.</summary>
    public double MeanLuminance { get; private set; }

    /// <summary>Light falling on flat ground from the whole sky (not the Sun itself).</summary>
    public LinearRgb SkyLight { get; private set; }

    /// <summary>Sunlight coming straight through to the ground, 0 once the Sun is down.</summary>
    public LinearRgb SunLight { get; private set; }

    /// <summary>
    /// How much the eye opens up: the sky at its average brightness comes out a little over half bright by day, and
    /// the eye only part makes up for the dark at dusk, so twilight is dimmer and night is dark.
    /// </summary>
    public double Exposure { get; private set; } = 1;

    /// <summary>Works the table out again if the Sun or the haze changed enough to see; true if it did.</summary>
    public bool Update(double sunAltitude, double haze)
    {
        // The colours change fast with the Sun low, slowly with it high.
        var step = Math.Abs(sunAltitude) < 8 ? 0.04 : 0.15;
        if (Math.Abs(sunAltitude - SunAltitude) < step && Math.Abs(haze - Haze) < 0.005) return false;
        Compute(sunAltitude, haze);
        var level = DaylightLevel.Value;
        Exposure = 0.75 / (Math.Pow(level, 0.35) * Math.Pow(Math.Max(MeanLuminance, level * 1e-3), 0.65));
        return true;
    }

    private void Compute(double sunAltitude, double haze)
    {
        SunAltitude = sunAltitude;
        Haze = haze;

        var bands = SkyOptics.Bands;
        var mieByBand = SkyOptics.Wavelengths.Select(nm => SkyOptics.Mie(nm, haze)).ToArray();
        var sun = new Vec(Math.Cos(sunAltitude * Deg), 0, Math.Sin(sunAltitude * Deg));

        Parallel.For(0, AltitudeSteps, row =>
        {
            var light = new double[bands];
            var altitude = RowAltitude(row);
            for (var column = 0; column < AzimuthSteps; column++)
            {
                var azimuth = column * 180.0 / (AzimuthSteps - 1);
                var view = new Vec(Math.Cos(altitude * Deg) * Math.Cos(azimuth * Deg), Math.Cos(altitude * Deg) * Math.Sin(azimuth * Deg),
                    Math.Sin(altitude * Deg));
                Scatter(view, sun, mieByBand, light);
                _table[(row * AzimuthSteps) + column] = SkyOptics.ToRgb(light);
            }
        });

        // Light scattered more than once fills in the shadowed and the dimmest parts of the sky. Stand in for it with a
        // share of the light from straight up, which is the bluest and comes from all round.
        var overhead = _table[^1];
        for (var i = 0; i < _table.Length; i++) _table[i] += overhead * MultipleScattering;

        // Average brightness above 5°, and the light the sky throws on the ground (weighted towards overhead).
        double sum = 0, weight = 0;
        var ground = default(LinearRgb);
        double groundWeight = 0;
        for (var row = 0; row < AltitudeSteps; row++)
        {
            var altitude = RowAltitude(row);
            var w = Math.Cos(altitude * Deg);
            for (var column = 0; column < AzimuthSteps; column++)
            {
                var value = _table[(row * AzimuthSteps) + column];
                if (altitude > 5)
                {
                    sum += value.Luminance * w;
                    weight += w;
                }

                var g = Math.Sin(Math.Max(altitude, 1) * Deg) * w;
                ground += value * g;
                groundWeight += g;
            }
        }

        MeanLuminance = weight > 0 ? sum / weight : 0;
        SkyLight = ground * (Math.PI / groundWeight);
        var through = SkyOptics.Transmittance(sunAltitude, haze);
        SunLight = SkyOptics.ToRgb(through) * Math.Max(0, Math.Sin(sunAltitude * Deg));
    }

    /// <summary>The sky's light looking at <paramref name="altitude"/>, <paramref name="fromSun"/>° round from the Sun.</summary>
    public LinearRgb Radiance(double altitude, double fromSun)
    {
        var azimuth = Math.Abs(Normalize(fromSun));
        var x = azimuth / 180 * (AzimuthSteps - 1);
        var y = Math.Sqrt(Math.Clamp(altitude, 0, 90) / 90) * (AltitudeSteps - 1);
        var x0 = Math.Min((int)x, AzimuthSteps - 2);
        var y0 = Math.Min((int)y, AltitudeSteps - 2);
        var fx = x - x0;
        var fy = y - y0;
        var a = _table[(y0 * AzimuthSteps) + x0];
        var b = _table[(y0 * AzimuthSteps) + x0 + 1];
        var c = _table[((y0 + 1) * AzimuthSteps) + x0];
        var d = _table[((y0 + 1) * AzimuthSteps) + x0 + 1];
        return (((a * (1 - fx)) + (b * fx)) * (1 - fy)) + (((c * (1 - fx)) + (d * fx)) * fy);
    }

    /// <summary>Degrees folded into −180…180.</summary>
    public static double Normalize(double degrees) => degrees - (360 * Math.Floor((degrees + 180) / 360));

    private static double RowAltitude(int row)
    {
        var v = row / (double)(AltitudeSteps - 1);
        return 90 * v * v;
    }

    /// <summary>
    /// Adds up, band by band, the sunlight scattered towards the eye along a view ray from the ground: at each step the
    /// air there scatters what sunlight reaches it, and what it scatters is dimmed on its way back to the eye.
    /// </summary>
    private static void Scatter(Vec view, Vec sun, double[] mieByBand, double[] light)
    {
        Array.Clear(light);
        var bands = light.Length;
        var r0 = SkyOptics.EarthRadius + 1;
        var mu = view.Z;
        var length = (-r0 * mu) + Math.Sqrt(Math.Max(0, (r0 * r0 * ((mu * mu) - 1)) + (SkyOptics.TopRadius * SkyOptics.TopRadius)));
        var cosAngle = view.Dot(sun);
        var rayleighPhase = SkyOptics.RayleighPhase(cosAngle);
        var miePhase = SkyOptics.MiePhase(cosAngle);

        double viewRayleigh = 0, viewMie = 0, viewOzone = 0;
        for (var i = 0; i < ViewSteps; i++)
        {
            var u = (i + 0.5) / ViewSteps;
            var t = length * u * u;
            var dt = 2 * length * u / ViewSteps;
            var p = new Vec(view.X * t, view.Y * t, r0 + (view.Z * t));
            var r = p.Length;
            var height = r - SkyOptics.EarthRadius;
            var (dr, dm, dz) = SkyOptics.Density(height);

            // The air between the eye and the middle of this step.
            var halfRayleigh = viewRayleigh + (dr * dt / 2);
            var halfMie = viewMie + (dm * dt / 2);
            var halfOzone = viewOzone + (dz * dt / 2);
            viewRayleigh += dr * dt;
            viewMie += dm * dt;
            viewOzone += dz * dt;

            var towardsSun = ColumnToSpace(height, p.Dot(sun) / r);
            if (towardsSun.Blocked) continue; // in the Earth's shadow

            var rayleighColumn = halfRayleigh + towardsSun.Rayleigh;
            var mieColumn = (halfMie + towardsSun.Mie) / SkyOptics.MieAlbedo;
            var ozoneColumn = halfOzone + towardsSun.Ozone;
            var scatterRayleigh = dr * dt * rayleighPhase;
            var scatterMie = dm * dt * miePhase;
            for (var b = 0; b < bands; b++)
            {
                var depth = (RayleighByBand[b] * rayleighColumn) + (mieByBand[b] * mieColumn) + (OzoneByBand[b] * ozoneColumn);
                light[b] += ((RayleighByBand[b] * scatterRayleigh) + (mieByBand[b] * scatterMie)) * Math.Exp(-depth);
            }
        }
    }

    /// <summary>Like <see cref="SkyOptics.ColumnToSpace"/>, from the table.</summary>
    internal static AirColumn ColumnToSpace(double height, double cosZenith)
    {
        var lut = ColumnLut.Value;
        var h = Math.Clamp(height, 0, SkyOptics.AtmosphereHeight);
        var r = SkyOptics.EarthRadius + h;
        var horizon = -Math.Sqrt(Math.Max(0, 1 - (SkyOptics.EarthRadius * SkyOptics.EarthRadius / (r * r))));
        if (cosZenith < horizon) return new AirColumn(0, 0, 0, true);

        var y = Math.Sqrt(h / SkyOptics.AtmosphereHeight) * (LutHeights - 1);
        var x = Math.Sqrt(Math.Clamp((cosZenith - horizon) / (1 - horizon), 0, 1)) * (LutAngles - 1);
        var x0 = Math.Min((int)x, LutAngles - 2);
        var y0 = Math.Min((int)y, LutHeights - 2);
        var fx = x - x0;
        var fy = y - y0;
        var a = lut[(y0 * LutAngles) + x0];
        var b = lut[(y0 * LutAngles) + x0 + 1];
        var c = lut[((y0 + 1) * LutAngles) + x0];
        var d = lut[((y0 + 1) * LutAngles) + x0 + 1];
        double Mix(double va, double vb, double vc, double vd) => (((va * (1 - fx)) + (vb * fx)) * (1 - fy)) + (((vc * (1 - fx)) + (vd * fx)) * fy);
        return new AirColumn(Mix(a.Rayleigh, b.Rayleigh, c.Rayleigh, d.Rayleigh), Mix(a.Mie, b.Mie, c.Mie, d.Mie),
            Mix(a.Ozone, b.Ozone, c.Ozone, d.Ozone));
    }

    /// <summary>
    /// Rows by height (closer together low down), columns from the point's own horizon to straight up (closer
    /// together near the horizon, where the air along the ray changes fastest).
    /// </summary>
    private static AirColumn[] BuildColumnLut()
    {
        var lut = new AirColumn[LutHeights * LutAngles];
        Parallel.For(0, LutHeights, row =>
        {
            var v = row / (double)(LutHeights - 1);
            var h = SkyOptics.AtmosphereHeight * v * v;
            var r = SkyOptics.EarthRadius + h;
            var horizon = -Math.Sqrt(Math.Max(0, 1 - (SkyOptics.EarthRadius * SkyOptics.EarthRadius / (r * r))));
            for (var column = 0; column < LutAngles; column++)
            {
                var u = Math.Max(column, 0.05) / (LutAngles - 1);
                var mu = horizon + (u * u * (1 - horizon));
                var length = (-r * mu) + Math.Sqrt(Math.Max(0, (r * r * ((mu * mu) - 1)) + (SkyOptics.TopRadius * SkyOptics.TopRadius)));
                lut[(row * LutAngles) + column] = SkyOptics.Integrate(r, mu, length, 64);
            }
        });
        return lut;
    }

    private readonly record struct Vec(double X, double Y, double Z)
    {
        public double Length => Math.Sqrt((X * X) + (Y * Y) + (Z * Z));

        public double Dot(Vec o) => (X * o.X) + (Y * o.Y) + (Z * o.Z);
    }
}
