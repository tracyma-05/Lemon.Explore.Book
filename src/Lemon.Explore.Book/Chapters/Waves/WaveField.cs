using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Waves;

/// <summary>Where a wave comes from: a point (a dipper, a slit) sending out rings, or a straight front coming from the left.</summary>
internal enum EmitterKind
{
    Point,
    Plane,
}

/// <summary>
/// A source of waves for <see cref="WaveField"/>, in world units (y down). A plane wave starts at <see cref="Position"/>.X
/// and runs right. The wave only exists between <see cref="MinX"/> and <see cref="MaxX"/> (the slits shine only past the
/// barrier, the laser's light only up to it) and from <see cref="Start"/>: its front spreads at the wave's speed from then.
/// </summary>
internal readonly record struct Emitter(EmitterKind Kind, Point Position, double Phase = 0, double Start = double.NegativeInfinity,
    double MinX = double.NegativeInfinity, double MaxX = double.PositiveInfinity);

/// <summary>
/// The surface of a ripple tank, or the light past two slits, painted pixel by pixel: every source's wave added up at each
/// point. Per pixel and source the distance r and A·cos kr, A·sin kr are worked out once, so a frame only needs
/// sin ωt·C − cos ωt·S per source. Drawn at one cell per <see cref="Cell"/> DIPs and stretched smoothly.
/// </summary>
internal sealed class WaveField
{
    /// <summary>DIPs per cell of the field.</summary>
    public const double Cell = 2;

    private readonly record struct Shape(int Columns, int Rows, Rect Area, Point Origin, double Scale, double Wavelength,
        string Emitters);

    /// <summary>Entries of the soft-limit table: heights times gain from −<see cref="Reach"/> to +<see cref="Reach"/>.</summary>
    private const int Steps = 2048;

    private const double Reach = 3;

    private Shape _shape;
    private float[][] _distance = [];
    private float[][] _cos = [];
    private float[][] _sin = [];
    private double[] _farthest = [];
    private float[] _re = [];
    private float[] _im = [];
    private uint[] _pixels = [];

    /// <summary>The colours by height for the palette last used: tanh's soft limit worked in, one entry per step.</summary>
    private (uint[] Palette, uint[] Table)? _table;

    /// <summary>What the bitmap shows now; nothing is painted again while it stays the same.</summary>
    private string _painted = string.Empty;

    public WriteableBitmap? Bitmap { get; private set; }

    /// <summary>
    /// Paints the field over <paramref name="area"/> (DIPs) at <paramref name="seconds"/>. <paramref name="origin"/> is where
    /// world (0, 0) is drawn, <paramref name="scale"/> DIPs per world unit. Each value of the height (−1 to 1 after
    /// <paramref name="gain"/> and a soft limit) picks a colour from <paramref name="palette"/>; with
    /// <paramref name="average"/>, how far the water rises and falls there picks from <paramref name="averagePalette"/>.
    /// </summary>
    public void Render(Rect area, Point origin, double scale, double wavelength, double speed, double seconds,
        IReadOnlyList<Emitter> emitters, uint[] palette, double gain, bool average = false, uint[]? averagePalette = null)
    {
        var columns = Math.Max(1, (int)Math.Ceiling(area.Width / Cell));
        var rows = Math.Max(1, (int)Math.Ceiling(area.Height / Cell));
        var shape = new Shape(columns, rows, area, origin, scale, wavelength,
            string.Join(";", emitters.Select(e => $"{e.Kind},{e.Position.X:R},{e.Position.Y:R},{e.MinX:R},{e.MaxX:R}")));
        if (shape != _shape || Bitmap is null)
        {
            Build(shape, emitters);
            _painted = string.Empty;
        }

        var omega = 2 * Math.PI * speed / wavelength;
        var band = (float)(0.6 * wavelength);
        var count = emitters.Count;
        var fronts = new double[count];
        var settled = true;
        for (var e = 0; e < count; e++)
        {
            fronts[e] = double.IsNegativeInfinity(emitters[e].Start) ? double.PositiveInfinity : speed * (seconds - emitters[e].Start);
            settled &= fronts[e] > _farthest[e] + band;
        }

        // The swing does not change once every front is past the whole field: paint it once.
        var key = average
            ? $"avg|{gain:R}|{string.Join(",", emitters.Select(e => e.Phase.ToString("R")))}|" +
              (settled ? "settled" : string.Join(",", fronts.Select(f => f.ToString("R"))))
            : $"now|{gain:R}|{seconds:R}|{palette.GetHashCode()}|{string.Join(",", emitters.Select(e => $"{e.Phase:R}/{e.Start:R}"))}";
        if (key == _painted) return;
        _painted = key;

        var n = _pixels.Length;
        Array.Clear(_re, 0, n);
        if (average) Array.Clear(_im, 0, n);
        for (var e = 0; e < count; e++)
        {
            var distance = _distance[e];
            var cos = _cos[e];
            var sin = _sin[e];
            var front = (float)Math.Min(fronts[e], float.MaxValue);
            var theta = (omega * seconds) + emitters[e].Phase;

            // Now: A·sin(θ − kr) = sin θ·C − cos θ·S. Swing: A·e^(i(phase − kr)), its size taken at the end.
            var a = (float)(average ? Math.Cos(emitters[e].Phase) : Math.Sin(theta));
            var b = (float)(average ? Math.Sin(emitters[e].Phase) : Math.Cos(theta));
            var re = _re;
            var im = _im;
            for (var i = 0; i < n; i++)
            {
                var r = distance[i];
                if (r > front) continue;

                var taper = front - r < band ? (front - r) / band : 1f;
                if (average)
                {
                    re[i] += taper * ((a * cos[i]) + (b * sin[i]));
                    im[i] += taper * ((b * cos[i]) - (a * sin[i]));
                }
                else
                {
                    re[i] += taper * ((a * cos[i]) - (b * sin[i]));
                }
            }
        }

        var pixels = _pixels;
        if (average && averagePalette is not null)
        {
            var last = averagePalette.Length - 1;
            var half = (float)(gain / 2);
            for (var i = 0; i < n; i++)
            {
                var size = MathF.Sqrt((_re[i] * _re[i]) + (_im[i] * _im[i])) * half;
                pixels[i] = averagePalette[(int)(Math.Clamp(size, 0f, 1f) * last)];
            }
        }
        else
        {
            var table = Table(palette);
            var perUnit = (float)(gain * (Steps - 1) / (2 * Reach));
            const float middle = (Steps - 1) / 2f;
            for (var i = 0; i < n; i++)
            {
                var index = (int)((_re[i] * perUnit) + middle);
                pixels[i] = table[index < 0 ? 0 : index >= Steps ? Steps - 1 : index];
            }
        }

        SpaceDrawing.WritePixels(Bitmap!, pixels, columns, rows);
    }

    /// <summary>The palette looked up by height × gain from −3 to 3, through tanh's soft limit.</summary>
    private uint[] Table(uint[] palette)
    {
        if (_table is { } cached && ReferenceEquals(cached.Palette, palette)) return cached.Table;

        var table = new uint[Steps];
        var last = palette.Length - 1;
        for (var i = 0; i < Steps; i++)
        {
            var v = Math.Tanh(((2.0 * i / (Steps - 1)) - 1) * Reach);
            table[i] = palette[(int)Math.Round((v + 1) * 0.5 * last)];
        }

        _table = (palette, table);
        return table;
    }

    public void Draw(PaintContext dc)
    {
        if (Bitmap is null) return;
        var area = _shape.Area;
        dc.DrawImage(Bitmap, new Rect(area.X, area.Y, _shape.Columns * Cell, _shape.Rows * Cell));
    }

    private void Build(Shape shape, IReadOnlyList<Emitter> emitters)
    {
        _shape = shape;
        var n = shape.Columns * shape.Rows;
        if (Bitmap is null || Bitmap.PixelSize.Width != shape.Columns || Bitmap.PixelSize.Height != shape.Rows)
        {
            Bitmap = new WriteableBitmap(new PixelSize(shape.Columns, shape.Rows), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            _pixels = new uint[n];
            _re = new float[n];
            _im = new float[n];
        }

        var k = 2 * Math.PI / shape.Wavelength;
        _distance = new float[emitters.Count][];
        _cos = new float[emitters.Count][];
        _sin = new float[emitters.Count][];
        _farthest = new double[emitters.Count];
        for (var e = 0; e < emitters.Count; e++)
        {
            var emitter = emitters[e];
            var distance = _distance[e] = new float[n];
            var cos = _cos[e] = new float[n];
            var sin = _sin[e] = new float[n];
            for (var row = 0; row < shape.Rows; row++)
            {
                var y = (shape.Area.Y + ((row + 0.5) * Cell) - shape.Origin.Y) / shape.Scale;
                for (var column = 0; column < shape.Columns; column++)
                {
                    var i = (row * shape.Columns) + column;
                    var x = (shape.Area.X + ((column + 0.5) * Cell) - shape.Origin.X) / shape.Scale;
                    if (x < emitter.MinX || x > emitter.MaxX)
                    {
                        distance[i] = float.PositiveInfinity;
                        continue;
                    }

                    double r, amplitude;
                    if (emitter.Kind == EmitterKind.Plane)
                    {
                        r = x - emitter.Position.X;
                        amplitude = r < 0 ? 0 : 1;
                    }
                    else
                    {
                        r = Math.Sqrt(((x - emitter.Position.X) * (x - emitter.Position.X)) + ((y - emitter.Position.Y) * (y - emitter.Position.Y)));
                        amplitude = WavePhysics.RippleAmplitude(r, shape.Wavelength);
                    }

                    distance[i] = (float)r;
                    _farthest[e] = Math.Max(_farthest[e], r);
                    cos[i] = (float)(amplitude * Math.Cos(k * r));
                    sin[i] = (float)(amplitude * Math.Sin(k * r));
                }
            }
        }
    }

    /// <summary>A palette of <paramref name="size"/> colours from −1 to 1 through the given stops (position −1…1, colour).</summary>
    public static uint[] Palette(int size, params (double At, Color Colour)[] stops)
    {
        var palette = new uint[size];
        for (var i = 0; i < size; i++)
        {
            var v = (2.0 * i / (size - 1)) - 1;
            var upper = 1;
            while (upper < stops.Length - 1 && stops[upper].At < v) upper++;
            var (a, ca) = stops[upper - 1];
            var (b, cb) = stops[upper];
            var t = b > a ? Math.Clamp((v - a) / (b - a), 0, 1) : 0;
            palette[i] = Pack(Mix(ca, cb, t));
        }

        return palette;
    }

    public static uint Pack(Color c) => 0xFF000000u | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;

    private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + ((b.R - a.R) * t)),
        (byte)Math.Round(a.G + ((b.G - a.G) * t)),
        (byte)Math.Round(a.B + ((b.B - a.B) * t)));
}
