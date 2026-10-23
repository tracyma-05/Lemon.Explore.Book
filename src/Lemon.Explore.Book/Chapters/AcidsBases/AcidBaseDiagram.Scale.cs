using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.AcidsBases;

/// <summary>
/// The scale view: the pH scale from 0 to 14 in the chosen indicator's colours, everyday liquids above it at their pH,
/// ten times more H₃O⁺ at each step down; under it the chosen liquid in a big test tube, and a sample of it in each of
/// the six indicators. A liquid's name picks it, a small tube picks that indicator, and the water pill waters it down.
/// </summary>
public sealed partial class AcidBaseDiagram
{
    /// <summary>The indicators in the row of small tubes, in order.</summary>
    internal static readonly Indicator[] Dyes =
    [
        Indicator.Universal, Indicator.Cabbage, Indicator.Litmus, Indicator.Phenolphthalein, Indicator.MethylOrange, Indicator.Bromothymol,
    ];

    /// <summary>The most times the chosen liquid can be watered down ten-fold.</summary>
    public const int MaxDilutions = 6;

    private readonly record struct ScaleLayout(Rect Bar, double PillTop, Rect Lower);

    /// <summary>A liquid's pill above the bar: its left edge, width and row.</summary>
    private readonly record struct SubstancePill(Substance Substance, string Text, double Left, double Width, int Row);

    private const double PillRowHeight = 25;

    private ScaleLayout LayOutScale()
    {
        var stage = Stage();
        var pillTop = stage.Top + 4;

        // At least the original's four rows of pills; more on the book's narrower page, where they would overlap.
        var across = new Rect(stage.Left + 24, 0, stage.Width - 48, 30);
        var rows = Math.Max(4, PlacePills(across).Max(p => p.Row) + 1);
        var bar = across.WithY(pillTop + (rows * PillRowHeight) + 18);
        var lowerTop = bar.Bottom + 84;
        var lower = new Rect(stage.Left + 10, lowerTop, stage.Width - 20, Math.Max(120, stage.Bottom - lowerTop));
        return new ScaleLayout(bar, pillTop, lower);
    }

    /// <summary>The liquids' pills, each in the first row where it does not run into the one before.</summary>
    private List<SubstancePill> PlacePills(Rect bar)
    {
        var pills = new List<SubstancePill>();
        var rowEnds = new List<double>();
        foreach (var substance in AcidBaseModel.Substances)
        {
            var text = Loc(substance.NameKey) + " " + Number(substance.Ph, "0.0");
            var width = _labels.Text(text, 11, Ink.Text).Width + 12;
            var left = Math.Clamp(PhX(bar, substance.Ph) - (width / 2), bar.Left - 20, bar.Right + 20 - width);
            var row = rowEnds.FindIndex(end => left >= end + 6);
            if (row < 0)
            {
                row = rowEnds.Count;
                rowEnds.Add(0);
            }

            rowEnds[row] = left + width;
            pills.Add(new SubstancePill(substance, text, left, width, row));
        }

        return pills;
    }

    /// <summary>The pH of the chosen liquid, watered down as often as asked.</summary>
    private double ScalePh => AcidBaseModel.DilutedPh(AcidBaseModel.Substance(SubstanceId), Dilutions);

    private void DrawScaleMode(PaintContext dc)
    {
        var layout = LayOutScale();
        var bar = layout.Bar;
        var now = _clock.Elapsed.TotalSeconds;
        var substance = AcidBaseModel.Substance(SubstanceId);
        var ph = ScalePh;

        // The scale itself.
        dc.DrawRoundedRectangle(Ink.PlotBack, null, Inflated(bar, 3, 3), 7, 7);
        dc.DrawRoundedRectangle(StripBrush(ScaleIndicator), Glows("scale") ? ExploreTargets.GlowPen(now, 3) : Ink.Frame, bar, 6, 6);
        AddBoxTarget("scale", bar);
        for (var p = 0; p <= 14; p++)
        {
            var x = PhX(bar, p);
            dc.DrawLine(Ink.Faint, new Point(x, bar.Bottom), new Point(x, bar.Bottom + 5));
            var text = DrawText(dc, Number(p, "0"), new Point(x, bar.Bottom + 14), p == 7 ? Ink.Green : Ink.Text, 12, TextAnchor.Center, bold: p == 7);
            if (p == 7) AddBoxTarget("neutral", Inflated(text, 4, 2));
        }

        // Each step down: ten times as much H₃O⁺.
        var hopY = bar.Bottom + 34;
        var hops = new StreamGeometry();
        using (var context = hops.Open())
        {
            for (var p = 0; p < 14; p++)
            {
                var a = new Point(PhX(bar, p + 1) - 3, hopY);
                var b = new Point(PhX(bar, p) + 3, hopY);
                context.BeginFigure(a, false);
                context.QuadraticBezierTo(new Point((a.X + b.X) / 2, hopY + 12), b);
                context.EndFigure(false);
            }
        }
        dc.DrawGeometry(null, Glows("steps") ? ExploreTargets.GlowPen(now, 3) : FadedPen(Ink.Gold, 0.55, 1.2), hops);
        var stepWidth = bar.Width / 14;
        if (ShowLabels && stepWidth > 30)
        {
            for (var p = 0; p < 14; p++) DrawText(dc, "×10", new Point(PhX(bar, p + 0.5), hopY + 16), FadedColor(Ink.Gold, 0.75), 9, TextAnchor.Center);
        }

        var stepsCaption = DrawText(dc, Loc("Acid_Scale_Steps"), new Point(bar.Left, hopY + 34), Ink.Gold, 11);
        AddBoxTarget("steps", stepsCaption.Union(new Rect(bar.Left, hopY - 4, bar.Width, 24)));
        // (clear of the caption, which a narrow page brings close)
        var acidic = Loc("Acid_Strip_Acidic");
        var acidicX = Math.Max(bar.Left + (bar.Width * 0.25), stepsCaption.Right + 12 + (_labels.Text(acidic, 11, Ink.Red).Width / 2));
        DrawText(dc, acidic, new Point(acidicX, hopY + 34), Ink.Red, 11, TextAnchor.Center);
        DrawText(dc, Loc("Acid_Strip_Basic"), new Point(bar.Left + (bar.Width * 0.78), hopY + 34), Ink.Violet, 11, TextAnchor.Center);

        DrawSubstancePills(dc, layout, substance.Id);

        // Where the chosen liquid is now (watered down, perhaps).
        var mark = PhX(bar, ph);
        dc.DrawLine(FadedPen(Colors.White, 0.95, 2.5), new Point(mark, bar.Top - 4), new Point(mark, bar.Bottom + 4));
        dc.DrawEllipse(Brushes.White, null, new Point(mark, bar.Top - 4), 3.5, 3.5);

        DrawLowerScale(dc, layout.Lower, substance, ph, now);
    }

    /// <summary>The everyday liquids as pills above the bar in rows, each with a thin line down to its pH.</summary>
    private void DrawSubstancePills(PaintContext dc, ScaleLayout layout, string chosen)
    {
        var bar = layout.Bar;
        foreach (var pill in PlacePills(bar))
        {
            var substance = pill.Substance;
            var isChosen = substance.Id == chosen;
            var color = isChosen ? Ink.Gold : NatureColor(substance.Ph);
            var x = PhX(bar, substance.Ph);
            var y = layout.PillTop + 10 + (pill.Row * PillRowHeight);
            dc.DrawLine(FadedPen(color, isChosen ? 0.9 : 0.35, 1, dashed: !isChosen), new Point(x, y + 10), new Point(x, bar.Top - 2));
            DrawTargetPill(dc, pill.Text, new Point(pill.Left + (pill.Width / 2), y), color, "sub_" + substance.Id, 11);
        }
    }

    private void DrawLowerScale(PaintContext dc, Rect lower, Substance substance, double ph, double now)
    {
        // The big tube of the chosen liquid in the scale's indicator.
        var tubeHeight = Math.Clamp(lower.Height - 30, 90, 250);
        var tube = new Rect(lower.Left + 34, lower.Top + 8, Math.Clamp(tubeHeight * 0.26, 34, 54), tubeHeight);
        DrawTube(dc, tube, AcidBaseModel.LiquidColor(ScaleIndicator, ph), 0.72, Glows("tube") ? ExploreTargets.GlowPen(now, 3) : null);
        AddBoxTarget("tube", tube);

        // What it is.
        var x = tube.Right + 22;
        var name = Loc(substance.NameKey);
        DrawText(dc, name, new Point(x, tube.Top + 10), Ink.Gold, 17, bold: true);
        DrawText(dc, LocFormat("Acid_Value_Ph", Number(ph, "0.0")), new Point(x, tube.Top + 38), NatureColor(ph), 15, bold: true);
        DrawText(dc, Loc("Acid_Nature_" + AcidBaseModel.Nature(ph)), new Point(x + 80, tube.Top + 38), NatureColor(ph), 12);
        var h = Math.Pow(10, -ph);
        DrawText(dc, LocFormat("Acid_Scale_Hydronium", Scientific(h)), new Point(x, tube.Top + 64), Ink.Dim, 11.5);
        DrawText(dc, CompareToWater(ph), new Point(x, tube.Top + 84), Ink.Text, 11.5);
        var diluted = Dilutions == 0 ? Loc("Acid_Scale_Undiluted") : LocFormat("Acid_Scale_Diluted", Number(Math.Pow(10, Dilutions), "#,##0"));
        DrawText(dc, diluted, new Point(x, tube.Top + 104), Ink.Cyan, 11.5);
        if (Dilutions < MaxDilutions)
        {
            DrawTargetPill(dc, Loc("Acid_Scale_AddWater"), new Point(x + 60, tube.Top + 134), Ink.Cyan, "dilute", 11.5);
        }

        // The same liquid in each indicator.
        var rowLeft = Math.Max(x + 250, lower.Left + (lower.Width * 0.5));
        var slot = Math.Min(78, (lower.Right - rowLeft) / Dyes.Length);
        if (slot < 34) return;
        var small = Math.Clamp(tubeHeight * 0.62, 70, 150);
        DrawText(dc, Loc("Acid_Scale_Indicators"), new Point(rowLeft, lower.Top + 4), Ink.Dim, 11);
        for (var i = 0; i < Dyes.Length; i++)
        {
            var dye = Dyes[i];
            var cx = rowLeft + (slot * (i + 0.5));
            var box = new Rect(cx - 11, lower.Top + 22, 22, small);
            var chosen = dye == ScaleIndicator;
            DrawTube(dc, box, AcidBaseModel.LiquidColor(dye, ph), 0.65, chosen ? FadedPen(Ink.Gold, 1, 2) : null);
            var label = DrawText(dc, Loc(IndicatorShortKey(dye)), new Point(cx, box.Bottom + 14), chosen ? Ink.Gold : Ink.Dim, 10, TextAnchor.Center);
            AddBoxTarget(IndicatorId(dye), box.Union(label));
        }
    }

    /// <summary>"10,000× more H₃O⁺ than pure water" or "1,000× less", or "as in pure water".</summary>
    private static string CompareToWater(double ph)
    {
        var times = Math.Pow(10, Math.Abs(7 - ph));
        if (times < 1.5) return Loc("Acid_Scale_AsWater");
        var text = TimesText(times);
        return LocFormat(ph < 7 ? "Acid_Scale_More" : "Acid_Scale_Less", text);
    }

    /// <summary>A large factor in words that read naturally: "3,200", "2.5 million" / "250万".</summary>
    internal static string TimesText(double times)
    {
        var big = double.Parse(Loc("Acid_Times_Big"), System.Globalization.CultureInfo.InvariantCulture);
        var digits = times >= 100 ? 2 : 1;
        var rounded = RoundSignificant(times, digits);
        return rounded >= big ? LocFormat("Acid_Times_BigFormat", Number(RoundSignificant(rounded / big, 2), "#,##0.#")) : Number(rounded, "#,##0");
    }

    private static double RoundSignificant(double value, int digits)
    {
        if (value <= 0) return 0;
        var scale = Math.Pow(10, Math.Floor(Math.Log10(value)) - digits + 1);
        return Math.Round(value / scale) * scale;
    }

    /// <summary>A test tube with a round bottom, liquid to <paramref name="fill"/> of its height.</summary>
    private void DrawTube(PaintContext dc, Rect tube, Color liquid, double fill, IPen? ring)
    {
        var radius = tube.Width / 2;
        var top = tube.Bottom - radius - ((tube.Height - radius) * fill);
        var liquidShape = new StreamGeometry();
        using (var context = liquidShape.Open())
        {
            context.BeginFigure(new Point(tube.Left + 2, top), true);
            context.LineTo(new Point(tube.Left + 2, tube.Bottom - radius));
            context.ArcTo(new Point(tube.Right - 2, tube.Bottom - radius), new Size(radius - 2, radius - 2), 0, false, SweepDirection.CounterClockwise);
            context.LineTo(new Point(tube.Right - 2, top));

            context.EndFigure(true);
        }
        dc.DrawGeometry(LiquidBrush(liquid), null, liquidShape);

        var glass = new StreamGeometry();
        using (var context = glass.Open())
        {
            context.BeginFigure(new Point(tube.Left, tube.Top), false);
            context.LineTo(new Point(tube.Left, tube.Bottom - radius));
            context.ArcTo(new Point(tube.Right, tube.Bottom - radius), new Size(radius, radius), 0, false, SweepDirection.CounterClockwise);
            context.LineTo(new Point(tube.Right, tube.Top));

            context.EndFigure(false);
        }
        dc.DrawGeometry(null, ring ?? Ink.GlassPen, glass);
        dc.DrawRectangle(Ink.Shine, null, new Rect(tube.Left + 4, tube.Top + 6, Math.Max(2, tube.Width * 0.12), tube.Height - radius - 8));
        dc.DrawLine(Ink.GlassPen, new Point(tube.Left - 3, tube.Top), new Point(tube.Right + 3, tube.Top));
    }

    internal static string IndicatorId(Indicator indicator) => indicator switch
    {
        Indicator.Universal => "ind_universal",
        Indicator.Cabbage => "ind_cabbage",
        Indicator.Litmus => "ind_litmus",
        Indicator.Phenolphthalein => "ind_phenol",
        Indicator.MethylOrange => "ind_methyl",
        Indicator.Bromothymol => "ind_bromothymol",
        _ => "ind_none",
    };

    internal static Indicator? IndicatorOf(string? id) => Dyes.Cast<Indicator?>().FirstOrDefault(d => IndicatorId(d!.Value) == id);

    /// <summary>The indicator's short name for a label: <c>Acid_Indicator_Universal</c>.</summary>
    internal static string IndicatorShortKey(Indicator indicator) => "Acid_Indicator_" + indicator;
}
