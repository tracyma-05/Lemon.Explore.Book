using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.BlackHole;

/// <summary>
/// The lensing view's sky, pixel by pixel: each pixel shows the point of the source picture its light really comes from,
/// β = θ − θE²·θ/|θ|² round the lens (<see cref="BlackHoleModel.LensSourceOf"/>). Light from a point is spread over a
/// bigger patch near the lens, so what is there looks brighter without anything added: brightness per patch is kept,
/// as it is in real lensing.
/// </summary>
/// <remarks>
/// Coordinates are "view units": the view is 2 units tall, the source sits at 0, 0. Behind everything is an endless field
/// of faint far galaxies (one tile repeated, so light bent from far away still finds sky); in front of it the source: a
/// spiral galaxy or a star (a picture 2 units each way), or an endless grid coloured by quarter.
/// </remarks>
internal sealed class LensRenderer
{
    private const int TexelsPerUnit = 300;

    /// <summary>The background tile's size, view units (it repeats).</summary>
    private const double FieldTile = 2;

    /// <summary>Half the size of the source picture, view units.</summary>
    private const double SourceHalf = 1;

    /// <summary>The grid's spacing, view units.</summary>
    private const double GridStep = 0.2;

    /// <summary>The radius of the source star (view units), the size its light is drawn with.</summary>
    public const double StarRadius = 0.035;

    private static readonly int FieldSize = (int)(FieldTile * TexelsPerUnit);
    private static readonly int SourceSize = (int)(2 * SourceHalf * TexelsPerUnit);
    private static readonly int GridSize = (int)(GridStep * TexelsPerUnit);

    private static readonly Lazy<float[]> Field = new(MakeField, isThreadSafe: true);
    private static readonly Lazy<float[]> Galaxy = new(() => MakeSource(LensSource.Galaxy), isThreadSafe: true);
    private static readonly Lazy<float[]> Star = new(() => MakeSource(LensSource.Star), isThreadSafe: true);
    private static readonly Lazy<float[]> GridTile = new(MakeGrid, isThreadSafe: true);

    private WriteableBitmap? _bitmap;
    private int[] _pixels = [];
    private object? _key;

    /// <summary>
    /// The lensed sky: <paramref name="width"/> × <paramref name="height"/> render pixels, the view's centre at
    /// <paramref name="origin"/> (render pixels) with <paramref name="unitsPerPixel"/>, the lens at <paramref name="lens"/>
    /// and the Einstein radius <paramref name="einstein"/> (view units); unlensed if <paramref name="lensOn"/> is false.
    /// </summary>
    public IImage Render(int width, int height, Point origin, double unitsPerPixel, LensSource source, Point lens, double einstein, bool lensOn)
    {
        var key = (width, height, Math.Round(origin.X, 2), Math.Round(origin.Y, 2), Math.Round(unitsPerPixel, 6), source, Math.Round(lens.X, 4), Math.Round(lens.Y, 4), Math.Round(einstein, 4), lensOn);
        if (_bitmap is not null && Equals(key, _key)) return _bitmap;
        _key = key;
        if (_bitmap is null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height)
        {
            _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            _pixels = new int[width * height];
        }

        var field = Field.Value;
        var main = source switch
        {
            LensSource.Star => Star.Value,
            LensSource.Grid => GridTile.Value,
            _ => Galaxy.Value,
        };
        var e2 = einstein * einstein;
        Parallel.For(0, height, py =>
        {
            var y = (origin.Y - (py + 0.5)) * unitsPerPixel;
            for (var px = 0; px < width; px++)
            {
                var x = (px + 0.5 - origin.X) * unitsPerPixel;
                double sx = x, sy = y;
                if (lensOn)
                {
                    var dx = x - lens.X;
                    var dy = y - lens.Y;
                    var d2 = (dx * dx) + (dy * dy);
                    var k = d2 < 1e-12 ? 1e6 : 1 - (e2 / d2);
                    sx = lens.X + (dx * k);
                    sy = lens.Y + (dy * k);
                }

                // Very close to the lens the light comes from very far off: the field repeats, the source does not.
                var (r, g, b) = Bilinear(field, FieldSize, FieldSize, Wrap(sx, FieldTile) * TexelsPerUnit, Wrap(-sy, FieldTile) * TexelsPerUnit, wrap: true);
                if (source == LensSource.Grid)
                {
                    var (line, _, _) = Bilinear(main, GridSize, GridSize, Wrap(sx, GridStep) * TexelsPerUnit, Wrap(-sy, GridStep) * TexelsPerUnit, wrap: true);
                    if (line > 0 && Math.Abs(sx) < 50 && Math.Abs(sy) < 50)
                    {
                        var (tr, tg, tb) = (sx >= 0, sy >= 0) switch
                        {
                            (true, true) => (0.45, 0.85, 1.0),
                            (false, true) => (1.0, 0.8, 0.4),
                            (false, false) => (0.5, 1.0, 0.6),
                            _ => (1.0, 0.5, 0.75),
                        };
                        r += line * tr;
                        g += line * tg;
                        b += line * tb;
                    }
                }
                else if (Math.Abs(sx) < SourceHalf && Math.Abs(sy) < SourceHalf)
                {
                    var (mr, mg, mb) = Bilinear(main, SourceSize, SourceSize, (sx + SourceHalf) * TexelsPerUnit, (SourceHalf - sy) * TexelsPerUnit, wrap: false);
                    r += mr;
                    g += mg;
                    b += mb;
                }

                _pixels[(py * width) + px] = Pack(r, g, b);
            }
        });

        SpaceDrawing.WritePixels(_bitmap, _pixels, width, height);
        return _bitmap;
    }

    private static double Wrap(double v, double period)
    {
        var w = v % period;
        return w < 0 ? w + period : w;
    }

    /// <summary>Linear light at a texel position, blended from the four texels round it.</summary>
    private static (double R, double G, double B) Bilinear(float[] texture, int w, int h, double tx, double ty, bool wrap)
    {
        tx -= 0.5;
        ty -= 0.5;
        var x0 = (int)Math.Floor(tx);
        var y0 = (int)Math.Floor(ty);
        var fx = tx - x0;
        var fy = ty - y0;
        int X(int x) => wrap ? ((x % w) + w) % w : Math.Clamp(x, 0, w - 1);
        int Y(int y) => wrap ? ((y % h) + h) % h : Math.Clamp(y, 0, h - 1);
        var i00 = ((Y(y0) * w) + X(x0)) * 3;
        var i10 = ((Y(y0) * w) + X(x0 + 1)) * 3;
        var i01 = ((Y(y0 + 1) * w) + X(x0)) * 3;
        var i11 = ((Y(y0 + 1) * w) + X(x0 + 1)) * 3;
        double C(int k) =>
            (texture[i00 + k] * (1 - fx) * (1 - fy)) + (texture[i10 + k] * fx * (1 - fy)) +
            (texture[i01 + k] * (1 - fx) * fy) + (texture[i11 + k] * fx * fy);
        return (C(0), C(1), C(2));
    }

    private static int Pack(double r, double g, double b)
    {
        static int Channel(double v, int floor) => Math.Min(255, floor + (int)(255 * Math.Pow(Math.Max(0, v), 0.85)));
        return unchecked((int)0xFF000000) | (Channel(r, 4) << 16) | (Channel(g, 5) << 8) | Channel(b, 11);
    }

    /// <summary>Faint far galaxies on a tile that repeats seamlessly (seeded, invented).</summary>
    private static float[] MakeField()
    {
        var light = new float[FieldSize * FieldSize * 3];
        var random = new Random(11);
        for (var n = 0; n < 260; n++)
        {
            var cx = random.NextDouble() * FieldTile;
            var cy = random.NextDouble() * FieldTile;
            var size = 0.006 + (Math.Pow(random.NextDouble(), 3) * 0.02);
            var bright = 0.15 + (Math.Pow(random.NextDouble(), 4) * 0.8);
            var warm = random.NextDouble();
            var (r, g, b) = warm < 0.4 ? (1.0, 0.85, 0.65) : (0.75, 0.85, 1.0);
            var elongation = 1 + (random.NextDouble() * 1.5);
            var angle = random.NextDouble() * Math.PI;
            var (c, s) = (Math.Cos(angle), Math.Sin(angle));
            var reach = size * 3 * elongation;
            for (var ty = (int)((cy - reach) * TexelsPerUnit); ty <= (int)((cy + reach) * TexelsPerUnit); ty++)
            {
                for (var tx = (int)((cx - reach) * TexelsPerUnit); tx <= (int)((cx + reach) * TexelsPerUnit); tx++)
                {
                    var x = ((tx + 0.5) / TexelsPerUnit) - cx;
                    var y = ((ty + 0.5) / TexelsPerUnit) - cy;
                    var u = ((x * c) + (y * s)) / elongation;
                    var v = (-x * s) + (y * c);
                    var d2 = ((u * u) + (v * v)) / (size * size);
                    if (d2 > 9) continue;
                    var f = Math.Exp(-d2) * bright;
                    var i = ((((ty % FieldSize) + FieldSize) % FieldSize * FieldSize) + (((tx % FieldSize) + FieldSize) % FieldSize)) * 3;
                    light[i] += (float)(r * f);
                    light[i + 1] += (float)(g * f);
                    light[i + 2] += (float)(b * f);
                }
            }
        }

        return light;
    }

    /// <summary>One cell of the grid: a thin line along its edges (brightness only; the colour comes from the quarter).</summary>
    private static float[] MakeGrid()
    {
        var light = new float[GridSize * GridSize * 3];
        for (var ty = 0; ty < GridSize; ty++)
        {
            for (var tx = 0; tx < GridSize; tx++)
            {
                var gx = Math.Min(tx + 0.5, GridSize - tx - 0.5);
                var gy = Math.Min(ty + 0.5, GridSize - ty - 0.5);
                var line = Math.Max(Math.Clamp(1.8 - gx, 0, 1), Math.Clamp(1.8 - gy, 0, 1));
                light[((ty * GridSize) + tx) * 3] = (float)(0.5 * line);
            }
        }

        return light;
    }

    /// <summary>The source picture 2 units across: a spiral galaxy, or a single star.</summary>
    private static float[] MakeSource(LensSource source)
    {
        var light = new float[SourceSize * SourceSize * 3];
        var mid = SourceSize / 2.0;
        if (source == LensSource.Star)
        {
            for (var ty = 0; ty < SourceSize; ty++)
            {
                for (var tx = 0; tx < SourceSize; tx++)
                {
                    var x = (tx + 0.5 - mid) / TexelsPerUnit;
                    var y = (ty + 0.5 - mid) / TexelsPerUnit;
                    var d2 = (x * x) + (y * y);
                    var core = Math.Exp(-d2 / (StarRadius * StarRadius));
                    var glow = Math.Exp(-d2 / (StarRadius * StarRadius * 6));
                    var i = ((ty * SourceSize) + tx) * 3;
                    light[i] = (float)((2.2 * core) + (0.35 * glow));
                    light[i + 1] = (float)((2.1 * core) + (0.38 * glow));
                    light[i + 2] = (float)((1.9 * core) + (0.5 * glow));
                }
            }

            return light;
        }

        // A face-on spiral galaxy 0.42 units in scale: a yellow core, two blue arms with pink star-forming knots.
        const double size = 0.42;
        var knots = new Random(3);
        var knotList = Enumerable.Range(0, 40).Select(_ => (A: knots.NextDouble() * Math.PI * 2, R: 0.25 + (knots.NextDouble() * 0.7))).ToList();
        for (var ty = 0; ty < SourceSize; ty++)
        {
            for (var tx = 0; tx < SourceSize; tx++)
            {
                var x = (tx + 0.5 - mid) / TexelsPerUnit / size;
                var y = -(ty + 0.5 - mid) / TexelsPerUnit / size;

                // Slightly tilted, so it is an oval; it fades out smoothly at its edge.
                var yy = y / 0.72;
                var rr = Math.Sqrt((x * x) + (yy * yy));
                if (rr > 1.3) continue;
                var edge = Math.Clamp((1.3 - rr) / 0.5, 0, 1);
                edge *= edge;
                var angle = Math.Atan2(yy, x);
                var arm = Math.Pow(0.5 + (0.5 * Math.Cos((2 * angle) - (5.5 * Math.Log(rr + 0.08)))), 3);
                var disk = Math.Exp(-rr / 0.26) * edge;
                var core = Math.Exp(-(rr * rr) / 0.012);
                var f = (disk * (0.2 + (1.1 * arm))) + (1.6 * core);
                var blue = disk * arm;
                var i = ((ty * SourceSize) + tx) * 3;
                light[i] = (float)((f * 0.9) + (core * 0.3));
                light[i + 1] = (float)(f * 0.82);
                light[i + 2] = (float)((f * 0.7) + (blue * 0.9));
                foreach (var (a, r) in knotList)
                {
                    var kx = (r * Math.Cos(a)) - x;
                    var ky = (r * Math.Sin(a)) - yy;
                    var k = Math.Exp(-((kx * kx) + (ky * ky)) / 0.0012) * arm * edge;
                    if (k <= 0.01) continue;
                    light[i] += (float)(k * 1.2);
                    light[i + 1] += (float)(k * 0.5);
                    light[i + 2] += (float)(k * 0.9);
                }
            }
        }

        return light;
    }
}
