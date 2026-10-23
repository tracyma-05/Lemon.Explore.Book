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
/// Cycle mode: on the left a hot and a cold place with the machine between them and arrows as wide as the heat and work
/// that flow; on the right the gas's pressure against its volume going round the ideal (Carnot) cycle, clockwise as an
/// engine, anticlockwise as a fridge or heat pump.
/// </summary>
public sealed partial class HeatDiagram
{
    private static readonly Color[] StepColors =
    [
        Color.FromRgb(0xFF, 0x7A, 0x5A),
        Color.FromRgb(0xB3, 0x9D, 0xFF),
        Color.FromRgb(0x5A, 0xB0, 0xFF),
        Color.FromRgb(0xB3, 0x9D, 0xFF),
    ];

    private bool Forward => Direction == CycleDirection.Engine;

    /// <summary>The step's number in the order it happens: an engine starts with step 0, a fridge with step 3.</summary>
    internal static int StepNumber(int step, bool forward) => forward ? step + 1 : 4 - step;

    private void DrawCycleMode(PaintContext dc, double width, double height)
    {
        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
        var stage = Stage();
        var hot = Math.Max(Hot, Cold + 1);
        var cold = Cold;
        var state = CarnotModel.At(_phase, hot, cold);

        var split = stage.Left + (stage.Width * 0.45);
        var left = new Rect(stage.Left + 24, stage.Top + 4, Math.Max(120, split - stage.Left - 40), stage.Height - 8);
        var right = new Rect(split + 16, stage.Top + 4, Math.Max(160, stage.Right - split - 40), stage.Height - 8);
        DrawReservoirs(dc, left, state, hot, cold);
        DrawPvLoop(dc, right, state, hot, cold);
    }

    private void DrawReservoirs(PaintContext dc, Rect area, CarnotState state, double hot, double cold)
    {
        var boxHeight = Math.Clamp(area.Height * 0.16, 44, 90);
        var boxWidth = Math.Min(area.Width * 0.8, 340);
        var cx = area.Left + (area.Width * 0.48);
        var hotBox = new Rect(cx - (boxWidth / 2), area.Top, boxWidth, boxHeight);
        var coldBox = new Rect(cx - (boxWidth / 2), area.Bottom - boxHeight, boxWidth, boxHeight);

        var hotBrush = SpaceDrawing.LinearGradient(Color.FromRgb(0xC8, 0x40, 0x2A), Color.FromRgb(0x7A, 0x22, 0x18), 90);
        var coldBrush = SpaceDrawing.LinearGradient(Color.FromRgb(0x1E, 0x4E, 0x8E), Color.FromRgb(0x2E, 0x76, 0xC8), 90);
        dc.DrawRoundedRectangle(hotBrush, Ink.LightEdge, hotBox, 12, 12);
        dc.DrawRoundedRectangle(coldBrush, Ink.LightEdge, coldBox, 12, 12);
        AddBoxTarget("hot", hotBox);
        AddBoxTarget("cold", coldBox);
        DrawText(dc, Loc($"Heat_Cycle_Hot_{Direction}"), new Point(cx, hotBox.Top + (boxHeight * 0.36)), Ink.Text, 13, center: true, bold: true);
        DrawText(dc, Temperature(hot), new Point(cx, hotBox.Top + (boxHeight * 0.7)), Ink.Text, 12, center: true);
        DrawText(dc, Loc($"Heat_Cycle_Cold_{Direction}"), new Point(cx, coldBox.Top + (boxHeight * 0.36)), Ink.Text, 13, center: true, bold: true);
        DrawText(dc, Temperature(cold), new Point(cx, coldBox.Top + (boxHeight * 0.7)), Ink.Text, 12, center: true);

        // The machine between them: a gas in a cylinder, its base touching the hot side, the cold side, or neither.
        var radius = Math.Clamp(Math.Min(area.Height * 0.17, area.Width * 0.2), 36, 92);
        var machine = new Point(cx, area.Top + (area.Height / 2));
        DrawMachine(dc, machine, radius, state, hot, cold);
        _targets.Add("machine", machine, radius);

        // Energy per cycle with the heat from (or to) the hot side as 100.
        var hotHeat = 100.0;
        var coldHeat = 100 * cold / hot;
        var work = hotHeat - coldHeat;
        var full = Math.Clamp(radius * 0.55, 20, 46);
        var gap = 6.0;
        var hotOn = state.Step == 0;
        var coldOn = state.Step == 2;
        if (Forward)
        {
            DrawFlowArrow(dc, new Point(cx, hotBox.Bottom + gap), new Point(cx, machine.Y - radius - gap), Ink.Red, full, hotOn ? 1 : 0.45);
            DrawFlowArrow(dc, new Point(cx, machine.Y + radius + gap), new Point(cx, coldBox.Top - gap), Ink.Blue, full * coldHeat / 100, coldOn ? 1 : 0.45);
            var wheel = new Point(Math.Min(area.Right - 30, cx + radius + 120), machine.Y);
            DrawFlowArrow(dc, new Point(machine.X + radius + gap, machine.Y), new Point(wheel.X - 34, machine.Y), Ink.Gold, full * work / 100, 0.9);
            DrawFlywheel(dc, wheel, 28);
            _targets.Add("work", wheel, 30);
            DrawFlowLabel(dc, LocFormat("Heat_Cycle_Flow_HotIn", Number(hotHeat, "0")), new Point(cx + (full / 2) + 12, (hotBox.Bottom + machine.Y - radius) / 2), Ink.Red, "hotflow");
            DrawFlowLabel(dc, LocFormat("Heat_Cycle_Flow_ColdOut", Number(coldHeat, "0")), new Point(cx + (full / 2) + 12, (machine.Y + radius + coldBox.Top) / 2), Ink.Cyan, "coldflow");
            DrawFlowLabel(dc, LocFormat("Heat_Cycle_Flow_WorkOut", Number(work, "0")), new Point(machine.X + radius + 12, machine.Y - (full / 2) - 16), Ink.Gold, "work");
        }
        else
        {
            DrawFlowArrow(dc, new Point(cx, coldBox.Top - gap), new Point(cx, machine.Y + radius + gap), Ink.Blue, full * coldHeat / 100, coldOn ? 1 : 0.45);
            DrawFlowArrow(dc, new Point(cx, machine.Y - radius - gap), new Point(cx, hotBox.Bottom + gap), Ink.Red, full, hotOn ? 1 : 0.45);
            var plug = new Point(Math.Max(area.Left + 24, cx - radius - 120), machine.Y);
            DrawFlowArrow(dc, new Point(plug.X + 20, machine.Y), new Point(machine.X - radius - gap, machine.Y), Ink.Gold, full * work / 100, 0.9);
            DrawPlug(dc, plug);
            _targets.Add("work", plug, 24);
            var useful = Direction == CycleDirection.Fridge ? "Heat_Cycle_Flow_ColdIn_Useful" : "Heat_Cycle_Flow_ColdIn";
            var hotKey = Direction == CycleDirection.HeatPump ? "Heat_Cycle_Flow_HotOut_Useful" : "Heat_Cycle_Flow_HotOut";
            DrawFlowLabel(dc, LocFormat(hotKey, Number(hotHeat, "0")), new Point(cx + (full / 2) + 12, (hotBox.Bottom + machine.Y - radius) / 2), Ink.Red, "hotflow");
            DrawFlowLabel(dc, LocFormat(useful, Number(coldHeat, "0")), new Point(cx + (full / 2) + 12, (machine.Y + radius + coldBox.Top) / 2), Ink.Cyan, "coldflow");
            DrawFlowLabel(dc, LocFormat("Heat_Cycle_Flow_WorkIn", Number(work, "0")), new Point(plug.X + 4, machine.Y - (full / 2) - 16), Ink.Gold, "work");
        }

        // The arrows themselves, between the boxes and the machine.
        _targets.Add("hotflow", new Point(cx, (hotBox.Bottom + machine.Y - radius) / 2), (full / 2) + 6);
        _targets.Add("coldflow", new Point(cx, (machine.Y + radius + coldBox.Top) / 2), (full / 2) + 6);
    }

    private void DrawFlowLabel(PaintContext dc, string text, Point at, Color color, string id)
    {
        var formatted = _labels.Text(text, 11.5, color);
        var topLeft = new Point(at.X, at.Y - 10);
        _labels.DrawTextPill(dc, text, topLeft, 11.5, color);
        AddBoxTarget(id, new Rect(topLeft, new Size(formatted.Width + 12, formatted.Height + 4)));
    }

    private void DrawMachine(PaintContext dc, Point centre, double radius, CarnotState state, double hot, double cold)
    {
        dc.DrawEllipse(Solid(255, 0x16, 0x1C, 0x28), Ink.LightEdge, centre, radius, radius);

        var (a, _, c, _) = CarnotModel.Volumes(hot, cold);
        var cylinder = new Rect(centre.X - (radius * 0.42), centre.Y - (radius * 0.72), radius * 0.84, radius * 1.3);
        var gasHeight = (cylinder.Height - 10) * (0.18 + (0.82 * (state.Volume - a) / Math.Max(1e-9, c - a)));
        var gas = new Rect(cylinder.Left + 3, cylinder.Bottom - gasHeight, cylinder.Width - 6, gasHeight);
        dc.DrawRectangle(Solid(255, 0x0C, 0x10, 0x18), null, cylinder);
        dc.DrawRectangle(FadedBrush(Quantize(HeatColor(state.Temperature - 273.15)), 0.75), null, gas);
        for (var i = 0; i < 18; i++)
        {
            var u = Frac(0.5 + (i * 0.6180339887));
            var v = Frac(0.5 + (i * 0.7548776662));
            var jiggle = Math.Sqrt(state.Temperature / 300) * 1.6;
            var p = new Point(gas.Left + 3 + (u * (gas.Width - 6)) + (Math.Sin((_t * 8 * jiggle) + i) * jiggle), gas.Top + 3 + (v * (gas.Height - 6)));
            dc.DrawEllipse(Solid(230, 0xEE, 0xF1, 0xF6), null, p, 1.6, 1.6);
        }

        // The piston, with a rod up out of the machine.
        var piston = new Rect(cylinder.Left + 2, gas.Top - 9, cylinder.Width - 4, 9);
        dc.DrawRectangle(Ink.Piston, Ink.Edge, piston);
        dc.DrawLine(MakePen(Ink.Rod, 4), new Point(centre.X, piston.Top), new Point(centre.X, cylinder.Top - 4));
        dc.DrawLine(Ink.Liner, cylinder.TopLeft, cylinder.BottomLeft);
        dc.DrawLine(Ink.Liner, cylinder.TopRight, cylinder.BottomRight);

        // The base: red-hot against the hot side, blue against the cold, grey insulation in between.
        var baseColor = state.Step switch
        {
            0 => Ink.Red,
            2 => Ink.Blue,
            _ => Color.FromRgb(0x6A, 0x72, 0x80),
        };
        var plate = new Rect(cylinder.Left - 6, cylinder.Bottom, cylinder.Width + 12, 7);
        dc.DrawRoundedRectangle(FadedBrush(baseColor, 1), Ink.Edge, plate, 2, 2);
        var contact = state.Step switch
        {
            0 => "Heat_Cycle_Contact_Hot",
            2 => "Heat_Cycle_Contact_Cold",
            _ => "Heat_Cycle_Contact_None",
        };
        DrawText(dc, Loc(contact), new Point(centre.X, plate.Bottom + 12), baseColor, 10.5, center: true);
    }

    private void DrawFlywheel(PaintContext dc, Point centre, double radius)
    {
        dc.DrawEllipse(null, MakePen(Solid(255, 0xC9, 0xD1, 0xDC), 4), centre, radius, radius);
        for (var k = 0; k < 4; k++)
        {
            var a = (_t * 2.2) + (k * Math.PI / 2);
            dc.DrawLine(MakePen(Solid(255, 0xC9, 0xD1, 0xDC), 2), centre, centre + new Vector(Math.Cos(a) * radius, Math.Sin(a) * radius));
        }

        dc.DrawEllipse(Solid(255, 0xC9, 0xD1, 0xDC), null, centre, 4, 4);
    }

    private static void DrawPlug(PaintContext dc, Point at)
    {
        dc.DrawRoundedRectangle(Solid(255, 0xE8, 0xEC, 0xF2), Ink.Edge, new Rect(at.X - 14, at.Y - 12, 22, 24), 4, 4);
        dc.DrawLine(MakePen(Solid(255, 0x9A, 0xA2, 0xAE), 3), new Point(at.X + 8, at.Y - 6), new Point(at.X + 18, at.Y - 6));
        dc.DrawLine(MakePen(Solid(255, 0x9A, 0xA2, 0xAE), 3), new Point(at.X + 8, at.Y + 6), new Point(at.X + 18, at.Y + 6));
        dc.DrawLine(MakePen(Solid(255, 0x30, 0x36, 0x40), 3), new Point(at.X - 14, at.Y), new Point(at.X - 30, at.Y + 14));
    }

    /// <summary>
    /// The loop on a pressure-volume chart zoomed to it (the axes are linear, so the shaded area is still the work), with
    /// the two temperatures' curves behind it and each step labelled outside the loop.
    /// </summary>
    private void DrawPvLoop(PaintContext dc, Rect area, CarnotState state, double hot, double cold)
    {
        var plot = new Rect(area.Left + 30, area.Top + 30, Math.Max(60, area.Width - 46), Math.Max(60, area.Height - 74));
        var (a, _, c, _) = CarnotModel.Volumes(hot, cold);
        var lowest = cold / hot / c;
        var minV = a - ((c - a) * 0.12);
        var maxV = c + ((c - a) * 0.12);
        var minP = Math.Max(0, lowest - ((1 - lowest) * 0.12));
        var maxP = 1 + ((1 - lowest) * 0.12);
        Point P(double volume, double pressure) => new(
            plot.Left + ((volume - minV) / (maxV - minV) * plot.Width),
            plot.Bottom - ((pressure - minP) / (maxP - minP) * plot.Height));

        dc.DrawRoundedRectangle(Solid(150, 0x08, 0x0C, 0x14), Ink.Frame, plot.Inflate(new Thickness(14, 14)), 10, 10);
        dc.DrawLine(Ink.Axis, plot.BottomLeft, plot.TopLeft);
        dc.DrawLine(Ink.Axis, plot.BottomLeft, plot.BottomRight);
        DrawArrowHead(dc, Solid(200, 0xC9, 0xD1, 0xDC), plot.TopLeft + new Vector(0, -6), new Vector(0, -1), 8);
        DrawArrowHead(dc, Solid(200, 0xC9, 0xD1, 0xDC), plot.BottomRight + new Vector(6, 0), new Vector(1, 0), 8);
        DrawText(dc, Loc("Heat_Cycle_Axis_Pressure"), plot.TopLeft + new Vector(10, -4), Ink.Dim, 11);
        DrawTextRight(dc, Loc("Heat_Cycle_Axis_Volume"), plot.BottomRight + new Vector(0, -10), Ink.Dim, 11);

        // The isotherms through the loop's hot and cold sides, across the chart.
        dc.PushClip(new RectangleGeometry(plot));
        foreach (var (temperature, color) in new[] { (hot, Ink.Red), (cold, Ink.Blue) })
        {
            var curve = Enumerable.Range(0, 81).Select(i => minV + (i / 80.0 * (maxV - minV))).Select(v => P(v, temperature / hot / v));
            dc.DrawGeometry(null, FadedPen(color, 0.45, 1.2), Line(curve));
        }

        dc.Pop();
        AddBoxTarget("isotherm", DrawText(dc, LocFormat("Heat_Cycle_Isotherm_Hot", Number(hot, "0")), P(a, 1) + new Vector(-6, -14), Ink.Red, 10.5, center: true));
        AddBoxTarget("isotherm", DrawTextRight(dc, LocFormat("Heat_Cycle_Isotherm_Cold", Number(cold, "0")), P(c, lowest) + new Vector(10, 16), Ink.Blue, 10.5));

        // The loop: shaded inside, each step in its own colour, an arrow on each showing the way round.
        var loop = Enumerable.Range(0, 400).Select(i => CarnotModel.At(i / 100.0, hot, cold)).Select(s => P(s.Volume, s.Pressure)).ToArray();
        var centroid = new Point(loop.Average(p => p.X), loop.Average(p => p.Y));
        var fillColor = Forward ? Ink.Gold : Ink.Cyan;
        var outline = Shape(loop);
        dc.DrawGeometry(FadedBrush(fillColor, 0.16), null, outline);
        if (Glows("loop")) dc.DrawGeometry(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds), outline);
        _targets.Add("loop", centroid, Math.Max(16, Math.Min(plot.Width, plot.Height) * 0.12));
        var placed = new List<Rect>();
        for (var step = 0; step < 4; step++)
        {
            var points = Enumerable.Range(0, 101).Select(i => CarnotModel.At(step + (i / 100.0 * 0.99999), hot, cold)).Select(s => P(s.Volume, s.Pressure));
            var current = state.Step == step;
            dc.DrawGeometry(null, FadedPen(StepColors[step], current ? 1 : 0.7, current ? 3.2 : 2.2), Line(points));

            var mid = CarnotModel.At(step + 0.5, hot, cold);
            var next = CarnotModel.At(step + (Forward ? 0.52 : 0.48), hot, cold);
            var at = P(mid.Volume, mid.Pressure);
            var direction = P(next.Volume, next.Pressure) - at;
            DrawArrowHead(dc, FadedBrush(StepColors[step], 0.95), at + (direction * 4), direction, 10);
            _targets.Add(StepId(step), at, 12);

            if (!ShowLabels) continue;

            // Out from the loop's middle, beyond the curve.
            Vector outward = at - centroid;
            if (outward.Length < 1e-6) outward = new Vector(0, -1);
            outward = outward.Normalize();
            var label = LocFormat("Heat_Cycle_StepLabel", StepNumber(step, Forward), Loc($"Heat_Cycle_Step{step}_{(Forward ? "Forward" : "Backward")}"));
            var formatted = _labels.Text(label, 11, StepColors[step]);
            var anchor = at + (outward * 16);
            var x = outward.X >= 0 ? anchor.X : anchor.X - formatted.Width - 12;
            var y = anchor.Y - (formatted.Height / 2) + (outward.Y * 8);
            x = Math.Clamp(x, plot.Left + 4, Math.Max(plot.Left + 4, plot.Right - formatted.Width - 12));
            y = Math.Clamp(y, plot.Top - 8, plot.Bottom - formatted.Height - 4);

            // A thin loop puts neighbouring steps' labels on top of each other: move a label on until it is clear.
            var box = new Rect(x, y, formatted.Width + 12, formatted.Height + 4);
            var shift = (outward.Y >= 0 ? 1 : -1) * (box.Height + 3);
            for (var tries = 0; tries < 6 && placed.Any(r => r.Intersects(box)); tries++)
            {
                box = box.WithY(Math.Clamp(box.Y + shift, plot.Top - 8, plot.Bottom - box.Height));
                if (box.Y <= plot.Top - 8 || box.Y >= plot.Bottom - box.Height) shift = -shift;
            }

            placed.Add(box);
            _labels.DrawTextPill(dc, label, box.TopLeft, 11, current ? StepColors[step] : Lerp(StepColors[step], Ink.Dim, 0.4));
            AddBoxTarget(StepId(step), box);
        }

        // The gas now.
        var dot = P(state.Volume, state.Pressure);
        dc.DrawEllipse(FadedBrush(Ink.Text, 0.25), null, dot, 11, 11);
        dc.DrawEllipse(Solid(255, 0xEE, 0xF1, 0xF6), null, dot, 5.5, 5.5);

        // What the shaded area means, as a key in the corner.
        var key = Loc(Forward ? "Heat_Cycle_Area_Work" : "Heat_Cycle_Area_WorkIn");
        var swatch = new Rect(plot.Right - 18, plot.Top + 6, 14, 10);
        dc.DrawRectangle(FadedBrush(fillColor, 0.35), FadedPen(fillColor, 0.8, 1), swatch);
        AddBoxTarget("loop", swatch.Union(DrawTextRight(dc, key, new Point(swatch.Left - 6, swatch.Top + 5), fillColor, 11)));
    }

    private static string Temperature(double kelvin) => $"{Number(kelvin, "0")} K · {Number(kelvin - 273.15, "0")} °C";
}
