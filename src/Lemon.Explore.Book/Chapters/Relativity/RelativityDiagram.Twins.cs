using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Relativity;

/// <summary>
/// The twins on a spacetime diagram: distance from Earth across, Earth's time up, one light-year as long as one year so
/// light runs at 45°. Earth's twin stays on the upright line; the traveller goes out to the star and back, and the year
/// marks on the traveller's line are further apart. Under it a strip shows where Earth, the ship and the star are now;
/// beside it the two twins' ages.
/// </summary>
public sealed partial class RelativityDiagram
{
    /// <summary>The slowest trip drawn: below this the trip is so long the diagram is a thin sliver.</summary>
    internal const double SlowestTrip = 0.3;

    /// <summary>Seconds the trip waits at its end before it starts again.</summary>
    private const double TwinRest = 2.5;

    private double _twinWait;

    /// <summary>The speed the twins' trip is drawn at for the page's speed.</summary>
    internal static double TripSpeed(double beta) => Math.Max(beta, SlowestTrip);

    private TwinTrip Trip() => RelativityModel.Trip(Distance, TripSpeed(Beta));

    private void StepTwins(double dt)
    {
        var trip = Trip();
        if (TwinTime >= trip.EarthYears)
        {
            _twinWait += dt;
            if (_twinWait < TwinRest) return;
            _twinWait = 0;
            SetCurrentValue(TwinTimeProperty, 0.0);
            return;
        }

        _twinWait = 0;

        // The whole trip plays in about twelve seconds.
        SetCurrentValue(TwinTimeProperty, Math.Min(trip.EarthYears, TwinTime + (dt * trip.EarthYears / 12)));
    }

    /// <summary>Where the diagram goes: the plot, its origin (Earth at the start), pixels per year, the strip and the twins' panel.</summary>
    private readonly record struct TwinLayout(Rect Plot, Point Origin, double Scale, Rect Strip, Rect Panel, double XMin, double XMax);

    private TwinLayout LayOutTwins(TwinTrip trip)
    {
        var stage = Stage();
        var panelWidth = Math.Min(240, stage.Width * 0.3);
        var room = new Rect(stage.Left + 70, stage.Top + 40, Math.Max(80, stage.Width - panelWidth - 70 - 150), Math.Max(80, stage.Height - 40 - 110));
        var xMin = -0.6;
        var xMax = trip.Distance + 0.6;
        var top = trip.EarthYears * 1.04;
        var scale = Math.Min(room.Width / (xMax - xMin), room.Height / top);
        var plot = new Rect(room.Left, room.Bottom - (top * scale), (xMax - xMin) * scale, top * scale);
        var origin = new Point(plot.Left - (xMin * scale), plot.Bottom);
        var strip = new Rect(plot.Left, plot.Bottom + 34, plot.Width, 62);

        // The twins' panel beside the diagram, past the pills on its right.
        var panel = new Rect(Math.Min(plot.Right + 130, stage.Right - panelWidth - 16), plot.Top, panelWidth, Math.Max(120, Math.Min(250, stage.Height - 40)));
        return new TwinLayout(plot, origin, scale, strip, panel, xMin, xMax);
    }

    private void DrawTwinsMode(PaintContext dc, double now)
    {
        var trip = Trip();
        var layout = LayOutTwins(trip);
        var t = Math.Clamp(TwinTime, 0, trip.EarthYears);
        Point P(double x, double years) => new(layout.Origin.X + (x * layout.Scale), layout.Origin.Y - (years * layout.Scale));

        DrawTwinGrid(dc, layout, trip, P);

        // Light from the start: 45°, nothing can go flatter.
        var lightTop = Math.Min(trip.EarthYears * 1.04, layout.XMax);
        var lightPen = FadedPen(Ink.Light, 0.55, 1.4, dashed: true);
        if (Glows("lightline")) dc.DrawLine(ExploreTargets.GlowPen(now, 8), P(0, 0), P(lightTop, lightTop));
        dc.DrawLine(lightPen, P(0, 0), P(lightTop, lightTop));
        dc.DrawLine(lightPen, P(0, 0), P(layout.XMin, -layout.XMin));
        _targets.Add("lightline", P(lightTop * 0.75, lightTop * 0.75), 12);
        if (ShowLabels) DrawText(dc, Loc("Rel_Twin_Light"), P(lightTop * 0.75, lightTop * 0.75) + new Vector(10, 8), Ink.Light, 10.5);

        // The star stays where it is.
        dc.DrawLine(FadedPen(Ink.Gold, 0.5, 1.4, dashed: true), P(trip.Distance, 0), P(trip.Distance, trip.EarthYears * 1.04));

        if (ShowAxes) DrawShipAxes(dc, layout, trip, P);
        if (ShowNowLines) DrawNowLines(dc, layout, trip, t, P, now);
        if (ShowSignals) DrawSignals(dc, trip, t, P);

        // Earth's worldline, then the ship's: faint for the future, bright for what has happened.
        var earthPen = FadedPen(Ink.Green, 1, 3);
        var shipPen = FadedPen(Ink.Cyan, 1, 3);
        if (Glows("earthline")) dc.DrawLine(ExploreTargets.GlowPen(now, 10), P(0, 0), P(0, trip.EarthYears));
        if (Glows("shipline")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 10), Line([P(0, 0), P(trip.Distance, trip.Turn), P(0, trip.EarthYears)]));
        dc.DrawLine(FadedPen(Ink.Green, 0.3, 2), P(0, 0), P(0, trip.EarthYears));
        dc.DrawGeometry(null, FadedPen(Ink.Cyan, 0.3, 2), Line([P(0, 0), P(trip.Distance, trip.Turn), P(0, trip.EarthYears)]));
        dc.DrawLine(earthPen, P(0, 0), P(0, t));
        var shipSoFar = t <= trip.Turn ? new[] { P(0, 0), P(trip.ShipX(t), t) } : [P(0, 0), P(trip.Distance, trip.Turn), P(trip.ShipX(t), t)];
        dc.DrawGeometry(null, shipPen, Line(shipSoFar));
        for (var k = 1; k <= 3; k++) _targets.Add("earthline", P(0, trip.EarthYears * k / 4), 10);
        _targets.Add("shipline", P(trip.Distance * 0.5, trip.Turn * 0.5), 12);
        _targets.Add("shipline", P(trip.Distance * 0.5, trip.Turn * 1.5), 12);
        _targets.Add("turn", P(trip.Distance, trip.Turn), 14);

        DrawYearMarks(dc, trip, P);

        // Earth's "now": everything on this level line happens at the same Earth time.
        dc.DrawLine(FadedPen(Ink.Text, 0.55, 1.2, dashed: true), P(layout.XMin, t), P(layout.XMax, t));
        dc.DrawEllipse(FadedBrush(Ink.Green, 1), FadedPen(Ink.Text, 1, 1.5), P(0, t), 6, 6);
        dc.DrawEllipse(FadedBrush(Ink.Cyan, 1), FadedPen(Ink.Text, 1, 1.5), P(trip.ShipX(t), t), 6, 6);
        _targets.Add("now", P(layout.XMax - 0.3, t), 10);
        if (ShowLabels)
        {
            var nowPill = DrawTargetPill(dc, LocFormat("Rel_Twin_NowPill", Number(t, "0.0")), P(layout.XMax, t) + new Vector(54, 0), Ink.Text, "now", 11);
            var turnAt = P(trip.Distance, trip.Turn) + new Vector(54, 0);
            if (Math.Abs(turnAt.Y - (nowPill.Top + (nowPill.Height / 2))) < 24) turnAt = turnAt.WithY(turnAt.Y - 26);
            DrawTargetPill(dc, Loc("Rel_Twin_Turn"), turnAt, Ink.Cyan, "turn", 11);
        }

        DrawSpaceStrip(dc, layout, trip, t);
        DrawTwinPanel(dc, layout, trip, t);
    }

    private void DrawTwinGrid(PaintContext dc, TwinLayout layout, TwinTrip trip, Func<double, double, Point> p)
    {
        var plot = layout.Plot;
        dc.DrawRoundedRectangle(Ink.PlotBack, null, new Rect(plot.Left - 6, plot.Top - 6, plot.Width + 12, plot.Height + 12), 8, 8);
        var yearStep = YearStep(layout.Scale);
        for (var y = 0.0; y <= trip.EarthYears * 1.04; y += yearStep)
        {
            dc.DrawLine(Ink.Grid, p(layout.XMin, y), p(layout.XMax, y));
            DrawText(dc, Number(y, "0"), p(layout.XMin, y) + new Vector(-8, 0), Ink.Dim, 10, TextAnchor.Right);
        }

        var lyStep = YearStep(layout.Scale);
        for (var x = 0.0; x <= layout.XMax; x += lyStep)
        {
            dc.DrawLine(Ink.Grid, p(x, 0), p(x, trip.EarthYears * 1.04));
            DrawText(dc, Number(x, "0"), p(x, 0) + new Vector(0, 10), Ink.Dim, 10, TextAnchor.Center);
        }

        dc.DrawLine(FadedPen(Ink.Dim, 0.6, 1), p(layout.XMin, 0), p(layout.XMax, 0));
        var timeLabel = DrawText(dc, Loc("Rel_Twin_AxisTime"), new Point(plot.Left - 56, plot.Top - 14), Ink.Dim, 10.5);
        var spaceLabel = DrawText(dc, Loc("Rel_Twin_AxisSpace"), p(layout.XMax, 0) + new Vector(0, 22), Ink.Dim, 10.5, TextAnchor.Right);
        AddBoxTarget("grid", timeLabel);
        AddBoxTarget("grid", spaceLabel);
    }

    /// <summary>A step for year lines that keeps them at least ~22 px apart.</summary>
    private static double YearStep(double scale) => scale >= 22 ? 1 : scale * 2 >= 22 ? 2 : scale * 5 >= 22 ? 5 : scale * 10 >= 22 ? 10 : 20;

    /// <summary>A mark for every birthday on each line: Earth's a year apart, the traveller's γ years of Earth time apart.</summary>
    private void DrawYearMarks(PaintContext dc, TwinTrip trip, Func<double, double, Point> p)
    {
        var step = Math.Max(1, YearStep(Math.Abs(p(0, 1).Y - p(0, 0).Y)) / 2);
        for (var y = 1.0; y <= trip.EarthYears + 1e-9; y += step)
        {
            var at = p(0, y);
            dc.DrawLine(FadedPen(Ink.Green, 0.95, 2), at + new Vector(-6, 0), at + new Vector(6, 0));
        }

        var count = (int)Math.Floor(trip.ShipYears + 1e-9);
        for (var k = 1; k <= count; k += (int)step)
        {
            var e = k * trip.Gamma;
            var at = p(trip.ShipX(e), e);

            // Across the ship's line.
            var dir = e <= trip.Turn ? new Vector(1, -trip.Beta) : new Vector(-1, -trip.Beta);
            dir = dir.Normalize();
            var across = new Vector(dir.Y, -dir.X) * 6;
            dc.DrawLine(FadedPen(Ink.Cyan, 0.95, 2), at - across, at + across);
        }

        var marks = p(trip.ShipX(trip.Gamma), trip.Gamma);
        _targets.Add("shipmarks", marks, 10);
        _targets.Add("earthmarks", p(0, 1), 9);
    }

    /// <summary>The traveller's time and space axes on the way out: tilted towards the light line from both sides.</summary>
    private void DrawShipAxes(PaintContext dc, TwinLayout layout, TwinTrip trip, Func<double, double, Point> p)
    {
        var pen = FadedPen(Ink.Violet, 0.9, 1.6, dashed: true);
        var timeEnd = Math.Min(trip.EarthYears * 1.02, layout.XMax / trip.Beta);
        dc.DrawLine(pen, p(0, 0), p(trip.Beta * timeEnd, timeEnd));
        var spaceEnd = layout.XMax;
        dc.DrawLine(pen, p(0, 0), p(spaceEnd, trip.Beta * spaceEnd));
        var a = DrawText(dc, Loc("Rel_Twin_ShipTime"), p(trip.Beta * timeEnd, timeEnd) + new Vector(6, 6), Ink.Violet, 10.5);
        var b = DrawText(dc, Loc("Rel_Twin_ShipSpace"), p(spaceEnd, trip.Beta * spaceEnd) + new Vector(-4, -12), Ink.Violet, 10.5, TextAnchor.Right);
        AddBoxTarget("shipaxes", a);
        AddBoxTarget("shipaxes", b);
    }

    /// <summary>
    /// The traveller's line of "now" through the ship, and the two either side of the turn: on the way out the ship
    /// calls an early Earth year "now", on the way back a late one, so at the turn its "now" on Earth jumps.
    /// </summary>
    private void DrawNowLines(PaintContext dc, TwinLayout layout, TwinTrip trip, double t, Func<double, double, Point> p, double now)
    {
        Point Line(double x0, double t0, bool outbound, double x) => p(x, t0 + ((outbound ? 1 : -1) * trip.Beta * (x - x0)));
        var before = trip.Turn - (trip.Beta * trip.Distance);
        var after = trip.Turn + (trip.Beta * trip.Distance);
        var faint = FadedPen(Ink.Violet, 0.45, 1.2);
        dc.DrawLine(faint, Line(trip.Distance, trip.Turn, true, layout.XMin), Line(trip.Distance, trip.Turn, true, trip.Distance));
        dc.DrawLine(faint, Line(trip.Distance, trip.Turn, false, layout.XMin), Line(trip.Distance, trip.Turn, false, trip.Distance));

        // The jump on Earth's line.
        var bracket = new Rect(p(0, after) + new Vector(-14, 0), p(0, before) + new Vector(-8, 0));
        if (Glows("jump")) dc.DrawRectangle(null, ExploreTargets.GlowPen(now, 6), bracket);
        dc.DrawLine(FadedPen(Ink.Violet, 0.9, 3, flat: true), p(0, before) + new Vector(-11, 0), p(0, after) + new Vector(-11, 0));
        AddBoxTarget("jump", bracket);
        if (ShowLabels) DrawTargetPill(dc, Loc("Rel_Twin_Jump"), p(0, (before + after) / 2) + new Vector(28, 0), Ink.Violet, "jump", 10.5);

        // The ship's "now" at this moment.
        var x = trip.ShipX(t);
        var outbound = t <= trip.Turn;
        var pen = FadedPen(Ink.Violet, 0.95, 2);
        if (Glows("shipnow")) dc.DrawLine(ExploreTargets.GlowPen(now, 8), Line(x, t, outbound, layout.XMin), Line(x, t, outbound, layout.XMax));
        dc.DrawLine(pen, Line(x, t, outbound, layout.XMin), Line(x, t, outbound, layout.XMax));
        var onEarth = t + ((outbound ? -1 : 1) * trip.Beta * x);
        dc.DrawEllipse(null, FadedPen(Ink.Violet, 1, 2), p(0, onEarth), 5, 5);
        _targets.Add("shipnow", p(0, onEarth), 10);
    }

    /// <summary>A light signal on every birthday: Earth's to the ship, the ship's home, drawn as far as they have got by now.</summary>
    private void DrawSignals(PaintContext dc, TwinTrip trip, double t, Func<double, double, Point> p)
    {
        var earthPen = FadedPen(Ink.Green, 0.75, 1.3);
        for (var k = 1; k < trip.EarthYears && k <= t; k++)
        {
            var end = trip.ArrivesAtShip(k) ?? trip.EarthYears;
            var upTo = Math.Min(t, end);
            dc.DrawLine(earthPen, p(0, k), p(upTo - k, upTo));
            if (upTo >= end && trip.ArrivesAtShip(k) is not null) dc.DrawEllipse(FadedBrush(Ink.Green, 1), null, p(trip.ShipX(end), end), 2.5, 2.5);
        }

        var shipPen = FadedPen(Ink.Pink, 0.8, 1.3);
        for (var k = 1; k * trip.Gamma < trip.EarthYears && k * trip.Gamma <= t; k++)
        {
            var sent = k * trip.Gamma;
            var x = trip.ShipX(sent);
            var end = trip.ArrivesAtEarth(sent);
            var upTo = Math.Min(t, end);
            dc.DrawLine(shipPen, p(x, sent), p(x - (upTo - sent), upTo));
            if (upTo >= end) dc.DrawEllipse(FadedBrush(Ink.Pink, 1), null, p(0, end), 2.5, 2.5);
        }

        var first = trip.ArrivesAtShip(1);
        if (t >= 1 && first is { } meet) _targets.Add("signals", p((Math.Min(t, meet) - 1) / 2, (1 + Math.Min(t, meet)) / 2), 10);
    }

    /// <summary>Under the diagram, on the same distance scale: Earth, the star and the ship where they are now.</summary>
    private void DrawSpaceStrip(PaintContext dc, TwinLayout layout, TwinTrip trip, double t)
    {
        var strip = layout.Strip;
        dc.DrawRoundedRectangle(Ink.Panel, Ink.Frame, strip, 10, 10);
        var y = strip.Top + (strip.Height / 2) + 6;
        double X(double ly) => layout.Origin.X + (ly * layout.Scale);
        dc.DrawLine(FadedPen(Ink.Dim, 0.4, 1, dashed: true), new Point(X(0), y), new Point(X(trip.Distance), y));
        dc.DrawEllipse(Ink.EarthBall, FadedPen(Ink.Green, 0.9, 1.4), new Point(X(0), y), 9, 9);
        DrawStar(dc, new Point(X(trip.Distance), y), 9);
        var shipAt = new Point(X(trip.ShipX(t)), y - 2);
        var facing = t < trip.Turn ? 1 : -1;
        var ship = Line([shipAt + new Vector(12 * facing, 0), shipAt + new Vector(-8 * facing, -6), shipAt + new Vector(-8 * facing, 6)], closed: true, filled: true);
        if (t > 0 && t < trip.EarthYears) dc.DrawGeometry(FadedBrush(Ink.Cyan, 1), null, ship);
        _targets.Add("strip", shipAt, 14);
        AddBoxTarget("strip", new Rect(strip.Left, strip.Top, 90, 18));
        DrawText(dc, Loc("Rel_Twin_Strip"), new Point(strip.Left + 10, strip.Top + 11), Ink.Dim, 10.5);
        _targets.Add("star", new Point(X(trip.Distance), y), 12);
        if (ShowLabels)
        {
            DrawText(dc, Loc("Rel_Twin_Earth"), new Point(X(0), y + 20), Ink.Green, 10.5, TextAnchor.Center);
            DrawText(dc, LocFormat("Rel_Twin_StarName", Number(trip.Distance, "0.#")), new Point(X(trip.Distance), y + 20), Ink.Gold, 10.5, TextAnchor.Center);
        }
    }

    private void DrawStar(PaintContext dc, Point at, double r)
    {
        var points = new List<Point>();
        for (var i = 0; i < 10; i++)
        {
            var a = (i * Math.PI / 5) - (Math.PI / 2);
            var rr = i % 2 == 0 ? r : r * 0.45;
            points.Add(at + new Vector(Math.Cos(a) * rr, Math.Sin(a) * rr));
        }

        dc.DrawEllipse(FadedBrush(Ink.Gold, 0.2), null, at, r * 1.8, r * 1.8);
        dc.DrawGeometry(FadedBrush(Ink.Gold, 1), null, Line(points, closed: true, filled: true));
    }

    /// <summary>The twins side by side: how many years each has lived since the ship left.</summary>
    private void DrawTwinPanel(PaintContext dc, TwinLayout layout, TwinTrip trip, double t)
    {
        var panel = layout.Panel;
        var height = panel.Height;
        var box = new Rect(panel.Left, panel.Top, panel.Width, height);
        dc.DrawRoundedRectangle(Ink.Panel, Ink.Frame, box, 14, 14);
        var earthYears = t;
        var shipYears = trip.ShipTime(t);
        var column = box.Width / 2;
        var dialY = box.Top + 92;
        var r = Math.Min(34, column / 2 - 12);
        var e = DrawDial(dc, new Point(box.Left + (column / 2), dialY), r, earthYears, 12, Ink.Green, null);
        var s = DrawDial(dc, new Point(box.Left + (column * 1.5), dialY), r, shipYears, 12, Ink.Cyan, null);
        DrawText(dc, Loc("Rel_Twin_EarthTwin"), new Point(box.Left + (column / 2), box.Top + 26), Ink.Green, 12, TextAnchor.Center, bold: true);
        DrawText(dc, Loc("Rel_Twin_Traveller"), new Point(box.Left + (column * 1.5), box.Top + 26), Ink.Cyan, 12, TextAnchor.Center, bold: true);
        // On the book's narrower page the panel is narrower too: the ages shrink to stay apart.
        var size = Math.Clamp(column / 6.5, 12, 17);
        e = e.Union(DrawText(dc, LocFormat("Rel_Value_PlusYears", Number(earthYears, "0.0")), new Point(box.Left + (column / 2), dialY + r + 22), Ink.Green, size, TextAnchor.Center, bold: true));
        s = s.Union(DrawText(dc, LocFormat("Rel_Value_PlusYears", Number(shipYears, "0.0")), new Point(box.Left + (column * 1.5), dialY + r + 22), Ink.Cyan, size, TextAnchor.Center, bold: true));
        AddBoxTarget("earthtwin", e);
        AddBoxTarget("traveller", s);

        var gap = earthYears - shipYears;
        var text = t >= trip.EarthYears - 1e-9 ? LocFormat("Rel_Twin_HomePill", Number(gap, "0.0")) : LocFormat("Rel_Twin_Gap", Number(gap, "0.0"));
        var gapBox = DrawPill(dc, text, new Point(box.Left + (box.Width / 2), dialY + r + 62), t >= trip.EarthYears - 1e-9 ? Ink.Gold : Ink.Text, 11.5);
        AddBoxTarget("younger", gapBox);
        DrawText(dc, LocFormat("Rel_Twin_Speed", Number(trip.Beta, "0.00")), new Point(box.Left + (box.Width / 2), box.Bottom - 22), Ink.Dim, 10.5, TextAnchor.Center);
    }
}
