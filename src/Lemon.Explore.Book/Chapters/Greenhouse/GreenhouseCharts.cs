using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Greenhouse;

/// <summary>A small chart of the greenhouse-effect side panel: redrawn when a property changes or the language does.</summary>
public abstract class GreenhouseChart : Control
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    private protected readonly LabelCache Labels;

    protected GreenhouseChart()
    {
        Labels = new LabelCache(this);
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        Labels.Clear();
        InvalidateVisual();
    }


    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        if (ActualWidth < 80 || ActualHeight < 60) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        Draw(dc, ActualWidth, ActualHeight);
    }

    protected abstract void Draw(PaintContext dc, double width, double height);

    private protected void AxisLabel(PaintContext dc, string value, Point at, bool alignRight = false, bool center = false)
    {
        var text = Labels.Text(value, 10, ChartInk.Dim);
        var x = alignRight ? at.X - text.Width : center ? at.X - (text.Width / 2) : at.X;
        dc.DrawText(text, new Point(x, at.Y - (text.Height / 2)));
    }

    private protected void Label(PaintContext dc, string value, Point at, Color color, bool alignRight = false, double size = 10.5, bool bold = false, bool center = false)
    {
        var text = Labels.Text(value, size, color, bold);
        var x = alignRight ? at.X - text.Width : center ? at.X - (text.Width / 2) : at.X;
        dc.DrawText(text, new Point(x, at.Y - (text.Height / 2)));
    }

    private protected static void Marker(PaintContext dc, Point point, Color color, double radius = 4)
    {
        dc.DrawEllipse(Solid(70, color.R, color.G, color.B), null, point, radius * 2, radius * 2);
        dc.DrawEllipse(Solid(255, color.R, color.G, color.B), null, point, radius, radius);
    }

    private protected static StreamGeometry Curve(IEnumerable<Point> points, bool closed = false)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var first = true;
            foreach (var point in points)
            {
                if (first) context.BeginFigure(point, closed);
                else context.LineTo(point);
                first = false;
            }

            if (!first) context.EndFigure(closed);
        }
        return geometry;
    }

    private protected static IPen LinePen(Color color, double thickness, bool dashed = false, byte alpha = 255) =>
        MakePen(Solid(alpha, color.R, color.G, color.B), thickness, dashed);

    private protected static IBrush Fill(Color color, byte alpha) => Solid(alpha, color.R, color.G, color.B);

    private protected static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private protected static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private protected static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    private protected static class ChartInk
    {
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
        public static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
        public static readonly Color Orange = Color.FromRgb(0xFF, 0x9F, 0x5A);
        public static readonly Color Red = Color.FromRgb(0xFF, 0x6A, 0x5A);
        public static readonly Color Blue = Color.FromRgb(0x5A, 0xB0, 0xFF);
        public static readonly Color Green = Color.FromRgb(0x8B, 0xE0, 0x8F);
        public static readonly Color Sun = Color.FromRgb(0xFF, 0xE2, 0x7A);
        public static readonly IPen Grid = MakePen(Solid(28, 255, 255, 255), 1);
        public static readonly IPen Axis = MakePen(Solid(110, 255, 255, 255), 1);
    }
}

/// <summary>
/// CO₂ in the air since 1750 (ice cores, then Mauna Loa), with the amount picked on the slider as a line across, so
/// "today" and "doubled" have a place.
/// </summary>
public sealed class Co2HistoryChart : GreenhouseChart
{
    static Co2HistoryChart()
    {
        AffectsRender<Co2HistoryChart>(Co2Property);
    }

    public static readonly StyledProperty<double> Co2Property =
        AvaloniaProperty.Register<Co2HistoryChart, double>(nameof(Co2), PlanetModel.TodayCo2);

    public double Co2
    {
        get => GetValue(Co2Property);
        set => SetValue(Co2Property, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(40, 16, width - 54, height - 40);
        var top = Math.Max(450, Math.Ceiling((Co2 + 40) / 100) * 100);
        const double bottom = 250;
        double X(double year) => plot.Left + ((year - 1750) / (2030 - 1750) * plot.Width);
        double Y(double ppm) => plot.Bottom - ((ppm - bottom) / (top - bottom) * plot.Height);

        var step = top > 700 ? 200 : 50;
        for (var p = 300.0; p <= top; p += step)
        {
            dc.DrawLine(ChartInk.Grid, new Point(plot.Left, Y(p)), new Point(plot.Right, Y(p)));
            AxisLabel(dc, Number(p, "0"), new Point(plot.Left - 6, Y(p)), alignRight: true);
        }

        foreach (var year in new[] { 1750, 1800, 1850, 1900, 1950, 2000 })
        {
            AxisLabel(dc, year.ToString(CultureInfo.InvariantCulture), new Point(X(year), plot.Bottom + 10), center: true);
        }

        AxisLabel(dc, "ppm", new Point(plot.Left - 6, plot.Top - 8), alignRight: true);

        // Before industry: about 280.
        dc.DrawLine(LinePen(ChartInk.Dim, 1, dashed: true), new Point(plot.Left, Y(280)), new Point(plot.Right, Y(280)));

        var points = Co2Record.Points;
        dc.DrawGeometry(Fill(ChartInk.Orange, 40), null, Curve(
            new[] { new Point(X(points[0].Year), plot.Bottom) }
                .Concat(points.Select(p => new Point(X(p.Year), Y(p.Ppm))))
                .Append(new Point(X(points[^1].Year), plot.Bottom)), closed: true));
        dc.DrawGeometry(null, LinePen(ChartInk.Orange, 2), Curve(points.Select(p => new Point(X(p.Year), Y(p.Ppm)))));
        var last = points[^1];
        Marker(dc, new Point(X(last.Year), Y(last.Ppm)), ChartInk.Orange, 3.5);
        Label(dc, LocFormat("Green_Chart_Co2_Last", last.Year, Number(last.Ppm, "0")), new Point(X(last.Year) - 6, Y(last.Ppm) - 12), ChartInk.Orange, alignRight: true, size: 10);
        Label(dc, Loc("Green_Chart_Co2_Before"), new Point(plot.Left + 4, Y(280) - 9), ChartInk.Dim, size: 10);

        // The amount on the slider.
        var chosen = Y(Co2);
        dc.DrawLine(LinePen(ChartInk.Gold, 1.4, dashed: true), new Point(plot.Left, chosen), new Point(plot.Right, chosen));
        Label(dc, LocFormat("Green_Chart_Co2_Chosen", Number(Co2, "0")), new Point(plot.Left + 4, chosen - 9), ChartInk.Gold, size: 10);
    }
}

/// <summary>Venus, Earth and Mars: the temperature each would have with no greenhouse gases, and the real one.</summary>
public sealed class WorldsChart : GreenhouseChart
{
    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(64, 12, width - 78, height - 36);
        const double low = -100;
        const double high = 500;
        double X(double celsius) => plot.Left + ((celsius - low) / (high - low) * plot.Width);

        for (var c = -100; c <= 500; c += 100)
        {
            dc.DrawLine(ChartInk.Grid, new Point(X(c), plot.Top), new Point(X(c), plot.Bottom));
            AxisLabel(dc, Number(c, "0"), new Point(X(c), plot.Bottom + 10), center: true);
        }

        AxisLabel(dc, "°C", new Point(plot.Right, plot.Bottom + 22), alignRight: true);
        var worlds = PlanetModel.Worlds;
        var row = plot.Height / worlds.Count;
        for (var i = 0; i < worlds.Count; i++)
        {
            var world = worlds[i];
            var y = plot.Top + (row * (i + 0.5));
            Label(dc, Loc($"Green_World_{world.Id}"), new Point(plot.Left - 8, y), ChartInk.Text, alignRight: true, size: 11);
            var bare = new Point(X(world.Bare - 273.15), y);
            var real = new Point(X(world.Surface - 273.15), y);
            var color = world.Id == "Earth" ? ChartInk.Cyan : world.Id == "Venus" ? ChartInk.Orange : ChartInk.Red;
            dc.DrawLine(LinePen(color, 3, alpha: 150), bare, real);
            dc.DrawEllipse(null, LinePen(ChartInk.Text, 1.4), bare, 4, 4);
            Marker(dc, real, color, 4);
            var label = LocFormat("Green_Chart_Worlds_Warming", Number(world.Warming, "0"));
            var right = real.X + 10 + Labels.Text(label, 10, color).Width < plot.Right;
            Label(dc, label, new Point(right ? real.X + 10 : real.X - 10, y - 11), color, alignRight: !right, size: 10);
        }
    }
}

/// <summary>
/// Sunlight and the ground's glow by wavelength (each scaled to its own peak, log axis): they hardly overlap, so air
/// that lets sunlight through can still catch the ground's heat. The air's absorption is shaded behind.
/// </summary>
public sealed class GlowChart : GreenhouseChart
{
    static GlowChart()
    {
        AffectsRender<GlowChart>(Co2Property);
    }

    public static readonly StyledProperty<double> Co2Property =
        AvaloniaProperty.Register<GlowChart, double>(nameof(Co2), PlanetModel.TodayCo2);

    public double Co2
    {
        get => GetValue(Co2Property);
        set => SetValue(Co2Property, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(12, 18, width - 24, height - 42);
        const double from = 0.2;
        const double to = 60;
        double X(double l) => plot.Left + (Math.Log(l / from) / Math.Log(to / from) * plot.Width);
        double Y(double share) => plot.Bottom - (share * plot.Height);
        var samples = Enumerable.Range(0, 241).Select(i => from * Math.Pow(to / from, i / 240.0)).ToArray();

        // What the air catches, as a grey band behind (ozone below 0.3 µm, the gases' infrared bands).
        var caught = samples.Select(l => l < 0.3 ? 1 : l < 2 ? 0.05 : InfraredModel.AirAbsorption(l, Co2)).ToArray();
        dc.DrawGeometry(Fill(ChartInk.Text, 26), null, Curve(
            new[] { new Point(X(from), plot.Bottom) }.Concat(samples.Select((l, i) => new Point(X(l), Y(caught[i])))).Append(new Point(X(to), plot.Bottom)), closed: true));

        var sunPeak = InfraredModel.PerLog(InfraredModel.PeakWavelength(5772) * 1.25, 5772);
        var groundPeak = InfraredModel.PerLog(InfraredModel.PeakWavelength(288) * 1.25, 288);
        dc.DrawGeometry(Fill(ChartInk.Sun, 60), LinePen(ChartInk.Sun, 1.6), Curve(
            new[] { new Point(X(from), plot.Bottom) }.Concat(samples.Select(l => new Point(X(l), Y(InfraredModel.PerLog(l, 5772) / sunPeak)))).Append(new Point(X(to), plot.Bottom)), closed: true));
        dc.DrawGeometry(Fill(ChartInk.Red, 60), LinePen(ChartInk.Red, 1.6), Curve(
            new[] { new Point(X(from), plot.Bottom) }.Concat(samples.Select(l => new Point(X(l), Y(InfraredModel.PerLog(l, 288) / groundPeak)))).Append(new Point(X(to), plot.Bottom)), closed: true));

        // Visible light: a little rainbow under the axis.
        for (var i = 0; i < 30; i++)
        {
            var a = 0.4 + (0.3 * i / 30);
            var b = 0.4 + (0.3 * (i + 1) / 30);
            var hue = BlueSky.SkyOptics.SpectralColour(((a + b) / 2) * 1000);
            dc.DrawRectangle(new SolidColorBrush(hue), null, new Rect(X(a), plot.Bottom + 2, X(b) - X(a) + 0.5, 4));
        }

        foreach (var tick in new[] { 0.3, 1.0, 3, 10, 30 })
        {
            AxisLabel(dc, Number(tick, tick < 1 ? "0.0" : "0"), new Point(X(tick), plot.Bottom + 14), center: true);
        }

        AxisLabel(dc, "µm", new Point(plot.Right, plot.Bottom + 14), alignRight: true);
        Label(dc, Loc("Green_Chart_Glow_Sun"), new Point(X(0.5), plot.Top - 8), ChartInk.Sun, center: true, size: 10);
        Label(dc, Loc("Green_Chart_Glow_Ground"), new Point(X(10), plot.Top - 8), ChartInk.Red, center: true, size: 10);
        Label(dc, Loc("Green_Chart_Glow_Air"), new Point(X(2.6), Y(0.82)), ChartInk.Dim, center: true, size: 9.5);
    }
}

/// <summary>
/// The shares of today's greenhouse effect (Schmidt et al. 2010): water vapour about half, clouds a quarter, CO₂ a
/// fifth, the rest the other gases. The gas shown on the page is highlighted.
/// </summary>
public sealed class GasShareChart : GreenhouseChart
{
    static GasShareChart()
    {
        AffectsRender<GasShareChart>(GasProperty);
    }

    public static readonly StyledProperty<Gas> GasProperty =
        AvaloniaProperty.Register<GasShareChart, Gas>(nameof(Gas), Gas.CO2);

    public Gas Gas
    {
        get => GetValue(GasProperty);
        set => SetValue(GasProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        (string Key, double Share, Color Color, bool Shown)[] rows =
        [
            ("Green_Share_Water", 50, ChartInk.Blue, Gas == Gas.H2O),
            ("Green_Share_Clouds", 25, ChartInk.Text, false),
            ("Green_Share_Co2", 20, ChartInk.Orange, Gas == Gas.CO2),
            ("Green_Share_Other", 5, ChartInk.Green, Gas == Gas.CH4),
            ("Green_Share_Main", 0, ChartInk.Dim, Gas is Gas.N2 or Gas.O2 or Gas.Ar),
        ];

        var labelWidth = rows.Max(r => Labels.Text(Loc(r.Key), 10.5, ChartInk.Text).Width) + 14;
        var plot = new Rect(labelWidth, 6, width - labelWidth - 46, height - 12);
        var row = plot.Height / rows.Length;
        for (var i = 0; i < rows.Length; i++)
        {
            var (key, share, color, shown) = rows[i];
            var y = plot.Top + (row * (i + 0.5));
            var bar = new Rect(plot.Left, y - (row * 0.3), Math.Max(1.5, share / 50 * plot.Width), row * 0.6);
            if (shown) dc.DrawRoundedRectangle(Fill(ChartInk.Gold, 30), null, new Rect(0, y - (row * 0.48), width, row * 0.96), 6, 6);
            dc.DrawRoundedRectangle(Fill(color, (byte)(shown ? 230 : 150)), null, bar, 3, 3);
            Label(dc, Loc(key), new Point(plot.Left - 8, y), shown ? ChartInk.Gold : ChartInk.Text, alignRight: true, size: 10.5, bold: shown);
            Label(dc, $"{Number(share, "0")}%", new Point(bar.Right + 6, y), shown ? ChartInk.Gold : ChartInk.Dim, size: 10.5);
        }
    }
}

/// <summary>The glass-house day: outside air, the greenhouse and the car through 24 hours, with the sunlight under them and now marked.</summary>
public sealed class DayChart : GreenhouseChart
{
    static DayChart()
    {
        AffectsRender<DayChart>(HourProperty, LatitudeProperty, SeasonProperty, VentsOpenProperty, SaltPaneProperty, CarWindowOpenProperty);
    }

    public static readonly StyledProperty<double> HourProperty =
        AvaloniaProperty.Register<DayChart, double>(nameof(Hour), 10.0);
    public static readonly StyledProperty<double> LatitudeProperty =
        AvaloniaProperty.Register<DayChart, double>(nameof(Latitude), 28.2);
    public static readonly StyledProperty<Season> SeasonProperty =
        AvaloniaProperty.Register<DayChart, Season>(nameof(Season), Season.Summer);
    public static readonly StyledProperty<bool> VentsOpenProperty =
        AvaloniaProperty.Register<DayChart, bool>(nameof(VentsOpen), false);
    public static readonly StyledProperty<bool> SaltPaneProperty =
        AvaloniaProperty.Register<DayChart, bool>(nameof(SaltPane), false);
    public static readonly StyledProperty<bool> CarWindowOpenProperty =
        AvaloniaProperty.Register<DayChart, bool>(nameof(CarWindowOpen), false);

    public double Hour
    {
        get => GetValue(HourProperty);
        set => SetValue(HourProperty, value);
    }

    public double Latitude
    {
        get => GetValue(LatitudeProperty);
        set => SetValue(LatitudeProperty, value);
    }

    public Season Season
    {
        get => GetValue(SeasonProperty);
        set => SetValue(SeasonProperty, value);
    }

    public bool VentsOpen
    {
        get => GetValue(VentsOpenProperty);
        set => SetValue(VentsOpenProperty, value);
    }

    public bool SaltPane
    {
        get => GetValue(SaltPaneProperty);
        set => SetValue(SaltPaneProperty, value);
    }

    public bool CarWindowOpen
    {
        get => GetValue(CarWindowOpenProperty);
        set => SetValue(CarWindowOpenProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var options = new DayOptions(Latitude, Season, VentsOpen, SaltPane, CarWindowOpen);
        var day = GlassHouseModel.Day(options);
        var plot = new Rect(34, 14, width - 46, height - 38);
        var low = Math.Floor(Math.Min(day.Min(s => s.Outside), day.Min(s => s.Greenhouse)) / 10) * 10;
        var high = Math.Ceiling(Math.Max(day.Max(s => s.Car), day.Max(s => s.Greenhouse)) / 10) * 10;
        double X(double hour) => plot.Left + (hour / 24 * plot.Width);
        double Y(double celsius) => plot.Bottom - ((celsius - low) / (high - low) * plot.Height);

        // The sunlight, as a pale hill along the bottom.
        dc.DrawGeometry(Fill(ChartInk.Sun, 34), null, Curve(
            day.Where((_, i) => i % 10 == 0).Select(s => new Point(X(s.Hour), plot.Bottom - (s.Sunlight / 1100 * plot.Height * 0.5)))
                .Prepend(new Point(X(0), plot.Bottom)).Append(new Point(X(24), plot.Bottom)), closed: true));

        for (var c = low; c <= high + 1e-9; c += 10)
        {
            dc.DrawLine(ChartInk.Grid, new Point(plot.Left, Y(c)), new Point(plot.Right, Y(c)));
            AxisLabel(dc, Number(c, "0"), new Point(plot.Left - 5, Y(c)), alignRight: true);
        }

        for (var h = 0; h <= 24; h += 6)
        {
            AxisLabel(dc, $"{h}:00", new Point(X(h), plot.Bottom + 10), center: true);
        }

        AxisLabel(dc, "°C", new Point(plot.Left - 5, plot.Top - 8), alignRight: true);

        IEnumerable<Point> Series(Func<DayState, double> value) => day.Where((_, i) => i % 5 == 0).Select(s => new Point(X(s.Hour), Y(value(s))));
        dc.DrawGeometry(null, LinePen(ChartInk.Text, 1.6), Curve(Series(s => s.Outside)));
        dc.DrawGeometry(null, LinePen(ChartInk.Green, 2), Curve(Series(s => s.Greenhouse)));
        dc.DrawGeometry(null, LinePen(ChartInk.Red, 2), Curve(Series(s => s.Car)));

        var now = GlassHouseModel.At(options, Hour);
        dc.DrawLine(LinePen(ChartInk.Gold, 1, dashed: true), new Point(X(Hour), plot.Top), new Point(X(Hour), plot.Bottom));
        Marker(dc, new Point(X(Hour), Y(now.Outside)), ChartInk.Text, 3);
        Marker(dc, new Point(X(Hour), Y(now.Greenhouse)), ChartInk.Green, 3);
        Marker(dc, new Point(X(Hour), Y(now.Car)), ChartInk.Red, 3);

        // The legend in a row along the top.
        var x = plot.Left + 4;
        foreach (var (key, color) in new[] { ("Green_Chart_Day_Outside", ChartInk.Text), ("Green_Chart_Day_Greenhouse", ChartInk.Green), ("Green_Chart_Day_Car", ChartInk.Red) })
        {
            var text = Loc(key);
            dc.DrawLine(LinePen(color, 2), new Point(x, plot.Top + 2), new Point(x + 12, plot.Top + 2));
            Label(dc, text, new Point(x + 15, plot.Top + 2), color, size: 10);
            x += 15 + Labels.Text(text, 10, color).Width + 12;
        }
    }
}
