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
/// Transmission mode. Along the bottom, the way from a power station to a town: a step-up transformer, a line of pylons
/// (its length squeezed), a substation stepping down, a pole-top transformer and the houses. The wires carry dots as
/// dense and fast as the current, glow as they heat up, and give off heat. Top right, the step-up transformer cut open:
/// two coils on one iron core, the field in the iron changing with the alternating current (or standing still on DC).
/// </summary>
public sealed partial class GridDiagram
{
    /// <summary>Cycles per second of the transformer's alternating current as drawn (slowed down from 50 Hz).</summary>
    private const double DrawnAcFrequency = 0.6;

    private void DrawTransmissionMode(PaintContext dc, double width, double height)
    {
        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
        var stage = Stage();
        var line = TransmissionModel.Send(Power, LineKv, Distance);

        var insetWidth = Math.Clamp(stage.Width * 0.44, 260, 500);
        var insetHeight = Math.Clamp(stage.Height * 0.42, 150, 270);
        var inset = new Rect(stage.Right - insetWidth - 24, stage.Top, insetWidth, insetHeight);
        var land = new Rect(stage.Left + 16, inset.Bottom + 14, Math.Max(200, stage.Width - 40), Math.Max(120, stage.Bottom - inset.Bottom - 14));

        DrawSummary(dc, new Rect(stage.Left + 28, stage.Top + 6, Math.Max(120, inset.Left - stage.Left - 48), insetHeight - 10), line);
        DrawLandscape(dc, land, line);
        DrawTransformer(dc, inset);
    }

    /// <summary>Top left: what is sent, what arrives and what warms the wires, as a bar.</summary>
    private void DrawSummary(PaintContext dc, Rect area, LineState line)
    {
        var y = area.Top + 10;
        DrawText(dc, LocFormat("Grid_Line_Sent", Number(Power, "0")), new Point(area.Left, y), Ink.Text, 14, bold: true);
        y += 26;
        var barWidth = Math.Min(area.Width, 340);
        var bar = new Rect(area.Left, y, barWidth, 16);
        dc.DrawRoundedRectangle(FadedBrush(Ink.Text, 0.08), Ink.Frame, bar, 4, 4);
        if (line.Impossible)
        {
            dc.DrawRoundedRectangle(FadedBrush(Ink.Red, 0.7), null, bar, 4, 4);
        }
        else
        {
            var arrived = bar.Width * (1 - line.LossFraction);
            dc.DrawRoundedRectangle(FadedBrush(Ink.Green, 0.75), null, new Rect(bar.Left, bar.Top, arrived, bar.Height), 4, 4);
            dc.DrawRectangle(FadedBrush(Ink.Orange, 0.9), null, new Rect(bar.Left + arrived, bar.Top, Math.Max(1.5, bar.Width - arrived), bar.Height));
        }

        y += 30;
        if (line.Impossible)
        {
            DrawText(dc, Loc("Grid_Line_Impossible"), new Point(area.Left, y), Ink.Red, 12.5, bold: true);
        }
        else
        {
            DrawText(dc, LocFormat("Grid_Line_Arrives", Number(line.Delivered, "0")), new Point(area.Left, y), Ink.Green, 12.5, bold: true);
            y += 20;
            DrawText(dc, LocFormat("Grid_Line_Lost", LossText(line.Loss), Number(line.LossFraction * 100, line.LossFraction < 0.1 ? "0.0" : "0")),
                new Point(area.Left, y), Ink.Orange, 12.5);
        }

        y += 22;
        if (line.Overloaded)
        {
            DrawText(dc, LocFormat("Grid_Line_Overloaded", Number(line.Current, "0"), Number(TransmissionModel.Ampacity, "0")), new Point(area.Left, y), Ink.Red, 12);
        }
    }

    private static string LossText(double megawatts) => Number(megawatts, megawatts < 10 ? "0.0" : "0");

    private void DrawLandscape(PaintContext dc, Rect area, LineState line)
    {
        var ground = area.Bottom - 16;
        dc.DrawRectangle(SpaceDrawing.LinearGradient(Color.FromRgb(0x24, 0x34, 0x2A), Color.FromRgb(0x12, 0x1A, 0x14), 90), null,
            new Rect(area.Left - 16, ground, area.Width + 40, area.Bottom - ground + 30));
        dc.DrawLine(FadedPen(Ink.Green, 0.35, 1), new Point(area.Left - 16, ground), new Point(area.Right + 24, ground));

        double X(double fraction) => area.Left + (area.Width * fraction);
        var towerHeight = Math.Clamp(area.Height * 0.62, 60, 170);
        var scale = towerHeight / 120;

        // The power station and its step-up yard.
        var station = DrawStation(dc, new Point(X(0.055), ground), scale);
        var stepUp = DrawYard(dc, new Point(X(0.175), ground), scale, "stepup", 1.0);
        var downYard = DrawYard(dc, new Point(X(0.795), ground), scale * 0.85, "substation", 0.85);

        // The pylons and the three wires hanging between them.
        var pylonX = new[] { X(0.26), X(0.42), X(0.58), X(0.72) };
        var arms = pylonX.Select(x => DrawPylon(dc, new Point(x, ground), towerHeight)).ToList();
        var starts = stepUp.Outputs;
        var ends = downYard.Inputs;
        var wires = new List<List<Point>>();
        for (var phase = 0; phase < 3; phase++)
        {
            var points = new List<Point> { starts[phase] };
            foreach (var arm in arms) points.Add(arm[phase]);
            points.Add(ends[phase]);
            wires.Add(Sagged(points, towerHeight * 0.08));
        }

        var glow = TransmissionModel.Glow(line.Current);
        var lineGlow = Glows("line") ? ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 9) : null;
        foreach (var wire in wires)
        {
            var geometry = Line(wire);
            if (lineGlow is not null) dc.DrawGeometry(null, lineGlow, geometry);
            dc.DrawGeometry(null, FadedPen(Color.FromRgb(0x9A, 0xA2, 0xAE), 0.9, 1.4), geometry);
            if (glow > 0.12)
            {
                var hot = glow < 1
                    ? Lerp(Color.FromRgb(0xC8, 0x3A, 0x2A), Color.FromRgb(0xFF, 0x9A, 0x3A), glow)
                    : Lerp(Color.FromRgb(0xFF, 0x9A, 0x3A), Color.FromRgb(0xFF, 0xF4, 0xC8), Math.Min(1, glow - 1));
                dc.DrawGeometry(null, FadedPen(Quantize(hot), Math.Clamp(glow * 0.8, 0.2, 0.9), 2 + Math.Min(4, glow * 2), flat: true), geometry);
            }
        }

        // Dots as dense and as fast as the current.
        var amps = line.Current;
        var spacing = Math.Clamp(60 / Math.Max(0.1, amps / 1000), 7, 70);
        var speed = Math.Clamp(amps / 1000 * 45, 10, 260);
        var dot = FadedBrush(Ink.Gold, 0.9);
        foreach (var wire in wires)
        {
            var total = 0.0;
            for (var i = 1; i < wire.Count; i++) total += ((Vector)(wire[i] - wire[i - 1])).Length;
            var count = (int)(total / spacing);
            var offset = Frac(_t * speed / spacing);
            for (var i = 0; i < count; i++) dc.DrawEllipse(dot, null, Along(wire, (i + offset) * spacing / total), 1.7, 1.7);
        }

        // Heat rising off the wires, more the more is lost.
        var squiggles = (int)Math.Clamp(Math.Round(Math.Sqrt(line.LossFraction) * 30), 0, 14);
        for (var i = 0; i < squiggles; i++)
        {
            var wire = wires[i % 3];
            var at = Along(wire, 0.12 + (0.76 * Frac(i * 0.6180339887)));
            DrawHeatSquiggle(dc, at - new Vector(0, 4), 18 + (10 * Frac(i * 0.7548776662)), Ink.Orange, Math.Clamp(0.35 + line.LossFraction, 0.35, 0.9), i);
        }

        // The squeezed distance between the middle pylons.
        var gapCenter = new Point((pylonX[1] + pylonX[2]) / 2, ground - (towerHeight * 0.32));
        DrawText(dc, LocFormat("Grid_Line_Distance", Number(Distance, "0")), gapCenter, Ink.Dim, 11.5, center: true);

        // The pole-top transformer and the town.
        var pole = DrawDistributionPole(dc, new Point(X(0.885), ground), scale, downYard.Outputs[1]);
        var town = DrawTown(dc, new Rect(X(0.915), ground - (towerHeight * 0.55), Math.Max(40, area.Right - X(0.915)), towerHeight * 0.55), line, pole);

        // Their labels: the voltage at each step on the way down.
        if (!ShowLabels) return;
        var top = ground - towerHeight - 26;
        DrawTargetPill(dc, LocFormat("Grid_Line_StationLabel", Number(TransmissionModel.GeneratorKv, "0")), new Point(station.X, station.Y - 46), Ink.Text, "station");
        DrawTargetPill(dc, LocFormat("Grid_Line_StepUpLabel", Number(TransmissionModel.GeneratorKv, "0"), KvText(LineKv)), new Point(stepUp.Top.X + 24, stepUp.Top.Y - 8), Ink.Gold, "stepup");
        DrawTargetPill(dc, LocFormat("Grid_Line_LineLabel", KvText(LineKv), Number(line.Current, "0")),
            new Point((pylonX[0] + pylonX[1]) / 2, top), line.Overloaded ? Ink.Red : Ink.Text, "line");
        if (squiggles > 0)
        {
            DrawTargetPill(dc, LocFormat("Grid_Line_HeatLabel", LossText(line.Loss)), new Point((pylonX[2] + pylonX[3]) / 2, top), Ink.Orange, "heat");
        }

        var ladder = TransmissionModel.StepsDown(LineKv).Where(v => v >= 10).Select(KvText).Prepend(KvText(LineKv));
        DrawTargetPill(dc, string.Join(" → ", ladder), new Point(downYard.Top.X - 30, downYard.Top.Y - 40), Ink.Cyan, "substation");
        DrawTargetPill(dc, Loc("Grid_Line_PoleLabel"), new Point(pole.X - 24, pole.Y - 14), Ink.Cyan, "pole", 11);
        DrawTargetPill(dc, Loc("Grid_Obj_Town"), new Point(town.X, town.Y - 18), Ink.Green, "town", 11);
    }

    private static string KvText(double kv) => kv < 1 ? $"{Number(kv * 1000, "0")} V" : $"{Number(kv, "0")} kV";

    /// <summary>Points along wires hung between the given points, each span sagging by <paramref name="sag"/>.</summary>
    private static List<Point> Sagged(IReadOnlyList<Point> supports, double sag)
    {
        var points = new List<Point>();
        for (var i = 1; i < supports.Count; i++)
        {
            var a = supports[i - 1];
            var b = supports[i];
            var span = Math.Abs(b.X - a.X);
            var depth = sag * Math.Clamp(span / 160, 0.25, 1.4);
            for (var k = i == 1 ? 0 : 1; k <= 12; k++)
            {
                var t = k / 12.0;
                points.Add(new Point(Mix(a.X, b.X, t), Mix(a.Y, b.Y, t) + (4 * depth * t * (1 - t))));
            }
        }

        return points;
    }

    /// <summary>A coal-fired power station: a cooling tower with its steam, a hall and a chimney. Returns its top centre.</summary>
    private Point DrawStation(PaintContext dc, Point foot, double scale)
    {
        var w = 70 * scale;
        var h = 92 * scale;
        var tower = new StreamGeometry();
        using (var context = tower.Open())
        {
            var left = foot.X - (w * 0.55);
            context.BeginFigure(new Point(left, foot.Y), true);
            context.QuadraticBezierTo(new Point(left + (w * 0.22), foot.Y - (h * 0.6)), new Point(left + (w * 0.1), foot.Y - h));
            context.LineTo(new Point(left + (w * 0.48), foot.Y - h));
            context.QuadraticBezierTo(new Point(left + (w * 0.36), foot.Y - (h * 0.6)), new Point(left + (w * 0.58), foot.Y));

            context.EndFigure(true);
        }
        dc.DrawGeometry(SpaceDrawing.LinearGradient(Color.FromRgb(0xA8, 0xAE, 0xB8), Color.FromRgb(0x6A, 0x70, 0x7A), 0), Ink.Edge, tower);

        // Steam drifting up from the tower.
        for (var i = 0; i < 5; i++)
        {
            var age = Frac((_t * 0.12) + (i / 5.0));
            var at = new Point(foot.X - (w * 0.26) + (age * 18 * scale), foot.Y - h - (age * 40 * scale));
            var r = (8 + (age * 16)) * scale;
            dc.DrawEllipse(FadedBrush(Ink.Text, 0.28 * (1 - age)), null, at, r, r * 0.8);
        }

        var hall = new Rect(foot.X + (w * 0.05), foot.Y - (h * 0.42), w * 0.75, h * 0.42);
        dc.DrawRectangle(Ink.MetalDark, Ink.Edge, hall);
        for (var i = 0; i < 3; i++)
        {
            dc.DrawRectangle(FadedBrush(Ink.Gold, 0.6), null, new Rect(hall.Left + (hall.Width * (0.12 + (0.28 * i))), hall.Top + (hall.Height * 0.3), hall.Width * 0.14, hall.Height * 0.25));
        }

        var chimney = new Rect(hall.Right - (w * 0.14), foot.Y - (h * 1.05), w * 0.09, h * 0.63);
        dc.DrawRectangle(Ink.Metal, Ink.Edge, chimney);
        dc.DrawRectangle(FadedBrush(Ink.Red, 0.8), null, new Rect(chimney.Left, chimney.Top + 3, chimney.Width, 3));

        var box = new Rect(foot.X - (w * 0.6), foot.Y - h - 4, w * 1.45, h + 4);
        if (Glows("station")) dc.DrawRoundedRectangle(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 5), box, 8, 8);
        _targets.AddBox("station", box);
        return new Point(foot.X + (w * 0.1), foot.Y - h - 6);
    }

    /// <summary>
    /// A substation yard: transformers with insulators on top behind a fence. Returns its top centre and where the three
    /// phases leave (to the right) and arrive (from the left), at the height the line's wires hang.
    /// </summary>
    private (Point Top, Point[] Inputs, Point[] Outputs) DrawYard(PaintContext dc, Point foot, double scale, string id, double size)
    {
        var w = 64 * scale;
        var h = 44 * scale;
        var body = new Rect(foot.X - (w / 2), foot.Y - h, w, h * 0.72);
        dc.DrawRectangle(SpaceDrawing.LinearGradient(Color.FromRgb(0x7A, 0x8A, 0x7A), Color.FromRgb(0x4A, 0x56, 0x4A), 90), Ink.Edge, body);
        for (var i = 1; i < 5; i++) dc.DrawLine(FadedPen(Ink.Text, 0.15, 1), new Point(body.Left + (body.Width * i / 5), body.Top + 3), new Point(body.Left + (body.Width * i / 5), body.Bottom - 3));

        // Bushings: stacks of insulator discs on top, three in, three out.
        var inputs = new Point[3];
        var outputs = new Point[3];
        for (var k = 0; k < 6; k++)
        {
            var x = body.Left + (body.Width * (0.1 + (0.16 * k)));
            var tall = (k < 3 ? 26 : 34) * scale * size;
            for (var d = 0; d < 5; d++)
            {
                var y = body.Top - (tall * (d + 0.5) / 5);
                dc.DrawEllipse(FadedBrush(Color.FromRgb(0xC8, 0xD2, 0xDE), 0.9), null, new Point(x, y), 3.4 * scale, 1.6 * scale);
            }

            var tip = new Point(x, body.Top - tall);
            if (k < 3) inputs[k] = tip;
            else outputs[k - 3] = tip;
        }

        // The fence.
        var fence = new Rect(foot.X - (w * 0.7), foot.Y - (h * 0.32), w * 1.4, h * 0.32);
        for (var x = fence.Left; x <= fence.Right; x += 5) dc.DrawLine(FadedPen(Ink.Dim, 0.4, 1), new Point(x, fence.Top), new Point(x, fence.Bottom));
        dc.DrawLine(FadedPen(Ink.Dim, 0.5, 1), fence.TopLeft, fence.TopRight);

        var box = new Rect(foot.X - (w * 0.72), foot.Y - h - (36 * scale * size), w * 1.44, h + (36 * scale * size));
        if (Glows(id)) dc.DrawRoundedRectangle(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 5), box, 8, 8);
        _targets.AddBox(id, box);
        return (new Point(foot.X, box.Top), inputs, outputs);
    }

    /// <summary>A lattice pylon; returns where its three wires hang (left arm, right arm, top arm).</summary>
    private Point[] DrawPylon(PaintContext dc, Point foot, double height)
    {
        var pen = FadedPen(Color.FromRgb(0xA8, 0xB0, 0xBC), 0.85, 1.3);
        var thin = FadedPen(Color.FromRgb(0xA8, 0xB0, 0xBC), 0.5, 0.8);
        var halfBase = height * 0.16;
        var halfTop = height * 0.05;
        var top = foot.Y - height;
        var bl = new Point(foot.X - halfBase, foot.Y);
        var br = new Point(foot.X + halfBase, foot.Y);
        var tl = new Point(foot.X - halfTop, top + (height * 0.08));
        var tr = new Point(foot.X + halfTop, top + (height * 0.08));
        dc.DrawLine(pen, bl, tl);
        dc.DrawLine(pen, br, tr);

        // Cross-bracing up the legs.
        for (var i = 0; i < 6; i++)
        {
            var a = i / 6.0;
            var b = (i + 1) / 6.0;
            dc.DrawLine(thin, Mix(bl, tl, a), Mix(br, tr, b));
            dc.DrawLine(thin, Mix(br, tr, a), Mix(bl, tl, b));
        }

        // Two cross-arms and a peak, insulators hanging from them.
        var armY = top + (height * 0.3);
        var arm2Y = top + (height * 0.1);
        var reach = height * 0.3;
        dc.DrawLine(pen, new Point(foot.X - reach, armY), new Point(foot.X + reach, armY));
        dc.DrawLine(pen, new Point(foot.X - (reach * 0.5), arm2Y), new Point(foot.X + (reach * 0.5), arm2Y));
        dc.DrawLine(pen, tl, new Point(foot.X, top));
        dc.DrawLine(pen, tr, new Point(foot.X, top));
        var insulator = height * 0.08;
        var hang = new[]
        {
            new Point(foot.X - (reach * 0.92), armY),
            new Point(foot.X + (reach * 0.92), armY),
            new Point(foot.X + (reach * 0.45), arm2Y),
        };
        var points = new Point[3];
        for (var k = 0; k < 3; k++)
        {
            dc.DrawLine(FadedPen(Color.FromRgb(0xC8, 0xD2, 0xDE), 0.9, 2.2), hang[k], hang[k] + new Vector(0, insulator));
            points[k] = hang[k] + new Vector(0, insulator);
        }

        if (Glows("pylon")) dc.DrawEllipse(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 5), new Point(foot.X, top + (height * 0.25)), height * 0.3, height * 0.3);
        _targets.Add("pylon", new Point(foot.X, top + (height * 0.45)), height * 0.25);
        return points;
    }

    /// <summary>A pole with its drum transformer, fed from the substation; returns the top of the pole.</summary>
    private Point DrawDistributionPole(PaintContext dc, Point foot, double scale, Point feed)
    {
        var height = 70 * scale;
        var top = new Point(foot.X, foot.Y - height);
        dc.DrawLine(MakePen(Solid(255, 0x8A, 0x6A, 0x4A), 4 * scale), foot, top);
        dc.DrawLine(FadedPen(Ink.Dim, 0.9, 2), top + new Vector(-12 * scale, 6), top + new Vector(12 * scale, 6));
        var drum = new Rect(foot.X + (3 * scale), top.Y + (16 * scale), 14 * scale, 20 * scale);
        dc.DrawRoundedRectangle(Ink.MetalDark, Ink.Edge, drum, 3, 3);

        // The 10 kV feed from the substation, and the low-voltage wires on to the houses.
        dc.DrawGeometry(null, FadedPen(Color.FromRgb(0x9A, 0xA2, 0xAE), 0.8, 1.2), Line(Sagged([feed, top + new Vector(-10 * scale, 6)], 10)));
        if (Glows("pole")) dc.DrawEllipse(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 5), drum.TopLeft + new Vector(drum.Width / 2, drum.Height / 2), 16 * scale, 16 * scale);
        _targets.Add("pole", new Point(drum.X + (drum.Width / 2), drum.Y + (drum.Height / 2)), 14 * scale);
        return top;
    }

    /// <summary>A few houses and flats, their windows lit by what arrives. Returns the top centre.</summary>
    private Point DrawTown(PaintContext dc, Rect area, LineState line, Point pole)
    {
        var lit = line.Impossible ? 0 : Math.Clamp(line.Delivered / Math.Max(1, Power), 0, 1);
        var widths = new[] { 0.3, 0.22, 0.34 };
        var heights = new[] { 0.55, 1.0, 0.7 };
        var x = area.Left;
        var feedEnd = pole;
        for (var i = 0; i < 3; i++)
        {
            var w = area.Width * widths[i];
            var h = area.Height * heights[i];
            var house = new Rect(x, area.Bottom - h, w - 3, h);
            dc.DrawRectangle(SpaceDrawing.LinearGradient(Color.FromRgb(0x3A, 0x40, 0x50), Color.FromRgb(0x24, 0x28, 0x34), 90), Ink.Edge, house);
            if (i != 1) dc.DrawGeometry(Solid(255, 0x5A, 0x3A, 0x34), Ink.Edge, Shape(new Point(house.Left - 2, house.Top), new Point(house.Left + (house.Width / 2), house.Top - (w * 0.4)), new Point(house.Right + 2, house.Top)));

            // Windows: lit in proportion to what arrives (with a little flicker in which ones).
            var rows = Math.Max(1, (int)(h / 14));
            var cols = Math.Max(1, (int)(w / 12));
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    var on = Frac(((r * 7) + (c * 13) + (i * 5)) * 0.6180339887) < lit * 0.92;
                    var window = new Rect(house.Left + 3 + (c * (house.Width - 4) / cols), house.Top + 4 + (r * (house.Height - 6) / rows), Math.Max(2, ((house.Width - 4) / cols) - 4), Math.Max(2, ((house.Height - 6) / rows) - 6));
                    dc.DrawRectangle(on ? FadedBrush(Ink.Gold, 0.85) : FadedBrush(Ink.Text, 0.08), null, window);
                }
            }

            if (i == 0) dc.DrawGeometry(null, FadedPen(Color.FromRgb(0x9A, 0xA2, 0xAE), 0.6, 1), Line(Sagged([feedEnd + new Vector(10, 6), new Point(house.Left + 4, house.Top + 2)], 6)));
            x += w;
        }

        var box = new Rect(area.Left - 4, area.Top - 6, area.Width + 4, area.Height + 6);
        if (Glows("town")) dc.DrawRoundedRectangle(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 5), box, 8, 8);
        _targets.AddBox("town", box);
        return new Point(area.Left + (area.Width / 2), area.Top - 4);
    }

    #region the transformer cut open

    private void DrawTransformer(PaintContext dc, Rect area)
    {
        dc.DrawRoundedRectangle(FadedBrush(Color.FromRgb(0x08, 0x0C, 0x14), 0.9), Ink.Frame, area, 12, 12);
        DrawText(dc, Loc("Grid_Tx_Caption"), new Point(area.Left + 14, area.Top + 14), Ink.Dim, 11.5);

        var ratio = TransmissionModel.StepUpRatio(LineKv);
        var wave = Math.Sin(2 * Math.PI * DrawnAcFrequency * _t);
        var flux = FeedDc ? 1 : wave;

        // The iron core: a square ring of thin sheets.
        var core = new Rect(area.Left + (area.Width * 0.27), area.Top + 34, area.Width * 0.46, area.Height - 74);
        var leg = Math.Clamp(Math.Min(core.Width * 0.18, core.Height * 0.24), 10, 40);
        var window = new Rect(core.Left + leg, core.Top + leg, Math.Max(4, core.Width - (2 * leg)), Math.Max(4, core.Height - (2 * leg)));
        var ring = new GeometryGroup { FillRule = FillRule.EvenOdd };
        ring.Children.Add(new RectangleGeometry(core));
        ring.Children.Add(new RectangleGeometry(window));
        dc.DrawGeometry(Ink.Iron, Ink.Edge, ring);
        for (var y = core.Top + 4; y < core.Bottom; y += 4)
        {
            if (y > window.Top && y < window.Bottom)
            {
                dc.DrawLine(Ink.Grid, new Point(core.Left, y), new Point(window.Left, y));
                dc.DrawLine(Ink.Grid, new Point(window.Right, y), new Point(core.Right, y));
            }
            else
            {
                dc.DrawLine(Ink.Grid, new Point(core.Left, y), new Point(core.Right, y));
            }
        }

        if (Glows("core")) dc.DrawRectangle(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 6), core);
        _targets.AddBox("core", new Rect(core.Left + leg, core.Top, window.Width, leg));

        // The field in the iron: arrows round the ring, as strong as it is, pointing the way it goes.
        var path = new[]
        {
            new Point(core.Left + (leg / 2), core.Top + (leg / 2)),
            new Point(core.Right - (leg / 2), core.Top + (leg / 2)),
            new Point(core.Right - (leg / 2), core.Bottom - (leg / 2)),
            new Point(core.Left + (leg / 2), core.Bottom - (leg / 2)),
            new Point(core.Left + (leg / 2), core.Top + (leg / 2)),
        };
        if (Math.Abs(flux) > 0.08)
        {
            var arrow = FadedBrush(Ink.Field, 0.25 + (0.7 * Math.Abs(flux)));
            for (var i = 0; i < 12; i++)
            {
                var s = (i + 0.5) / 12;
                var at = Along(path, s);
                Vector ahead = Along(path, Math.Min(1, s + 0.01)) - Along(path, Math.Max(0, s - 0.01));
                if (ahead.Length < 1e-6) continue;
                ahead = ahead.Normalize();
                DrawArrowHead(dc, arrow, at, ahead * Math.Sign(flux), 5 + (5 * Math.Abs(flux)));
            }
        }

        // The coils: few thick turns in, many thin turns out.
        var outTurns = (int)Math.Clamp(Math.Round(4 * ratio), 4, 26);
        var primary = new Rect(core.Left - 6, window.Top + 4, leg + 12, window.Height - 8);
        var secondary = new Rect(core.Right - leg - 6, window.Top + 2, leg + 12, window.Height - 4);
        DrawWinding(dc, primary, 4, 3.4, "primary");
        DrawWinding(dc, secondary, outTurns, outTurns > 14 ? 1.2 : 1.8, "secondary");

        // The supply into the first coil, and what comes out of the second.
        var inMid = primary.Top + (primary.Height / 2);
        var outMid = secondary.Top + (secondary.Height / 2);
        var supplyX = area.Left + (area.Width * 0.13);
        dc.DrawLine(Ink.Wire, new Point(primary.Left, primary.Top + 4), new Point(supplyX, primary.Top + 4));
        dc.DrawLine(Ink.Wire, new Point(primary.Left, primary.Bottom - 4), new Point(supplyX, primary.Bottom - 4));
        var outX = area.Right - (area.Width * 0.13);
        dc.DrawLine(Ink.Wire, new Point(secondary.Right, secondary.Top + 4), new Point(outX, secondary.Top + 4));
        dc.DrawLine(Ink.Wire, new Point(secondary.Right, secondary.Bottom - 4), new Point(outX, secondary.Bottom - 4));

        var supply = FeedDc ? LocFormat("Grid_Tx_SupplyDc", Number(TransmissionModel.GeneratorKv, "0")) : LocFormat("Grid_Tx_SupplyAc", Number(TransmissionModel.GeneratorKv, "0"));
        DrawTargetPill(dc, supply, new Point(supplyX, inMid), FeedDc ? Ink.Orange : Ink.Gold, "supply", 11);
        var output = FeedDc ? Loc("Grid_Tx_OutputNone") : LocFormat("Grid_Tx_Output", KvText(LineKv));
        DrawPill(dc, output, new Point(outX, outMid), FeedDc ? Ink.Dim : Ink.Gold, 11);

        // Little traces of the two voltages, the output's as many times taller as it has turns.
        var traceWidth = Math.Min(area.Width * 0.2, 90);
        DrawMiniWave(dc, new Rect(supplyX - (traceWidth / 2), inMid + 16, traceWidth, 22), FeedDc ? 1 : 0.3, FeedDc, Ink.Gold);
        DrawMiniWave(dc, new Rect(outX - (traceWidth / 2), outMid + 16, traceWidth, 22), FeedDc ? 0 : 1, false, Ink.Gold);

        var ratioText = LocFormat("Grid_Tx_Ratio", Number(ratio, ratio < 10 ? "0.#" : "0"));
        DrawText(dc, ratioText, new Point(area.Left + (area.Width / 2), area.Bottom - 18), Ink.Text, 11.5, center: true);
    }

    /// <summary>A coil wound round a leg of the core: front halves of the turns bright, back halves dark.</summary>
    private void DrawWinding(PaintContext dc, Rect area, int turns, double thickness, string id)
    {
        var back = FadedPen(Color.FromRgb(0x7A, 0x4A, 0x22), 0.9, thickness);
        var front = FadedPen(Ink.Copper, 1, thickness);
        var gap = area.Height / turns;
        for (var i = 0; i < turns; i++)
        {
            var y = area.Top + ((i + 0.5) * gap);
            dc.DrawLine(back, new Point(area.Left, y - (gap * 0.25)), new Point(area.Right, y + (gap * 0.25)));
        }

        for (var i = 0; i < turns; i++)
        {
            var y = area.Top + ((i + 0.5) * gap);
            dc.DrawLine(front, new Point(area.Left, y + (gap * 0.25)), new Point(area.Right, y - (gap * 0.25)));
        }

        if (Glows(id)) dc.DrawRoundedRectangle(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 6), area, 4, 4);
        AddBoxTarget(id, area);
    }

    /// <summary>A small wave of the given height (0…1 of the box), scrolling; flat for steady DC.</summary>
    private void DrawMiniWave(PaintContext dc, Rect box, double amplitude, bool steady, Color color)
    {
        dc.DrawLine(Ink.Grid, new Point(box.Left, box.Top + (box.Height / 2)), new Point(box.Right, box.Top + (box.Height / 2)));
        var points = new List<Point>();
        for (var i = 0; i <= 30; i++)
        {
            var x = i / 30.0;
            var v = steady ? amplitude : amplitude * Math.Sin((2 * Math.PI * ((x * 2) - (DrawnAcFrequency * _t))));
            points.Add(new Point(box.Left + (x * box.Width), box.Top + (box.Height / 2) - (v * box.Height / 2)));
        }

        dc.DrawGeometry(null, FadedPen(color, 0.9, 1.6), Line(points));
    }

    #endregion
}
