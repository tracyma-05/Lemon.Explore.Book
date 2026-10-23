using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Relativity;

/// <summary>
/// The light-clock view: two lanes, a ship flying above and the ground below, each carrying a light clock — a flash
/// bouncing between two mirrors, one tick per round trip. In the frame being watched one clock stands still and its
/// light goes straight up and down; the other moves, so its light has to run the long slanted way and it ticks slower.
/// </summary>
public sealed partial class RelativityDiagram
{
    /// <summary>Frame time per second of watching: light crosses a clock's gap in 0.9 s.</summary>
    private const double ClockRate = 1 / 0.9;

    /// <summary>The frame's time since the clocks were set to 0 (light crosses the gap in 1).</summary>
    private double _clockTime;

    /// <summary>Faint stars for the ship's lane, made once (x and y as fractions of the lane, brightness).</summary>
    private static readonly (double X, double Y, double B)[] LaneStars = MakeStars(70, 17);

    private static (double X, double Y, double B)[] MakeStars(int count, int seed)
    {
        var random = new Random(seed);
        return [.. Enumerable.Range(0, count).Select(_ => (random.NextDouble(), random.NextDouble(), 0.25 + (random.NextDouble() * 0.6)))];
    }

    private void ResetClocks()
    {
        _clockTime = 0;
        SetCurrentValue(StillTicksProperty, 0);
        SetCurrentValue(MovingTicksProperty, 0);
    }

    private void StepClocks(double dt)
    {
        _clockTime += dt * ClockRate;
        var still = RelativityModel.Flash(_clockTime, 1, 0).Ticks;
        var moving = RelativityModel.Flash(_clockTime, 1, Beta).Ticks;
        if (still != StillTicks) SetCurrentValue(StillTicksProperty, still);
        if (moving != MovingTicks) SetCurrentValue(MovingTicksProperty, moving);
    }

    /// <summary>Where the lanes and the clocks go.</summary>
    private readonly record struct ClockLayout(Rect Stage, Rect ShipLane, Rect GroundLane, double Gap, double Span, double WrapLeft);

    private ClockLayout LayOutClocks()
    {
        var stage = Stage();
        var laneHeight = (stage.Height - 16) / 2;
        var ship = new Rect(stage.Left, stage.Top, stage.Width, laneHeight);
        var ground = new Rect(stage.Left, stage.Top + laneHeight + 16, stage.Width, laneHeight);
        var gap = Math.Clamp(laneHeight * 0.56, 70, 190);
        return new ClockLayout(stage, ship, ground, gap, stage.Width + 220, stage.Left - 110);
    }

    /// <summary>The bottom mirror's middle of the clock in <paramref name="lane"/> when it is at <paramref name="x"/>.</summary>
    private static Point ClockBase(ClockLayout layout, Rect lane, double x) => new(x, lane.Top + (lane.Height / 2) + (layout.Gap / 2));

    /// <summary>
    /// Where the moving clock is (unwrapped, pixels) at frame time <paramref name="s"/>: it starts on one side and goes
    /// across, coming round again when it leaves the picture.
    /// </summary>
    private double MovingX(ClockLayout layout, double s)
    {
        var u = MovingVelocity(layout);
        var x0 = Frame == RelativityFrame.Ground ? layout.Stage.Left + (layout.Stage.Width * 0.2) : layout.Stage.Left + (layout.Stage.Width * 0.8);
        return x0 + (u * s);
    }

    /// <summary>The moving clock's speed across the picture, pixels per unit of frame time (light's is the gap).</summary>
    private double MovingVelocity(ClockLayout layout) => (Frame == RelativityFrame.Ground ? 1 : -1) * Beta * layout.Gap;

    /// <summary>How many times the moving clock has gone round the picture by <paramref name="s"/>.</summary>
    private static int Wraps(ClockLayout layout, double x) => (int)Math.Floor((x - layout.WrapLeft) / layout.Span);

    private void DrawClockMode(PaintContext dc, double now)
    {
        var layout = LayOutClocks();
        var t = _clockTime;
        var groundMoves = Frame == RelativityFrame.Ship;
        var groundShift = groundMoves ? -Beta * layout.Gap * t : 0;

        DrawShipLane(dc, layout, groundShift);
        DrawGroundLane(dc, layout, groundShift);

        // The clock standing still in this frame is in the middle of its lane.
        var stillLane = groundMoves ? layout.ShipLane : layout.GroundLane;
        var movingLane = groundMoves ? layout.GroundLane : layout.ShipLane;
        var stillX = layout.Stage.Left + (layout.Stage.Width * 0.5);
        var unwrapped = MovingX(layout, t);
        var wraps = Wraps(layout, unwrapped);
        var movingX = unwrapped - (wraps * layout.Span);

        // The ship around its clock, the ground's clock on a stand.
        if (groundMoves)
        {
            DrawShip(dc, ClockBase(layout, layout.ShipLane, stillX), layout.Gap, moving: false);
            DrawClockStand(dc, ClockBase(layout, layout.GroundLane, movingX), layout.Gap, layout.GroundLane, moving: Beta > 0.01);
        }
        else
        {
            DrawShip(dc, ClockBase(layout, layout.ShipLane, movingX), layout.Gap, moving: Beta > 0.01);
            DrawClockStand(dc, ClockBase(layout, layout.GroundLane, stillX), layout.Gap, layout.GroundLane, moving: false);
        }

        // The still clock: straight up and down.
        var stillBase = ClockBase(layout, stillLane, stillX);
        var still = RelativityModel.Flash(t, 1, 0);
        DrawLightClock(dc, stillBase, layout.Gap, still.Height, groundMoves ? "shipclock" : "groundclock", now, straightPath: true);
        var stillFlash = stillBase + new Vector(0, -still.Height * layout.Gap);

        // The moving clock: its light's zigzag since it came into the picture, then the clock and its flash.
        var movingBase = ClockBase(layout, movingLane, movingX);
        var moving = RelativityModel.Flash(t, 1, Beta);
        if (ShowTrail && Beta > 0.01) DrawZigzag(dc, layout, movingLane, t, wraps);
        if (ShowTriangle && Beta > 0.02) DrawTriangle(dc, layout, movingLane, t, wraps);
        DrawLightClock(dc, movingBase, layout.Gap, moving.Height, groundMoves ? "groundclock" : "shipclock", now, straightPath: Beta <= 0.01);
        var movingFlash = movingBase + new Vector(0, -moving.Height * layout.Gap);
        DrawFlash(dc, stillFlash, 5, Ink.Light);
        DrawFlash(dc, movingFlash, 5, Ink.Light);
        _targets.Add("flash", movingFlash, 14);
        if (Beta <= 0.01) _targets.Add("flash", stillFlash, 14);

        // Each clock's ticks, by its lane.
        DrawCounter(dc, layout.ShipLane, groundMoves ? StillTicks : MovingTicks, "Rel_Clock_ShipTicks", Ink.Cyan, groundMoves ? t : t / RelativityModel.Gamma(Beta));
        DrawCounter(dc, layout.GroundLane, groundMoves ? MovingTicks : StillTicks, "Rel_Clock_GroundTicks", Ink.Green, groundMoves ? t / RelativityModel.Gamma(Beta) : t);

        if (ShowLabels)
        {
            var frameText = Loc(groundMoves ? "Rel_Frame_SeenFromShip" : "Rel_Frame_SeenFromGround");
            DrawTargetPill(dc, frameText, new Point(layout.Stage.Right - 130, layout.Stage.Top + 12), Ink.Gold, "frame", 12);
            var gamma = RelativityModel.Gamma(Beta);
            DrawTargetPill(dc, LocFormat("Rel_Clock_GammaPill", Number(Beta, "0.00"), Number(gamma, gamma < 10 ? "0.00" : "0.0")),
                new Point(layout.Stage.Left + (layout.Stage.Width / 2), layout.GroundLane.Top - 8), Ink.Gold, "gamma", 12);
        }
    }

    /// <summary>The ship's lane: dark space with stars that drift past when the ship is the one standing still.</summary>
    private void DrawShipLane(PaintContext dc, ClockLayout layout, double groundShift)
    {
        var lane = layout.ShipLane;
        dc.DrawRoundedRectangle(Ink.SpaceLane, Ink.Faint, lane, 14, 14);
        dc.PushClip(new RectangleGeometry(lane, 14, 14));
        foreach (var (x, y, b) in LaneStars)
        {
            var sx = lane.Left + Wrap((x * lane.Width) + (groundShift * 0.35), lane.Width);
            dc.DrawEllipse(FadedBrush(Ink.Text, b), null, new Point(sx, lane.Top + (y * lane.Height)), 1.1, 1.1);
        }

        dc.Pop();
        AddBoxTarget("ship", new Rect(lane.Left + 8, lane.Top + 6, 150, 24));
        DrawText(dc, Loc("Rel_Lane_Ship"), new Point(lane.Left + 16, lane.Top + 18), Ink.Cyan, 12, bold: true);
    }

    /// <summary>The ground's lane: sky going down to a platform whose posts slide by when the ship stands still.</summary>
    private void DrawGroundLane(PaintContext dc, ClockLayout layout, double groundShift)
    {
        var lane = layout.GroundLane;
        dc.DrawRoundedRectangle(Ink.GroundSky, Ink.Faint, lane, 14, 14);
        dc.PushClip(new RectangleGeometry(lane, 14, 14));
        var floor = lane.Top + (lane.Height / 2) + (layout.Gap / 2) + 30;
        dc.DrawRectangle(Ink.Platform, null, new Rect(lane.Left, floor, lane.Width, lane.Bottom - floor));
        dc.DrawLine(FadedPen(Ink.Steel, 0.5, 1.5), new Point(lane.Left, floor), new Point(lane.Right, floor));
        const double postGap = 120;
        for (var x = Wrap(groundShift, postGap) - postGap; x < lane.Width + postGap; x += postGap)
        {
            var px = lane.Left + x;
            dc.DrawLine(FadedPen(Ink.Steel, 0.35, 3, flat: true), new Point(px, floor + 6), new Point(px, lane.Bottom));
            dc.DrawLine(FadedPen(Ink.Steel, 0.18, 1), new Point(px - 30, floor + 14), new Point(px + 30, floor + 14));
        }

        dc.Pop();
        AddBoxTarget("ground", new Rect(lane.Left + 8, lane.Top + 6, 150, 24));
        DrawText(dc, Loc("Rel_Lane_Ground"), new Point(lane.Left + 16, lane.Top + 18), Ink.Green, 12, bold: true);
    }

    private static double Wrap(double x, double span) => ((x % span) + span) % span;

    /// <summary>A light clock: two mirrors <paramref name="gap"/> apart in a glass tube, the bottom one's middle at <paramref name="bottom"/>.</summary>
    private void DrawLightClock(PaintContext dc, Point bottom, double gap, double flashHeight, string id, double now, bool straightPath)
    {
        var top = bottom + new Vector(0, -gap);
        const double half = 30;
        dc.DrawRoundedRectangle(Ink.Glass, FadedPen(Ink.Steel, 0.35, 1), new Rect(top.X - half + 6, top.Y, (half - 6) * 2, gap), 6, 6);
        if (straightPath) dc.DrawLine(FadedPen(Ink.Light, 0.35, 2), bottom + new Vector(0, -3), top + new Vector(0, 3));
        DrawMirror(dc, top, half, facingDown: true);
        DrawMirror(dc, bottom, half, facingDown: false);
        _targets.Add(id, new Point(bottom.X, bottom.Y - (gap / 2)), Math.Max(24, gap / 2.4));
        if (id == "groundclock" || id == "shipclock")
        {
            AddBoxTarget("mirror", new Rect(top.X - half, top.Y - 8, half * 2, 10));
        }
    }

    private void DrawMirror(PaintContext dc, Point middle, double half, bool facingDown)
    {
        var y = middle.Y + (facingDown ? -6 : 0);
        dc.DrawRoundedRectangle(Ink.MirrorBack, null, new Rect(middle.X - half, y, half * 2, 6), 2, 2);
        var face = facingDown ? y + 6 : y;
        dc.DrawLine(FadedPen(Ink.Text, 0.95, 1.6, flat: true), new Point(middle.X - half + 2, face), new Point(middle.X + half - 2, face));
    }

    /// <summary>The ship around its clock, nose to the right; speed lines behind it when it moves.</summary>
    private void DrawShip(PaintContext dc, Point clockBottom, double gap, bool moving)
    {
        var body = new Rect(clockBottom.X - 78, clockBottom.Y - gap - 26, 156, gap + 46);
        if (moving)
        {
            for (var i = 0; i < 4; i++)
            {
                var y = body.Top + (body.Height * (0.2 + (0.2 * i)));
                dc.DrawLine(FadedPen(Ink.Cyan, 0.35 - (i * 0.05), 1.5), new Point(body.Left - 70 - (i * 12), y), new Point(body.Left - 12, y));
            }

            var flame = Line([new Point(body.Left, body.Top + (body.Height * 0.32)), new Point(body.Left - 34, body.Top + (body.Height * 0.5)), new Point(body.Left, body.Top + (body.Height * 0.68))], closed: true, filled: true);
            dc.DrawGeometry(Ink.Flame, null, flame);
        }

        // Fins, hull and nose.
        var finTop = Line([new Point(body.Left + 6, body.Top + 8), new Point(body.Left - 18, body.Top - 16), new Point(body.Left + 40, body.Top + 2)], closed: true, filled: true);
        var finBottom = Line([new Point(body.Left + 6, body.Bottom - 8), new Point(body.Left - 18, body.Bottom + 16), new Point(body.Left + 40, body.Bottom - 2)], closed: true, filled: true);
        dc.DrawGeometry(Ink.Fin, null, finTop);
        dc.DrawGeometry(Ink.Fin, null, finBottom);
        var nose = Line([new Point(body.Right - 2, body.Top + 4), new Point(body.Right + 46, body.Top + (body.Height / 2)), new Point(body.Right - 2, body.Bottom - 4)], closed: true, filled: true);
        dc.DrawGeometry(Ink.Hull, FadedPen(Ink.Steel, 0.6, 1.2), nose);
        dc.DrawRoundedRectangle(Ink.Hull, FadedPen(Ink.Steel, 0.6, 1.2), body, 18, 18);
        dc.DrawEllipse(Ink.Window, FadedPen(Ink.Cyan, 0.6, 1.2), new Point(body.Right + 6, body.Top + (body.Height / 2)), 9, 9);
        AddBoxTarget("ship", new Rect(body.Left - 18, body.Top, body.Width + 64, 18));
    }

    /// <summary>The ground's clock on a stand, with someone standing beside it; speed lines behind it when it moves.</summary>
    private void DrawClockStand(PaintContext dc, Point clockBottom, double gap, Rect lane, bool moving)
    {
        var floor = lane.Top + (lane.Height / 2) + (gap / 2) + 30;
        dc.DrawRectangle(Ink.Stand, null, new Rect(clockBottom.X - 12, clockBottom.Y + 2, 24, floor - clockBottom.Y - 2));
        dc.DrawRectangle(Ink.Stand, null, new Rect(clockBottom.X - 30, floor - 6, 60, 6));
        if (moving)
        {
            for (var i = 0; i < 3; i++)
            {
                var y = clockBottom.Y - (gap * (0.25 + (0.3 * i)));
                dc.DrawLine(FadedPen(Ink.Green, 0.3, 1.5), new Point(clockBottom.X + 46, y), new Point(clockBottom.X + 110 + (i * 10), y));
            }
        }

        // A person beside the clock, for scale and to say whose clock it is.
        var feet = new Point(clockBottom.X - 58, floor);
        var person = FadedPen(Ink.Green, 0.85, 2.2);
        var head = feet + new Vector(0, -64);
        dc.DrawEllipse(null, person, head, 7, 7);
        dc.DrawLine(person, head + new Vector(0, 7), feet + new Vector(0, -26));
        dc.DrawLine(person, feet + new Vector(0, -26), feet + new Vector(-8, 0));
        dc.DrawLine(person, feet + new Vector(0, -26), feet + new Vector(8, 0));
        dc.DrawLine(person, head + new Vector(0, 18), head + new Vector(-10, 34));
        dc.DrawLine(person, head + new Vector(0, 18), head + new Vector(12, 30));
        _targets.Add("observer", head + new Vector(0, 26), 22);
    }

    /// <summary>
    /// The moving clock's light since the clock came into the picture: straight between bounces, slanted because the
    /// clock moves on while the light goes up and down. Its corners are where the light hits a mirror.
    /// </summary>
    private void DrawZigzag(PaintContext dc, ClockLayout layout, Rect lane, double t, int wraps)
    {
        var u = MovingVelocity(layout);
        if (Math.Abs(u) < 1e-6) return;
        var x0 = MovingX(layout, 0);
        var enter = u > 0 ? layout.WrapLeft + (wraps * layout.Span) : layout.WrapLeft + ((wraps + 1) * layout.Span);
        var start = Math.Max(0, (enter - x0) / u);
        var half = RelativityModel.TickTime(1, Beta) / 2;
        Point At(double s)
        {
            var b = ClockBase(layout, lane, MovingX(layout, s) - (wraps * layout.Span));
            return b + new Vector(0, -RelativityModel.Flash(s, 1, Beta).Height * layout.Gap);
        }

        var points = new List<Point> { At(start) };
        for (var k = Math.Floor(start / half) + 1; k * half < t; k++) points.Add(At(k * half));
        points.Add(At(t));
        if (points.Count < 2) return;
        var zigzag = Line(points);
        if (Glows("zigzag")) dc.DrawGeometry(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 9), zigzag);
        dc.DrawGeometry(null, FadedPen(Ink.Light, 0.9, 2.2), zigzag);

        // Clickable along its length.
        for (var i = 1; i < points.Count; i++) _targets.Add("zigzag", points[i - 1] + ((points[i] - points[i - 1]) / 2), 12);
    }

    /// <summary>
    /// The light-clock triangle on the moving clock's current trip between mirrors: the slanted path c·t, the distance the
    /// clock moves on v·t, and the gap c·t₀ that its own light crosses. Pythagoras gives t = γ·t₀.
    /// </summary>
    private void DrawTriangle(PaintContext dc, ClockLayout layout, Rect lane, double t, int wraps)
    {
        var half = RelativityModel.TickTime(1, Beta) / 2;
        var leg = Math.Floor(t / half);
        var up = ((int)leg % 2) == 0;
        Point Base(double s) => ClockBase(layout, lane, MovingX(layout, s) - (wraps * layout.Span));
        var a = Base(leg * half);
        var b = Base((leg + 1) * half);
        if (Math.Min(a.X, b.X) < layout.Stage.Left + 4 || Math.Max(a.X, b.X) > layout.Stage.Right - 4) return;
        var gap = new Vector(0, -layout.Gap);

        // Up: from the bottom mirror at the start to the top one at the end. Down: from the top at the start to the bottom at the end.
        var from = up ? a : a + gap;
        var to = up ? b + gap : b;
        var corner = up ? b : b + gap;
        var pulse = Glows("triangle") ? ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 9) : null;
        if (pulse is not null) dc.DrawGeometry(null, pulse, Line([from, corner, to], closed: true));
        dc.DrawLine(FadedPen(Ink.Cyan, 0.95, 2.4), from, corner);
        dc.DrawLine(FadedPen(Ink.Green, 0.95, 2.4), corner, to);
        dc.DrawLine(FadedPen(Ink.Gold, 0.95, 2.4, dashed: true), from, to);

        var gamma = RelativityModel.Gamma(Beta);
        var mid = from + ((to - from) / 2);
        var normal = new Vector(-(to - from).Y, (to - from).X);
        normal = normal.Normalize();
        if (normal * (corner - mid) > 0) normal = -normal;
        DrawText(dc, Loc("Rel_Tri_Slant"), mid + (normal * 18), Ink.Gold, 12, TextAnchor.Center, bold: true);
        DrawText(dc, Loc("Rel_Tri_Along"), from + ((corner - from) / 2) + new Vector(0, up ? 14 : -14), Ink.Cyan, 12, TextAnchor.Center, bold: true);
        DrawText(dc, Loc("Rel_Tri_Up"), corner + ((to - corner) / 2) + new Vector(Math.Sign((b - a).X) * 10, 0), Ink.Green, 12,
            (b - a).X >= 0 ? TextAnchor.Left : TextAnchor.Right, bold: true);
        // (the box round the three corners: Avalonia's Union takes no point)
        var box = new Rect(
            new Point(Math.Min(Math.Min(from.X, to.X), corner.X), Math.Min(Math.Min(from.Y, to.Y), corner.Y)),
            new Point(Math.Max(Math.Max(from.X, to.X), corner.X), Math.Max(Math.Max(from.Y, to.Y), corner.Y)));
        box = box.Inflate(new Thickness(6, 6));
        AddBoxTarget("triangle", box);
        // The sum under the triangle, when the lane has room for it above the speed pill.
        if (ShowLabels && box.Bottom + 40 < lane.Bottom)
        {
            var below = new Point(box.Left + (box.Width / 2), box.Bottom + 26);
            DrawTargetPill(dc, LocFormat("Rel_Tri_Pill", Number(gamma, "0.00")), below, Ink.Gold, "triangle", 11);
        }
    }

    /// <summary>A lane's tick count and its clock's dial, at the lane's left.</summary>
    private void DrawCounter(PaintContext dc, Rect lane, int ticks, string key, Color color, double ownTime)
    {
        var dial = new Point(lane.Left + 40, lane.Top + (lane.Height / 2) + 4);
        var box = DrawDial(dc, dial, 22, ownTime, 24, color, null);
        var text = DrawText(dc, LocFormat(key, ticks), new Point(lane.Left + 16, lane.Bottom - 18), color, 12.5, bold: true);
        box = box.Union(text);
        AddBoxTarget("counter", box);
    }
}
