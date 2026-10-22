using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;

namespace Lemon.Explore.Book.Controls.Space;

/// <summary>Drawing helpers shared by the space diagrams (brushes, pens, shapes, ray-cast spheres).</summary>
internal static class SpaceDrawing
{
    /// <summary>Draws a ray-cast body centred on <paramref name="center"/>; <paramref name="scale"/> is device pixels per DIP.</summary>
    public static void DrawSphere(DrawingContext dc, PlanetRenderer renderer, Point center, double radius, LightVector light, double scale, double spin = 0)
    {
        if (radius < 0.5)
        {
            return;
        }

        var size = renderer.SizeFor(radius * scale);
        renderer.Render(size, light, spin);
        var edge = size / scale;
        dc.DrawImage(renderer.Bitmap!, new Rect(center.X - (edge / 2), center.Y - (edge / 2), edge, edge));
    }

    /// <summary>Light for a body drawn on screen, coming from the screen point <paramref name="sun"/>.</summary>
    public static LightVector LightFrom(Point body, Point sun, double towardsViewer = 0.3)
    {
        Vector v = sun - body;
        if (v.Length < 1e-6)
        {
            return new LightVector(0, 0, 1);
        }

        v /= v.Length;
        return LightVector.Normalize(v.X, -v.Y, towardsViewer);
    }

    public static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)(a.A + ((b.A - a.A) * t)),
            (byte)(a.R + ((b.R - a.R) * t)),
            (byte)(a.G + ((b.G - a.G) * t)),
            (byte)(a.B + ((b.B - a.B) * t)));
    }

    public static Color WithAlpha(Color color, double alpha) =>
        Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), color.R, color.G, color.B);

    public static IImmutableSolidColorBrush Solid(byte a, byte r, byte g, byte b) =>
        new ImmutableSolidColorBrush(Color.FromArgb(a, r, g, b));

    public static IImmutableSolidColorBrush Solid(Color color) => new ImmutableSolidColorBrush(color);

    private static readonly ImmutableDashStyle Dashes = new([4, 3], 0);

    public static IPen MakePen(IBrush brush, double thickness, bool dashed = false) =>
        new ImmutablePen((IImmutableBrush)brush, thickness, dashed ? Dashes : null, PenLineCap.Round);

    /// <summary>A two-colour linear gradient at <paramref name="angle"/> degrees (0 = left to right, 90 = top to bottom).</summary>
    public static IImmutableBrush LinearGradient(Color from, Color to, double angle)
    {
        var (sin, cos) = Math.SinCos(angle * Math.PI / 180);
        return new ImmutableLinearGradientBrush(
            [new ImmutableGradientStop(0, from), new ImmutableGradientStop(1, to)],
            startPoint: new RelativePoint(0.5 - (cos / 2), 0.5 - (sin / 2), RelativeUnit.Relative),
            endPoint: new RelativePoint(0.5 + (cos / 2), 0.5 + (sin / 2), RelativeUnit.Relative));
    }

    /// <summary>A linear gradient with several stops at <paramref name="angle"/> degrees (0 = left to right).</summary>
    public static IImmutableBrush LinearGradient(double angle, params (Color Color, double Offset)[] stops)
    {
        var (sin, cos) = Math.SinCos(angle * Math.PI / 180);
        return new ImmutableLinearGradientBrush(
            stops.Select(s => new ImmutableGradientStop(s.Offset, s.Color)).ToArray(),
            startPoint: new RelativePoint(0.5 - (cos / 2), 0.5 - (sin / 2), RelativeUnit.Relative),
            endPoint: new RelativePoint(0.5 + (cos / 2), 0.5 + (sin / 2), RelativeUnit.Relative));
    }

    /// <summary>The signed angle in degrees from <paramref name="from"/> to <paramref name="to"/>, as WPF's Vector.AngleBetween.</summary>
    public static double AngleBetween(Vector from, Vector to) =>
        Math.Atan2((from.X * to.Y) - (from.Y * to.X), (from.X * to.X) + (from.Y * to.Y)) * 180 / Math.PI;

    /// <summary>The body (or moon) with this id from the catalog.</summary>
    public static CelestialBody Body(string id) => SolarSystemCatalog.Body(id);

    /// <summary>A rotation by <paramref name="degrees"/> about the origin (WPF's RotateTransform).</summary>
    public static Matrix Rotation(double degrees) => Matrix.CreateRotation(degrees * Math.PI / 180);

    /// <summary>A rotation by <paramref name="degrees"/> about (<paramref name="x"/>, <paramref name="y"/>), as WPF's RotateTransform.</summary>
    public static Matrix Rotation(double degrees, double x, double y) =>
        Matrix.CreateTranslation(-x, -y) * Matrix.CreateRotation(degrees * Math.PI / 180) * Matrix.CreateTranslation(x, y);

    /// <summary>An ellipse geometry by centre and radii, as WPF's EllipseGeometry.</summary>
    public static EllipseGeometry Ellipse(Point center, double radiusX, double radiusY) =>
        new(new Rect(center.X - radiusX, center.Y - radiusY, 2 * radiusX, 2 * radiusY));

    /// <summary>A radial gradient from the centre (offset 0) to the edge (offset 1) of what it fills.</summary>
    public static IImmutableBrush RadialGradient(params (Color Color, double Offset)[] stops)
    {
        var brush = new RadialGradientBrush();
        foreach (var (color, offset) in stops)
        {
            brush.GradientStops.Add(new GradientStop(color, offset));
        }

        return brush.ToImmutable();
    }

    public static StreamGeometry Polygon(params Point[] points)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], true);
            for (var i = 1; i < points.Length; i++)
            {
                ctx.LineTo(points[i]);
            }

            ctx.EndFigure(true);
        }

        return geometry;
    }

    /// <summary>A small arrow head at <paramref name="tip"/> pointing along <paramref name="direction"/>.</summary>
    public static void DrawArrowHead(DrawingContext dc, IBrush brush, Point tip, Vector direction, double size)
    {
        if (direction.Length < 1e-9)
        {
            return;
        }

        direction /= direction.Length;
        var normal = new Vector(-direction.Y, direction.X);
        var back = tip - (direction * size);
        dc.DrawGeometry(brush, null, Polygon(tip, back + (normal * size * 0.55), back - (normal * size * 0.55)));
    }

    /// <summary>Copies premultiplied BGRA pixels (one <see cref="int"/> each) into a bitmap of the same size.</summary>
    public static void WritePixels(WriteableBitmap bitmap, int[] pixels, int width, int height) =>
        WritePixels(bitmap, MemoryMarshal.Cast<int, uint>(pixels.AsSpan()), width, height);

    /// <summary>Copies premultiplied BGRA pixels (one <see cref="uint"/> each) into a bitmap of the same size.</summary>
    public static void WritePixels(WriteableBitmap bitmap, uint[] pixels, int width, int height) =>
        WritePixels(bitmap, pixels.AsSpan(), width, height);

    /// <summary>
    /// Draws all of <paramref name="bitmap"/> into <paramref name="destination"/>. DrawImage's source rectangle is in
    /// pixels while a bitmap's Size is in DIPs, so DrawImage(bitmap, rect) shows only the top-left part of a bitmap made
    /// at more than 96 DPI (a high-DPI screen's).
    /// </summary>
    public static void DrawWhole(DrawingContext dc, Bitmap bitmap, Rect destination) =>
        dc.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), destination);

    /// <summary>
    /// Draws once into a bitmap <paramref name="width"/> × <paramref name="height"/> DIPs big, at <paramref name="scale"/>
    /// device pixels per DIP (what WPF does with a DrawingVisual and RenderTargetBitmap).
    /// </summary>
    public static RenderTargetBitmap RenderToBitmap(double width, double height, double scale, Action<PaintContext> draw)
    {
        var pixels = new PixelSize(Math.Max(1, (int)Math.Ceiling(width * scale)), Math.Max(1, (int)Math.Ceiling(height * scale)));
        // A 96 DPI bitmap with the scale pushed by hand, so DrawImage(bitmap, rect) shows all of it (see DrawWhole).
        var bitmap = new RenderTargetBitmap(pixels, new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext(true))
        using (var dc = new PaintContext(context))
        {
            dc.PushTransform(Matrix.CreateScale(scale, scale));
            draw(dc);
        }

        return bitmap;
    }

    private static unsafe void WritePixels(WriteableBitmap bitmap, ReadOnlySpan<uint> pixels, int width, int height)
    {
        using var buffer = bitmap.Lock();
        var source = MemoryMarshal.AsBytes(pixels);
        var rowBytes = width * 4;
        for (var y = 0; y < height; y++)
        {
            var target = new Span<byte>((byte*)buffer.Address + (y * buffer.RowBytes), rowBytes);
            source.Slice(y * rowBytes, rowBytes).CopyTo(target);
        }
    }
}
