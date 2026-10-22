using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.WaterCycle;

/// <summary>Cycle mode: the landscape from the sea over the mountain, every way water moves in it, and a followed drop.</summary>
public sealed partial class WaterCycleDiagram
{
    /// <summary>
    /// Where the sky's top (<see cref="CycleScene.SkyTop"/>) is drawn: the title sits over the empty top of the sky, but in
    /// a narrow window, where the title wraps across the whole width, the sky starts lower.
    /// </summary>
    private double CycleTop => Math.Clamp(64 + ((1000 - ActualWidth) * 0.28), 64, 150);

    /// <summary>How much the processes not picked are dimmed.</summary>
    private const double Dimmed = 0.22;

    private IReadOnlyList<DropStep> _journey = [];
    private IReadOnlyList<ScenePoint> _river = [];
    private double _dropTime;

    /// <summary>The landscape's coordinates on screen.</summary>
    private readonly record struct SceneView(Rect Stage)
    {
        public double X(double x) => Stage.Left + (x * Stage.Width);

        public double Y(double z) => Stage.Top + ((CycleScene.SkyTop - z) / (CycleScene.SkyTop - CycleScene.Bottom) * Stage.Height);

        public Point P(double x, double z) => new(X(x), Y(z));

        public Point P(ScenePoint point) => P(point.X, point.Z);

        /// <summary>Pixels per metre up.</summary>
        public double PerMetre => Stage.Height / (CycleScene.SkyTop - CycleScene.Bottom);
    }

    /// <summary>The route, the warmth or following changed: work the journey out again (from the start, unless only the warmth changed).</summary>
    private void OnJourneyChanged()
    {
        var restart = _journey.Count == 0 || _journeyRoute != Route || _journeyFollow != FollowDrop;
        _journeyRoute = Route;
        _journeyFollow = FollowDrop;
        _journey = CycleScene.Journey(Route, Warming);
        _river = CycleScene.River(Warming);
        if (restart)
        {
            _dropTime = 0;
            Push(LegIndexProperty, 0);
            return;
        }

        _dropTime = Math.Min(_dropTime, _journey.Sum(s => s.Seconds) - 1e-6);
    }

    private DropRoute _journeyRoute;
    private bool _journeyFollow;

    private void AdvanceDrop(double step)
    {
        if (!FollowDrop || _journey.Count == 0) return;
        var total = _journey.Sum(s => s.Seconds);
        _dropTime += step * 0.8;
        if (_dropTime >= total) _dropTime %= total;
        Push(LegIndexProperty, DropAt(_dropTime).Index);
    }

    /// <summary>The step a drop is on after <paramref name="time"/> seconds of its journey, and the seconds into that step.</summary>
    private (int Index, double Seconds) DropAt(double time)
    {
        for (var i = 0; i < _journey.Count; i++)
        {
            if (time < _journey[i].Seconds) return (i, time);
            time -= _journey[i].Seconds;
        }

        return (_journey.Count - 1, _journey[^1].Seconds);
    }

    /// <summary>The way of moving lit up: the followed drop's, or the one picked.</summary>
    private WaterProcess ActiveProcess
    {
        get
        {
            if (!FollowDrop || _journey.Count == 0) return SelectedProcess;
            var index = Math.Clamp(LegIndex, 0, _journey.Count - 1);
            return WaterCycleModel.ProcessOf(_journey[index].Leg);
        }
    }

    private double Emphasis(WaterProcess process)
    {
        var active = ActiveProcess;
        return active == WaterProcess.None || active == process ? 1 : Dimmed;
    }

    private void DrawCycle(PaintContext dc, double width, double height)
    {
        var view = new SceneView(Stage(CycleTop));
        var freezing = CycleScene.FreezingLevel(Warming);
        var snowLine = CycleScene.SnowLine(Warming);

        dc.DrawRectangle(Ink.Sky, null, new Rect(0, 0, width, height));
        DrawSun(dc, view.P(0.93, 6250));
        _targets.Add("sun", view.P(0.93, 6250), 24);

        // The freezing level across the sky: above it what falls is snow.
        var freezingY = view.Y(freezing);
        dc.DrawLine(Ink.Freezing, new Point(0, freezingY), new Point(width, freezingY));

        DrawGround(dc, view, width, height);
        DrawSea(dc, view);
        DrawSnowcap(dc, view, snowLine);
        DrawTrees(dc, view);

        dc.PushOpacity(Emphasis(WaterProcess.Groundwater));
        DrawGroundwaterFlow(dc, view);
        dc.Pop();
        dc.PushOpacity(Emphasis(WaterProcess.Infiltration));
        DrawInfiltration(dc, view);
        dc.Pop();
        dc.PushOpacity(Emphasis(WaterProcess.Runoff));
        DrawRiver(dc, view);
        dc.Pop();
        dc.PushOpacity(Emphasis(WaterProcess.Snowmelt));
        DrawSnowmelt(dc, view, snowLine);
        dc.Pop();

        DrawVapour(dc, view, snowLine);
        DrawSeaClouds(dc, view, freezing);
        DrawMountainCloud(dc, view, freezing);
        dc.PushOpacity(Emphasis(WaterProcess.Transport));
        DrawWind(dc, view);
        dc.Pop();

        if (ShowLabels)
        {
            AddBoxTarget("freezing", DrawPill(dc, LocFormat("Water_Freezing_Label", Number(freezing, "N0")), new Point(view.X(0.045), freezingY), Ink.Cyan, 10.5));
            DrawProcessLabels(dc, view, snowLine);
        }
        if (FollowDrop) DrawDrop(dc, view);
    }

    #region the landscape

    private void DrawSun(PaintContext dc, Point center)
    {
        var pulse = 1 + (0.04 * Math.Sin(_t * 1.3));
        dc.DrawEllipse(Ink.SunGlow, null, center, 74 * pulse, 74 * pulse);
        dc.DrawEllipse(Ink.SunCore, null, center, 17, 17);
    }

    /// <summary>The rock under everything, the water-soaked rock below the water table, and grass or bare rock on top.</summary>
    private static void DrawGround(PaintContext dc, SceneView view, double width, double height)
    {
        var bottom = Math.Max(height, view.Y(CycleScene.Bottom));
        var ground = new StreamGeometry();
        using (var context = ground.Open())
        {
            context.BeginFigure(new Point(0, bottom), true);
            for (var i = 0; i <= 240; i++)
            {
                var x = i / 240.0;
                context.LineTo(view.P(x, CycleScene.Ground(x)));
            }

            context.LineTo(new Point(width, bottom));

            context.EndFigure(true);
        }
        dc.DrawGeometry(Ink.Rock, null, ground);

        // Water-soaked rock: below the water table under the land, and everything under the sea floor.
        var soaked = new StreamGeometry();
        using (var context = soaked.Open())
        {
            context.BeginFigure(new Point(0, bottom), true);
            for (var i = 0; i <= 80; i++)
            {
                var x = CycleScene.Coast * i / 80;
                context.LineTo(view.P(x, CycleScene.Ground(x)));
            }

            for (var i = 0; i <= 160; i++)
            {
                var x = CycleScene.Coast + ((1 - CycleScene.Coast) * i / 160);
                context.LineTo(view.P(x, CycleScene.WaterTable(x)));
            }

            context.LineTo(new Point(width, bottom));

            context.EndFigure(true);
        }
        dc.DrawGeometry(Ink.Saturated, null, soaked);
        dc.DrawGeometry(null, Ink.WaterTable, Line(view, CycleScene.Coast, 1, 160, CycleScene.WaterTable));

        // Grass on the lowlands, bare rock higher up.
        var treeLine = CycleScene.WindwardAt(1700);
        dc.DrawGeometry(null, Ink.Grass, Line(view, CycleScene.Coast, treeLine, 80, CycleScene.Ground));
        dc.DrawGeometry(null, Ink.Bare, Line(view, treeLine, 1, 120, CycleScene.Ground));
    }

    /// <summary>A line through <paramref name="height"/> from <paramref name="from"/> to <paramref name="to"/>.</summary>
    private static StreamGeometry Line(SceneView view, double from, double to, int count, Func<double, double> height, double lift = 0)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var i = 0; i <= count; i++)
            {
                var x = from + ((to - from) * i / count);
                var point = view.P(x, height(x)) - new Vector(0, lift);
                if (i == 0) context.BeginFigure(point, false);
                else context.LineTo(point);
            }

            context.EndFigure(false);
        }
        return geometry;
    }

    private void DrawSea(PaintContext dc, SceneView view)
    {
        var sea = new StreamGeometry();
        using (var context = sea.Open())
        {
            context.BeginFigure(view.P(0, 0), true);
            context.LineTo(view.P(CycleScene.Coast, 0));
            for (var i = 0; i <= 80; i++)
            {
                var x = CycleScene.Coast * (1 - (i / 80.0));
                context.LineTo(view.P(x, CycleScene.Ground(x)));
            }

            context.EndFigure(true);
        }
        dc.DrawGeometry(Ink.Sea, null, sea);

        // Waves on the surface.
        var waves = new StreamGeometry();
        using (var context = waves.Open())
        {
            var surface = view.Y(0);
            var right = view.X(CycleScene.Coast) - 2;
            context.BeginFigure(new Point(0, surface), false);
            for (var x = 0.0; x <= right; x += 4)
            {
                context.LineTo(new Point(x, surface + (1.6 * Math.Sin((x / 13) + (_t * 2.2)))));
            }

            context.EndFigure(false);
        }
        dc.DrawGeometry(null, Ink.Wave, waves);
    }

    /// <summary>Snow lying above the snow line, thicker toward the top.</summary>
    private static void DrawSnowcap(PaintContext dc, SceneView view, double snowLine)
    {
        var points = new List<(double X, double Z)>();
        for (var i = 0; i <= 200; i++)
        {
            var x = 0.6 + (0.4 * i / 200);
            var z = CycleScene.Ground(x);
            if (z > snowLine) points.Add((x, z));
        }

        if (points.Count < 2) return;
        var peak = CycleScene.Ground(CycleScene.PeakX);
        var cap = new StreamGeometry();
        using (var context = cap.Open())
        {
            context.BeginFigure(view.P(points[0].X, points[0].Z), true);
            foreach (var (x, z) in points)
            {
                var depth = 3 + (8 * Math.Clamp((z - snowLine) / Math.Max(1, peak - snowLine), 0, 1));
                context.LineTo(view.P(x, z) - new Vector(0, depth));
            }

            for (var i = points.Count - 1; i >= 0; i--) context.LineTo(view.P(points[i].X, points[i].Z) + new Vector(0, 1.5));

            context.EndFigure(true);
        }
        dc.DrawGeometry(Ink.Snowcap, Ink.SnowEdge, cap);
    }

    /// <summary>The trees of the plain and the lower slope.</summary>
    private static void DrawTrees(PaintContext dc, SceneView view)
    {
        foreach (var x in TreeXs)
        {
            var foot = view.P(x, CycleScene.Ground(x));
            var tall = Math.Max(12, CycleScene.TreeHeight(x) * view.PerMetre);
            dc.DrawRectangle(Ink.Trunk, null, new Rect(foot.X - 1.5, foot.Y - (tall * 0.45), 3, tall * 0.45));
            dc.DrawEllipse(Ink.Leaves, null, new Point(foot.X, foot.Y - (tall * 0.62)), tall * 0.3, tall * 0.38);
            dc.DrawEllipse(Ink.LeavesLight, null, new Point(foot.X - (tall * 0.08), foot.Y - (tall * 0.7)), tall * 0.16, tall * 0.2);
        }
    }

    private static readonly double[] TreeXs = [0.365, 0.385, 0.405, 0.43, 0.45, 0.465, 0.49, 0.51, 0.53, 0.55, 0.575, 0.6];

    #endregion

    #region water moving

    /// <summary>Vapour rising from the sea, from the leaves and from the snow.</summary>
    private void DrawVapour(PaintContext dc, SceneView view, double snowLine)
    {
        // From the sea: more in a warmer world, as warmer air holds more vapour.
        dc.PushOpacity(Emphasis(WaterProcess.Evaporation));
        var count = (int)Math.Round(34 * WaterCycleModel.VapourFactor(CycleScene.SeaLevelTemperature, Warming));
        for (var i = 0; i < count; i++)
        {
            var s1 = Frac(i * 0.6180339887);
            var s2 = Frac((i * 0.7548776662) + 0.13);
            var age = Frac((_t / 7.5) + s1);
            var x = 0.012 + (0.285 * s2) + (0.05 * age * age) + (0.004 * Math.Sin((age * 9) + i));
            var z = 20 + (age * (CycleScene.CloudBase - 250));
            DrawVapourDot(dc, view.P(x, z), age, Ink.Vapour);
        }

        dc.Pop();

        // From the leaves of the trees.
        dc.PushOpacity(Emphasis(WaterProcess.Transpiration));
        for (var i = 0; i < TreeXs.Length * 2; i++)
        {
            var tree = TreeXs[i % TreeXs.Length];
            var crown = CycleScene.Ground(tree) + (CycleScene.TreeHeight(tree) * 0.85);
            var age = Frac((_t / 6) + Frac(i * 0.7548776662));
            var x = tree + (0.025 * age * age) + (0.003 * Math.Sin((age * 8) + i));
            DrawVapourDot(dc, view.P(x, crown + (age * 1000)), age, Ink.LeafVapour);
        }

        dc.Pop();

        // Straight from the snow to vapour.
        dc.PushOpacity(Emphasis(WaterProcess.Sublimation));
        var from = CycleScene.WindwardAt(snowLine);
        if (!double.IsNaN(from))
        {
            for (var i = 0; i < 12; i++)
            {
                var x0 = from + ((0.97 - from) * Frac(i * 0.6180339887));
                var z0 = CycleScene.Ground(x0);
                if (z0 < snowLine) continue;
                var age = Frac((_t / 6.5) + Frac(i * 0.4142135624));
                DrawVapourDot(dc, view.P(x0 + (0.03 * age), z0 + 40 + (age * 800)), age, Ink.Vapour);
            }
        }

        dc.Pop();
    }

    private static void DrawVapourDot(PaintContext dc, Point at, double age, IBrush brush)
    {
        var alpha = Smooth(0, 0.12, age) * (1 - Smooth(0.65, 1, age));
        if (alpha <= 0.02) return;
        dc.PushOpacity(alpha);
        dc.DrawEllipse(brush, null, at, 1.9, 1.9);
        dc.Pop();
    }

    /// <summary>Rain soaking down from the surface to the water table.</summary>
    private void DrawInfiltration(PaintContext dc, SceneView view)
    {
        for (var i = 0; i < 20; i++)
        {
            var x = 0.36 + (0.36 * Frac(i * 0.6180339887));
            var top = CycleScene.Ground(x) - 15;
            var bottom = CycleScene.WaterTable(x) + 10;
            if (top <= bottom) continue;
            var age = Frac((_t / 4.5) + Frac(i * 0.7548776662));
            var at = view.P(x, WaterCycleModel.Lerp(top, bottom, age));
            dc.PushOpacity(Smooth(0, 0.15, age) * (1 - Smooth(0.8, 1, age)));
            dc.DrawLine(Ink.Seep, at, at + new Vector(0, 4));
            dc.Pop();
        }
    }

    /// <summary>Groundwater creeping through the rock toward the sea, seeping out near the shore.</summary>
    private void DrawGroundwaterFlow(PaintContext dc, SceneView view)
    {
        for (var i = 0; i < 28; i++)
        {
            var s1 = Frac(i * 0.6180339887);
            var s2 = Frac((i * 0.7548776662) + 0.27);
            var start = 0.42 + (0.38 * s2);
            var end = CycleScene.Coast - 0.022;
            var age = Frac((_t / 13) + s1);
            var x = WaterCycleModel.Lerp(start, end, age);
            var depth = (60 + (420 * Frac(i * 0.2360679775))) * Smooth(CycleScene.Coast - 0.02, CycleScene.Coast + 0.12, x);
            var z = x >= CycleScene.Coast ? CycleScene.WaterTable(x) - depth : CycleScene.Ground(x) - (depth * 0.3) - 10;
            dc.PushOpacity(Smooth(0, 0.1, age) * (1 - Smooth(0.9, 1, age)));
            dc.DrawEllipse(Ink.WaterDot, null, view.P(x, z), 1.8, 1.8);
            dc.Pop();
        }
    }

    /// <summary>The river from the snow down to the sea, wider as it goes, its water running.</summary>
    private void DrawRiver(PaintContext dc, SceneView view)
    {
        var river = _river;
        if (river.Count < 2) return;
        for (var i = 1; i < river.Count; i++)
        {
            var f = i / (double)(river.Count - 1);
            var pen = new ImmutablePen(((IBrush)Ink.River).ToImmutable(), 1.6 + (2.6 * f), null, PenLineCap.Round, PenLineJoin.Miter);
            dc.DrawLine(pen, view.P(river[i - 1]) - new Vector(0, 1.5), view.P(river[i]) - new Vector(0, 1.5));
        }

        // Bright flecks running down it.
        for (var k = 0; k < 16; k++)
        {
            var u = Frac((k / 16.0) + (_t * 0.07));
            var at = u * (river.Count - 1);
            var index = Math.Min(river.Count - 2, (int)at);
            var a = view.P(river[index]);
            var b = view.P(river[index + 1]);
            var p = a + ((b - a) * (at - index)) - new Vector(0, 1.5);
            Vector direction = b - a;
            if (direction.Length < 1e-6) continue;
            direction /= direction.Length;
            dc.DrawLine(Ink.Flow, p - (direction * 2.5), p + (direction * 2.5));
        }
    }

    /// <summary>Meltwater trickling from the snow down to where the river rises.</summary>
    private void DrawSnowmelt(PaintContext dc, SceneView view, double snowLine)
    {
        var source = CycleScene.RiverSource(Warming);
        var top = Math.Min(0.795, source + 0.04);
        for (var i = 0; i < 7; i++)
        {
            var age = Frac((_t / 3.2) + Frac(i * 0.6180339887));
            var x = WaterCycleModel.Lerp(top, source, age);
            var z = CycleScene.Ground(x);
            dc.PushOpacity(Smooth(0, 0.15, age) * (1 - Smooth(0.85, 1, age)));
            dc.DrawEllipse(Ink.WaterDot, null, view.P(x, z) - new Vector(0, 2), 1.7, 1.7);
            dc.Pop();
        }
    }

    /// <summary>Clouds forming over the sea and drifting inland, some raining as they go.</summary>
    private void DrawSeaClouds(PaintContext dc, SceneView view, double freezing)
    {
        for (var i = 0; i < 3; i++)
        {
            var u = Frac((_t / 38) + (i / 3.0));
            var x = -0.1 + (u * 0.7);
            var opacity = Smooth(-0.1, 0.0, x) * (1 - Smooth(0.5, 0.6, x));
            var w = (0.07 + (0.05 * Smooth(-0.1, 0.35, x))) * CloudUnit(view);
            var center = view.P(x, 2350 + (180 * Math.Sin(i * 2.1)));

            // Raining over the sea, and again over the plain.
            var rain = Smooth(0.11, 0.14, x) * (1 - Smooth(0.22, 0.25, x)) + (Smooth(0.41, 0.44, x) * (1 - Smooth(0.5, 0.53, x)));
            if (rain > 0.02)
            {
                dc.PushOpacity(rain * opacity * Emphasis(WaterProcess.Precipitation));
                for (var k = 0; k < 12; k++)
                {
                    var px = center.X + ((Frac(k * 0.6180339887) - 0.5) * w * 0.7);
                    var sx = (px - view.Stage.Left) / view.Stage.Width;
                    var groundZ = Math.Max(0, CycleScene.Ground(sx));
                    var topY = view.Y(CycleScene.CloudBase + 200);
                    var age = Frac((_t / 1.1) + Frac(k * 0.7548776662));
                    var y = WaterCycleModel.Lerp(topY, view.Y(groundZ), age);
                    DrawRainStreak(dc, new Point(px, y), 9);
                }

                dc.Pop();
            }

            dc.PushOpacity(Emphasis(WaterProcess.Condensation));
            DrawCloud(dc, center, w, w * 0.5, opacity, 11 + i, dark: rain > 0.3);
            dc.Pop();
        }
    }

    /// <summary>The cloud the mountain lifts the air into, and the rain and snow falling from it onto the slope.</summary>
    private void DrawMountainCloud(PaintContext dc, SceneView view, double freezing)
    {
        // Rain below the freezing level, snow above it.
        dc.PushOpacity(Emphasis(WaterProcess.Precipitation));
        for (var i = 0; i < 56; i++)
        {
            var x = CycleScene.RainFrom + ((CycleScene.RainTo - CycleScene.RainFrom) * Frac(i * 0.6180339887));
            var ground = CycleScene.Ground(x);
            var top = Math.Max(CycleScene.CloudBase + 150, ground + 700);
            var snowing = ground > freezing;
            var age = Frac((_t / (snowing ? 3.2 : 1.3)) + Frac(i * 0.7548776662));
            var z = WaterCycleModel.Lerp(top, ground, age);
            var at = view.P(x + (snowing ? 0.003 * Math.Sin((age * 12) + i) : 0), z);
            if (z > freezing) DrawFlake(dc, at, 2.6);
            else DrawRainStreak(dc, at, 9);
        }

        dc.Pop();

        dc.PushOpacity(Emphasis(WaterProcess.Condensation));
        var width = CloudUnit(view);
        var breathe = 1 + (0.025 * Math.Sin(_t * 0.6));
        DrawCloud(dc, view.P(0.6, 2250), width * 0.12 * breathe, width * 0.06, 0.95, 31, dark: true);
        DrawCloud(dc, view.P(0.67, 2900), width * 0.14 * breathe, width * 0.07, 0.95, 32, dark: true);
        DrawCloud(dc, view.P(0.72, 3700), width * 0.13 * breathe, width * 0.065, 0.95, 33, withBase: false);
        DrawCloud(dc, view.P(0.65, 3700), width * 0.1 * breathe, width * 0.06, 0.9, 34, withBase: false);
        DrawCloud(dc, view.P(0.7, 4500), width * 0.09 * breathe, width * 0.05, 0.85, 35, withBase: false);
        dc.Pop();
    }

    /// <summary>What clouds are sized by: the stage's width, but no wider than its height allows, so they stay cloud-shaped on wide screens.</summary>
    private static double CloudUnit(SceneView view) => Math.Min(view.Stage.Width, view.Stage.Height * 1.55);

    /// <summary>The wind carrying moist air from the sea toward the mountain.</summary>
    private void DrawWind(PaintContext dc, SceneView view)
    {
        for (var k = 0; k < 7; k++)
        {
            var u = Frac((_t / 8) + (k / 7.0));
            var x = 0.03 + (u * 0.5);
            var z = k % 2 == 0 ? 3150 : 3650;
            var from = view.P(x, z);
            var to = from + new Vector(view.Stage.Width * 0.045, 0);
            dc.PushOpacity(Smooth(0, 0.12, u) * (1 - Smooth(0.82, 1, u)));
            DrawArrow(dc, from, to, Ink.Wind, 1.6, 7);
            dc.Pop();
        }
    }

    #endregion

    #region labels and the drop

    /// <summary>A label for every way of moving, with a line to where it happens; clicking one lights it up.</summary>
    private void DrawProcessLabels(PaintContext dc, SceneView view, double snowLine)
    {
        var source = CycleScene.RiverSource(Warming);
        var labels = new (WaterProcess Process, ScenePoint Pill, ScenePoint Anchor)[]
        {
            (WaterProcess.Evaporation, new(0.1, 950), new(0.12, 120)),
            (WaterProcess.Condensation, new(0.2, 3550), new(0.24, 2650)),
            (WaterProcess.Transport, new(0.36, 4250), new(0.33, 3500)),
            (WaterProcess.Transpiration, new(0.45, 1250), new(0.46, 620)),
            (WaterProcess.Precipitation, new(0.535, 2050), new(0.6, 1500)),
            (WaterProcess.Sublimation, new(0.92, 5300), new(0.835, 4480)),
            (WaterProcess.Snowmelt, new(0.78, 5850), new(source + 0.01, CycleScene.Ground(source + 0.01))),
            (WaterProcess.Runoff, new(0.4, -330), new(0.42, CycleScene.Ground(0.42))),
            (WaterProcess.Infiltration, new(0.64, -420), new(0.6, CycleScene.Ground(0.6) - 150)),
            (WaterProcess.Groundwater, new(0.5, -1150), new(0.5, CycleScene.WaterTable(0.5) - 250)),
        };

        var active = ActiveProcess;
        foreach (var (process, pill, anchor) in labels)
        {
            var isActive = process == active;
            var center = view.P(pill);
            var target = view.P(anchor);
            dc.PushOpacity(active == WaterProcess.None || isActive ? 1 : 0.55);
            dc.DrawLine(isActive ? Ink.LeaderBright : Ink.Leader, center, target);
            dc.DrawEllipse(isActive ? Ink.DropCore : Ink.Wind, null, target, 2.4, 2.4);
            var rect = DrawPill(dc, Loc($"Water_Process_{process}"), center, isActive ? Ink.Gold : Ink.Text, 12);
            dc.Pop();
            AddBoxTarget(ProcessId(process), rect);
        }

        // Behind the mountain: the air that came over has rained out.
        AddBoxTarget("rainshadow", DrawText(dc, Loc("Water_RainShadow"), view.P(0.935, 2150), Ink.Dim, 11, center: true));
        DrawText(dc, Loc("Water_Sea_Label"), view.P(0.07, -260), Ink.Dim, 11, center: true);
        AddBoxTarget("snowline", DrawText(dc, LocFormat("Water_SnowLine_Label", Number(snowLine, "N0")), new Point(view.X(0.86), view.Y(snowLine) + 2), Ink.Snow, 10.5));
    }

    /// <summary>The followed drop, glowing, with the way it is going now.</summary>
    private void DrawDrop(PaintContext dc, SceneView view)
    {
        if (_journey.Count == 0) return;
        var (index, seconds) = DropAt(_dropTime);
        var step = _journey[index];

        if (step.Path.Count > 1)
        {
            var path = new StreamGeometry();
            using (var context = path.Open())
            {
                context.BeginFigure(view.P(step.Path[0]), false);
                for (var i = 1; i < step.Path.Count; i++) context.LineTo(view.P(step.Path[i]));

                context.EndFigure(false);
            }
            dc.DrawGeometry(null, Ink.DropTrail, path);
        }

        var at = view.P(CycleScene.Along(step, seconds));
        var pulse = 1 + (0.15 * Math.Sin(_t * 5));
        dc.DrawEllipse(Ink.DropGlow, null, at, 15 * pulse, 15 * pulse);
        dc.DrawEllipse(Ink.DropCore, null, at, 4.5, 4.5);
        _targets.Add("drop", at, 14);
        DrawPill(dc, Loc($"Water_Leg_{step.Leg}"), at + new Vector(0, -24), Ink.Gold, 12);
    }

    #endregion
}
