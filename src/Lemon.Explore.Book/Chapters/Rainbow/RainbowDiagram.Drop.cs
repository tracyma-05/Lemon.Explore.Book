using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Rainbow;

public sealed partial class RainbowDiagram
{
    /// <summary>Rays in the sheet across the drop, per bow.</summary>
    private const int SheetRays = 22;

    /// <summary>The colour the sheet of rays is drawn in for white light.</summary>
    private static readonly Color SheetColour = Color.FromRgb(0xFF, 0xE6, 0xA8);

    /// <summary>The drop's centre and radius, pixels: right of the middle, high enough for the light coming out below.</summary>
    private static (Point Centre, double Radius) DropLayout(Rect stage) =>
        (new Point(stage.Left + (stage.Width * 0.62), stage.Top + (stage.Height * 0.33)), Math.Min(stage.Height * 0.28, stage.Width * 0.18));

    /// <summary>The bows followed, each with the side of the drop its ray enters (primary above the middle, secondary below).</summary>
    private IEnumerable<(int Order, double Sign)> Bows => Order switch
    {
        BowOrder.Secondary => [(2, -1.0)],
        BowOrder.Both => [(1, 1.0), (2, -1.0)],
        _ => [(1, 1.0)],
    };

    private void DrawDrop(PaintContext dc, Rect stage, double width, double height)
    {
        dc.DrawRectangle(Ink.AirBack, null, new Rect(0, 0, width, height));
        var (centre, radius) = DropLayout(stage);
        Point Map(Point p) => new(centre.X + (p.X * radius), centre.Y - (p.Y * radius));
        static Vector MapDirection(Vector v) => new(v.X, -v.Y);

        var white = Colour == LightColour.White;
        var middleNm = white ? RainbowOptics.YellowNm : RainbowOptics.Wavelength(Colour);
        var middleN = RainbowOptics.Index(OpticalMedium.Water, middleNm);
        var reach = radius * 3.2;
        var area = new Rect(stage.Left - 24, stage.Top - 8, stage.Width + 48, stage.Height + 16);

        // Sunlight from the left, and the Sun.
        var sun = new Point(stage.Left + 26, centre.Y);
        for (var y = centre.Y - (radius * 1.15); y <= centre.Y + (radius * 1.15); y += 15)
        {
            dc.DrawLine(SunbeamPen, new Point(sun.X + 30, y), new Point(centre.X - Math.Sqrt(Math.Max(0, (radius * radius) - Math.Pow(y - centre.Y, 2))), y));
        }

        dc.DrawEllipse(SunGlow, null, sun, 34, 34);
        dc.DrawEllipse(SunDisc, null, sun, 16, 16);
        var sunlight = _labels.Text(Loc("Rain_Diagram_Sunlight"), 11.5, Ink.Gold);
        dc.DrawText(sunlight, new Point(sun.X - (sunlight.Width / 2), sun.Y + 40));

        dc.PushClip(new RectangleGeometry(area));

        // The sheet: rays across the whole drop, crowding where they turn back.
        if (ShowSheet)
        {
            var sheetColour = white ? SheetColour : LightColor(middleNm);
            var pen = MakePen(new SolidColorBrush(WithAlpha(sheetColour, Order == BowOrder.Both ? 0.14 : 0.2)), 1);
            foreach (var (order, sign) in Bows)
            {
                // Line by line: one stroked geometry of crossing lines makes WPF merge every overlap, many times slower.
                for (var j = 0; j < SheetRays; j++)
                {
                    var path = RainbowOptics.TraceDrop(sign * (j + 0.5) / SheetRays, middleN, order);
                    for (var k = 0; k + 1 < path.Points.Count; k++) dc.DrawLine(pen, Map(path.Points[k]), Map(path.Points[k + 1]));
                    dc.DrawLine(pen, Map(path.ExitPoint), Map(path.ExitPoint) + (MapDirection(path.Exit) * reach));
                }

                // Where they crowd: the rainbow ray.
                var bow = RainbowOptics.TraceDrop(sign * RainbowOptics.RainbowImpact(order, middleN), middleN, order);
                var start = Map(bow.ExitPoint);
                var direction = MapDirection(bow.Exit);
                if (Glows("bowray")) dc.DrawLine(ExploreTargets.GlowPen(Now), start, start + (direction * reach));
                dc.DrawLine(MakePen(Solid(170, 0xFF, 0xD3, 0x6B), 1.4, dashed: true), start, start + (direction * reach));
                // Labelled on the far side from the Sun's rays, well along the line.
                var label = LocFormat(order == 1 ? "Rain_Diagram_Crowd" : "Rain_Diagram_CrowdSecondary",
                    Number(RainbowOptics.RainbowAngle(order, middleN), "0.0"));
                DrawPill(dc, area, label, start + (direction * radius * 1.7) + (Side(direction) * 70), Ink.Gold, id: "crowd");
            }
        }

        // The drop.
        dc.DrawEllipse(DropFill, DropRim, centre, radius, radius);
        dc.DrawEllipse(DropShine, null, centre + new Vector(-radius * 0.5, -radius * 0.62), radius * 0.1, radius * 0.06);
        _targets.Add("drop", centre, radius);

        // The rays followed, split into colours inside for white light.
        var first = true;
        foreach (var (order, sign) in Bows)
        {
            var height0 = sign * Math.Clamp(Impact, 0, 0.995);
            var middle = RainbowOptics.TraceDrop(height0, middleN, order);
            var entry = Map(middle.Entry);
            DrawRay(dc, new Point(sun.X + 30, entry.Y), entry, Colors.White, 1, 2.4);

            foreach (var nm in Wavelengths)
            {
                var n = RainbowOptics.Index(OpticalMedium.Water, nm);
                var path = RainbowOptics.TraceDrop(height0, n, order);
                var color = LightColor(nm);
                for (var k = 0; k + 1 < path.Points.Count; k++)
                {
                    DrawRay(dc, Map(path.Points[k]), Map(path.Points[k + 1]), color, white ? 0.55 : 0.9, white ? 1.6 : 2.2);
                }

                DrawRay(dc, Map(path.ExitPoint), Map(path.ExitPoint) + (MapDirection(path.Exit) * reach), color, white ? 0.7 : 1, white ? 1.6 : 2.2);
            }

            if (ShowLosses && first) DrawLosses(dc, area, middle, Map, radius);

            // The angle it comes out at, from the line back toward the Sun.
            var exit = Map(middle.ExitPoint);
            var outgoing = MapDirection(middle.Exit);
            dc.DrawLine(Ink.Guide, exit, exit + new Vector(-radius * 1.25, 0));
            var label = white
                ? LocFormat("Rain_Diagram_ExitWhite",
                    Number(RainbowOptics.ExitAngle(order, RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.RedNm), height0), "0.0"),
                    Number(RainbowOptics.ExitAngle(order, RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.VioletNm), height0), "0.0"))
                : $"{Number(RainbowOptics.ExitAngle(order, middleN, height0), "0.0")}°";
            if (outgoing.X < 0) DrawAngle(dc, exit, new Vector(-1, 0), outgoing, radius * 0.6, label, Ink.Gold, "exitangle");
            else DrawPill(dc, area, label, exit + (outgoing * 60), Ink.Gold, id: "exitangle");

            // Beside the light coming out, below it and past the angle's label (further along for the secondary, whose
            // light leaves from the top of the drop).
            DrawPill(dc, area, LocFormat("Rain_Diagram_Out", Percent(middle.ExitPower)),
                exit + (outgoing * radius * (order == 1 ? 0.9 : 1.9)) - (Side(outgoing) * 80), Ink.Text, id: "out");

            // In and inside: the angles at the surface.
            if (first && Math.Abs(height0) > 0.05)
            {
                Vector normal = entry - centre;
                var bent = Map(middle.Points[1]) - entry;
                var incidence = Math.Asin(Math.Abs(height0)) / Deg;
                DrawAngle(dc, entry, normal, new Vector(-1, 0), radius * 0.28, $"{Number(incidence, "0")}°", Ink.Cyan, "entry");
                DrawAngle(dc, entry, -normal, bent, radius * 0.36,
                    $"{Number(Math.Asin(Math.Abs(height0) / middleN) / Deg, "0")}°", Ink.Cyan, "entry");
                dc.DrawLine(Ink.Normal, entry + (normal / normal.Length * 40), entry - (normal / normal.Length * radius * 0.45));
            }

            var legs = new List<Leg> { new(new Point(sun.X + 30, entry.Y), entry, 1, 1) };
            for (var k = 0; k + 1 < middle.Points.Count; k++)
            {
                legs.Add(new Leg(Map(middle.Points[k]), Map(middle.Points[k + 1]), 1 / middleN, 1));
            }

            legs.Add(new Leg(exit, exit + (outgoing * reach), 1, 1));
            DrawPulses(dc, legs, white ? Colors.White : LightColor(middleNm));
            first = false;
        }

        dc.Pop();

        // The scale of things, below the drop on the side away from the light coming out.
        DrawPill(dc, stage, Loc("Rain_Diagram_DropSize"), new Point(centre.X + (radius * 0.9), centre.Y + radius + 18), Ink.Dim);
    }

    /// <summary>Square to a direction, toward its upper side on screen (for rays going down and left: up and left).</summary>
    private static Vector Side(Vector direction)
    {
        var side = new Vector(direction.Y, -direction.X);
        side = side.Normalize();
        return side.Y > 0 ? -side : side;
    }

    /// <summary>The light that leaves on the way: reflected off the outside, and out through the back at each bounce.</summary>
    private void DrawLosses(PaintContext dc, Rect area, DropPath path, Func<Point, Point> map, double radius)
    {
        foreach (var leak in path.Leaks)
        {
            var from = map(leak.At);
            var direction = new Vector(leak.Direction.X, -leak.Direction.Y);
            direction = direction.Normalize();
            var to = from + (direction * radius * 0.95);
            var alpha = Math.Clamp(0.25 + (0.75 * Math.Sqrt(leak.Power)), 0, 1);
            dc.DrawLine(MakePen(new SolidColorBrush(WithAlpha(Ink.Violet, alpha)), 1.6, dashed: true), from, to);
            DrawArrowHead(dc, new SolidColorBrush(WithAlpha(Ink.Violet, alpha)), to, direction, 8);
            DrawPill(dc, area, Percent(leak.Power), to + (direction * 16), Ink.Violet, 10.5, "losses");
        }
    }

    private static readonly IPen SunbeamPen = MakePen(Solid(46, 0xFF, 0xE6, 0xA8), 1);
    private static readonly IBrush SunDisc = Solid(255, 0xFF, 0xE9, 0xA8);

    private static readonly IBrush SunGlow = SpaceDrawing.RadialGradient(
            (Color.FromArgb(150, 0xFF, 0xD3, 0x6B), 0),
            (Color.FromArgb(0, 0xFF, 0xD3, 0x6B), 1));

    private static readonly IBrush DropFill = SpaceDrawing.RadialGradient(
            (Color.FromArgb(40, 0x40, 0x90, 0xD0), 0),
            (Color.FromArgb(70, 0x50, 0xA0, 0xE0), 0.8),
            (Color.FromArgb(120, 0x80, 0xC8, 0xFF), 1));

    private static readonly IPen DropRim = MakePen(Solid(200, 0xA8, 0xD8, 0xFF), 1.6);

    private static readonly IBrush DropShine = Solid(70, 255, 255, 255);
}
