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
    /// <summary>The longest pendulum, metres; the drawing is scaled so it fits swung out to 90°.</summary>
    public const double MaxLength = 2.0;

    /// <summary>Where the pendulum hangs, how many pixels a metre is, and the sheet of paper beneath it.</summary>
    private readonly record struct PendulumFrame(Point Pivot, double Scale, Rect Paper)
    {
        public Point Bob(double angle, double length) =>
            new(Pivot.X + (Math.Sin(angle) * length * Scale), Pivot.Y + (Math.Cos(angle) * length * Scale));

        /// <summary>Height on the paper of a moment <paramref name="ago"/> seconds back: the paper runs down.</summary>
        public double PaperY(double ago) => Paper.Top + (ago / TraceSeconds * Paper.Height);
    }

    private PendulumFrame PendulumLayout()
    {
        var width = ActualWidth;
        var height = ActualHeight;
        var pivot = new Point(Math.Round(width * 0.6), 175);
        var bottom = PictureHeight - 96;
        var paper = Math.Clamp((bottom - pivot.Y - 40) * 0.32, 100, 170);
        var scale = Math.Min((Math.Min(pivot.X, width - pivot.X) - 40) / MaxLength, (bottom - paper - pivot.Y - 40) / MaxLength);
        scale = Math.Max(scale, 30);
        var paperTop = pivot.Y + (MaxLength * scale) + 34;
        var reach = (Math.Max(Length, 0.4) * scale) + 26;
        return new PendulumFrame(pivot, scale, new Rect(pivot.X - reach, paperTop, 2 * reach, Math.Max(40, bottom - paperTop)));
    }

    private void DrawPendulum(PaintContext dc, double now)
    {
        var frame = PendulumLayout();
        var length = Math.Max(0.05, Length);
        var pivot = frame.Pivot;
        var bob = frame.Bob(_angle, length);
        var radius = Math.Clamp(length * frame.Scale * 0.12, 7, 15);

        DrawRuler(dc, frame);
        DrawPaper(dc, frame, length, now);

        // The beam it hangs from, the place, and straight down.
        DrawBeam(dc, pivot, 180);
        var badge = _labels.Text(LocFormat("Osc_Diagram_PlaceBadge", Loc($"Osc_Place_{Place}"), Number(Gravity, "0.00")), 12, Ink.Text);
        DrawPlaceBadge(dc, new Point(pivot.X + 104 + (badge.Width / 2), pivot.Y - 8));
        dc.DrawLine(Ink.Guide, pivot, new Point(pivot.X, pivot.Y + (length * frame.Scale) + 24));

        // The formula twin, faint and dashed.
        if (ShowFormula)
        {
            var twin = frame.Bob(TwinAngle(_time), length);
            dc.DrawLine(Ink.TwinRod, pivot, twin);
            DrawBall(dc, twin, radius, Ink.Steel, 0.4);
            var twinLabel = _labels.Get("Osc_Diagram_Formula", 11, Ink.Dim);
            dc.DrawText(twinLabel, new Point(twin.X - (twinLabel.Width / 2), twin.Y + radius + 3));
            AddTextTarget("formula", twinLabel, new Point(twin.X - (twinLabel.Width / 2), twin.Y + radius + 3));
        }

        // The angle from straight down.
        if (Math.Abs(_angle) > 1.5 * Deg)
        {
            var arcRadius = Math.Min(54, length * frame.Scale * 0.55);
            var start = new Point(pivot.X, pivot.Y + arcRadius);
            var end = new Point(pivot.X + (Math.Sin(_angle) * arcRadius), pivot.Y + (Math.Cos(_angle) * arcRadius));
            var arc = new StreamGeometry();
            using (var context = arc.Open())
            {
                context.BeginFigure(start, false);
                context.ArcTo(end, new Size(arcRadius, arcRadius), 0, false,
                    _angle > 0 ? SweepDirection.CounterClockwise : SweepDirection.Clockwise);

                context.EndFigure(false);
            }
            dc.DrawGeometry(null, Ink.Arc, arc);
            var label = _labels.Text($"{Number(Math.Abs(_angle) / Deg, "0")}°", 12, Ink.Gold, true);
            var mid = _angle / 2;
            var at = new Point(pivot.X + (Math.Sin(mid) * (arcRadius + 14)), pivot.Y + (Math.Cos(mid) * (arcRadius + 14)));
            dc.DrawText(label, new Point(_angle > 0 ? at.X - 2 : at.X - label.Width + 2, at.Y - (label.Height / 2)));
            AddTextTarget("angle", label, new Point(_angle > 0 ? at.X - 2 : at.X - label.Width + 2, at.Y - (label.Height / 2)));
        }

        // Rod, its length, and the bob.
        dc.DrawLine(Ink.Rod, pivot, bob);
        dc.DrawEllipse(Ink.Pivot, null, pivot, 4, 4);
        var lengthText = _labels.Text($"L = {Number(length, "0.00")} m", 11.5, Ink.Dim);
        var middle = new Point((pivot.X + bob.X) / 2, (pivot.Y + bob.Y) / 2);
        var side = _angle >= 0 ? 1 : -1;
        var normal = new Vector(Math.Cos(_angle) * side, -Math.Sin(_angle) * side);
        if (length * frame.Scale > 70)
        {
            var place = middle + (normal * 12);
            dc.DrawText(lengthText, new Point(normal.X < 0 ? place.X - lengthText.Width : place.X, place.Y - (lengthText.Height / 2)));
            AddTextTarget("length", lengthText, new Point(normal.X < 0 ? place.X - lengthText.Width : place.X, place.Y - (lengthText.Height / 2)));
        }

        DrawBall(dc, bob, radius, Ink.Brass);
        _targets.Add("bob", bob, radius + 4);

        if (ShowForces) DrawPendulumForces(dc, bob, length, radius);
    }

    /// <summary>Weight straight down, the part of it pulling the bob back along its path, and the velocity.</summary>
    private void DrawPendulumForces(PaintContext dc, Point bob, double length, double radius)
    {
        var weight = Math.Max(10, 46 * Gravity / OscillatorPhysics.EarthGravity);
        var along = new Vector(Math.Cos(_angle), -Math.Sin(_angle));
        DrawArrow(dc, bob + new Vector(0, radius), new Vector(0, weight), Ink.WhiteBrush, Ink.Text, "Osc_Diagram_Weight");

        var pullBack = along * (-weight * Math.Sin(_angle));
        DrawArrow(dc, bob + (pullBack.Length > 1 ? pullBack / pullBack.Length * radius : default), pullBack, Ink.RedBrush, Ink.Red,
            pullBack.Length > 14 ? "Osc_Diagram_PullBack" : null);

        var speed = _angularVelocity * length;
        var velocity = along * Math.Clamp(speed * 26, -150, 150);
        DrawArrow(dc, bob + (velocity.Length > 1 ? velocity / velocity.Length * radius : default), velocity, Ink.CyanBrush, Ink.Cyan,
            velocity.Length > 18 ? "Osc_Diagram_Velocity" : null);
    }

    /// <summary>A beam with hatching above, <paramref name="width"/> wide and centred over <paramref name="pivot"/>.</summary>
    private static void DrawBeam(PaintContext dc, Point pivot, double width)
    {
        var rect = new Rect(pivot.X - (width / 2), pivot.Y - 14, width, 14);
        dc.DrawRectangle(Ink.BeamFill, Ink.Beam, rect);
        for (var x = rect.Left + 6; x < rect.Right; x += 10)
        {
            dc.DrawLine(Ink.Hatch, new Point(x, rect.Top), new Point(x - 8, rect.Top - 8));
        }
    }

    /// <summary>A metre rule down the left of the pendulum, every 10 cm, labelled every half metre.</summary>
    private void DrawRuler(PaintContext dc, PendulumFrame frame)
    {
        var x = frame.Pivot.X - (MaxLength * frame.Scale) - 22;
        dc.DrawLine(Ink.Ruler, new Point(x, frame.Pivot.Y), new Point(x, frame.Pivot.Y + (MaxLength * frame.Scale)));
        for (var i = 0; i <= MaxLength * 10 + 0.5; i++)
        {
            var y = frame.Pivot.Y + (i / 10.0 * frame.Scale);
            var major = i % 5 == 0;
            dc.DrawLine(Ink.Ruler, new Point(x, y), new Point(x + (major ? 10 : 5), y));
            if (!major || i == 0) continue;
            var text = _labels.Text($"{Number(i / 10.0, "0.#")} m", 10.5, Ink.Dim);
            dc.DrawText(text, new Point(x - text.Width - 5, y - (text.Height / 2)));
        }

        var length = frame.Pivot.Y + (Math.Max(0.05, Length) * frame.Scale);
        dc.DrawLine(Ink.PenArm, new Point(x, length), new Point(x + 14, length));
        DrawArrowHead(dc, Ink.GoldBrush, new Point(x + 1, length), new Vector(-1, 0), 8);
    }

    /// <summary>
    /// The paper running down under the pendulum, the pen on it following the bob from side to side: the swing over the
    /// last seconds, the formula twin dashed, and the time between turns on the right.
    /// </summary>
    private void DrawPaper(PaintContext dc, PendulumFrame frame, double length, double now)
    {
        var paper = frame.Paper;
        dc.DrawRoundedRectangle(Ink.Paper, Ink.PaperEdge, paper, 6, 6);
        _targets.AddBox("paper", paper);
        dc.PushClip(new RectangleGeometry(paper, 6, 6));

        // A line every second, moving down with the paper; the middle line under the pivot.
        dc.DrawLine(Ink.PaperCentre, new Point(frame.Pivot.X, paper.Top), new Point(frame.Pivot.X, paper.Bottom));
        for (var second = Math.Floor(_time); second > _time - TraceSeconds; second--)
        {
            var y = frame.PaperY(_time - second);
            dc.DrawLine(Ink.PaperGrid, new Point(paper.Left, y), new Point(paper.Right, y));
            if (second <= 0 || second % 2 != 0 || y - paper.Top < 14) continue;
            var text = _labels.Text($"{Number(second, "0")} s", 10, Ink.Dim);
            dc.DrawText(text, new Point(paper.Left + 6, y - text.Height));
        }

        // The swings timed between turns at the right.
        for (var i = 0; i < _turns.Count; i++)
        {
            var y = frame.PaperY(_time - _turns[i]);
            dc.DrawLine(Ink.Guide, new Point(frame.Pivot.X, y), new Point(paper.Right - 8, y));
            if (i == 0) continue;
            var above = frame.PaperY(_time - _turns[i - 1]);
            DrawBracket(dc, new Point(paper.Right - 10, y), new Point(paper.Right - 10, above), _turns[i] - _turns[i - 1], vertical: true);
        }

        if (ShowFormula) DrawTrace(dc, frame, length, twin: true, now);
        DrawTrace(dc, frame, length, twin: false, now);
        dc.Pop();

        // The pen at the top edge, under the bob.
        var bob = frame.Bob(_angle, length);
        var pen = new Point(bob.X, paper.Top);
        dc.DrawLine(Ink.PenArm, new Point(bob.X, bob.Y + 16), pen);
        dc.DrawEllipse(Ink.GoldBrush, null, pen, 4, 4);

        var caption = _labels.Get("Osc_Diagram_Paper", 11, Ink.Dim);
        dc.DrawText(caption, new Point(paper.Left, paper.Bottom + 4));
        var down = _labels.Get("Osc_Diagram_TimeDown", 11, Ink.Dim);
        dc.DrawText(down, new Point(paper.Right - down.Width, paper.Bottom + 4));
    }

    private void DrawTrace(PaintContext dc, PendulumFrame frame, double length, bool twin, double now)
    {
        if (_trace.Count < 2) return;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var i = 0; i < _trace.Count; i++)
            {
                var sample = _trace[i];
                var angle = twin ? sample.Twin : sample.Offset;
                var point = new Point(frame.Pivot.X + (Math.Sin(angle) * length * frame.Scale), frame.PaperY(_time - sample.Time));
                if (i == 0) context.BeginFigure(point, false);
                else context.LineTo(point);
            }

            context.EndFigure(false);
        }
        if (!twin && Glows("trace")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now), geometry);
        dc.DrawGeometry(null, twin ? Ink.TwinTrace : Ink.Trace, geometry);
    }

    /// <summary>A bracket between two turns, labelled with the seconds between them.</summary>
    private void DrawBracket(PaintContext dc, Point a, Point b, double seconds, bool vertical)
    {
        dc.DrawLine(Ink.Bracket, a, b);
        var tick = vertical ? new Vector(5, 0) : new Vector(0, 5);
        dc.DrawLine(Ink.Bracket, a - tick, a + tick);
        dc.DrawLine(Ink.Bracket, b - tick, b + tick);
        if (((Vector)(b - a)).Length < 26) return;

        var text = _labels.Text($"{Number(seconds, seconds < 10 ? "0.00" : "0.0")} s", 11, Ink.Green, true);
        var mid = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        dc.DrawText(text, vertical
            ? new Point(mid.X - text.Width - 7, mid.Y - (text.Height / 2))
            : new Point(mid.X - (text.Width / 2), mid.Y - text.Height - 6));
    }
}
