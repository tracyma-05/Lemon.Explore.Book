using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.StarLife;

/// <summary>
/// The diagram view: the Hertzsprung–Russell diagram, surface temperature across (hot on the left, as astronomers draw
/// it) and luminosity up, both on log scales. Most stars sit on the main sequence; giants above and to the right,
/// white dwarfs below and to the left. The star's whole track is drawn faintly and the part it has lived brightly,
/// with the star moving along it; dashed lines join stars of the same size.
/// </summary>
public sealed partial class StarDiagram
{
    private const double HotLog = 4.7;   // 50,000 K
    private const double CoolLog = 3.36; // about 2,300 K
    private const double DimLog = -5;
    private const double BrightLog = 6.5;

    private Rect _plot;

    private double HrX(double logT) => _plot.Left + (_plot.Width * (HotLog - logT) / (HotLog - CoolLog));

    private double HrY(double logL) => _plot.Bottom - (_plot.Height * (logL - DimLog) / (BrightLog - DimLog));

    private Point HrPoint(double logT, double logL) => new(HrX(logT), HrY(logL));

    private void DrawHr(PaintContext dc, IReadOnlyList<StarStageSpan> track, StarState state, double now)
    {
        _plot = new Rect(96, 150, Math.Max(200, ActualWidth - 96 - 40), Math.Max(150, ActualHeight - 150 - 230 - _reserve.Current));
        dc.DrawRectangle(HrPaint.PlotBack, HrPaint.Frame, _plot);

        // Star colours along the bottom: what each temperature looks like.
        var band = new Rect(_plot.Left, _plot.Bottom + 1, _plot.Width, 6);
        var stops = new (Color, double)[11];
        for (var i = 0; i <= 10; i++)
        {
            var logT = HotLog - ((HotLog - CoolLog) * i / 10);
            stops[i] = (StarModel.ColorOf(Math.Pow(10, logT)), i / 10.0);
        }

        var colours = SpaceDrawing.LinearGradient(0, stops);
        dc.DrawRectangle(colours, null, band);

        DrawHrAxes(dc);
        dc.PushClip(new RectangleGeometry(_plot));
        if (ShowRadiusLines) DrawRadiusLines(dc);
        DrawRegions(dc);
        if (ShowKnownStars) DrawKnownStars(dc);
        DrawTrack(dc, track, state, now);
        dc.Pop();
        DrawTrackEnds(dc, track, state);
    }

    private void DrawHrAxes(PaintContext dc)
    {
        foreach (var (logL, text) in new[] { (-4.0, "10⁻⁴"), (-2.0, "0.01"), (0.0, "1"), (2.0, "100"), (4.0, "10⁴"), (6.0, "10⁶") })
        {
            var y = HrY(logL);
            dc.DrawLine(HrPaint.Grid, new Point(_plot.Left, y), new Point(_plot.Right, y));
            var label = _labels.Text(text, 11, StarDrawing.DimText);
            dc.DrawText(label, new Point(_plot.Left - label.Width - 8, y - (label.Height / 2)));
        }

        foreach (var kelvin in new[] { 40000, 20000, 10000, 6000, 4000, 3000 })
        {
            var x = HrX(Math.Log10(kelvin));
            dc.DrawLine(HrPaint.Grid, new Point(x, _plot.Top), new Point(x, _plot.Bottom));
            var label = _labels.Text(kelvin.ToString("N0", CultureInfo.InvariantCulture), 11, StarDrawing.DimText);
            dc.DrawText(label, new Point(x - (label.Width / 2), _plot.Bottom + 10));
        }

        var xTitle = _labels.Get("Star_Hr_AxisT", 11.5, StarDrawing.DimText);
        dc.DrawText(xTitle, new Point(_plot.Right - xTitle.Width, _plot.Bottom + 26));
        var yTitle = _labels.Get("Star_Hr_AxisL", 11.5, StarDrawing.DimText);
        dc.PushTransform(SpaceDrawing.Rotation(-90, 28, _plot.Top + (_plot.Height / 2)));
        dc.DrawText(yTitle, new Point(28 - (yTitle.Width / 2), _plot.Top + (_plot.Height / 2) - (yTitle.Height / 2)));
        dc.Pop();
    }

    /// <summary>Stars of one size lie on a straight line: L = R² (T / T☉)⁴.</summary>
    private void DrawRadiusLines(PaintContext dc)
    {
        foreach (var (radius, key) in new[] { (0.01, "0.01"), (0.1, "0.1"), (1.0, "1"), (10.0, "10"), (100.0, "100"), (1000.0, "1000") })
        {
            var a = HrPoint(HotLog, Math.Log10(StarModel.LuminosityOf(radius, Math.Pow(10, HotLog))));
            var b = HrPoint(CoolLog, Math.Log10(StarModel.LuminosityOf(radius, Math.Pow(10, CoolLog))));
            dc.DrawLine(HrPaint.RadiusLine, a, b);

            // Labelled where the line crosses the 5,000 K line, inside the plot.
            var at = HrPoint(Math.Log10(5000), Math.Log10(StarModel.LuminosityOf(radius, 5000)));
            if (at.Y < _plot.Top + 10 || at.Y > _plot.Bottom - 10) at = HrPoint(Math.Log10(25000), Math.Log10(StarModel.LuminosityOf(radius, 25000)));
            if (at.Y < _plot.Top + 10 || at.Y > _plot.Bottom - 10) continue;
            var text = _labels.Text(LocFormat("Star_Hr_Radius", key), 10, HrPaint.RadiusText);
            dc.DrawText(text, new Point(at.X + 4, at.Y - text.Height - 2));
        }
    }

    /// <summary>The main sequence band and where giants, supergiants and white dwarfs gather, named.</summary>
    private void DrawRegions(PaintContext dc)
    {
        var band = new StreamGeometry();
        using (var ctx = band.Open())
        {
            var first = true;
            for (var logM = Math.Log10(StarModel.MinMass); logM <= Math.Log10(StarModel.MaxMass) + 1e-9; logM += 0.05)
            {
                var mass = Math.Pow(10, logM);
                var point = HrPoint(Math.Log10(StarModel.ZamsTemperature(mass)), Math.Log10(StarModel.ZamsLuminosity(mass)) + 0.25);
                if (first) ctx.BeginFigure(point, false);
                else ctx.LineTo(point);
                first = false;
            }

            ctx.EndFigure(false);
        }
        dc.DrawGeometry(null, HrPaint.MainSequence, band);

        var ms = HrPoint(Math.Log10(12000), 1.0);
        _labels.DrawPill(dc, "Star_Hr_MainSequence", new Point(ms.X + 26, ms.Y + 10), 11.5, StarDrawing.Text);
        var msText = _labels.Get("Star_Hr_MainSequence", 11.5, StarDrawing.Text);
        _targets.AddBox("mainsequence", new Rect(ms.X + 26, ms.Y + 10, msText.Width + 12, msText.Height + 4));

        var giants = HrPoint(Math.Log10(4300), 2.2);
        dc.DrawEllipse(HrPaint.Region, null, giants, _plot.Width * 0.09, _plot.Height * 0.09);
        _labels.DrawCentered(dc, "Star_Hr_Giants", new Point(giants.X, giants.Y + (_plot.Height * 0.09) + 2), 11.5, StarDrawing.DimText);
        var giantsText = _labels.Get("Star_Hr_Giants", 11.5, StarDrawing.DimText);
        AddTextTarget("giants", giantsText, new Point(giants.X - (giantsText.Width / 2), giants.Y + (_plot.Height * 0.09) + 2));

        var supergiants = HrPoint(Math.Log10(19000), 5.0);
        dc.DrawRoundedRectangle(HrPaint.Region, null, new Rect(HrX(Math.Log10(30000)), HrY(5.6), HrX(Math.Log10(3200)) - HrX(Math.Log10(30000)), HrY(4.5) - HrY(5.6)), 18, 18);
        _labels.DrawCentered(dc, "Star_Hr_Supergiants", new Point(supergiants.X, HrY(5.6) + 4), 11.5, StarDrawing.DimText);
        var superText = _labels.Get("Star_Hr_Supergiants", 11.5, StarDrawing.DimText);
        AddTextTarget("supergiants", superText, new Point(supergiants.X - (superText.Width / 2), HrY(5.6) + 4));

        var dwarfs = HrPoint(Math.Log10(14000), -2.8);
        dc.DrawEllipse(HrPaint.Region, null, dwarfs, _plot.Width * 0.1, _plot.Height * 0.07);
        _labels.DrawCentered(dc, "Star_Hr_WhiteDwarfs", new Point(dwarfs.X, dwarfs.Y + (_plot.Height * 0.07) + 2), 11.5, StarDrawing.DimText);
        var dwarfText = _labels.Get("Star_Hr_WhiteDwarfs", 11.5, StarDrawing.DimText);
        AddTextTarget("whitedwarfs", dwarfText, new Point(dwarfs.X - (dwarfText.Width / 2), dwarfs.Y + (_plot.Height * 0.07) + 2));
    }

    private void DrawKnownStars(PaintContext dc)
    {
        foreach (var star in StarModel.KnownStars)
        {
            var at = HrPoint(Math.Log10(star.Temperature), Math.Log10(star.Luminosity));
            var sun = star.Id == "Sun";
            dc.DrawEllipse(new SolidColorBrush(StarModel.ColorOf(star.Temperature)), StarDrawing.DotOutline, at, sun ? 5 : 4, sun ? 5 : 4);
            var name = _labels.Get($"Star_Known_{star.Id}", 10.5, sun ? StarDrawing.Gold : StarDrawing.DimText, sun);
            dc.DrawText(name, new Point(at.X + 7, at.Y - (name.Height / 2)));
            _targets.Add(KnownPrefix + star.Id, at, 6);
        }
    }

    /// <summary>The track: all of it faint, the part lived bright, and the star where it is now.</summary>
    private void DrawTrack(PaintContext dc, IReadOnlyList<StarStageSpan> track, StarState state, double now)
    {
        var mass = Mass;
        var lived = new StreamGeometry();
        var whole = new StreamGeometry();
        using (var all = whole.Open())
        using (var done = lived.Open())
        {
            var startedAll = false;
            var startedDone = false;
            var story = 0.0;
            for (var i = 0; i < track.Count; i++)
            {
                var span = track[i];
                if (span.Stage is StarStage.Supernova or StarStage.NeutronStar or StarStage.BlackHole) break;
                const int steps = 40;
                for (var k = 0; k <= steps; k++)
                {
                    var s = story + (span.Seconds * k / steps);
                    var point = StatePoint(StarModel.At(track, mass, Math.Min(s, story + span.Seconds - 1e-9)));
                    if (!startedAll) all.BeginFigure(point, false);
                    else all.LineTo(point);
                    startedAll = true;

                    if (s <= _story)
                    {
                        if (!startedDone) done.BeginFigure(point, false);
                        else done.LineTo(point);
                        startedDone = true;
                    }
                }

                story += span.Seconds;
            }

            if (startedDone && state.Stage is not (StarStage.Supernova or StarStage.NeutronStar or StarStage.BlackHole))
            {
                done.LineTo(StatePoint(state));
            }

            if (startedDone) done.EndFigure(false);
            if (startedAll) all.EndFigure(false);
        }
        if (Glows("track")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now), whole);
        dc.DrawGeometry(null, HrPaint.TrackAll, whole);
        dc.DrawGeometry(null, HrPaint.TrackLived, lived);

        if (state.Stage is StarStage.Supernova or StarStage.NeutronStar or StarStage.BlackHole) return;

        var at = StatePoint(state);
        var color = StarModel.ColorOf(state.Temperature);
        var pulse = 1 + (0.15 * Math.Sin(now * 4));
        var glow = SpaceDrawing.RadialGradient((WithAlpha(color, 0.7), 0), (WithAlpha(color, 0), 1));
        dc.DrawEllipse(glow, null, at, 18 * pulse, 18 * pulse);
        dc.DrawEllipse(new SolidColorBrush(color), StarDrawing.DotOutline, at, 7, 7);
        _targets.Add(StageTarget(state.Stage), at, 9);
    }

    /// <summary>Off the chart: the supernova far above it, and the remnant that leaves it altogether.</summary>
    private void DrawTrackEnds(PaintContext dc, IReadOnlyList<StarStageSpan> track, StarState state)
    {
        var last = track.LastOrDefault(s => s.Stage == StarStage.LateBurning);
        if (last is null) return;

        var from = HrPoint(last.LogT1, last.LogL1);
        var top = new Point(from.X, _plot.Top + 4);
        var exploding = state.Stage is StarStage.Supernova or StarStage.NeutronStar or StarStage.BlackHole;
        dc.DrawLine(exploding ? HrPaint.BlastLine : HrPaint.TrackAll, from, top);
        DrawArrowHead(dc, exploding ? HrPaint.BlastBrush : HrPaint.ArrowBrush, top, new Vector(0, -1), 9);
        if (!exploding) return;

        var text = state.Stage == StarStage.Supernova
            ? LocFormat("Star_Hr_Supernova", StarFormat.Times(state.Luminosity))
            : Loc(state.Stage == StarStage.NeutronStar ? "Star_Hr_NeutronStar" : "Star_Hr_BlackHole");
        var label = _labels.Text(text, 12, StarDrawing.Blast);
        _labels.DrawTextPill(dc, text, new Point(Math.Max(_plot.Left + 4, top.X - label.Width - 24), top.Y + 8), 12, StarDrawing.Blast);
    }

    private Point StatePoint(StarState state) =>
        HrPoint(Math.Log10(Math.Max(state.Temperature, 1)), Math.Log10(Math.Max(state.Luminosity, 1e-9)));

    private static class HrPaint
    {
        public static readonly IBrush PlotBack = Solid(150, 4, 6, 12);
        public static readonly IPen Frame = MakePen(Solid(60, 255, 255, 255), 1);
        public static readonly IPen Grid = MakePen(Solid(22, 255, 255, 255), 1);
        public static readonly IPen RadiusLine = MakePen(Solid(45, 0xB8, 0xC7, 0xFF), 1, dashed: true);
        public static readonly Color RadiusText = Color.FromArgb(150, 0xB8, 0xC7, 0xFF);
        public static readonly IPen MainSequence = new ImmutablePen(((IBrush)Solid(38, 255, 255, 255)).ToImmutable(), 30, null, PenLineCap.Round, PenLineJoin.Round);
        public static readonly IBrush Region = Solid(20, 255, 255, 255);
        public static readonly IPen TrackAll = MakePen(Solid(90, 255, 255, 255), 1.4, dashed: true);
        public static readonly IPen TrackLived = MakePen(Solid(240, 0xFF, 0xD3, 0x6B), 2.4);
        public static readonly IPen BlastLine = MakePen(Solid(230, 0xFF, 0x9A, 0x5A), 2.4);
        public static readonly IBrush BlastBrush = Solid(255, 0xFF, 0x9A, 0x5A);
        public static readonly IBrush ArrowBrush = Solid(120, 255, 255, 255);
    }
}
