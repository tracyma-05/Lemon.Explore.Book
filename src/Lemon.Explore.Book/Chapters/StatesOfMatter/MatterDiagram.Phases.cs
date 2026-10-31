using Avalonia;
using Avalonia.Media;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.StatesOfMatter;

/// <summary>
/// The phase diagram view: the substance's map of states, temperature across and pressure up (powers of ten), its
/// regions, lines and special points named; a few real places marked on it; the dot for the temperature and pressure
/// to drag about; and beside it a window on the molecules at the dot.
/// </summary>
public sealed partial class MatterDiagram
{
    /// <summary>Where the parts of the phase diagram view go.</summary>
    private readonly record struct PhasesLayout(Rect Plot, Rect Window);

    private PhasesLayout LayOutPhases()
    {
        var stage = Stage();
        if (stage.Width < 520)
        {
            // Narrow: the window goes under the plot.
            var size = Math.Clamp(stage.Height * 0.24, 60, 140);
            var narrowPlot = new Rect(stage.Left + 50, stage.Top + 14, stage.Width - 60, Math.Max(80, stage.Height - size - 74));
            return new PhasesLayout(narrowPlot, new Rect(stage.Left + 50, narrowPlot.Bottom + 32, size / 0.75, size));
        }

        var plot = new Rect(stage.Left + 54, stage.Top + 14, Math.Max(80, (stage.Width * 0.64) - 54), Math.Max(80, stage.Height - 46));
        var windowWidth = Math.Clamp(stage.Right - plot.Right - 30, 70, 240);
        var window = new Rect(plot.Right + 16 + ((stage.Right - plot.Right - 16 - windowWidth) / 2), plot.Top + 6, windowWidth, windowWidth * 0.75);
        return new PhasesLayout(plot, window);
    }

    private bool PlotContains(Point point) => LayOutPhases().Plot.Inflate(new Thickness(4)).Contains(point);

    /// <summary>The dot to the temperature and pressure under <paramref name="point"/>.</summary>
    private void MoveDot(Point point)
    {
        var plot = new PhasePlot(Model, LayOutPhases().Plot);
        SetCurrentValue(CelsiusProperty, Math.Round((plot.Kelvin(point.X) - MatterModel.ZeroCelsius) * 2) / 2);
        SetCurrentValue(PressureLogProperty, Math.Round(plot.PressureLog(point.Y) * 100) / 100);
    }

    /// <summary>The places marked on each substance's diagram.</summary>
    internal static IReadOnlyList<string> Landmarks(MatterSubstance substance) => substance == MatterSubstance.CarbonDioxide
        ? ["lm_dryice", "lm_marspole", "lm_extinguisher"]
        : ["lm_everest", "lm_cooker", "lm_mars"];

    /// <summary>
    /// Where a marked place sits: water boiling on Everest and in a pressure cooker, the thin cold air of Mars; dry ice at
    /// ordinary pressure, the frost of Mars's polar caps, the liquid in a carbon dioxide fire extinguisher.
    /// </summary>
    internal static (double Celsius, double Pascals)? LandmarkState(string id)
    {
        double BoilsAt(MatterModel.Substance s, double p) => MatterModel.BoilingTemperature(s, p) - MatterModel.ZeroCelsius;
        return id switch
        {
            "lm_everest" => (Math.Round(BoilsAt(MatterModel.Water, MatterModel.PressureAt(HeatingPlace.Everest)), 1), MatterModel.PressureAt(HeatingPlace.Everest)),
            "lm_cooker" => (Math.Round(BoilsAt(MatterModel.Water, MatterModel.PressureAt(HeatingPlace.Cooker)), 1), MatterModel.PressureAt(HeatingPlace.Cooker)),
            "lm_mars" => (-60, 600),
            "lm_dryice" => (-80, MatterModel.Atmosphere),
            "lm_marspole" => (-128, 600),
            "lm_extinguisher" => (20, 6e6),
            _ => null,
        };
    }

    private void DrawPhasesMode(PaintContext dc, double now)
    {
        var layout = LayOutPhases();
        var s = Model;
        var plot = new PhasePlot(s, layout.Plot);
        dc.DrawRectangle(Ink.PlotBack, null, layout.Plot);
        plot.Draw(dc, 2);
        if (Glows("plot")) dc.DrawRectangle(null, ExploreTargets.GlowPen(now), layout.Plot);
        AddBoxTarget("plot", layout.Plot);
        DrawAxes(dc, plot);
        DrawGlowingLines(dc, plot, now);

        // The regions' names, then the lines', the points' and the places'.
        var mid = (s.TripleT + s.CriticalT) / 2;
        var area = layout.Plot;
        Point Inside(Point p) => new(Math.Clamp(p.X, area.Left + 40, area.Right - 40), Math.Clamp(p.Y, area.Top + 12, area.Bottom - 12));
        DrawTargetPill(dc, Loc(MatterViewModel.StateNameKey(Substance, MatterState.Solid)),
            Inside(new Point((area.Left + plot.X(MatterModel.SolidLimit(s, s.TripleP * 10))) / 2, plot.Y(s.TripleP * 10))), Ink.Cyan, "solid", 12);
        DrawTargetPill(dc, Loc(MatterViewModel.StateNameKey(Substance, MatterState.Liquid)),
            Inside(plot.At(mid, Math.Min(5e7, MatterModel.BoilingPressure(s, mid) * 25))), Ink.Blue, "liquid", 12);
        DrawTargetPill(dc, Loc(MatterViewModel.StateNameKey(Substance, MatterState.Gas)),
            Inside(plot.At(mid + ((s.MaxT - mid) * 0.35), Math.Max(5, MatterModel.BoilingPressure(s, mid) / 400))), Ink.Gold, "gas", 12);
        DrawTargetPill(dc, Loc("Matter_State_Supercritical"),
            Inside(new Point((plot.X(s.CriticalT) + area.Right) / 2, plot.Y(Math.Sqrt(s.CriticalP * MatterModel.MaxPressure)))), Ink.Violet, "supercritical", 11);

        var triple = plot.At(s.TripleT, s.TripleP);
        var critical = plot.At(s.CriticalT, s.CriticalP);
        _targets.Add("triple", triple, 7);
        _targets.Add("critical", critical, 7);
        _targets.AddBox("atm", new Rect(area.Left, plot.Y(MatterModel.Atmosphere) - 4, area.Width * 0.12, 8));
        if (ShowLabels)
        {
            DrawText(dc, Loc("Matter_Label_Triple"), triple + new Vector(10, 10), Ink.Gold, 10.5);
            DrawText(dc, Loc("Matter_Label_Critical"), critical + new Vector(-10, 0), Ink.Gold, 10.5, TextAnchor.Right);
            DrawText(dc, Loc("Matter_Label_Atm"), new Point(area.Left + 4, plot.Y(MatterModel.Atmosphere) - 9), Ink.Text, 10);
        }

        foreach (var id in Landmarks(Substance))
        {
            var (c, p) = LandmarkState(id)!.Value;
            var at = plot.At(c + MatterModel.ZeroCelsius, p);
            dc.DrawEllipse(Brushes.Transparent, FadedPen(Ink.Green, 0.95, 2), at, 5, 5);
            _targets.Add(id, at, 8);
            if (!ShowLabels) continue;

            // Mars sits under the triple point's label and the extinguisher beside the critical point's: theirs go elsewhere.
            var name = Loc($"Matter_{ObjectName(id)}");
            if (id == "lm_mars") DrawText(dc, name, at + new Vector(0, 14), Ink.Green, 10, TextAnchor.Center);
            else if (id == "lm_extinguisher") DrawText(dc, name, at + new Vector(9, 12), Ink.Green, 10);
            else DrawText(dc, name, at + new Vector(9, -9), Ink.Green, 10);
        }

        // The dot, with its temperature and pressure.
        var state = MatterModel.StateAt(s, Kelvin, Pressure);
        var dot = plot.At(Kelvin, Pressure);
        var color = StateColor(state);
        dc.DrawEllipse(FadedBrush(color, 0.25), null, dot, 13, 13);
        dc.DrawEllipse(Brushes.White, FadedPen(color, 1, 3), dot, 7, 7);
        _targets.Add("dot", dot, 10);
        // Its reading beside it, on whichever side has room.
        var reading = LocFormat("Matter_Value_Dot", Number(Celsius, "0"), MatterViewModel.PressureWords(Pressure));
        var half = (_labels.Text(reading, 11, Ink.Text).Width / 2) + 6;
        var side = dot.X + 18 + (half * 2) < area.Right ? 1 : -1;
        DrawPill(dc, reading, new Point(dot.X + (side * (18 + half)), dot.Y), Ink.Text, 11);

        // The window on the molecules at the dot.
        DrawWindow(dc, layout.Window, "window", now, Loc(MatterViewModel.StateNameKey(Substance, state)));
    }

    /// <summary>Temperatures under the plot and pressures beside it.</summary>
    private void DrawAxes(PaintContext dc, PhasePlot plot)
    {
        var area = plot.Area;
        foreach (var c in plot.CelsiusTicks())
        {
            var x = plot.X(c + MatterModel.ZeroCelsius);
            dc.DrawLine(Ink.Faint, new Point(x, area.Bottom), new Point(x, area.Bottom + 4));
            DrawText(dc, LocFormat("Matter_Value_Celsius", Number(c, "0")), new Point(x, area.Bottom + 13), Ink.Dim, 10, TextAnchor.Center);
        }

        foreach (var p in PhasePlot.PressureTicks())
        {
            var y = plot.Y(p);
            dc.DrawLine(Ink.Faint, new Point(area.Left - 4, y), new Point(area.Left, y));
            DrawText(dc, PhasePlot.PressureTickText(p), new Point(area.Left - 6, y), Ink.Dim, 10, TextAnchor.Right);
        }

        DrawText(dc, Loc("Matter_Axis_Temperature"), new Point(area.Right, area.Bottom + 27), Ink.Dim, 10.5, TextAnchor.Right);
    }

    /// <summary>The boundary lines as targets (a pill on each), glowing when the walkthrough points at one.</summary>
    private void DrawGlowingLines(PaintContext dc, PhasePlot plot, double now)
    {
        var lines = new (string Id, List<Point> Points, double At)[]
        {
            ("subline", plot.SublimationLine(), 0.45),
            ("meltline", plot.MeltingLine(), 0.75),
            ("boilline", plot.BoilingLine(), 0.55),
        };
        dc.PushClip(plot.Area);
        foreach (var (id, points, _) in lines)
        {
            if (Glows(id)) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 5), Line(points));
        }

        dc.Pop();
        foreach (var (id, points, at) in lines)
        {
            var inside = points.Where(p => plot.Area.Contains(p)).ToList();
            if (inside.Count == 0) continue;
            var p = inside[(int)((inside.Count - 1) * at)];
            if (ShowLabels) DrawTargetPill(dc, Loc($"Matter_Line_{id}"), p, Ink.Text, id, 10);
            else AddBoxTarget(id, new Rect(p.X - 8, p.Y - 8, 16, 16));
        }
    }
}
