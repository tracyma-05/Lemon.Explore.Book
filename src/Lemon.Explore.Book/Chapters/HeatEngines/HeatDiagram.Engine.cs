using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.HeatEngines;

/// <summary>
/// Engine mode: one cylinder cut open down its middle, drawn to scale from <see cref="EngineModel"/> (86 mm bore and
/// stroke), with the valves, cams and spark plug, the gas coloured by its temperature, and a dial of the four strokes.
/// </summary>
public sealed partial class HeatDiagram
{
    /// <summary>Gas particles in the cylinder.</summary>
    private const int GasParticles = 64;

    /// <summary>Particles along each port while its valve is open.</summary>
    private const int PortParticles = 18;

    /// <summary>A frame of the engine in screen space: millimetres from the top of the cylinder, on its axis.</summary>
    private readonly record struct EngineFrame(double X, double Y, double Scale)
    {
        public Point P(double x, double y) => new(X + (x * Scale), Y + (y * Scale));

        public double L(double mm) => mm * Scale;
    }

    private const double HalfBore = EngineModel.Bore * 500;
    private const double PistonHeight = 55;
    private const double PinBelowTop = 30;

    /// <summary>The fresh charge's colour: blue, turning violet as compression heats it.</summary>
    private static Color FreshColor(double kelvin) =>
        Lerp(Color.FromRgb(0x4A, 0x8B, 0xDF), Color.FromRgb(0xB8, 0x7A, 0xE0), Math.Clamp((kelvin - 330) / 380, 0, 1));

    private static Color BurnedColor(double kelvin) => HeatColor(kelvin - 273.15);

    private void DrawEngineMode(PaintContext dc, double width, double height)
    {
        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));

        var stage = Stage();
        var state = EngineModel.At(_angle, CompressionRatio, Ambient);
        var clearance = EngineModel.ClearanceVolume(CompressionRatio) / EngineModel.PistonArea * 1000;

        // Fit the engine, 456 mm from the spark plug's tip to the sump, into the height; the dial takes the right part.
        var dialSpace = Math.Clamp(width * 0.3, 190, 360);
        var scale = Math.Min(stage.Height / 470, (width - dialSpace - 40) / 300);
        scale = Math.Max(0.4, scale);
        var engineX = Math.Max((150 * scale) + 20, (width - dialSpace) * 0.52);
        var frame = new EngineFrame(engineX, stage.Top + (142 * scale), scale);

        var pistonTop = clearance + (state.PistonTravel * 1000);
        var crankY = clearance + PinBelowTop + (EngineModel.RodLength * 1000) + (EngineModel.CrankRadius * 1000);

        DrawCrankcase(dc, frame, crankY);
        DrawCrank(dc, frame, crankY, state.Angle, behindRod: true);
        DrawRod(dc, frame, pistonTop + PinBelowTop, crankY, state.Angle);
        DrawCrank(dc, frame, crankY, state.Angle, behindRod: false);
        DrawGas(dc, frame, state, pistonTop);
        DrawPiston(dc, frame, pistonTop, state);
        DrawBlock(dc, frame);
        DrawHead(dc, frame, state);
        DrawValves(dc, frame, state);
        DrawSparkPlug(dc, frame, state);
        DrawPortFlow(dc, frame, state);

        if (ShowLabels) DrawEngineLabels(dc, frame, pistonTop, crankY);
        AddEngineTargets(frame, state, pistonTop, crankY);

        // The gas's pressure and temperature, beside the cylinder.
        var reading = LocFormat("Heat_Engine_Reading", Number(state.Pressure, state.Pressure < 10 ? "0.00" : "0"), Number(state.Temperature - 273.15, "0"));
        AddBoxTarget("gas", DrawPill(dc, reading, frame.P(0, Math.Max(18, pistonTop / 2)) + new Vector(frame.L(HalfBore + 70), 0), GasLabelColor(state)));

        var dialRadius = Math.Clamp(Math.Min(dialSpace * 0.36, stage.Height * 0.24), 60, 120);
        var dial = new Point(width - (dialSpace / 2) - 6, stage.Top + (stage.Height * 0.44));
        DrawStrokeDial(dc, dial, dialRadius, state);
    }

    /// <summary>
    /// The parts themselves as targets, after their labels so a walkthrough rings the part. The gas is the space between
    /// the head and the piston; the valves and the plug, smaller, win a click over it.
    /// </summary>
    private void AddEngineTargets(EngineFrame f, EngineState state, double pistonTop, double crankY)
    {
        var a = EngineModel.CrankRadius * 1000;
        var theta = state.Angle * Math.PI / 180;
        var pin = new Point(a * Math.Sin(theta), crankY - (a * Math.Cos(theta)));
        var small = new Point(0, pistonTop + PinBelowTop);
        if (pistonTop > 6) AddBoxTarget("gas", new Rect(f.P(-HalfBore, 0), f.P(HalfBore, pistonTop)));
        AddBoxTarget("oil", new Rect(f.P(-98, crankY + 70), f.P(98, crankY + 92)));
        _targets.Add("crank", f.P(0, crankY), f.L(46));
        _targets.Add("rod", f.P((small.X + pin.X) / 2, (small.Y + pin.Y) / 2), f.L(15));
        AddBoxTarget("piston", new Rect(f.P(-HalfBore, pistonTop), f.P(HalfBore, pistonTop + PistonHeight)));
        foreach (var side in new[] { -1, 1 })
        {
            AddBoxTarget("coolant", new Rect(f.P(Math.Min(side * (HalfBore + 7), side * (HalfBore + 21)), 8), f.P(Math.Max(side * (HalfBore + 7), side * (HalfBore + 21)), 112)));
        }

        AddBoxTarget("intake", IntakePort(f).Bounds);
        AddBoxTarget("exhaust", ExhaustPort(f).Bounds);
        _targets.Add("camshaft", f.P(20, -102), f.L(21));
        _targets.Add("camshaft", f.P(-20, -102), f.L(21));
        _targets.Add("exhaustvalve", f.P(20, (state.ExhaustLift * 9) - 2), f.L(15));
        _targets.Add("intakevalve", f.P(-20, (state.IntakeLift * 9) - 2), f.L(15));
        AddBoxTarget("sparkplug", new Rect(f.P(-9, -136), f.P(9, -64)));
    }

    private static Color GasLabelColor(EngineState state) => state.Burned > 0.5 ? BurnedColor(state.Temperature) : FreshColor(state.Temperature);

    #region the moving parts

    private static void DrawCrankcase(PaintContext dc, EngineFrame f, double crankY)
    {
        var top = 150.0;
        var bottom = crankY + 92;
        var half = 98.0;
        var outline = new StreamGeometry();
        using (var context = outline.Open())
        {
            context.BeginFigure(f.P(-half + 26, top), true);
            context.LineTo(f.P(half - 26, top));
            context.QuadraticBezierTo(f.P(half, top), f.P(half, top + 30));
            context.LineTo(f.P(half, bottom - 34));
            context.QuadraticBezierTo(f.P(half, bottom), f.P(half - 34, bottom));
            context.LineTo(f.P(-half + 34, bottom));
            context.QuadraticBezierTo(f.P(-half, bottom), f.P(-half, bottom - 34));
            context.LineTo(f.P(-half, top + 30));
            context.QuadraticBezierTo(f.P(-half, top), f.P(-half + 26, top));

            context.EndFigure(true);
        }
        dc.DrawGeometry(Ink.CrankcaseInside, Ink.Edge, outline);

        // Oil in the sump, splashed by the crank.
        dc.PushClip(outline);
        dc.DrawRectangle(Ink.Oil, null, new Rect(f.P(-half, bottom - 22), f.P(half, bottom)));
        dc.Pop();
        dc.DrawGeometry(null, MakePen(Ink.MetalDark, f.L(6)), outline);
    }

    private static void DrawCrank(PaintContext dc, EngineFrame f, double crankY, double angle, bool behindRod)
    {
        var a = EngineModel.CrankRadius * 1000;
        var theta = angle * Math.PI / 180;
        var centre = f.P(0, crankY);
        var pin = f.P(a * Math.Sin(theta), crankY - (a * Math.Cos(theta)));
        if (behindRod)
        {
            // The counterweight, opposite the pin, and the web joining the main journal to it.
            var opposite = theta + Math.PI;
            var weight = new StreamGeometry();
            using (var context = weight.Open())
            {
                const int steps = 24;
                for (var i = 0; i <= steps; i++)
                {
                    var u = opposite - 1.2 + (2.4 * i / steps);
                    var point = f.P(64 * Math.Sin(u), crankY - (64 * Math.Cos(u)));
                    if (i == 0) context.BeginFigure(point, true);
                    else context.LineTo(point);
                }

                context.LineTo(f.P(24 * Math.Sin(theta + 1.9), crankY - (24 * Math.Cos(theta + 1.9))));
                context.LineTo(f.P(24 * Math.Sin(theta - 1.9), crankY - (24 * Math.Cos(theta - 1.9))));

                context.EndFigure(true);
            }
            dc.DrawGeometry(Ink.Counterweight, Ink.Edge, weight);
            dc.DrawLine(MakePen(Ink.Crank, f.L(30)), centre, pin);
            dc.DrawEllipse(Ink.Crank, Ink.Edge, centre, f.L(22), f.L(22));
            return;
        }

        dc.DrawEllipse(Ink.MetalDark, Ink.Edge, centre, f.L(11), f.L(11));
        dc.DrawEllipse(Ink.CrankPin, Ink.Edge, pin, f.L(9), f.L(9));
    }

    private static void DrawRod(PaintContext dc, EngineFrame f, double pinY, double crankY, double angle)
    {
        var a = EngineModel.CrankRadius * 1000;
        var theta = angle * Math.PI / 180;
        var small = f.P(0, pinY);
        var big = f.P(a * Math.Sin(theta), crankY - (a * Math.Cos(theta)));
        Vector along = big - small;
        along = along.Normalize();
        var across = new Vector(-along.Y, along.X);
        var body = Shape(small + (across * f.L(8)), big + (across * f.L(13)), big - (across * f.L(13)), small - (across * f.L(8)));
        dc.DrawGeometry(Ink.Rod, Ink.Edge, body);
        dc.DrawEllipse(Ink.Rod, Ink.Edge, big, f.L(19), f.L(19));
        dc.DrawEllipse(Ink.Rod, Ink.Edge, small, f.L(12), f.L(12));
    }

    private void DrawPiston(PaintContext dc, EngineFrame f, double top, EngineState state)
    {
        var half = HalfBore - 0.8;
        var body = new StreamGeometry();
        using (var context = body.Open())
        {
            context.BeginFigure(f.P(-half, top), true);
            context.LineTo(f.P(half, top));
            context.LineTo(f.P(half, top + PistonHeight - 6));
            context.QuadraticBezierTo(f.P(half, top + PistonHeight), f.P(half - 8, top + PistonHeight));
            context.LineTo(f.P(26, top + PistonHeight));
            context.LineTo(f.P(18, top + PinBelowTop + 14));
            context.LineTo(f.P(-18, top + PinBelowTop + 14));
            context.LineTo(f.P(-26, top + PistonHeight));
            context.LineTo(f.P(-half + 8, top + PistonHeight));
            context.QuadraticBezierTo(f.P(-half, top + PistonHeight), f.P(-half, top + PistonHeight - 6));

            context.EndFigure(true);
        }
        dc.DrawGeometry(Ink.Piston, Ink.Edge, body);
        foreach (var ring in new[] { 5.0, 10, 15 })
        {
            dc.DrawLine(Ink.Ring, f.P(-half, top + ring), f.P(-half + 6, top + ring));
            dc.DrawLine(Ink.Ring, f.P(half - 6, top + ring), f.P(half, top + ring));
        }

        dc.DrawEllipse(Ink.MetalDark, Ink.Edge, f.P(0, top + PinBelowTop), f.L(7), f.L(7));

        // Which way the piston goes, and in the power stroke the gas pushing it.
        var down = state.Stroke is EngineStroke.Intake or EngineStroke.Power;
        var arrowX = -HalfBore - 34;
        var mid = top + (PistonHeight / 2);
        var color = Ink.Strokes[(int)state.Stroke];
        DrawArrow(dc, f.P(arrowX, mid + (down ? -18 : 18)), f.P(arrowX, mid + (down ? 18 : -18)), FadedBrush(color, 0.95), 2.4, 9);
        if (state.Stroke == EngineStroke.Power && state.Burned > 0.2)
        {
            var push = Math.Clamp(state.Pressure / 50, 0.25, 1);
            foreach (var x in new[] { -24.0, 0, 24 })
            {
                DrawArrow(dc, f.P(x, top - 4 - (14 * push)), f.P(x, top - 1), FadedBrush(Ink.Gold, 0.9 * push), 2.2, 7);
            }
        }
    }

    #endregion

    #region block, head, valves and plug

    private static void DrawBlock(PaintContext dc, EngineFrame f)
    {
        foreach (var side in new[] { -1, 1 })
        {
            var inner = side * HalfBore;
            var outer = side * (HalfBore + 27);
            dc.DrawRectangle(Ink.Block, null, new Rect(f.P(Math.Min(inner, outer), 0), f.P(Math.Max(inner, outer), 150)));

            // The water jacket round the cylinder carries the walls' heat to the radiator.
            var jacketInner = side * (HalfBore + 7);
            var jacketOuter = side * (HalfBore + 21);
            dc.DrawRoundedRectangle(Ink.Water, null, new Rect(f.P(Math.Min(jacketInner, jacketOuter), 8), f.P(Math.Max(jacketInner, jacketOuter), 112)), f.L(3), f.L(3));
            dc.DrawLine(Ink.Liner, f.P(inner, 0), f.P(inner, 150));
        }
    }

    /// <summary>The head with the two ports cut through it; the ports carry the colour of the gas in them.</summary>
    private void DrawHead(PaintContext dc, EngineFrame f, EngineState state)
    {
        dc.DrawRectangle(Ink.Head, null, new Rect(f.P(-128, -64), f.P(128, 0)));

        var intake = IntakePort(f);
        var exhaust = ExhaustPort(f);
        var fresh = FreshColor(EngineModel.IntakeTemperature);
        var burned = BurnedColor(state.Stroke == EngineStroke.Exhaust || state.ExhaustLift > 0 ? Math.Max(700, state.Temperature) : 750);
        dc.DrawGeometry(Ink.Port, null, intake);
        dc.DrawGeometry(FadedBrush(fresh, 0.18 + (0.3 * state.IntakeLift)), Ink.Edge, intake);
        dc.DrawGeometry(Ink.Port, null, exhaust);
        dc.DrawGeometry(FadedBrush(burned, 0.15 + (0.35 * state.ExhaustLift)), Ink.Edge, exhaust);

        // The cam cover over the valve train.
        var cover = new StreamGeometry();
        using (var context = cover.Open())
        {
            context.BeginFigure(f.P(-110, -64), true);
            context.LineTo(f.P(-110, -112));
            context.QuadraticBezierTo(f.P(-110, -124), f.P(-96, -124));
            context.LineTo(f.P(96, -124));
            context.QuadraticBezierTo(f.P(110, -124), f.P(110, -112));
            context.LineTo(f.P(110, -64));

            context.EndFigure(true);
        }
        dc.DrawGeometry(Ink.Cover, Ink.LightEdge, cover);
    }

    private static StreamGeometry IntakePort(EngineFrame f)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(f.P(-128, -50), true);
            context.LineTo(f.P(-70, -50));
            context.CubicBezierTo(f.P(-34, -48), f.P(-8, -26), f.P(-6, 0));
            context.LineTo(f.P(-34, 0));
            context.CubicBezierTo(f.P(-38, -12), f.P(-52, -24), f.P(-74, -24));
            context.LineTo(f.P(-128, -24));

            context.EndFigure(true);
        }
        return geometry;
    }

    private static StreamGeometry ExhaustPort(EngineFrame f)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(f.P(128, -50), true);
            context.LineTo(f.P(70, -50));
            context.CubicBezierTo(f.P(34, -48), f.P(8, -26), f.P(6, 0));
            context.LineTo(f.P(34, 0));
            context.CubicBezierTo(f.P(38, -12), f.P(52, -24), f.P(74, -24));
            context.LineTo(f.P(128, -24));

            context.EndFigure(true);
        }
        return geometry;
    }

    /// <summary>The valves, their springs, and the cams (turning at half the crank's speed) that push them open.</summary>
    private void DrawValves(PaintContext dc, EngineFrame f, EngineState state)
    {
        const double maxLift = 9;
        foreach (var (x, lift, opens, closes) in new[]
        {
            (-20.0, state.IntakeLift, EngineModel.IntakeOpens, EngineModel.IntakeCloses + 720),
            (20.0, state.ExhaustLift, EngineModel.ExhaustOpens, EngineModel.ExhaustCloses),
        })
        {
            var drop = lift * maxLift;

            // The stem through the head, the spring round it, the cap the cam presses on.
            dc.DrawRectangle(Ink.Valve, Ink.Edge, new Rect(f.P(x - 2.6, -86 + drop), f.P(x + 2.6, drop - 2)));
            var head = Shape(f.P(x - 3.5, drop - 5), f.P(x + 3.5, drop - 5), f.P(x + 14, drop - 1), f.P(x + 14, drop + 2.5),
                f.P(x - 14, drop + 2.5), f.P(x - 14, drop - 1));
            dc.DrawGeometry(Ink.Valve, Ink.Edge, head);

            var springTop = -84 + drop;
            var springBottom = -66.0;
            var coil = new List<Point>();
            const int turns = 7;
            for (var i = 0; i <= turns * 2; i++)
            {
                var y = springTop + ((springBottom - springTop) * i / (turns * 2));
                coil.Add(f.P(x + (i % 2 == 0 ? -8 : 8), y));
            }

            dc.DrawGeometry(null, Ink.Spring, Line(coil));
            dc.DrawRectangle(Ink.MetalDark, Ink.Edge, new Rect(f.P(x - 10, springTop - 4), f.P(x + 10, springTop)));

            // The cam: a circle with a lobe that points down at the middle of the valve's opening.
            var middle = opens + ((closes - opens) / 2);
            var turn = (state.Angle - middle) / 2 * Math.PI / 180;
            DrawCam(dc, f, new Point(x, -102 + 0), turn);
        }
    }

    private static void DrawCam(PaintContext dc, EngineFrame f, Point centre, double turn)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            const int steps = 48;
            for (var i = 0; i <= steps; i++)
            {
                var u = 2 * Math.PI * i / steps;

                // Base circle 11 mm, rising to 20 mm at the lobe (lobe straight down at turn 0).
                var lobe = Math.Pow(Math.Max(0, Math.Cos(u)), 3);
                var r = 11 + (9 * lobe);
                var a = u + turn;
                var point = f.P(centre.X + (r * Math.Sin(a)), centre.Y + (r * Math.Cos(a)));
                if (i == 0) context.BeginFigure(point, true);
                else context.LineTo(point);
            }

            context.EndFigure(true);
        }
        dc.DrawGeometry(Ink.Cam, Ink.Edge, geometry);
        dc.DrawEllipse(Ink.MetalDark, null, f.P(centre.X, centre.Y), f.L(4), f.L(4));
    }

    private void DrawSparkPlug(PaintContext dc, EngineFrame f, EngineState state)
    {
        dc.DrawRectangle(Ink.PlugMetal, Ink.Edge, new Rect(f.P(-5, -64), f.P(5, 0)));
        dc.DrawRectangle(Ink.PlugMetal, Ink.Edge, new Rect(f.P(-9, -84), f.P(9, -64)));
        dc.DrawRoundedRectangle(Ink.Plug, Ink.Edge, new Rect(f.P(-5.5, -128), f.P(5.5, -84)), f.L(2), f.L(2));
        dc.DrawRectangle(Ink.PlugMetal, Ink.Edge, new Rect(f.P(-3, -136), f.P(3, -128)));
        dc.DrawLine(MakePen(Ink.PlugMetal, f.L(1.6)), f.P(0, 0), f.P(0, 3));

        // The spark: a bright flash at the gap for a few degrees.
        var since = EngineModel.Wrap(state.Angle - EngineModel.SparkAngle);
        if (since < 7)
        {
            var strength = 1 - (since / 7);
            var tip = f.P(0, 4);
            var glow = SpaceDrawing.RadialGradient((Color.FromArgb((byte)(220 * strength), 0xFF, 0xF6, 0xC8), 0), (Color.FromArgb(0, 0xFF, 0xD3, 0x6B), 1));
            dc.DrawEllipse(glow, null, tip, f.L(22), f.L(22));
            var zig = new List<Point>();
            for (var i = 0; i < 7; i++)
            {
                var angle = (i * 0.9) + (_t * 30);
                var length = f.L(i % 2 == 0 ? 11 : 6);
                zig.Add(tip + new Vector(Math.Cos(angle) * length, Math.Sin(angle) * length));
                zig.Add(tip);
            }

            dc.DrawGeometry(null, FadedPen(Ink.Spark, strength, 1.6), Line(zig));
        }
    }

    #endregion

    #region the gas

    /// <summary>
    /// The gas between the head and the piston: its colour, the flame spreading out from the plug while it burns, and
    /// particles jiggling faster the hotter it is.
    /// </summary>
    private void DrawGas(PaintContext dc, EngineFrame f, EngineState state, double pistonTop)
    {
        var chamber = new Rect(f.P(-HalfBore, 0), f.P(HalfBore, pistonTop));
        var fresh = FreshColor(state.Temperature);
        var burned = BurnedColor(state.Temperature);
        var burning = state.Angle > EngineModel.SparkAngle && state.Angle < EngineModel.ExhaustOpens && state.Burned < 0.999;

        dc.PushClip(new RectangleGeometry(chamber));
        if (burning)
        {
            // Unburned charge ahead of the flame, burned gas behind it.
            dc.DrawRectangle(FadedBrush(Quantize(fresh), 0.55), null, chamber);
            var radius = FlameRadius(f, state.Burned, pistonTop);
            var flame = SpaceDrawing.RadialGradient(
            (Color.FromArgb(235, burned.R, burned.G, burned.B), 0.6),
            (Color.FromArgb(255, 0xFF, 0xF2, 0xB0), 0.93),
            (Color.FromArgb(0, 0xFF, 0xD3, 0x6B), 1));
            dc.DrawEllipse(flame, null, f.P(0, 3), radius, radius);
        }
        else
        {
            var color = Lerp(fresh, burned, state.Burned);
            dc.DrawRectangle(FadedBrush(Quantize(color), 0.6), null, chamber);
        }

        // Particles: their speed grows as the square root of the temperature.
        var jiggle = 1.2 * Math.Sqrt(state.Temperature / 300);
        var height = Math.Max(1, pistonTop - 3);
        var flameRadius = burning ? FlameRadius(f, state.Burned, pistonTop) : 0;
        for (var i = 0; i < GasParticles; i++)
        {
            var u = Frac(0.5 + (i * 0.6180339887));
            var v = Frac(0.5 + (i * 0.7548776662));
            var phase = i * 2.39996;
            var x = -HalfBore + 4 + (u * ((HalfBore * 2) - 8)) + (Math.Sin((_t * 7.3 * jiggle) + phase) * jiggle * 1.6);
            var y = 1.5 + (v * height) + (Math.Cos((_t * 6.1 * jiggle) + (phase * 1.7)) * jiggle * Math.Min(1.4, height * 0.08));
            var point = f.P(x, Math.Clamp(y, 1, pistonTop - 1));
            var isBurned = burning
                ? ((Vector)(point - f.P(0, 3))).Length < flameRadius
                : Frac(i * 0.381966) < state.Burned;
            var color = isBurned ? burned : Lerp(fresh, Colors.White, 0.35);
            dc.DrawEllipse(FadedBrush(Quantize(color), 0.95), null, point, f.L(1.7), f.L(1.7));
        }

        dc.Pop();
    }

    /// <summary>The flame's reach from the plug in pixels: it has swept the whole chamber when everything has burned.</summary>
    private static double FlameRadius(EngineFrame f, double burned, double pistonTop)
    {
        var far = Math.Sqrt((HalfBore * HalfBore) + (pistonTop * pistonTop));
        return f.L(far * Math.Sqrt(Math.Clamp(burned, 0, 1)) * 1.05);
    }

    /// <summary>Fresh charge streaming in past the open intake valve, exhaust streaming out past the exhaust valve.</summary>
    private void DrawPortFlow(PaintContext dc, EngineFrame f, EngineState state)
    {
        if (state.IntakeLift > 0.04)
        {
            Point[] path = [new(-128, -37), new(-78, -37), new(-40, -30), new(-24, -12), new(-20, 4), new(-16, 22)];
            var color = Lerp(FreshColor(EngineModel.IntakeTemperature), Colors.White, 0.35);
            DrawStream(dc, f, path, state.IntakeLift, color, 0);
        }

        if (state.ExhaustLift > 0.04)
        {
            Point[] path = [new(16, 22), new(20, 4), new(24, -12), new(40, -30), new(78, -37), new(128, -37)];
            DrawStream(dc, f, path, state.ExhaustLift, BurnedColor(Math.Max(650, state.Temperature)), 0.37);
        }
    }

    private void DrawStream(PaintContext dc, EngineFrame f, Point[] path, double strength, Color color, double offset)
    {
        var lengths = new double[path.Length];
        for (var i = 1; i < path.Length; i++) lengths[i] = lengths[i - 1] + ((Vector)(path[i] - path[i - 1])).Length;
        var total = lengths[^1];
        for (var k = 0; k < PortParticles; k++)
        {
            var s = Frac((k / (double)PortParticles) + (_t * 0.9) + offset) * total;
            var i = 1;
            while (i < path.Length - 1 && lengths[i] < s) i++;
            var t = (s - lengths[i - 1]) / Math.Max(1e-9, lengths[i] - lengths[i - 1]);
            var at = Mix(path[i - 1], path[i], t);
            var wobble = Math.Sin((k * 2.1) + (_t * 5)) * 3;
            dc.DrawEllipse(FadedBrush(Quantize(color), 0.85 * Math.Min(1, strength * 1.6)), null, f.P(at.X, at.Y + wobble), f.L(1.9), f.L(1.9));
        }
    }

    #endregion

    #region labels and the dial

    private void DrawEngineLabels(PaintContext dc, EngineFrame f, double pistonTop, double crankY)
    {
        var left = -150.0;
        var right = 150.0;
        DrawCallout(dc, Loc("Heat_Part_IntakeValve"), f.P(-20, -20), f.P(left, -6), right: false, "intakevalve");
        DrawCallout(dc, Loc("Heat_Part_Camshaft"), f.P(-30, -104), f.P(left, -104), right: false, "camshaft");
        DrawCallout(dc, Loc("Heat_Part_Coolant"), f.P(-HalfBore - 14, 60), f.P(left, 60), right: false, "coolant");
        DrawCallout(dc, Loc("Heat_Part_Rod"), f.P(-6, pistonTop + PinBelowTop + 50), f.P(left, pistonTop + PinBelowTop + 50), right: false, "rod");
        DrawCallout(dc, Loc("Heat_Part_Crank"), f.P(-30, crankY), f.P(left, crankY + 6), right: false, "crank");

        DrawCallout(dc, Loc("Heat_Part_SparkPlug"), f.P(6, -104), f.P(right - 14, -136), right: true, "sparkplug");
        DrawCallout(dc, Loc("Heat_Part_ExhaustValve"), f.P(30, -20), f.P(right, -6), right: true, "exhaustvalve");
        DrawCallout(dc, Loc("Heat_Part_Piston"), f.P(HalfBore - 10, pistonTop + 22), f.P(right, pistonTop + 22), right: true, "piston");
        DrawCallout(dc, Loc("Heat_Part_Oil"), f.P(60, crankY + 78), f.P(right - 10, crankY + 78), right: true, "oil");

        AddBoxTarget("intake", DrawText(dc, Loc("Heat_Port_Intake"), f.P(-128, -64) + new Vector(0, -12), Ink.Cyan, 11.5));
        AddBoxTarget("exhaust", DrawTextRight(dc, Loc("Heat_Port_Exhaust"), f.P(128, -64) + new Vector(0, -12), Ink.Orange, 11.5));
    }

    /// <summary>
    /// The cycle as a dial: two turns of the crank once round it, the four strokes as coloured quarters, the spark and
    /// the valves' timing marked, and a hand at the crank angle now.
    /// </summary>
    private void DrawStrokeDial(PaintContext dc, Point centre, double radius, EngineState state)
    {
        var thickness = Math.Max(10, radius * 0.16);
        for (var k = 0; k < 4; k++)
        {
            var current = (int)state.Stroke == k;
            var color = Ink.Strokes[k];
            dc.DrawGeometry(null, FadedPen(color, current ? 0.95 : 0.35, thickness, flat: true), Arc(centre, radius, (k * 90) + 1, (k * 90) + 89));

            var middle = ((k * 90) + 45) * Math.PI / 180;
            var labelAt = centre + new Vector(Math.Sin(middle) * (radius + thickness + 20), -Math.Cos(middle) * (radius + thickness + 20));
            var name = DrawText(dc, LocFormat("Heat_Dial_Stroke", k + 1, Loc($"Heat_Stroke_{(EngineStroke)k}")), labelAt, current ? color : Ink.Dim, current ? 12.5 : 11.5,
                center: true, bold: current);
            AddBoxTarget(StrokeId((EngineStroke)k), name);
        }

        // Timing marks: the spark, and where each valve opens and closes.
        DrawDialMark(dc, centre, radius, thickness, EngineModel.SparkAngle, Ink.Spark, "Heat_Dial_Spark");
        DrawDialTick(dc, centre, radius, thickness, EngineModel.IntakeOpens, Ink.Cyan);
        DrawDialTick(dc, centre, radius, thickness, EngineModel.IntakeCloses, Ink.Cyan);
        DrawDialTick(dc, centre, radius, thickness, EngineModel.ExhaustOpens, Ink.Orange);
        DrawDialTick(dc, centre, radius, thickness, EngineModel.ExhaustCloses, Ink.Orange);

        // The dial as a whole, under its strokes' names and the spark.
        _targets.Add("dial", centre, radius * 0.7);

        // The hand.
        var hand = state.Angle / 2 * Math.PI / 180;
        var tip = centre + new Vector(Math.Sin(hand) * (radius + (thickness / 2) + 4), -Math.Cos(hand) * (radius + (thickness / 2) + 4));
        dc.DrawLine(MakePen(Solid(255, 0xEE, 0xF1, 0xF6), 2.2), centre, tip);
        dc.DrawEllipse(Solid(255, 0xEE, 0xF1, 0xF6), null, centre, 4, 4);

        var strokeColor = Ink.Strokes[(int)state.Stroke];
        DrawText(dc, Loc($"Heat_Stroke_{state.Stroke}"), centre + new Vector(0, -radius * 0.32), strokeColor, Math.Max(15, radius * 0.2), center: true, bold: true);
        DrawText(dc, LocFormat("Heat_Dial_Angle", Number(state.Angle, "0")), centre + new Vector(0, radius * 0.22), Ink.Text, 11.5, center: true);
        DrawText(dc, LocFormat("Heat_Dial_Turn", state.Angle < 360 ? 1 : 2), centre + new Vector(0, radius * 0.42), Ink.Dim, 10.5, center: true);

        // Under the dial: the two valves now.
        var below = centre + new Vector(0, radius + thickness + 46);
        var intake = LocFormat("Heat_Dial_Valve", Loc("Heat_Part_IntakeValve"), Loc(state.IntakeLift > 0.02 ? "Heat_Open" : "Heat_Shut"));
        var exhaust = LocFormat("Heat_Dial_Valve", Loc("Heat_Part_ExhaustValve"), Loc(state.ExhaustLift > 0.02 ? "Heat_Open" : "Heat_Shut"));
        DrawText(dc, intake, below, state.IntakeLift > 0.02 ? Ink.Cyan : Ink.Dim, 11.5, center: true);
        DrawText(dc, exhaust, below + new Vector(0, 18), state.ExhaustLift > 0.02 ? Ink.Orange : Ink.Dim, 11.5, center: true);
    }

    private void DrawDialMark(PaintContext dc, Point centre, double radius, double thickness, double crank, Color color, string key)
    {
        var a = crank / 2 * Math.PI / 180;
        var inner = centre + new Vector(Math.Sin(a) * (radius - (thickness / 2) - 2), -Math.Cos(a) * (radius - (thickness / 2) - 2));
        dc.DrawEllipse(FadedBrush(color, 0.95), null, inner, 3.2, 3.2);
        var label = centre + new Vector(Math.Sin(a) * (radius - thickness - 16), -Math.Cos(a) * (radius - thickness - 16));
        var drawn = DrawText(dc, Loc(key), label, color, 10, center: true);
        _targets.Add("spark", inner, 7);
        AddBoxTarget("spark", drawn);
    }

    private void DrawDialTick(PaintContext dc, Point centre, double radius, double thickness, double crank, Color color)
    {
        var a = EngineModel.Wrap(crank) / 2 * Math.PI / 180;
        var direction = new Vector(Math.Sin(a), -Math.Cos(a));
        dc.DrawLine(FadedPen(color, 0.9, 1.6), centre + (direction * (radius + (thickness / 2) + 1)), centre + (direction * (radius + (thickness / 2) + 7)));
    }

    /// <summary>An arc of a circle from <paramref name="from"/>° to <paramref name="to"/>°, clockwise from the top.</summary>
    private static StreamGeometry Arc(Point centre, double radius, double from, double to)
    {
        var points = new List<Point>();
        const int steps = 40;
        for (var i = 0; i <= steps; i++)
        {
            var a = (from + ((to - from) * i / steps)) * Math.PI / 180;
            points.Add(centre + new Vector(Math.Sin(a) * radius, -Math.Cos(a) * radius));
        }

        return Line(points);
    }

    #endregion
}
