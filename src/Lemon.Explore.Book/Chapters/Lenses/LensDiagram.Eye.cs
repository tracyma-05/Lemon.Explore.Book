using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Lenses;

/// <summary>
/// The eye: a chart on a track of distances (squeezed), the light from its top and bottom through glasses (if worn), the
/// cornea, the pupil and the crystalline lens to the retina, the near and far points, and what the eye sees. The eye is
/// drawn to scale in millimetres; the light inside it follows the two-surface model of <see cref="LensModel"/>.
/// </summary>
public sealed partial class LensDiagram
{
    /// <summary>Distances on the track: from 7 cm (right, by the eye) to 60 m, then infinity at the far end.</summary>
    internal const double TrackNear = 0.07;

    internal const double TrackFar = 60;

    /// <summary>The longest eye drawn, mm (strong short sight).</summary>
    private const double LongestEye = 27;

    /// <summary>The iris's depth behind the cornea, mm.</summary>
    private const double IrisDepth = 3.2;

    /// <summary>How steeply the light from the chart's top comes in (radians, exaggerated so the turned-over image shows).</summary>
    private const double TipAngle = -0.1;

    /// <summary>In front of the cornea, lengths are drawn at half scale (the glasses sit 12 mm away).</summary>
    private const double FrontSqueeze = 0.5;

    private readonly EyeChart _eyeChart = new();
    private EyeView _eye;

    /// <summary>The eye's scale: the cornea's apex on screen, pixels per millimetre, the track and the picture's place.</summary>
    private readonly record struct EyeView(double X0, double Y0, double Scale, Rect Stage, double TrackLeft, double TrackRight, Rect Inset)
    {
        public Point P(double x, double y) => new(X0 + (x * Scale * (x < 0 ? FrontSqueeze : 1)), Y0 - (y * Scale));

        /// <summary>Where a distance (m, +∞ allowed) is on the track.</summary>
        public double TrackX(double distance)
        {
            if (double.IsPositiveInfinity(distance) || distance >= TrackFar * 1.5) return TrackLeft + 8;
            var t = (Math.Log10(Math.Clamp(distance, TrackNear, TrackFar)) - Math.Log10(TrackNear)) / (Math.Log10(TrackFar) - Math.Log10(TrackNear));
            return TrackRight - (t * (TrackRight - TrackLeft - 40));
        }

        /// <summary>The distance at a place on the track (+∞ at its far end).</summary>
        public double TrackDistance(double x)
        {
            if (x < TrackLeft + 24) return double.PositiveInfinity;
            var t = Math.Clamp((TrackRight - x) / (TrackRight - TrackLeft - 40), 0, 1);
            return Math.Pow(10, Math.Log10(TrackNear) + (t * (Math.Log10(TrackFar) - Math.Log10(TrackNear))));
        }
    }

    private EyeView EyeLayout()
    {
        var stage = Stage();
        var scale = Math.Min((stage.Height - 40) / 27, stage.Width * 0.56 / (LongestEye + 5 + (16 * FrontSqueeze)));
        var x0 = stage.Right - 24 - ((LongestEye + 5) * scale);
        var y0 = stage.Top + (stage.Height * 0.44);
        var trackRight = x0 - (16 * FrontSqueeze * scale) - 16;

        // What the eye sees: bottom left, as big as the room under the track allows.
        var room = stage.Bottom - (y0 + 78);
        var s = Math.Clamp((room - 30) / EyeChart.Height, 0.5, 1);
        var inset = new Rect(stage.Left + 70, stage.Bottom - (EyeChart.Height * s) - 10, EyeChart.Width * s, EyeChart.Height * s);
        return new EyeView(x0, y0, scale, stage, stage.Left + 24, trackRight, inset);
    }

    private void DrawEyeMode(PaintContext dc, double now)
    {
        var v = _eye = EyeLayout();
        var error = Error;
        var glasses = LensModel.GlassesPower(Glasses, error);
        var vergence = LensModel.VergenceAtEye(Distance, glasses);
        var focus = LensModel.Focus(error, Age, vergence);
        var length = LensModel.AxialLength(error) * 1000;
        var lensPower = LensModel.LensPower(length / 1000, focus.Focused);
        var pupil = LensModel.Pupil(Light) * 1000;
        var accommodation = Math.Clamp((lensPower - LensModel.LensPowerRelaxed) / 12, 0, 1);

        DrawTrack(dc, v, error, glasses, focus.Amplitude);
        DrawEyeball(dc, v, length, pupil, accommodation, now);
        if (Glasses != Spectacles.None) DrawGlasses(dc, v, glasses);
        DrawEyeRays(dc, v, length, vergence, lensPower, pupil, glasses, focus.Defocus);
        DrawWhatYouSee(dc, v, focus, pupil);
    }

    #region the track

    private void DrawTrack(PaintContext dc, EyeView v, double error, double glasses, double amplitude)
    {
        var y = v.Y0;
        dc.DrawLine(Ink.Axis, new Point(v.TrackLeft, y), new Point(v.X0, y));

        // Where the eye can focus: from its near point out to its far point.
        var near = LensModel.DistanceFor(error - amplitude, glasses);
        var far = LensModel.DistanceFor(error, glasses);
        if (!double.IsNaN(far) || !double.IsNaN(near))
        {
            var from = double.IsNaN(near) ? double.NaN : v.TrackX(Math.Max(near, TrackNear));
            var to = double.IsNaN(far) ? double.NaN : v.TrackX(far);
            if (double.IsFinite(from) && double.IsFinite(to))
            {
                dc.DrawRoundedRectangle(FadedBrush(Ink.Green, 0.16), null, new Rect(Math.Min(from, to), y - 5, Math.Abs(from - to), 10), 5, 5);
            }
        }

        foreach (var (distance, key) in new[] { (0.1, "Lens_Tick_10cm"), (0.25, "Lens_Tick_25cm"), (1.0, "Lens_Tick_1m"), (6.0, "Lens_Tick_6m") })
        {
            var x = v.TrackX(distance);
            dc.DrawLine(Ink.Faint, new Point(x, y - 4), new Point(x, y + 4));
            if (ShowLabels) DrawText(dc, Loc(key), new Point(x, y + 14), Ink.Dim, 10.5, TextAnchor.Center);
        }

        DrawText(dc, Loc("Lens_Value_Infinity"), new Point(v.TrackLeft + 8, y + 14), Ink.Dim, 12, TextAnchor.Center);

        if (!double.IsNaN(near)) DrawPin(dc, v, Math.Max(near, TrackNear), "nearpoint", near < TrackNear ? "Lens_Label_NearPointClose" : "Lens_Label_NearPoint", Ink.Orange, below: true);
        if (!double.IsNaN(far)) DrawPin(dc, v, far, "farpoint", "Lens_Label_FarPoint", Ink.Violet, below: true, second: true);

        // The chart, standing on the axis.
        var chartX = v.TrackX(Distance);
        var card = new Rect(chartX - 17, y - 46, 34, 46);
        dc.DrawRoundedRectangle(Solid(255, 0xF4, 0xF2, 0xEC), MakePen(Solid(255, 0x9A, 0x96, 0x8E), 1), card, 3, 3);
        var ink = Solid(255, 0x14, 0x14, 0x18);
        var u = 4.2;
        var ex = card.X + 8;
        var ey = card.Y + 12;
        dc.DrawRectangle(ink, null, new Rect(ex, ey, u, 5 * u));
        for (var i = 0; i < 3; i++) dc.DrawRectangle(ink, null, new Rect(ex, ey + (2 * i * u), 5 * u, u));
        dc.DrawRectangle(Solid(255, 0x5A, 0x56, 0x50), null, new Rect(chartX - 1.5, y - 2, 3, 6));
        AddBoxTarget("chart", card.Inflate(new Thickness(3, 3)));
        if (ShowLabels) DrawPill(dc, Metres(Distance), new Point(chartX, card.Top - 14), Ink.Text, 11);
    }

    private void DrawPin(PaintContext dc, EyeView v, double distance, string id, string key, Color colour, bool below, bool second = false)
    {
        var x = v.TrackX(distance);
        var y = v.Y0;
        var tip = new Point(x, y + 6);
        var head = new Point(x, y + (second ? 58 : 40));
        dc.DrawLine(FadedPen(colour, 0.8, 1.3), tip, head);
        dc.DrawEllipse(Solid(255, colour.R, colour.G, colour.B), null, tip, 3, 3);
        var box = ShowLabels ? DrawPill(dc, Loc(key), new Point(x, head.Y + 8), colour, 10.5) : new Rect(head.X - 6, head.Y - 6, 12, 12);
        AddBoxTarget(id, box);
        _ = below;
    }

    #endregion

    #region the eyeball

    /// <summary>The inside of the eyeball (the retina's surface): an ellipse from just behind the cornea to the back at <paramref name="length"/>.</summary>
    private static (double Cx, double Rx, double Ry) Globe(double length) => ((length + 1.6) / 2, (length - 1.6) / 2, 11.0);

    private void DrawEyeball(PaintContext dc, EyeView v, double length, double pupil, double accommodation, double now)
    {
        var (cx, rx, ry) = Globe(length);
        const double Wall = 0.8;
        const double Limbus = 5.9;

        // The vitreous: the clear jelly filling the eye.
        dc.DrawGeometry(FadedBrush(Color.FromRgb(0x4A, 0x6A, 0x8C), 0.16), null, EllipsePath(v, cx, rx, ry, -180, 180));
        _targets.Add("vitreous", v.P(cx + (rx * 0.25), ry * 0.42), 12);

        // The white of the eye (sclera): a band round the back, open at the front where the cornea is.
        var start = Math.Asin(Limbus / (ry + Wall)) * 180 / Math.PI;
        var outer = EllipsePoints(cx, rx + Wall, ry + Wall, 180 - start, -180 + start, 64);
        var innerStart = Math.Asin(Limbus / ry) * 180 / Math.PI;
        var inner = EllipsePoints(cx, rx, ry, -180 + innerStart, 180 - innerStart, 64);
        var band = outer.Concat(inner).Select(p => v.P(p.X, p.Y)).ToList();
        dc.DrawGeometry(Solid(255, 0xE6, 0xE2, 0xDA), MakePen(Solid(255, 0xB8, 0xB2, 0xA8), 1), Line(band, closed: true, filled: true));

        // The retina lines the back, from the ora serrata round.
        var ora = Math.Acos(Math.Clamp((7.0 - cx) / rx, -1, 1)) * 180 / Math.PI;
        var retina = EllipsePoints(cx, rx - 0.15, ry - 0.15, ora, -ora, 80).Select(p => v.P(p.X, p.Y)).ToList();
        var retinaGeometry = Line(retina);
        if (Glows("retina")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 9), retinaGeometry);
        dc.DrawGeometry(null, MakePen(Solid(255, 0xE0, 0x5A, 0x3C), Math.Max(2, 0.32 * v.Scale)), retinaGeometry);
        var retinaMark = EllipsePoints(cx, rx, ry, 55, 55, 1)[0];
        _targets.Add("retina", v.P(retinaMark.X, retinaMark.Y), 10);

        // The fovea: a small pit on the axis at the back, where vision is sharpest.
        var fovea = v.P(length, 0);
        dc.DrawEllipse(Solid(255, 0xFF, 0xD8, 0x60), null, fovea, Math.Max(2.5, 0.25 * v.Scale), Math.Max(4, 0.5 * v.Scale));
        _targets.Add("fovea", fovea, 8);

        // The optic nerve leaves from the disc, below the axis here: no light-sensitive cells there (the blind spot).
        var discY = -3.6;
        var discX = cx + (rx * Math.Sqrt(1 - Math.Pow(discY / ry, 2)));
        var nerve = new StreamGeometry();
        using (var context = nerve.Open())
        {
            context.BeginFigure(v.P(discX - 0.3, discY + 1.1), true);
            context.LineTo(v.P(discX + 4.2, discY + 0.4));
            context.LineTo(v.P(discX + 4.2, discY - 2.6));
            context.LineTo(v.P(discX - 0.3, discY - 1.1));

            context.EndFigure(true);
        }
        dc.DrawGeometry(Solid(255, 0xE8, 0xC8, 0x7A), MakePen(Solid(255, 0xB0, 0x90, 0x50), 1), nerve);
        dc.DrawEllipse(Solid(255, 0xFF, 0xF0, 0xC0), null, v.P(discX, discY), Math.Max(2.5, 0.3 * v.Scale), Math.Max(4, 0.75 * v.Scale));
        _targets.Add("blindspot", v.P(discX + 1.2, discY - 0.6), 11);

        // The cornea: the clear front window, curved more steeply than the eyeball.
        var cornea = new List<Point>();
        for (var y = -Limbus; y <= Limbus + 1e-9; y += Limbus / 20) cornea.Add(v.P(7.8 - Math.Sqrt((7.8 * 7.8) - (y * y)), y));
        for (var y = Limbus - 0.3; y >= -Limbus + 0.3 - 1e-9; y -= (Limbus - 0.3) / 20) cornea.Add(v.P(0.55 + 6.8 - Math.Sqrt((6.8 * 6.8) - (y * y)), y));
        dc.DrawGeometry(FadedBrush(Ink.Glass, 0.35), FadedPen(Ink.Glass, 0.9, 1.3), Line(cornea, closed: true, filled: true));
        _targets.Add("cornea", v.P(0.6, 2.8), 10);

        DrawCrystallineLens(dc, v, accommodation, now);

        // The iris, in front of the lens, with the pupil between its edges.
        var iris = Solid(255, 0x6E, 0x4A, 0x2E);
        var irisEdge = MakePen(Solid(255, 0x3E, 0x2A, 0x1A), 1);
        foreach (var side in new[] { 1, -1 })
        {
            var a = v.P(IrisDepth - 0.2, side * (pupil / 2));
            var b = v.P(IrisDepth + 0.25, side * Limbus);
            dc.DrawRoundedRectangle(iris, irisEdge, new Rect(new Point(a.X, Math.Min(a.Y, b.Y)), new Point(b.X, Math.Max(a.Y, b.Y))), 2, 2);
        }

        _targets.Add("iris", v.P(IrisDepth, (pupil / 2) + 1.4), 9);

        if (!ShowLabels) return;
        DrawPill(dc, Loc("Lens_Label_Cornea"), v.P(-1.5, Limbus + 1.6), Ink.Glass, 11);
        DrawPill(dc, Loc("Lens_Label_Pupil"), v.P(-3.0, -Limbus - 0.6), Color.FromRgb(0xD8, 0xA8, 0x78), 11);
        DrawPill(dc, Loc("Lens_Label_EyeLens"), v.P(4.2, 10.6), Ink.Glass, 11);
        DrawPill(dc, Loc("Lens_Label_Retina"), v.P(cx + (rx * 0.62), ry + 1.9), Color.FromRgb(0xFF, 0x8A, 0x6A), 11);
        DrawPill(dc, Loc("Lens_Label_Fovea"), new Point(fovea.X + 4, fovea.Y - 36), Ink.Gold, 10.5);
        DrawPill(dc, Loc("Lens_Label_BlindSpot"), v.P(discX + 2.4, discY - 4.2), Color.FromRgb(0xE8, 0xC8, 0x7A), 10.5);
    }

    /// <summary>
    /// The crystalline lens and the ciliary muscle round it. Looking far, the muscle is relaxed and the fibres pull the lens
    /// flat; to focus near, the muscle tightens, the fibres slacken and the lens bulges (<paramref name="accommodation"/> 0–1).
    /// </summary>
    private void DrawCrystallineLens(PaintContext dc, EyeView v, double accommodation, double now)
    {
        var k = accommodation;
        var front = 3.6 - (0.35 * k);
        var equator = 4.6 - (0.35 * k);
        var ra = 10 - (4.4 * k);
        var rp = 6 - (0.6 * k);
        var frontSag = ra - Math.Sqrt((ra * ra) - (equator * equator));
        var backSag = rp - Math.Sqrt((rp * rp) - (equator * equator));
        var rim = front + frontSag;
        var back = rim + backSag;

        var points = new List<Point>();
        for (var i = 0; i <= 24; i++)
        {
            var y = equator - (2 * equator * i / 24);
            points.Add(v.P(rim - frontSag + (ra - Math.Sqrt((ra * ra) - (y * y))), y));
        }

        for (var i = 0; i <= 24; i++)
        {
            var y = -equator + (2 * equator * i / 24);
            points.Add(v.P(rim + backSag - (rp - Math.Sqrt((rp * rp) - (y * y))), y));
        }

        var lens = Line(points, closed: true, filled: true);
        var fill = SpaceDrawing.RadialGradient((Color.FromArgb(150, 0xE8, 0xF4, 0xFF), 0), (Color.FromArgb(70, 0x9F, 0xD0, 0xF0), 1));
        if (Glows("eyelens")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 8), lens);
        dc.DrawGeometry(fill, FadedPen(Ink.Glass, 0.9, 1.2), lens);
        _targets.Add("eyelens", v.P((front + back) / 2, 0), equator * v.Scale * 0.6);

        // The ciliary muscle on each side, bulkier when it tightens, and the zonule fibres to the lens's rim.
        var muscle = Lerp(Color.FromRgb(0xA8, 0x5A, 0x50), Color.FromRgb(0xD8, 0x40, 0x3A), k);
        foreach (var side in new[] { 1, -1 })
        {
            var body = v.P(rim + 0.3, side * (equator + 2.4 - (0.5 * k)));
            dc.DrawEllipse(Solid(255, muscle.R, muscle.G, muscle.B), MakePen(Solid(255, 0x6A, 0x2A, 0x24), 1), body, (1.0 + (0.3 * k)) * v.Scale, (1.0 + (0.35 * k)) * v.Scale);
            for (var j = -1; j <= 1; j++)
            {
                var from = v.P(rim + (0.35 * j), side * (equator + 1.3 - (0.3 * k)));
                var to = v.P(rim + (0.25 * j), side * equator);
                if (k < 0.3)
                {
                    dc.DrawLine(FadedPen(Ink.Ivory, 0.75 - k, 1), from, to);
                }
                else
                {
                    // Slack fibres sag.
                    var sag = new StreamGeometry();
                    using (var context = sag.Open())
                    {
                        context.BeginFigure(from, false);
                        context.QuadraticBezierTo(new Point(((from.X + to.X) / 2) + (2.5 * k * v.Scale * 0.3), (from.Y + to.Y) / 2), to);

                        context.EndFigure(false);
                    }
                    dc.DrawGeometry(null, FadedPen(Ink.Ivory, 0.45, 1), sag);
                }
            }
        }

        _targets.Add("ciliary", v.P(rim + 0.3, equator + 2.4 - (0.5 * k)), Math.Max(9, 1.1 * v.Scale));
        if (ShowLabels) DrawPill(dc, Loc("Lens_Label_Ciliary"), v.P(rim + 6.2, 10.6), Lerp(Ink.Text, Ink.Red, 0.4 + (0.4 * k)), 10.5);
    }

    private static List<Point> EllipsePoints(double cx, double rx, double ry, double fromDegrees, double toDegrees, int steps)
    {
        var points = new List<Point>(steps + 1);
        for (var i = 0; i <= steps; i++)
        {
            var a = (fromDegrees + ((toDegrees - fromDegrees) * i / Math.Max(1, steps))) * Math.PI / 180;
            points.Add(new Point(cx + (rx * Math.Cos(a)), ry * Math.Sin(a)));
        }

        return points;
    }

    private static StreamGeometry EllipsePath(EyeView v, double cx, double rx, double ry, double from, double to) =>
        Line(EllipsePoints(cx, rx, ry, from, to, 72).Select(p => v.P(p.X, p.Y)), closed: true, filled: true);

    /// <summary>The glasses' lens, 12 mm in front of the cornea: thin in the middle for short sight, thick for long sight or reading.</summary>
    private void DrawGlasses(PaintContext dc, EyeView v, double power)
    {
        var x = -LensModel.GlassesGap * 1000;
        var half = 9.0;
        var centre = 1.0 + (power > 0 ? 0.25 * power : 0);
        var edge = 1.0 + (power < 0 ? -0.25 * power : 0);
        var points = new List<Point>();
        for (var i = 0; i <= 20; i++)
        {
            var y = half - (2 * half * i / 20);
            var t = 1 - Math.Pow(y / half, 2);
            var thickness = edge + ((centre - edge) * t);
            points.Add(v.P(x - (thickness / 2), y));
        }

        for (var i = 0; i <= 20; i++)
        {
            var y = -half + (2 * half * i / 20);
            var t = 1 - Math.Pow(y / half, 2);
            var thickness = edge + ((centre - edge) * t);
            points.Add(v.P(x + (thickness / 2), y));
        }

        dc.DrawGeometry(FadedBrush(Ink.Glass, 0.3), FadedPen(Ink.Glass, 0.95, 1.4), Line(points, closed: true, filled: true));
        dc.DrawLine(MakePen(Solid(255, 0x3A, 0x3E, 0x48), 2.5), v.P(x, half + 0.3), v.P(x, half + 1.6));
        dc.DrawLine(MakePen(Solid(255, 0x3A, 0x3E, 0x48), 2.5), v.P(x, half + 1.6), v.P(2.5, half + 3.2));
        AddBoxTarget("glasses", new Rect(v.P(x - 2, half), v.P(x + 2, -half)));
        if (ShowLabels)
        {
            var key = Glasses == Spectacles.Reading ? "Lens_Label_ReadingGlasses" : power < 0 ? "Lens_Label_ConcaveGlasses" : "Lens_Label_ConvexGlasses";
            DrawPill(dc, LocFormat(key, Number(power, "+0.00;-0.00")), v.P(x, -half - 2.2), Ink.Glass, 10.5);
        }
    }

    #endregion

    #region the light

    /// <summary>
    /// Two bundles of rays, from the chart's bottom (on the axis, blue) and its top (gold), traced through the glasses, the
    /// cornea and the crystalline lens: where each comes together, on, in front of or behind the retina, and the
    /// upside-down image they make there.
    /// </summary>
    private void DrawEyeRays(PaintContext dc, EyeView v, double length, double vergence, double lensPower, double pupil, double glasses, double defocus)
    {
        var (cx, rx, ry) = Globe(length);
        var chartX = v.TrackX(Distance);
        var cornea = LensModel.CorneaPower / 1000;
        var lens = lensPower / 1000;
        var ve = vergence / 1000;
        var g = glasses / 1000;
        const double N = LensModel.EyeIndex;
        var depth = LensModel.LensDepth * 1000;
        var k = IrisDepth / N;

        var landings = new List<(Point At, bool Tip)>();
        foreach (var (angle, colour, from) in new[] { (0.0, Ink.Cyan, new Point(chartX, v.Y0)), (TipAngle, Ink.Gold, new Point(chartX, v.Y0 - 46)) })
        {
            var hits = new List<(Point Lens, Vector Out)>();
            for (var i = 0; i < 5; i++)
            {
                // A ray through the pupil, from its top edge to its bottom.
                var target = (pupil / 2) * 0.92 * (1 - (i / 2.0));
                var y0 = (target - (k * angle)) / (1 - (k * (ve + cornea)));
                var u0 = angle - (y0 * ve);
                var x0 = 7.8 - Math.Sqrt((7.8 * 7.8) - (y0 * y0));
                var w1 = u0 - (y0 * cornea);
                var y2 = y0 + ((depth - x0) * w1 / N);
                var w2 = w1 - (y2 * lens);
                var direction = new Vector(1, w2 / N);

                // Inside: cornea to lens, lens to where it meets the retina.
                var end = HitGlobe(new Point(depth, y2), direction, cx, rx - 0.15, ry - 0.15);
                var alpha = i == 2 ? 0.95 : 0.7;
                dc.DrawGeometry(null, FadedPen(colour, alpha, 1.2), Line([v.P(x0, y0), v.P(depth, y2), v.P(end.X, end.Y)]));
                hits.Add((new Point(depth, y2), direction));
                landings.Add((end, angle != 0));

                // Outside: back to the glasses (bent there) and on towards the chart.
                var gap = LensModel.GlassesGap * 1000;
                Point outer;
                if (g != 0)
                {
                    var yg = y0 - (gap * u0);
                    var before = u0 + (yg * g);
                    dc.DrawLine(FadedPen(colour, alpha * 0.8, 1.1), v.P(-gap, yg), v.P(x0, y0));
                    outer = new Point(-gap - 3.5, yg - (3.5 * before));
                    dc.DrawLine(FadedPen(colour, alpha * 0.8, 1.1), v.P(outer.X, outer.Y), v.P(-gap, yg));
                }
                else
                {
                    outer = new Point(-15, y0 - (15 * u0));
                    dc.DrawLine(FadedPen(colour, alpha * 0.8, 1.1), v.P(outer.X, outer.Y), v.P(x0, y0));
                }

                dc.DrawLine(FadedPen(colour, 0.22, 1), from, v.P(outer.X, outer.Y));
            }

            // Where the bundle comes to a point: its top and bottom rays cross.
            if (Cross(hits[0].Lens, hits[0].Out, hits[4].Lens, hits[4].Out) is { } meet)
            {
                var inside = meet.X < length;
                var marker = v.P(meet.X, meet.Y);
                if (meet.X > depth && meet.X < length + 14)
                {
                    if (!inside)
                    {
                        // Behind the retina: the light would only meet out there.
                        foreach (var (start, direction) in new[] { hits[0], hits[4] })
                        {
                            var stop = HitGlobe(start, direction, cx, rx - 0.15, ry - 0.15);
                            dc.DrawLine(FadedPen(colour, 0.45, 1.1, dashed: true), v.P(stop.X, stop.Y), marker);
                        }
                    }

                    dc.DrawEllipse(Solid(255, colour.R, colour.G, colour.B), null, marker, 3, 3);
                }
            }
        }

        // The image on the retina: from where the bottom's light lands to the middle of the top's patch (upside down).
        var bottom = landings.Where(l => !l.Tip).Select(l => l.At).ToList();
        var top = landings.Where(l => l.Tip).Select(l => l.At).ToList();
        var bottomCentre = new Point(bottom.Average(p => p.X), bottom.Average(p => p.Y));
        var topCentre = new Point(top.Average(p => p.X), top.Average(p => p.Y));
        var a = v.P(bottomCentre.X + 0.5, bottomCentre.Y);
        var b = v.P(topCentre.X + 0.5, topCentre.Y);
        dc.DrawLine(FadedPen(Ink.Ivory, 0.9, 2.2), new Point(a.X + 8, a.Y), new Point(b.X + 8, b.Y));
        var direction2 = new Vector(0, b.Y - a.Y);
        if (direction2.Length > 2) DrawArrowHead(dc, Solid(255, Ink.Ivory.R, Ink.Ivory.G, Ink.Ivory.B), new Point(b.X + 8, b.Y), direction2 / direction2.Length, 7);
        _targets.Add("retinalimage", new Point(b.X + 8, (a.Y + b.Y) / 2), Math.Max(9, Math.Abs(b.Y - a.Y) / 2));

        if (!ShowLabels) return;

        // In front of the retina when the eye bends the light too much (positive), behind it when too little.
        var sharp = Math.Abs(defocus) < 0.25;
        var where = sharp ? "Lens_Label_FocusOn" : defocus > 0 ? "Lens_Label_FocusFront" : "Lens_Label_FocusBehind";
        var (cx2, _, ry2) = Globe(length);
        DrawPill(dc, Loc(where), v.P(cx2 + 1, -ry2 + 2.2), sharp ? Ink.Green : Ink.Orange, 11);
    }

    /// <summary>Where a ray from <paramref name="start"/> inside the eye meets its back wall.</summary>
    private static Point HitGlobe(Point start, Vector direction, double cx, double rx, double ry)
    {
        // ((x − cx)/rx)² + (y/ry)² = 1 along start + t·direction.
        var dx = direction.X / rx;
        var dy = direction.Y / ry;
        var ox = (start.X - cx) / rx;
        var oy = start.Y / ry;
        var a = (dx * dx) + (dy * dy);
        var b = 2 * ((ox * dx) + (oy * dy));
        var c = (ox * ox) + (oy * oy) - 1;
        var disc = (b * b) - (4 * a * c);
        if (disc < 0) return start + (direction * 10);
        var t = (-b + Math.Sqrt(disc)) / (2 * a);
        return start + (direction * t);
    }

    private static Point? Cross(Point a, Vector da, Point b, Vector db)
    {
        var denominator = (da.X * db.Y) - (da.Y * db.X);
        if (Math.Abs(denominator) < 1e-12) return null;
        var t = (((b.X - a.X) * db.Y) - ((b.Y - a.Y) * db.X)) / denominator;
        return a + (da * t);
    }

    #endregion

    #region what the eye sees

    private void DrawWhatYouSee(PaintContext dc, EyeView v, EyeFocus focus, double pupil)
    {
        var blur = LensModel.BlurAngle(focus.Defocus, pupil / 1000) * 180 / Math.PI * 60;
        var dim = Light switch
        {
            LightLevel.Dim => 0.6,
            LightLevel.Indoor => 0.12,
            _ => 0.0,
        };
        var inset = v.Inset;
        var frame = new Rect(inset.X - 64, inset.Y - 26, inset.Width + 72, inset.Height + 32);
        DrawFrame(dc, frame);
        dc.DrawImage(_eyeChart.Picture(blur, dim), inset);
        var detail = LensModel.SmallestDetail(blur);
        var score = LensModel.FivePointScore(detail);
        DrawText(dc, LocFormat("Lens_Label_WhatYouSee", Number(score, "0.0"), Number(20 * detail, "0")), new Point(frame.X + 10, inset.Y - 13), Ink.Text, 11.5);

        // The rows' scores beside them.
        var scale = inset.Height / EyeChart.Height;
        for (var i = 0; i < EyeChart.RowDetail.Length; i++)
        {
            var rowDetail = EyeChart.RowDetail[i];
            var readable = detail <= rowDetail * 1.05;
            DrawText(dc, LocFormat("Lens_Label_Row", Number(LensModel.FivePointScore(rowDetail), "0.0"), Number(20 * rowDetail, "0")),
                new Point(inset.X - 8, inset.Y + (EyeChart.RowCentres[i] * scale)), readable ? Ink.Green : Ink.Dim, 10.5, TextAnchor.Right);
        }

        AddBoxTarget("view", frame);
    }

    #endregion
}
