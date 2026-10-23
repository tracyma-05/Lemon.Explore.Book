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
    /// <summary>Length of the cannon's barrel, pixels; the ball leaves it from the wheel's axle.</summary>
    private const double BarrelLength = 30;

    private Vector AimDirection => new(Math.Cos(Angle * Deg), -Math.Sin(Angle * Deg));

    private void DrawLaunch(PaintContext dc, View view)
    {
        var flight = _flight;
        var time = _time;
        var now = flight.At(time);
        var landed = flight.Duration <= 0 || time >= flight.Duration - 1e-9;
        var launch = view.P(0, CliffHeight);

        DrawCliff(dc, view, CliffHeight, 58);
        if (KeepTrails) DrawTrails(dc, view);

        // The same shot without air, dashed, and where it would land.
        double? airlessX = null;
        if (_airless is { Duration: > 0 } airless)
        {
            dc.DrawGeometry(null, Ink.Airless, PathGeometry(view, airless, double.MaxValue));
            var mark = view.P(airless.Range, 0);
            DrawLandingMark(dc, mark, Ink.WhiteBrush);
            airlessX = mark.X;
            DrawPill(dc, view, LocFormat("Proj_Diagram_Airless", Metres(airless.Range)), mark - new Vector(0, 24), Ink.Text, id: "airless");
        }

        if (_dragging)
        {
            // Aiming: where this shot will go.
            dc.DrawGeometry(null, Ink.Preview, PathGeometry(view, flight, double.MaxValue));
        }
        else if (time > 0)
        {
            var path = PathGeometry(view, flight, time);
            if (Glows("path")) dc.DrawGeometry(null, ExploreTargets.GlowPen(Now), path);
            dc.DrawGeometry(null, Ink.Path, path);
        }

        var strobe = ProjectilePhysics.StrobeInterval(flight.Duration);
        if (ShowStrobe && !_dragging)
        {
            for (var k = 1; k * strobe < Math.Min(time, flight.Duration) - 1e-9; k++)
            {
                var image = flight.At(k * strobe);
                DrawProjectile(dc, view.P(image), new Vector(image.Vx, -image.Vy), 0.4);
                _targets.Add("strobe", view.P(image), 8);
            }
        }

        if (ShowShadows) DrawShadows(dc, view, flight, time, strobe);

        // The top of the flight, once passed.
        if (!_dragging && flight.PeakPoint.Time > 0 && time >= flight.PeakPoint.Time)
        {
            var peak = view.P(flight.PeakPoint);
            dc.DrawLine(Ink.Guide, peak, view.P(flight.PeakPoint.X, 0));
            DrawPill(dc, view, LocFormat("Proj_Diagram_Peak", Metres(flight.Peak)), peak - new Vector(0, 26), Ink.Gold, id: "peak");
        }

        if (landed && !_dragging && flight.Duration > 0)
        {
            var mark = view.P(flight.Range, 0);
            DrawLandingMark(dc, mark, Ink.GoldBrush);
            // Above the no-air mark's pill or a kept path's label when one lands near it.
            var crowded = (airlessX is { } x && Math.Abs(x - mark.X) < 150) ||
                          (KeepTrails && _trails.Any(t => Math.Abs(view.P(t.Flight.Range, 0).X - mark.X) < 60));
            var lift = crowded ? 50 : 24;
            DrawPill(dc, view, LocFormat("Proj_Diagram_Range", Metres(flight.Range)), mark - new Vector(0, lift), Ink.Gold, id: "range");
        }

        // The ball comes out of the barrel: under the cannon until it has left it.
        var ball = view.P(now);
        var inBarrel = ((Vector)(ball - launch)).Length < BarrelLength;
        if (inBarrel) DrawProjectile(dc, ball, new Vector(now.Vx, -now.Vy));
        DrawCannon(dc, launch);
        if (!inBarrel) DrawProjectile(dc, ball, new Vector(now.Vx, -now.Vy));
        _targets.Add("cannon", launch, 16);
        if (!inBarrel) _targets.Add("ball", ball, BallRadius + 3);

        // Before it fires (and while aiming): the launch arrow, as long as the speed.
        if (_dragging || time <= 0)
        {
            var from = launch + (AimDirection * BarrelLength);
            DrawArrow(dc, from, AimDirection * LaunchSpeed * ArrowPerSpeed, Ink.GoldBrush, Ink.Gold,
                $"{Number(LaunchSpeed, "0.#")} m/s · {Number(Angle, "0")}°", 2.6);
        }
        else if (ShowVectors && !landed)
        {
            DrawVectors(dc, ball, now, flight);
        }
    }

    /// <summary>A cannon on its wheel, the barrel at the launch angle, half sunk behind a low bank of earth.</summary>
    private void DrawCannon(PaintContext dc, Point axle)
    {
        var direction = AimDirection;
        var side = new Vector(-direction.Y, direction.X);
        var breech = axle - (direction * 8);
        var muzzle = axle + (direction * BarrelLength);
        dc.DrawGeometry(Ink.CannonBody, MakePen(Solid(200, 0x22, 0x26, 0x2E), 1),
            Polygon(breech + (side * 7), muzzle + (side * 5), muzzle - (side * 5), breech - (side * 7)));
        dc.DrawEllipse(Ink.CannonBody, null, breech, 7, 7);

        dc.DrawEllipse(Ink.Wheel, Ink.WheelRim, axle, 10, 10);
        for (var i = 0; i < 3; i++)
        {
            var spoke = new Vector(Math.Cos(i * Math.PI / 3), Math.Sin(i * Math.PI / 3)) * 9;
            dc.DrawLine(Ink.WheelRim, axle - spoke, axle + spoke);
        }

        dc.DrawEllipse(Ink.GoldBrush, null, axle, 2.5, 2.5);
    }

    /// <summary>
    /// The velocity, split into its part across (the same all the way without air) and its part up (shrinking by g
    /// every second), with the weight and, in air, the drag against the motion.
    /// </summary>
    private void DrawVectors(PaintContext dc, Point ball, FlightPoint now, Flight flight)
    {
        var across = new Vector(now.Vx * ArrowPerSpeed, 0);
        var up = new Vector(0, -now.Vy * ArrowPerSpeed);
        var radius = BallRadius;
        if (across.Length > 3 && up.Length > 3)
        {
            dc.DrawLine(Ink.Guide, ball + across, ball + across + up);
            dc.DrawLine(Ink.Guide, ball + up, ball + across + up);
        }

        DrawArrow(dc, ball, across, Ink.GreenBrush, Ink.Green, "vx", id: "vx");
        DrawArrow(dc, ball, up, Ink.CyanBrush, Ink.Cyan, "vy", id: "vy");
        DrawArrow(dc, ball, across + up, Ink.WhiteBrush, Ink.Text, "v", 2.6, "velocity");

        DrawArrow(dc, ball + new Vector(0, radius), new Vector(0, 40), Ink.GoldBrush, Ink.Gold, Loc("Proj_Diagram_Weight"), id: "weight");
        var share = flight.DragShare(now);
        var speed = now.Speed;
        if (share > 0.02 && speed > 1e-6)
        {
            var against = new Vector(-now.Vx, now.Vy) / speed;
            DrawArrow(dc, ball + (against * radius), against * Math.Min(120, 40 * share), Ink.RedBrush, Ink.Red,
                40 * share > 16 ? Loc("Proj_Diagram_Drag") : null, id: "drag");
        }
    }

    /// <summary>
    /// The ball's shadow on the ground (straight down) and on the post (straight across): one moves steadily, the
    /// other goes up and comes down like a ball thrown straight up. The strobe marks them too.
    /// </summary>
    private void DrawShadows(PaintContext dc, View view, Flight flight, double time, double strobe)
    {
        var now = flight.At(time);
        var ground = view.Origin.Y;
        var post = view.PostX;

        for (var k = 1; k * strobe < Math.Min(time, flight.Duration) - 1e-9; k++)
        {
            var image = flight.At(k * strobe);
            var x = view.P(image).X;
            var y = view.P(image).Y;
            dc.DrawLine(Ink.Bracket, new Point(x, ground - 4), new Point(x, ground + 4));
            dc.DrawLine(Ink.Bracket, new Point(post - 4, y), new Point(post + 4, y));
        }

        var ball = view.P(now);
        dc.DrawEllipse(Ink.ShadowBrush, null, new Point(ball.X, ground - 1), 8, 3);
        dc.DrawEllipse(Ink.GreenBrush, null, new Point(ball.X, ground), 3, 3);
        dc.DrawEllipse(Ink.CyanBrush, null, new Point(post, ball.Y), 4, 4);
        dc.DrawLine(Ink.Guide, ball, new Point(ball.X, ground));
        dc.DrawLine(Ink.Guide, ball, new Point(post, ball.Y));

        var groundKey = flight.Drag > 0 ? "Proj_Diagram_GroundShadowSlowing" : "Proj_Diagram_GroundShadow";
        var groundText = _labels.Get(groundKey, 11, Ink.Green);
        var groundAt = new Point(Math.Max(view.Origin.X + 40, ball.X - (groundText.Width / 2)), ground - groundText.Height - 6);
        dc.DrawText(groundText, groundAt);
        AddTextTarget("groundshadow", groundText, groundAt);
        var postText = _labels.Get("Proj_Diagram_PostShadow", 11, Ink.Cyan);
        var postAt = new Point(post + 8, Math.Max(view.Stage.Top + 2, view.P(0, flight.Peak).Y - postText.Height - 8));
        dc.DrawText(postText, postAt);
        AddTextTarget("postshadow", postText, postAt);
    }

    /// <summary>The shots kept from before, faint, each with its angle where it landed.</summary>
    private void DrawTrails(PaintContext dc, View view)
    {
        var labelled = new List<Rect>();
        foreach (var trail in _trails)
        {
            var color = trail.Color;
            dc.DrawGeometry(null, MakePen(Solid(130, color.R, color.G, color.B), 1.4), PathGeometry(view, trail.Flight, double.MaxValue));
            var mark = view.P(trail.Flight.Range, 0);
            DrawLandingMark(dc, mark, Solid(200, color.R, color.G, color.B));

            var text = _labels.Text($"{Number(trail.Angle, "0")}° · {Number(trail.Speed, "0.#")} m/s", 10.5, color);
            var rect = new Rect(mark.X - (text.Width / 2), mark.Y - text.Height - 8, text.Width, text.Height);
            while (labelled.Any(r => r.Intersects(rect))) rect = rect.Translate(new Vector(0, -(text.Height + 2)));
            labelled.Add(rect);
            dc.DrawText(text, rect.TopLeft);
        }
    }
}
