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

namespace Lemon.Explore.Book.Chapters.Flight;

/// <summary>
/// The wind tunnel: the wing's section, the ideal flow round it (streamlines, smoke, coloured pressure and pressure
/// arrows), the lift and drag, columns of smoke released together, and after the stall the wake torn off the top.
/// Positions here are in the airfoil's display frame (chords, wind along +x, y up, the quarter chord at the origin).
/// </summary>
public sealed partial class FlightDiagram
{
    private const int SmokeCount = 380;
    private const int TimelineDots = 41;
    private const double TimelineEvery = 2.6;

    /// <summary>The wing in the tunnel now and what the stall has torn off.</summary>
    private Airfoil _airfoil = WingSection.Airfoil(WingShape.Cambered, false, 4);

    private IReadOnlyList<Point> _outline = [];
    private IReadOnlyList<Point> _camber = [];

    /// <summary>The surface on the side the flow tears off (top for nose up), from the trailing edge forwards.</summary>
    private List<Point> _stallSide = [];

    private double _separation;

    /// <summary>+1 when the flow tears off the top (nose up past the stall), −1 off the bottom.</summary>
    private double _stallSign = 1;

    private Point _separationPoint;
    private bool _wingDirty = true;

    private readonly Point[] _smoke = new Point[SmokeCount];
    private readonly double[] _smokeSlow = new double[SmokeCount];
    private bool _smokeSeeded;
    private readonly List<Point[]> _timelines = [];
    private double _sinceTimeline = TimelineEvery;
    private readonly List<(Point Point, double Age)> _wand = [];
    private readonly Random _random = new(7);
    private readonly LineBatch _wingBatch = new();

    private List<List<Point>> _streamlines = [];
    private (Rect Stage, double Scale)? _streamKey;
    private WriteableBitmap? _pressure;
    private (Rect Stage, double Scale)? _pressureKey;

    /// <summary>Where the airfoil's frame sits on screen this frame.</summary>
    private WingView _wingView;

    private readonly record struct WingView(Rect Stage, Point Origin, double Scale)
    {
        public Point P(Point display) => new(Origin.X + (display.X * Scale), Origin.Y - (display.Y * Scale));

        public Point D(Point screen) => new((screen.X - Origin.X) / Scale, (Origin.Y - screen.Y) / Scale);

        public Vector V(Vector display) => new(display.X * Scale, -display.Y * Scale);

        public double MinX => D(Stage.TopLeft).X;

        public double MaxX => D(Stage.BottomRight).X;

        public double MinY => D(Stage.BottomRight).Y;

        public double MaxY => D(Stage.TopLeft).Y;
    }

    private void OnWingChanged() => _wingDirty = true;

    private void OnTimelinesChanged()
    {
        _timelines.Clear();
        _sinceTimeline = TimelineEvery;
    }

    private WingView MakeWingView()
    {
        var stage = Stage();
        var scale = Math.Min(stage.Width / 2.9, stage.Height / 1.45);
        return new WingView(stage, new Point(stage.X + (stage.Width * 0.42), stage.Y + (stage.Height * 0.5)), scale);
    }

    /// <summary>Rebuilds the wing and the stall's wake after the shape or the angle changed.</summary>
    private void UpdateWing()
    {
        if (!_wingDirty) return;
        _wingDirty = false;
        _airfoil = WingSection.Airfoil(Shape, Inverted, Alpha);
        _outline = _airfoil.Outline(240);
        _camber = _airfoil.CamberLine(40);
        _separation = WingSection.Separation(Shape, Inverted, Alpha);
        _stallSign = Alpha >= _airfoil.ZeroLiftAlpha ? 1 : -1;

        // The outline runs from the trailing edge over the top to the leading edge and back underneath.
        var le = _airfoil.LeadingEdge;
        var lead = 0;
        for (var i = 1; i < _outline.Count; i++)
        {
            if (((Vector)(_outline[i] - le)).SquaredLength < ((Vector)(_outline[lead] - le)).SquaredLength) lead = i;
        }

        _stallSide = _stallSign > 0
            ? _outline.Take(lead + 1).ToList()
            : _outline.Skip(lead).Reverse().ToList();
        var te = _airfoil.TrailingEdge;
        var x = le.X + ((1 - (0.85 * _separation)) * (te.X - le.X));
        _separationPoint = new Point(x, SurfaceY(_stallSide, x));
        _streamKey = null;
        _pressureKey = null;
    }

    /// <summary>The height of a surface (from the trailing edge forwards) at <paramref name="x"/>.</summary>
    private static double SurfaceY(List<Point> surface, double x)
    {
        for (var i = 1; i < surface.Count; i++)
        {
            var a = surface[i - 1];
            var b = surface[i];
            if ((a.X - x) * (b.X - x) <= 0 && Math.Abs(b.X - a.X) > 1e-9) return a.Y + ((b.Y - a.Y) * (x - a.X) / (b.X - a.X));
        }

        return surface.Count > 0 ? surface[^1].Y : 0;
    }

    /// <summary>
    /// How deep a point is in the torn-off flow behind the separation point: 0 outside, 1 well inside, easing in at the
    /// shear layer's edge and fading far downstream (drawn, not computed).
    /// </summary>
    private double WakeWeight(Point p)
    {
        if (_separation < 0.02 || p.X < _separationPoint.X) return 0;
        var s = _stallSign;
        var te = _airfoil.TrailingEdge;
        var dx = p.X - _separationPoint.X;
        var top = _separationPoint.Y + (s * (0.02 + (0.22 * _separation * Math.Sqrt(dx))));
        var fromTop = s * (top - p.Y);
        if (fromTop <= 0) return 0;
        double weight;
        if (p.X >= te.X)
        {
            var fromBottom = s * (p.Y - (te.Y - (s * 0.03 * (p.X - te.X))));
            if (fromBottom <= 0) return 0;
            weight = WingSection.SmoothStep(0, 0.03, fromBottom) * Math.Exp(-(p.X - te.X) / 2.5);
        }
        else
        {
            // Ahead of the trailing edge the wing itself is the wake's floor.
            if (s * (p.Y - SurfaceY(_stallSide, p.X)) <= 0) return 0;
            weight = 1;
        }

        return weight * WingSection.SmoothStep(0, 0.04 + (0.06 * Math.Min(1, dx)), fromTop);
    }

    /// <summary>The air's velocity at a point (free stream = 1): the ideal flow, blended into the churning wake past the stall.</summary>
    private Vector FlowAt(Point p, double t)
    {
        var ideal = _airfoil.Velocity(p);
        var w = WakeWeight(p);
        if (w <= 0) return ideal;
        var u = 0.24 + (0.16 * Math.Sin((9 * p.X) - (3.1 * t) + (2 * p.Y)));
        var v = 0.32 * _separation * Math.Sin((7 * p.X) - (4.3 * t)) * Math.Cos((5 * p.Y) + (1.7 * t));
        return (ideal * (1 - w)) + (new Vector(u, v) * w);
    }

    /// <summary>The pressure coefficient at a point (NaN inside the wing); in the wake an even, mild suction that recovers downstream.</summary>
    private double PressureAt(Point p)
    {
        if (_airfoil.Inside(p)) return double.NaN;
        var cp = 1 - _airfoil.Velocity(p).SquaredLength;
        var w = WakeWeight(p);
        if (w <= 0) return cp;
        var wake = -0.55 * Math.Exp(-Math.Max(0, p.X - _airfoil.TrailingEdge.X) / 1.2);
        return cp + ((wake - cp) * w * Math.Min(1, 1.5 * _separation));
    }

    /// <summary>How fast the smoke moves on screen: chords per second at the tunnel's speed.</summary>
    private double SmokeSpeed => 0.5 * Math.Clamp(AirSpeed / 200, 0.25, 1.8);

    private bool NearWing(Point screen)
    {
        if (Mode != FlightMode.Wing || _wingView.Scale <= 0) return false;
        var p = _wingView.D(screen);
        if (_airfoil.Inside(p)) return true;
        var slop = 10 / _wingView.Scale;
        foreach (var q in _outline)
        {
            if (((Vector)(q - p)).SquaredLength < slop * slop) return true;
        }

        return false;
    }

    /// <summary>The pointer's angle round the wing's pivot on screen (y up), rad.</summary>
    private double PointerAngle(Point screen) => Math.Atan2(_wingView.Origin.Y - screen.Y, screen.X - _wingView.Origin.X);

    /// <summary>Smoke from the wand at a screen point.</summary>
    private void Puff(Point screen, int count)
    {
        var p = _wingView.D(screen);
        for (var i = 0; i < count; i++)
        {
            var q = p + new Vector((_random.NextDouble() - 0.5) * 0.04, (_random.NextDouble() - 0.5) * 0.04);
            if (!_airfoil.Inside(q)) _wand.Add((q, 0));
        }

        if (_wand.Count > 900) _wand.RemoveRange(0, _wand.Count - 900);
    }

    #region motion

    private void StepWing(double dt)
    {
        if (_wingView.Scale <= 0) return;
        UpdateWing();
        var view = _wingView;
        var step = dt * SmokeSpeed;
        if (!_smokeSeeded)
        {
            for (var i = 0; i < SmokeCount; i++) _smoke[i] = Spawn(view, anywhere: true);
            _smokeSeeded = true;
        }

        for (var i = 0; i < SmokeCount; i++)
        {
            var p = Advect(_smoke[i], step);
            var speed = ((Vector)(p - _smoke[i])).Length / Math.Max(step, 1e-9);
            _smokeSlow[i] = speed < 0.04 ? _smokeSlow[i] + dt : 0;
            _smoke[i] = p.X > view.MaxX + 0.05 || p.Y < view.MinY - 0.1 || p.Y > view.MaxY + 0.1 || double.IsNaN(p.X) || _smokeSlow[i] > 1.5
                ? Spawn(view, anywhere: false)
                : p;
        }

        if (ShowTimelines)
        {
            _sinceTimeline += dt;
            if (_sinceTimeline >= TimelineEvery)
            {
                _sinceTimeline = 0;
                var column = new Point[TimelineDots];
                var top = Math.Min(view.MaxY - 0.05, 0.9);
                var bottom = Math.Max(view.MinY + 0.05, -0.9);
                for (var i = 0; i < TimelineDots; i++) column[i] = new Point(view.MinX + 0.04, top - ((top - bottom) * i / (TimelineDots - 1)));
                _timelines.Add(column);
            }

            for (var c = _timelines.Count - 1; c >= 0; c--)
            {
                var column = _timelines[c];
                var left = double.MaxValue;
                for (var i = 0; i < column.Length; i++)
                {
                    if (double.IsNaN(column[i].X)) continue;
                    var p = Advect(column[i], step);
                    column[i] = _airfoil.Inside(p) ? new Point(double.NaN, double.NaN) : p;
                    if (!double.IsNaN(p.X)) left = Math.Min(left, p.X);
                }

                if (left > view.MaxX + 0.1) _timelines.RemoveAt(c);
            }
        }

        for (var i = _wand.Count - 1; i >= 0; i--)
        {
            var (point, age) = _wand[i];
            var p = Advect(point, step);
            if (age + dt > 7 || p.X > view.MaxX + 0.05 || double.IsNaN(p.X) || _airfoil.Inside(p)) _wand.RemoveAt(i);
            else _wand[i] = (p, age + dt);
        }
    }

    /// <summary>One midpoint step along the flow (step in chords at free-stream speed).</summary>
    private Point Advect(Point p, double step)
    {
        var v1 = FlowAt(p, _time);
        var mid = p + (v1 * (step / 2));
        var v2 = FlowAt(mid, _time);
        var q = p + (v2 * step);
        return _airfoil.Inside(q) ? p : q;
    }

    private Point Spawn(WingView view, bool anywhere)
    {
        for (var tries = 0; tries < 20; tries++)
        {
            var x = anywhere ? view.MinX + (_random.NextDouble() * (view.MaxX - view.MinX)) : view.MinX - (_random.NextDouble() * 0.1);
            var y = view.MinY + (_random.NextDouble() * (view.MaxY - view.MinY));
            var p = new Point(x, y);
            if (!_airfoil.Inside(p)) return p;
        }

        return new Point(view.MinX, view.MaxY);
    }

    #endregion

    #region drawing

    private void DrawWingMode(PaintContext dc, double now)
    {
        _wingView = MakeWingView();
        UpdateWing();
        var v = _wingView;
        dc.PushClip(new RectangleGeometry(new Rect(0, v.Stage.Y - 12, ActualWidth, v.Stage.Height + 24)));

        if (ShowPressure) DrawPressureField(dc, v);
        DrawTunnelLines(dc, v);
        if (ShowStreamlines) DrawStreamlines(dc, v, now);
        if (ShowSmoke) DrawSmoke(dc, v);
        if (ShowTimelines) DrawTimelines(dc, v);
        DrawWand(dc, v);
        DrawWingShape(dc, v, now);
        if (ShowArrows) DrawPressureArrows(dc, v);
        DrawWingForces(dc, v);
        DrawWingLabels(dc, v, now);
        dc.Pop();
    }

    /// <summary>The tip of the wing's lift arrow, from the quarter chord, for a lift coefficient of <paramref name="cl"/>.</summary>
    private static Point LiftTip(WingView v, double cl) => v.P(new Point(0, 0)) + new Vector(0, -Math.Clamp(cl * 0.32, -0.9, 0.9) * v.Scale);

    /// <summary>Where the lift's label sits: just beyond the arrow's tip.</summary>
    private static Point LiftPill(WingView v, double cl) => LiftTip(v, cl) + new Vector(0, cl >= 0 ? -16 : 16);

    /// <summary>The pressure as colour: blue where it is below the free stream's, red where above.</summary>
    private void DrawPressureField(PaintContext dc, WingView v)
    {
        var key = (v.Stage, v.Scale);
        if (_pressure is null || _pressureKey != key)
        {
            _pressureKey = key;
            var cell = Math.Max(3, Math.Sqrt(v.Stage.Width * v.Stage.Height / 36_000));
            var cols = Math.Max(1, (int)Math.Ceiling(v.Stage.Width / cell));
            var rows = Math.Max(1, (int)Math.Ceiling(v.Stage.Height / cell));
            var pixels = new int[cols * rows];
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    var cp = PressureAt(v.D(new Point(v.Stage.X + ((c + 0.5) * cell), v.Stage.Y + ((r + 0.5) * cell))));
                    pixels[(r * cols) + c] = PressureColour(cp);
                }
            }

            _pressure = new WriteableBitmap(new PixelSize(cols, rows), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            SpaceDrawing.WritePixels(_pressure, pixels, cols, rows);
            _pressureCell = cell;
        }

        // A few DIPs per cell: smoothed between cells, as WPF drew it, rather than in hard blocks.
        SpaceDrawing.DrawSmooth(dc, _pressure, new Rect(v.Stage.X, v.Stage.Y, _pressure.PixelSize.Width * _pressureCell, _pressure.PixelSize.Height * _pressureCell));
    }

    private double _pressureCell = 3;

    /// <summary>A premultiplied BGRA colour for a pressure coefficient: suction blue, pressure red, the free stream clear.</summary>
    internal static int PressureColour(double cp)
    {
        if (double.IsNaN(cp)) return 0;
        double r, g, b, a;
        if (cp < 0)
        {
            a = Math.Pow(Math.Min(1, -cp / 1.6), 0.75) * 0.7;
            (r, g, b) = (0.24, 0.55, 1.0);
        }
        else
        {
            a = Math.Pow(Math.Min(1, cp / 0.9), 0.85) * 0.7;
            (r, g, b) = (1.0, 0.36, 0.3);
        }

        var alpha = (int)(a * 255);
        return (alpha << 24) | ((int)(r * alpha) << 16) | ((int)(g * alpha) << 8) | (int)(b * alpha);
    }

    /// <summary>The tunnel's walls and a faint grid, so the picture reads as a test section.</summary>
    private static void DrawTunnelLines(PaintContext dc, WingView v)
    {
        var top = v.Stage.Y + 4;
        var bottom = v.Stage.Bottom - 4;
        dc.DrawLine(Ink.Faint, new Point(v.Stage.X, top), new Point(v.Stage.Right, top));
        dc.DrawLine(Ink.Faint, new Point(v.Stage.X, bottom), new Point(v.Stage.Right, bottom));
    }

    private void DrawStreamlines(PaintContext dc, WingView v, double now)
    {
        var key = (v.Stage, v.Scale);
        if (_streamKey != key)
        {
            _streamKey = key;
            _streamlines = [];
            const int seeds = 17;
            var top = v.MaxY - 0.06;
            var bottom = v.MinY + 0.06;
            for (var i = 0; i < seeds; i++)
            {
                var line = new List<Point>();
                var p = new Point(v.MinX, top - ((top - bottom) * i / (seeds - 1)));
                for (var n = 0; n < 900 && p.X < v.MaxX + 0.02; n++)
                {
                    line.Add(p);
                    if (WakeWeight(p) > 0.5) break;
                    var u = _airfoil.Velocity(p);
                    if (u.Length < 1e-6) break;
                    var mid = p + (u / u.Length * 0.006);
                    var u2 = _airfoil.Velocity(mid);
                    if (u2.Length < 1e-6) break;
                    p += u2 / u2.Length * 0.012;
                    if (_airfoil.Inside(p)) break;
                }

                _streamlines.Add(line);
            }
        }

        var pen = FadedPen(Ink.Smoke, 0.3, 1.1, flat: true);
        for (var i = 0; i < _streamlines.Count; i++)
        {
            var screen = _streamlines[i].Select(v.P).ToList();
            if (i == LabelledStreamline && Glows("streamline")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 6), Line(screen));
            _wingBatch.Add(pen, screen);
        }

        _wingBatch.Draw(dc);
    }

    /// <summary>The streamline carrying the "streamline" label: the fourth from the top, which runs over the wing.</summary>
    private const int LabelledStreamline = 5;

    private void DrawSmoke(PaintContext dc, WingView v)
    {
        var tail = 0.05;
        for (var i = 0; i < SmokeCount; i++)
        {
            var p = _smoke[i];
            if (double.IsNaN(p.X)) continue;
            var u = FlowAt(p, _time);
            var speed = u.Length;
            var alpha = Math.Round((0.3 + (0.4 * Math.Min(1, speed / 1.6))) * 10) / 10;
            var head = v.P(p);
            var back = v.P(p - (u * tail));
            if (((Vector)(head - back)).Length < 1.2) back = head - new Vector(1.2, 0);
            _wingBatch.Add(FadedPen(Ink.Smoke, alpha, 1.6, flat: true), back, head);
        }

        _wingBatch.Draw(dc);
    }

    /// <summary>The columns of smoke released together: a line through each column's puffs, broken where the wing splits it.</summary>
    private void DrawTimelines(PaintContext dc, WingView v)
    {
        for (var c = 0; c < _timelines.Count; c++)
        {
            var column = _timelines[c];
            var colour = c % 2 == 0 ? Ink.Gold : Ink.Pink;
            var pen = FadedPen(colour, 0.85, 1.6);
            var run = new List<Point>();
            for (var i = 0; i < column.Length; i++)
            {
                var p = column[i];
                var broken = double.IsNaN(p.X) || (i > 0 && !double.IsNaN(column[i - 1].X) &&
                    (((Vector)(p - column[i - 1])).Length > 0.22 || _airfoil.Inside(new Point((p.X + column[i - 1].X) / 2, (p.Y + column[i - 1].Y) / 2))));
                if (broken)
                {
                    if (run.Count > 1) _wingBatch.Add(pen, run.ToList());
                    run.Clear();
                }

                if (!double.IsNaN(p.X)) run.Add(v.P(p));
            }

            if (run.Count > 1) _wingBatch.Add(pen, run.ToList());
            var dots = FadedBrush(colour, 1);
            foreach (var p in column)
            {
                if (!double.IsNaN(p.X)) dc.DrawEllipse(dots, null, v.P(p), 2.2, 2.2);
            }
        }

        _wingBatch.Draw(dc);
    }

    private void DrawWand(PaintContext dc, WingView v)
    {
        foreach (var (point, age) in _wand)
        {
            dc.DrawEllipse(FadedBrush(Ink.Gold, 0.9 * (1 - (age / 7))), null, v.P(point), 2, 2);
        }
    }

    private void DrawWingShape(PaintContext dc, WingView v, double now)
    {
        var outline = _outline.Select(v.P).ToList();
        var body = Line(outline, closed: true, filled: true);
        var fill = SpaceDrawing.LinearGradient(Color.FromRgb(0xC9, 0xD2, 0xDE), Color.FromRgb(0x6E, 0x7A, 0x8C), 90);
        dc.DrawGeometry(fill, FadedPen(Colors.White, 0.85, 1.2), body);

        var le = v.P(_airfoil.LeadingEdge);
        var te = v.P(_airfoil.TrailingEdge);
        var bounds = body.Bounds;
        AddBoxTarget("wing", bounds);
        _targets.Add("leadingedge", le, 7);
        _targets.Add("trailingedge", te, 7);

        // The chord line, extended forward past the nose to show the angle of attack against the wind.
        Vector along = te - le;
        along /= along.Length;
        var front = le - (along * 0.45 * v.Scale);
        var back = te + (along * 0.32 * v.Scale);
        if (Glows("chord")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 6), Line([front, back]));
        if (ShowLabels || Glows("chord")) dc.DrawLine(FadedPen(Ink.Text, 0.55, 1, dashed: true), front, back);

        var cambered = Shape is WingShape.Cambered or WingShape.Bird;
        if (cambered && (ShowLabels || Glows("camber")))
        {
            var line = _camber.Select(v.P).ToList();
            if (Glows("camber")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 5), Line(line));
            dc.DrawGeometry(null, FadedPen(Ink.Gold, 0.9, 1.2, dashed: true), Line(line));
        }

        if (ShowLabels)
        {
            dc.DrawLine(FadedPen(Ink.Text, 0.4, 1, dashed: true), new Point(le.X - (0.75 * v.Scale), le.Y), le);
            var alpha = Alpha;
            if (Math.Abs(alpha) >= 1)
            {
                // The arc from the wind's line to the chord line, at the nose.
                var r = 0.42 * v.Scale;
                var start = new Point(le.X - r, le.Y);
                var end = le - (along * r);
                var arc = new StreamGeometry();
                using (var context = arc.Open())
                {
                    context.BeginFigure(start, false);
                    context.ArcTo(end, new Size(r, r), 0, false, alpha > 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);

                    context.EndFigure(false);
                }
                dc.DrawGeometry(null, FadedPen(Ink.Gold, 1, 1.6), arc);
            }

            var mid = new Point(le.X - (0.62 * v.Scale), le.Y - (Math.Sign(alpha) * Math.Min(0.18, Math.Abs(alpha) / 60) * v.Scale) - (alpha >= 0 ? 12 : -12));
            DrawTargetPill(dc, LocFormat("Flight_Label_Aoa", Number(alpha, "0.#")), mid, Ink.Gold, "aoa");
            DrawTargetPill(dc, Loc("Flight_Label_LeadingEdge"), le + new Vector(-30, alpha >= 0 ? -34 : 34), Ink.Text, "leadingedge");
            DrawTargetPill(dc, Loc("Flight_Label_TrailingEdge"), te + new Vector(14, alpha >= 0 ? 30 : -30), Ink.Text, "trailingedge");
            DrawTargetPill(dc, Loc("Flight_Label_Chord"), front + new Vector(-12, alpha >= 0 ? 16 : -16), Ink.Dim, "chord");
            if (cambered)
            {
                var c = v.P(_camber[_camber.Count / 2]);
                var below = SurfaceY(_outline.Skip(_outline.Count / 2).Reverse().ToList(), _camber[_camber.Count / 2].X);
                DrawTargetPill(dc, Loc("Flight_Label_Camber"), new Point(c.X + (0.1 * v.Scale), v.P(new Point(0, below)).Y + 20), Ink.Gold, "camber");
            }
        }
        else
        {
            AddBoxTarget("chord", new Rect(front, back));
        }

        // Where the air splits and stands still.
        var stagnation = v.P(_airfoil.Stagnation);
        dc.DrawEllipse(FadedBrush(Ink.Gold, 1), FadedPen(Colors.Black, 0.6, 1), stagnation, 4, 4);
        _targets.Add("stagnation", stagnation, 7);
        if (ShowLabels) DrawTargetPill(dc, Loc("Flight_Label_Stagnation"), stagnation + new Vector(-78, Alpha >= 0 ? 30 : -30), Ink.Gold, "stagnation");
    }

    /// <summary>Arrows round the surface: red pushing in where the pressure is above the free stream's, blue pulling out where below.</summary>
    private void DrawPressureArrows(PaintContext dc, WingView v)
    {
        const int count = 40;
        for (var i = 0; i < count; i++)
        {
            var angle = 0.1 + ((2 * Math.PI - 0.2) * i / (count - 1));
            var (point, velocity) = _airfoil.Surface(angle);
            var before = _airfoil.Surface(angle - 0.01).Point;
            var after = _airfoil.Surface(angle + 0.01).Point;
            Vector edge = after - before;
            if (edge.Length < 1e-9) continue;
            var outward = new Vector(edge.Y, -edge.X);
            outward = outward.Normalize();
            var cp = 1 - velocity.SquaredLength;
            var onStallSide = (_stallSign > 0 ? angle < Math.PI : angle > Math.PI) && point.X > _separationPoint.X;
            if (onStallSide && _separation > 0.02) cp += (-0.6 - cp) * Math.Min(1, 1.5 * _separation);
            var length = Math.Min(0.3, 0.1 * Math.Abs(cp));
            if (length < 0.012) continue;
            var surface = v.P(point);
            var outer = v.P(point + (outward * length));
            if (cp < 0) DrawArrow(dc, surface, outer, Ink.Blue, 1.6, 0.95);
            else DrawArrow(dc, outer, surface, Ink.Red, 1.6, 0.95);
        }
    }

    private void DrawWingForces(PaintContext dc, WingView v)
    {
        var cl = WingSection.Lift(Shape, Inverted, Alpha);
        var cd = WingSection.Drag(Shape, Inverted, Alpha);
        var origin = v.P(new Point(0, 0));
        var liftTip = LiftTip(v, cl);
        var dragTip = origin + new Vector(Math.Min(cd * 0.32, 0.6) * v.Scale, 0);
        if (Math.Abs(liftTip.Y - origin.Y) > 4) DrawArrow(dc, origin, liftTip, Ink.Cyan, 3.2);
        if (dragTip.X - origin.X > 3) DrawArrow(dc, origin, dragTip, Ink.Red, 2.6);
        dc.DrawEllipse(FadedBrush(Colors.White, 1), null, origin, 3, 3);

        // Lift per square metre of wing at the tunnel's speed, as the weight of so many kilograms.
        var speed = AirSpeed / 3.6;
        var kg = 0.5 * 1.225 * speed * speed * cl / Trainer.G;
        var liftPill = LiftPill(v, cl);
        DrawTargetPill(dc, LocFormat("Flight_Label_WingLift", Number(Math.Abs(kg), "0")), liftPill, Ink.Cyan, "wlift");
        _targets.Add("wlift", liftTip, 9);
        if (dragTip.X - origin.X > 14) DrawTargetPill(dc, Loc("Flight_Label_WingDrag"), dragTip + new Vector(30, 14), Ink.Red, "wdrag");
    }

    private void DrawWingLabels(PaintContext dc, WingView v, double now)
    {
        var stage = v.Stage;

        // The wind blowing in from the left.
        var windY = v.Origin.Y - (0.62 * v.Scale);
        var windFrom = new Point(stage.X + 26, windY);
        var windTo = new Point(stage.X + 26 + Math.Min(110, 0.5 * v.Scale), windY);
        DrawArrow(dc, windFrom, windTo, Ink.Text, 2.4, 0.8);
        var windText = LocFormat("Flight_Label_Wind", Number(AirSpeed, "0"));
        var windWidth = _labels.Text(windText, 11.5, Ink.Text).Width;
        DrawTargetPill(dc, windText, new Point(stage.X + 26 + (windWidth / 2) + 6, windY + 22), Ink.Text, "wind");

        if (ShowLabels)
        {
            var up = WingSection.Lift(Shape, Inverted, Alpha) >= 0;
            var upper = v.P(new Point(0.08, up ? 0.44 : -0.44));

            // The lift's label rides on its arrow: where the two would meet, this one moves back along the chord.
            var lift = LiftPill(v, WingSection.Lift(Shape, Inverted, Alpha));
            if (Math.Abs(upper.Y - lift.Y) < 24 && Math.Abs(upper.X - lift.X) < 110) upper = v.P(new Point(0.5, up ? 0.36 : -0.36));
            var lower = v.P(new Point(0.08, up ? -0.4 : 0.4));
            DrawTargetPill(dc, Loc("Flight_Label_Upper"), upper, Ink.Blue, "upper");
            DrawTargetPill(dc, Loc("Flight_Label_Lower"), lower, Ink.Red, "lower");

            if (ShowStreamlines && _streamlines.Count > LabelledStreamline && _streamlines[LabelledStreamline].Count > 1)
            {
                var line = _streamlines[LabelledStreamline];
                var at = line[(int)(line.Count * 0.82)];
                DrawTargetPill(dc, Loc("Flight_Label_Streamline"), v.P(at) + new Vector(0, -14), Ink.Smoke, "streamline");
            }
        }
        else
        {
            AddBoxTarget("upper", new Rect(v.P(new Point(-0.3, 0.55)), v.P(new Point(0.5, 0.2))));
            AddBoxTarget("lower", new Rect(v.P(new Point(-0.3, -0.2)), v.P(new Point(0.5, -0.45))));
        }

        // The air turned downwards as it leaves the trailing edge.
        var te = _airfoil.TrailingEdge;
        var probe = te + new Vector(0.1, 0);
        var flow = FlowAt(probe, _time);
        if (_separation < 0.6 && flow.Length > 0.1)
        {
            flow = flow.Normalize();
            var from = v.P(te + new Vector(0.06, 0));
            var to = from + (v.V(flow) * 0.42);
            DrawArrow(dc, from, to, Ink.Cyan, 2, 0.7);
            if (ShowLabels) DrawTargetPill(dc, Loc("Flight_Label_Downwash"), to + new Vector(40, 10), Ink.Cyan, "downwash");
            else _targets.Add("downwash", to, 10);
        }

        if (_separation > 0.15)
        {
            var mid = v.P(new Point(te.X + 0.32, ((_separationPoint.Y + te.Y) / 2) + (_stallSign * 0.12)));
            DrawTargetPill(dc, Loc("Flight_Label_Wake"), mid, Ink.Orange, "wake");
        }

        // Legends for what is switched on, top left of the tunnel.
        var y = stage.Y + 16;
        if (ShowTimelines)
        {
            var text = Loc("Flight_Label_Timeline");
            var width = _labels.Text(text, 11.5, Ink.Gold).Width;
            DrawTargetPill(dc, text, new Point(stage.X + 26 + (width / 2) + 6, y), Ink.Gold, "timeline");
            y += 26;
        }

        if (ShowArrows)
        {
            var text = Loc("Flight_Label_Arrows");
            var width = _labels.Text(text, 11.5, Ink.Text).Width;
            DrawTargetPill(dc, text, new Point(stage.X + 26 + (width / 2) + 6, y), Ink.Text, "arrows");
        }
    }

    #endregion
}
