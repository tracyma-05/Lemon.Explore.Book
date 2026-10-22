using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Typhoon;

/// <summary>
/// A typhoon's cloud seen from above, as a satellite would: the clear eye, the bright eyewall, the central dense
/// overcast and the spiral rain bands (curling anticlockwise in the north, clockwise in the south), ragged with noise.
/// Rendered into a bitmap once per storm (pressure in 5 hPa steps) and turned by the caller as it spins.
/// </summary>
internal sealed class StormCloud
{
    /// <summary>The bitmap's half-width covers this far from the centre, km.</summary>
    public const double ViewRadius = 600;

    private const int Size = 384;

    private readonly Dictionary<(int Pressure, bool South, bool Spin), Bitmap> _cache = [];

    /// <summary>The cloud of a storm with this central pressure.</summary>
    public Bitmap Get(double centralPressure, bool south, bool spin)
    {
        var key = ((int)(Math.Round(centralPressure / 5) * 5), south, spin);
        if (_cache.TryGetValue(key, out var bitmap)) return bitmap;
        if (_cache.Count > 12) _cache.Clear();
        return _cache[key] = Render(key.Item1, south, spin);
    }

    private static Bitmap Render(double pressure, bool south, bool spin)
    {
        var rmax = StormModel.RadiusOfMaxWind(pressure);
        var eye = StormModel.EyeRadius(pressure);
        var wind = StormModel.MaxWind(pressure);
        var organized = StormModel.Smooth(15, 45, wind);
        var eyeClear = StormModel.Smooth(30, 42, wind);
        var overcast = 70 + (3 * rmax) + (90 * organized);
        var hand = south ? -1 : 1;

        var pixels = new uint[Size * Size];
        var half = Size / 2.0;
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var dx = (x + 0.5 - half) / half * ViewRadius;
                var dy = -(y + 0.5 - half) / half * ViewRadius;
                var r = Math.Sqrt((dx * dx) + (dy * dy));
                if (r > ViewRadius) continue;
                var noise = Noise(dx / 70, dy / 70);
                double value;
                if (!spin)
                {
                    // Without the Earth's turn: loose clumps of thunderstorms over the low, no eye and no bands.
                    value = (1 - StormModel.Smooth(60, 240, r)) * StormModel.Smooth(0.35, 0.75, (noise * 0.9) + (Noise(dx / 25, dy / 25) * 0.35));
                }
                else
                {
                    var theta = Math.Atan2(dy, dx);
                    var wall = Math.Exp(-Math.Pow((r - (1.25 * rmax)) / ((0.55 * rmax) + 6), 2));
                    var core = (1 - StormModel.Smooth(0.55 * overcast, overcast, r)) * (0.55 + (0.45 * organized));
                    var phase = 3 * (theta + (hand * 3.2 * Math.Log(Math.Max(r, 1) / rmax)));
                    var band = Math.Pow(0.5 + (0.5 * Math.Cos(phase + (noise * 1.2))), 5)
                        * StormModel.Smooth(0.5 * overcast, 0.9 * overcast, r)
                        * (1 - StormModel.Smooth(0.55 * ViewRadius, 0.95 * ViewRadius, r))
                        * (0.25 + (0.75 * organized));
                    value = Math.Max(Math.Max(wall * (0.4 + (0.6 * organized)), core), band * (0.55 + (0.7 * noise)));
                    value *= 0.72 + (0.4 * noise);

                    // The eye: clear in a typhoon, cloudy in a weaker storm.
                    var inEye = 1 - StormModel.Smooth(0.75 * eye, 1.15 * eye, r);
                    value *= 1 - (inEye * eyeClear * 0.95);
                }

                value = Math.Clamp(value, 0, 1);
                if (value < 0.02) continue;

                // Thin cloud is grey-blue, thick cloud white; premultiplied alpha.
                var alpha = Math.Clamp(value * 1.15, 0, 1);
                var shade = 0.72 + (0.28 * value);
                var r8 = (byte)(alpha * 255 * shade * 0.97);
                var g8 = (byte)(alpha * 255 * shade * 0.98);
                var b8 = (byte)(alpha * 255 * Math.Min(1, shade * 1.03));
                pixels[(y * Size) + x] = ((uint)(alpha * 255) << 24) | ((uint)r8 << 16) | ((uint)g8 << 8) | b8;
            }
        }

        var bitmap = new WriteableBitmap(new PixelSize(Size, Size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        SpaceDrawing.WritePixels(bitmap, pixels, Size, Size);
        return bitmap;
    }

    /// <summary>Smooth value noise, three octaves, 0…1.</summary>
    private static double Noise(double x, double y)
    {
        double sum = 0, amplitude = 0.55, total = 0;
        for (var octave = 0; octave < 3; octave++)
        {
            sum += amplitude * ValueNoise(x, y);
            total += amplitude;
            x *= 2.1;
            y *= 2.1;
            amplitude *= 0.5;
        }

        return sum / total;
    }

    private static double ValueNoise(double x, double y)
    {
        var x0 = Math.Floor(x);
        var y0 = Math.Floor(y);
        var fx = x - x0;
        var fy = y - y0;
        fx = fx * fx * (3 - (2 * fx));
        fy = fy * fy * (3 - (2 * fy));
        var a = Hash(x0, y0);
        var b = Hash(x0 + 1, y0);
        var c = Hash(x0, y0 + 1);
        var d = Hash(x0 + 1, y0 + 1);
        return ((a + ((b - a) * fx)) * (1 - fy)) + ((c + ((d - c) * fx)) * fy);
    }

    private static double Hash(double x, double y)
    {
        var h = Math.Sin((x * 127.1) + (y * 311.7)) * 43758.5453;
        return h - Math.Floor(h);
    }
}
