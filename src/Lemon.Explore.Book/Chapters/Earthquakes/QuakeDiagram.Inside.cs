using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Earthquakes;

/// <summary>
/// The inside view: a slice through the Earth with its layers to scale, a quake on the rim, the P and S wavefronts
/// spreading through it (from the traced rays), S stopping at the liquid core, the shadow zones on the far side, stations
/// on the rim that light up as the waves arrive; beside it the textbook chart of wave speed against depth and a true-scale
/// look at the top 700 km (crust, plate, the soft asthenosphere).
/// </summary>
public sealed partial class QuakeDiagram
{
    /// <summary>A minute of wave travel goes by in a second.</summary>
    private const double WaveSpeedUp = 60;

    /// <summary>The waves are followed for 25 minutes; by then even the ones through the centre have arrived.</summary>
    public const double WaveDuration = 1500;

    private Point _earthCentre;
    private double _earthRadius;
    private Rect _speedChart;
    private Rect _magnifier;

    /// <summary>Which side of the quake the picked station is on: +1 clockwise, −1 the other way.</summary>
    private int _stationSide = 1;

    private static readonly Lazy<(double PEnd, double PkpStart, double SEnd)> Shadows = new(() =>
    {
        var pEnd = EarthModel.Fan(WaveKind.P).Where(r => r.Phase == Phase.P).Max(r => r.Distance);
        var pkp = EarthModel.Fan(WaveKind.P).Where(r => r.Phase == Phase.PKP).Min(r => r.Distance);
        var sEnd = EarthModel.Fan(WaveKind.S).Where(r => r.Phase == Phase.S).Max(r => r.Distance);
        return (pEnd, pkp, sEnd);
    });

    /// <summary>Where the direct P waves stop, where the waves through the core come back, and where the S waves stop, °.</summary>
    public static (double PEnd, double PkpStart, double SEnd) ShadowZones => Shadows.Value;

    private void StepWaves(double dt)
    {
        if (!IsPlaying || _drag == DragKind.Quake) return;
        var t = WaveTime + (WaveSpeedUp * dt);
        if (t >= WaveDuration)
        {
            t = WaveDuration;
            SetCurrentValue(IsPlayingProperty, false);
        }

        SetCurrentValue(WaveTimeProperty, t);
    }

    /// <summary>A point in the slice: radius (km) and angle from the quake (rad) on one side (+1 clockwise).</summary>
    private Point EarthPoint(double radiusKm, double angle, int side)
    {
        var phi = (QuakeAngle * Math.PI / 180) + (side * angle);
        var r = radiusKm / EarthModel.Radius * _earthRadius;
        return new Point(_earthCentre.X + (r * Math.Sin(phi)), _earthCentre.Y - (r * Math.Cos(phi)));
    }

    private void DrawInsideMode(PaintContext dc, double now)
    {
        var stage = Stage();
        var column = stage.Width > 760 ? Math.Clamp(stage.Width * 0.3, 230, 330) : 0;
        _earthRadius = Math.Max(60, Math.Min((stage.Height / 2) - 22, (stage.Width - column - 80) / 2));
        _earthCentre = new Point(stage.X + 40 + _earthRadius + Math.Max(0, (stage.Width - column - 80 - (2 * _earthRadius)) / 2), stage.Y + (stage.Height / 2));

        DrawEarthLayers(dc);
        if (ShowRays) DrawRayTrails(dc);
        DrawWavefronts(dc);
        DrawSurfaceWaves(dc);
        DrawShadowZones(dc);
        DrawStations(dc);
        DrawQuakeMark(dc, now);
        DrawEarthLabels(dc);

        if (column > 0)
        {
            var x = stage.Right - column - 18;
            var chartHeight = stage.Height > 420 ? stage.Height * 0.62 : stage.Height - 10;
            _speedChart = new Rect(x, stage.Y + 4, column, chartHeight);
            DrawSpeedChart(dc);
            if (stage.Height > 420)
            {
                _magnifier = new Rect(x, _speedChart.Bottom + 14, column, stage.Bottom - _speedChart.Bottom - 18);
                DrawMagnifier(dc);
            }
            else
            {
                _magnifier = default(Rect);
            }
        }
        else
        {
            _speedChart = default(Rect);
            _magnifier = default(Rect);
        }
    }

    #region the slice

    private void DrawEarthLayers(PaintContext dc)
    {
        var c = _earthCentre;
        double Px(double depth) => (EarthModel.Radius - depth) / EarthModel.Radius * _earthRadius;

        dc.DrawEllipse(Ink.CrustRing, null, c, _earthRadius, _earthRadius);
        dc.DrawEllipse(Ink.UpperMantle, null, c, Px(Math.Max(EarthModel.Moho, 6371 * 1.6 / _earthRadius)), Px(Math.Max(EarthModel.Moho, 6371 * 1.6 / _earthRadius)));

        // The asthenosphere, a soft band in the upper mantle.
        var outer = Px(EarthModel.AsthenosphereTop);
        var inner = Px(EarthModel.AsthenosphereBottom);
        dc.DrawEllipse(null, FadedPen(Ink.Orange, 0.28, outer - inner, flat: true), c, (outer + inner) / 2, (outer + inner) / 2);

        dc.DrawEllipse(Ink.LowerMantle, null, c, Px(EarthModel.UpperMantle), Px(EarthModel.UpperMantle));
        dc.DrawEllipse(Ink.OuterCore, null, c, Px(EarthModel.Gutenberg), Px(EarthModel.Gutenberg));
        dc.DrawEllipse(Ink.InnerCore, null, c, Px(EarthModel.InnerCore), Px(EarthModel.InnerCore));

        // The two big boundaries as lines.
        dc.DrawEllipse(null, FadedPen(Ink.Gold, 0.55, 1.2), c, Px(EarthModel.Gutenberg), Px(EarthModel.Gutenberg));
        dc.DrawEllipse(null, FadedPen(Colors.White, 0.35, 1), c, Px(EarthModel.InnerCore), Px(EarthModel.InnerCore));
        dc.DrawEllipse(null, FadedPen(Colors.White, 0.5, 1), c, _earthRadius, _earthRadius);

        // Glow for a highlighted layer.
        if (Highlight is "gutenberg") dc.DrawEllipse(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds), c, Px(EarthModel.Gutenberg), Px(EarthModel.Gutenberg));
    }

    private RenderTargetBitmap? _rayImage;
    private double _rayImageRadius;

    /// <summary>
    /// The rays, faint, once the quake has gone off: about one for every degree of take-off. They are the same whatever
    /// the quake's place, so they are drawn once (quake at the top) into a picture that is turned to the quake.
    /// </summary>
    private void DrawRayTrails(PaintContext dc)
    {
        if (WaveTime <= 0) return;
        if (_rayImage is null || Math.Abs(_rayImageRadius - _earthRadius) > 0.5)
        {
            _rayImageRadius = _earthRadius;
            _rayImage = RenderRays();
        }

        var half = _earthRadius + 2;
        dc.PushTransform(SpaceDrawing.Rotation(QuakeAngle, _earthCentre.X, _earthCentre.Y));
        dc.DrawImage(_rayImage, new Rect(_earthCentre.X - half, _earthCentre.Y - half, half * 2, half * 2));
        dc.Pop();
    }

    private RenderTargetBitmap RenderRays()
    {
        var dpi = RenderScale();
        var half = _earthRadius + 2;
        var centre = new Point(half, half);
        return SpaceDrawing.RenderToBitmap(half * 2, half * 2, dpi, dc =>
        {
            foreach (var kind in new[] { WaveKind.P, WaveKind.S })
            {
                // One geometry per ray: many crossing lines in one geometry make WPF merge them on the render thread.
                var pen = FadedPen(kind == WaveKind.P ? Ink.Gold : Ink.Cyan, 0.24, 0.8, flat: true);
                var lastTakeoff = -10.0;
                foreach (var ray in EarthModel.Fan(kind))
                {
                    var gap = ray.Takeoff < 14 && kind == WaveKind.P ? 0.6 : 2.2;
                    if (ray.Takeoff - lastTakeoff < gap) continue;
                    lastTakeoff = ray.Takeoff;
                    foreach (var side in new[] { -1, 1 })
                    {
                        var points = new List<Point>(ray.Radius.Length / 2);
                        for (var i = 0; i < ray.Radius.Length; i += i + 3 < ray.Radius.Length ? 3 : 1)
                        {
                            var r = ray.Radius[i] / EarthModel.Radius * _earthRadius;
                            var angle = side * ray.Angle[i];
                            points.Add(new Point(centre.X + (r * Math.Sin(angle)), centre.Y - (r * Math.Cos(angle))));
                        }

                        dc.DrawGeometry(null, pen, Line(points));
                    }
                }
            }
        });
    }

    /// <summary>The wavefronts: every ray's place at this moment, joined to its neighbour where they are close.</summary>
    private void DrawWavefronts(PaintContext dc)
    {
        var t = WaveTime;
        if (t <= 0) return;
        var glowP = Highlight == "pwave";
        var glowS = Highlight == "swave";
        foreach (var kind in new[] { WaveKind.S, WaveKind.P })
        {
            var fan = EarthModel.Fan(kind);
            var geometry = new StreamGeometry();
            var join = _earthRadius * 0.09;
            Point? labelAt = null;
            using (var ctx = geometry.Open())
            {
                foreach (var side in new[] { -1, 1 })
                {
                    Point? last = null;
                    Phase? lastPhase = null;
                    foreach (var ray in fan)
                    {
                        if (ray.At(t) is not { } place || (ray.Phase == Phase.SStopped && t >= ray.TravelTime - 0.5))
                        {
                            last = null;
                            continue;
                        }

                        var point = EarthPoint(place.Radius, place.Angle, side);
                        var family = ray.Phase is Phase.PKiKP ? Phase.PKP : ray.Phase;
                        if (last is { } previous && lastPhase == family && ((Vector)(point - previous)).Length < join) ctx.LineTo(point);
                        else ctx.BeginFigure(point, false);
                        last = point;
                        lastPhase = family;
                        if (side == 1 && ray.Phase is Phase.P or Phase.S && labelAt is null && ray.Takeoff > 30) labelAt = point;
                    }
                }

                ctx.EndFigure(false);
            }
            var colour = kind == WaveKind.P ? Ink.Gold : Ink.Cyan;
            var glow = kind == WaveKind.P ? glowP : glowS;
            if (glow) dc.DrawGeometry(null, FadedPen(colour, 0.45, 8, flat: true), geometry);
            dc.DrawGeometry(null, FadedPen(colour, 1, 2.4, flat: true), geometry);

            if (labelAt is { } at && t < 1100)
            {
                var id = kind == WaveKind.P ? "pwave" : "swave";
                var text = Loc(kind == WaveKind.P ? "Quake_Label_PWave" : "Quake_Label_SWave");
                if (ShowLabels) AddBoxTarget(id, DrawPill(dc, text, at + new Vector(16, 0), colour, 10.5));
                _targets.Add(id, at, 10);
            }
        }
    }

    /// <summary>The surface waves: slow, running round the rim both ways.</summary>
    private void DrawSurfaceWaves(PaintContext dc)
    {
        var t = WaveTime;
        if (t <= 0) return;
        var angle = t * EarthModel.SurfaceWaveSpeed / EarthModel.Radius;
        if (angle > Math.PI) return;
        foreach (var side in new[] { -1, 1 })
        {
            var points = Enumerable.Range(0, 9).Select(i => EarthPoint(EarthModel.Radius + (_earthRadius * 0.0), Math.Max(0, angle - (i * 0.012)), side) + RimNormal(Math.Max(0, angle - (i * 0.012)), side) * 3);
            dc.DrawGeometry(null, FadedPen(Ink.Violet, 0.95, 4), Line(points));
            _targets.Add("surfacewave", EarthPoint(EarthModel.Radius, angle, side), 9);
        }
    }

    private Vector RimNormal(double angle, int side)
    {
        Vector v = EarthPoint(EarthModel.Radius, angle, side) - _earthCentre;
        v = v.Normalize();
        return v;
    }

    /// <summary>
    /// Once the waves have reached the far side, the bands of rim no direct P (between about 100° and 143°) and no S
    /// (beyond about 100°) reach.
    /// </summary>
    private void DrawShadowZones(PaintContext dc)
    {
        var (pEnd, pkpStart, sEnd) = ShadowZones;
        var pDone = EarthModel.FirstArrivals(pEnd - 1).P ?? 800;
        var show = WaveTime > pDone + 60 || Highlight is "shadowp" or "shadows";
        if (!show) return;

        foreach (var side in new[] { -1, 1 })
        {
            var s = Arc(sEnd, 180, side, 12);
            dc.DrawGeometry(null, FadedPen(Ink.Cyan, 0.55, 5, flat: true), s);
            var p = Arc(pEnd, pkpStart, side, 6);
            dc.DrawGeometry(null, FadedPen(Ink.Red, 0.8, 5, flat: true), p);
        }

        var pLabel = EarthPoint(EarthModel.Radius, (pEnd + pkpStart) / 2 * Math.PI / 180, 1) + (RimNormal((pEnd + pkpStart) / 2 * Math.PI / 180, 1) * 34);
        var sLabel = EarthPoint(EarthModel.Radius, 165 * Math.PI / 180, -1) + (RimNormal(165 * Math.PI / 180, -1) * 30);
        if (ShowLabels)
        {
            AddBoxTarget("shadowp", DrawPill(dc, Loc("Quake_Label_PShadow"), pLabel, Ink.Red, 10.5));
            AddBoxTarget("shadows", DrawPill(dc, Loc("Quake_Label_SShadow"), sLabel, Ink.Cyan, 10.5));
        }
        else
        {
            _targets.Add("shadowp", pLabel, 12);
            _targets.Add("shadows", sLabel, 12);
        }
    }

    /// <summary>An arc just outside the rim from one distance to another (°), on one side.</summary>
    private StreamGeometry Arc(double from, double to, int side, double outside)
    {
        var points = new List<Point>();
        for (var d = from; d <= to + 0.01; d += 1)
        {
            var angle = d * Math.PI / 180;
            points.Add(EarthPoint(EarthModel.Radius, angle, side) + (RimNormal(angle, side) * outside));
        }

        return Line(points);
    }

    /// <summary>Stations every 20° round the rim, lit gold when P has come, cyan when S has too; the picked one larger.</summary>
    private void DrawStations(PaintContext dc)
    {
        var t = WaveTime;
        foreach (var side in new[] { -1, 1 })
        {
            for (var d = 20; d <= 180; d += 20)
            {
                if (side == -1 && d == 180) continue;
                DrawStation(dc, d, side, 4.5, t, false);
            }
        }

        var picked = DrawStation(dc, StationDistance, _stationSide, 7.5, t, true);
        _targets.Add("station", picked, 10);
        if (ShowLabels)
        {
            var name = string.IsNullOrEmpty(StationName) ? LocFormat("Quake_Value_Degrees", Number(StationDistance, "0")) : StationName;
            DrawPill(dc, name, picked - (RimNormal(StationDistance * Math.PI / 180, _stationSide) * 26), Ink.Text, 10.5);
        }
    }

    private Point DrawStation(PaintContext dc, double distance, int side, double size, double t, bool picked)
    {
        var angle = distance * Math.PI / 180;
        var normal = RimNormal(angle, side);
        var at = EarthPoint(EarthModel.Radius, angle, side) + (normal * (size * 0.6));
        var (p, s) = EarthModel.FirstArrivals(distance);
        var colour = s is { } sTime && t >= sTime ? Ink.Cyan : p is { } pTime && t >= pTime ? Ink.Gold : Color.FromRgb(0x80, 0x88, 0x96);
        var tangent = new Vector(-normal.Y, normal.X);
        var tip = at + (normal * size * 1.4);
        dc.DrawGeometry(FadedBrush(colour, 1), FadedPen(Colors.Black, 0.6, picked ? 1.2 : 0.8), Polygon(at - (tangent * size), at + (tangent * size), tip));
        return at + (normal * size * 0.5);
    }

    private void DrawQuakeMark(PaintContext dc, double now)
    {
        var at = EarthPoint(EarthModel.Radius - EarthModel.SourceDepth, 0, 1);
        var pulse = (now * 1.2) % 1;
        dc.DrawEllipse(null, FadedPen(Ink.Red, 1 - pulse, 1.6), at, 6 + (pulse * 16), 6 + (pulse * 16));
        dc.DrawGeometry(FadedBrush(Ink.Red, 1), FadedPen(Colors.White, 0.9, 1), Star(at, 8));
        _targets.Add("quake", at, 12);
        if (ShowLabels)
        {
            var label = WaveTime <= 0 ? Loc("Quake_Label_Quake") : LocFormat("Quake_Label_Clock", Number(Math.Floor(WaveTime / 60), "0"), Number(Math.Floor(WaveTime % 60), "00"));
            DrawPill(dc, label, at + (RimNormal(0, 1) * 26), Ink.Red, 11);
        }
    }

    private void DrawEarthLabels(PaintContext dc)
    {
        double Px(double depth) => (EarthModel.Radius - depth) / EarthModel.Radius * _earthRadius;
        var c = _earthCentre;

        // Labels sit on the side away from the quake, where the picture is quiet.
        var away = (QuakeAngle + 180) * Math.PI / 180;
        var dir = new Vector(Math.Sin(away), -Math.Cos(away));
        var across = new Vector(-dir.Y, dir.X);

        Rect Label(string key, double depth, Color colour)
        {
            var at = c + (dir * depth) + (across * 0);
            return DrawPill(dc, Loc(key), at, colour, 10.5);
        }

        _targets.Add("innercore", c, Px(EarthModel.InnerCore) * 0.7);
        if (ShowLabels)
        {
            AddBoxTarget("innercore", Label("Quake_Label_InnerCore", 0, Ink.Text));
            AddBoxTarget("outercore", Label("Quake_Label_OuterCore", (Px(EarthModel.InnerCore) + Px(EarthModel.Gutenberg)) / 2, Ink.Text));
            AddBoxTarget("mantle", Label("Quake_Label_Mantle", (Px(EarthModel.Gutenberg) + Px(EarthModel.UpperMantle)) / 2, Ink.Text));
            var gutenberg = c + (across * Px(EarthModel.Gutenberg) * 0.98);
            AddBoxTarget("gutenberg", DrawPill(dc, Loc("Quake_Label_Gutenberg"), gutenberg, Ink.Gold, 10));
            var crust = c + (across * -(_earthRadius + 2));
            AddBoxTarget("crust", DrawPill(dc, Loc("Quake_Label_CrustThin"), crust + new Vector(-across.X * 34, -across.Y * 34), Ink.Text, 10));
        }
        else
        {
            _targets.Add("outercore", c + (dir * ((Px(EarthModel.InnerCore) + Px(EarthModel.Gutenberg)) / 2)), 14);
            _targets.Add("mantle", c + (dir * ((Px(EarthModel.Gutenberg) + Px(EarthModel.UpperMantle)) / 2)), 14);
        }
    }

    #endregion

    #region the speed chart and the magnifier

    /// <summary>Depth to the chart's height: the top 100 km get a tenth of it so the crust and the Moho show.</summary>
    private double ChartY(double depth)
    {
        var plot = ChartPlot();
        var f = depth <= 100 ? depth / 100 * 0.1 : 0.1 + ((depth - 100) / (EarthModel.Radius - 100) * 0.9);
        return plot.Y + (f * plot.Height);
    }

    private Rect ChartPlot() => new(_speedChart.X + 40, _speedChart.Y + 30, _speedChart.Width - 52, _speedChart.Height - 48);

    private double ChartX(double speed)
    {
        var plot = ChartPlot();
        return plot.X + (speed / 14 * plot.Width);
    }

    private void DrawSpeedChart(PaintContext dc)
    {
        DrawFrame(dc, _speedChart);
        var plot = ChartPlot();
        DrawText(dc, Loc("Quake_Chart_Speed_Title"), new Point(_speedChart.X + 12, _speedChart.Y + 14), Ink.Text, 11.5, bold: true);

        // Layer bands behind the curves.
        (double From, double To, IBrush IBrush)[] bands =
        [
            (0, EarthModel.Moho, Ink.CrustRing),
            (EarthModel.Moho, EarthModel.Gutenberg, Ink.UpperMantle),
            (EarthModel.Gutenberg, EarthModel.InnerCore, Ink.OuterCore),
            (EarthModel.InnerCore, EarthModel.Radius, Ink.InnerCore),
        ];
        dc.PushOpacity(0.28);
        foreach (var (from, to, brush) in bands) dc.DrawRectangle(brush, null, new Rect(plot.X, ChartY(from), plot.Width, ChartY(to) - ChartY(from)));
        dc.Pop();

        // Axes: speed along the top, depth down the side.
        dc.DrawLine(Ink.Tick, plot.TopLeft, plot.TopRight);
        dc.DrawLine(Ink.Tick, plot.TopLeft, plot.BottomLeft);
        for (var v = 0; v <= 14; v += 2)
        {
            dc.DrawLine(Ink.Tick, new Point(ChartX(v), plot.Y), new Point(ChartX(v), plot.Y - 3));
            if (v % 4 == 0) DrawText(dc, Number(v, "0"), new Point(ChartX(v), plot.Y - 9), Ink.Dim, 9, TextAnchor.Center);
        }

        DrawText(dc, Loc("Quake_Chart_Speed_Axis"), new Point(plot.Right, plot.Bottom + 10), Ink.Dim, 9.5, TextAnchor.Right);
        foreach (var depth in new[] { 0.0, 1000, 2900, 5150, 6371 })
        {
            DrawText(dc, Number(depth, "0"), new Point(plot.X - 4, ChartY(depth)), Ink.Dim, 9, TextAnchor.Right);
        }

        DrawText(dc, Loc("Quake_Chart_Depth_Axis"), new Point(plot.X + 4, plot.Bottom + 10), Ink.Dim, 9.5);

        // The two curves.
        foreach (var kind in new[] { WaveKind.P, WaveKind.S })
        {
            var points = new List<Point>();
            for (var depth = 0.0; depth <= EarthModel.Radius; depth += depth < 100 ? 1 : 10)
            {
                var jump = new[] { 20.0, EarthModel.Moho, 410, 660, EarthModel.Gutenberg, EarthModel.InnerCore }.FirstOrDefault(j => j > depth && j < depth + (depth < 100 ? 1 : 10));
                points.Add(new Point(ChartX(EarthModel.Velocity(kind, depth)), ChartY(depth)));
                if (jump > 0)
                {
                    points.Add(new Point(ChartX(EarthModel.Velocity(kind, jump)), ChartY(jump)));
                    points.Add(new Point(ChartX(EarthModel.Velocity(kind, jump, below: true)), ChartY(jump)));
                }
            }

            var colour = kind == WaveKind.P ? Ink.Gold : Ink.Cyan;
            var id = kind == WaveKind.P ? "pcurve" : "scurve";
            var line = Line(points);
            if (Highlight == id) dc.DrawGeometry(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 6), line);
            dc.DrawGeometry(null, FadedPen(colour, 1, 2), line);
            var labelDepth = kind == WaveKind.P ? 1800.0 : 1500;
            AddBoxTarget(id, DrawPill(dc, Loc(kind == WaveKind.P ? "Quake_Label_PWave" : "Quake_Label_SWave"), new Point(ChartX(EarthModel.Velocity(kind, labelDepth)) + (kind == WaveKind.P ? -28 : -24), ChartY(labelDepth)), colour, 9.5));
        }

        // The Moho and the Gutenberg discontinuity.
        foreach (var (depth, key, id) in new[] { (EarthModel.Moho, "Quake_Label_Moho", "moho"), (EarthModel.Gutenberg, "Quake_Label_GutenbergShort", "gutenberg") })
        {
            var y = ChartY(depth);
            dc.DrawLine(FadedPen(Colors.White, 0.6, 1, dashed: true), new Point(plot.X, y), new Point(plot.Right, y));
            AddBoxTarget(id, DrawText(dc, Loc(key), new Point(plot.Right - 2, y - 7), Ink.Text, 9.5, TextAnchor.Right));
        }

        // How deep the P wave has reached so far.
        if (WaveTime > 0 && WaveTime < WaveDuration)
        {
            var deepest = 0.0;
            foreach (var ray in EarthModel.Fan(WaveKind.P))
            {
                if (ray.At(WaveTime) is { } place) deepest = Math.Max(deepest, EarthModel.Radius - place.Radius);
            }

            var y = ChartY(deepest);
            dc.DrawLine(FadedPen(Ink.Gold, 0.8, 1.2), new Point(plot.X, y), new Point(plot.Right, y));
            dc.DrawEllipse(FadedBrush(Ink.Gold, 1), null, new Point(plot.X, y), 3, 3);
        }
    }

    /// <summary>The top 700 km under the quake to true scale: crust, the rigid plate, the soft asthenosphere, the upper mantle.</summary>
    private void DrawMagnifier(PaintContext dc)
    {
        if (_magnifier.Height < 60) return;
        DrawFrame(dc, _magnifier);
        DrawText(dc, Loc("Quake_Magnifier_Title"), new Point(_magnifier.X + 12, _magnifier.Y + 13), Ink.Text, 11, bold: true);
        var box = new Rect(_magnifier.X + 12, _magnifier.Y + 26, _magnifier.Width - 24, _magnifier.Height - 36);
        double Y(double depth) => box.Y + (depth / 700 * box.Height);

        (double From, double To, IBrush IBrush, string Key, string Id, Color Colour)[] layers =
        [
            (0, EarthModel.Moho, Ink.CrustRing, "Quake_Label_CrustShort", "crust", Ink.Text),
            (EarthModel.Moho, EarthModel.AsthenosphereTop, Ink.Lithosphere, "Quake_Label_LithosphereShort", "lithosphere", Ink.Text),
            (EarthModel.AsthenosphereTop, EarthModel.AsthenosphereBottom, Ink.Asthenosphere, "Quake_Label_Asthenosphere", "asthenosphere", Ink.Orange),
            (EarthModel.AsthenosphereBottom, 700, Ink.UpperMantle, "Quake_Label_UpperMantle", "mantle", Ink.Text),
        ];
        foreach (var (from, to, brush, key, id, colour) in layers)
        {
            var band = new Rect(box.X, Y(from), box.Width, Math.Max(1, Y(to) - Y(from)));
            dc.DrawRectangle(brush, null, band);
            if (ShowLabels && band.Height > 9)
            {
                AddBoxTarget(id, DrawText(dc, Loc(key), new Point(box.X + 8, band.Y + (band.Height / 2)), colour, 10));
            }
            else
            {
                AddBoxTarget(id, band);
            }
        }

        // The crust is too thin to write in: its name sits beside it.
        if (ShowLabels) AddBoxTarget("crust", DrawText(dc, Loc("Quake_Label_CrustShort"), new Point(box.Right - 6, Y(EarthModel.Moho) + 7), Ink.Text, 9.5, TextAnchor.Right));
        dc.DrawLine(FadedPen(Colors.White, 0.6, 1), new Point(box.X, Y(EarthModel.Moho)), new Point(box.Right, Y(EarthModel.Moho)));
        foreach (var depth in new[] { EarthModel.AsthenosphereTop, EarthModel.AsthenosphereBottom, EarthModel.UpperMantle })
        {
            DrawText(dc, LocFormat("Quake_Value_Km", Number(depth, "0")), new Point(box.Right - 4, Y(depth) - 6), Ink.Dim, 9, TextAnchor.Right);
        }

        dc.DrawRectangle(null, Ink.Frame, box);
    }

    #endregion
}
