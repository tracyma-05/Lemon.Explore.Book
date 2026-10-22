using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.BlackHole;

/// <summary>
/// The light-and-orbits view, the hole seen from above: light coming in from the left at different distances, bent a
/// little far out, sent round the photon sphere near 5.2 M and caught closer in; and a probe launched from the right,
/// followed with relativity (gold) and with Newton's gravity (grey) from the same start, with a far-away clock and the
/// probe's own clock. Dragging the probe moves its start; dragging its arrow sets the launch speed and direction.
/// </summary>
public sealed partial class BlackHoleDiagram
{
    /// <summary>The probe's own time that passes per second of animation, M.</summary>
    private const double ProbeTimeScale = 40;

    /// <summary>Launch arrow length per unit of speed (c), M.</summary>
    private const double ArrowPerC = 18;

    /// <summary>How fast the light pulses run along the rays, M per second.</summary>
    private const double PulseSpeed = 9;

    /// <summary>The light rays drawn, by impact parameter (+ above the hole, − below).</summary>
    private static readonly Lazy<IReadOnlyList<RayLine>> RayLines = new(MakeRays);

    private Probe _probe = new(10, 0, 0, BlackHoleModel.CircularAngularMomentum(10), newton: false);
    private Probe _newton = new(10, 0, 0, BlackHoleModel.CircularAngularMomentum(10), newton: true);
    private readonly List<Point> _trail = [];
    private readonly List<Point> _newtonTrail = [];
    private double _rayTime;

    /// <summary>A light ray as points (M) from the far left, and the distance along it to each point.</summary>
    private sealed record RayLine(double Impact, Point[] Points, double[] Along, bool Captured);

    private static IReadOnlyList<RayLine> MakeRays()
    {
        var impacts = new List<double>();
        foreach (var b in new[] { 2.5, 4.8, 6.2, 8.0, 11.0 })
        {
            impacts.Add(b);
            impacts.Add(-b);
        }

        // Two rays a hair outside the critical one: they go once round the photon sphere before leaving.
        impacts.Add(BlackHoleModel.CriticalImpact * 1.0004);
        impacts.Add(-BlackHoleModel.CriticalImpact * 1.0004);
        return impacts.Select(b =>
        {
            var path = BlackHoleModel.TraceLight(Math.Abs(b), 0, 0.004, 4 * Math.PI);
            var sign = Math.Sign(b);
            var points = new List<Point>();
            for (var i = 0; i < path.U.Length; i++)
            {
                var u = path.U[i];
                if (u < 1.0 / 40) continue;
                var phi = Math.Min(i * path.Step, path.EndAngle);
                var angle = Math.PI - (sign * phi);
                points.Add(new Point(Math.Cos(angle) / u, Math.Sin(angle) / u));
            }

            var along = new double[points.Count];
            for (var i = 1; i < points.Count; i++) along[i] = along[i - 1] + ((Vector)(points[i] - points[i - 1])).Length;
            return new RayLine(b, [.. points], along, path.Captured);
        }).ToList();
    }

    /// <summary>The hole's centre on the screen and pixels per M.</summary>
    private (Point Centre, double Scale) OrbitLayout(Rect stage)
    {
        var scale = Math.Min(stage.Width / 2 / 26, stage.Height / 2 / 19.5);
        return (new Point(stage.Left + (stage.Width * 0.45), stage.Top + (stage.Height / 2)), scale);
    }

    /// <summary>Sends both probes off again from the start.</summary>
    private void ResetProbes()
    {
        var r = Math.Max(StartRadius, 2.3);
        _probe = new Probe(r, 0, StartRadialSpeed, StartAngularMomentum, newton: false);
        _newton = new Probe(r, 0, StartRadialSpeed, StartAngularMomentum, newton: true);
        _trail.Clear();
        _newtonTrail.Clear();
        _trail.Add(new Point(r, 0));
        _newtonTrail.Add(new Point(r, 0));
        _hold = 0;
        Report();
        InvalidateVisual();
    }

    private void StepOrbits(double dt)
    {
        if (_probe.IsLost || _probe.HasEscaped)
        {
            _hold += dt;
            if (_newton is { IsLost: false, HasEscaped: false }) Move(_newton, _newtonTrail, dt);
            if (_hold > 3) ResetProbes();
            return;
        }

        Move(_probe, _trail, dt);
        Move(_newton, _newtonTrail, dt);
        Report();
    }

    private static void Move(Probe probe, List<Point> trail, double dt)
    {
        if (probe.IsLost || probe.HasEscaped) return;
        probe.Advance(dt * ProbeTimeScale);
        var at = new Point(probe.X, probe.Y);
        if (((Vector)(at - trail[^1])).Length > 0.12) trail.Add(at);
        if (trail.Count > 1600) trail.RemoveRange(0, 400);
    }

    /// <summary>The probe's radius, clocks and state, back to the page.</summary>
    private void Report()
    {
        SetCurrentValue(ProbeRadiusProperty, _probe.R);
        SetCurrentValue(ProbeTimeProperty, _probe.ProperTime);
        SetCurrentValue(FarTimeProperty, _probe.FarTime);
        var state = _probe.IsLost ? "Lost" : _probe.HasEscaped ? "Escaped" : "Orbiting";
        if (ProbeState != state) SetCurrentValue(ProbeStateProperty, state);
    }

    private void DrawOrbitsMode(PaintContext dc, double now)
    {
        var stage = Stage();
        var (centre, scale) = OrbitLayout(stage);
        Point P(double x, double y) => new(centre.X + (x * scale), centre.Y - (y * scale));
        Point Pt(Point m) => P(m.X, m.Y);

        // Distance rings.
        foreach (var r in new[] { 5.0, 10, 15, 20 })
        {
            dc.DrawEllipse(null, Ink.Faint, centre, r * scale, r * scale);
            if (ShowLabels) DrawText(dc, LocFormat("Hole_Ring_M", Number(r, "0")), P(r * 0.707, -r * 0.707) + new Vector(4, 6), Ink.Dim, 10);
        }

        if (ShowRays) DrawRays(dc, now, Pt);

        // The hole, the photon sphere and the innermost stable orbit.
        var horizon = BlackHoleModel.Horizon * scale;
        dc.DrawEllipse(Brushes.Black, FadedPen(Ink.Orange, 0.6, 1.5), centre, horizon, horizon);
        _targets.Add("horizon", centre, horizon);
        var sphere = BlackHoleModel.PhotonSphere * scale;
        dc.DrawEllipse(null, FadedPen(Ink.Gold, Glows("photonsphere") ? 1 : 0.7, Glows("photonsphere") ? 2.5 : 1.2, dashed: true), centre, sphere, sphere);
        var isco = BlackHoleModel.Isco * scale;
        dc.DrawEllipse(null, FadedPen(Ink.Pink, Glows("isco") ? 1 : 0.6, Glows("isco") ? 2.5 : 1.2, dashed: true), centre, isco, isco);
        if (Glows("photonsphere")) dc.DrawEllipse(null, ExploreGlow(now), centre, sphere, sphere);
        if (Glows("isco")) dc.DrawEllipse(null, ExploreGlow(now), centre, isco, isco);

        if (ShowLabels)
        {
            DrawCallout(dc, Loc("Hole_Label_HorizonTop"), centre + new Vector(0, horizon * 0.4), centre + new Vector(-scale * 3.5, scale * 9.5), Ink.Orange, "horizon");
            DrawCallout(dc, Loc("Hole_Label_PhotonSphere"), P(-2.12, 2.12), P(-9, 9.5), Ink.Gold, "photonsphere");
            DrawCallout(dc, Loc("Hole_Label_Isco"), P(-4.24, -4.24), P(-10, -11), Ink.Pink, "isco");
        }
        else
        {
            _targets.Add("photonsphere", P(0, BlackHoleModel.PhotonSphere), 8);
            _targets.Add("isco", P(0, -BlackHoleModel.Isco), 8);
        }

        // Newton's path first, so relativity's is on top.
        if (ShowNewton && _newtonTrail.Count > 1)
        {
            dc.DrawGeometry(null, SolidPen(Dim(Ink.Dim, Glows("newton") ? 1 : 0.7), Glows("newton") ? 2.4 : 1.4, dashed: true), Line(_newtonTrail.Select(Pt)));
            var at = P(_newton.X, _newton.Y);
            dc.DrawEllipse(FadedBrush(Ink.Dim, 0.95), null, at, 5, 5);
            _targets.Add("newton", at, 8);
            if (ShowLabels) DrawTargetPill(dc, Loc("Hole_Label_Newton"), at + new Vector(0, 18), Ink.Dim, "newton", 10.5);
        }

        if (_trail.Count > 1)
        {
            if (Glows("trail")) dc.DrawGeometry(null, ExploreGlow(now), Line(_trail.Select(Pt)));
            dc.DrawGeometry(null, SolidPen(Dim(Ink.Gold, 0.85), 1.8), Line(_trail.Select(Pt)));
            _targets.Add("trail", Pt(_trail[_trail.Count / 2]), 8);
        }

        if (!_probe.IsLost)
        {
            var at = P(_probe.X, _probe.Y);
            dc.DrawEllipse(FadedBrush(Ink.Gold, 0.25), null, at, 11, 11);
            dc.DrawEllipse(FadedBrush(Ink.Gold, 1), MakePenOnce, at, 6, 6);
            _targets.Add("probe", at, 10);
            if (ShowLabels) DrawTargetPill(dc, Loc("Hole_Label_Probe"), at + new Vector(0, -20), Ink.Gold, "probe", 10.5);
        }
        else if (ShowLabels)
        {
            DrawTargetPill(dc, Loc("Hole_Label_Lost"), centre + new Vector(0, -horizon - 16), Ink.Red, "probe");
        }

        DrawLaunch(dc, P);
        DrawClocks(dc, stage);
    }

    /// <summary>The rays' outlines for the current layout, made once per layout (their points never change).</summary>
    private readonly Dictionary<double, StreamGeometry> _rayGeometries = [];
    private (Point Centre, double Scale) _rayLayout;

    private StreamGeometry RayGeometry(RayLine ray, Func<Point, Point> pt)
    {
        var layout = OrbitLayout(Stage());
        if (layout != _rayLayout)
        {
            _rayGeometries.Clear();
            _rayLayout = layout;
        }

        if (!_rayGeometries.TryGetValue(ray.Impact, out var geometry)) _rayGeometries[ray.Impact] = geometry = Line(ray.Points.Select(pt));
        return geometry;
    }

    /// <summary>A colour darkened towards the backdrop: opaque lines are much cheaper to draw than see-through ones.</summary>
    private static Color Dim(Color color, double share) => SpaceDrawing.Lerp(Color.FromRgb(0x05, 0x06, 0x0E), color, share);

    private readonly Dictionary<(Color, double, bool), IPen> _solidPens = [];
    private readonly Dictionary<Color, IBrush> _solidBrushes = [];

    private IPen SolidPen(Color color, double thickness, bool dashed = false)
    {
        if (_solidPens.TryGetValue((color, thickness, dashed), out var pen)) return pen;
        pen = new ImmutablePen(((IBrush)SpaceDrawing.Solid(255, color.R, color.G, color.B)).ToImmutable(), thickness, dashed ? new ImmutableDashStyle([4, 3], 0) : null, PenLineCap.Flat, PenLineJoin.Round);
        return _solidPens[(color, thickness, dashed)] = pen;
    }

    private IBrush SolidBrush(Color color)
    {
        if (!_solidBrushes.TryGetValue(color, out var brush)) _solidBrushes[color] = brush = SpaceDrawing.Solid(255, color.R, color.G, color.B);
        return brush;
    }

    private static readonly IPen MakePenOnce = SpaceDrawing.MakePen(SpaceDrawing.Solid(255, 0x30, 0x22, 0x08), 1.2);

    /// <summary>The start point and the launch arrow (1 c = <see cref="ArrowPerC"/> M), both draggable.</summary>
    private void DrawLaunch(PaintContext dc, Func<double, double, Point> p)
    {
        var r = Math.Max(StartRadius, 2.3);
        var start = p(r, 0);
        var tip = LaunchTip(p);
        dc.DrawEllipse(null, FadedPen(Ink.Green, 0.9, 1.5), start, 7, 7);
        _targets.Add("start", start, 9);
        DrawArrow(dc, start, tip, Ink.Green, 2, Glows("arrow") ? 1 : 0.85);
        dc.DrawEllipse(FadedBrush(Ink.Green, 0.9), null, tip, 4, 4);
        _targets.Add("arrow", tip, 9);
        if (ShowLabels) DrawTargetPill(dc, Loc("Hole_Label_Launch"), tip + new Vector(30, 12), Ink.Green, "arrow", 10.5);
    }

    /// <summary>The launch arrow's tip: sideways speed L/r, outward speed dr/dτ.</summary>
    private Point LaunchTip(Func<double, double, Point> p)
    {
        var r = Math.Max(StartRadius, 2.3);
        var sideways = StartAngularMomentum / r;
        return p(r + (StartRadialSpeed * ArrowPerC), sideways * ArrowPerC);
    }

    private void DrawRays(PaintContext dc, double now, Func<Point, Point> pt)
    {
        var rays = RayLines.Value;
        var critical = Glows("critical");
        foreach (var ray in rays)
        {
            var isCritical = Math.Abs(Math.Abs(ray.Impact) - BlackHoleModel.CriticalImpact) < 0.01;
            var color = isCritical ? Ink.Violet : ray.Captured ? Ink.Red : Ink.Cyan;
            var glow = (isCritical && critical) || (ray.Captured && Glows("caught") && Math.Abs(ray.Impact) == 4.8) || (!ray.Captured && !isCritical && Glows("bent") && Math.Abs(ray.Impact) == 6.2);
            var geometry = RayGeometry(ray, pt);
            if (glow) dc.DrawGeometry(null, ExploreGlow(now), geometry);
            dc.DrawGeometry(null, SolidPen(Dim(color, isCritical ? 0.8 : 0.45), isCritical ? 1.4 : 1.1), geometry);

            // Light pulses running along the ray.
            var length = ray.Along[^1];
            var period = length + 12;
            for (var k = 0; k < 3; k++)
            {
                var s = ((_rayTime * PulseSpeed) + (k * period / 3) + (Math.Abs(ray.Impact) * 3.1)) % period;
                if (s > length) continue;
                var i = Array.BinarySearch(ray.Along, s);
                if (i < 0) i = Math.Max(0, ~i - 1);
                var at = pt(ray.Points[Math.Min(i, ray.Points.Length - 1)]);
                dc.DrawEllipse(SolidBrush(color), null, at, 2.6, 2.6);
            }
        }

        // Labels on three of them: one caught, one bent, one sent round, each where the ray is a set distance from the
        // hole (on its way in, or on its way out).
        RayLine? Ray(double b) => rays.FirstOrDefault(r => Math.Abs(r.Impact - b) < 1e-6);
        void Label(RayLine? ray, double radius, bool outward, string key, Color color, string id, Vector offset)
        {
            if (ray is null || ray.Points.Length == 0) return;
            var closest = 0;
            for (var k = 1; k < ray.Points.Length; k++)
            {
                if (((Vector)ray.Points[k]).Length < ((Vector)ray.Points[closest]).Length) closest = k;
            }

            var i = outward ? closest : 0;
            if (outward)
            {
                while (i < ray.Points.Length - 1 && ((Vector)ray.Points[i]).Length < radius) i++;
            }
            else
            {
                while (i < closest && ((Vector)ray.Points[i]).Length > radius) i++;
            }

            var at = pt(ray.Points[i]);
            if (ShowLabels) DrawCallout(dc, Loc(key), at, at + offset, color, id);
            else _targets.Add(id, at, 9);
        }

        Label(Ray(4.8), 12, false, "Hole_Label_Caught", Ink.Red, "caught", new Vector(-30, -44));
        Label(Ray(-6.2), 14, true, "Hole_Label_Bent", Ink.Cyan, "bent", new Vector(30, 30));
        Label(Ray(BlackHoleModel.CriticalImpact * 1.0004), 11, true, "Hole_Label_Critical", Ink.Violet, "critical", new Vector(-40, -34));
    }

    /// <summary>Two clocks in the top-right corner of the stage: one far away and the probe's own.</summary>
    private void DrawClocks(PaintContext dc, Rect stage)
    {
        var box = new Rect(stage.Right - 252, stage.Top + 6, 236, 112);
        DrawFrame(dc, box);
        AddBoxTarget("clocks", box);
        void Face(Point at, double time, string key, Color color, bool stopped)
        {
            const double radius = 24;
            dc.DrawEllipse(FadedBrush(Ink.Text, 0.06), FadedPen(color, 0.8, 1.4), at, radius, radius);
            for (var k = 0; k < 12; k++)
            {
                var a = k * Math.PI / 6;
                var d = new Vector(Math.Sin(a), -Math.Cos(a));
                dc.DrawLine(FadedPen(Ink.Text, 0.4, 1), at + (d * radius * 0.8), at + (d * radius * 0.95));
            }

            var turn = double.IsInfinity(time) ? 0 : time / ClockTurn * 2 * Math.PI;
            var hand = new Vector(Math.Sin(turn), -Math.Cos(turn));
            dc.DrawLine(FadedPen(stopped ? Ink.Red : color, 1, 2), at, at + (hand * radius * 0.8));
            DrawText(dc, Loc(key), at + new Vector(radius + 10, -12), color, 11.5, bold: true);
            var reading = double.IsInfinity(time) ? Loc("Hole_Clock_Forever") : LocFormat("Hole_Clock_M", Number(time, "0"));
            DrawText(dc, reading, at + new Vector(radius + 10, 6), Ink.Text, 11);
        }

        Face(new Point(box.Left + 36, box.Top + 30), _probe.FarTime, "Hole_Clock_Far", Ink.Cyan, double.IsInfinity(_probe.FarTime));
        Face(new Point(box.Left + 36, box.Top + 82), _probe.ProperTime, "Hole_Clock_Probe", Ink.Gold, false);
    }

    /// <summary>M of time per turn of a clock's hand.</summary>
    private const double ClockTurn = 200;

    /// <summary>Sets the launch from a drag of the arrow's tip to a point (M coordinates).</summary>
    private void SetLaunchTip(Point m)
    {
        var r = Math.Max(StartRadius, 2.3);
        var radial = Math.Clamp((m.X - r) / ArrowPerC, -0.8, 0.8);
        var sideways = Math.Clamp(m.Y / ArrowPerC, -0.95, 0.95);
        SetCurrentValue(StartRadialSpeedProperty, Math.Round(radial, 3));
        SetCurrentValue(StartAngularMomentumProperty, Math.Round(sideways * r, 3));
    }

    /// <summary>Moves the start along the line to the right of the hole, keeping the launch speed.</summary>
    private void SetStartRadius(double x)
    {
        var old = Math.Max(StartRadius, 2.3);
        var r = Math.Clamp(x, 3, 24);
        var sideways = StartAngularMomentum / old;
        SetCurrentValue(StartRadiusProperty, Math.Round(r, 2));
        SetCurrentValue(StartAngularMomentumProperty, Math.Round(sideways * r, 3));
    }
}
