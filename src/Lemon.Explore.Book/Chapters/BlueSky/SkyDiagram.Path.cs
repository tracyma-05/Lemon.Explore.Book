using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.BlueSky;

/// <summary>
/// Path mode: a slice through the Earth and its air, the air drawn many times too thick, coloured everywhere by the
/// sunlight that reaches that point: white high up, yellow, orange and red low down where the light has come a long
/// way through the air, dark in the Earth's shadow. The ray that reaches you is drawn over it, short at noon and long
/// at sunset; after sunset the light skims over you and lights the sky above. Sparks along the ray are light
/// bounced out of it, blue first.
/// </summary>
public sealed partial class SkyDiagram
{
    /// <summary>The air drawn: the lowest 50 km, where nearly all of it is.</summary>
    internal const double PathAirShown = 50_000;

    private const int PathPixel = 3;
    private const int MaxSparkles = 140;

    private WriteableBitmap? _pathBitmap;
    private byte[] _pathPixels = [];
    private (double Altitude, double Haze, double Width, double Height, int Side) _pathDrawn;
    private readonly List<Sparkle> _sparkles = [];
    private double _sparkleDebt;

    private struct Sparkle
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        public double Age;
        public Color Colour;
    }

    /// <summary>The air's thickness on screen against the Earth's radius, against the real thickness shown.</summary>
    internal double PathExaggeration() => PathAir() / PathRadius() / (PathAirShown / SkyOptics.EarthRadius);

    private Point PathObserver() => new(Math.Round(ActualWidth / 2), Math.Round(ActualHeight * 0.64));

    private double PathRadius() => ActualWidth * 1.2;

    private double PathAir() => Math.Round(ActualHeight * 0.2);

    private Point PathCentre() => PathObserver() + new Vector(0, PathRadius());

    /// <summary>West (right) in the afternoon, east (left) in the morning.</summary>
    private int PathSide => _hour >= 12 ? 1 : -1;

    /// <summary>The direction to the Sun on screen.</summary>
    private Vector PathSun()
    {
        var a = Altitude * Deg;
        return new Vector(PathSide * Math.Cos(a), -Math.Sin(a));
    }

    /// <summary>The sunlight reaching a point on screen, or nothing in the shadow, the ground or space.</summary>
    private (LinearRgb Light, double Height, bool Shadow, bool InAir) PathLightAt(Point p, Vector sun, double haze)
    {
        var (column, height, shadow, inAir) = PathColumnAt(p, sun);
        return inAir && !shadow ? (Through(column, haze), height, false, true) : (default, height, shadow, inAir);
    }

    /// <summary>
    /// The real air between a point on screen and the Sun, at the point's real height. Where the Sun is above the
    /// point's horizon the angle on screen is the real one (the Sun is drawn at its real altitude). Where it is below,
    /// the air drawn thick would put the horizon in the wrong place, so the ray on screen is matched to the real ray
    /// that comes as close to the ground, which keeps the shadow and the reddening where they are drawn.
    /// </summary>
    private (AirColumn Column, double Height, bool Shadow, bool InAir) PathColumnAt(Point p, Vector sun)
    {
        Vector d = p - PathCentre();
        var r = d.Length;
        var radius = PathRadius();
        var air = PathAir();
        var v = r - radius;
        if (v < 0 || v > air) return (default, 0, false, false);

        var height = v / air * PathAirShown;
        var along = (d.X * sun.X) + (d.Y * sun.Y);
        var lowest = Math.Abs((d.X * sun.Y) - (d.Y * sun.X)) - radius;

        // Heading down towards the Sun and passing below the ground: in the Earth's shadow.
        if (along < 0 && lowest < 0) return (default, height, true, true);

        if (along >= 0) return (SkyRenderer.ColumnToSpace(height, along / r), height, false, true);

        var lowestReal = Math.Clamp(lowest / air * PathAirShown, 0, height);
        var ratio = (SkyOptics.EarthRadius + lowestReal) / (SkyOptics.EarthRadius + height);
        return (SkyRenderer.ColumnToSpace(height, -Math.Sqrt(Math.Max(0, 1 - (ratio * ratio)))), height, false, true);
    }

    /// <summary>
    /// The air between a point on the drawn ray and the Sun. While the Sun is up this is the real ray that reaches you,
    /// at the point's real height: drawn at its true angle in air drawn far too thick, the ray stays low for much
    /// longer on screen than it really does, so its colour goes by height, red near you and white high up, as the
    /// light you see comes in. Once the Sun is down, as anywhere else (<see cref="PathColumnAt"/>).
    /// </summary>
    private (AirColumn Column, bool Lit) RayColumnAt(Point p, Vector sun, bool reachesYou)
    {
        if (!reachesYou)
        {
            var (column, _, shadow, inAir) = PathColumnAt(p, sun);
            return (column, inAir && !shadow);
        }

        var height = Math.Clamp((((Vector)(p - PathCentre())).Length - PathRadius()) / PathAir() * PathAirShown, 0, PathAirShown);
        var ratio = SkyOptics.EarthRadius * Math.Cos(Altitude * Deg) / (SkyOptics.EarthRadius + height);
        return (SkyRenderer.ColumnToSpace(height, Math.Sqrt(Math.Max(0, 1 - (ratio * ratio)))), true);
    }

    /// <summary>The ray itself is drawn brighter than the air round it, so its colour reads even where it is dim.</summary>
    private static Color RayColour(LinearRgb light) =>
        Lerp(Colors.Black, SunColour(light), Math.Clamp(0.6 + (light.Luminance * 2), 0, 1));

    private static Color PathColour(LinearRgb light) =>
        Lerp(Colors.Black, SunColour(light), Math.Clamp(0.2 + (light.Luminance * 1.6), 0, 1));

    /// <summary>
    /// The part of the beam drawn coloured: from where it enters the air to you while the Sun is up; once it is down,
    /// the ray that just skims the Earth, from where it enters the air to where it leaves it.
    /// </summary>
    private (Point From, Point To, bool ReachesYou) PathRay()
    {
        var sun = PathSun();
        var observer = PathObserver();
        var centre = PathCentre();
        var top = PathRadius() + PathAir();
        if (Altitude > 0)
        {
            var d = observer - centre;
            var b = (d.X * sun.X) + (d.Y * sun.Y);
            var t = -b + Math.Sqrt((b * b) - ((d.X * d.X) + (d.Y * d.Y) - (top * top)));
            return (observer + (sun * t), observer, true);
        }

        var normal = new Vector(-sun.Y, sun.X);
        if (normal.Y > 0) normal = -normal;
        var tangent = centre + (normal * (PathRadius() + 3)); // just clear of the ground, so it is lit all along
        var half = Math.Sqrt((top * top) - (PathRadius() * PathRadius()));
        return (tangent + (sun * half), tangent - (sun * half), false);
    }

    private void UpdatePathBitmap(double width, double height)
    {
        var key = (Math.Round(Altitude, 2), Haze, width, height, PathSide);
        if (_pathBitmap is not null && key == _pathDrawn) return;
        _pathDrawn = key;

        var w = Math.Max(1, (int)Math.Ceiling(width / PathPixel));
        var h = Math.Max(1, (int)Math.Ceiling(height / PathPixel));
        if (_pathBitmap is null || _pathBitmap.PixelSize.Width != w || _pathBitmap.PixelSize.Height != h)
        {
            _pathBitmap = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            _pathPixels = new byte[w * h * 4];
        }

        var pixels = _pathPixels;
        var sun = PathSun();
        var haze = Haze;
        var firstRow = Math.Max(0, (int)((PathObserver().Y - PathAir() - 4) / PathPixel));
        Array.Clear(pixels);
        Parallel.For(firstRow, h, row =>
        {
            for (var column = 0; column < w; column++)
            {
                var p = new Point((column + 0.5) * PathPixel, (row + 0.5) * PathPixel);
                var (light, altitude, shadow, inAir) = PathLightAt(p, sun, haze);
                if (!inAir) continue;

                // Thicker air low down glows more.
                var alpha = 0.22 + (0.6 * Math.Exp(-altitude / 9_000));
                var colour = shadow ? Path.Shadow : PathColour(light);
                if (shadow) alpha *= 0.8;
                var i = ((row * w) + column) * 4;
                pixels[i] = (byte)(colour.B * alpha);
                pixels[i + 1] = (byte)(colour.G * alpha);
                pixels[i + 2] = (byte)(colour.R * alpha);
                pixels[i + 3] = (byte)(255 * alpha);
            }
        });

        SpaceDrawing.WritePixels(_pathBitmap, pixels, w, h);
    }

    private void StepSparkles(double dt)
    {
        if (dt <= 0 || ActualWidth < 200) return;
        for (var i = _sparkles.Count - 1; i >= 0; i--)
        {
            var s = _sparkles[i];
            s.X += s.Vx * dt;
            s.Y += s.Vy * dt;
            s.Age += dt;
            if (s.Age > 1.6) _sparkles.RemoveAt(i);
            else _sparkles[i] = s;
        }

        var (from, to, reachesYou) = PathRay();
        var sun = PathSun();
        _sparkleDebt += 45 * dt;
        while (_sparkleDebt >= 1 && _sparkles.Count < MaxSparkles)
        {
            _sparkleDebt -= 1;
            var p = from + ((to - from) * _random.NextDouble());
            var (column, lit) = RayColumnAt(p, sun, reachesYou);
            if (!lit || Through(column, Haze).Luminance < 0.01) continue;

            // The colour bounced out here: the light left here times how strongly each colour scatters. Bluish high up
            // where the light is still white; orange low down where the blue is gone.
            var scattered = SkyOptics.ToRgb(SkyOptics.Wavelengths.Select(nm =>
                Math.Exp(-SkyOptics.OpticalDepth(nm, column, Haze)) * SkyOptics.Rayleigh(nm)).ToArray());

            var angle = _random.NextDouble() * 2 * Math.PI;
            var speed = 35 + (_random.NextDouble() * 40);
            _sparkles.Add(new Sparkle
            {
                X = p.X,
                Y = p.Y,
                Vx = speed * Math.Cos(angle),
                Vy = speed * Math.Sin(angle),
                Colour = Lerp(SkyOptics.Hue(scattered), Colors.White, 0.15),
            });
        }

        _sparkleDebt = Math.Min(_sparkleDebt, 1);
    }

    private void DrawPath(PaintContext dc, double now)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));
        _stars.Draw(dc, width, height, RenderScale(), now, 0.6);
        if (width < 200) return;

        var observer = PathObserver();
        var centre = PathCentre();
        var radius = PathRadius();
        var air = PathAir();
        var sun = PathSun();

        // The Earth, lit on the Sun's side.
        var earth = new ImmutableLinearGradientBrush(
        [
            new ImmutableGradientStop(0, Color.FromRgb(0x2E, 0x5C, 0x3E)),
            new ImmutableGradientStop(0.4, Color.FromRgb(0x1F, 0x40, 0x2C)),
            new ImmutableGradientStop(0.6, Color.FromRgb(0x08, 0x10, 0x0C)),
            new ImmutableGradientStop(1, Color.FromRgb(0x05, 0x09, 0x07)),
        ],
            startPoint: new RelativePoint(centre + (sun * radius), RelativeUnit.Absolute),
            endPoint: new RelativePoint(centre - (sun * radius), RelativeUnit.Absolute));
        UpdatePathBitmap(width, height);
        if (_pathBitmap is not null) SpaceDrawing.DrawSmooth(dc, _pathBitmap, new Rect(0, 0, width, height));

        // Light bounced out of the ray, behind the Earth where it flies down past it.
        foreach (var s in _sparkles)
        {
            var alpha = Math.Clamp(1 - (s.Age / 1.6), 0, 1);
            dc.DrawEllipse(new SolidColorBrush(WithAlpha(s.Colour, alpha)), null, new Point(s.X, s.Y), 2.2, 2.2);
        }

        dc.DrawEllipse(earth, Path.EarthEdge, centre, radius, radius);
        dc.DrawEllipse(null, Path.AirEdge, centre, radius + air, radius + air);

        // Your horizon, and the way overhead for comparison.
        dc.DrawLine(Path.Horizon, observer + new Vector(-170, 0), observer + new Vector(170, 0));
        var overheadTop = observer + new Vector(0, -air);
        dc.DrawLine(Path.Overhead, overheadTop, observer + new Vector(0, -14));
        var overhead = _labels.Text(Loc("Sky_Path_Overhead"), 11, Ink.Dim);
        _labels.DrawTextPill(dc, Loc("Sky_Path_Overhead"), new Point(observer.X - overhead.Width - 18, overheadTop.Y - overhead.Height - 8), 11, Ink.Dim);
        AddPillTarget("overhead", Loc("Sky_Path_Overhead"), new Point(observer.X - overhead.Width - 18, overheadTop.Y - overhead.Height - 8), 11, Ink.Dim);

        // The ray of sunlight, coloured as it goes.
        var (from, to, reachesYou) = PathRay();
        if (Glows("ray")) dc.DrawLine(ExploreTargets.GlowPen(now, 12), from, to);
        const int segments = 48;
        var previous = from;
        for (var i = 1; i <= segments; i++)
        {
            var point = from + ((to - from) * (i / (double)segments));
            var middle = previous + ((point - previous) / 2);
            var (column, lit) = RayColumnAt(middle, sun, reachesYou);
            var colour = lit ? RayColour(Through(column, Haze)) : Color.FromRgb(0x30, 0x10, 0x08);
            dc.DrawLine(new ImmutablePen(((IBrush)new SolidColorBrush(colour)).ToImmutable(), 4, null, PenLineCap.Round, PenLineJoin.Miter), previous, point);
            previous = point;
        }

        if (reachesYou) DrawArrowHead(dc, new SolidColorBrush(RayColour(Through(RayColumnAt(to + (sun * 6), sun, true).Column, Haze))), to + (sun * 10), -sun, 12);

        // Beyond the air, the light comes white from the Sun.
        var sunAt = SunIconPosition();
        dc.DrawLine(Path.Space, sunAt - (sun * 26), from);
        dc.DrawEllipse(Scatter.SunGlow, null, sunAt, 50, 50);
        dc.DrawEllipse(Scatter.SunDisc, null, sunAt, 20, 20);
        _labels.DrawCentered(dc, "Sky_Label_SunShort", sunAt + new Vector(0, 26), 12, Ink.Gold);
        _targets.Add("sun", sunAt, 20);

        DrawObserver(dc, observer);

        // Labels: how far the light came, the air drawn thick, the Earth's shadow.
        var mid = from + ((to - from) * 0.45);
        var pathLabel = reachesYou
            ? LocFormat("Sky_Path_Length", SkyOptics.AirMass(Altitude).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
            : Loc("Sky_Path_Grazing");
        var pathText = _labels.Text(pathLabel, 12, Ink.Text);
        var labelAt = mid + new Vector(-pathText.Width / 2, -pathText.Height - 22);
        labelAt = labelAt.WithX(Math.Clamp(labelAt.X, 12, width - pathText.Width - 24));
        labelAt = labelAt.WithY(Math.Max(12, labelAt.Y));
        _labels.DrawTextPill(dc, pathLabel, labelAt, 12, Ink.Text);
        AddPillTarget("ray", pathLabel, labelAt, 12, Ink.Text);

        var airLabelAt = centre + (Rotate(new Vector(0, -1), -PathSide * 15) * (radius + (air * 0.55)));
        DrawCenteredPill(dc, LocFormat("Sky_Path_Air", Math.Round(PathExaggeration())), airLabelAt, 11, Ink.Dim);
        AddCenteredPillTarget("air", LocFormat("Sky_Path_Air", Math.Round(PathExaggeration())), airLabelAt, 11, Ink.Dim);

        var shadowAt = centre + (Rotate(new Vector(0, -1), -PathSide * 12) * (radius + (air * 0.07)));
        if (Altitude < 0 && PathLightAt(shadowAt, sun, Haze).Shadow)
        {
            DrawCenteredPill(dc, Loc("Sky_Label_EarthShadow"), shadowAt, 11, Ink.Text);
            AddCenteredPillTarget("earthshadow", Loc("Sky_Label_EarthShadow"), shadowAt, 11, Ink.Text);
        }
    }

    /// <summary>The Sun drawn off towards its direction, kept on screen.</summary>
    private Point SunIconPosition()
    {
        var observer = PathObserver();
        var sun = PathSun();
        var distance = Math.Min(ActualWidth * 0.44, (ActualWidth / 2) - 40);
        var p = observer + (sun * distance);
        return new Point(Math.Clamp(p.X, 40, ActualWidth - 40), Math.Clamp(p.Y, 40, ActualHeight - 140));
    }

    private void DrawObserver(PaintContext dc, Point foot)
    {
        dc.DrawLine(Path.Person, foot, foot + new Vector(0, -10));
        dc.DrawLine(Path.Person, foot + new Vector(0, -10), foot + new Vector(-4, -4));
        dc.DrawLine(Path.Person, foot + new Vector(0, -10), foot + new Vector(4, -4));
        dc.DrawEllipse(Path.PersonFill, null, foot + new Vector(0, -14), 3.5, 3.5);
        var you = _labels.Get("Sky_Path_You", 12, Ink.Gold, true);
        _labels.DrawPill(dc, "Sky_Path_You", foot + new Vector(-you.Width - 24, -32), 12, Ink.Gold);
        _targets.Add("you", foot + new Vector(0, -9), 10);
        var pill = _labels.Get("Sky_Path_You", 12, Ink.Gold);
        var pillAt = foot + new Vector(-you.Width - 24, -32);
        _targets.AddBox("you", new Rect(pillAt.X, pillAt.Y, pill.Width + 12, pill.Height + 4));
    }

    private static Vector Rotate(Vector v, double degrees)
    {
        var a = degrees * Deg;
        return new Vector((v.X * Math.Cos(a)) - (v.Y * Math.Sin(a)), (v.X * Math.Sin(a)) + (v.Y * Math.Cos(a)));
    }

    private static class Path
    {
        public static readonly Color Shadow = Color.FromRgb(0x10, 0x18, 0x30);
        public static readonly IPen EarthEdge = MakePen(Solid(120, 0x7F, 0xB8, 0x8F), 1.2);
        public static readonly IPen AirEdge = MakePen(Solid(50, 255, 255, 255), 1, dashed: true);
        public static readonly IPen Horizon = MakePen(Solid(90, 255, 255, 255), 1, dashed: true);
        public static readonly IPen Overhead = MakePen(Solid(150, 255, 255, 255), 2, dashed: true);
        public static readonly IPen Space = MakePen(Solid(200, 255, 250, 235), 3);
        public static readonly IPen Person = new ImmutablePen(((IBrush)Solid(255, 0xFF, 0xD3, 0x6B)).ToImmutable(), 2, null, PenLineCap.Round, PenLineJoin.Miter);
        public static readonly IBrush PersonFill = Solid(255, 0xFF, 0xD3, 0x6B);
    }
}
