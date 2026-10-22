using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Chapters.BlueSky;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Rainbow;

/// <summary>
/// How the sky view is laid out: pixels per degree (the same across and up), the horizon's height and the screen column
/// of the point opposite the Sun. Altitude and azimuth are drawn straight (an equirectangular view), so the horizon is
/// flat and the bows are very nearly circles.
/// </summary>
internal readonly record struct SkyView(double Scale, double HorizonY, double CentreX)
{
    public Point Project(double altitude, double azimuth) => new(CentreX + (azimuth * Scale), HorizonY - (altitude * Scale));

    /// <summary>
    /// The point <paramref name="radius"/> degrees from the point opposite the Sun, at <paramref name="around"/> degrees
    /// round it (90 straight up), as altitude and azimuth.
    /// </summary>
    public static (double Altitude, double Azimuth) OnCircle(double radius, double around, double sunAltitude)
    {
        const double deg = Math.PI / 180;
        var h = sunAltitude * deg;
        var r = radius * deg;
        var phi = around * deg;

        // The antisolar direction A = (cos h, 0, −sin h); "up" round it u = (sin h, 0, cos h); "right" v = (0, 1, 0).
        var x = (Math.Cos(h) * Math.Cos(r)) + (Math.Sin(h) * Math.Sin(phi) * Math.Sin(r));
        var y = Math.Cos(phi) * Math.Sin(r);
        var z = (-Math.Sin(h) * Math.Cos(r)) + (Math.Cos(h) * Math.Sin(phi) * Math.Sin(r));
        return (Math.Asin(Math.Clamp(z, -1, 1)) / deg, Math.Atan2(y, x) / deg);
    }
}

/// <summary>
/// The rainy sky opposite the Sun, worked out pixel by pixel: dark cloud and a landscape, and the light the rain sends
/// back by its angle from the point opposite the Sun (<see cref="RainbowOptics.BowColours"/>), coloured by the sunlight
/// reaching the rain. Rendered at half resolution and only again when the Sun, the drops or the view change.
/// </summary>
internal sealed class BowSkyRenderer
{
    /// <summary>Screen pixels per rendered pixel, each way.</summary>
    public const int Pixel = 2;

    /// <summary>How much brighter the eye takes the scene than its light: a rain sky is dim.</summary>
    private const double Exposure = 2.2;

    /// <summary>How much of the rain's light is in front of the land (the drops between you and the hills).</summary>
    private const double RainOverLand = 0.3;

    private const double Deg = Math.PI / 180;

    /// <summary>Scales the rain's light so the primary bow of 1 mm drops under a high Sun is bright but not white.</summary>
    private static readonly Lazy<double> Brightness = new(() =>
    {
        var bow = RainbowOptics.BowColours(DropSize.Rain, false);
        var peak = bow.Skip(38 * RainbowOptics.ProfileSteps).Take(6 * RainbowOptics.ProfileSteps).Max(c => c.Luminance);
        return 0.3 / peak;
    });

    private WriteableBitmap? _bitmap;
    private int[] _pixels = [];
    private object? _key;

    private LinearRgb[] _bow = [];
    private object? _bowKey;

    /// <summary>The sky for this view (in screen pixels), or the last one if nothing has changed.</summary>
    public IImage Render(double width, double height, SkyView view, double sunAltitude, DropSize size, bool secondary)
    {
        var w = Math.Max(1, (int)Math.Ceiling(width / Pixel));
        var h = Math.Max(1, (int)Math.Ceiling(height / Pixel));
        var key = (w, h, view, Math.Round(sunAltitude, 2), size, secondary);
        if (_bitmap is not null && Equals(key, _key)) return _bitmap;
        _key = key;

        // The sunlight reaching the rain: less, and redder, with the Sun low.
        var sun = Math.Max(0.5, sunAltitude);
        var bowKey = (Math.Round(sun, 1), size, secondary);
        if (!Equals(bowKey, _bowKey))
        {
            _bowKey = bowKey;
            // The eye makes up for part of the dimming at sunset (up to 4×), not for the reddening.
            var sunlight = SkyOptics.Transmittance(sun, 0.3);
            var level = SkyOptics.ToRgb(sunlight).Luminance;
            var adapt = Math.Clamp(0.45 / Math.Max(level, 1e-6), 1, 4);
            _bow = RainbowOptics.BowColours(size, secondary, sunlight.Select(t => t * adapt).ToArray());
        }

        if (_bitmap is null || _bitmap.PixelSize.Width != w || _bitmap.PixelSize.Height != h)
        {
            _bitmap = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            _pixels = new int[w * h];
        }

        var sunlit = Math.Clamp(SkyOptics.TransmittanceAt(560, sun, 0.3) / 0.8, 0.2, 1);
        var warm = Math.Clamp(1 - (sunAltitude / 25), 0, 1);
        var brightness = Brightness.Value;
        var cosH = Math.Cos(sunAltitude * Deg);
        var sinH = Math.Sin(sunAltitude * Deg);

        var cosAz = new double[w];
        var hills = new double[w];
        for (var x = 0; x < w; x++)
        {
            var azimuth = (((x + 0.5) * Pixel) - view.CentreX) / view.Scale;
            cosAz[x] = Math.Cos(Math.Clamp(azimuth, -179, 179) * Deg);
            hills[x] = HillAltitude(azimuth);
        }

        var bow = _bow;
        var pixels = _pixels;
        var lastBin = bow.Length - 1;
        Parallel.For(0, h, y =>
        {
            var altitude = (view.HorizonY - ((y + 0.5) * Pixel)) / view.Scale;
            var cosAlt = Math.Cos(altitude * Deg) * cosH;
            var sinAlt = Math.Sin(altitude * Deg) * sinH;
            var cloud = Cloud(altitude, sunlit, warm);
            var ground = Ground(altitude, sunlit, warm);
            var row = y * w;
            for (var x = 0; x < w; x++)
            {
                // How much of the pixel is land, so the hills' outline is smooth.
                var land = Math.Clamp(((hills[x] - altitude) * view.Scale / Pixel) + 0.5, 0, 1);
                var light = land <= 0 ? cloud : land >= 1 ? ground : (cloud * (1 - land)) + (ground * land);

                var index = Math.Min(lastBin, AngleBin((cosAlt * cosAz[x]) - sinAlt));
                light += bow[index] * (brightness * (1 - (land * (1 - RainOverLand))));
                pixels[row + x] = Shade(light);
            }
        });

        SpaceDrawing.WritePixels(_bitmap, _pixels, w, h);
        return _bitmap;
    }

    /// <summary>Steps of the table from the cosine of an angle to its bin in a bow profile.</summary>
    private const int CosineSteps = 1 << 16;

    /// <summary>
    /// The bow profile's bin for each cosine of the angle from the point opposite the Sun: an acos per pixel is the
    /// slowest part of a frame. Fine enough where it matters (0.003° near the bows); coarse only right at the centre.
    /// </summary>
    private static readonly int[] CosineBins = Enumerable.Range(0, CosineSteps + 1)
        .Select(i => (int)(Math.Acos(Math.Clamp((2.0 * i / CosineSteps) - 1, -1, 1)) / Deg * RainbowOptics.ProfileSteps))
        .ToArray();

    private static int AngleBin(double cosine) => CosineBins[(int)((Math.Clamp(cosine, -1, 1) + 1) * 0.5 * CosineSteps)];

    /// <summary>1 − e^(−x) for x from 0 to <see cref="ExposureReach"/>, the eye's compression of brightness.</summary>
    private const double ExposureReach = 12;

    private const int ExposureSteps = 4096;

    private static readonly double[] ExposureCurve = Enumerable.Range(0, ExposureSteps + 1)
        .Select(i => 1 - Math.Exp(-(double)i / ExposureSteps * ExposureReach)).ToArray();

    /// <summary>
    /// <see cref="SkyOptics.Display"/> with <see cref="Exposure"/>, from tables: brightness compressed as a whole so colours
    /// keep their hue, and what is still too bright fading to white. Packed as BGR32.
    /// </summary>
    private static int Shade(LinearRgb light)
    {
        var r = Math.Max(0, light.R);
        var g = Math.Max(0, light.G);
        var b = Math.Max(0, light.B);
        var y = (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
        if (y <= 0) return 0;

        var shown = ExposureCurve[(int)(Math.Min(y * Exposure, ExposureReach) / ExposureReach * ExposureSteps)];
        var k = shown / y;
        r *= k;
        g *= k;
        b *= k;
        var max = Math.Max(r, Math.Max(g, b));
        if (max > 1)
        {
            var white = 1 - (1 / max);
            var keep = (1 - white) / max;
            r = (r * keep) + white;
            g = (g * keep) + white;
            b = (b * keep) + white;
        }

        return (SkyOptics.Encode(r) << 16) | (SkyOptics.Encode(g) << 8) | SkyOptics.Encode(b);
    }

    /// <summary>Low hills along the horizon, degrees high.</summary>
    internal static double HillAltitude(double azimuth) =>
        Math.Max(0, 0.7 + (0.9 * Math.Sin((azimuth * 0.07) + 1.3)) + (0.45 * Math.Sin((azimuth * 0.21) + 0.4)) + (0.2 * Math.Sin(azimuth * 0.53)));

    /// <summary>Rain cloud: dark overhead, lighter toward the horizon, warmer with the Sun low.</summary>
    private static LinearRgb Cloud(double altitude, double sunlit, double warm)
    {
        var t = Math.Clamp(altitude / 50, 0, 1);
        var level = (0.4 + (0.6 * sunlit)) * (1 - (0.6 * t));
        var tint = new LinearRgb(0.05 + (0.025 * warm), 0.055 + (0.008 * warm), 0.068 - (0.012 * warm));
        return tint * level;
    }

    /// <summary>Wet land in the low sunlight, darker nearer.</summary>
    private static LinearRgb Ground(double altitude, double sunlit, double warm)
    {
        var t = Math.Clamp(-altitude / 25, 0, 1);
        var level = (0.35 + (0.65 * sunlit)) * (1 - (0.65 * t));
        return new LinearRgb(0.018 + (0.012 * warm), 0.03 + (0.006 * warm), 0.014) * level;
    }
}
