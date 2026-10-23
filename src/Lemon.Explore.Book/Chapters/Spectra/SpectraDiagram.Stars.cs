using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Chapters.BlueSky;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Spectra;

/// <summary>
/// The stars: a star in the colour its temperature gives, its black-body curve with the visible band and the peak (which
/// drags), its spectrum with the dark lines of its temperature, shifted by its motion, over a lab hydrogen lamp's lines.
/// </summary>
public sealed partial class SpectraDiagram
{
    /// <summary>The black-body chart's wavelengths, nm.</summary>
    private const double ChartFrom = 100;

    private const double ChartTo = 2000;

    private StarsView _stars;
    private WriteableBitmap? _starBitmap;
    private byte[] _starPixels = [];
    private WriteableBitmap? _referenceBitmap;
    private byte[] _referencePixels = [];

    private readonly record struct StarsView(Rect Stage, Point Star, double StarRadius, Rect Chart, Rect StarStrip, Rect Reference)
    {
        public double ChartX(double nm) => Chart.X + (Chart.Width * (nm - ChartFrom) / (ChartTo - ChartFrom));

        public double ChartNm(double x) => Math.Clamp(ChartFrom + ((x - Chart.X) / Chart.Width * (ChartTo - ChartFrom)), 60, ChartTo);

        public double X(double nm) => StarStrip.X + (StarStrip.Width * (nm - SpectraModel.VisibleFrom) / (SpectraModel.VisibleTo - SpectraModel.VisibleFrom));

        public double Nm(double x) => SpectraModel.VisibleFrom + ((x - StarStrip.X) / StarStrip.Width * (SpectraModel.VisibleTo - SpectraModel.VisibleFrom));
    }

    private StarsView StarsLayout()
    {
        var stage = Stage();
        var left = stage.Left + 70;
        var width = stage.Width - 130;
        var reference = new Rect(left, stage.Bottom - 50, width, 26);
        var starStrip = new Rect(left, reference.Top - 74, width, 48);
        var top = stage.Top + 6;
        var bandBottom = starStrip.Top - 40;
        var radius = Math.Clamp((bandBottom - top) * 0.3, 30, 70);
        var star = new Point(stage.Left + Math.Max(110, stage.Width * 0.16), (top + bandBottom) / 2);
        var chart = new Rect(stage.Left + (stage.Width * 0.34), top + 14, (stage.Width * 0.66) - 50, Math.Max(60, bandBottom - top - 40));
        return new StarsView(stage, star, radius, chart, starStrip, reference);
    }

    private void DrawStarsMode(PaintContext dc, double now)
    {
        var v = _stars = StarsLayout();
        DrawStar(dc, v, now);
        DrawPlanckChart(dc, v, now);
        DrawStarSpectrum(dc, v);
    }

    private void DrawStar(PaintContext dc, StarsView v, double now)
    {
        var colour = SpectraModel.BodyColour(Kelvin);
        var pulse = 1 + (0.02 * Math.Sin(now * 1.7));
        var glow = SpaceDrawing.RadialGradient((Color.FromArgb(150, colour.R, colour.G, colour.B), 0), (Color.FromArgb(0, colour.R, colour.G, colour.B), 1));
        dc.DrawEllipse(glow, null, v.Star, v.StarRadius * 1.9 * pulse, v.StarRadius * 1.9 * pulse);
        var body = new ImmutableRadialGradientBrush(
        [
            new ImmutableGradientStop(0, Color.FromRgb(255, 255, 255)),
            new ImmutableGradientStop(1, colour),
        ],
            gradientOrigin: new RelativePoint(0.45, 0.4, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.6, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.6, RelativeUnit.Relative));
        dc.DrawEllipse(body, null, v.Star, v.StarRadius, v.StarRadius);
        _targets.Add("star", v.Star, v.StarRadius);
        DrawPill(dc, LocFormat("Spec_Label_Star", Number(Kelvin, "#,0"), SpectraModel.SpectralClass(Kelvin)), new Point(v.Star.X, v.Star.Y + v.StarRadius + 22), colour, 11.5);
    }

    /// <summary>The black-body curve for this temperature: the visible band shaded, the peak marked (drag it).</summary>
    private void DrawPlanckChart(PaintContext dc, StarsView v, double now)
    {
        var box = v.Chart;
        dc.DrawLine(Ink.Faint, new Point(box.Left, box.Bottom), new Point(box.Right, box.Bottom));

        // The visible band.
        var from = v.ChartX(SpectraModel.VisibleFrom);
        var to = v.ChartX(SpectraModel.VisibleTo);
        var rainbow = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative) };
        for (var i = 0; i <= 8; i++)
        {
            var c = PhotonColour(SpectraModel.VisibleFrom + ((SpectraModel.VisibleTo - SpectraModel.VisibleFrom) * i / 8));
            rainbow.GradientStops.Add(new GradientStop(Color.FromArgb(70, c.R, c.G, c.B), i / 8.0));
        }
        dc.DrawRectangle(rainbow, null, new Rect(from, box.Top, to - from, box.Height));
        var visibleLabel = DrawText(dc, Loc("Spec_Label_Visible"), new Point((from + to) / 2, box.Top + 10), Ink.Text, 10.5, TextAnchor.Center);
        AddBoxTarget("visible", visibleLabel.Inflate(new Thickness(4, 3)));
        if (ShowLabels)
        {
            DrawText(dc, Loc("Spec_Label_Ultraviolet"), new Point((box.Left + from) / 2, box.Top + 10), Ink.Violet, 10, TextAnchor.Center);
            DrawText(dc, Loc("Spec_Label_Infrared"), new Point((to + box.Right) / 2, box.Top + 10), Color.FromRgb(0xE0, 0x80, 0x70), 10, TextAnchor.Center);
        }

        var peakNm = SpectraModel.PeakWavelength(Kelvin);
        var max = SpectraModel.Planck(peakNm, Kelvin);
        var points = new List<Point> { new(box.Left, box.Bottom) };
        for (var x = box.Left; x <= box.Right; x += 2)
        {
            var b = SpectraModel.Planck(v.ChartNm(x), Kelvin) / max;
            points.Add(new Point(x, box.Bottom - ((box.Height - 24) * b)));
        }

        points.Add(new Point(box.Right, box.Bottom));
        var colour = SpectraModel.BodyColour(Kelvin);
        var curve = Line(points, closed: true, filled: true);
        if (Glows("curve")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now), curve);
        dc.DrawGeometry(FadedBrush(colour, 0.16), FadedPen(colour, 0.95, 2), curve);
        var mid = points[Math.Min(points.Count - 1, (int)(points.Count * 0.7))];
        _targets.Add("curve", mid, 10);

        // The peak (Wien): drag it to change the temperature.
        if (peakNm >= ChartFrom)
        {
            var x = v.ChartX(peakNm);
            var top = new Point(x, box.Bottom - (box.Height - 24));
            dc.DrawLine(FadedPen(Ink.Gold, 0.8, 1, dashed: true), top, new Point(x, box.Bottom));
            dc.DrawEllipse(Solid(255, 255, 255, 255), MakePen(Solid(255, Ink.Gold.R, Ink.Gold.G, Ink.Gold.B), 2.5), top, 6, 6);
            _targets.Add("peak", top, 10);
            if (ShowLabels) DrawPill(dc, LocFormat("Spec_Label_Peak", Nanometres(peakNm)), new Point(Math.Min(x + 60, box.Right - 50), top.Y + 4), Ink.Gold, 10.5);
        }
        else if (ShowLabels)
        {
            DrawPill(dc, LocFormat("Spec_Label_PeakLeft", Nanometres(peakNm)), new Point(box.Left + 70, box.Top + 30), Ink.Gold, 10.5);
        }

        foreach (var nm in new[] { 500.0, 1000, 1500, 2000 })
        {
            var x = v.ChartX(nm);
            dc.DrawLine(Ink.Tick, new Point(x, box.Bottom), new Point(x, box.Bottom + 4));
            DrawText(dc, Number(nm, "0"), new Point(x, box.Bottom + 12), Ink.Dim, 9.5, TextAnchor.Center);
        }

        DrawText(dc, Loc("Spec_Label_Nm"), new Point(box.Right + 6, box.Bottom + 12), Ink.Dim, 9.5);
    }

    /// <summary>The star's spectrum with its dark lines, shifted by its motion, over a lab hydrogen lamp's bright lines.</summary>
    private void DrawStarSpectrum(PaintContext dc, StarsView v)
    {
        var lines = SpectraModel.StarLines(Kelvin);
        var nmPerPixel = (SpectraModel.VisibleTo - SpectraModel.VisibleFrom) / v.StarStrip.Width;
        var widened = lines.Select(l => l.Band ? l : l with { Width = Math.Max(l.Width, nmPerPixel * 1.2) }).ToList();
        var peak = Enumerable.Range(0, 38).Max(i => SpectraModel.Planck(SpectraModel.VisibleFrom + (i * 10), Kelvin));
        DrawStrip(dc, v.StarStrip, v.Nm,
            nm => SpectraModel.LineLight(nm, 1.15 * SpectraModel.Planck(nm, Kelvin) / peak * SpectraModel.Transmission(widened, nm, Velocity)),
            ref _starBitmap, ref _starPixels);
        dc.DrawRectangle(null, Ink.Frame, v.StarStrip);
        AddBoxTarget("starspectrum", new Rect(v.StarStrip.Right - 90, v.StarStrip.Y, 90, v.StarStrip.Height));

        var hydrogen = SpectraModel.Lines(Gas.Hydrogen);
        DrawStrip(dc, v.Reference, v.Nm, nm => EmissionLight(hydrogen, nm, Math.Max(0.12, nmPerPixel * 1.2), 1.4), ref _referenceBitmap, ref _referencePixels);
        dc.DrawRectangle(null, Ink.Frame, v.Reference);
        var refLabel = DrawText(dc, Loc("Spec_Label_LabHydrogen"), new Point(v.Reference.X - 8, v.Reference.Y + (v.Reference.Height / 2)), Ink.Dim, 10.5, TextAnchor.Right);
        AddBoxTarget("reference", refLabel.Union(new Rect(v.Reference.X, v.Reference.Y, 60, v.Reference.Height)));
        DrawText(dc, Loc("Spec_Label_StarLight"), new Point(v.StarStrip.X - 8, v.StarStrip.Y + (v.StarStrip.Height / 2)), Ink.Dim, 10.5, TextAnchor.Right);

        for (var nm = 400; nm <= 750; nm += 50)
        {
            var x = v.X(nm);
            dc.DrawLine(Ink.Tick, new Point(x, v.Reference.Bottom), new Point(x, v.Reference.Bottom + 4));
            DrawText(dc, Number(nm, "0"), new Point(x, v.Reference.Bottom + 12), Ink.Dim, 9.5, TextAnchor.Center);
        }

        // The families of dark lines present at this temperature, each clickable where it falls.
        var (h, he, _, calcium, _, _, molecules) = SpectraModel.LineFamilies(Kelvin);
        void Mark(string id, double nm, double strength, string key)
        {
            if (strength < 0.15) return;
            var x = v.X(SpectraModel.Shift(nm, Velocity));
            if (x < v.StarStrip.Left || x > v.StarStrip.Right) return;
            dc.DrawLine(FadedPen(Ink.Text, 0.6, 1), new Point(x, v.StarStrip.Top - 3), new Point(x, v.StarStrip.Top - 12));
            var box = ShowLabels ? DrawPill(dc, Loc(key), new Point(x, v.StarStrip.Top - 22), Ink.Text, 10) : new Rect(x - 7, v.StarStrip.Top - 16, 14, 14);
            AddBoxTarget(id, box);
        }

        Mark("balmer", 656.28, h, "Spec_Label_Hydrogen");
        Mark("calcium", 396.85, calcium, "Spec_Label_Calcium");
        Mark("helium", 447.15, he, "Spec_Label_Helium");
        Mark("bands", 516.7, molecules, "Spec_Label_Molecules");

        // The shift: each hydrogen line in the lamp joined to where the star shows it.
        if (Math.Abs(Velocity) < 1) return;
        var colour = Velocity > 0 ? Ink.Red : Ink.Cyan;
        foreach (var line in hydrogen.Take(3))
        {
            var lab = v.X(line.Nm);
            var seen = v.X(SpectraModel.Shift(line.Nm, Velocity));
            dc.DrawLine(FadedPen(colour, 0.8, 1.2, dashed: true), new Point(lab, v.Reference.Top), new Point(seen, v.StarStrip.Bottom));
        }

        var key2 = Velocity > 0 ? "Spec_Label_RedShift" : "Spec_Label_BlueShift";
        var middle = (v.X(SpectraModel.Shift(486.13, Velocity)) + v.X(486.13)) / 2;
        AddBoxTarget("shift", DrawPill(dc, LocFormat(key2, Number(Math.Abs(Velocity), "#,0")), new Point(middle, (v.StarStrip.Bottom + v.Reference.Top) / 2), colour, 10.5));
    }
}
