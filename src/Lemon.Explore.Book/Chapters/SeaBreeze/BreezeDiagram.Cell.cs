using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.SeaBreeze;

/// <summary>
/// Cell mode: the thermal circulation built up one step at a time in a box of air over the sea (left) and the land
/// (right), with its pressure surfaces bending as the land warms (or cools).
/// </summary>
public sealed partial class BreezeDiagram
{
    private readonly List<CellParticle> _cellParticles = [];

    /// <summary>The pressure levels drawn, hPa, with where they sit in the box (share of the air's height).</summary>
    private static readonly (double HPa, double Height)[] CellLevels = [(1000, 0.2), (950, 0.4), (900, 0.6), (850, 0.8)];

    /// <summary>How much warmer the land is than the sea, scaled so 8 °C is 1 (negative when colder).</summary>
    private double CellContrast => Math.Clamp((LandTemperature - SeaTemperature) / 8, -1.5, 1.5);

    private readonly record struct CellBox(Rect Box, double GroundY)
    {
        /// <summary>A point by its share across the box (0 = left, the sea; 1 = right, the land) and up the air (0 = ground).</summary>
        public Point P(double across, double up) => new(Box.Left + (across * Box.Width), GroundY - (up * (GroundY - Box.Top)));

        public double AirHeight => GroundY - Box.Top;
    }

    private CellBox CellLayout()
    {
        var stage = Stage(TitleSpace + 34);
        var width = Math.Min(stage.Width - 90, (stage.Height - 40) * 1.75);
        var height = width / 1.75;
        // Centred in the stage's height when its width is what limits it.
        var box = new Rect(stage.Left + ((stage.Width - width) / 2), stage.Top + 8 + Math.Max(0, (stage.Height - 40 - height) / 2), width, height);
        return new CellBox(box, box.Bottom - (height * 0.17));
    }

    private void AdvanceCellParticles(double step)
    {
        var loop = BreezeModel.Progress(_stepTime, CellStage.Circulation);
        var contrast = CellContrast;
        AdvanceParticles(_cellParticles, 90, contrast >= 0 ? 1 : -1, 0.08 * Math.Min(1, Math.Abs(contrast)) * loop, step);
    }

    private void DrawCellMode(PaintContext dc, double width, double height)
    {
        var layout = CellLayout();
        var box = layout.Box;
        var t = _stepTime;
        var stage = BreezeModel.StageAt(t);
        var heat = BreezeModel.Progress(t, CellStage.Heating);
        var aloft = BreezeModel.Progress(t, CellStage.AloftFlow);
        var surface = BreezeModel.Progress(t, CellStage.SurfacePressure);
        var wind = BreezeModel.Progress(t, CellStage.SurfaceWind);
        var loop = BreezeModel.Progress(t, CellStage.Circulation);
        var contrast = CellContrast;
        var day = contrast >= 0;
        var strength = Math.Min(1, Math.Abs(contrast));

        _stars.Draw(dc, width, height, RenderScale(), _clock.Elapsed.TotalSeconds, day ? 0.25 : 0.7);

        // The box of air.
        var air = SpaceDrawing.LinearGradient(day ? Color.FromRgb(0x1B, 0x3A, 0x66) : Color.FromRgb(0x0A, 0x14, 0x2E), day ? Color.FromRgb(0x4A, 0x7E, 0xB0) : Color.FromRgb(0x1A, 0x2A, 0x4E), 90);
        dc.DrawRoundedRectangle(air, Ink.Frame, box, 10, 10);
        dc.PushClip(new RectangleGeometry(box, 10, 10));

        DrawCellColumns(dc, layout, heat, day, strength);
        DrawCellGround(dc, layout, heat, day, strength);
        DrawCellIsobars(dc, layout, heat, surface, contrast);
        if (ShowFlow)
        {
            DrawParticles(dc, _cellParticles, day ? 1 : -1, 0.08 * strength * loop, loop,
                (s, q) => layout.P(0.08 + (s * 0.84), 0.04 + (q * 0.9)), Ink.Text);
        }

        DrawCellArrows(dc, layout, aloft, wind, loop, day, strength);
        DrawCellMarks(dc, layout, aloft, surface, day, strength);
        dc.Pop();

        DrawCellHeatSource(dc, layout, heat, day, strength);
        DrawCellThermometers(dc, layout, heat);
        DrawCellSteps(dc, layout, stage, t);

    }

    /// <summary>The columns of air over the sea and the land; the land's is tinted as it warms (or cools).</summary>
    private static void DrawCellColumns(PaintContext dc, CellBox layout, double heat, bool day, double strength)
    {
        var amount = heat * strength;
        if (amount < 0.02) return;
        var color = day ? Ink.Orange : Ink.Blue;
        var glow = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, WithAlpha(color, 0.34 * amount)), new ImmutableGradientStop(1, WithAlpha(color, 0))],
            center: new RelativePoint(0.5, 1, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.5, 1, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.55, RelativeUnit.Relative),
            radiusY: new RelativeScalar(1, RelativeUnit.Relative));
        dc.DrawRectangle(glow, null, new Rect(layout.P(0.5, 1), layout.P(1, 0)));
    }

    /// <summary>The sea on the left, the land on the right, warming to orange (or cooling to grey-blue) with the heat.</summary>
    private void DrawCellGround(PaintContext dc, CellBox layout, double heat, bool day, double strength)
    {
        var box = layout.Box;
        var middle = box.Left + (box.Width / 2);
        var sea = new Rect(box.Left, layout.GroundY, box.Width / 2, box.Bottom - layout.GroundY);
        dc.DrawRectangle(Ink.Sea, null, sea);
        for (var i = 0; i < 6; i++)
        {
            var x = box.Left + (Frac((i * 0.173) + (_t * 0.02)) * (box.Width / 2 - 20));
            var y = layout.GroundY + 6 + (i % 3 * 7);
            dc.DrawLine(Ink.Wave, new Point(x, y), new Point(x + 14, y));
        }

        var warm = Lerp(Color.FromRgb(0x6A, 0x52, 0x38), day ? Color.FromRgb(0xC8, 0x6A, 0x30) : Color.FromRgb(0x3A, 0x48, 0x60), heat * strength);
        var land = SpaceDrawing.LinearGradient(warm, Color.FromRgb(0x24, 0x1C, 0x16), 90);
        dc.DrawRectangle(land, null, new Rect(middle, layout.GroundY, box.Width / 2, box.Bottom - layout.GroundY));
        dc.DrawLine(Ink.Grass, new Point(middle, layout.GroundY), new Point(box.Right, layout.GroundY));

        var middleY = layout.GroundY + ((box.Bottom - layout.GroundY) * 0.55);
        _targets.Add("sea", new Point(box.Left + (box.Width / 4), middleY), 26);
        _targets.Add("land", new Point(box.Left + (box.Width * 0.75), middleY), 26);
        if (!ShowLabels) return;
        DrawText(dc, Loc("Breeze_Sea"), new Point(box.Left + (box.Width / 4), middleY), Ink.Text, 13, center: true, bold: true);
        DrawText(dc, Loc("Breeze_Land"), new Point(box.Left + (box.Width * 0.75), middleY), Ink.Text, 13, center: true, bold: true);
    }

    /// <summary>
    /// The pressure surfaces. Heating lifts those over the land, the more the higher up; once the air has moved out aloft
    /// the land column is lighter, so near the ground they sink over the land and rise over the sea. Bumps exaggerated.
    /// </summary>
    private void DrawCellIsobars(PaintContext dc, CellBox layout, double heat, double surface, double contrast)
    {
        var bend = 0.13 * layout.AirHeight * Math.Clamp(contrast, -1.2, 1.2);
        var box = layout.Box;
        foreach (var (hPa, up) in CellLevels)
        {
            var landShift = bend * ((heat * up * 1.0) - (surface * 0.5));
            var seaShift = bend * surface * 0.3 * (1 - up);
            var line = new StreamGeometry();
            using (var context = line.Open())
            {
                for (var i = 0; i <= 120; i++)
                {
                    var across = i / 120.0;
                    var land = across > 0.5 ? Math.Sin(Math.PI * (across - 0.5) / 0.5) : 0;
                    var sea = across < 0.5 ? Math.Sin(Math.PI * across / 0.5) : 0;
                    var at = layout.P(across, up);
                    var point = new Point(at.X, at.Y - (landShift * land) - (seaShift * sea));
                    if (i == 0) context.BeginFigure(point, false);
                    else context.LineTo(point);
                }

                context.EndFigure(false);
            }
            if (Glows("isobars")) dc.DrawGeometry(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds), line);
            dc.DrawGeometry(null, Ink.IsobarStrong, line);
            if (ShowLabels) AddBoxTarget("isobars", DrawTextRight(dc, $"{Number(hPa, "0")} hPa", layout.P(1, up) + new Vector(-6, -9), Ink.Dim, 10));
        }

        if (ShowLabels) AddBoxTarget("isobars", DrawText(dc, Loc("Breeze_Isobar"), new Point(box.Left + 10, layout.P(0, CellLevels[^1].Height).Y - 12), Ink.Dim, 10.5));
    }

    /// <summary>The arrows of the cell, each fading in at its own step: aloft first, then along the ground, then up and down.</summary>
    private void DrawCellArrows(PaintContext dc, CellBox layout, double aloft, double wind, double loop, bool day, double strength)
    {
        if (strength < 0.05) return;
        var thickness = 3 + (3 * strength);
        var landSide = 0.78;
        var seaSide = 0.22;
        var low = 0.08;
        var high = 0.9;

        // Aloft: from the land's high to the sea's low by day, the other way at night.
        void Arrow(string id, Point from, Point control, Point to, Color color, double width, double alpha)
        {
            DrawCurvedArrow(dc, from, control, to, color, width, alpha);
            if (alpha > 0.5) _targets.Add(id, CurveMiddle(from, control, to), 10 + width);
        }

        var aloftFrom = layout.P(day ? 0.66 : 0.34, high);
        var aloftTo = layout.P(day ? 0.34 : 0.66, high);
        Arrow("return", aloftFrom, layout.P(0.5, high + 0.02), aloftTo, Ink.Text, thickness * 0.8, aloft);

        // Along the ground: the sea breeze by day, the land breeze at night.
        var groundFrom = layout.P(day ? 0.3 : 0.7, low);
        var groundTo = layout.P(day ? 0.7 : 0.3, low);
        var groundColor = day ? Ink.Cyan : Color.FromRgb(0xC8, 0xB8, 0xFF);
        Arrow(day ? "seabreeze" : "landbreeze", groundFrom, layout.P(0.5, low - 0.02), groundTo, groundColor, thickness, wind);

        // Up where it is warm, down where it is cool.
        var up = day ? landSide : seaSide;
        var down = day ? seaSide : landSide;
        Arrow("rising", layout.P(up, 0.2), layout.P(up + (day ? 0.06 : -0.06), 0.5), layout.P(up, 0.8), Ink.Orange, thickness * 0.85, loop);
        Arrow("sinking", layout.P(down, 0.8), layout.P(down + (day ? -0.06 : 0.06), 0.5), layout.P(down, 0.2), Ink.Blue, thickness * 0.85, loop);

        if (!ShowLabels) return;
        if (aloft > 0.5) AddBoxTarget("return", DrawPill(dc, Loc("Breeze_Cell_AloftLabel"), layout.P(0.5, high) + new Vector(0, -20), Ink.Text, 11));
        if (wind > 0.5)
        {
            AddBoxTarget(day ? "seabreeze" : "landbreeze",
                DrawPill(dc, Loc(day ? "Breeze_SeaBreeze" : "Breeze_LandBreeze"), layout.P(0.5, low) + new Vector(0, -22), groundColor, 12));
        }

        if (loop > 0.5)
        {
            AddBoxTarget("rising", DrawPill(dc, Loc("Breeze_Rising"), layout.P(up, 0.5) + new Vector(day ? 44 : -44, 0), Ink.Orange, 11));
            AddBoxTarget("sinking", DrawPill(dc, Loc("Breeze_Sinking"), layout.P(down, 0.5) + new Vector(day ? -44 : 44, 0), Ink.Blue, 11));
        }
    }

    /// <summary>"High" and "low" aloft (from the aloft step) and at the ground (from the surface step).</summary>
    private void DrawCellMarks(PaintContext dc, CellBox layout, double aloft, double surface, bool day, double strength)
    {
        if (strength < 0.05) return;
        DrawPressureMark(dc, layout.P(0.75, 0.97) + new Vector(0, 6), day, 17, aloft);
        DrawPressureMark(dc, layout.P(0.25, 0.97) + new Vector(0, 6), !day, 17, aloft);
        DrawPressureMark(dc, layout.P(0.9, 0.1), !day, 17, surface);
        DrawPressureMark(dc, layout.P(0.1, 0.1), day, 17, surface);
    }

    /// <summary>The Sun warming the land (by day), or the land giving off its heat to the night sky.</summary>
    private void DrawCellHeatSource(PaintContext dc, CellBox layout, double heat, bool day, double strength)
    {
        var box = layout.Box;
        if (day)
        {
            var sun = new Point(box.Right - 30, box.Top + 30);
            dc.DrawEllipse(Ink.SunGlow, null, sun, 46, 46);
            dc.DrawEllipse(Ink.SunCore, null, sun, 12, 12);
            _targets.Add("sun", sun, 18);
            if (heat * strength < 0.02) return;
            var ray = MakePen(new SolidColorBrush(WithAlpha(Ink.Gold, 0.55 * heat * strength)), 2, dashed: true);
            for (var i = 0; i < 4; i++)
            {
                var target = layout.P(0.58 + (i * 0.11), 0);
                var shift = Frac(_t * 0.6) * 0.25;
                var from = sun + ((target - sun) * (0.18 + shift));
                dc.DrawLine(ray, from, from + ((target - sun) * 0.4));
            }

            return;
        }

        var moon = new Point(box.Right - 30, box.Top + 30);
        dc.DrawEllipse(Ink.MoonGlow, null, moon, 30, 30);
        dc.DrawEllipse(Ink.MoonCore, null, moon, 9, 9);
        if (heat * strength < 0.02) return;
        _targets.Add("cooling", layout.P(0.75, 0.12), 30);

        // Heat leaving the land as wavy arrows going up.
        var pen = MakePen(new SolidColorBrush(WithAlpha(Ink.Orange, 0.6 * heat * strength)), 1.4);
        for (var i = 0; i < 4; i++)
        {
            var rise = Frac((_t * 0.4) + (i * 0.25));
            var start = layout.P(0.6 + (i * 0.1), 0.02 + (rise * 0.3));
            var wave = new StreamGeometry();
            using (var context = wave.Open())
            {
                context.BeginFigure(start, false);
                for (var k = 1; k <= 6; k++) context.LineTo(start + new Vector(Math.Sin(k * 1.5) * 3, -k * 4));

                context.EndFigure(false);
            }
            dc.PushOpacity(Math.Sin(Math.PI * rise));
            dc.DrawGeometry(null, pen, wave);
            dc.Pop();
        }
    }

    /// <summary>Thermometers on the sea and on the land; the land's climbs (or falls) as the step plays.</summary>
    private void DrawCellThermometers(PaintContext dc, CellBox layout, double heat)
    {
        var sea = SeaTemperature;
        var land = sea + ((LandTemperature - sea) * heat);
        var seaFoot = layout.P(0.4, 0) + new Vector(0, -4);
        var landFoot = layout.P(0.6, 0) + new Vector(0, -4);
        DrawThermometer(dc, seaFoot, 34, sea);
        DrawThermometer(dc, landFoot, 34, land);
        AddBoxTarget("sea", DrawPill(dc, $"{Number(sea, "0.0")} °C", seaFoot + new Vector(0, -52), Ink.Cyan, 11.5));
        AddBoxTarget("land", DrawPill(dc, $"{Number(land, "0.0")} °C", landFoot + new Vector(0, -52),
            land > sea + 0.05 ? Ink.Orange : land < sea - 0.05 ? Ink.Blue : Ink.Cyan, 11.5));
    }

    /// <summary>The step's title over the box, and a row of dots for the six steps under it.</summary>
    private void DrawCellSteps(PaintContext dc, CellBox layout, CellStage stage, double t)
    {
        var box = layout.Box;
        var index = (int)stage;
        var day = CellContrast >= 0;
        var even = Math.Abs(CellContrast) < 0.05 && stage > CellStage.Even;
        var name = even ? Loc("Breeze_Cell_NoDifference") : Loc($"Breeze_Cell_{stage}_{(day ? "Day" : "Night")}_Title");
        var title = LocFormat("Breeze_Cell_StepTitle", index + 1, BreezeModel.StageCount, name);
        DrawText(dc, title, new Point(box.Left + (box.Width / 2), box.Top - 16), Ink.Text, 15, center: true, bold: true);

        var dotY = box.Bottom + 14;
        for (var i = 0; i < BreezeModel.StageCount; i++)
        {
            var x = box.Left + (box.Width / 2) + ((i - ((BreezeModel.StageCount - 1) / 2.0)) * 18);
            var done = i < index || (i == index && t >= (i * BreezeModel.StageSeconds));
            var brush = i == index ? Ink.SunCore : done ? Solid(200, 0xC9, 0xD1, 0xDC) : Solid(70, 0xC9, 0xD1, 0xDC);
            dc.DrawEllipse(brush, null, new Point(x, dotY), i == index ? 5 : 3.5, i == index ? 5 : 3.5);
        }
    }
}
