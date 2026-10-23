using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Projectile;

public sealed partial class ProjectileDiagram
{
    /// <summary>
    /// The fan: a shot every ten degrees from 15° to 75° at the same speed, and the one that goes the farthest when it
    /// is none of them (in air, or from up high).
    /// </summary>
    private void BuildFan(double gravity, double drag)
    {
        _fan.Clear();
        var best = ProjectilePhysics.BestAngle(LaunchSpeed, CliffHeight, gravity, drag);
        foreach (var angle in ProjectilePhysics.FanAngles)
        {
            _fan.Add(new FanPath(angle, ProjectilePhysics.Fly(LaunchSpeed, angle, CliffHeight, gravity, drag), AngleColor(angle),
                Math.Abs(angle - best) < 0.5));
        }

        if (!_fan.Any(p => p.IsBest))
        {
            _fan.Add(new FanPath(best, ProjectilePhysics.Fly(LaunchSpeed, best, CliffHeight, gravity, drag), Ink.Green, true));
        }
    }

    private void DrawBestAngle(PaintContext dc, View view)
    {
        var time = _time;
        var launch = view.P(0, CliffHeight);
        DrawCliff(dc, view, CliffHeight, 58);

        // Without air: the edge no throw at this speed gets past.
        if (Drag <= 0) DrawEnvelope(dc, view);

        // The best path last, so it is drawn over the others.
        foreach (var path in _fan.OrderBy(p => p.IsBest))
        {
            var color = path.Color;
            var thick = path.IsBest || Math.Abs(path.Angle - 45) < 0.01;
            if (time > 0)
            {
                dc.DrawGeometry(null, MakePen(Solid(230, color.R, color.G, color.B), thick ? 2.6 : 1.5), PathGeometry(view, path.Flight, time));
            }
        }

        // Where they landed, the angles that land together sharing a label.
        var marks = new List<(double X, string Text, Color Color, bool IsBest)>();
        foreach (var path in _fan.Where(p => time >= p.Flight.Duration).OrderBy(p => p.Flight.Range))
        {
            var mark = view.P(path.Flight.Range, 0);
            DrawLandingMark(dc, mark, Solid(255, path.Color.R, path.Color.G, path.Color.B));
            var text = $"{Number(path.Angle, "0")}°";
            if (marks.Count > 0 && Math.Abs(marks[^1].X - mark.X) < 8 && !path.IsBest && !marks[^1].IsBest)
            {
                marks[^1] = (marks[^1].X, $"{marks[^1].Text} · {text}", marks[^1].Color, false);
                continue;
            }

            if (!path.IsBest) marks.Add((mark.X, text, path.Color, false));
        }

        var placed = new List<Rect>();
        foreach (var (x, text, color, _) in marks)
        {
            var formatted = _labels.Text(text, 11, color, true);
            var rect = new Rect(x - (formatted.Width / 2), view.Origin.Y - formatted.Height - 10, formatted.Width, formatted.Height);
            while (placed.Any(r => r.Inflate(new Thickness(4, 0)).Intersects(rect))) rect = rect.Translate(new Vector(0, -(formatted.Height + 1)));
            placed.Add(rect);
            dc.DrawText(formatted, rect.TopLeft);
            AddTextTarget("landing", formatted, rect.TopLeft);
        }

        var best = _fan.FirstOrDefault(p => p.IsBest);
        if (best is not null && time >= best.Flight.Duration)
        {
            var mark = view.P(best.Flight.Range, 0);
            var top = placed.Count == 0 ? mark.Y : Math.Min(mark.Y, placed.Min(r => r.Top));
            DrawPill(dc, view, LocFormat("Proj_Diagram_Farthest", Number(best.Angle, "0"), Metres(best.Flight.Range)),
                new Point(mark.X, top - 18), Ink.Green, 12, "farthest");
        }

        // The balls, still flying or resting where they landed.
        foreach (var path in _fan.OrderBy(p => p.IsBest))
        {
            var now = path.Flight.At(time);
            DrawBall(dc, view.P(now), 6, path.Color);
        }

        DrawFanLauncher(dc, launch);
        _targets.Add("launcher", launch, 16);
    }

    /// <summary>The "safety parabola": every path at this speed touches it, none gets past it.</summary>
    private void DrawEnvelope(PaintContext dc, View view)
    {
        var speed = LaunchSpeed;
        var gravity = Gravity;
        var reach = ProjectilePhysics.IdealMaxRange(speed, CliffHeight, gravity);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var i = 0; i <= 120; i++)
            {
                var x = reach * i / 120;
                var point = view.P(x, Math.Max(0, ProjectilePhysics.Envelope(x, speed, CliffHeight, gravity)));
                if (i == 0) context.BeginFigure(point, false);
                else context.LineTo(point);
            }

            context.EndFigure(false);
        }
        if (Glows("envelope")) dc.DrawGeometry(null, ExploreTargets.GlowPen(Now), geometry);
        dc.DrawGeometry(null, Ink.Envelope, geometry);

        var x0 = reach * 0.72;
        var at = view.P(x0, ProjectilePhysics.Envelope(x0, speed, CliffHeight, gravity));
        var text = _labels.Get("Proj_Diagram_Envelope", 11, Ink.Violet);
        var textAt = new Point(at.X + 8, at.Y - text.Height - 2);
        dc.DrawText(text, textAt);
        AddTextTarget("envelope", text, textAt);
    }

    /// <summary>A launcher with a short barrel for each angle of the fan.</summary>
    private void DrawFanLauncher(PaintContext dc, Point axle)
    {
        foreach (var path in _fan)
        {
            var direction = new Vector(Math.Cos(path.Angle * Deg), -Math.Sin(path.Angle * Deg));
            dc.DrawLine(MakePen(Solid(220, path.Color.R, path.Color.G, path.Color.B), 3), axle, axle + (direction * 22));
        }

        dc.DrawEllipse(Ink.Wheel, Ink.WheelRim, axle, 9, 9);
        dc.DrawEllipse(Ink.GoldBrush, null, axle, 2.5, 2.5);
    }
}
