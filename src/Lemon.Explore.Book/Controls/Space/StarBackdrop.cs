using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Lemon.Explore.Book.Controls.Space;

/// <summary>The background stars of a view, regenerated (after the size settles) when the view is resized.</summary>
internal sealed class StarBackdrop
{
    private WriteableBitmap? _stars;
    private WriteableBitmap? _nebula;
    private Starfield.Twinkle[] _twinkles = [];
    private PixelSize _pixelSize;
    private double _sizeChangedAt = double.NegativeInfinity;

    public void OnSizeChanged(double now) => _sizeChangedAt = now;

    /// <param name="scale">Device pixels per DIP.</param>
    public void Draw(DrawingContext dc, double width, double height, double scale, double now, double opacity = 1)
    {
        var pixelSize = new PixelSize((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale));
        if (pixelSize != _pixelSize && (_stars is null || now - _sizeChangedAt > 0.3))
        {
            _pixelSize = pixelSize;
            _stars?.Dispose();
            _nebula?.Dispose();
            (_stars, _nebula, _twinkles) = Starfield.Create(pixelSize.Width, pixelSize.Height);
        }

        var rect = new Rect(0, 0, width, height);
        using (dc.PushOpacity(opacity))
        {
            if (_nebula is not null)
            {
                dc.DrawImage(_nebula, rect);
            }

            if (_stars is not null)
            {
                dc.DrawImage(_stars, rect);
            }

            foreach (var twinkle in _twinkles)
            {
                using (dc.PushOpacity(0.35 + (0.65 * (0.5 + (0.5 * Math.Sin((now * twinkle.Speed) + twinkle.Phase))))))
                {
                    dc.DrawEllipse(TwinkleBrush, null, new Point(twinkle.X * width, twinkle.Y * height), twinkle.Size, twinkle.Size);
                }
            }
        }
    }

    private static readonly IBrush TwinkleBrush = SpaceDrawing.RadialGradient(
        (Color.FromArgb(255, 255, 255, 255), 0),
        (Color.FromArgb(90, 200, 220, 255), 0.35),
        (Color.FromArgb(0, 200, 220, 255), 1));
}

/// <summary>Background sky: a faint Milky Way band (low resolution, scaled up) and a sharp star layer.</summary>
internal static class Starfield
{
    internal readonly record struct Twinkle(double X, double Y, double Size, double Phase, double Speed);

    public static (WriteableBitmap Stars, WriteableBitmap Nebula, Twinkle[] Twinkles) Create(int width, int height)
    {
        width = Math.Max(width, 8);
        height = Math.Max(height, 8);
        var random = new Random(20191214);

        var stars = new float[width * height * 3];
        var count = width * height / 650;
        for (var i = 0; i < count; i++)
        {
            var x = random.NextDouble() * width;
            var y = random.NextDouble() * height;
            var brightness = 0.12 + (0.88 * Math.Pow(random.NextDouble(), 3.2));
            var (r, g, b) = random.Next(10) switch
            {
                0 or 1 => (0.78, 0.86, 1.0),
                2 => (1.0, 0.90, 0.76),
                3 => (1.0, 0.80, 0.62),
                _ => (1.0, 0.98, 0.96),
            };

            var radius = brightness > 0.55 ? 1.6 : 0.8;
            var x0 = (int)Math.Floor(x - radius);
            var y0 = (int)Math.Floor(y - radius);
            for (var py = y0; py <= y + radius; py++)
            {
                for (var px = x0; px <= x + radius; px++)
                {
                    if (px < 0 || py < 0 || px >= width || py >= height)
                    {
                        continue;
                    }

                    var d = Math.Sqrt(Math.Pow(px + 0.5 - x, 2) + Math.Pow(py + 0.5 - y, 2));
                    var falloff = Math.Exp(-(d * d) / (radius * 0.45));
                    var k = ((py * width) + px) * 3;
                    stars[k] += (float)(r * brightness * falloff);
                    stars[k + 1] += (float)(g * brightness * falloff);
                    stars[k + 2] += (float)(b * brightness * falloff);
                }
            }
        }

        var starPixels = new uint[width * height];
        for (var i = 0; i < starPixels.Length; i++)
        {
            var r = Math.Min(1, stars[i * 3]);
            var g = Math.Min(1, stars[(i * 3) + 1]);
            var b = Math.Min(1, stars[(i * 3) + 2]);
            var a = Math.Max(r, Math.Max(g, b));
            starPixels[i] = a <= 0.002f ? 0 : Pack(r, g, b, a);
        }

        // Milky Way: a soft diagonal band of coloured noise with darker dust lanes.
        var nw = Math.Max(width / 4, 2);
        var nh = Math.Max(height / 4, 2);
        var nebulaPixels = new uint[nw * nh];
        for (var j = 0; j < nh; j++)
        {
            for (var i = 0; i < nw; i++)
            {
                var u = i / (double)nw;
                var v = j / (double)nh;
                var aspect = nw / (double)nh;
                var p = new V3(u * aspect * 3, v * 3, 0.5);
                var distance = Math.Abs((v - 0.78) + (u * 0.62)) / Math.Sqrt(1 + (0.62 * 0.62));
                var band = Math.Exp(-Math.Pow(distance / 0.17, 2));
                var cloud = 0.55 + Noise.Fbm(p, 6);
                var dust = Noise.SmoothStep(0.05, 0.3, Noise.Fbm((p * 2.2) + new V3(3, 1, 7), 5));
                var density = Math.Clamp(band * cloud * (1 - (0.65 * dust)), 0, 1);
                var hue = Noise.SmoothStep(-0.3, 0.3, Noise.Fbm((p * 0.7) + new V3(9, 2, 4), 3));
                var r = 0.38 + (0.30 * hue);
                var g = 0.30 + (0.12 * hue);
                var b = 0.62 - (0.25 * hue);
                var a = (float)(density * 0.20);
                nebulaPixels[(j * nw) + i] = Pack((float)r * a, (float)g * a, (float)b * a, a, premultiplied: true);
            }
        }

        var twinkles = new Twinkle[40];
        for (var i = 0; i < twinkles.Length; i++)
        {
            twinkles[i] = new Twinkle(
                random.NextDouble(), random.NextDouble(), 1.4 + (random.NextDouble() * 1.6),
                random.NextDouble() * 2 * Math.PI, 0.8 + (random.NextDouble() * 2.2));
        }

        var starBitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        SpaceDrawing.WritePixels(starBitmap, starPixels, width, height);
        var nebulaBitmap = new WriteableBitmap(new PixelSize(nw, nh), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        SpaceDrawing.WritePixels(nebulaBitmap, nebulaPixels, nw, nh);
        return (starBitmap, nebulaBitmap, twinkles);
    }

    private static uint Pack(float r, float g, float b, float a, bool premultiplied = false)
    {
        // Stars are additive light: use the brightest channel as coverage and keep the colour premultiplied.
        if (!premultiplied)
        {
            (r, g, b) = (Math.Min(r, a), Math.Min(g, a), Math.Min(b, a));
        }

        static uint C(float v) => (uint)Math.Clamp((int)((v * 255) + 0.5f), 0, 255);
        return (C(a) << 24) | (C(r) << 16) | (C(g) << 8) | C(b);
    }
}
