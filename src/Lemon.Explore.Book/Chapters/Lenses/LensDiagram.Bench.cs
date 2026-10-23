using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Chapters.Rainbow;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Lenses;

/// <summary>
/// The bench: a candle on the axis, the lens, the three rays every textbook draws (or many real rays, or sunlight), the
/// image where they meet (or seem to come from), the focal points, a screen and the picture on it.
/// </summary>
public sealed partial class LensDiagram
{
    /// <summary>The bench shown, cm from the lens: the candle's side, the screen's side.</summary>
    internal const double BenchLeft = -72;

    internal const double BenchRight = 92;

    /// <summary>Heights are drawn this many times larger than lengths.</summary>
    private const double Exaggeration = 4;

    /// <summary>The lens's clear half-height, cm.</summary>
    internal const double LensAperture = 4;

    /// <summary>The screen's half-height, cm.</summary>
    private const double ScreenHalf = 6;

    /// <summary>How much the glass's spread of colours is exaggerated when the rays are shown in colour.</summary>
    internal const double ColourSpread = 6;

    /// <summary>The Sun's angular diameter, radians.</summary>
    private const double SunAngle = 0.0093;

    private readonly ScreenPicture _screenPicture = new();
    private BenchView _bench;

    /// <summary>The bench's scale: the lens's middle on screen, pixels per cm across and up.</summary>
    private readonly record struct BenchView(double X0, double Y0, double Sx, double Sy, Rect Stage, Rect Inset)
    {
        public Point P(double x, double y) => new(X0 + (x * Sx), Y0 - (y * Sy));

        public double Cm(double px) => (px - X0) / Sx;

        public double CmUp(double py) => (Y0 - py) / Sy;
    }

    /// <summary>The focal length with its sign: negative for a concave lens.</summary>
    private double SignedFocal => LensKind == LensKind.Convex ? FocalLength : -FocalLength;

    private BenchView BenchLayout()
    {
        var stage = Stage();
        var sx = stage.Width / (BenchRight - BenchLeft);
        var sy = Math.Min(sx * Exaggeration, (stage.Height - 30) / 17);
        var y0 = stage.Top + (stage.Height * 0.54);

        // The screen's picture in the top right corner, above the screen's top if it fits.
        var room = y0 - (ScreenHalf * sy) - stage.Top - 34;
        var scale = Math.Clamp(room / ScreenPicture.Height, 0.6, 1);

        // Where it does not fit (a phone's page, the walkthrough's subtitles taking the bottom), the bench moves down
        // to clear the picture, as far as leaves room under the axis for the screen's stand and the u and v labels;
        // past that the picture gets smaller instead of covering the lens.
        var lowest = stage.Bottom - (ScreenHalf * sy) - 60;
        var clear = stage.Top + 24 + (ScreenPicture.Height * scale) + 34 + (ScreenHalf * sy);
        if (clear > lowest)
        {
            var fits = (lowest - (ScreenHalf * sy) - 34 - stage.Top - 24) / ScreenPicture.Height;
            scale = Math.Clamp(fits, 0.3, scale);
            clear = stage.Top + 24 + (ScreenPicture.Height * scale) + 34 + (ScreenHalf * sy);
        }

        y0 = Math.Max(y0, Math.Min(clear, lowest));
        var inset = new Rect(stage.Right - (ScreenPicture.Width * scale) - 16, stage.Top + 24, ScreenPicture.Width * scale, ScreenPicture.Height * scale);
        return new BenchView(stage.Left - (BenchLeft * sx), y0, sx, sy, stage, inset);
    }

    private void DrawBenchMode(PaintContext dc, double now)
    {
        var v = _bench = BenchLayout();
        var f = SignedFocal;
        var u = ObjectDistance;
        var h = ObjectHeight;
        var sunlight = RayStyle == RayStyle.Sunlight;
        var screen = ShowScreen ? ScreenDistance : double.NaN;
        var xEnd = ShowScreen ? screen : BenchRight;

        // The optical axis, and the bench's ruler marks every 10 cm.
        var axisGeometry = Line([v.P(BenchLeft, 0), v.P(BenchRight, 0)]);
        if (Glows("axis")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now), axisGeometry);
        dc.DrawGeometry(null, Ink.Axis, axisGeometry);
        for (var x = -70; x <= 90; x += 10)
        {
            var at = v.P(x, 0);
            dc.DrawLine(Ink.Faint, new Point(at.X, at.Y - 3), new Point(at.X, at.Y + 3));
        }

        _targets.Add("axis", v.P(BenchLeft + 6, 0), 8);

        DrawFocalMarks(dc, v, f);
        var shape = LensModel.Shape(f, LensAperture);
        DrawLens(dc, v, shape, now);

        if (sunlight)
        {
            DrawSunlight(dc, v, shape, xEnd, now);
        }
        else
        {
            if (RayStyle == RayStyle.Fan) DrawRayFan(dc, v, shape, u, h, xEnd);
            else DrawPrincipalRays(dc, v, f, u, h, xEnd, now);
        }

        // The screen before the image: a real image on the screen shows on it.
        if (ShowScreen) DrawScreen(dc, v, f, u, h, screen, sunlight, now);
        if (!sunlight)
        {
            DrawCandle(dc, v.P(-u, 0), h * v.Sy, CandleWidth(h, v), ghost: false, now);
            AddBoxTarget("object", CandleBox(v.P(-u, 0), h * v.Sy, CandleWidth(h, v)));
            DrawImage(dc, v, f, u, h, screen, now);
        }
        DrawDistances(dc, v, f, u, sunlight);
    }

    #region the lens and its marks

    private void DrawLens(PaintContext dc, BenchView v, LensShape shape, double now)
    {
        var outline = new List<Point>();
        const int Steps = 24;
        for (var i = 0; i <= Steps; i++)
        {
            var y = -shape.Aperture + (2 * shape.Aperture * i / Steps);
            outline.Add(v.P(shape.FrontAt(y), y));
        }

        for (var i = Steps; i >= 0; i--)
        {
            var y = -shape.Aperture + (2 * shape.Aperture * i / Steps);
            outline.Add(v.P(shape.BackAt(y), y));
        }

        var glass = Line(outline, closed: true, filled: true);
        var top = v.P(0, shape.Aperture);
        var bottom = v.P(0, -shape.Aperture);
        var fill = SpaceDrawing.LinearGradient(Color.FromArgb(70, Ink.Glass.R, Ink.Glass.G, Ink.Glass.B), Color.FromArgb(28, Ink.Glass.R, Ink.Glass.G, Ink.Glass.B), 0);
        dc.DrawGeometry(fill, FadedPen(Ink.Glass, 0.85, 1.4), glass);

        // The lens's plane, where the thin-lens rays bend, a little past the glass.
        dc.DrawLine(FadedPen(Ink.Glass, 0.25, 1, dashed: true), v.P(0, shape.Aperture + 1.6), top);
        dc.DrawLine(FadedPen(Ink.Glass, 0.25, 1, dashed: true), bottom, v.P(0, -shape.Aperture - 1.6));

        var box = new Rect(v.P(Math.Min(shape.FrontAt(shape.Aperture), shape.Front) - 0.4, shape.Aperture), v.P(Math.Max(shape.BackAt(shape.Aperture), shape.Back) + 0.4, -shape.Aperture));
        AddBoxTarget("lens", box);
        if (ShowLabels)
        {
            var key = LensKind == LensKind.Convex ? "Lens_Label_Convex" : "Lens_Label_Concave";
            DrawPill(dc, Loc(key), new Point(top.X, top.Y - 26), Ink.Glass);
        }

        // The middle of the lens: rays through it go straight on.
        var centre = v.P(0, 0);
        dc.DrawEllipse(Solid(255, Ink.Text.R, Ink.Text.G, Ink.Text.B), null, centre, 2.5, 2.5);
        if (ShowLabels) DrawText(dc, Loc("Lens_Label_O"), new Point(centre.X + 6, centre.Y + 11), Ink.Dim, 11);
        _targets.Add("centre", centre, 7);
        _ = now;
    }

    private void DrawFocalMarks(PaintContext dc, BenchView v, double f)
    {
        var g = Math.Abs(f);
        foreach (var side in new[] { -1, 1 })
        {
            var focus = v.P(side * g, 0);
            var twice = v.P(side * 2 * g, 0);
            dc.DrawEllipse(Solid(255, Ink.Orange.R, Ink.Orange.G, Ink.Orange.B), null, focus, 4, 4);
            dc.DrawEllipse(null, FadedPen(Ink.Orange, 0.8, 1.2), twice, 3.5, 3.5);
            if (ShowLabels)
            {
                DrawText(dc, Loc("Lens_Label_F"), new Point(focus.X, focus.Y + 14), Ink.Orange, 12, TextAnchor.Center, bold: true);
                DrawText(dc, Loc("Lens_Label_2F"), new Point(twice.X, twice.Y + 14), Ink.Orange, 11, TextAnchor.Center);
            }

            _targets.Add("twof", twice, 7);
            _targets.Add("focus", focus, 8);
        }
    }

    #endregion

    #region rays

    /// <summary>
    /// The three easy rays from the flame's tip: along the axis then through F; through the middle, straight on; through
    /// F then along the axis. Beyond a concave lens they spread as if from a focal point; a virtual image is where their
    /// lines, traced back, meet.
    /// </summary>
    private void DrawPrincipalRays(PaintContext dc, BenchView v, double f, double u, double h, double xEnd, double now)
    {
        var tip = new Point(-u, h);
        var image = LensModel.ImageDistance(f, u);
        var virtualImage = image < 0;

        void Ray(string id, Color colour, double y, double slope, Point? construction)
        {
            var atLens = new Point(0, y);
            var end = new Point(xEnd, y + (slope * xEnd));
            var path = Line([v.P(tip.X, tip.Y), v.P(atLens.X, atLens.Y), v.P(end.X, end.Y)]);
            if (Glows(id)) dc.DrawGeometry(null, ExploreTargets.GlowPen(now), path);
            dc.DrawGeometry(null, FadedPen(colour, 0.95, 1.8), path);

            // Arrows show which way the light goes.
            var mid = v.P(tip.X * 0.45, tip.Y + ((y - tip.Y) * 0.55));
            Vector direction = v.P(atLens.X, atLens.Y) - v.P(tip.X, tip.Y);
            if (direction.Length > 1) DrawArrowHead(dc, Solid(255, colour.R, colour.G, colour.B), mid, direction / direction.Length, 8);
            var after = v.P(xEnd * 0.55, y + (slope * xEnd * 0.55));
            Vector outDirection = v.P(end.X, end.Y) - v.P(atLens.X, atLens.Y);
            if (outDirection.Length > 1) DrawArrowHead(dc, Solid(255, colour.R, colour.G, colour.B), after, outDirection / outDirection.Length, 8);

            if (virtualImage && double.IsFinite(image))
            {
                dc.DrawLine(FadedPen(colour, 0.55, 1.3, dashed: true), v.P(0, y), v.P(image, y + (slope * image)));
            }

            if (construction is { } from) dc.DrawLine(FadedPen(colour, 0.45, 1.2, dashed: true), v.P(from.X, from.Y), v.P(tip.X, tip.Y));
            _targets.Add(id, v.P(tip.X * 0.7, tip.Y + ((y - tip.Y) * 0.3)), 9);
        }

        // 1: parallel to the axis, then bent through F (or away from it).
        Ray("ray1", Ink.Gold, h, -h / f, null);

        // 2: through the middle, straight on.
        Ray("ray2", Ink.Cyan, 0, -h / u, null);

        // 3: towards F (the near one for a convex lens, the far one for a concave), then parallel.
        if (Math.Abs(u - f) > 0.25)
        {
            var y3 = h * f / (f - u);
            Point? construction = f > 0 && u < f ? new Point(-f, 0) : null;
            Ray("ray3", Ink.Pink, y3, 0, construction);
            if (f < 0)
            {
                // Where the concave lens's third ray was heading: the far focal point.
                dc.DrawLine(FadedPen(Ink.Pink, 0.4, 1.2, dashed: true), v.P(0, y3), v.P(-f, 0));
            }
        }
    }

    /// <summary>Many rays from the flame's tip through the real glass lens (Snell's law at both faces), in colour if asked.</summary>
    private void DrawRayFan(PaintContext dc, BenchView v, LensShape shape, double u, double h, double xEnd)
    {
        var tip = new Point(-u, h);
        foreach (var (shapeFor, colour) in ColourLenses(shape))
        {
            for (var k = 0; k < 11; k++)
            {
                var y = -shape.Aperture * 0.94 + (shape.Aperture * 1.88 * k / 10);
                var aim = new Point(shape.FrontAt(y), y);
                if (LensModel.Trace(shapeFor, tip, aim - tip) is not { } ray) continue;
                var end = Extend(ray, xEnd);
                dc.DrawLine(FadedPen(ShowColours ? Ink.Ivory : colour, ShowColours ? 0.3 : 0.55, 1.1), v.P(tip.X, tip.Y), v.P(ray.Entry.X, ray.Entry.Y));
                dc.DrawGeometry(null, FadedPen(colour, 0.6, 1.1), Line([v.P(ray.Entry.X, ray.Entry.Y), v.P(ray.Exit.X, ray.Exit.Y), v.P(end.X, end.Y)]));
            }
        }

        // Where the edge rays and the middle rays come together: not quite the same place.
        var image = LensModel.ImageDistance(SignedFocal, u);
        if (image > 0 && image < BenchRight)
        {
            _targets.Add("aberration", v.P(image, LensModel.Magnification(SignedFocal, u) * h), 12);
            if (ShowColours) _targets.Add("chromatic", v.P(image, LensModel.Magnification(SignedFocal, u) * h - (1.5 / Exaggeration)), 10);
        }
    }

    /// <summary>Parallel light from the Sun through the real lens: the edge rays cross the axis nearer the lens.</summary>
    private void DrawSunlight(PaintContext dc, BenchView v, LensShape shape, double xEnd, double now)
    {
        var sun = v.P(BenchLeft + 4, 0);
        var glow = SpaceDrawing.RadialGradient((Color.FromArgb(220, 0xFF, 0xE8, 0x9A), 0), (Color.FromArgb(0, 0xFF, 0xC0, 0x40), 1));
        dc.DrawEllipse(glow, null, sun, 26 + (2 * Math.Sin(now * 2)), 26 + (2 * Math.Sin(now * 2)));
        dc.DrawEllipse(Solid(255, 0xFF, 0xF0, 0xB8), null, sun, 11, 11);
        if (ShowLabels) DrawPill(dc, Loc("Lens_Label_Sunlight"), new Point(sun.X + 6, sun.Y - 44), Ink.Gold);

        foreach (var (shapeFor, colour) in ColourLenses(shape))
        {
            for (var k = 0; k < 13; k++)
            {
                var y = -shape.Aperture * 0.95 + (shape.Aperture * 1.9 * k / 12);
                var start = new Point(BenchLeft + 10, y);
                if (LensModel.Trace(shapeFor, start, new Vector(1, 0)) is not { } ray)
                {
                    continue;
                }

                var end = Extend(ray, xEnd);
                dc.DrawLine(FadedPen(ShowColours ? Ink.Ivory : colour, ShowColours ? 0.3 : 0.55, 1.1), v.P(start.X, start.Y), v.P(ray.Entry.X, ray.Entry.Y));
                dc.DrawGeometry(null, FadedPen(colour, 0.6, 1.1), Line([v.P(ray.Entry.X, ray.Entry.Y), v.P(ray.Exit.X, ray.Exit.Y), v.P(end.X, end.Y)]));
            }
        }

        if (SignedFocal > 0)
        {
            var edge = LensModel.AxisCrossing(shape, shape.Aperture * 0.95);
            var middle = LensModel.AxisCrossing(shape, 0.3);
            if (double.IsFinite(edge) && double.IsFinite(middle))
            {
                _targets.Add("aberration", v.P((edge + middle) / 2, 0), Math.Max(10, (middle - edge) * v.Sx / 2));
                if (ShowColours) _targets.Add("chromatic", v.P(middle, -0.9), 10);
            }
        }
    }

    /// <summary>The lens for each colour drawn: one white-gold one, or red, green and blue with the spread exaggerated.</summary>
    private IEnumerable<(LensShape Shape, Color Colour)> ColourLenses(LensShape shape)
    {
        if (!ShowColours)
        {
            yield return (shape, Ink.Gold);
            yield break;
        }

        var yellow = LensModel.GlassYellow;
        foreach (var (nm, colour) in new[] { (656.0, Color.FromRgb(0xFF, 0x50, 0x50)), (546.0, Color.FromRgb(0x60, 0xFF, 0x80)), (450.0, Color.FromRgb(0x60, 0x90, 0xFF)) })
        {
            var n = yellow + (ColourSpread * (LensModel.GlassIndex(nm) - yellow));
            yield return (LensModel.WithIndex(shape, n), colour);
        }
    }

    /// <summary>A ray's straight path after the lens, out to x = <paramref name="xEnd"/>.</summary>
    private static Point Extend(LensRay ray, double xEnd)
    {
        if (ray.Out.X <= 1e-9) return ray.Exit + (ray.Out * 40);
        var t = (xEnd - ray.Exit.X) / ray.Out.X;
        return ray.Exit + (ray.Out * Math.Max(0, t));
    }

    #endregion

    #region candle, image and screen

    private static double CandleWidth(double height, BenchView v) => Math.Max(5, 0.32 * Math.Abs(height) * v.Sy);

    private static Rect CandleBox(Point basePoint, double heightPx, double widthPx)
    {
        var top = Math.Min(basePoint.Y, basePoint.Y - heightPx);
        return new Rect(basePoint.X - (widthPx / 2) - 3, top - 2, widthPx + 6, Math.Abs(heightPx) + 4);
    }

    /// <summary>
    /// A candle standing on the axis at <paramref name="basePoint"/>, <paramref name="heightPx"/> high to the flame's tip
    /// (negative: hanging upside down). A ghost is see-through: an image.
    /// </summary>
    private void DrawCandle(PaintContext dc, Point basePoint, double heightPx, double widthPx, bool ghost, double now, bool dashed = false)
    {
        var flicker = 1 + (0.04 * Math.Sin(now * 11)) + (0.03 * Math.Sin(now * 17.3));
        var bodyTop = basePoint.Y - (heightPx * 0.7);
        var body = new Rect(basePoint.X - (widthPx / 2), Math.Min(basePoint.Y, bodyTop), widthPx, Math.Abs(heightPx * 0.7));
        var wax = ghost ? FadedBrush(Ink.Ivory, 0.32) : Solid(255, Ink.Ivory.R, Ink.Ivory.G, Ink.Ivory.B);
        var edge = dashed ? FadedPen(Ink.Ivory, 0.75, 1.2, dashed: true) : ghost ? FadedPen(Ink.Ivory, 0.6, 1) : null;
        dc.DrawRoundedRectangle(dashed ? null : wax, edge, body, Math.Min(3, widthPx / 4), Math.Min(3, widthPx / 4));

        var flameBase = basePoint.Y - (heightPx * 0.73);
        var flameTip = basePoint.Y - (heightPx * (0.73 + (0.27 * flicker)));
        var half = widthPx * 0.42;
        var flame = new StreamGeometry();
        using (var context = flame.Open())
        {
            context.BeginFigure(new Point(basePoint.X, flameTip), true);
            var shoulder = flameTip + ((flameBase - flameTip) * 0.45);
            context.CubicBezierTo(new Point(basePoint.X + half, shoulder), new Point(basePoint.X + half, flameBase), new Point(basePoint.X, flameBase));
            context.CubicBezierTo(new Point(basePoint.X - half, flameBase), new Point(basePoint.X - half, shoulder), new Point(basePoint.X, flameTip));

            context.EndFigure(true);
        }
        if (!ghost)
        {
            var glow = SpaceDrawing.RadialGradient((Color.FromArgb(90, 0xFF, 0xC8, 0x5A), 0), (Color.FromArgb(0, 0xFF, 0xC8, 0x5A), 1));
            var r = Math.Abs(heightPx) * 0.35;
            dc.DrawEllipse(glow, null, new Point(basePoint.X, (flameBase + flameTip) / 2), r, r);
        }

        var flameFill = ghost ? FadedBrush(Ink.Flame, dashed ? 0.25 : 0.6) : Solid(255, 0xFF, 0xD6, 0x6E);
        dc.DrawGeometry(flameFill, dashed ? FadedPen(Ink.Flame, 0.8, 1.1, dashed: true) : null, flame);
    }

    /// <summary>The image: a real one upside down where the rays meet, a virtual one dashed where they seem to come from.</summary>
    private void DrawImage(PaintContext dc, BenchView v, double f, double u, double h, double screen, double now)
    {
        var image = LensModel.ImageDistance(f, u);
        var m = LensModel.Magnification(f, u);
        if (!double.IsFinite(image) || Math.Abs(image) > 2000)
        {
            if (ShowLabels) DrawPill(dc, Loc("Lens_Label_NoImage"), v.P(BenchRight - 22, h + 2.5), Ink.Dim);
            return;
        }

        var height = m * h;
        var real = image > 0;
        var blocked = real && ShowScreen && screen < image - 0.2;
        if (image > BenchRight - 1 || image < BenchLeft + 1 || Math.Abs(height) * v.Sy > v.Stage.Height * 0.9)
        {
            // Off the bench: say where.
            var right = image > 0;
            var at = v.P(right ? BenchRight - 14 : BenchLeft + 14, right ? -3.5 : 6.5);
            if (ShowLabels) DrawTargetPill(dc, LocFormat(right ? "Lens_Label_ImageFarRight" : "Lens_Label_ImageFarLeft", Centimetres(Math.Abs(image))), at, real ? Ink.Gold : Ink.Cyan, real ? "image" : "virtual");
            return;
        }

        var basePoint = v.P(image, 0);
        var widthPx = CandleWidth(height, v);
        DrawCandle(dc, basePoint, height * v.Sy, widthPx, ghost: true, now, dashed: !real || blocked);
        var id = real ? "image" : "virtual";
        AddBoxTarget(id, CandleBox(basePoint, height * v.Sy, widthPx));
        if (!ShowLabels) return;

        var key = !real ? "Lens_Label_VirtualImage" : blocked ? "Lens_Label_BlockedImage" : "Lens_Label_RealImage";
        // Beside the image, clear of the screen it may stand on.
        var labelY = basePoint.Y - (height * v.Sy * 0.5);
        var labelX = real ? basePoint.X + (widthPx / 2) + 40 : basePoint.X - (widthPx / 2) - 40;
        DrawPill(dc, Loc(key), new Point(labelX, labelY), real ? Ink.Gold : Ink.Cyan);
    }

    /// <summary>The screen, the light landing on it, and its picture in the corner.</summary>
    private void DrawScreen(PaintContext dc, BenchView v, double f, double u, double h, double screen, bool sunlight, double now)
    {
        var top = v.P(screen, ScreenHalf);
        var bottom = v.P(screen, -ScreenHalf);
        var card = new Rect(top.X - 3, top.Y, 7, bottom.Y - top.Y);
        dc.DrawRoundedRectangle(Solid(235, 0xE8, 0xE4, 0xDA), MakePen(Solid(255, 0x9A, 0x96, 0x8E), 1), card, 2, 2);
        dc.DrawRectangle(Solid(255, 0x5A, 0x56, 0x50), null, new Rect(top.X - 1.5, bottom.Y, 3, 14));
        dc.DrawRoundedRectangle(Solid(255, 0x5A, 0x56, 0x50), null, new Rect(top.X - 12, bottom.Y + 12, 24, 4), 2, 2);

        // The patch the flame's tip makes on the screen.
        double centre, blur;
        if (sunlight)
        {
            centre = 0;
            blur = LensModel.BlurOnScreen(f, 1e9, LensAperture, screen) + (SunAngle * screen);
        }
        else
        {
            centre = LensModel.HeightOnScreen(h, u, screen);
            blur = LensModel.BlurOnScreen(f, u, LensAperture, screen);
        }

        var low = Math.Max(-ScreenHalf, centre - (blur / 2));
        var high = Math.Min(ScreenHalf, centre + (blur / 2));
        if (high > low)
        {
            var spot = new Rect(top.X - 3, v.P(0, high).Y, 7, Math.Max(2, (high - low) * v.Sy));
            var intensity = Math.Clamp(0.4 / Math.Max(0.4, blur), 0.25, 1);
            dc.DrawRoundedRectangle(FadedBrush(Ink.Flame, intensity), null, spot, 2, 2);
        }

        AddBoxTarget("screen", new Rect(top.X - 6, top.Y - 4, 13, bottom.Y - top.Y + 22));
        if (ShowLabels) DrawPill(dc, Loc("Lens_Label_Screen"), new Point(top.X, top.Y - 16), Ink.Ivory);

        // What the screen shows, enlarged in the corner.
        var inset = v.Inset;
        var picture = sunlight
            ? _screenPicture.Picture(SunAngle * screen, 0, blur, sun: true)
            : _screenPicture.Picture(LensModel.HeightOnScreen(h, u, screen), 0.32 * Math.Abs(LensModel.HeightOnScreen(h, u, screen)), blur, sun: false);
        var frame = new Rect(inset.X - 6, inset.Y - 24, inset.Width + 12, inset.Height + 30);
        DrawFrame(dc, frame);
        dc.DrawImage(picture, inset);
        dc.DrawRectangle(null, Ink.Faint, inset);
        var sharp = blur < 0.15;
        var caption = sharp ? Loc("Lens_Label_OnScreenSharp") : LocFormat("Lens_Label_OnScreenBlur", Centimetres(blur));
        DrawText(dc, caption, new Point(inset.X, inset.Y - 12), sharp ? Ink.Green : Ink.Dim, 11.5);
        AddBoxTarget("screenview", frame);
        _ = now;
    }

    /// <summary>The distances along the bottom: candle to lens (u), lens to image (v), and the focal length.</summary>
    private void DrawDistances(PaintContext dc, BenchView v, double f, double u, bool sunlight)
    {
        if (!ShowLabels) return;
        var y = v.Stage.Bottom - 14;
        var lens = v.P(0, 0).X;
        if (!sunlight)
        {
            var candle = v.P(-u, 0).X;
            DrawSpan(dc, new Point(candle, y), new Point(lens, y), Ink.Ivory);
            DrawPill(dc, LocFormat("Lens_Label_U", Centimetres(u)), new Point((candle + lens) / 2, y), Ink.Ivory, 11);
        }

        var image = sunlight ? f : LensModel.ImageDistance(f, u);
        if (double.IsFinite(image) && image > BenchLeft && image < BenchRight && Math.Abs(image) > 1)
        {
            var at = v.P(image, 0).X;
            var colour = image > 0 ? Ink.Gold : Ink.Cyan;
            DrawSpan(dc, new Point(lens, y - 22), new Point(at, y - 22), colour);
            DrawPill(dc, LocFormat("Lens_Label_V", Centimetres(image)), new Point((lens + at) / 2, y - 22), colour, 11);
        }
    }

    #endregion
}
