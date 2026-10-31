using Avalonia;
using Avalonia.Media;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.StatesOfMatter;

/// <summary>
/// The particles view: forty molecules in a glass cylinder on a heater, under a piston with weights on it. The heater
/// glows or frosts with the temperature; the piston rests on a solid or liquid and rides up on a gas, lower the harder it
/// is pressed. A thermometer and a pressure gauge stand beside it, and a pill names the state.
/// </summary>
public sealed partial class MatterDiagram
{
    /// <summary>Where the parts of the particles view go.</summary>
    private readonly record struct ParticlesLayout(Rect Cylinder, Rect Inner, Rect Heater, Rect Thermometer, Point Gauge, double GaugeRadius, Rect PistonGrip);

    /// <summary>The piston's height in the cylinder, container widths, eased towards where it should be.</summary>
    private double _piston = 0.6;

    private ParticlesLayout LayOutParticles()
    {
        var stage = Stage();
        var width = Math.Min(stage.Width * 0.5, (stage.Height - 120) * 0.75);
        width = Math.Max(120, width);
        var height = Math.Max(140, stage.Height - 110);
        var cylinder = new Rect(stage.Left + Math.Max(10, (stage.Width * 0.42) - width) / 2 + 10, stage.Top + 34, width, height);
        var inner = cylinder.Deflate(new Thickness(5, 0, 5, 5));
        var heater = new Rect(cylinder.Left - 8, cylinder.Bottom + 4, cylinder.Width + 16, 16);
        var tube = new Rect(cylinder.Right + 34, cylinder.Top + 30, 12, Math.Max(60, cylinder.Height - 70));
        var right = stage.Right - tube.Right;
        var gaugeRadius = Math.Clamp(right * 0.3, 26, 52);
        var gauge = new Point(tube.Right + (right / 2), cylinder.Top + gaugeRadius + 20);
        var pistonY = inner.Bottom - (_piston * inner.Width);
        var grip = new Rect(cylinder.Center.X - 20, pistonY - 46, 40, 46);
        return new ParticlesLayout(cylinder, inner, heater, tube, gauge, gaugeRadius, grip);
    }

    /// <summary>The temperature at height <paramref name="y"/> on the thermometer, °C.</summary>
    private double ThermometerCelsius(double y)
    {
        var tube = LayOutParticles().Thermometer;
        var s = Model;
        var f = Math.Clamp((tube.Bottom - y) / tube.Height, 0, 1);
        return s.MinCelsius + (f * (s.MaxCelsius - s.MinCelsius));
    }

    private void DrawParticlesMode(PaintContext dc, double now)
    {
        var s = Model;
        var state = MatterModel.StateAt(s, Kelvin, Pressure);

        // The piston: just above a solid or liquid; on a gas, higher the hotter and the less it is pressed.
        var preliminary = LayOutParticles();
        var full = (preliminary.Inner.Height / preliminary.Inner.Width) - 0.04;
        var target = _box.GasCount > 0
            ? Math.Max(_box.CondensedTop + 0.1, full * (0.32 + (0.68 * MatterModel.GasVolume(Kelvin, Pressure))))
            : _box.CondensedTop + (MoleculeBox.Radius * 2.2);
        _piston += (Math.Min(full, target) - _piston) * 0.08;
        var layout = LayOutParticles();
        var inner = layout.Inner;
        _box.Ceiling = _piston;

        // The bench, the heater and the cylinder with its molecules.
        dc.DrawRectangle(Ink.Bench, null, new Rect(layout.Heater.Left - 30, layout.Heater.Bottom, layout.Heater.Width + 60, 30));
        DrawHeater(dc, layout.Heater, now);
        dc.DrawRectangle(Ink.GlassFill, null, layout.Cylinder);
        dc.PushClip(inner);
        _box.Draw(dc, inner, ShowBonds);
        dc.Pop();
        var glass = Glows("cylinder") ? ExploreTargets.GlowPen(now) : Ink.GlassPen;
        dc.DrawLine(glass, layout.Cylinder.TopLeft, layout.Cylinder.BottomLeft);
        dc.DrawLine(glass, layout.Cylinder.TopRight, layout.Cylinder.BottomRight);
        dc.DrawLine(glass, layout.Cylinder.BottomLeft, layout.Cylinder.BottomRight);
        AddBoxTarget("molecule", MoleculeSpot(inner));
        AddBoxTarget("cylinder", layout.Cylinder);

        DrawPiston(dc, layout, now);
        DrawThermometer(dc, layout.Thermometer, (Celsius - s.MinCelsius) / (s.MaxCelsius - s.MinCelsius), "thermometer", now);
        DrawGauge(dc, layout.Gauge, layout.GaugeRadius, now);

        if (ShowLabels)
        {
            DrawText(dc, LocFormat("Matter_Value_Celsius", Number(Celsius, "0")), new Point(layout.Thermometer.Center.X, layout.Thermometer.Top - 14), Ink.Red, 12, TextAnchor.Center, bold: true);
            DrawText(dc, Loc("Matter_Label_Molecules"), new Point(layout.Cylinder.Center.X, layout.Cylinder.Bottom + 40), Ink.Dim, 10.5, TextAnchor.Center);
        }

        var name = Loc(MatterViewModel.StateNameKey(Substance, state));
        var pillAt = new Point(layout.Gauge.X, layout.Gauge.Y + layout.GaugeRadius + 44);
        DrawTargetPill(dc, name, pillAt, StateColor(state), "state", 13);
        if (ShowLabels)
        {
            DrawText(dc, Loc($"Matter_Motion_{state}"), new Point(layout.Gauge.X, pillAt.Y + 26), Ink.Dim, 10.5, TextAnchor.Center);
        }
    }

    /// <summary>A small region around one molecule near the middle of the box, as the "a molecule" target.</summary>
    private static Rect MoleculeSpot(Rect inner)
    {
        var size = inner.Width * MoleculeBox.Radius * 3;
        return new Rect(inner.Left + (inner.Width * 0.5) - size, inner.Bottom - (inner.Width * 0.1) - size, size * 2, size * 2);
    }

    /// <summary>The plate under the cylinder: red-hot above room temperature, frosty blue below, its glow growing with the difference.</summary>
    private void DrawHeater(PaintContext dc, Rect plate, double now)
    {
        var warmth = Math.Clamp((Celsius - 20) / 200, -1, 1);
        var color = warmth >= 0 ? Ink.Orange : Ink.Cyan;
        var glow = Math.Abs(warmth);
        dc.DrawRoundedRectangle(Ink.Metal, Glows("heater") ? ExploreTargets.GlowPen(now) : Ink.Frame, plate, 4, 4);
        if (glow > 0.02)
        {
            dc.DrawRoundedRectangle(FadedBrush(color, 0.25 + (0.6 * glow)), null, plate.Deflate(new Thickness(3, 3)), 3, 3);
            for (var i = 0; i < 3; i++)
            {
                var y = plate.Top - 4 - (i * 5) - (2 * Math.Sin((now * 3) + i));
                dc.DrawLine(FadedPen(color, glow * (0.5 - (i * 0.14)), 1.5), new Point(plate.Left + 12, y), new Point(plate.Right - 12, y));
            }
        }

        AddBoxTarget("heater", plate);
    }

    /// <summary>The piston: a plate across the cylinder on a rod, with weights stacked on it for the pressure (one more for each power of ten).</summary>
    private void DrawPiston(PaintContext dc, ParticlesLayout layout, double now)
    {
        var inner = layout.Inner;
        var y = inner.Bottom - (_piston * inner.Width);
        var plate = new Rect(layout.Cylinder.Left + 1, y - 10, layout.Cylinder.Width - 2, 10);
        dc.DrawRectangle(Ink.Metal, Glows("piston") ? ExploreTargets.GlowPen(now) : Ink.Frame, plate);
        var rodTop = Math.Max(layout.Cylinder.Top - 26, 4);
        dc.DrawRectangle(Ink.Metal, null, new Rect(layout.Cylinder.Center.X - 3, rodTop, 6, plate.Top - rodTop));

        // Weights: one block per power of ten above 1 Pa, stacked on the plate beside the rod.
        var blocks = (int)Math.Round(PressureLog);
        var blockWidth = Math.Min(26, (plate.Width / 2) - 14);
        for (var i = 0; i < blocks; i++)
        {
            var column = i % 2 == 0 ? -1 : 1;
            var level = i / 2;
            var x = layout.Cylinder.Center.X + (column * (8 + (blockWidth / 2))) - (blockWidth / 2);
            var block = new Rect(x, plate.Top - 9 - (level * 9), blockWidth, 8);
            if (block.Top < layout.Cylinder.Top - 30) break;
            dc.DrawRoundedRectangle(FadedBrush(Ink.Dim, 0.85), null, block, 2, 2);
        }

        AddBoxTarget("piston", new Rect(plate.Left, plate.Top - 30, plate.Width, 40));
    }

    /// <summary>A round pressure gauge: the needle on a scale of powers of ten from 1 Pa to 100 MPa, the reading under it.</summary>
    private void DrawGauge(PaintContext dc, Point center, double radius, double now)
    {
        dc.DrawEllipse(Ink.Dial, Glows("gauge") ? ExploreTargets.GlowPen(now) : Ink.GlassPen, center, radius, radius);
        const double start = Math.PI * 0.75;
        const double sweep = Math.PI * 1.5;
        for (var i = 0; i <= 8; i++)
        {
            var a = start + (sweep * i / 8);
            var outer = center + new Vector(Math.Cos(a), Math.Sin(a)) * (radius - 4);
            var tick = center + new Vector(Math.Cos(a), Math.Sin(a)) * (radius - (i % 2 == 0 ? 11 : 8));
            dc.DrawLine(Ink.Faint, tick, outer);
        }

        // The band of ordinary air pressure, and the needle.
        var atmAngle = start + (sweep * Math.Log10(MatterModel.Atmosphere) / 8);
        dc.DrawLine(FadedPen(Ink.Green, 0.8, 3), center + new Vector(Math.Cos(atmAngle - 0.06), Math.Sin(atmAngle - 0.06)) * (radius - 6),
            center + new Vector(Math.Cos(atmAngle + 0.06), Math.Sin(atmAngle + 0.06)) * (radius - 6));
        var angle = start + (sweep * Math.Clamp(PressureLog, 0, 8) / 8);
        dc.DrawLine(FadedPen(Ink.Red, 1, 2.5), center, center + new Vector(Math.Cos(angle), Math.Sin(angle)) * (radius - 10));
        dc.DrawEllipse(Ink.Mercury, null, center, 4, 4);
        AddBoxTarget("gauge", new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2));
        if (ShowLabels)
        {
            DrawText(dc, MatterViewModel.PressureWords(Pressure), new Point(center.X, center.Y + radius + 14), Ink.Text, 11.5, TextAnchor.Center, bold: true);
        }
    }
}
