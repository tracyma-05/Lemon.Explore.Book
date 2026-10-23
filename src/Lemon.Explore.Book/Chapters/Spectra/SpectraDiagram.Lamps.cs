using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Chapters.BlueSky;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Spectra;

/// <summary>
/// The lamps: a gas lamp, a bulb, the Sun or a bulb seen through a cool gas, its light through a slit and a prism, the
/// colours fanning out onto the screen below, how bright each wavelength is, and the spectrum itself; a second gas to
/// compare with and a magnified look at one line.
/// </summary>
public sealed partial class SpectraDiagram
{
    /// <summary>How many times the magnifier enlarges.</summary>
    private const double ZoomFactor = 40;

    private LampsView _lamps;
    private WriteableBitmap? _lampBitmap;
    private byte[] _lampPixels = [];
    private WriteableBitmap? _compareBitmap;
    private byte[] _comparePixels = [];
    private WriteableBitmap? _zoomBitmap;
    private byte[] _zoomPixels = [];

    private readonly record struct LampsView(Rect Stage, Rect Bench, Rect Trace, Rect Strip, Rect Compare, Rect ZoomBox)
    {
        public double X(double nm) => Strip.X + (Strip.Width * (nm - SpectraModel.VisibleFrom) / (SpectraModel.VisibleTo - SpectraModel.VisibleFrom));

        public double Nm(double x) => SpectraModel.VisibleFrom + ((x - Strip.X) / Strip.Width * (SpectraModel.VisibleTo - SpectraModel.VisibleFrom));

        /// <summary>How many nanometres one pixel of the strip covers.</summary>
        public double NmPerPixel => (SpectraModel.VisibleTo - SpectraModel.VisibleFrom) / Math.Max(1, Strip.Width);
    }

    private LampsView LampsLayout()
    {
        var stage = Stage();
        var left = stage.Left + 60;
        var width = stage.Width - 120;
        var compare = CompareGas is not null;
        var stripBottom = stage.Bottom - 30 - (compare ? 44 : 0);
        var strip = new Rect(left, stripBottom - 60, width, 60);
        var trace = new Rect(left, strip.Top - 74, width, 66);
        var bench = new Rect(stage.Left, stage.Top, stage.Width, Math.Max(80, trace.Top - stage.Top - 6));
        var compareBox = compare ? new Rect(left, strip.Bottom + 22, width, 22) : default(Rect);
        var zoom = new Rect(stage.Right - 300, bench.Top + 6, 270, 64);
        return new LampsView(stage, bench, trace, strip, compareBox, zoom);
    }

    /// <summary>The lines the spectrum shows (bright or dark), for picking the nearest with a click.</summary>
    private IReadOnlyList<SpectralLine> ShownLines() => Source switch
    {
        LightSource.Lamp or LightSource.ThroughGas => SpectraModel.Lines(Gas),
        LightSource.Sun => SpectraModel.SunLines.Select(l => new SpectralLine(l.Nm, l.Depth)).ToList(),
        _ => [],
    };

    /// <summary>How bright the light is at a wavelength (about 0–1.5) for the source shown, lines at least a pixel wide.</summary>
    private double Brightness(double nm, double minWidth)
    {
        switch (Source)
        {
            case LightSource.Lamp:
            {
                var total = 0.0;
                var width = Math.Max(0.02, minWidth);
                foreach (var line in SpectraModel.Lines(Gas))
                {
                    var d = (nm - line.Nm) / width;
                    if (Math.Abs(d) < 5) total += line.Strength * Math.Exp(-0.5 * d * d);
                }

                return 1.4 * total;
            }

            case LightSource.Bulb:
                return BulbLight(nm);

            case LightSource.Sun:
            {
                var lines = SpectraModel.SunLines.Select(l => l with { Width = Math.Max(l.Width * 0.5, minWidth) });
                return SunLight(nm) * SpectraModel.Transmission(lines, nm);
            }

            default:
            {
                // Only the strong lines: the ones the gas's atoms are ready to absorb.
                var lines = SpectraModel.Lines(Gas).Where(l => l.Strength >= 0.3).Select(l => new Absorption(l.Nm, 0.92 * Math.Pow(l.Strength, 0.35), Math.Max(0.02, minWidth)));
                return BulbLight(nm) * SpectraModel.Transmission(lines, nm);
            }
        }
    }

    private static double BulbLight(double nm) => 0.95 * SpectraModel.Planck(nm, SpectraModel.BulbKelvin) / SpectraModel.Planck(SpectraModel.VisibleTo, SpectraModel.BulbKelvin);

    private static double SunLight(double nm) => 0.9 * SpectraModel.Planck(nm, SpectraModel.SunKelvin) / SpectraModel.Planck(SpectraModel.PeakWavelength(SpectraModel.SunKelvin), SpectraModel.SunKelvin);

    private LinearRgb LightAt(double nm, double minWidth) => Source == LightSource.Lamp
        ? EmissionLight(SpectraModel.Lines(Gas), nm, Math.Max(0.02, minWidth), 1.4)
        : SpectraModel.LineLight(nm, Brightness(nm, minWidth));

    private void DrawLampsMode(PaintContext dc, double now)
    {
        var v = _lamps = LampsLayout();
        DrawBench(dc, v, now);
        DrawTrace(dc, v);

        var px = v.NmPerPixel * 1.2;
        DrawStrip(dc, v.Strip, x => v.Nm(x), nm => LightAt(nm, px), ref _lampBitmap, ref _lampPixels);
        dc.DrawRectangle(null, Ink.Frame, v.Strip);
        DrawRuler(dc, v);

        if (CompareGas is { } other)
        {
            var lines = SpectraModel.Lines(other);
            DrawStrip(dc, v.Compare, x => v.Nm(x), nm => EmissionLight(lines, nm, Math.Max(0.12, px), 1.4), ref _compareBitmap, ref _comparePixels);
            dc.DrawRectangle(null, Ink.Frame, v.Compare);
            var name = DrawText(dc, Loc(GasKey(other)), new Point(v.Compare.X - 8, v.Compare.Y + (v.Compare.Height / 2)), Ink.Text, 11, TextAnchor.Right);
            AddBoxTarget("compare", name.Union(v.Compare));
        }

        DrawSelectedLine(dc, v);
        if (Zoom && !double.IsNaN(SelectedLine)) DrawZoom(dc, v);
    }

    /// <summary>The light's source, the slit, the prism and the colours fanning out to the screen.</summary>
    private void DrawBench(PaintContext dc, LampsView v, double now)
    {
        var y = v.Bench.Top + Math.Min(70, v.Bench.Height * 0.4);
        var lampX = v.Stage.Left + 70;
        var slitX = lampX + 110;
        // (on a narrow page a share of the width would put the prism on the slit)
        var prism = new Point(Math.Max(slitX + 85, v.Stage.Left + (v.Stage.Width * 0.32)), y);
        var fanY = v.Bench.Bottom - 4;

        // The beam: from the source through the slit to the prism.
        var beamColour = Source == LightSource.Lamp ? SpectraModel.GlowColour(Gas) : Color.FromRgb(0xFF, 0xF2, 0xD8);
        dc.DrawLine(FadedPen(beamColour, 0.6, 3), new Point(lampX + 20, y), new Point(prism.X - 20, y));

        // The colours leaving the prism, each landing above its place on the screen.
        var exit = new Point(prism.X + 16, y + 8);
        if (Source == LightSource.Lamp)
        {
            foreach (var line in SpectraModel.Lines(Gas))
            {
                dc.DrawLine(FadedPen(PhotonColour(line.Nm), 0.25 + (0.6 * line.Strength), 1.4), exit, new Point(v.X(line.Nm), fanY));
            }
        }
        else
        {
            for (var nm = SpectraModel.VisibleFrom + 3; nm < SpectraModel.VisibleTo; nm += 6)
            {
                var b = Math.Clamp(Brightness(nm, 3), 0, 1);
                if (b < 0.08) continue;
                dc.DrawLine(FadedPen(PhotonColour(nm), 0.15 + (0.45 * b), 1.4), exit, new Point(v.X(nm), fanY));
            }
        }

        // The prism.
        var prismShape = Polygon(new Point(prism.X, y - 34), new Point(prism.X - 30, y + 24), new Point(prism.X + 30, y + 24));
        dc.DrawGeometry(FadedBrush(Ink.Glass, 0.22), FadedPen(Ink.Glass, 0.9, 1.4), prismShape);
        AddBoxTarget("prism", new Rect(prism.X - 30, y - 34, 60, 58));

        // The slit: a dark plate with a narrow gap.
        var plate = Solid(255, 0x2A, 0x2E, 0x38);
        dc.DrawRectangle(plate, null, new Rect(slitX - 3, y - 40, 6, 37));
        dc.DrawRectangle(plate, null, new Rect(slitX - 3, y + 3, 6, 37));
        AddBoxTarget("slit", new Rect(slitX - 8, y - 40, 16, 80));

        DrawSource(dc, new Point(lampX, y), slitX, now);

        if (!ShowLabels) return;
        DrawPill(dc, Loc("Spec_Label_Slit"), new Point(slitX, y + 54), Ink.Dim, 10.5);
        DrawPill(dc, Loc("Spec_Label_Prism"), new Point(prism.X, y + 42), Ink.Glass, 10.5);
        DrawText(dc, Loc("Spec_Label_Screen"), new Point(v.Trace.Right, v.Bench.Bottom - 12), Ink.Dim, 10.5, TextAnchor.Right);
    }

    private void DrawSource(PaintContext dc, Point at, double slitX, double now)
    {
        switch (Source)
        {
            case LightSource.Lamp:
            {
                var colour = SpectraModel.GlowColour(Gas);
                var glow = SpaceDrawing.RadialGradient((Color.FromArgb(130, colour.R, colour.G, colour.B), 0), (Color.FromArgb(0, colour.R, colour.G, colour.B), 1));
                var flicker = 1 + (0.03 * Math.Sin(now * 9));
                dc.DrawEllipse(glow, null, at, 46 * flicker, 46 * flicker);
                var tube = new Rect(at.X - 9, at.Y - 36, 18, 72);
                dc.DrawRoundedRectangle(Solid(255, colour.R, colour.G, colour.B), MakePen(Solid(200, 255, 255, 255), 1), tube, 9, 9);
                dc.DrawRectangle(Solid(255, 0x60, 0x66, 0x70), null, new Rect(at.X - 6, at.Y - 44, 12, 8));
                dc.DrawRectangle(Solid(255, 0x60, 0x66, 0x70), null, new Rect(at.X - 6, at.Y + 36, 12, 8));
                var id = Mystery ? "mystery" : "gaslamp";
                var label = Mystery ? Loc("Spec_Label_Mystery") : Loc(GasKey(Gas));
                var box = ShowLabels || Mystery ? DrawPill(dc, label, new Point(at.X, at.Y + 58), Mystery ? Ink.Gold : Ink.Text, 11) : tube;
                AddBoxTarget(id, tube.Union(box));
                break;
            }

            case LightSource.Sun:
            {
                var glow = SpaceDrawing.RadialGradient((Color.FromArgb(200, 0xFF, 0xF0, 0xB0), 0), (Color.FromArgb(0, 0xFF, 0xD0, 0x60), 1));
                dc.DrawEllipse(glow, null, at, 48, 48);
                dc.DrawEllipse(Solid(255, 0xFF, 0xF4, 0xC8), null, at, 22, 22);
                _targets.Add("sun", at, 24);
                if (ShowLabels) DrawPill(dc, Loc("Spec_Label_Sun"), new Point(at.X, at.Y + 54), Ink.Gold, 11);
                break;
            }

            default:
            {
                // A bulb, and for "through a gas" a cell of cool gas between it and the slit.
                var glow = SpaceDrawing.RadialGradient((Color.FromArgb(150, 0xFF, 0xD8, 0x9A), 0), (Color.FromArgb(0, 0xFF, 0xB0, 0x60), 1));
                dc.DrawEllipse(glow, null, at, 42, 42);
                dc.DrawEllipse(Solid(255, 0xFF, 0xE8, 0xB8), MakePen(Solid(255, 0xFF, 0xFF, 0xFF), 1), at, 16, 18);
                dc.DrawRectangle(Solid(255, 0x8A, 0x8E, 0x96), null, new Rect(at.X - 8, at.Y + 16, 16, 12));
                _targets.Add("bulb", at, 20);
                if (ShowLabels) DrawPill(dc, Loc("Spec_Label_Bulb"), new Point(at.X, at.Y + 46), Ink.Gold, 11);
                if (Source == LightSource.ThroughGas)
                {
                    var cellX = (at.X + slitX) / 2 + 4;
                    var cell = new Rect(cellX - 16, at.Y - 26, 32, 52);
                    var tint = SpectraModel.GlowColour(Gas);
                    dc.DrawRoundedRectangle(FadedBrush(tint, 0.18), FadedPen(Ink.Glass, 0.7, 1.2), cell, 6, 6);
                    AddBoxTarget("coolgas", cell);
                    if (ShowLabels) DrawPill(dc, LocFormat("Spec_Label_CoolGas", Loc(GasKey(Gas))), new Point(cellX, cell.Top - 14), Ink.Glass, 10.5);
                }

                break;
            }
        }
    }

    /// <summary>How bright each wavelength is, as a graph over the strip (peaks for bright lines, dips for dark ones).</summary>
    private void DrawTrace(PaintContext dc, LampsView v)
    {
        var box = v.Trace;
        dc.DrawLine(Ink.Faint, new Point(box.Left, box.Bottom), new Point(box.Right, box.Bottom));
        var px = v.NmPerPixel * 1.2;
        var points = new List<Point>();
        var values = new List<double>();
        for (var x = box.Left; x <= box.Right; x += 1)
        {
            values.Add(Brightness(v.Nm(x), px));
        }

        var max = Math.Max(1, values.Max());
        for (var i = 0; i < values.Count; i++) points.Add(new Point(box.Left + i, box.Bottom - (box.Height * Math.Min(1, values[i] / max))));
        points.Add(new Point(box.Right, box.Bottom));
        points.Insert(0, new Point(box.Left, box.Bottom));
        dc.DrawGeometry(FadedBrush(Ink.Text, 0.08), FadedPen(Ink.Text, 0.75, 1.2), Line(points, closed: true, filled: true));
        AddBoxTarget("trace", new Rect(box.Left, box.Top, 70, box.Height));
        if (ShowLabels) DrawText(dc, Loc("Spec_Label_Brightness"), new Point(box.Left + 4, box.Top + 8), Ink.Dim, 10.5);
    }

    private void DrawRuler(PaintContext dc, LampsView v)
    {
        var y = v.Strip.Bottom;
        Rect? labels = null;
        for (var nm = 400; nm <= 750; nm += 50)
        {
            var x = v.X(nm);
            dc.DrawLine(Ink.Tick, new Point(x, y), new Point(x, y + 5));
            var r = DrawText(dc, Number(nm, "0"), new Point(x, y + 13), Ink.Dim, 10, TextAnchor.Center);
            labels = labels is { } l ? l.Union(r) : r;
        }

        var unit = DrawText(dc, Loc("Spec_Label_Nm"), new Point(v.Strip.Right + 8, v.Strip.Y + (v.Strip.Height / 2)), Ink.Dim, 10);
        if (labels is { } all) AddBoxTarget("ruler", all.Union(unit));
    }

    /// <summary>The picked line: a marker through the graph and the strip, with its wavelength.</summary>
    private void DrawSelectedLine(PaintContext dc, LampsView v)
    {
        if (double.IsNaN(SelectedLine) || SelectedLine < SpectraModel.VisibleFrom || SelectedLine > SpectraModel.VisibleTo) return;
        var x = v.X(SelectedLine);
        dc.DrawLine(FadedPen(Ink.Text, 0.8, 1, dashed: true), new Point(x, v.Trace.Top), new Point(x, v.Strip.Bottom));
        dc.DrawGeometry(Solid(255, 255, 255, 255), null, Polygon(new Point(x, v.Strip.Top - 1), new Point(x - 6, v.Strip.Top - 10), new Point(x + 6, v.Strip.Top - 10)));
        var id = Source == LightSource.Lamp ? "line" : "darkline";
        AddBoxTarget(id, DrawPill(dc, Nanometres(SelectedLine), new Point(x, v.Trace.Top - 10), PhotonColour(SelectedLine), 11));
    }

    /// <summary>A few nanometres round the picked line, magnified: sodium's yellow line turns out to be two.</summary>
    private void DrawZoom(PaintContext dc, LampsView v)
    {
        var box = v.ZoomBox;
        var frame = new Rect(box.X - 8, box.Y - 22, box.Width + 16, box.Height + 46);
        DrawFrame(dc, frame);
        var nmPerPixel = v.NmPerPixel / ZoomFactor;
        var span = box.Width * nmPerPixel;
        var from = SelectedLine - (span / 2);
        DrawStrip(dc, box, x => from + ((x - box.X) * nmPerPixel), nm => LightAt(nm, nmPerPixel * 1.5), ref _zoomBitmap, ref _zoomPixels);
        dc.DrawRectangle(null, Ink.Frame, box);
        for (var nm = Math.Ceiling(from * 2) / 2; nm <= from + span; nm += 0.5)
        {
            var x = box.X + ((nm - from) / nmPerPixel);
            var whole = Math.Abs(nm - Math.Round(nm)) < 1e-6;
            dc.DrawLine(Ink.Tick, new Point(x, box.Bottom), new Point(x, box.Bottom + (whole ? 5 : 3)));
            if (whole) DrawText(dc, Number(nm, "0"), new Point(x, box.Bottom + 13), Ink.Dim, 9.5, TextAnchor.Center);
        }

        DrawText(dc, LocFormat("Spec_Label_Zoom", Number(ZoomFactor, "0")), new Point(frame.X + 10, box.Y - 11), Ink.Text, 11);
        AddBoxTarget("zoom", frame);
    }

    /// <summary>A gas's name, by its resource key.</summary>
    internal static string GasKey(Gas gas) => gas switch
    {
        Gas.Helium => "Spec_Gas_Helium",
        Gas.Neon => "Spec_Gas_Neon",
        Gas.Sodium => "Spec_Gas_Sodium",
        Gas.Mercury => "Spec_Gas_Mercury",
        _ => "Spec_Gas_Hydrogen",
    };
}
