using Avalonia;
using Avalonia.Media;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.StatesOfMatter;

/// <summary>
/// The heating view: 100 g of ice (or dry ice) in a beaker on a burner, with a thermometer in it and a window on its
/// molecules; below, its heating curve, the temperature against the heat put in, drawn as far as the heat has gone, with
/// its flat stretches named. A pill says where the beaker is, which sets the air pressure and so the boiling point.
/// </summary>
public sealed partial class MatterDiagram
{
    /// <summary>Where the parts of the heating view go.</summary>
    private readonly record struct HeatingLayout(Rect Beaker, Rect Burner, Rect Window, Rect Plot, Point Place);

    private HeatingLayout LayOutHeating()
    {
        var stage = Stage();
        var plotHeight = Math.Clamp(stage.Height * 0.4, 120, 260);
        var plot = new Rect(stage.Left + 52, stage.Bottom - plotHeight + 6, Math.Max(80, stage.Width - 64), plotHeight - 34);
        var top = new Rect(stage.Left, stage.Top + 28, stage.Width, Math.Max(100, plot.Top - stage.Top - 64));
        var beakerWidth = Math.Clamp(top.Width * 0.36, 90, 190);
        var beakerHeight = Math.Clamp(top.Height - 40, 70, beakerWidth * 1.15);
        var beaker = new Rect(top.Left + (top.Width * 0.27) - (beakerWidth / 2), top.Bottom - 30 - beakerHeight, beakerWidth, beakerHeight);
        var burner = new Rect(beaker.Center.X - 30, beaker.Bottom + 6, 60, 22);
        var windowWidth = Math.Clamp(Math.Min(top.Width * 0.4, (top.Height - 10) / 0.8), 80, 240);
        var window = new Rect(top.Left + (top.Width * 0.74) - (windowWidth / 2), top.Top, windowWidth, windowWidth * 0.8);
        return new HeatingLayout(beaker, burner, window, plot, new Point(stage.Left + (stage.Width / 2), stage.Top + 10));
    }

    /// <summary>The heat under <paramref name="point"/> on the heating curve, kJ.</summary>
    private double CurveKj(Point point)
    {
        var plot = LayOutHeating().Plot;
        var total = MatterModel.TotalKj(Curve);
        return Math.Clamp((point.X - plot.Left) / plot.Width, 0, 1) * total;
    }

    private void DrawHeatingMode(PaintContext dc, double now)
    {
        var layout = LayOutHeating();
        var curve = Curve;
        var (solid, liquid, gas) = MatterModel.FractionsAt(curve, HeatKj);
        var celsius = MatterModel.CelsiusAt(curve, HeatKj);
        var stretch = MatterModel.StretchAt(curve, HeatKj);

        var placeText = LocFormat("Matter_Place_Pill", Loc($"Matter_Place_{Place}"), MatterViewModel.PressureWords(MatterModel.PressureAt(Place)));
        DrawTargetPill(dc, placeText, layout.Place, Ink.Green, "place", 12);

        dc.DrawRectangle(Ink.Bench, null, new Rect(layout.Beaker.Left - 50, layout.Burner.Bottom, layout.Beaker.Width + 100, 26));
        DrawBurner(dc, layout.Burner, now);
        DrawBeaker(dc, layout.Beaker, solid, liquid, gas, stretch, now);

        var s = Model;
        var tube = new Rect(layout.Beaker.Right - 26, layout.Beaker.Top - 30, 8, layout.Beaker.Height * 0.7);
        var fill = (celsius - s.StartCelsius) / (MatterModel.TotalKj(curve) > 0 ? curve[^1].EndC - s.StartCelsius : 1);
        DrawThermometer(dc, tube, 0.05 + (0.9 * fill), "thermometer", now);
        DrawPill(dc, LocFormat("Matter_Value_Celsius", Number(celsius, "0.0")), new Point(tube.Center.X + 30, tube.Top + 4), Ink.Red, 12);

        var caption = ShowLabels ? Loc($"Matter_Stretch_{stretch.Kind}_Title") : null;
        DrawWindow(dc, layout.Window, "window", now, caption);

        DrawHeatingCurve(dc, layout.Plot, curve, now);
    }

    /// <summary>The gas burner: its body, and a flame as tall as it is turned up.</summary>
    private void DrawBurner(PaintContext dc, Rect burner, double now)
    {
        var size = Heat switch
        {
            HeatSetting.Low => 0.45,
            HeatSetting.High => 1.0,
            _ => 0,
        };
        DrawFlame(dc, new Point(burner.Center.X, burner.Top), burner.Width * 0.8, size, now);
        dc.DrawRoundedRectangle(Ink.Metal, Glows("flame") ? ExploreTargets.GlowPen(now) : Ink.Frame, burner, 4, 4);
        AddBoxTarget("flame", new Rect(burner.Left, burner.Top - 34, burner.Width, burner.Height + 34));
        if (ShowLabels)
        {
            DrawText(dc, Loc($"Matter_Heat_{Heat}"), new Point(burner.Right + 10, burner.Center.Y), Heat == HeatSetting.Off ? Ink.Dim : Ink.Orange, 10.5);
        }
    }

    /// <summary>
    /// The beaker and what is in it: lumps of ice (or dry ice) as large as the solid's share; water as deep as the
    /// liquid's; bubbles while it boils, and steam or carbon dioxide fog rising off it while it boils or sublimes.
    /// </summary>
    private void DrawBeaker(PaintContext dc, Rect beaker, double solid, double liquid, double gas, MatterModel.Stretch stretch, double now)
    {
        var inside = beaker.Deflate(new Thickness(4, 0, 4, 4));
        var full = inside.Height * 0.55;
        var water = full * liquid;
        if (water > 0.5)
        {
            dc.DrawRectangle(Ink.Water, null, new Rect(inside.Left, inside.Bottom - water, inside.Width, water));
        }

        // Lumps: up to six, fewer and smaller as the solid goes; ice floats on its water, dry ice lies on the bottom.
        var lumps = (int)Math.Ceiling(solid * 6);
        var lumpSize = Math.Min(inside.Width / 4.2, 30) * (0.6 + (0.4 * Math.Sqrt(solid)));
        for (var i = 0; i < lumps; i++)
        {
            var row = i / 3;
            var column = i % 3;
            var x = inside.Left + 6 + (column * (inside.Width - 12) / 3) + (row * 6);
            var floor = Substance == MatterSubstance.Water && water > lumpSize * 0.5 ? inside.Bottom - water + (lumpSize * 0.8) : inside.Bottom;
            var y = floor - lumpSize - (row * lumpSize * 0.85);
            var lump = new Rect(x, y, lumpSize, lumpSize * 0.85);
            dc.DrawRoundedRectangle(Substance == MatterSubstance.Water ? Ink.Ice : Ink.DryIce, Ink.IceRim, lump, 4, 4);
        }

        // Boiling: bubbles rising through the water.
        if (stretch.Kind == "Boil" && water > 4)
        {
            for (var i = 0; i < 9; i++)
            {
                var phase = ((now * 0.9) + (i * 0.37)) % 1;
                var x = inside.Left + 8 + ((inside.Width - 16) * ((i * 0.618) % 1));
                var y = inside.Bottom - (phase * water);
                var r = 2 + (3 * phase);
                dc.DrawEllipse(null, FadedPen(Ink.Text, 0.7 * (1 - phase), 1.2), new Point(x, y), r, r);
            }
        }

        // Steam (or carbon dioxide fog) over the top while the change is going on.
        if (stretch.Kind is "Boil" or "Sublime" || (stretch.Kind == "WarmGas" && gas > 0.99 && Heat != HeatSetting.Off))
        {
            var surface = inside.Bottom - Math.Max(water, solid > 0 ? lumpSize : 0);
            for (var i = 0; i < 7; i++)
            {
                var phase = ((now * 0.35) + (i * 0.29)) % 1;
                var x = inside.Left + (inside.Width * ((i * 0.41) % 1)) + (8 * Math.Sin((now * 2) + i));
                var y = surface - (phase * (surface - beaker.Top + 40));
                var r = 5 + (12 * phase);
                dc.DrawEllipse(FadedBrush(Ink.Text, 0.22 * (1 - phase)), null, new Point(x, y), r, r * 0.7);
            }
        }

        var glass = Glows("beaker") ? ExploreTargets.GlowPen(now) : Ink.GlassPen;
        dc.DrawGeometry(Ink.GlassFill, glass, Polygon(beaker.TopLeft, beaker.BottomLeft, beaker.BottomRight, beaker.TopRight));
        AddBoxTarget("beaker", beaker);
    }

    /// <summary>
    /// The heating curve: temperature up, heat across. The whole curve faintly, the part already heated through brightly,
    /// a dot where it is now, and each flat stretch named (melting, boiling, subliming) with its temperature.
    /// </summary>
    private void DrawHeatingCurve(PaintContext dc, Rect plot, IReadOnlyList<MatterModel.Stretch> curve, double now)
    {
        var total = MatterModel.TotalKj(curve);
        var lowC = curve[0].StartC - 10;
        var highC = curve[^1].EndC + 10;
        double X(double kj) => plot.Left + (kj / total * plot.Width);
        double Y(double c) => plot.Bottom - ((c - lowC) / (highC - lowC) * plot.Height);

        dc.DrawRectangle(Ink.PlotBack, Glows("curve") ? ExploreTargets.GlowPen(now) : Ink.Frame, plot);
        var step = highC - lowC > 150 ? 50 : 25;
        for (var c = Math.Ceiling(lowC / step) * step; c <= highC; c += step)
        {
            var y = Y(c);
            dc.DrawLine(Ink.Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            DrawText(dc, LocFormat("Matter_Value_Celsius", Number(c, "0")), new Point(plot.Left - 6, y), Ink.Dim, 10, TextAnchor.Right);
        }

        var points = new List<Point> { new(X(0), Y(curve[0].StartC)) };
        points.AddRange(curve.Select(stretch => new Point(X(stretch.EndKj), Y(stretch.EndC))));
        dc.DrawGeometry(null, FadedPen(Ink.Text, 0.25, 2), Line(points));
        var done = points.Where(p => p.X <= X(HeatKj)).ToList();
        var here = new Point(X(HeatKj), Y(MatterModel.CelsiusAt(curve, HeatKj)));
        done.Add(here);
        dc.DrawGeometry(null, FadedPen(Ink.Orange, 1, 3), Line(done));
        dc.DrawEllipse(Brushes.White, FadedPen(Ink.Orange, 1, 2), here, 5, 5);

        foreach (var stretch in curve.Where(s => s.IsChange))
        {
            var id = stretch.Kind switch
            {
                "Melt" => "melt",
                "Boil" => "boil",
                _ => "sublime",
            };
            var middle = new Point((X(stretch.StartKj) + X(stretch.EndKj)) / 2, Y(stretch.StartC) - 14);
            DrawTargetPill(dc, LocFormat($"Matter_Plateau_{stretch.Kind}", Number(stretch.StartC, "0")), middle, Ink.Gold, id, 11);
        }

        DrawText(dc, Loc("Matter_Axis_Heat"), new Point(plot.Right, plot.Bottom + 14), Ink.Dim, 10.5, TextAnchor.Right);
        DrawText(dc, LocFormat("Matter_Value_Kj", Number(HeatKj, "0.0")), new Point(Math.Clamp(here.X, plot.Left + 20, plot.Right - 60), plot.Bottom + 14), Ink.Orange, 10.5, TextAnchor.Center);
        AddBoxTarget("curve", plot);
    }
}
