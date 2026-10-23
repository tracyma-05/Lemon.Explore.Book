using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Lenses;

/// <summary>
/// The camera's photo: a scene at four distances (a branch close by, a person, trees and a house, the mountains), each
/// drawn as its own layer for the focal length, blurred as much as the lens blurs that distance, laid together and
/// developed with the exposure's brightness and the ISO's grain. Redrawn only when something it depends on changes.
/// </summary>
internal sealed class CameraPhoto
{
    public const int Width = 480;
    public const int Height = 320;

    /// <summary>The camera's height above the ground, m.</summary>
    public const double CameraHeight = 1.5;

    /// <summary>How far away each layer is, m, the nearest first: branch, person, trees, mountains.</summary>
    public static readonly double[] Distances = [0.9, 5, 25, double.PositiveInfinity];

    /// <summary>The ball is thrown across in front of the person, m away.</summary>
    public const double BallDistance = 4.6;

    /// <summary>The ball's diameter, m.</summary>
    public const double BallSize = 0.22;

    private readonly byte[]?[] _sharp = new byte[]?[4];
    private readonly byte[]?[] _blurred = new byte[]?[4];
    private readonly double[] _blurDone = [-1, -1, -1, -1];
    private double _drawnFocal = double.NaN;
    private SceneLight _drawnLight;
    private (double Gain, double Grain) _developed = (double.NaN, double.NaN);
    private bool _dirty = true;
    private WriteableBitmap? _bitmap;

    /// <summary>The photo for these settings: <paramref name="gain"/> is 2 to the power of the exposure error.</summary>
    public IImage Picture(double f, double fNumber, double focus, double gain, double grain, SceneLight light)
    {
        if (f != _drawnFocal || light != _drawnLight)
        {
            _drawnFocal = f;
            _drawnLight = light;
            var palette = Palette.For(light);
            for (var i = 0; i < 4; i++)
            {
                var layer = i;
                _sharp[i] = SoftImage.Draw(Width, Height, dc => DrawLayer(dc, layer, f, palette));
                _blurDone[i] = -1;
            }
        }

        for (var i = 0; i < 4; i++)
        {
            var diameter = Math.Min(160, LensModel.BlurOnSensor(f, fNumber, focus, Distances[i]) / LensModel.SensorWidth * Width);
            if (_blurred[i] is not null && Math.Abs(diameter - _blurDone[i]) < 0.3) continue;
            _blurred[i] = SoftImage.Blur(_sharp[i]!, Width, Height, diameter);
            _blurDone[i] = diameter;
            _dirty = true;
        }

        if (!_dirty && _developed == (gain, grain) && _bitmap is not null) return _bitmap;
        var photo = (byte[])_blurred[3]!.Clone();
        for (var i = 2; i >= 0; i--) SoftImage.Over(photo, _blurred[i]!);
        SoftImage.Develop(photo, gain, grain);
        _developed = (gain, grain);
        _dirty = false;
        return SoftImage.Show(ref _bitmap, photo, Width, Height);
    }

    /// <summary>The nearest layer with something solid at a pixel of the photo (3, the mountains and sky, if none).</summary>
    public int LayerAt(Point pixel)
    {
        var x = (int)Math.Clamp(pixel.X, 0, Width - 1);
        var y = (int)Math.Clamp(pixel.Y, 0, Height - 1);
        for (var i = 0; i < 3; i++)
        {
            if (_sharp[i] is { } layer && layer[(((y * Width) + x) * 4) + 3] > 120) return i;
        }

        return 3;
    }

    /// <summary>Where the middle of a layer's subject is in the photo, pixels (for the focus box).</summary>
    public static Point SubjectPoint(int layer, double f) => layer switch
    {
        0 => Project(new Point(0.2, 1.72), 0.9, f),
        1 => Project(new Point(-0.6, 1.35), 5, f),
        2 => Project(new Point(-2, 2.5), 25, f),
        _ => new Point(Width * 0.62, (Height / 2.0) - (Math.Tan(1.6 * Math.PI / 180) * f / LensModel.SensorWidth * Width)),
    };

    /// <summary>Pixels per metre across the view at <paramref name="distance"/> m.</summary>
    public static double Scale(double f, double distance) => f / LensModel.SensorWidth * Width / distance;

    /// <summary>A point <paramref name="metres"/> (sideways, height above the ground) at <paramref name="distance"/> m, in photo pixels.</summary>
    public static Point Project(Point metres, double distance, double f)
    {
        var k = Scale(f, distance);
        return new Point((Width / 2.0) + (metres.X * k), (Height / 2.0) - ((metres.Y - CameraHeight) * k));
    }

    /// <summary>A direction (degrees right of straight ahead, degrees up) in photo pixels.</summary>
    private static Point Direction(double azimuth, double elevation, double f)
    {
        var k = f / LensModel.SensorWidth * Width;
        return new Point((Width / 2.0) + (Math.Tan(azimuth * Math.PI / 180) * k), (Height / 2.0) - (Math.Tan(elevation * Math.PI / 180) * k));
    }

    #region the scene

    private static void DrawLayer(PaintContext dc, int layer, double f, Palette p)
    {
        switch (layer)
        {
            case 0:
                DrawBranch(dc, f, p);
                break;

            case 1:
                DrawPerson(dc, f, p);
                break;

            case 2:
                DrawTrees(dc, f, p);
                break;

            default:
                DrawFar(dc, f, p);
                break;
        }
    }

    private static void DrawFar(PaintContext dc, double f, Palette p)
    {
        var horizon = Height / 2.0;
        dc.DrawRectangle(Gradient(p.SkyTop, p.SkyHorizon, 0, horizon), null, new Rect(0, 0, Width, horizon + 1));
        dc.DrawRectangle(Gradient(p.FarGround, p.Ground, horizon, Height), null, new Rect(0, horizon, Width, Height - horizon));

        if (p.Stars)
        {
            var random = new Random(7);
            for (var i = 0; i < 70; i++)
            {
                var star = Direction((random.NextDouble() * 120) - 60, 2 + (random.NextDouble() * 40), f);
                if (star.Y > horizon - 4) continue;
                var r = 0.5 + (random.NextDouble() * 0.9);
                dc.DrawEllipse(Solid((byte)(120 + random.Next(120)), 255, 255, 240), null, star, r, r);
            }

            var moon = Direction(-18, 14, f);
            var moonR = Math.Max(2, Math.Tan(0.26 * Math.PI / 180) * f / LensModel.SensorWidth * Width);
            dc.DrawEllipse(Solid(255, 0xF4, 0xF1, 0xE0), null, moon, moonR, moonR);
        }

        // Clouds: soft lumps in the sky.
        if (p.Clouds > 0)
        {
            var random = new Random(3);
            for (var i = 0; i < 9; i++)
            {
                var centre = Direction((random.NextDouble() * 110) - 55, 5 + (random.NextDouble() * 14), f);
                var k = f / LensModel.SensorWidth * Width * Math.PI / 180;
                var rx = (4 + (random.NextDouble() * 6)) * k;
                var ry = rx * 0.32;
                var brush = SpaceDrawing.RadialGradient((WithAlpha(p.Cloud, p.Clouds), 0), (WithAlpha(p.Cloud, 0), 1));
                dc.DrawEllipse(brush, null, centre, rx, ry);
            }
        }

        // Two ranges of mountains, the far one paler.
        DrawRange(dc, f, p.MountainFar, 2.2, 0.9, 0.0);
        DrawRange(dc, f, p.MountainNear, 1.3, 0.6, 2.1);
    }

    private static void DrawRange(PaintContext dc, double f, Color color, double height, double swing, double seed)
    {
        var points = new List<Point>();
        for (var a = -75.0; a <= 75.0; a += 0.5)
        {
            var e = height + (swing * Math.Sin((a * 0.11) + seed)) + (0.45 * swing * Math.Sin((a * 0.37) + (2 * seed))) + (0.2 * swing * Math.Sin((a * 1.3) + seed));
            points.Add(Direction(a, Math.Max(0.05, e), f));
        }

        points.Add(Direction(75, -0.02, f));
        points.Add(Direction(-75, -0.02, f));
        dc.DrawGeometry(Solid(255, color.R, color.G, color.B), null, Polygon([.. points]));
    }

    private static void DrawTrees(PaintContext dc, double f, Palette p)
    {
        const double D = 25;
        var k = Scale(f, D);
        var groundTop = Project(new Point(0, 0), D, f).Y;
        if (groundTop < Height)
        {
            dc.DrawRectangle(Gradient(p.Ground, Darker(p.Ground, 0.85), groundTop, Height), null, new Rect(0, groundTop, Width, Height - groundTop));
        }

        // A house with a roof, a door and two windows.
        var wallBrush = Solid(255, p.Wall.R, p.Wall.G, p.Wall.B);
        var roofBrush = Solid(255, p.Roof.R, p.Roof.G, p.Roof.B);
        var windowBrush = Solid(255, p.Window.R, p.Window.G, p.Window.B);
        var bottomLeft = Project(new Point(-4.5, 0), D, f);
        var topRight = Project(new Point(0.5, 3.4), D, f);
        dc.DrawRectangle(wallBrush, null, new Rect(bottomLeft, topRight));
        dc.DrawGeometry(roofBrush, null, Polygon(Project(new Point(-5.1, 3.3), D, f), Project(new Point(1.1, 3.3), D, f), Project(new Point(-2, 5.4), D, f)));
        dc.DrawRectangle(Solid(255, 0x5B, 0x3A, 0x26), null, new Rect(Project(new Point(-2.5, 0), D, f), Project(new Point(-1.5, 2.1), D, f)));
        dc.DrawRectangle(windowBrush, null, new Rect(Project(new Point(-4, 1.3), D, f), Project(new Point(-3, 2.4), D, f)));
        dc.DrawRectangle(windowBrush, null, new Rect(Project(new Point(-0.8, 1.3), D, f), Project(new Point(0.2, 2.4), D, f)));

        DrawTree(dc, f, D, -11, 9.5, p);
        DrawTree(dc, f, D, 5.5, 7.5, p);
        DrawTree(dc, f, D, 11, 11, p);
        _ = k;
    }

    private static void DrawTree(PaintContext dc, double f, double d, double x, double height, Palette p)
    {
        var trunk = Solid(255, p.Trunk.R, p.Trunk.G, p.Trunk.B);
        dc.DrawRectangle(trunk, null, new Rect(Project(new Point(x - 0.25, 0), d, f), Project(new Point(x + 0.25, height * 0.45), d, f)));
        var k = Scale(f, d);
        var crown = height * 0.32;
        (double Dx, double Dy, double R, double Shade)[] puffs =
        [
            (0, 0.62, 1.0, 1.0), (-0.55, 0.52, 0.75, 0.85), (0.55, 0.5, 0.78, 0.9), (-0.25, 0.8, 0.7, 1.1), (0.3, 0.82, 0.65, 1.05),
        ];
        foreach (var (dx, dy, r, shade) in puffs)
        {
            var centre = Project(new Point(x + (dx * crown), height * dy), d, f);
            var colour = Darker(p.Leaves, shade);
            dc.DrawEllipse(Solid(255, colour.R, colour.G, colour.B), null, centre, r * crown * k, r * crown * k * 0.95);
        }
    }

    private static void DrawPerson(PaintContext dc, double f, Palette p)
    {
        const double D = 5;
        var x = -0.6;

        Rect Box(double left, double bottom, double right, double top) => new(Project(new Point(x + left, bottom), D, f), Project(new Point(x + right, top), D, f));
        double K(double metres) => metres * Scale(f, D);

        var trousers = Solid(255, p.Trousers.R, p.Trousers.G, p.Trousers.B);
        var jacket = Solid(255, p.Jacket.R, p.Jacket.G, p.Jacket.B);
        var skin = Solid(255, p.Skin.R, p.Skin.G, p.Skin.B);
        var hair = Solid(255, p.Hair.R, p.Hair.G, p.Hair.B);
        dc.DrawRoundedRectangle(trousers, null, Box(-0.17, 0, -0.02, 0.86), K(0.05), K(0.05));
        dc.DrawRoundedRectangle(trousers, null, Box(0.02, 0, 0.17, 0.86), K(0.05), K(0.05));
        dc.DrawRoundedRectangle(jacket, null, Box(-0.22, 0.78, 0.22, 1.45), K(0.1), K(0.1));
        dc.DrawRoundedRectangle(jacket, null, Box(-0.31, 0.86, -0.19, 1.42), K(0.06), K(0.06));
        dc.DrawRoundedRectangle(jacket, null, Box(0.19, 0.86, 0.31, 1.42), K(0.06), K(0.06));
        dc.DrawEllipse(skin, null, Project(new Point(x - 0.25, 0.86), D, f), K(0.055), K(0.055));
        dc.DrawEllipse(skin, null, Project(new Point(x + 0.25, 0.86), D, f), K(0.055), K(0.055));
        dc.DrawRectangle(skin, null, Box(-0.05, 1.42, 0.05, 1.5));
        dc.DrawEllipse(skin, null, Project(new Point(x, 1.6), D, f), K(0.1), K(0.12));
        dc.DrawGeometry(hair, null, SpaceDrawing.Ellipse(Project(new Point(x, 1.66), D, f), K(0.105), K(0.08)));

        if (p.Lamp)
        {
            // A street lamp beside the path.
            var pole = Solid(255, 0x2A, 0x2E, 0x36);
            dc.DrawRectangle(pole, null, Box(1.95, 0, 2.03, 3.4));
            dc.DrawRectangle(pole, null, Box(1.75, 3.35, 2.1, 3.42));
            var glow = SpaceDrawing.RadialGradient((Color.FromArgb(200, 0xFF, 0xE0, 0x9A), 0), (Color.FromArgb(0, 0xFF, 0xD0, 0x80), 1));
            var lamp = Project(new Point(x + 1.8, 3.3), D, f);
            dc.DrawEllipse(glow, null, lamp, K(0.6), K(0.6));
            dc.DrawEllipse(Solid(255, 0xFF, 0xF4, 0xD6), null, lamp, K(0.07), K(0.05));
        }
    }

    private static void DrawBranch(PaintContext dc, double f, Palette p)
    {
        const double D = 0.9;
        var k = Scale(f, D);
        var wood = Solid(255, p.Trunk.R, p.Trunk.G, p.Trunk.B);
        var stem = new StreamGeometry();
        using (var context = stem.Open())
        {
            context.BeginFigure(Project(new Point(0.75, 2.05), D, f), false);
            context.QuadraticBezierTo(Project(new Point(0.4, 1.86), D, f), Project(new Point(0.1, 1.7), D, f));

            context.EndFigure(false);
        }
        dc.DrawGeometry(null, MakePen(wood, Math.Max(1, 0.012 * k)), stem);

        (double X, double Y, double Angle, double Shade)[] leaves =
        [
            (0.12, 1.69, 200, 1.0), (0.17, 1.73, 140, 0.9), (0.23, 1.73, 230, 1.05), (0.29, 1.78, 150, 0.85), (0.35, 1.78, 215, 1.0),
            (0.42, 1.84, 140, 0.95), (0.48, 1.84, 220, 0.9), (0.56, 1.9, 150, 1.05), (0.63, 1.92, 210, 0.9),
        ];
        foreach (var (lx, ly, angle, shade) in leaves)
        {
            var centre = Project(new Point(lx, ly), D, f);
            var colour = Darker(p.Leaves, shade * 1.15);
            var leaf = SpaceDrawing.Ellipse(new Point(0, 0), 0.045 * k, 0.018 * k);
            leaf.Transform = new MatrixTransform(SpaceDrawing.Rotation(angle) * Matrix.CreateTranslation(centre.X, centre.Y));
            dc.DrawGeometry(Solid(255, colour.R, colour.G, colour.B), null, leaf);
        }
    }

    private static IBrush Gradient(Color top, Color bottom, double from, double to)
    {
        // In pixels of the picture, not a share of each shape's box.
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, from, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(0, Math.Max(from + 1, to), RelativeUnit.Absolute),
            GradientStops = { new GradientStop(top, 0), new GradientStop(bottom, 1) },
        };
    }

    private static Color Darker(Color c, double factor) =>
        Color.FromRgb((byte)Math.Clamp(c.R * factor, 0, 255), (byte)Math.Clamp(c.G * factor, 0, 255), (byte)Math.Clamp(c.B * factor, 0, 255));

    /// <summary>The scene's colours in each light, as a well exposed photo shows them.</summary>
    private sealed record Palette(Color SkyTop, Color SkyHorizon, Color MountainFar, Color MountainNear, Color FarGround, Color Ground,
        Color Leaves, Color Trunk, Color Wall, Color Roof, Color Window, Color Jacket, Color Trousers, Color Skin, Color Hair,
        Color Cloud, double Clouds, bool Stars, bool Lamp)
    {
        public static Palette For(SceneLight light) => light switch
        {
            SceneLight.Cloud => new(C(0x8E, 0x99, 0xA6), C(0xC8, 0xCE, 0xD4), C(0x8E, 0x9A, 0xA6), C(0x6C, 0x7A, 0x86), C(0x6E, 0x86, 0x5E), C(0x55, 0x74, 0x47),
                C(0x3A, 0x62, 0x34), C(0x4A, 0x3A, 0x2E), C(0xD2, 0xC8, 0xB6), C(0x8A, 0x4A, 0x38), C(0x55, 0x60, 0x6A), C(0xB8, 0x48, 0x40), C(0x34, 0x3C, 0x52),
                C(0xD8, 0xB0, 0x8E), C(0x34, 0x28, 0x20), C(0xE0, 0xE3, 0xE6), 0.9, false, false),
            SceneLight.Dusk => new(C(0x24, 0x2A, 0x58), C(0xF0, 0x8E, 0x56), C(0x5A, 0x4A, 0x6E), C(0x3A, 0x32, 0x4E), C(0x3A, 0x40, 0x34), C(0x2C, 0x34, 0x28),
                C(0x22, 0x34, 0x24), C(0x30, 0x24, 0x1E), C(0x9A, 0x84, 0x7A), C(0x5E, 0x30, 0x2C), C(0xFF, 0xC8, 0x6A), C(0x96, 0x38, 0x36), C(0x26, 0x2A, 0x3A),
                C(0xB8, 0x8A, 0x70), C(0x24, 0x1C, 0x18), C(0xF6, 0xA8, 0x88), 0.5, false, true),
            SceneLight.Night => new(C(0x05, 0x08, 0x16), C(0x18, 0x20, 0x3C), C(0x1C, 0x22, 0x36), C(0x12, 0x16, 0x24), C(0x14, 0x1A, 0x18), C(0x10, 0x16, 0x12),
                C(0x10, 0x1C, 0x12), C(0x1C, 0x16, 0x12), C(0x4A, 0x46, 0x4A), C(0x2E, 0x1C, 0x1C), C(0xFF, 0xC4, 0x5E), C(0x6E, 0x2C, 0x2C), C(0x1A, 0x1E, 0x2A),
                C(0x9A, 0x7A, 0x66), C(0x1A, 0x14, 0x10), C(0x30, 0x34, 0x48), 0.0, true, true),
            _ => new(C(0x2E, 0x6F, 0xC9), C(0xB8, 0xDA, 0xF2), C(0x86, 0xA4, 0xC4), C(0x5E, 0x7E, 0x9C), C(0x86, 0xB0, 0x5C), C(0x5C, 0x8E, 0x3E),
                C(0x3E, 0x7A, 0x34), C(0x5A, 0x40, 0x2C), C(0xEE, 0xE0, 0xC6), C(0xB0, 0x4A, 0x30), C(0x6E, 0x8E, 0xA8), C(0xD8, 0x44, 0x3A), C(0x2E, 0x3A, 0x5A),
                C(0xEC, 0xBC, 0x94), C(0x3A, 0x2A, 0x1E), Colors.White, 0.85, false, false),
        };

        private static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
    }

    /// <summary>The ball's colour in this light.</summary>
    public static Color BallColour(SceneLight light) => light switch
    {
        SceneLight.Dusk => Color.FromRgb(0xC8, 0x9A, 0x30),
        SceneLight.Night => Color.FromRgb(0x8A, 0x70, 0x2C),
        SceneLight.Cloud => Color.FromRgb(0xE8, 0xBE, 0x3C),
        _ => Color.FromRgb(0xFF, 0xD0, 0x3A),
    };

    #endregion
}

/// <summary>
/// A chart of rotating Es (as on the Chinese standard chart), four rows from 4.3 (20/100) to 5.0 (20/20), drawn at
/// <see cref="PixelsPerArcMinute"/> and blurred by the eye's focus error.
/// </summary>
internal sealed class EyeChart
{
    public const int Width = 300;
    public const int Height = 200;

    /// <summary>The chart's scale: how many pixels an arc minute of the view spans.</summary>
    public const double PixelsPerArcMinute = 2.4;

    /// <summary>The rows' smallest detail (a stroke of the E, a fifth of its height), arc minutes, top row first.</summary>
    public static readonly double[] RowDetail = [5.0119, 2.5119, 1.5849, 1.0];

    private static readonly int[] RowCounts = [2, 3, 4, 5];

    private byte[]? _sharp;
    private byte[]? _shown;
    private (double Blur, double Dim) _done = (double.NaN, double.NaN);
    private WriteableBitmap? _bitmap;

    /// <summary>The vertical middle of each row, pixels from the top.</summary>
    public static IReadOnlyList<double> RowCentres { get; } = Layout().Select(r => r.Centre).ToList();

    private static IReadOnlyList<(double Centre, double Size)> Layout()
    {
        var rows = new List<(double, double)>();
        var y = 14.0;
        foreach (var detail in RowDetail)
        {
            var size = detail * 5 * PixelsPerArcMinute;
            rows.Add((y + (size / 2), size));
            y += size + 14;
        }

        return rows;
    }

    /// <summary>The chart seen with a blur of <paramref name="blurArcMinutes"/>, darker in dim light (<paramref name="dim"/> 0–1).</summary>
    public IImage Picture(double blurArcMinutes, double dim)
    {
        var blur = Math.Round(Math.Min(blurArcMinutes * PixelsPerArcMinute, 120) * 4) / 4;
        if (_bitmap is not null && _done == (blur, dim)) return _bitmap;
        _sharp ??= SoftImage.Draw(Width, Height, DrawChart);
        _shown = SoftImage.Blur(_sharp, Width, Height, blur);
        if (dim > 0) SoftImage.Develop(_shown, 1 - (0.55 * dim), 0);
        _done = (blur, dim);
        return SoftImage.Show(ref _bitmap, _shown, Width, Height);
    }

    private static void DrawChart(PaintContext dc)
    {
        dc.DrawRectangle(Solid(255, 0xF4, 0xF2, 0xEC), null, new Rect(0, 0, Width, Height));
        var ink = Solid(255, 0x14, 0x14, 0x18);
        var layout = Layout();
        var turn = 0;
        for (var row = 0; row < layout.Count; row++)
        {
            var (centre, size) = layout[row];
            var count = RowCounts[row];
            var gap = size * 0.9;
            var total = (count * size) + ((count - 1) * gap);
            var x = (Width + 30 - total) / 2;
            for (var i = 0; i < count; i++)
            {
                // The Es point every way, in a fixed order.
                DrawE(dc, ink, new Rect(x, centre - (size / 2), size, size), (turn * 7 % 4) * 90);
                turn++;
                x += size + gap;
            }
        }
    }

    /// <summary>An E of strokes a fifth of its size, turned by <paramref name="angle"/> degrees (0: open to the right).</summary>
    private static void DrawE(PaintContext dc, IBrush ink, Rect box, double angle)
    {
        var u = box.Width / 5;
        dc.PushTransform(SpaceDrawing.Rotation(angle, box.X + (box.Width / 2), box.Y + (box.Height / 2)));
        dc.DrawRectangle(ink, null, new Rect(box.X, box.Y, u, box.Height));
        dc.DrawRectangle(ink, null, new Rect(box.X, box.Y, box.Width, u));
        dc.DrawRectangle(ink, null, new Rect(box.X, box.Y + (2 * u), box.Width, u));
        dc.DrawRectangle(ink, null, new Rect(box.X, box.Y + (4 * u), box.Width, u));
        dc.Pop();
    }
}

/// <summary>
/// What the bench's screen shows: the candle (or the Sun) seen through the lens, as big as its middle rays make it, upside
/// down, and blurred by how far the screen is from the image.
/// </summary>
internal sealed class ScreenPicture
{
    public const int Width = 220;
    public const int Height = 150;

    /// <summary>Pixels per centimetre of screen.</summary>
    public const double PixelsPerCm = 12;

    private (double Tip, double Width, double Blur, bool Sun) _done = (double.NaN, 0, 0, false);
    private WriteableBitmap? _bitmap;

    /// <summary>
    /// The screen's picture: the flame's tip lands <paramref name="tip"/> cm from the axis (negative below), the candle is
    /// <paramref name="candleWidth"/> cm wide there, and every point is smeared over <paramref name="blur"/> cm. For the
    /// Sun, <paramref name="tip"/> is the size of its sharp image.
    /// </summary>
    public IImage Picture(double tip, double candleWidth, double blur, bool sun)
    {
        var key = (Math.Round(tip, 2), Math.Round(candleWidth, 2), Math.Round(blur * PixelsPerCm * 2) / 2, sun);
        if (_bitmap is not null && _done == key) return _bitmap;
        var light = SoftImage.Draw(Width, Height, dc => DrawLight(dc, key.Item1, key.Item2, sun));
        var blurred = SoftImage.Blur(light, Width, Height, key.Item3);
        var picture = SoftImage.Draw(Width, Height, dc => dc.DrawRectangle(Solid(255, 0x2C, 0x29, 0x26), null, new Rect(0, 0, Width, Height)));
        SoftImage.Over(picture, blurred);
        _done = key;
        return SoftImage.Show(ref _bitmap, picture, Width, Height);
    }

    private static void DrawLight(PaintContext dc, double tip, double candleWidth, bool sun)
    {
        var centre = new Point(Width / 2.0, Height / 2.0);
        if (sun)
        {
            var r = Math.Max(1.2, Math.Abs(tip) * PixelsPerCm / 2);
            dc.DrawEllipse(Solid(255, 0xFF, 0xF6, 0xD8), null, centre, r, r);
            return;
        }

        // Upside down when tip < 0: the base stays on the axis, the flame at the tip.
        var h = tip * PixelsPerCm;
        var w = Math.Max(1, candleWidth * PixelsPerCm);
        var wax = new Rect(centre.X - (w / 2), Math.Min(centre.Y, centre.Y - (0.7 * h)), w, Math.Abs(0.7 * h));
        dc.DrawRectangle(Solid(150, 0xE8, 0xD8, 0xB0), null, wax);
        var flameBase = centre.Y - (0.74 * h);
        var flameTip = centre.Y - h;
        var flame = new StreamGeometry();
        using (var context = flame.Open())
        {
            var half = w * 0.42;
            context.BeginFigure(new Point(centre.X, flameTip), true);
            context.CubicBezierTo(new Point(centre.X + half, flameTip + (0.35 * (flameBase - flameTip))), new Point(centre.X + half, flameBase), new Point(centre.X, flameBase));
            context.CubicBezierTo(new Point(centre.X - half, flameBase), new Point(centre.X - half, flameTip + (0.35 * (flameBase - flameTip))), new Point(centre.X, flameTip));

            context.EndFigure(true);
        }
        dc.DrawGeometry(Solid(255, 0xFF, 0xD8, 0x70), null, flame);
    }
}
