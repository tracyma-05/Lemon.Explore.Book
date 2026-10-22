using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Expansion;

/// <summary>
/// Small galaxy pictures drawn once into bitmaps (spirals at several tilts and turns, ellipticals, the Milky Way, a
/// quasar), so hundreds of galaxies cost one image draw each.
/// </summary>
internal sealed class GalaxySprites
{
    /// <summary>The sprite's size in DIPs; it is drawn scaled to the galaxy's size.</summary>
    private const double Box = 48;

    private readonly IImage?[] _looks = new IImage?[GalaxyField.Looks];
    private IImage? _milkyWay;
    private IImage? _quasar;
    private IImage? _ring;
    private double _scale;

    /// <summary>Draws look <paramref name="look"/> centred on <paramref name="center"/> with a radius of <paramref name="radius"/>.</summary>
    public void Draw(PaintContext dc, int look, Point center, double radius, double dpi, double opacity = 1)
    {
        Prepare(dpi);
        var image = look < 0 ? _milkyWay : _looks[look % GalaxyField.Looks];
        if (image is null) return;
        if (opacity < 0.999) dc.PushOpacity(opacity);
        dc.DrawImage(image, new Rect(center.X - radius, center.Y - radius, 2 * radius, 2 * radius));
        if (opacity < 0.999) dc.Pop();
    }

    /// <summary>The Milky Way: a bigger, golden barred spiral seen a little tilted.</summary>
    public void DrawMilkyWay(PaintContext dc, Point center, double radius, double dpi) => Draw(dc, -1, center, radius, dpi);

    /// <summary>A quasar: a blazing point in a faint galaxy.</summary>
    public void DrawQuasar(PaintContext dc, Point center, double radius, double dpi)
    {
        Prepare(dpi);
        if (_quasar is not null) dc.DrawImage(_quasar, new Rect(center.X - radius, center.Y - radius, 2 * radius, 2 * radius));
    }

    /// <summary>A thin cyan ring (where a galaxy used to be), from a bitmap.</summary>
    public void DrawRing(PaintContext dc, Point center, double radius, double dpi)
    {
        Prepare(dpi);
        if (_ring is not null) dc.DrawImage(_ring, new Rect(center.X - radius, center.Y - radius, 2 * radius, 2 * radius));
    }

    private void Prepare(double dpi)
    {
        var scale = Math.Max(1, dpi) * 2;
        if (Math.Abs(scale - _scale) < 1e-6 && _milkyWay is not null) return;
        _scale = scale;
        for (var i = 0; i < GalaxyField.Looks; i++)
        {
            _looks[i] = i switch
            {
                0 => Render(scale, dc => Spiral(dc, 0.55, 0.4, Color.FromRgb(0xFF, 0xE6, 0xB0), Color.FromRgb(0x9C, 0xC4, 0xFF), 2.2)),
                1 => Render(scale, dc => Spiral(dc, 0.9, 1.6, Color.FromRgb(0xFF, 0xF0, 0xC8), Color.FromRgb(0xB4, 0xD2, 0xFF), 1.8)),
                2 => Render(scale, dc => Spiral(dc, 0.3, 2.6, Color.FromRgb(0xFF, 0xDC, 0xA0), Color.FromRgb(0x8E, 0xB8, 0xFF), 2.5)),
                3 => Render(scale, dc => Elliptical(dc, 0.75, 0.3, Color.FromRgb(0xFF, 0xD8, 0x9A))),
                4 => Render(scale, dc => Spiral(dc, 0.7, 4.1, Color.FromRgb(0xFF, 0xEC, 0xC0), Color.FromRgb(0xA8, 0xCC, 0xFF), 2.0)),
                5 => Render(scale, dc => Elliptical(dc, 0.95, 1.2, Color.FromRgb(0xFF, 0xE0, 0xB0))),
                6 => Render(scale, dc => Spiral(dc, 0.45, 5.2, Color.FromRgb(0xFF, 0xE4, 0xB4), Color.FromRgb(0x96, 0xBE, 0xFF), 2.4)),
                _ => Render(scale, dc => Elliptical(dc, 0.55, 2.2, Color.FromRgb(0xFF, 0xCC, 0x90))),
            };
        }

        _milkyWay = Render(scale, dc => Spiral(dc, 0.62, 0.9, Color.FromRgb(0xFF, 0xE2, 0x8A), Color.FromRgb(0xFF, 0xD3, 0x6B), 2.3, bar: true));
        _quasar = Render(scale, Quasar);
        _ring = Render(scale, dc =>
        {
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(150, 0x6F, 0xD8, 0xFF)), Box * 0.09);
            dc.DrawEllipse(null, pen, new Point(0, 0), Box * 0.42, Box * 0.42);
        });
    }

    private static IImage Render(double scale, Action<PaintContext> draw) =>
        SpaceDrawing.RenderToBitmap(Box, Box, scale, dc =>
        {
            dc.PushTransform(Matrix.CreateTranslation(Box / 2, Box / 2));
            draw(dc);
            dc.Pop();
        });

    /// <summary>A spiral seen at a tilt (1 face-on), turned by <paramref name="angle"/>, its arms wound <paramref name="wind"/> radians.</summary>
    private static void Spiral(PaintContext dc, double tilt, double angle, Color core, Color arms, double wind, bool bar = false)
    {
        const double r = Box / 2 - 1;
        dc.PushTransform(SpaceDrawing.Rotation(angle * 180 / Math.PI));
        dc.PushTransform(new ScaleTransform(1, tilt));

        var halo = SpaceDrawing.RadialGradient(
            (WithAlpha(arms, 0.55), 0),
            (WithAlpha(arms, 0.22), 0.55),
            (WithAlpha(arms, 0), 1));
        dc.DrawEllipse(halo, null, new Point(0, 0), r, r);

        // Two arms: logarithmic spirals drawn as fading strokes.
        foreach (var start in new[] { 0.0, Math.PI })
        {
            var points = new List<Point>();
            for (var i = 0; i <= 40; i++)
            {
                var t = i / 40.0;
                var rr = r * (0.18 + (0.78 * t));
                var th = start + (wind * t * Math.PI);
                points.Add(new Point(rr * Math.Cos(th), rr * Math.Sin(th)));
            }

            for (var i = 1; i < points.Count; i++)
            {
                var t = i / (double)points.Count;
                var pen = new Pen(new SolidColorBrush(WithAlpha(Lerp(Colors.White, arms, 0.4 + (0.5 * t)), 0.85 * (1 - (0.7 * t)))), 3.4 - (2 * t))
                {
                    LineCap = PenLineCap.Round,
                };
                dc.DrawLine(pen, points[i - 1], points[i]);
            }
        }

        if (bar)
        {
            var barPen = new ImmutablePen(((IBrush)new SolidColorBrush(WithAlpha(core, 0.8))).ToImmutable(), 4.5, null, PenLineCap.Round, PenLineJoin.Miter);
            dc.DrawLine(barPen, new Point(-r * 0.22, 0), new Point(r * 0.22, 0));
        }

        var bulge = SpaceDrawing.RadialGradient(
            (Colors.White, 0),
            (WithAlpha(core, 0.95), 0.35),
            (WithAlpha(core, 0), 1));
        dc.DrawEllipse(bulge, null, new Point(0, 0), r * 0.36, r * 0.36);
        dc.Pop();
        dc.Pop();
    }

    /// <summary>An elliptical galaxy: a smooth golden glow, squashed by <paramref name="ratio"/>.</summary>
    private static void Elliptical(PaintContext dc, double ratio, double angle, Color color)
    {
        const double r = Box / 2 - 2;
        dc.PushTransform(SpaceDrawing.Rotation(angle * 180 / Math.PI));
        var glow = SpaceDrawing.RadialGradient(
            (Colors.White, 0),
            (WithAlpha(color, 0.9), 0.18),
            (WithAlpha(color, 0.35), 0.55),
            (WithAlpha(color, 0), 1));
        dc.DrawEllipse(glow, null, new Point(0, 0), r * 0.85, r * 0.85 * ratio);
        dc.Pop();
    }

    private static void Quasar(PaintContext dc)
    {
        const double r = Box / 2 - 1;
        var host = SpaceDrawing.RadialGradient(
            (Color.FromArgb(120, 0xC8, 0xB4, 0xFF), 0),
            (Color.FromArgb(40, 0x9C, 0x8C, 0xFF), 0.6),
            (Color.FromArgb(0, 0x9C, 0x8C, 0xFF), 1));
        dc.DrawEllipse(host, null, new Point(0, 0), r, r * 0.7);
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(150, 0xDC, 0xE8, 0xFF)), 1.4);
        dc.DrawLine(pen, new Point(0, -r * 0.95), new Point(0, r * 0.95));
        var core = SpaceDrawing.RadialGradient(
            (Colors.White, 0),
            (Color.FromArgb(230, 0xE6, 0xF0, 0xFF), 0.25),
            (Color.FromArgb(0, 0xB4, 0xC8, 0xFF), 1));
        dc.DrawEllipse(core, null, new Point(0, 0), r * 0.42, r * 0.42);
    }
}
