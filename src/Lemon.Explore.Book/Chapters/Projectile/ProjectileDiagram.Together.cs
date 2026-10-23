using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Projectile;

public sealed partial class ProjectileDiagram
{
    /// <summary>Pixels the balls start right of the table's edge, so the dropped one falls clear of it.</summary>
    private const double EdgeGap = 12;

    /// <summary>A strobe interval for the falling scenes: few enough images (at most six) to read the spacing.</summary>
    internal static double TogetherStrobe(double duration)
    {
        foreach (var step in new[] { 0.1, 0.2, 0.25, 0.4, 0.5, 1 })
        {
            if (duration / step <= 6) return step;
        }

        return 2;
    }

    private double TogetherDuration(double gravity) => Scene == TogetherScene.MonkeyHunter
        ? ProjectilePhysics.MonkeyEnd(DartSpeed, gravity, MonkeyLetsGo).Time
        : ProjectilePhysics.FallTime(TowerHeight, gravity);

    private (double Across, double Up) TogetherExtent() => Scene == TogetherScene.MonkeyHunter
        ? (ProjectilePhysics.MonkeyDistance + 5, ProjectilePhysics.MonkeyHeight + 5)
        : (Math.Max(3, ThrowSpeed * ProjectilePhysics.FallTime(TowerHeight, Gravity)) + 1, Math.Max(1, TowerHeight));

    private void DrawTogether(PaintContext dc, View view)
    {
        if (Scene == TogetherScene.MonkeyHunter) DrawMonkeyHunter(dc, view);
        else DrawDropAndThrow(dc, view);
    }

    /// <summary>
    /// One ball dropped and one thrown sideways at the same moment from the same table: the strobe shows them at the
    /// same height every time, falling 1, 3, 5, 7… times as far in each step; they land together.
    /// </summary>
    private void DrawDropAndThrow(PaintContext dc, View view)
    {
        var gravity = Gravity;
        var height = TowerHeight;
        var speed = ThrowSpeed;
        var duration = ProjectilePhysics.FallTime(height, gravity);
        var time = Math.Min(_time, duration);
        var strobe = TogetherStrobe(duration);
        Point Q(double x, double y) => view.P(x, y) + new Vector(EdgeGap, 0);

        DrawCliff(dc, view, height, 46);

        // Where the thrown ball would go if nothing pulled it down: straight on, level.
        var start = Q(0, height);
        dc.DrawLine(Ink.Guide, start, Q(speed * duration, height));

        // The thrown ball's path so far.
        var path = new StreamGeometry();
        using (var context = path.Open())
        {
            context.BeginFigure(start, false);
            for (var i = 1; i <= 60; i++)
            {
                var t = time * i / 60;
                context.LineTo(Q(speed * t, height - ProjectilePhysics.FallDistance(t, gravity)));
            }

            context.EndFigure(false);
        }
        if (time > 0) dc.DrawGeometry(null, MakePen(Solid(200, 0x6F, 0xD8, 0xFF), 1.8), path);

        // The strobe: each pair at the same height, and the distance each falls in the step.
        var previous = start;
        for (var k = 1; k * strobe <= time + 1e-9; k++)
        {
            var t = k * strobe;
            var y = height - ProjectilePhysics.FallDistance(t, gravity);
            var dropped = Q(0, y);
            var thrown = Q(speed * t, y);
            var ghost = Q(speed * t, height);
            dc.DrawLine(Ink.Guide, dropped, thrown);
            dc.DrawLine(Ink.Guide, ghost, thrown);
            dc.DrawEllipse(null, MakePen(Solid(150, 0xEE, 0xF1, 0xF6), 1), ghost, 3.5, 3.5);
            DrawBall(dc, dropped, 7, Ink.Pink, 0.45);
            DrawBall(dc, thrown, 7, Ink.Cyan, 0.45);

            // 1, 3, 5, 7… on the table's side.
            if (dropped.Y - previous.Y >= 10)
            {
                var x = view.P(0, 0).X - 10;
                dc.DrawLine(Ink.Bracket, new Point(x, previous.Y), new Point(x, dropped.Y));
                dc.DrawLine(Ink.Bracket, new Point(x - 4, dropped.Y), new Point(x + 4, dropped.Y));
                var text = _labels.Text($"{(2 * k) - 1}", 11.5, Ink.Green, true);
                var textAt = new Point(x - text.Width - 6, ((previous.Y + dropped.Y) / 2) - (text.Height / 2));
                dc.DrawText(text, textAt);
                AddTextTarget("oddnumbers", text, textAt);
            }

            previous = dropped;
        }

        if (time > 0)
        {
            var steps = _labels.Text(LocFormat("Proj_Diagram_OddNumbers", Number(strobe, "0.##")), 11, Ink.Green);
            var stepsAt = new Point(start.X + 4, start.Y - steps.Height - 12);
            dc.DrawText(steps, stepsAt);
            AddTextTarget("oddnumbers", steps, stepsAt);
        }

        var y0 = height - ProjectilePhysics.FallDistance(time, gravity);
        var droppedNow = Q(0, y0);
        var thrownNow = Q(speed * time, y0);
        DrawBall(dc, droppedNow, 8, Ink.Pink);
        DrawBall(dc, thrownNow, 8, Ink.Cyan);
        _targets.Add("dropped", droppedNow, 11);
        _targets.Add("thrown", thrownNow, 11);
        if (time < duration)
        {
            var dropText = _labels.Get("Proj_Diagram_Dropped", 11, Ink.Pink, true);
            var throwText = _labels.Get("Proj_Diagram_Thrown", 11, Ink.Cyan, true);
            if (time <= 0)
            {
                dc.DrawText(dropText, new Point(droppedNow.X - (dropText.Width / 2), droppedNow.Y - dropText.Height - 30));
                dc.DrawText(throwText, new Point(droppedNow.X + 14, droppedNow.Y - throwText.Height - 10));
            }
            else
            {
                dc.DrawText(dropText, new Point(droppedNow.X + 12, droppedNow.Y - (dropText.Height / 2)));
                dc.DrawText(throwText, new Point(thrownNow.X + 12, thrownNow.Y - (throwText.Height / 2)));
            }
        }
        else
        {
            DrawPill(dc, view, LocFormat("Proj_Diagram_LandTogether", Number(duration, "0.00")),
                new Point((droppedNow.X + thrownNow.X) / 2, view.Origin.Y - 30), Ink.Gold, 12);
        }
    }

    /// <summary>
    /// A dart aimed straight at a monkey, who lets go the moment it is fired: both fall ½gt² below where they would be,
    /// so the dart meets the monkey whatever its speed (if it gets there before the ground does).
    /// </summary>
    private void DrawMonkeyHunter(PaintContext dc, View view)
    {
        var gravity = Gravity;
        var speed = DartSpeed;
        var letsGo = MonkeyLetsGo;
        var (end, outcome) = ProjectilePhysics.MonkeyEnd(speed, gravity, letsGo);
        var time = Math.Min(_time, end);
        var done = _time >= end - 1e-9;
        var aim = ProjectilePhysics.MonkeyAim * Deg;
        var direction = new Vector(Math.Cos(aim), -Math.Sin(aim));
        const double distance = ProjectilePhysics.MonkeyDistance;
        const double height = ProjectilePhysics.MonkeyHeight;

        var branch = view.P(distance, height) - new Vector(0, 24);
        DrawTree(dc, view, branch);

        // The aim: straight at the monkey.
        var launch = view.P(0, 0);
        var target = view.P(distance, height);
        dc.DrawLine(Ink.Guide, launch, target);
        var aimText = _labels.Get("Proj_Diagram_Aim", 11, Ink.Dim);
        var middle = launch + ((target - launch) * 0.42);
        var aimAt = new Point(middle.X - aimText.Width - 4, middle.Y - aimText.Height - 2);
        dc.DrawText(aimText, aimAt);
        AddTextTarget("aim", aimText, aimAt);

        // The dart's path so far, and where it would be without gravity.
        var dart = ProjectilePhysics.Dart(speed, gravity, time);
        if (time > 0)
        {
            var path = new StreamGeometry();
            using (var context = path.Open())
            {
                context.BeginFigure(launch, false);
                for (var i = 1; i <= 60; i++)
                {
                    context.LineTo(view.P(ProjectilePhysics.Dart(speed, gravity, time * i / 60)));
                }

                context.EndFigure(false);
            }
            dc.DrawGeometry(null, Ink.Path, path);
        }

        var fall = ProjectilePhysics.FallDistance(time, gravity);
        var ghost = view.P(dart.X, dart.Y + fall);
        var monkeyY = ProjectilePhysics.MonkeyY(gravity, time, letsGo);
        var monkey = view.P(distance, monkeyY);
        if (time > 0)
        {
            dc.DrawEllipse(null, MakePen(Solid(170, 0xEE, 0xF1, 0xF6), 1.2), ghost, 5, 5);
            DrawFallBracket(dc, ghost, view.P(dart), fall, left: true);
            if (letsGo && monkeyY > 0) DrawFallBracket(dc, target, monkey, fall, left: false);
        }

        DrawMonkey(dc, monkey, branch, holding: !letsGo || time <= 0);

        // The dart, nose along its velocity.
        var velocity = new Vector(dart.Vx, -dart.Vy);
        var nose = velocity.Length > 1e-6 ? velocity / velocity.Length : direction;
        var tip = view.P(dart);
        dc.DrawLine(MakePen(Ink.GoldBrush, 2.4), tip - (nose * 16), tip);
        DrawArrowHead(dc, Ink.GoldBrush, tip + (nose * 4), nose, 9);
        dc.DrawLine(MakePen(Ink.PinkBrush, 3), tip - (nose * 16), tip - (nose * 11));

        DrawBlowpipe(dc, launch, direction);
        _targets.Add("hunter", launch, 18);
        _targets.Add("monkey", monkey + new Vector(0, 6), 17);
        _targets.Add("dart", tip, 11);

        if (!done) return;
        switch (outcome)
        {
            case MonkeyOutcome.Hit:
                DrawBurst(dc, monkey);
                // Under the burst: above it is where the dart's "½gt²" bracket puts its label.
                DrawPill(dc, view, Loc("Proj_Diagram_Hit"), monkey + new Vector(0, 40), Ink.Gold, 12.5);
                break;

            case MonkeyOutcome.Short:
                DrawPill(dc, view, Loc("Proj_Diagram_Short"), view.P(dart.X, 0) - new Vector(0, 30), Ink.Red, 12);
                break;

            default:
                DrawPill(dc, view, LocFormat("Proj_Diagram_Below", Metres(fall)), monkey - new Vector(0, 50), Ink.Violet, 12);
                break;
        }
    }

    /// <summary>A bracket from where it would be to where it is, "½gt² = 2.3 m".</summary>
    private void DrawFallBracket(PaintContext dc, Point from, Point to, double fall, bool left)
    {
        if (to.Y - from.Y < 6) return;
        var x = from.X + (left ? -12 : 26);
        dc.DrawLine(Ink.Bracket, new Point(x, from.Y), new Point(x, to.Y));
        dc.DrawLine(Ink.Bracket, new Point(x - 4, from.Y), new Point(x + 4, from.Y));
        dc.DrawLine(Ink.Bracket, new Point(x - 4, to.Y), new Point(x + 4, to.Y));
        if (to.Y - from.Y < 16) return;
        var text = _labels.Text($"½gt² = {Metres(fall)}", 11, Ink.Green, true);
        var textAt = new Point(left ? x - text.Width - 6 : x + 6, ((from.Y + to.Y) / 2) - (text.Height / 2));
        dc.DrawText(text, textAt);
        AddTextTarget("fall", text, textAt);
    }

    /// <summary>A tree with a branch reaching left to <paramref name="branch"/>, where the monkey hangs.</summary>
    private static void DrawTree(PaintContext dc, View view, Point branch)
    {
        var trunkX = branch.X + 40;
        var ground = view.Origin.Y;
        var top = branch.Y - 60;
        dc.DrawGeometry(Ink.Trunk, null, Polygon(new Point(trunkX - 9, ground), new Point(trunkX - 5, top), new Point(trunkX + 5, top),
            new Point(trunkX + 9, ground)));
        dc.DrawLine(MakePen(Ink.Trunk, 6), new Point(trunkX, branch.Y + 6), new Point(branch.X - 14, branch.Y));
        foreach (var (dx, dy, r) in new[] { (0.0, -70.0, 42.0), (-38, -48, 30), (34, -46, 32), (-6, -36, 30) })
        {
            dc.DrawEllipse(Ink.Leaves, null, new Point(trunkX + dx, branch.Y + dy), r, r * 0.8);
        }
    }

    /// <summary>A cartoon monkey: hanging by an arm from the branch, or falling with its arms up.</summary>
    private static void DrawMonkey(PaintContext dc, Point center, Point branch, bool holding)
    {
        var arm = MakePen(Ink.MonkeyFur, 3.5);
        if (holding)
        {
            dc.DrawLine(arm, center + new Vector(-4, -8), new Point(branch.X, branch.Y + 2));
        }
        else
        {
            dc.DrawLine(arm, center + new Vector(-6, -8), center + new Vector(-12, -22));
            dc.DrawLine(arm, center + new Vector(6, -8), center + new Vector(12, -22));
        }

        dc.DrawEllipse(Ink.MonkeyFur, null, center + new Vector(0, 12), 9, 11);
        dc.DrawLine(MakePen(Ink.MonkeyFur, 3), center + new Vector(7, 18), center + new Vector(18, 10));
        dc.DrawEllipse(Ink.MonkeyFur, null, center + new Vector(-9, -1), 4, 4);
        dc.DrawEllipse(Ink.MonkeyFur, null, center + new Vector(9, -1), 4, 4);
        dc.DrawEllipse(Ink.MonkeyFur, null, center, 9, 9);
        dc.DrawEllipse(Ink.MonkeyFace, null, center + new Vector(0, 2), 6, 5);
        dc.DrawEllipse(Brushes.Black, null, center + new Vector(-2.5, 0), 1.2, 1.2);
        dc.DrawEllipse(Brushes.Black, null, center + new Vector(2.5, 0), 1.2, 1.2);
    }

    /// <summary>The hunter's blowpipe resting on a bank of earth, aimed along <paramref name="direction"/>.</summary>
    private static void DrawBlowpipe(PaintContext dc, Point foot, Vector direction)
    {
        dc.DrawLine(MakePen(Solid(255, 0x9A, 0xA3, 0xB0), 5), foot - (direction * 14), foot + (direction * 26));
        dc.DrawEllipse(Ink.Mound, null, foot + new Vector(0, 4), 18, 8);
        dc.DrawEllipse(Ink.GoldBrush, null, foot, 2.5, 2.5);
    }

    /// <summary>A little star burst where the dart meets the monkey.</summary>
    private static void DrawBurst(PaintContext dc, Point center)
    {
        var points = new Point[16];
        for (var i = 0; i < points.Length; i++)
        {
            var angle = i * Math.PI / 8;
            var radius = i % 2 == 0 ? 24 : 11;
            points[i] = center + new Vector(Math.Cos(angle) * radius, Math.Sin(angle) * radius);
        }

        dc.DrawGeometry(Solid(110, 0xFF, 0xD3, 0x6B), MakePen(Ink.GoldBrush, 1.5), Polygon(points));
    }
}
