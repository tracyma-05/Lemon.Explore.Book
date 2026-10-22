using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Oscillations;

public sealed partial class OscillatorDiagram
{
    /// <summary>The farthest the mass can be pulled from where it rests, metres.</summary>
    public const double MaxPull = 0.25;

    /// <summary>Half the height of the mass as drawn, pixels.</summary>
    private const double MassHalf = 23;

    /// <summary>Height of the plate the spring hangs from (with the motor, when shaken), pixels.</summary>
    private const double PlateHeight = 22;

    /// <summary>
    /// Where the spring hangs, how many pixels a metre is, how long the spring is at rest, the paper beside it and the
    /// centre of the circle; <see cref="HighestHeight"/> is as high as the mass can be lifted (the spring kept 8 cm long).
    /// </summary>
    private readonly record struct SpringFrame(double SpringX, double CeilingY, double Scale, double RestLength, Rect Paper,
        Point CircleCenter, double HighestHeight)
    {
        /// <summary>Top of the spring when the motor is still.</summary>
        public double TopY => CeilingY + PlateHeight;

        /// <summary>Height of the mass's centre when it rests.</summary>
        public double EquilibriumY => TopY + RestLength + MassHalf;

        public Rect MassRect(double height) =>
            new(SpringX - 30, TopY + RestLength - (height * Scale), 60, 2 * MassHalf);

        /// <summary>Left-right place on the paper of a moment <paramref name="ago"/> seconds back: the paper runs left.</summary>
        public double PaperX(double ago) => Paper.Right - (ago / TraceSeconds * Paper.Width);
    }

    /// <summary>Height of the top of the spring above its usual place, metres: it moves only while the motor shakes it.</summary>
    private double SupportHeight => Shake ? OscillatorPhysics.DriveAmplitude * Math.Sin(_drivePhase) : 0;

    private SpringFrame SpringLayout()
    {
        var width = ActualWidth;
        var height = ActualHeight;
        var springX = Math.Round(width * 0.52);
        const double ceiling = 180;
        var stretch = OscillatorPhysics.SpringStretch(Mass, Stiffness, Gravity);
        var rest = OscillatorPhysics.SpringNaturalLength + stretch;
        var available = PictureHeight - 110 - ceiling - PlateHeight - (2 * MassHalf) - 10;

        // Tall enough for the spring pulled all the way down, narrow enough for the circle and its labels on the right.
        var scale = Math.Clamp(Math.Min(available / (rest + MaxPull), (width - springX - 66 - 130) / (2 * MaxPull)), 40, 600);

        var equilibrium = ceiling + PlateHeight + (rest * scale) + MassHalf;
        var half = (MaxPull * scale) + 16;
        var top = Math.Max(ceiling + 8, equilibrium - half);
        var paper = new Rect(36, top, Math.Max(80, springX - 36 - 76), (equilibrium + half) - top);
        var circle = new Point(springX + 66 + (MaxPull * scale), equilibrium);
        return new SpringFrame(springX, ceiling, scale, rest * scale, paper, circle, rest - 0.08);
    }

    private void DrawSpring(PaintContext dc, double now)
    {
        var frame = SpringLayout();
        var omega = SpringOmega;
        var mass = frame.MassRect(_height);
        var massCenter = mass.Center();
        var support = SupportHeight * frame.Scale;

        DrawSpringPaper(dc, frame, now);

        // The beam, the plate (with the motor when shaken) and the place.
        DrawBeam(dc, new Point(frame.SpringX, frame.CeilingY), 200);
        var badge = _labels.Text(LocFormat("Osc_Diagram_PlaceBadge", Loc($"Osc_Place_{Place}"), Number(Gravity, "0.00")), 12, Ink.Text);
        DrawPlaceBadge(dc, new Point(frame.SpringX + 114 + (badge.Width / 2), frame.CeilingY - 8));
        var plate = new Rect(frame.SpringX - 22, frame.CeilingY - support, 44, PlateHeight);
        if (Shake) DrawMotor(dc, frame, plate);
        else dc.DrawRoundedRectangle(Ink.BeamFill, Ink.Beam, new Rect(frame.SpringX - 10, frame.CeilingY, 20, PlateHeight), 3, 3);

        // The spring's own length, and where the mass rests: mg/k lower.
        var natural = frame.TopY + (OscillatorPhysics.SpringNaturalLength * frame.Scale);
        var restTop = frame.TopY + frame.RestLength;
        var markX = frame.SpringX + 36;
        dc.DrawLine(Ink.Guide, new Point(frame.SpringX - 22, natural), new Point(markX + 10, natural));
        var naturalText = _labels.Get("Osc_Diagram_Unstretched", 11, Ink.Dim);
        dc.DrawText(naturalText, new Point(markX + 14, natural - (naturalText.Height / 2)));
        AddTextTarget("unstretched", naturalText, new Point(markX + 14, natural - (naturalText.Height / 2)));
        if (restTop - natural > 16)
        {
            DrawStretchBracket(dc, new Point(markX, natural), new Point(markX, restTop));
        }

        dc.DrawLine(Ink.EquilibriumLine, new Point(frame.Paper.Left, frame.EquilibriumY),
            new Point(frame.CircleCenter.X + (MaxPull * frame.Scale) + 8, frame.EquilibriumY));
        var restText = _labels.Get("Osc_Diagram_Equilibrium", 11, Ink.Gold);
        dc.DrawText(restText, new Point(frame.CircleCenter.X + (MaxPull * frame.Scale) + 12, frame.EquilibriumY - (restText.Height / 2)));
        AddTextTarget("rest", restText, new Point(frame.CircleCenter.X + (MaxPull * frame.Scale) + 12, frame.EquilibriumY - (restText.Height / 2)));

        DrawCoil(dc, new Point(frame.SpringX, frame.TopY - support), new Point(frame.SpringX, mass.Top));
        _targets.AddBox("spring", new Rect(new Point(frame.SpringX - 17, frame.TopY - support + 10), new Point(frame.SpringX + 17, Math.Max(frame.TopY - support + 12, mass.Top - 10))));
        DrawMass(dc, mass);
        _targets.AddBox("mass", mass);
        DrawReferenceCircle(dc, frame, massCenter, mass, omega);
        DrawScaleBar(dc, new Point(frame.CircleCenter.X + (MaxPull * frame.Scale) + 16, frame.EquilibriumY + (MaxPull * frame.Scale)), frame.Scale);

        if (ShowForces) DrawSpringForces(dc, frame, mass, support);
    }

    /// <summary>The motor: a plate that rides up and down on a turning wheel, its pin going round.</summary>
    private void DrawMotor(PaintContext dc, SpringFrame frame, Rect plate)
    {
        var wheel = new Point(frame.SpringX - 60, frame.CeilingY + 12);
        dc.DrawEllipse(Ink.BeamFill, Ink.Beam, wheel, 13, 13);
        var pin = wheel + new Vector(Math.Cos(_drivePhase) * 8, -Math.Sin(_drivePhase) * 8);
        dc.DrawLine(Ink.Rod, wheel, pin);
        dc.DrawEllipse(Ink.GoldBrush, null, pin, 3, 3);
        dc.DrawLine(Ink.Guide, pin, new Point(plate.Left, pin.Y));
        dc.DrawRoundedRectangle(Ink.BeamFill, MotorPen, plate, 3, 3);
        var text = _labels.Get("Osc_Diagram_Motor", 11, Ink.Gold);
        dc.DrawText(text, new Point(wheel.X - text.Width - 18, wheel.Y - (text.Height / 2)));
        AddTextTarget("motor", text, new Point(wheel.X - text.Width - 18, wheel.Y - (text.Height / 2)));
        _targets.Add("motor", wheel, 14);
    }

    /// <summary>A coiled spring between two points: the back of each turn drawn darker, behind the front.</summary>
    private static void DrawCoil(PaintContext dc, Point top, Point bottom)
    {
        const int turns = 16;
        const double halfWidth = 15;
        const double lead = 10;
        var length = bottom.Y - top.Y - (2 * lead);
        var back = new StreamGeometry();
        var front = new StreamGeometry();
        using (var backContext = back.Open())
        using (var frontContext = front.Open())
        {
            const int samples = turns * 24;
            var previous = new Point(top.X, top.Y + lead);
            for (var i = 1; i <= samples; i++)
            {
                var u = i / (double)samples;
                var phi = u * turns * 2 * Math.PI;
                var point = new Point(top.X + (Math.Sin(phi) * halfWidth), top.Y + lead + (u * length));
                var context = Math.Cos(phi - (Math.PI / 24)) >= 0 ? frontContext : backContext;
                context.BeginFigure(previous, false);
                context.LineTo(point);
                context.EndFigure(false);
                previous = point;
            }
        }
        dc.DrawLine(CoilPens.Front, top, new Point(top.X, top.Y + lead));
        dc.DrawGeometry(null, CoilPens.Back, back);
        dc.DrawGeometry(null, CoilPens.Front, front);
        dc.DrawLine(CoilPens.Front, new Point(bottom.X, bottom.Y - lead), bottom);
    }

    private void DrawMass(PaintContext dc, Rect mass)
    {
        dc.DrawRoundedRectangle(MassBrush, Ink.BallRim, mass, 6, 6);
        var text = _labels.Text($"{Number(Mass, "0.00")} kg", 12, Color.FromRgb(0x22, 0x18, 0x08), true);
        dc.DrawText(text, new Point(mass.X + ((mass.Width - text.Width) / 2), mass.Y + ((mass.Height - text.Height) / 2)));
    }

    /// <summary>
    /// The circle whose shadow moves like the mass: a point going round once a period, as far out as the mass swings,
    /// level with the mass all the time.
    /// </summary>
    private void DrawReferenceCircle(PaintContext dc, SpringFrame frame, Point massCenter, Rect mass, double omega)
    {
        var center = frame.CircleCenter;
        var amplitude = OscillatorPhysics.SpringAmplitude(_height, _velocity, omega) * frame.Scale;
        var dot = new Point(center.X - (_velocity / omega * frame.Scale), center.Y - (_height * frame.Scale));
        if (amplitude > 3)
        {
            dc.DrawEllipse(null, CirclePen, center, amplitude, amplitude);
            dc.DrawLine(Ink.Guide, center, dot);
        }

        dc.DrawEllipse(Ink.Pivot, null, center, 2.5, 2.5);
        dc.DrawLine(Ink.PenArm, new Point(mass.Right, massCenter.Y), dot);
        dc.DrawEllipse(Ink.GoldBrush, null, dot, 6, 6);

        var caption = _labels.Get("Osc_Diagram_Circle", 11, Ink.Dim);
        dc.DrawText(caption, new Point(center.X - (caption.Width / 2), center.Y + (MaxPull * frame.Scale) + 12));
        AddTextTarget("circle", caption, new Point(center.X - (caption.Width / 2), center.Y + (MaxPull * frame.Scale) + 12));
        _targets.Add("circle", center, Math.Max(12, amplitude));
    }

    /// <summary>Weight down and the spring's pull up, both on the mass, and what is left of them; and the velocity.</summary>
    private void DrawSpringForces(PaintContext dc, SpringFrame frame, Rect mass, double support)
    {
        const double pixelsPerNewton = 7;
        var weight = Mass * Gravity;
        var stretch = OscillatorPhysics.SpringStretch(Mass, Stiffness, Gravity) - _height + (support / frame.Scale);
        var springForce = Stiffness * stretch;
        var center = mass.Center();

        DrawArrow(dc, new Point(mass.Left - 14, center.Y), new Vector(0, Math.Min(170, weight * pixelsPerNewton)),
            Ink.WhiteBrush, Ink.Text, "Osc_Diagram_Weight");
        DrawArrow(dc, new Point(mass.Left - 14, center.Y), new Vector(0, -Math.Clamp(springForce * pixelsPerNewton, -170, 170)),
            Ink.GreenBrush, Ink.Green, "Osc_Diagram_SpringForce");

        var net = springForce - weight;
        DrawArrow(dc, new Point(mass.Right + 14, center.Y), new Vector(0, -Math.Clamp(net * pixelsPerNewton, -170, 170)),
            Ink.RedBrush, Ink.Red, Math.Abs(net * pixelsPerNewton) > 16 ? "Osc_Diagram_Net" : null);
        DrawArrow(dc, new Point(mass.Right + 40, center.Y), new Vector(0, -Math.Clamp(_velocity * 90, -150, 150)),
            Ink.CyanBrush, Ink.Cyan, Math.Abs(_velocity * 90) > 18 ? "Osc_Diagram_Velocity" : null);
    }

    /// <summary>The bracket from the spring's own length down to where it rests, labelled mg/k.</summary>
    private void DrawStretchBracket(PaintContext dc, Point a, Point b)
    {
        dc.DrawLine(Ink.Bracket, a, b);
        dc.DrawLine(Ink.Bracket, a - new Vector(4, 0), a + new Vector(4, 0));
        dc.DrawLine(Ink.Bracket, b - new Vector(4, 0), b + new Vector(4, 0));
        var stretch = OscillatorPhysics.SpringStretch(Mass, Stiffness, Gravity);
        var text = _labels.Text($"mg/k = {Number(stretch * 100, stretch < 0.1 ? "0.0" : "0")} cm", 11, Ink.Green, true);
        dc.DrawText(text, new Point(a.X + 8, ((a.Y + b.Y) / 2) - (text.Height / 2)));
        AddTextTarget("stretch", text, new Point(a.X + 8, ((a.Y + b.Y) / 2) - (text.Height / 2)));
    }

    /// <summary>A 10 cm bar standing upright, for the scale.</summary>
    private void DrawScaleBar(PaintContext dc, Point bottom, double scale)
    {
        var top = new Point(bottom.X, bottom.Y - (0.1 * scale));
        dc.DrawLine(Ink.Ruler, bottom, top);
        dc.DrawLine(Ink.Ruler, bottom - new Vector(4, 0), bottom + new Vector(4, 0));
        dc.DrawLine(Ink.Ruler, top - new Vector(4, 0), top + new Vector(4, 0));
        var text = _labels.Text("10 cm", 10.5, Ink.Dim);
        dc.DrawText(text, new Point(bottom.X + 7, ((top.Y + bottom.Y) / 2) - (text.Height / 2)));
    }

    /// <summary>The paper running left beside the mass, the pen on its right edge level with the mass.</summary>
    private void DrawSpringPaper(PaintContext dc, SpringFrame frame, double now)
    {
        var paper = frame.Paper;
        dc.DrawRoundedRectangle(Ink.Paper, Ink.PaperEdge, paper, 6, 6);
        _targets.AddBox("paper", paper);
        dc.PushClip(new RectangleGeometry(paper, 6, 6));

        for (var second = Math.Floor(_time); second > _time - TraceSeconds; second--)
        {
            var x = frame.PaperX(_time - second);
            dc.DrawLine(Ink.PaperGrid, new Point(x, paper.Top), new Point(x, paper.Bottom));
            if (second <= 0 || paper.Right - x < 20) continue;
            var text = _labels.Text($"{Number(second, "0")} s", 10, Ink.Dim);
            dc.DrawText(text, new Point(x + 3, paper.Bottom - text.Height - 2));
        }

        for (var i = 0; i < _turns.Count; i++)
        {
            var x = frame.PaperX(_time - _turns[i]);
            dc.DrawLine(Ink.Guide, new Point(x, paper.Top + 4), new Point(x, frame.EquilibriumY));
            if (i == 0) continue;
            var before = frame.PaperX(_time - _turns[i - 1]);
            DrawBracket(dc, new Point(before, paper.Top + 26), new Point(x, paper.Top + 26), _turns[i] - _turns[i - 1], vertical: false);
        }

        if (_trace.Count > 1)
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                for (var i = 0; i < _trace.Count; i++)
                {
                    var sample = _trace[i];
                    var point = new Point(frame.PaperX(_time - sample.Time), frame.EquilibriumY - (sample.Offset * frame.Scale));
                    if (i == 0) context.BeginFigure(point, false);
                    else context.LineTo(point);
                }

                context.EndFigure(false);
            }
            if (Glows("trace")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now), geometry);
            dc.DrawGeometry(null, Ink.Trace, geometry);
        }

        dc.Pop();

        var pen = new Point(paper.Right, frame.EquilibriumY - (_height * frame.Scale));
        dc.DrawLine(Ink.PenArm, pen, new Point(frame.SpringX - 30, pen.Y));
        dc.DrawEllipse(Ink.GoldBrush, null, pen, 4, 4);

        var caption = _labels.Get("Osc_Diagram_PaperSpring", 11, Ink.Dim);
        dc.DrawText(caption, new Point(paper.Left, paper.Bottom + 4));
    }

    private static readonly IPen MotorPen = MakePen(Solid(255, 0xFF, 0xD3, 0x6B), 1.2);

    private static readonly IPen CirclePen = MakePen(Solid(110, 0xFF, 0xD3, 0x6B), 1.2);

    private static readonly IBrush MassBrush = SpaceDrawing.LinearGradient(90,
            (Color.FromRgb(0xF3, 0xC9, 0x6B), 0),
            (Color.FromRgb(0xC8, 0x8E, 0x2E), 0.6),
            (Color.FromRgb(0x8A, 0x5A, 0x16), 1));

    private static class CoilPens
    {
        public static readonly IPen Front = MakePen(Solid(255, 0xC9, 0xD3, 0xE0), 2.2);
        public static readonly IPen Back = MakePen(Solid(150, 0x6E, 0x7A, 0x8A), 1.8);
    }
}
