using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.BlueSky;

/// <summary>What the blue sky page shows.</summary>
public enum SkyMode
{
    /// <summary>The sky itself, worked out from how sunlight scatters in the air, over a sea horizon.</summary>
    Sky,

    /// <summary>A beam of sunlight through a box of air: blue is bounced aside far more than red.</summary>
    Scattering,

    /// <summary>The way sunlight comes through the air to you, short at noon and long at sunset.</summary>
    Path,
}

/// <summary>The part of the day, by how high the Sun is.</summary>
public enum SkyPhase
{
    Day,
    GoldenHour,
    Sunrise,
    Sunset,
    Twilight,
    NauticalTwilight,
    Night,
}

/// <summary>Linear RGB relative to sunlight in space: (1, 1, 1) is the Sun's own light before it meets the air.</summary>
public readonly record struct LinearRgb(double R, double G, double B)
{
    public double Luminance => (0.2126 * R) + (0.7152 * G) + (0.0722 * B);

    public double Max => Math.Max(R, Math.Max(G, B));

    public static LinearRgb operator +(LinearRgb a, LinearRgb b) => new(a.R + b.R, a.G + b.G, a.B + b.B);

    public static LinearRgb operator *(LinearRgb a, double k) => new(a.R * k, a.G * k, a.B * k);

    public static LinearRgb operator *(LinearRgb a, LinearRgb b) => new(a.R * b.R, a.G * b.G, a.B * b.B);
}

/// <summary>
/// How much air a ray of light crosses: metres of air at sea-level density, of haze at ground-level density and of
/// ozone at its peak density; <see cref="Blocked"/> when the ray runs into the ground instead of reaching space.
/// </summary>
public readonly record struct AirColumn(double Rayleigh, double Mie, double Ozone, bool Blocked = false)
{
    public static AirColumn operator +(AirColumn a, AirColumn b) =>
        new(a.Rayleigh + b.Rayleigh, a.Mie + b.Mie, a.Ozone + b.Ozone, a.Blocked || b.Blocked);
}

/// <summary>
/// The optics of the sky: sunlight scattered by the air's molecules (Rayleigh, as 1/λ⁴), by haze (Mie, nearly the same
/// for every colour) and absorbed a little by ozone, in a spherical atmosphere; the colour a spectrum looks; and the
/// Sun's path through one day.
/// </summary>
/// <remarks>
/// Light is worked out in <see cref="Wavelengths"/> bands from 400 to 700 nm, as a fraction of the sunlight in space, and
/// only turned into a colour at the end (CIE 1931 colour matching, white-balanced so the Sun in space is white).
/// Coefficients are the usual sea-level values: Rayleigh 5.8, 13.6 and 33.1 per 1000 km at 680, 550 and 440 nm with an
/// 8 km scale height; haze with a 1.2 km scale height. The day is an equinox at <see cref="Latitude"/>° north, so the
/// Sun rises in the east at 6:00, is 60° high at noon and sets in the west at 18:00 (local solar time).
/// </remarks>
public static class SkyOptics
{
    public const double EarthRadius = 6_371_000;
    public const double AtmosphereHeight = 100_000;
    public const double TopRadius = EarthRadius + AtmosphereHeight;
    public const double RayleighScaleHeight = 8_000;
    public const double MieScaleHeight = 1_200;

    /// <summary>Of the light haze takes out of a beam, the share it scatters; the rest it absorbs.</summary>
    public const double MieAlbedo = 0.9;

    /// <summary>How much haze scatters forward, for the Henyey–Greenstein phase function.</summary>
    public const double MieAsymmetry = 0.76;

    /// <summary>
    /// Where the day is worked out unless a latitude is given (degrees north), at an equinox. The page passes the
    /// shared observer location's latitude.
    /// </summary>
    public const double DefaultLatitude = 30;

    public const double FirstHour = 5;
    public const double LastHour = 19;

    /// <summary>The blue and red the page quotes figures for.</summary>
    public const double BlueNm = 450;

    public const double RedNm = 650;

    /// <summary>The centres of the 15 nm bands light is worked out in, 400…700 nm.</summary>
    public static readonly double[] Wavelengths = Enumerable.Range(0, 21).Select(i => 400.0 + (15.0 * i)).ToArray();

    public static int Bands => Wavelengths.Length;

    private const double Deg = Math.PI / 180;

    #region scattering and absorption

    /// <summary>Rayleigh scattering by the air's molecules at sea level, per metre: as 1/λ⁴.</summary>
    public static double Rayleigh(double nm) => 5.802e-6 * Math.Pow(680 / nm, 4);

    /// <summary>Scattering by haze at ground level, per metre, from very clean air (0) to thick haze (1).</summary>
    public static double Mie(double nm, double haze) => (1e-6 + (24e-6 * Math.Clamp(haze, 0, 1))) * Math.Pow(550 / nm, 0.8);

    /// <summary>Ozone's absorption at its peak density, per metre: the Chappuis band, strongest in orange light.</summary>
    public static double Ozone(double nm) => 2.8e-6 * Math.Exp(-Square((nm - 600) / 80));

    /// <summary>How much a column of air dims light of this wavelength: the light left is e to the minus this.</summary>
    public static double OpticalDepth(double nm, AirColumn column, double haze) =>
        (Rayleigh(nm) * column.Rayleigh) + (Mie(nm, haze) / MieAlbedo * column.Mie) + (Ozone(nm) * column.Ozone);

    /// <summary>The air's densities at a height in metres, relative to sea level (ozone: to its peak at 25 km).</summary>
    public static (double Rayleigh, double Mie, double Ozone) Density(double height) =>
        (Math.Exp(-height / RayleighScaleHeight), Math.Exp(-height / MieScaleHeight),
            Math.Max(0, 1 - (Math.Abs(height - 25_000) / 15_000)));

    /// <summary>Molecules scatter light forward and back twice as much as sideways.</summary>
    public static double RayleighPhase(double cosAngle) => 3 / (16 * Math.PI) * (1 + (cosAngle * cosAngle));

    /// <summary>Haze scatters mostly forward, which makes the bright glow round the Sun.</summary>
    public static double MiePhase(double cosAngle)
    {
        const double g = MieAsymmetry;
        var denominator = 1 + (g * g) - (2 * g * cosAngle);
        return (1 - (g * g)) / (4 * Math.PI * denominator * Math.Sqrt(denominator));
    }

    /// <summary>
    /// The air a ray crosses on its way out to space from a point <paramref name="height"/> metres up, going at
    /// <paramref name="cosZenith"/> from straight up; blocked if it runs into the ground first.
    /// </summary>
    public static AirColumn ColumnToSpace(double height, double cosZenith, int steps = 48)
    {
        var r = EarthRadius + Math.Clamp(height, 0, AtmosphereHeight);
        var mu = Math.Clamp(cosZenith, -1, 1);
        if (mu < 0 && r * r * (1 - (mu * mu)) < EarthRadius * EarthRadius) return new AirColumn(0, 0, 0, true);

        var length = (-r * mu) + Math.Sqrt(Math.Max(0, (r * r * ((mu * mu) - 1)) + (TopRadius * TopRadius)));
        return Integrate(r, mu, length, steps);
    }

    /// <summary>
    /// The air along a ray from a point at radius <paramref name="r"/> going at <paramref name="mu"/> from straight up,
    /// for <paramref name="length"/> metres. Steps are spaced closer near the start, where the air is densest.
    /// </summary>
    internal static AirColumn Integrate(double r, double mu, double length, int steps)
    {
        double rayleigh = 0, mie = 0, ozone = 0;
        for (var i = 0; i < steps; i++)
        {
            var u = (i + 0.5) / steps;
            var t = length * u * u;
            var dt = 2 * length * u / steps;
            var height = Math.Sqrt((r * r) + (t * t) + (2 * r * t * mu)) - EarthRadius;
            var (dr, dm, dz) = Density(height);
            rayleigh += dr * dt;
            mie += dm * dt;
            ozone += dz * dt;
        }

        return new AirColumn(rayleigh, mie, ozone);
    }

    /// <summary>
    /// How many times as much air as straight up the Sun's light crosses at this altitude (Kasten and Young, 1989):
    /// 1 overhead, 2 at 30°, about 38 on the horizon. At or below the horizon, the horizon's.
    /// </summary>
    public static double AirMass(double altitude)
    {
        var h = Math.Max(0, altitude);
        return 1 / (Math.Sin(h * Deg) + (0.50572 * Math.Pow(h + 6.07995, -1.6364)));
    }

    /// <summary>The share of sunlight in each band that comes straight through the air to the ground; 0 if the Sun is down.</summary>
    public static double[] Transmittance(double altitude, double haze)
    {
        var column = ColumnToSpace(0, Math.Sin(altitude * Deg));
        return Wavelengths.Select(nm => column.Blocked ? 0 : Math.Exp(-OpticalDepth(nm, column, haze))).ToArray();
    }

    /// <summary>The share of one wavelength that comes straight through to the ground.</summary>
    public static double TransmittanceAt(double nm, double altitude, double haze)
    {
        var column = ColumnToSpace(0, Math.Sin(altitude * Deg));
        return column.Blocked ? 0 : Math.Exp(-OpticalDepth(nm, column, haze));
    }

    #endregion

    #region colour

    /// <summary>Sunlight in space, a 5778 K black body, relative to its value at 550 nm.</summary>
    public static double SunSpectrum(double nm) => Planck(nm, 5778) / Planck(550, 5778);

    private static double Planck(double nm, double kelvin) => 1 / (Math.Pow(nm, 5) * (Math.Exp(1.4388e7 / (nm * kelvin)) - 1));

    /// <summary>The CIE 1931 colour matching functions, as the multi-lobe fit of Wyman, Sloan and Shirley (2013).</summary>
    public static (double X, double Y, double Z) ColourMatch(double nm)
    {
        static double G(double x, double mu, double below, double above)
        {
            var s = (x - mu) / (x < mu ? below : above);
            return Math.Exp(-0.5 * s * s);
        }

        var x = (1.056 * G(nm, 599.8, 37.9, 31.0)) + (0.362 * G(nm, 442.0, 16.0, 26.7)) - (0.065 * G(nm, 501.1, 20.4, 26.2));
        var y = (0.821 * G(nm, 568.8, 46.9, 40.5)) + (0.286 * G(nm, 530.9, 16.3, 31.1));
        var z = (1.217 * G(nm, 437.0, 11.8, 36.0)) + (0.681 * G(nm, 459.0, 26.0, 13.8));
        return (x, y, z);
    }

    internal static LinearRgb XyzToRgb(double x, double y, double z) => new(
        (3.2406 * x) - (1.5372 * y) - (0.4986 * z),
        (-0.9689 * x) + (1.8758 * y) + (0.0415 * z),
        (0.0557 * x) - (0.2040 * y) + (1.0570 * z));

    /// <summary>What sunlight in each band adds to a colour, scaled so that all of it together is white (1, 1, 1).</summary>
    public static readonly LinearRgb[] BandColours = MakeBandColours();

    private static LinearRgb[] MakeBandColours()
    {
        var raw = Wavelengths.Select(nm =>
        {
            var (x, y, z) = ColourMatch(nm);
            return XyzToRgb(x, y, z) * SunSpectrum(nm);
        }).ToArray();
        var white = raw.Aggregate(default(LinearRgb), (a, b) => a + b);
        return raw.Select(c => new LinearRgb(c.R / white.R, c.G / white.G, c.B / white.B)).ToArray();
    }

    /// <summary>The colour of light that is, in each band, this share of sunlight.</summary>
    public static LinearRgb ToRgb(IReadOnlyList<double> perBand)
    {
        double r = 0, g = 0, b = 0;
        for (var i = 0; i < BandColours.Length; i++)
        {
            r += perBand[i] * BandColours[i].R;
            g += perBand[i] * BandColours[i].G;
            b += perBand[i] * BandColours[i].B;
        }

        return new LinearRgb(r, g, b);
    }

    /// <summary>
    /// A light's colour on screen after the eye (or camera) adjusts by <paramref name="exposure"/>. Brightness is
    /// compressed as a whole so colours keep their hue; what is still too bright for the screen fades to white.
    /// </summary>
    public static Color Display(LinearRgb light, double exposure, byte alpha = 255, double saturation = 1)
    {
        var c = new LinearRgb(Math.Max(0, light.R), Math.Max(0, light.G), Math.Max(0, light.B));
        var y = c.Luminance;
        if (y <= 0) return Color.FromArgb(alpha, 0, 0, 0);

        var shown = 1 - Math.Exp(-y * exposure);
        c *= shown / y;
        if (saturation != 1)
        {
            c = new LinearRgb(Math.Max(0, shown + ((c.R - shown) * saturation)), Math.Max(0, shown + ((c.G - shown) * saturation)),
                Math.Max(0, shown + ((c.B - shown) * saturation)));
        }

        var max = c.Max;
        if (max > 1)
        {
            var white = 1 - (1 / max);
            c = (c * ((1 - white) / max)) + new LinearRgb(white, white, white);
        }

        return Color.FromArgb(alpha, Encode(c.R), Encode(c.G), Encode(c.B));
    }

    /// <summary>A light's colour at full brightness, whatever its strength: its brightest channel at 1.</summary>
    public static Color Hue(LinearRgb light, byte alpha = 255)
    {
        var c = new LinearRgb(Math.Max(0, light.R), Math.Max(0, light.G), Math.Max(0, light.B));
        var max = c.Max;
        if (max <= 0) return Color.FromArgb(alpha, 0, 0, 0);
        return Color.FromArgb(alpha, Encode(c.R / max), Encode(c.G / max), Encode(c.B / max));
    }

    /// <summary>The colour of pure light of one wavelength, as near as a screen gets.</summary>
    public static Color SpectralColour(double nm)
    {
        var (x, y, z) = ColourMatch(nm);
        var c = XyzToRgb(x, y, z);

        // Pure spectral colours lie outside what a screen shows: add white until no channel is negative.
        var lift = Math.Max(0, -Math.Min(c.R, Math.Min(c.G, c.B)));
        return Hue(new LinearRgb(c.R + lift, c.G + lift, c.B + lift));
    }

    /// <summary>sRGB gamma, from a table (whole skies are drawn with it).</summary>
    public static byte Encode(double linear) => GammaTable[(int)Math.Round(Math.Clamp(linear, 0, 1) * (GammaSteps - 1))];

    private const int GammaSteps = 4096;

    private static readonly byte[] GammaTable = Enumerable.Range(0, GammaSteps).Select(i =>
    {
        var v = i / (double)(GammaSteps - 1);
        v = v <= 0.0031308 ? 12.92 * v : (1.055 * Math.Pow(v, 1 / 2.4)) - 0.055;
        return (byte)Math.Round(v * 255);
    }).ToArray();

    /// <summary>How blue the Sun's light is against its red: blue's share coming through over red's.</summary>
    public static double BlueToRed(double altitude, double haze)
    {
        var red = TransmittanceAt(RedNm, altitude, haze);
        return red <= 0 ? 0 : TransmittanceAt(BlueNm, altitude, haze) / red;
    }

    #endregion

    #region the day

    /// <summary>The Sun's altitude in degrees at a local solar time, hours.</summary>
    public static double SunAltitude(double hour, double latitude = DefaultLatitude) =>
        Math.Asin(Math.Cos(latitude * Deg) * Math.Cos(HourAngle(hour))) / Deg;

    /// <summary>The Sun's azimuth in degrees from south, west positive: −90 at sunrise, 0 at noon, 90 at sunset.</summary>
    /// <remarks>South of the equator the Sun is north at noon (±180).</remarks>
    public static double SunAzimuth(double hour, double latitude = DefaultLatitude)
    {
        var h = HourAngle(hour);
        return Math.Atan2(Math.Sin(h), Math.Cos(h) * Math.Sin(latitude * Deg)) / Deg;
    }

    /// <summary>The highest the Sun gets, at noon.</summary>
    public static double NoonAltitude(double latitude = DefaultLatitude) => 90 - Math.Abs(latitude);

    /// <summary>The time the Sun is at this altitude, in the morning or the afternoon.</summary>
    public static double HourAt(double altitude, bool afternoon, double latitude = DefaultLatitude)
    {
        var cos = Math.Clamp(Math.Sin(Math.Clamp(altitude, -90, NoonAltitude(latitude)) * Deg) / Math.Cos(latitude * Deg), -1, 1);
        var hours = Math.Acos(cos) / Deg / 15;
        return Math.Clamp(afternoon ? 12 + hours : 12 - hours, FirstHour, LastHour);
    }

    private static double HourAngle(double hour) => (hour - 12) * 15 * Deg;

    /// <summary>The part of the day: golden hour below 6°, sunrise or sunset with the Sun on the horizon, then twilight.</summary>
    public static SkyPhase PhaseAt(double hour, double latitude = DefaultLatitude)
    {
        var altitude = SunAltitude(hour, latitude);
        return altitude switch
        {
            > 6 => SkyPhase.Day,
            > 0.5 => SkyPhase.GoldenHour,
            > -1 => hour < 12 ? SkyPhase.Sunrise : SkyPhase.Sunset,
            > -6 => SkyPhase.Twilight,
            > -12 => SkyPhase.NauticalTwilight,
            _ => SkyPhase.Night,
        };
    }

    #endregion

    private static double Square(double x) => x * x;
}
