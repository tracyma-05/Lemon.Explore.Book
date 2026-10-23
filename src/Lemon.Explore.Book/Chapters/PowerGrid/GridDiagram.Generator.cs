using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.PowerGrid;

/// <summary>
/// Generator mode. A coil turning between a magnet's north and south poles, seen at a slant: its two ends go to two slip
/// rings (or one split ring, a commutator) with carbon brushes on them, and wires from the brushes to a lamp and an
/// oscilloscope. The coil's face is tinted by the field through it, arrows on its sides show the current and the push
/// back on them. The three-phase machine is drawn end-on: a magnet (rotor) turning inside three pairs of coils.
/// </summary>
public sealed partial class GridDiagram
{
    /// <summary>How much of the past the oscilloscope shows, seconds of the real machine's time.</summary>
    private const double ScopeWindow = 2.5;

    /// <summary>Slant of the depth axis: a step towards the viewer moves this far right and down, in units.</summary>
    private const double DepthX = 0.42;

    private const double DepthY = 0.3;

    private readonly List<ScopeSample> _scope = [];
    private double _scopeTime;

    /// <summary>The current round the lamp's circuit this frame (A), and how far the dots on the wires have moved.</summary>
    private double _current;

    private double _flow;

    /// <summary>The voltages at the brushes (or the three coils) this frame, V.</summary>
    private ScopeSample _now;

    /// <summary>The picture's centre and unit (pixels per unit) for the slanted view of the coil.</summary>
    private Point _c;

    private double _s;

    private readonly record struct ScopeSample(double T, double A, double B, double C);

    private void ClearScope()
    {
        _scope.Clear();
        _scopeTime = 0;
    }

    /// <summary>Adds this frame's voltages to the oscilloscope, made at the rate the coil really turned.</summary>
    private void RecordScope(double physical)
    {
        var sample = Sample(_turnRate);
        _scopeTime += physical;
        _flow += _current * physical * 90;
        _scope.Add(sample with { T = _scopeTime });
        while (_scope.Count > 2 && _scope[0].T < _scopeTime - ScopeWindow) _scope.RemoveAt(0);
        if (_scope.Count > 4000) _scope.RemoveRange(0, _scope.Count - 4000);
    }

    /// <summary>The voltages (and the lamp's current) at the coil's angle now, turning at <paramref name="rate"/> turns a second.</summary>
    private ScopeSample Sample(double rate)
    {
        var phi = _angle * Math.PI / 180;
        var peak = GeneratorModel.PeakEmf(Turns, Field, rate);
        ScopeSample sample;
        if (Kind == GeneratorKind.ThreePhase)
        {
            var (a, b, c) = GeneratorModel.ThreePhase(peak, phi, Math.Max(1, PolePairs));
            sample = new ScopeSample(_scopeTime, a, b, c);
            _current = LampOn ? Math.Abs(a) / (GeneratorModel.LampResistance + GeneratorModel.CoilResistance(Turns)) : 0;
        }
        else
        {
            var output = GeneratorModel.Output(Kind, peak * Math.Sin(phi));
            sample = new ScopeSample(_scopeTime, output, 0, 0);
            _current = GeneratorModel.Current(Turns, output, LampOn);
        }

        _now = sample;
        return sample;
    }

    /// <summary>The peak voltage the scope's scale is set for: the setting's, or a faster hand's.</summary>
    private double ScopePeak => Math.Max(GeneratorModel.PeakEmf(Turns, Field, Math.Max(CoilSpeed, Math.Abs(_turnRate))), 0.5);

    private void DrawGeneratorMode(PaintContext dc, double width, double height)
    {
        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
        var stage = Stage();
        var split = stage.Left + (stage.Width * (stage.Width < 760 ? 0.56 : 0.6));
        var machine = new Rect(stage.Left + 20, stage.Top, Math.Max(160, split - stage.Left - 20), stage.Height);
        var side = new Rect(split + 10, stage.Top + 4, Math.Max(150, stage.Right - split - 34), stage.Height - 8);

        // The lamp(s) above, the oscilloscope below.
        var scopeHeight = Math.Clamp(side.Height * 0.46, 110, 230);
        var scope = new Rect(side.Left, side.Bottom - scopeHeight, side.Width, scopeHeight);
        var lampArea = new Rect(side.Left, side.Top, side.Width, Math.Max(80, scope.Top - side.Top - 16));

        if (Kind == GeneratorKind.ThreePhase)
        {
            DrawThreePhaseMachine(dc, machine, lampArea, scope);
        }
        else
        {
            DrawCoilMachine(dc, machine, lampArea, scope);
        }

        DrawScope(dc, scope);
    }

    #region the coil between the poles

    /// <summary>A point of the slanted view: x across the gap (north to south), y up, z along the axle towards the viewer.</summary>
    private Point P(double x, double y, double z) => new(_c.X + ((x + (DepthX * z)) * _s), _c.Y + ((-y + (DepthY * z)) * _s));

    private void DrawCoilMachine(PaintContext dc, Rect machine, Rect lampArea, Rect scope)
    {
        _s = Math.Clamp(Math.Min(machine.Width / 6.3, machine.Height / 4.3), 24, 120);
        _c = new Point(machine.Left + (machine.Width * 0.5), machine.Top + (machine.Height * 0.44));
        var phi = _angle * Math.PI / 180;
        var labels = ShowLabels;

        // The south pole first: everything in the gap is in front of its face. Then the field lines across the gap (the
        // back layer, the coil, the front layer), and the north pole last, whose block hides what is behind it.
        DrawPoleBlock(dc, 1.35, 2.3, Ink.SouthPole, Ink.SouthTop, Ink.SouthSide, "S");
        DrawFieldLayer(dc, -0.55, 0.32);
        DrawCoil(dc, phi);
        DrawFieldLayer(dc, 0.55, 0.55);
        DrawPoleBlock(dc, -2.3, -1.35, Ink.NorthPole, Ink.NorthTop, Ink.NorthSide, "N");

        // The crank on the back of the axle: behind the coil, but in line with the gap, so the north pole never hides it.
        const double back = -2.15;
        var crankHub = P(0, 0, back);
        var arm = new Vector(-Math.Sin(phi), -Math.Cos(phi)) * 0.5 * _s;
        dc.DrawLine(MakePen(Ink.Metal, Math.Max(4, _s * 0.1)), crankHub, P(0, 0, -1.05));
        var knob = crankHub + arm;
        dc.DrawLine(MakePen(Ink.Metal, Math.Max(4, _s * 0.12)), crankHub, knob);
        dc.DrawEllipse(Ink.MetalDark, Ink.Edge, crankHub, _s * 0.1, _s * 0.1);
        var handle = SpaceDrawing.RadialGradient((Color.FromRgb(0xD8, 0x9A, 0x5A), 0), (Color.FromRgb(0x7A, 0x4A, 0x22), 1));
        dc.DrawEllipse(handle, Ink.Edge, knob, _s * 0.13, _s * 0.13);
        DrawTurningArrow(dc, crankHub, _s * 0.72);
        _targets.Add("crank", knob, _s * 0.16);
        DrawCoilArrows(dc, phi);
        _targets.AddBox("north", BoxOf(P(-2.3, 1.15, 0.9), P(-1.35, -1.15, 0.9)));
        _targets.AddBox("south", BoxOf(P(1.35, 1.15, 0.9), P(2.3, -1.15, 0.9)));

        // The front of the axle and the rings (or split ring) with their brushes.
        dc.DrawLine(MakePen(Ink.Metal, Math.Max(4, _s * 0.1)), P(0, 0, 1.0), P(0, 0, 2.05));
        var (brushA, brushB) = Kind == GeneratorKind.Dc ? DrawCommutator(dc, phi) : DrawSlipRings(dc, phi);

        // Wires from the brushes round to the lamp, with the current's dots moving along them.
        var lamp = new Point(lampArea.Left + (lampArea.Width * 0.42), lampArea.Top + (lampArea.Height * 0.48));
        var lampRadius = Math.Clamp(Math.Min(lampArea.Width, lampArea.Height) * 0.2, 16, 40);
        var bottom = Math.Min(machine.Bottom - 10, P(0, -1.55, 1.8).Y);
        var baseY = lamp.Y + (lampRadius * 1.55);
        var circuit = new List<Point>
        {
            brushA,
            new(brushA.X, bottom),
            new(lamp.X - (lampRadius * 1.6), bottom),
            new(lamp.X - (lampRadius * 1.6), baseY + 10),
            new(lamp.X - 6, baseY + 10),
            new(lamp.X - 6, baseY),
            new(lamp.X + 6, baseY),
            new(lamp.X + 6, baseY + 10),
            new(lamp.X + (lampRadius * 1.6), baseY + 10),
            new(lamp.X + (lampRadius * 1.6), bottom + 12),
            new(brushB.X, bottom + 12),
            brushB,
        };
        DrawCircuit(dc, circuit, LampOn);

        // The scope's probes clip onto the two wires.
        var probeA = new Point(scope.Left - 14, bottom);
        var probeB = new Point(scope.Left - 6, bottom + 12);
        if (probeA.X > brushA.X + 20)
        {
            dc.DrawLine(FadedPen(Ink.Text, 0.4, 1.2), probeA, new Point(probeA.X, scope.Top + (scope.Height * 0.4)));
            dc.DrawLine(FadedPen(Ink.Text, 0.4, 1.2), new Point(probeA.X, scope.Top + (scope.Height * 0.4)), new Point(scope.Left, scope.Top + (scope.Height * 0.4)));
            dc.DrawLine(FadedPen(Ink.Text, 0.4, 1.2), probeB, new Point(probeB.X, scope.Top + (scope.Height * 0.55)));
            dc.DrawLine(FadedPen(Ink.Text, 0.4, 1.2), new Point(probeB.X, scope.Top + (scope.Height * 0.55)), new Point(scope.Left, scope.Top + (scope.Height * 0.55)));
            dc.DrawEllipse(Ink.LeaderDot, null, probeA, 2.4, 2.4);
            dc.DrawEllipse(Ink.LeaderDot, null, probeB, 2.4, 2.4);
        }

        var power = _now.A * _now.A / Math.Pow(GeneratorModel.LampResistance + GeneratorModel.CoilResistance(Turns), 2) * GeneratorModel.LampResistance;
        DrawLamp(dc, lamp, lampRadius, LampOn ? power : 0, Ink.Gold, "lamp");
        DrawSwitch(dc, new Point(lamp.X + (lampRadius * 1.6), baseY + 10 + ((bottom + 12 - baseY - 10) / 2)), LampOn);
        _targets.Add("lamp", lamp, lampRadius * 1.15);

        // The lamp would burn out: far more than its 3 W on average.
        if (LampOn && GeneratorModel.AverageLampPower(Turns, Field, Math.Abs(_turnRate), true) > 2.5 * GeneratorModel.LampWatts)
        {
            DrawPill(dc, Loc("Grid_Gen_TooBright"), new Point(lamp.X, lamp.Y - (lampRadius * 1.9)), Ink.Red);
        }

        if (!labels) return;
        DrawCallout(dc, Loc("Grid_Obj_North"), P(-2.0, 1.15, -0.2), P(-2.4, 1.75, -0.2), right: false, "north");
        DrawCallout(dc, Loc("Grid_Obj_South"), P(2.0, 1.15, -0.2), P(2.25, 1.75, -0.2), right: true, "south");
        DrawCallout(dc, Loc("Grid_Obj_Field"), P(0.9, 0.9, 0.55), P(1.7, 1.55, 0.55), right: true, "field");
        var side = new Point(-Math.Sin(phi), Math.Cos(phi));
        var coilPoint = P(side.X * 0.95, side.Y * 0.95, 0.5);
        DrawCallout(dc, Loc("Grid_Obj_Coil"), coilPoint, new Point(machine.Left + 12, P(0, -0.2, 0).Y), right: true, "coil");
        var ringPoint = P(0, -0.25, Kind == GeneratorKind.Dc ? 1.55 : 1.4);
        DrawCallout(dc, Loc(Kind == GeneratorKind.Dc ? "Grid_Obj_Commutator" : "Grid_Obj_Rings"), ringPoint,
            new Point(machine.Left + 12, ringPoint.Y + (_s * 0.55)), right: true, Kind == GeneratorKind.Dc ? "commutator" : "rings");
        DrawCallout(dc, Loc("Grid_Obj_Brushes"), brushB, new Point(brushB.X + (_s * 0.8), brushB.Y + (_s * 0.2)), right: true, "brushes");
        DrawCallout(dc, Loc("Grid_Obj_Crank"), knob, new Point(Math.Max(machine.Left + 120, crankHub.X - (_s * 1.3)), crankHub.Y - (_s * 0.85)), right: false, "crank");
    }

    /// <summary>A pole of the magnet as a slanted block from <paramref name="x0"/> to <paramref name="x1"/>.</summary>
    private void DrawPoleBlock(PaintContext dc, double x0, double x1, IBrush front, IBrush top, IBrush side, string letter)
    {
        const double y0 = -1.15, y1 = 1.15, z0 = -0.9, z1 = 0.9;
        dc.DrawGeometry(side, Ink.Edge, Shape(P(x0, y1, z1), P(x0, y1, z0), P(x0, y0, z0), P(x0, y0, z1)));
        dc.DrawGeometry(top, Ink.Edge, Shape(P(x0, y1, z1), P(x0, y1, z0), P(x1, y1, z0), P(x1, y1, z1)));
        dc.DrawGeometry(front, Ink.Edge, Shape(P(x0, y1, z1), P(x1, y1, z1), P(x1, y0, z1), P(x0, y0, z1)));
        var center = P((x0 + x1) / 2, 0, z1);
        DrawText(dc, letter, center, Ink.Text, Math.Clamp(_s * 0.55, 16, 40), center: true, bold: true);
    }

    private static Rect BoxOf(Point a, Point b) => new(a, b);

    /// <summary>Lines of the field across the gap at depth <paramref name="z"/>, with chevrons drifting from north to south.</summary>
    private void DrawFieldLayer(PaintContext dc, double z, double opacity)
    {
        var glow = Glows("field") ? ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 5) : null;
        var pen = FadedPen(Ink.Field, opacity * 0.7, 1.3);
        var chevron = FadedBrush(Ink.Field, opacity);
        foreach (var y in new[] { -0.9, -0.45, 0.0, 0.45, 0.9 })
        {
            var from = P(-1.35, y, z);
            var to = P(1.35, y, z);
            if (glow is not null) dc.DrawLine(glow, from, to);
            dc.DrawLine(pen, from, to);
            Vector direction = to - from;
            direction = direction.Normalize();
            for (var k = 0; k < 3; k++)
            {
                var at = Mix(from, to, 0.12 + (0.76 * Frac((k / 3.0) + (_t * 0.18))));
                DrawArrowHead(dc, chevron, at, direction, 7);
            }
        }
    }

    /// <summary>
    /// The coil at angle <paramref name="phi"/>: its face tinted by the field through it, its windings, the current along
    /// its sides and the push back on them.
    /// </summary>
    private void DrawCoil(PaintContext dc, double phi)
    {
        const double a = 0.95, l = 1.0;
        var side = new Point(-Math.Sin(phi), Math.Cos(phi));
        Point Corner(double sign, double z) => P(sign * side.X * a, sign * side.Y * a, z);
        var c1 = Corner(1, -l);
        var c2 = Corner(1, l);
        var c3 = Corner(-1, l);
        var c4 = Corner(-1, -l);

        // The field through the coil: most when it faces the poles square on.
        var flux = Math.Abs(Math.Cos(phi));
        dc.DrawGeometry(FadedBrush(Ink.Gold, 0.05 + (0.32 * flux)), null, Shape(c1, c2, c3, c4));

        // Windings: a few loops side by side, the far edge drawn darker.
        if (Glows("coil")) dc.DrawGeometry(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 10), Shape(c1, c2, c3, c4));
        for (var k = 0; k < 3; k++)
        {
            var shrink = 1 - (k * 0.035);
            Point C(double sign, double z) => P(sign * side.X * a * shrink, sign * side.Y * a * shrink, z * shrink);
            var outline = Shape(C(1, -l), C(1, l), C(-1, l), C(-1, -l));
            dc.DrawGeometry(null, FadedPen(Ink.Copper, 1, Math.Max(2, _s * 0.05)), outline);
        }

        dc.DrawGeometry(null, FadedPen(Ink.CopperLight, 0.6, 1), Shape(c1, c2, c3, c4));
        _targets.Add("coil", Mix(c1, c3, 0.5), _s * 0.45);
    }

    /// <summary>The current along the coil's sides and the push back on them, drawn over the poles.</summary>
    private void DrawCoilArrows(PaintContext dc, double phi)
    {
        const double a = 0.95;
        var side = new Point(-Math.Sin(phi), Math.Cos(phi));
        Point Corner(double sign, double z) => P(sign * side.X * a, sign * side.Y * a, z);

        // The current along the sides (one way on one side, back on the other) and the push back on them.
        if (!LampOn || Math.Abs(_current) < 0.02) return;
        var size = Math.Clamp(Math.Abs(_current) / 0.6, 0.25, 1);
        var sign1 = Math.Sign(_now.A);
        if (Kind == GeneratorKind.Dc) sign1 = Math.Sign(Math.Sin(phi));
        foreach (var s in new[] { 1.0, -1.0 })
        {
            var flowSign = s * sign1;
            var from = Corner(s, -0.35 * flowSign);
            var to = Corner(s, 0.35 * flowSign);
            DrawArrow(dc, from, to, FadedBrush(Ink.Gold, 0.9), 2.4, 9);

            // The force on the side is up or down, against the way it is moving (Lenz).
            var velocityY = -s * Math.Sin(phi) * Math.Sign(_turnRate == 0 ? 1 : _turnRate);
            var mid = Corner(s, 0);
            var push = new Vector(0, -Math.Sign(velocityY)) * -1;
            var tip = mid + (push * (_s * 0.55 * size));
            if (Math.Abs(Math.Sin(phi)) > 0.08) DrawArrow(dc, mid, tip, FadedBrush(Ink.Red, 0.95), 2.6, 10);
        }

        if (ShowLabels && Math.Abs(Math.Sin(phi)) > 0.3)
        {
            var mid = Corner(1, 0);
            var velocityY = -Math.Sin(phi) * Math.Sign(_turnRate == 0 ? 1 : _turnRate);
            var label = mid + new Vector(-_s * 0.9, velocityY > 0 ? _s * 0.75 : -_s * 0.75);
            DrawTargetPill(dc, Loc("Grid_Obj_PushBack"), label, Ink.Red, "pushback", 11);
        }
    }

    /// <summary>A circular arrow round the crank, showing which way it turns.</summary>
    private void DrawTurningArrow(PaintContext dc, Point center, double radius)
    {
        var geometry = new StreamGeometry();
        const double start = 200, sweep = 70;
        using (var context = geometry.Open())
        {
            for (var i = 0; i <= 16; i++)
            {
                var angle = (start + (sweep * i / 16.0)) * Math.PI / 180;
                var point = center + new Vector(Math.Cos(angle) * radius, Math.Sin(angle) * radius);
                if (i == 0) context.BeginFigure(point, false);
                else context.LineTo(point);
            }

            context.EndFigure(false);
        }
        dc.DrawGeometry(null, FadedPen(Ink.Text, 0.45, 1.6), geometry);
        var end = (start + sweep) * Math.PI / 180;
        var tip = center + new Vector(Math.Cos(end) * radius, Math.Sin(end) * radius);
        DrawArrowHead(dc, FadedBrush(Ink.Text, 0.6), tip, new Vector(-Math.Sin(end), Math.Cos(end)), 8);
    }

    /// <summary>Two slip rings on the axle with a brush under each; returns where the wires leave the brushes.</summary>
    private (Point A, Point B) DrawSlipRings(PaintContext dc, double phi)
    {
        var glow = Glows("rings");
        var points = new List<Point>();
        foreach (var (z, lead) in new[] { (1.3, 1.0), (1.68, -1.0) })
        {
            // The lead from one end of the coil along the axle to its ring.
            var side = new Point(-Math.Sin(phi) * lead, Math.Cos(phi) * lead);
            dc.DrawLine(FadedPen(Ink.Copper, 1, 2), P(side.X * 0.95, side.Y * 0.95, 1.0), P(side.X * 0.12, side.Y * 0.12, 1.08));
            dc.DrawLine(FadedPen(Ink.Copper, 1, 2), P(side.X * 0.12, side.Y * 0.12, 1.08), P(side.X * 0.12, side.Y * 0.12, z));
            var r = 0.26 * _s;
            var back = P(0, 0, z);
            var front = P(0, 0, z + 0.14);
            dc.DrawEllipse(Ink.MetalDark, Ink.Edge, back, r, r);
            dc.DrawGeometry(Ink.Brass, null, Shape(back + new Vector(0, -r), front + new Vector(0, -r), front + new Vector(0, r), back + new Vector(0, r)));
            dc.DrawEllipse(Ink.Brass, Ink.Edge, front, r, r);
            dc.DrawEllipse(Ink.MetalDark, null, front, r * 0.35, r * 0.35);

            // A mark on the ring turning with it.
            var mark = front + new Vector(side.X * r * 0.7, -side.Y * r * 0.7);
            dc.DrawEllipse(FadedBrush(Ink.Copper, 1), null, mark, 2.4, 2.4);
            if (glow) dc.DrawEllipse(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 6), front, r + 3, r + 3);
            _targets.Add("rings", front, r);

            // The brush pressing up on the ring from below.
            var brushTop = front + new Vector(0, r);
            var brush = new Rect(brushTop.X - (_s * 0.08), brushTop.Y, _s * 0.16, _s * 0.3);
            dc.DrawRectangle(Ink.Carbon, Ink.LightEdge, brush);
            points.Add(new Point(brush.X + (brush.Width / 2), brush.Bottom));
            _targets.AddBox("brushes", brush);
        }

        return (points[0], points[1]);
    }

    /// <summary>One ring split in two (a commutator) with a brush on each side; returns where the wires leave the brushes.</summary>
    private (Point A, Point B) DrawCommutator(PaintContext dc, double phi)
    {
        const double z = 1.45;
        var r = 0.3 * _s;
        var back = P(0, 0, z);
        var front = P(0, 0, z + 0.2);
        dc.DrawEllipse(Ink.MetalDark, Ink.Edge, back, r, r);
        dc.DrawGeometry(Ink.Brass, null, Shape(back + new Vector(0, -r), front + new Vector(0, -r), front + new Vector(0, r), back + new Vector(0, r)));

        // Two half rings, each joined to one end of the coil, with gaps that turn with the coil.
        for (var half = 0; half < 2; half++)
        {
            var start = phi + (half * Math.PI) + 0.12;
            var end = phi + ((half + 1) * Math.PI) - 0.12;
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(front, true);
                for (var i = 0; i <= 20; i++)
                {
                    var angle = start + ((end - start) * i / 20);
                    context.LineTo(front + new Vector(Math.Cos(angle) * r, -Math.Sin(angle) * r));
                }

                context.EndFigure(true);
            }
            dc.DrawGeometry(half == 0 ? Ink.Brass : FadedBrush(Color.FromRgb(0xD8, 0xA8, 0x4A), 1), Ink.Edge, geometry);

            var lead = half == 0 ? 1.0 : -1.0;
            var side = new Point(-Math.Sin(phi) * lead, Math.Cos(phi) * lead);
            dc.DrawLine(FadedPen(Ink.Copper, 1, 2), P(side.X * 0.95, side.Y * 0.95, 1.0), P(side.X * 0.18, side.Y * 0.18, z));
        }

        dc.DrawEllipse(Ink.MetalDark, null, front, r * 0.3, r * 0.3);
        if (Glows("commutator")) dc.DrawEllipse(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 6), front, r + 3, r + 3);
        _targets.Add("commutator", front, r);

        // Brushes touching left and right, where the gaps pass as the voltage goes through zero.
        var left = new Rect(front.X - r - (_s * 0.3), front.Y - (_s * 0.08), _s * 0.3, _s * 0.16);
        var right = new Rect(front.X + r, front.Y - (_s * 0.08), _s * 0.3, _s * 0.16);
        dc.DrawRectangle(Ink.Carbon, Ink.LightEdge, left);
        dc.DrawRectangle(Ink.Carbon, Ink.LightEdge, right);
        _targets.AddBox("brushes", left);
        _targets.AddBox("brushes", right);
        var a = new Point(left.Left, left.Top + (left.Height / 2));
        var b = new Point(right.Right, right.Top + (right.Height / 2));
        dc.DrawLine(Ink.Wire, a, a + new Vector(-_s * 0.12, 0));
        dc.DrawLine(Ink.Wire, b, b + new Vector(_s * 0.12, 0));
        return (a + new Vector(-_s * 0.12, 0), b + new Vector(_s * 0.12, 0));
    }

    /// <summary>The wires of a circuit, and dots moving along them as fast as the current (back and forth with AC).</summary>
    private void DrawCircuit(PaintContext dc, IReadOnlyList<Point> points, bool closed)
    {
        var line = Line(points);
        dc.DrawGeometry(null, Ink.Wire, line);
        dc.DrawGeometry(null, Ink.WireCore, line);
        if (!closed) return;

        var total = 0.0;
        for (var i = 1; i < points.Count; i++) total += ((Vector)(points[i] - points[i - 1])).Length;
        const double spacing = 22;
        var count = (int)(total / spacing);
        var offset = Frac(_flow / spacing);
        var brush = FadedBrush(Ink.Gold, Math.Clamp(Math.Abs(_current) * 2.5, 0.25, 1));
        for (var i = 0; i < count; i++)
        {
            var at = Along(points, (i + offset) * spacing / total);
            dc.DrawEllipse(brush, null, at, 2.2, 2.2);
        }
    }

    /// <summary>A lamp glowing with <paramref name="watts"/> (its rating is 3 W), ringed in <paramref name="tint"/>.</summary>
    private void DrawLamp(PaintContext dc, Point center, double radius, double watts, Color tint, string id)
    {
        var brightness = Math.Pow(Math.Max(0, watts) / GeneratorModel.LampWatts, 0.6);
        if (brightness > 0.02)
        {
            var glowRadius = radius * (1.4 + (2.2 * Math.Min(1.6, brightness)));
            var glow = SpaceDrawing.RadialGradient((Color.FromArgb((byte)Math.Clamp(200 * Math.Min(1, brightness), 0, 220), 0xFF, 0xE6, 0x9A), 0), (Color.FromArgb(0, 0xFF, 0xC8, 0x5A), 1));
            dc.DrawEllipse(glow, null, center, glowRadius, glowRadius);
        }

        var glass = brightness > 0.05
            ? FadedBrush(Lerp(Color.FromRgb(0xFF, 0xF0, 0xC0), Colors.White, Math.Min(1, brightness * 0.5)), Math.Clamp(0.25 + (0.7 * brightness), 0, 0.95))
            : Ink.Glass;
        dc.DrawEllipse(glass, Ink.GlassEdge, center, radius, radius);
        if (tint != Ink.Gold) dc.DrawEllipse(null, FadedPen(tint, 0.9, 2), center, radius + 2, radius + 2);

        // The screw base and the filament.
        var baseRect = new Rect(center.X - (radius * 0.45), center.Y + (radius * 0.85), radius * 0.9, radius * 0.7);
        dc.DrawRectangle(Ink.Metal, Ink.Edge, baseRect);
        for (var i = 1; i < 3; i++) dc.DrawLine(Ink.Edge, new Point(baseRect.Left, baseRect.Top + (i * baseRect.Height / 3)), new Point(baseRect.Right, baseRect.Top + (i * baseRect.Height / 3)));
        var filament = new List<Point> { new(center.X - (radius * 0.3), center.Y + (radius * 0.85)), new(center.X - (radius * 0.3), center.Y) };
        for (var i = 0; i <= 6; i++) filament.Add(new Point(center.X - (radius * 0.3) + (radius * 0.6 * i / 6), center.Y - (i % 2 == 0 ? 0 : radius * 0.15)));
        filament.Add(new Point(center.X + (radius * 0.3), center.Y + (radius * 0.85)));
        var hot = brightness > 0.05 ? Lerp(Color.FromRgb(0xFF, 0x8A, 0x3A), Color.FromRgb(0xFF, 0xF8, 0xE0), Math.Min(1, brightness)) : Color.FromRgb(0x8A, 0x8A, 0x8A);
        dc.DrawGeometry(null, FadedPen(Quantize(hot), 1, 1.4), Line(filament));
        if (Glows(id)) dc.DrawEllipse(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 6), center, radius + 6, radius + 6);
    }

    /// <summary>A knife switch in the wire, closed or open.</summary>
    private static void DrawSwitch(PaintContext dc, Point at, bool closed)
    {
        var pivot = at + new Vector(0, 9);
        var contact = at - new Vector(0, 9);
        dc.DrawEllipse(Ink.Metal, Ink.Edge, pivot, 3, 3);
        dc.DrawEllipse(Ink.Metal, Ink.Edge, contact, 3, 3);
        var tip = closed ? contact : pivot + new Vector(12, -12);
        dc.DrawLine(MakePen(Ink.Brass, 3), pivot, tip);
    }

    #endregion

    #region the three-phase machine

    private void DrawThreePhaseMachine(PaintContext dc, Rect machine, Rect lampArea, Rect scope)
    {
        var pairs = Math.Max(1, PolePairs);
        var outer = Math.Clamp(Math.Min(machine.Width * 0.36, machine.Height * 0.4), 60, 230);
        var center = new Point(machine.Left + (machine.Width * 0.5), machine.Top + (machine.Height * 0.48));
        var inner = outer * 0.68;
        var rotorRadius = outer * 0.56;
        var phi = _angle * Math.PI / 180;
        var (ea, eb, ec) = (_now.A, _now.B, _now.C);
        var emf = new[] { ea, eb, ec };
        var peak = ScopePeak;
        Point At(double degrees, double radius) => center + new Vector(Math.Cos(degrees * Math.PI / 180) * radius, -Math.Sin(degrees * Math.PI / 180) * radius);

        // End windings outside the stator, joining each coil's two sides.
        for (var k = 0; k < 3; k++)
        {
            for (var j = 0; j < pairs; j++)
            {
                var a0 = 90 + (((k * 120.0) + (j * 360)) / pairs);
                var a1 = a0 + (180.0 / pairs);
                var radius = outer + 8 + (k * 6);
                var points = Enumerable.Range(0, 25).Select(i => At(a0 + ((a1 - a0) * i / 24), radius)).ToList();
                points.Insert(0, At(a0, outer - ((outer - inner) * 0.3)));
                points.Add(At(a1, outer - ((outer - inner) * 0.3)));
                var strength = 0.3 + (0.6 * Math.Min(1, Math.Abs(emf[k]) / peak));
                dc.DrawGeometry(null, FadedPen(Ink.Phases[k], strength, 3), Line(points));
            }
        }

        // The stator: an iron ring with slots holding the coils' sides.
        var ring = new GeometryGroup { FillRule = FillRule.EvenOdd };
        ring.Children.Add(SpaceDrawing.Ellipse(center, outer, outer));
        ring.Children.Add(SpaceDrawing.Ellipse(center, inner, inner));
        dc.DrawGeometry(Ink.Iron, Ink.Edge, ring);
        if (Glows("stator")) dc.DrawEllipse(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 6), center, outer + 3, outer + 3);
        _targets.Add("stator", At(225, (outer + inner) / 2), (outer - inner) / 2);

        for (var k = 0; k < 3; k++)
        {
            var phase = PhaseId(k);
            for (var j = 0; j < pairs; j++)
            {
                for (var side = 0; side < 2; side++)
                {
                    var angle = 90 + (((k * 120.0) + (j * 360)) / pairs) + (side * 180.0 / pairs);
                    var slot = At(angle, inner + ((outer - inner) * 0.32));
                    var size = Math.Max(7, (outer - inner) * 0.26);
                    var strength = 0.35 + (0.65 * Math.Min(1, Math.Abs(emf[k]) / peak));
                    dc.DrawEllipse(FadedBrush(Ink.Phases[k], strength), Ink.Edge, slot, size, size);

                    // Current out of the picture (dot) or into it (cross); the return side the other way.
                    var sign = Math.Sign(emf[k]) * (side == 0 ? 1 : -1);
                    if (LampOn && Math.Abs(emf[k]) > peak * 0.1)
                    {
                        if (sign > 0)
                        {
                            dc.DrawEllipse(Brushes.Black, null, slot, size * 0.28, size * 0.28);
                        }
                        else
                        {
                            var d = size * 0.45;
                            var pen = MakePen(Brushes.Black, 1.6);
                            dc.DrawLine(pen, slot + new Vector(-d, -d), slot + new Vector(d, d));
                            dc.DrawLine(pen, slot + new Vector(-d, d), slot + new Vector(d, -d));
                        }
                    }

                    if (Glows(phase)) dc.DrawEllipse(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 5), slot, size + 3, size + 3);
                    _targets.Add(phase, slot, size + 2);
                    if (side == 0 && j == 0) DrawText(dc, Loc($"Grid_Phase_{k}"), At(angle, outer + 34 + (k * 6)), Ink.Phases[k], 13, center: true, bold: true);
                }
            }
        }

        // The rotor: a magnet with 2 or 4 poles, turning.
        var rotorAngle = 90 + (_angle - (90.0 / pairs));
        var poles = 2 * pairs;
        for (var i = 0; i < poles; i++)
        {
            var mid = rotorAngle + (i * 360.0 / poles);
            var start = mid - (180.0 / poles);
            var end = mid + (180.0 / poles);
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(center, true);
                for (var s = 0; s <= 16; s++) context.LineTo(At(start + ((end - start) * s / 16), rotorRadius));

                context.EndFigure(true);
            }
            dc.DrawGeometry(i % 2 == 0 ? Ink.NorthPole : Ink.SouthPole, Ink.Edge, geometry);
            DrawText(dc, i % 2 == 0 ? "N" : "S", At(mid, rotorRadius * 0.66), Ink.Text, Math.Clamp(rotorRadius * 0.22, 11, 22), center: true, bold: true);
        }

        dc.DrawEllipse(Ink.MetalDark, Ink.Edge, center, rotorRadius * 0.16, rotorRadius * 0.16);
        if (Glows("rotor")) dc.DrawEllipse(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 6), center, rotorRadius + 3, rotorRadius + 3);
        _targets.Add("rotor", center, rotorRadius * 0.8);

        // The field: out of each north pole across the gap, round the stator's back iron, and into the next south poles.
        var fieldPen = FadedPen(Ink.Field, 0.6, 1.4);
        var chevron = FadedBrush(Ink.Field, 0.9);
        var backIron = outer - ((outer - inner) * 0.2);
        for (var i = 0; i < poles; i += 2)
        {
            var north = rotorAngle + (i * 360.0 / poles);
            foreach (var dir in new[] { 1.0, -1.0 })
            {
                var start = north + (dir * 28 / pairs);
                var end = north + (dir * 152 / pairs);
                var path = new List<Point> { At(start, rotorRadius * 0.92) };
                for (var k = 0; k <= 20; k++) path.Add(At(start + ((end - start) * k / 20), backIron));
                path.Add(At(end, rotorRadius * 0.92));
                dc.DrawGeometry(null, fieldPen, Line(path));
                DrawArrowHead(dc, chevron, At(start, (rotorRadius + inner) / 2), At(start, inner) - At(start, rotorRadius), 7);
                DrawArrowHead(dc, chevron, At(end, (rotorRadius + inner) / 2), At(end, rotorRadius) - At(end, inner), 7);
            }
        }

        // Three lamps, one on each phase, each lit by its own voltage.
        var lampRadius = Math.Clamp(Math.Min(lampArea.Width / 9, lampArea.Height * 0.2), 12, 30);
        var lampY = lampArea.Top + (lampArea.Height * 0.45);
        var resistance = GeneratorModel.LampResistance + GeneratorModel.CoilResistance(Turns);
        for (var k = 0; k < 3; k++)
        {
            var lamp = new Point(lampArea.Left + (lampArea.Width * (0.18 + (0.3 * k))), lampY);
            var watts = LampOn ? emf[k] * emf[k] / (resistance * resistance) * GeneratorModel.LampResistance : 0;
            DrawLamp(dc, lamp, lampRadius, watts, Ink.Phases[k], "lamps");
            _targets.Add("lamps", lamp, lampRadius * 1.1);
            DrawText(dc, Loc($"Grid_Phase_{k}"), lamp + new Vector(0, (lampRadius * 1.9) + 8), Ink.Phases[k], 12, center: true, bold: true);
        }

        if (!ShowLabels) return;
        DrawCallout(dc, Loc("Grid_Obj_Rotor"), At(rotorAngle + 180, rotorRadius * 0.4), new Point(machine.Left + 10, center.Y + (outer * 0.15)), right: true, "rotor");
        DrawCallout(dc, Loc("Grid_Obj_Stator"), At(250, (outer + inner) / 2), new Point(machine.Left + 10, center.Y + (outer * 0.95)), right: true, "stator");
    }

    #endregion

    #region the oscilloscope

    /// <summary>The oscilloscope: the voltage (or the three phases' and their sum) over the last couple of seconds.</summary>
    private void DrawScope(PaintContext dc, Rect area)
    {
        dc.DrawRoundedRectangle(Ink.ScopeBody, Ink.Edge, area, 10, 10);
        var screen = new Rect(area.Left + 10, area.Top + 26, Math.Max(40, area.Width - 20), Math.Max(40, area.Height - 36));
        dc.DrawRoundedRectangle(Ink.ScopeScreen, null, screen, 4, 4);
        DrawText(dc, Loc("Grid_Obj_Scope"), new Point(area.Left + 12, area.Top + 13), Ink.Dim, 11);
        if (Glows("scope")) dc.DrawRoundedRectangle(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 6), area, 10, 10);
        AddBoxTarget("scope", area);

        // The grid: ten across, a line at zero.
        for (var i = 1; i < 10; i++)
        {
            var x = screen.Left + (screen.Width * i / 10);
            dc.DrawLine(Ink.ScopeGrid, new Point(x, screen.Top), new Point(x, screen.Bottom));
        }

        for (var i = 1; i < 4; i++)
        {
            var y = screen.Top + (screen.Height * i / 4);
            dc.DrawLine(i == 2 ? Ink.ScopeAxis : Ink.ScopeGrid, new Point(screen.Left, y), new Point(screen.Right, y));
        }

        var scale = NiceScale(ScopePeak * 1.15);
        DrawTextRight(dc, $"±{Number(scale, scale < 10 ? "0.#" : "0")} V", new Point(area.Right - 12, area.Top + 13), Ink.Dim, 11);
        if (_scope.Count < 2 && (IsPlaying || _dragging)) return;

        var mid = screen.Top + (screen.Height / 2);
        var half = screen.Height / 2 * 0.95;
        Point Map(double t, double v) => new(
            screen.Right - ((_scopeTime - t) / ScopeWindow * screen.Width),
            mid - (Math.Clamp(v / scale, -1.05, 1.05) * half));

        // Paused, the trace is a still picture of the coil turning at its set speed up to where it stands.
        var samples = IsPlaying || _dragging ? _scope.Where(p => p.T >= _scopeTime - ScopeWindow).ToList() : StillTrace();
        var traces = Kind == GeneratorKind.ThreePhase ? 3 : 1;
        for (var k = 0; k < traces; k++)
        {
            var color = Kind == GeneratorKind.ThreePhase ? Ink.Phases[k] : Color.FromRgb(0x6A, 0xFF, 0xB0);
            var points = samples.Select(p => Map(p.T, k switch { 0 => p.A, 1 => p.B, _ => p.C })).ToList();
            if (points.Count < 2) continue;
            dc.DrawGeometry(null, FadedPen(color, 0.95, 2), Line(points));
            dc.DrawEllipse(FadedBrush(color, 1), null, Map(_scopeTime, k switch { 0 => _now.A, 1 => _now.B, _ => _now.C }), 3.5, 3.5);
        }

        if (Kind == GeneratorKind.ThreePhase)
        {
            // The three added together: always zero.
            var sum = samples.Select(p => Map(p.T, p.A + p.B + p.C)).ToList();
            if (sum.Count >= 2) dc.DrawGeometry(null, FadedPen(Ink.Text, 0.75, 1.4), Line(sum));
            DrawText(dc, Loc("Grid_Gen_Sum"), new Point(screen.Left + 6, mid + 10), Ink.Text, 10.5);
        }
        else
        {
            var value = _now.A;
            DrawTextRight(dc, $"{(value >= 0 ? "+" : "−")}{Number(Math.Abs(value), "0.0")} V", new Point(screen.Right - 6, screen.Top + 10), Color.FromRgb(0x6A, 0xFF, 0xB0), 12, bold: true);
        }
    }

    /// <summary>The scope's window as if the coil had been turning steadily at its set speed up to its angle now.</summary>
    private List<ScopeSample> StillTrace()
    {
        var samples = new List<ScopeSample>(241);
        var peak = GeneratorModel.PeakEmf(Turns, Field, CoilSpeed);
        for (var i = 0; i <= 240; i++)
        {
            var back = ScopeWindow * (240 - i) / 240;
            var phi = (_angle - (360 * CoilSpeed * back)) * Math.PI / 180;
            if (Kind == GeneratorKind.ThreePhase)
            {
                var (a, b, c) = GeneratorModel.ThreePhase(peak, phi, Math.Max(1, PolePairs));
                samples.Add(new ScopeSample(_scopeTime - back, a, b, c));
            }
            else
            {
                samples.Add(new ScopeSample(_scopeTime - back, GeneratorModel.Output(Kind, peak * Math.Sin(phi)), 0, 0));
            }
        }

        return samples;
    }

    /// <summary>The first of 1, 2, 3, 5, 10, 15, 20, 30, 50… that is at least <paramref name="value"/>.</summary>
    private static double NiceScale(double value)
    {
        foreach (var step in new[] { 0.5, 1, 2, 3, 5, 10, 15, 20, 30, 50, 100, 200 })
        {
            if (step >= value) return step;
        }

        return 500;
    }

    #endregion
}
