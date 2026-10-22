using Avalonia;
using Lemon.Explore.Book.Controls.Explore;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.IceAges;

/// <summary>
/// The sunshine view: the northern half of the globe from above the pole with its ice sheets and summer sea ice for the
/// time shown, the 65° N circle where the summer sunshine is measured, and beside it the midsummer sunshine at 65° N and
/// the ice it grows over 800,000 years (with the model's states, the ends of the big glacials and the future).
/// </summary>
public sealed partial class IceAgeDiagram
{
    private readonly PolarIceMap _map = new();

    /// <summary>Named moments marked on the ice chart: thousand years from 1950 and the label's key.</summary>
    private static readonly (double Kyr, string Key)[] Moments =
    [
        (-21, "Ice_Moment_Lgm"),
        (-125, "Ice_Moment_Eemian"),
        (-410, "Ice_Moment_Mis11"),
    ];

    /// <summary>The sunshine view's parts: the globe, the sunshine chart and the ice chart.</summary>
    private readonly record struct SunshineLayout(Rect Globe, Rect Sun, Rect Ice);

    private SunshineLayout LayOutSunshine()
    {
        var stage = Stage();
        var area = new Rect(stage.Left + 24, stage.Top + 4, Math.Max(100, stage.Width - 48), Math.Max(120, stage.Height - 14));
        var size = Math.Max(120, Math.Min(area.Height - 8, area.Width * 0.46));
        var globe = new Rect(area.Left, area.Top + ((area.Height - size) / 2), size, size);
        var left = globe.Right + 56;
        var width = Math.Max(120, area.Right - left - 34);
        var each = Math.Max(60, (area.Height - 76) / 2);
        var sun = new Rect(left, area.Top + 22, width, each);
        var ice = new Rect(left, sun.Bottom + 44, width, each);
        return new SunshineLayout(globe, sun, ice);
    }

    private void DrawSunshineMode(PaintContext dc, double now)
    {
        var layout = LayOutSunshine();
        var moment = IceRecord.Instance.At(TimeKa);
        DrawPolarMap(dc, layout.Globe, moment, now);
        DrawSunshineChart(dc, layout.Sun, moment);
        DrawIceChart(dc, layout.Ice, moment);

        // The time now, through both charts.
        var x = TimeX(layout.Sun, Math.Clamp(TimeKa, IceRecord.Start, IceRecord.End));
        dc.DrawLine(FadedPen(Ink.Text, 0.85, 1.4), new Point(x, layout.Sun.Top - 4), new Point(x, layout.Ice.Bottom + 4));
        var label = TimeText(TimeKa);
        var half = (_labels.Text(label, 11, Ink.Text).Width / 2) + 6;
        DrawPill(dc, label, new Point(Math.Clamp(x, layout.Sun.Left + half, layout.Sun.Right - half), layout.Sun.Bottom + 22), Ink.Text, 11);
    }

    #region the globe

    private void DrawPolarMap(PaintContext dc, Rect globe, IceMoment moment, double now)
    {
        var dpi = RenderScale();
        var bitmap = _map.Render((int)Math.Round(globe.Width * dpi), moment.IceShare);
        dc.DrawImage(bitmap, globe);

        var center = new Point(globe.Left + (globe.Width / 2), globe.Top + (globe.Height / 2));
        var radius = globe.Width / 2;
        Point Map(double lat, double lon) => center + (PolarIceMap.Project(lat, lon) * radius);

        // Latitude circles every 15° and meridians every 30°; the 65° N circle stands out.
        foreach (var lat in new[] { 45.0, 60, 75 })
        {
            var r = (90 - lat) / (90 - PolarIceMap.EdgeLatitude) * radius;
            dc.DrawEllipse(null, Ink.Faint, center, r, r);
        }

        for (var lon = -180; lon < 180; lon += 30) dc.DrawLine(Ink.Grid, center, Map(PolarIceMap.EdgeLatitude, lon));
        dc.DrawEllipse(null, FadedPen(Ink.Text, 0.35, 1.2), center, radius, radius);

        var r65 = (90 - IceAgeModel.KeyLatitude) / (90 - PolarIceMap.EdgeLatitude) * radius;
        var glow = Glows("ring65");
        if (glow) dc.DrawEllipse(null, ExploreTargets.GlowPen(now, 8), center, r65, r65);
        dc.DrawEllipse(null, FadedPen(Ink.Gold, 0.9, 1.8, dashed: true), center, r65, r65);
        foreach (var lon in new[] { -150.0, -60, 30, 120 }) _targets.Add("ring65", Map(IceAgeModel.KeyLatitude, lon), 9);
        if (ShowLabels) DrawPill(dc, Loc("Ice_Map_65"), Map(IceAgeModel.KeyLatitude, -135), Ink.Gold, 10.5);

        _targets.Add("globe", Map(48, 100), radius * 0.28);
        if (ShowLabels)
        {
            DrawText(dc, Loc("Ice_Map_Pole"), center + new Vector(0, -9), Ink.Dim, 9.5, TextAnchor.Center);
            DrawText(dc, Loc("Ice_Map_America"), Map(42, -100), Ink.Dim, 10, TextAnchor.Center);
            DrawText(dc, Loc("Ice_Map_Europe"), Map(44, 12), Ink.Dim, 10, TextAnchor.Center);
            DrawText(dc, Loc("Ice_Map_Asia"), Map(46, 95), Ink.Dim, 10, TextAnchor.Center);
        }

        // The ice sheets, named where they are big enough to see.
        var share = moment.IceShare;
        foreach (var (index, id, key) in new[] { (2, "greenland", "Ice_Map_Greenland"), (0, "laurentide", "Ice_Map_Laurentide"), (3, "fennoscandian", "Ice_Map_Fennoscandian") })
        {
            var (lat, lon, r) = PolarIceMap.SheetCentre(index, share);
            if (r < 2.5) continue;
            var at = Map(lat, lon);
            _targets.Add(id, at, Math.Max(10, r / (90 - PolarIceMap.EdgeLatitude) * radius * 0.6));
            if (ShowLabels) DrawTargetPill(dc, Loc(key), at, Ink.Ice, id, 10.5);
        }

        var edge = PolarIceMap.SeaIceEdge(share);
        var seaIceAt = Map(Math.Min(87, edge + 4), 150);
        _targets.Add("seaice", seaIceAt, 12);
        if (ShowLabels && edge < 88) DrawText(dc, Loc("Ice_Map_SeaIce"), seaIceAt + new Vector(0, -2), Ink.Steel, 9.5, TextAnchor.Center);

        // The summer Sun's strength at 65° N: a sun that grows and glows with it, and the value against today's.
        var today = IceRecord.Instance.At(0).Sunshine;
        var change = moment.Sunshine - today;
        var sunAt = new Point(globe.Left + 22, globe.Top + 22);
        var size = 9 + (Math.Clamp((moment.Sunshine - 420) / 150, 0, 1) * 10);
        DrawSun(dc, sunAt, size);
        var text = LocFormat("Ice_Map_SunMeter", Number(moment.Sunshine, "0"), (change >= 0 ? "+" : "−") + Number(Math.Abs(change), "0"));
        var box = DrawText(dc, text, new Point(sunAt.X + 26, sunAt.Y), change >= 0 ? Ink.Gold : Ink.Cyan, 11.5, bold: true);
        box = box.Union(new Rect(sunAt.X - size, sunAt.Y - size, size * 2, size * 2));
        AddBoxTarget("sunmeter", box);
    }

    #endregion

    #region the charts

    private readonly Dictionary<(string, Rect), StreamGeometry> _chartCurves = [];

    /// <summary>The charts' parts that do not change with the time, drawn once into bitmaps (the filled curves are slow to draw each frame).</summary>
    private readonly Dictionary<string, (string Key, RenderTargetBitmap Bitmap)> _layers = [];

    /// <summary>Draws <paramref name="draw"/> (in this control's coordinates, inside <paramref name="bounds"/>) from a kept bitmap, made again when the key changes.</summary>
    private void DrawLayer(PaintContext dc, string name, Rect bounds, string key, Action<PaintContext> draw)
    {
        var dpi = RenderScale();
        var full = $"{key}|{bounds}|{LocalizationService.Instance.CurrentCulture.Name}|{dpi}";
        if (!_layers.TryGetValue(name, out var layer) || layer.Key != full)
        {
            var bitmap = SpaceDrawing.RenderPixels(Math.Max(1, (int)Math.Ceiling(bounds.Width * dpi)), Math.Max(1, (int)Math.Ceiling(bounds.Height * dpi)), 96 * dpi, context =>
            {
                context.PushTransform(new TranslateTransform(-bounds.X, -bounds.Y));
                draw(context);
                context.Pop();
            });
            layer = (full, bitmap);
            _layers[name] = layer;
        }

        dc.DrawImage(layer.Bitmap, bounds);
    }

    /// <summary>A chart's room with its axis labels, caption and the sea-level scale round it.</summary>
    private static Rect LayerBounds(Rect plot) => new(plot.Left - 52, plot.Top - 30, plot.Width + 104, plot.Height + 56);

    private StreamGeometry ChartCurve(string key, Rect plot, Func<double, double> y, bool filled = false)
    {
        if (_chartCurves.TryGetValue((key, plot), out var geometry)) return geometry;
        var points = new List<Point>();
        for (var k = IceRecord.Start; k <= IceRecord.End + 1e-9; k += 0.5) points.Add(new Point(TimeX(plot, k), y(k)));
        if (filled)
        {
            points.Add(new Point(plot.Right, plot.Bottom));
            points.Add(new Point(plot.Left, plot.Bottom));
        }

        if (_chartCurves.Count > 24) _chartCurves.Clear();
        geometry = Line(points, closed: filled, filled: filled);
        _chartCurves[(key, plot)] = geometry;
        return geometry;
    }

    /// <summary>The axes of a time chart: ticks every 100,000 years, today, the future shaded; its caption above it.</summary>
    private void DrawTimeAxes(PaintContext dc, Rect plot, string caption, Color color, double[] ys, Func<double, double> y, string format)
    {
        dc.DrawRoundedRectangle(Ink.PlotBack, Ink.Frame, Inflated(plot, 8, 6), 8, 8);
        dc.DrawRectangle(Ink.Future, null, new Rect(TimeX(plot, 0), plot.Top, TimeX(plot, IceRecord.End) - TimeX(plot, 0), plot.Height));
        foreach (var v in ys)
        {
            var py = y(v);
            dc.DrawLine(Ink.Grid, new Point(plot.Left, py), new Point(plot.Right, py));
            DrawText(dc, Number(v, format), new Point(plot.Left - 6, py), Ink.Dim, 9.5, TextAnchor.Right);
        }

        for (var k = -800; k <= 100; k += 100)
        {
            var x = TimeX(plot, k);
            dc.DrawLine(Ink.Grid, new Point(x, plot.Top), new Point(x, plot.Bottom));
            if (k % 200 == 0 || k == 100) DrawText(dc, TickText(k), new Point(x, plot.Bottom + 12), Ink.Dim, 9.5, TextAnchor.Center);
        }

        DrawText(dc, caption, new Point(plot.Left - 4, plot.Top - 17), color, 11.5, bold: true);
    }

    /// <summary>Midsummer sunshine at 65° N (W/m²) over the whole story, today's level dashed.</summary>
    private void DrawSunshineChart(PaintContext dc, Rect plot, IceMoment moment)
    {
        const double low = 420, high = 570;
        double Y(double w) => plot.Bottom - ((Math.Clamp(w, low, high) - low) / (high - low) * plot.Height);
        DrawLayer(dc, "sun", LayerBounds(plot), "sun", layer =>
        {
            DrawTimeAxes(layer, plot, Loc("Ice_Chart_Sunshine"), Ink.Gold, [440, 480, 520, 560], Y, "0");
            var today = IceRecord.Instance.At(0).Sunshine;
            layer.DrawLine(FadedPen(Ink.Dim, 0.7, 1, dashed: true), new Point(plot.Left, Y(today)), new Point(plot.Right, Y(today)));
            layer.DrawGeometry(null, FadedPen(Ink.Gold, 0.95, 1.3, flat: true), ChartCurve("sun", plot, k => Y(IceRecord.Instance.At(k).Sunshine)));
        });
        var dot = new Point(TimeX(plot, Math.Clamp(TimeKa, IceRecord.Start, IceRecord.End)), Y(moment.Sunshine));
        dc.DrawEllipse(Brushes.White, FadedPen(Ink.Gold, 1, 2), dot, 4.5, 4.5);
        DrawText(dc, LocFormat("Ice_Value_Wm2", Number(moment.Sunshine, "0")), dot + new Vector(dot.X > plot.Right - 70 ? -10 : 10, -12), Ink.Gold, 10.5,
            dot.X > plot.Right - 70 ? TextAnchor.Right : TextAnchor.Left, bold: true);
        AddBoxTarget("sunshinechart", plot);
    }

    /// <summary>The model's northern ice (0 today to 1 at the last maximum) with sea level on the right, coloured by state.</summary>
    private void DrawIceChart(PaintContext dc, Rect plot, IceMoment moment)
    {
        const double high = 1.2;
        double Y(double share) => plot.Bottom - (Math.Clamp(share, 0, high) / high * plot.Height);
        var future = new Rect(TimeX(plot, 0), plot.Top, TimeX(plot, IceRecord.End) - TimeX(plot, 0), plot.Height);
        DrawLayer(dc, "ice", LayerBounds(plot), "ice" + ShowLabels, layer =>
        {
            DrawTimeAxes(layer, plot, Loc("Ice_Chart_Ice"), Ink.Ice, [0, 0.5, 1], Y, "0.0");

            // Sea level on the right: today's with no ice beyond today's, 120 m lower at the last maximum.
            foreach (var metres in new[] { 0.0, -60, -120 })
                DrawText(layer, LocFormat("Ice_Value_M", Number(metres, "0")), new Point(plot.Right + 6, Y(-metres / IceRecord.SeaDrop)), Ink.Cyan, 9.5);

            // Filled under the curve, a slice per state.
            foreach (var state in Enum.GetValues<IceState>())
            {
                var geometry = ChartCurve("state" + (int)state, plot, k =>
                {
                    var m = IceRecord.Instance.At(k);
                    return m.State == state ? Y(m.IceShare) : plot.Bottom;
                }, filled: true);
                layer.DrawGeometry(StateBrush(state), null, geometry);
            }

            layer.DrawGeometry(null, FadedPen(Ink.Ice, 0.95, 1.3, flat: true), ChartCurve("ice", plot, k => Y(IceRecord.Instance.At(k).IceShare)));
            if (ShowLabels && future.Width > 30) DrawText(layer, Loc("Ice_Chart_Future"), new Point(future.Left + (future.Width / 2), plot.Top + 9), Ink.Violet, 9.5, TextAnchor.Center);
        });
        AddBoxTarget("icechart", plot);
        AddBoxTarget("future", future);

        if (ShowLabels)
        {
            foreach (var (kyr, key) in Moments)
            {
                var x = TimeX(plot, kyr);
                if (Math.Abs(x - TimeX(plot, TimeKa)) < 90) continue;
                DrawText(dc, Loc(key), new Point(x, plot.Top + 24), Ink.Dim, 9.5, TextAnchor.Center);
            }
        }

        if (ShowTerminations)
        {
            var n = 0;
            foreach (var end in IceRecord.Instance.Terminations.OrderByDescending(t => t))
            {
                n++;
                var at = new Point(TimeX(plot, end), plot.Bottom - 7);
                dc.DrawEllipse(FadedBrush(Ink.Red, 0.9), null, at, 6.5, 6.5);
                DrawText(dc, Number(n, "0"), at, Ink.Text, 8.5, TextAnchor.Center, bold: true);
                _targets.Add("terminations", at, 8);
            }
        }

        var nowX = TimeX(plot, 0);
        dc.DrawLine(FadedPen(Ink.Green, 0.8, 1.2), new Point(nowX, plot.Top), new Point(nowX, plot.Bottom));
        _targets.Add("now", new Point(nowX, plot.Top + 2), 8);

        var dot = new Point(TimeX(plot, Math.Clamp(TimeKa, IceRecord.Start, IceRecord.End)), Y(moment.IceShare));
        dc.DrawEllipse(Brushes.White, FadedPen(Ink.Ice, 1, 2), dot, 4.5, 4.5);
        var right = dot.X > plot.Right - 90;
        DrawText(dc, LocFormat("Ice_Value_Sea", Number(moment.SeaLevel, "0")), dot + new Vector(right ? -10 : 10, -12), Ink.Cyan, 10.5,
            right ? TextAnchor.Right : TextAnchor.Left, bold: true);
    }

    private static IBrush StateBrush(IceState state) => state switch
    {
        IceState.Interglacial => Ink.StateWarm,
        IceState.Glacial => Ink.StateGlacial,
        _ => Ink.StateFull,
    };

    #endregion
}
