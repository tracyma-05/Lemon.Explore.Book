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

namespace Lemon.Explore.Book.Chapters.Typhoon;

/// <summary>A small chart of the typhoon side panel: redrawn when a property changes or the language does.</summary>
public abstract class TyphoonChart : Control
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

    protected TyphoonChart()
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

    /// <summary>Faint bands of the storm grades behind a wind axis (from <paramref name="y"/> of a speed).</summary>
    private protected void GradeBands(PaintContext dc, Rect plot, Func<double, double> y, double top, bool labels = true)
    {
        double[] edges = [10.8, 17.2, 24.5, 32.7, 41.5, 51.0, 1000];
        for (var i = 0; i < edges.Length - 1; i++)
        {
            if (edges[i] >= top) break;
            var grade = StormModel.Grade(edges[i] + 0.01);
            var color = TyphoonDiagram.GradeColor(grade);
            var y1 = Math.Max(plot.Top, y(Math.Min(top, edges[i + 1])));
            var y0 = y(edges[i]);
            dc.DrawRectangle(Solid(22, color.R, color.G, color.B), null, new Rect(plot.Left, y1, plot.Width, Math.Max(0, y0 - y1)));
            if (labels && y0 - y1 > 11) Label(dc, TyphoonDiagram.GradeName(grade), new Point(plot.Right - 4, (y0 + y1) / 2), color, alignRight: true, size: 9.5);
        }
    }

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
        public static readonly IPen Grid = MakePen(Solid(28, 255, 255, 255), 1);
    }
}

/// <summary>The storm's wind and pressure from the centre out to 500 km, with the probe's distance marked.</summary>
public sealed class ProfileChart : TyphoonChart
{
    static ProfileChart()
    {
        AffectsRender<ProfileChart>(CentralPressureProperty, ProbeDistanceProperty);
    }

    public static readonly StyledProperty<double> CentralPressureProperty =
        AvaloniaProperty.Register<ProfileChart, double>(nameof(CentralPressure), 950.0);
    public static readonly StyledProperty<double> ProbeDistanceProperty =
        AvaloniaProperty.Register<ProfileChart, double>(nameof(ProbeDistance), 160.0);

    public double CentralPressure
    {
        get => GetValue(CentralPressureProperty);
        set => SetValue(CentralPressureProperty, value);
    }

    public double ProbeDistance
    {
        get => GetValue(ProbeDistanceProperty);
        set => SetValue(ProbeDistanceProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(34, 16, width - 78, height - 40);
        const double far = 500;
        const double top = 80;
        var low = Math.Floor(CentralPressure / 20) * 20;
        double X(double km) => plot.Left + (km / far * plot.Width);
        double Y(double speed) => plot.Bottom - (speed / top * plot.Height);
        double P(double hPa) => plot.Bottom - ((hPa - low) / (1020 - low) * plot.Height);

        GradeBands(dc, plot, Y, top, labels: false);
        for (var v = 0.0; v <= top; v += 20)
        {
            dc.DrawLine(ChartInk.Grid, new Point(plot.Left, Y(v)), new Point(plot.Right, Y(v)));
            AxisLabel(dc, Number(v, "0"), new Point(plot.Left - 5, Y(v)), alignRight: true);
        }

        for (var hPa = low; hPa <= 1020; hPa += 20) AxisLabel(dc, Number(hPa, "0"), new Point(plot.Right + 5, P(hPa)));
        for (var km = 0; km <= far; km += 100) AxisLabel(dc, Number(km, "0"), new Point(X(km), plot.Bottom + 10), center: true);
        AxisLabel(dc, "m/s", new Point(plot.Left - 5, plot.Top - 9), alignRight: true);
        AxisLabel(dc, "hPa", new Point(plot.Right + 5, plot.Top - 9));
        AxisLabel(dc, "km", new Point(plot.Right, plot.Bottom + 22), alignRight: true);

        var samples = Enumerable.Range(0, 201).Select(i => i * far / 200).ToArray();
        dc.DrawGeometry(null, LinePen(ChartInk.Cyan, 1.6), Curve(samples.Select(r => new Point(X(r), P(StormModel.Pressure(r, CentralPressure))))));
        dc.DrawGeometry(null, LinePen(ChartInk.Gold, 2.2), Curve(samples.Select(r => new Point(X(r), Y(StormModel.Wind(r, CentralPressure))))));

        var probe = Math.Clamp(ProbeDistance, 0, far);
        dc.DrawLine(LinePen(ChartInk.Text, 1, dashed: true, alpha: 160), new Point(X(probe), plot.Top), new Point(X(probe), plot.Bottom));
        Marker(dc, new Point(X(probe), Y(StormModel.Wind(probe, CentralPressure))), ChartInk.Gold, 3.5);
        Marker(dc, new Point(X(probe), P(StormModel.Pressure(probe, CentralPressure))), ChartInk.Cyan, 3);

        Label(dc, Loc("Typhoon_Chart_Profile_Wind"), new Point(plot.Left + 6, plot.Top + 4), ChartInk.Gold, size: 10);
        Label(dc, Loc("Typhoon_Chart_Profile_Pressure"), new Point(plot.Left + 6, plot.Top + 18), ChartInk.Cyan, size: 10);
    }
}

/// <summary>How strong a storm the sea lets grow, against its temperature, with 26.5 °C marked.</summary>
public sealed class PotentialChart : TyphoonChart
{
    static PotentialChart()
    {
        AffectsRender<PotentialChart>(SeaTemperatureProperty);
    }

    public static readonly StyledProperty<double> SeaTemperatureProperty =
        AvaloniaProperty.Register<PotentialChart, double>(nameof(SeaTemperature), 29.0);

    public double SeaTemperature
    {
        get => GetValue(SeaTemperatureProperty);
        set => SetValue(SeaTemperatureProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(34, 12, width - 46, height - 36);
        const double from = 24;
        const double to = 31;
        const double top = 90;
        double X(double celsius) => plot.Left + ((celsius - from) / (to - from) * plot.Width);
        double Y(double speed) => plot.Bottom - (speed / top * plot.Height);

        GradeBands(dc, plot, Y, top);
        for (var v = 0.0; v <= top; v += 30)
        {
            dc.DrawLine(ChartInk.Grid, new Point(plot.Left, Y(v)), new Point(plot.Right, Y(v)));
            AxisLabel(dc, Number(v, "0"), new Point(plot.Left - 5, Y(v)), alignRight: true);
        }

        for (var c = from; c <= to; c++) AxisLabel(dc, Number(c, "0"), new Point(X(c), plot.Bottom + 10), center: true);
        AxisLabel(dc, "°C", new Point(plot.Right, plot.Bottom + 22), alignRight: true);
        AxisLabel(dc, "m/s", new Point(plot.Left - 5, plot.Top - 6), alignRight: true);

        var edge = X(StormModel.GenesisSea);
        dc.DrawLine(LinePen(ChartInk.Cyan, 1.2, dashed: true), new Point(edge, plot.Top), new Point(edge, plot.Bottom));
        Label(dc, Loc("Typhoon_Chart_Potential_Edge"), new Point(edge - 4, plot.Top + 8), ChartInk.Cyan, alignRight: true, size: 9.5);

        var samples = Enumerable.Range(0, 141).Select(i => StormModel.GenesisSea + (i * (to - StormModel.GenesisSea) / 140));
        dc.DrawGeometry(null, LinePen(ChartInk.Orange, 2.2), Curve(samples.Select(c => new Point(X(c), Y(StormModel.PotentialIntensity(c))))));
        var now = StormModel.PotentialIntensity(SeaTemperature);
        Marker(dc, new Point(X(Math.Clamp(SeaTemperature, from, to)), Y(now)), ChartInk.Gold, 4);
    }
}

/// <summary>The storm's strongest wind in the two days after landfall, with now marked.</summary>
public sealed class DecayChart : TyphoonChart
{
    static DecayChart()
    {
        AffectsRender<DecayChart>(SeaTemperatureProperty, HoursProperty, LandfallProperty);
    }

    public static readonly StyledProperty<double> SeaTemperatureProperty =
        AvaloniaProperty.Register<DecayChart, double>(nameof(SeaTemperature), 29.0);
    public static readonly StyledProperty<double> HoursProperty =
        AvaloniaProperty.Register<DecayChart, double>(nameof(Hours), 0.0);
    public static readonly StyledProperty<bool> LandfallProperty =
        AvaloniaProperty.Register<DecayChart, bool>(nameof(Landfall), false);

    public double SeaTemperature
    {
        get => GetValue(SeaTemperatureProperty);
        set => SetValue(SeaTemperatureProperty, value);
    }

    public double Hours
    {
        get => GetValue(HoursProperty);
        set => SetValue(HoursProperty, value);
    }

    public bool Landfall
    {
        get => GetValue(LandfallProperty);
        set => SetValue(LandfallProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(34, 12, width - 46, height - 36);
        const double span = TyphoonDiagram.LandfallSpan;
        const double top = 80;
        double X(double hours) => plot.Left + (hours / span * plot.Width);
        double Y(double speed) => plot.Bottom - (speed / top * plot.Height);

        GradeBands(dc, plot, Y, top);
        for (var v = 0.0; v <= top; v += 20)
        {
            dc.DrawLine(ChartInk.Grid, new Point(plot.Left, Y(v)), new Point(plot.Right, Y(v)));
            AxisLabel(dc, Number(v, "0"), new Point(plot.Left - 5, Y(v)), alignRight: true);
        }

        for (var h = 0; h <= span; h += 12) AxisLabel(dc, Number(h, "0"), new Point(X(h), plot.Bottom + 10), center: true);
        AxisLabel(dc, Loc("Typhoon_Chart_Hours"), new Point(plot.Right, plot.Bottom + 22), alignRight: true);
        AxisLabel(dc, "m/s", new Point(plot.Left - 5, plot.Top - 6), alignRight: true);

        var start = TyphoonDiagram.EngineWind(SeaTemperature, false, 0);
        if (start <= 0)
        {
            Label(dc, Loc("Typhoon_Chart_Decay_None"), new Point(plot.Left + (plot.Width / 2), plot.Top + (plot.Height / 2)), ChartInk.Dim, center: true);
            return;
        }

        dc.DrawGeometry(null, LinePen(ChartInk.Orange, 2.2), Curve(Enumerable.Range(0, 97).Select(i => i * span / 96)
            .Select(h => new Point(X(h), Y(StormModel.AfterLandfall(start, h))))));
        var hours = Landfall ? Hours : 0;
        Marker(dc, new Point(X(hours), Y(TyphoonDiagram.EngineWind(SeaTemperature, Landfall, hours))), ChartInk.Gold, 4);
    }
}

/// <summary>The storm's strongest wind along its track, landfalls marked, now marked.</summary>
public sealed class IntensityChart : TyphoonChart
{
    static IntensityChart()
    {
        AffectsRender<IntensityChart>(StartLatitudeProperty, StartLongitudeProperty, RidgeWestProperty, MonthProperty, HourProperty);
    }

    public static readonly StyledProperty<double> StartLatitudeProperty =
        AvaloniaProperty.Register<IntensityChart, double>(nameof(StartLatitude), 13.5);
    public static readonly StyledProperty<double> StartLongitudeProperty =
        AvaloniaProperty.Register<IntensityChart, double>(nameof(StartLongitude), 144.0);
    public static readonly StyledProperty<double> RidgeWestProperty =
        AvaloniaProperty.Register<IntensityChart, double>(nameof(RidgeWest), 125.0);
    public static readonly StyledProperty<int> MonthProperty =
        AvaloniaProperty.Register<IntensityChart, int>(nameof(Month), 8);
    public static readonly StyledProperty<double> HourProperty =
        AvaloniaProperty.Register<IntensityChart, double>(nameof(Hour), 0.0);

    public double StartLatitude
    {
        get => GetValue(StartLatitudeProperty);
        set => SetValue(StartLatitudeProperty, value);
    }

    public double StartLongitude
    {
        get => GetValue(StartLongitudeProperty);
        set => SetValue(StartLongitudeProperty, value);
    }

    public double RidgeWest
    {
        get => GetValue(RidgeWestProperty);
        set => SetValue(RidgeWestProperty, value);
    }

    public int Month
    {
        get => GetValue(MonthProperty);
        set => SetValue(MonthProperty, value);
    }

    public double Hour
    {
        get => GetValue(HourProperty);
        set => SetValue(HourProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var track = TrackModel.Track(new TrackOptions(StartLatitude, StartLongitude, RidgeWest, Month));
        var plot = new Rect(34, 12, width - 46, height - 36);
        var span = Math.Max(48, Math.Ceiling(track[^1].Hour / 24) * 24);
        const double top = 80;
        double X(double hours) => plot.Left + (hours / span * plot.Width);
        double Y(double speed) => plot.Bottom - (speed / top * plot.Height);

        GradeBands(dc, plot, Y, top);
        for (var v = 0.0; v <= top; v += 20)
        {
            dc.DrawLine(ChartInk.Grid, new Point(plot.Left, Y(v)), new Point(plot.Right, Y(v)));
            AxisLabel(dc, Number(v, "0"), new Point(plot.Left - 5, Y(v)), alignRight: true);
        }

        var dayStep = span > 240 ? 48 : 24;
        for (var h = 0.0; h <= span; h += dayStep) AxisLabel(dc, LocFormat("Typhoon_Track_Day", h / 24), new Point(X(h), plot.Bottom + 10), center: true);
        AxisLabel(dc, "m/s", new Point(plot.Left - 5, plot.Top - 6), alignRight: true);

        foreach (var landfall in TrackModel.Landfalls(track))
        {
            var x = X(track[landfall.Index].Hour);
            dc.DrawLine(LinePen(ChartInk.Red, 1.2, dashed: true), new Point(x, plot.Top), new Point(x, plot.Bottom));
            Label(dc, Loc($"Typhoon_City_{landfall.City.Id}"), new Point(x + 3, plot.Top + 8), ChartInk.Red, size: 9.5);
        }

        dc.DrawGeometry(null, LinePen(ChartInk.Orange, 2.2), Curve(track.Select(p => new Point(X(p.Hour), Y(p.Wind)))));
        var now = TrackModel.At(track, Hour);
        Marker(dc, new Point(X(now.Hour), Y(now.Wind)), ChartInk.Gold, 3.5);
    }
}

/// <summary>Named storms a month over the north-west Pacific (JMA, 1991–2020), the chosen month highlighted.</summary>
public sealed class SeasonChart : TyphoonChart
{
    static SeasonChart()
    {
        AffectsRender<SeasonChart>(MonthProperty);
    }

    public static readonly StyledProperty<int> MonthProperty =
        AvaloniaProperty.Register<SeasonChart, int>(nameof(Month), 8);

    public int Month
    {
        get => GetValue(MonthProperty);
        set => SetValue(MonthProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(26, 12, width - 36, height - 34);
        var counts = TrackModel.StormsPerMonth;
        const double top = 6;
        var column = plot.Width / 12;
        for (var v = 0.0; v <= top; v += 2)
        {
            var y = plot.Bottom - (v / top * plot.Height);
            dc.DrawLine(ChartInk.Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            AxisLabel(dc, Number(v, "0"), new Point(plot.Left - 5, y), alignRight: true);
        }

        for (var i = 0; i < 12; i++)
        {
            var chosen = i + 1 == Month;
            var h = counts[i] / top * plot.Height;
            var bar = new Rect(plot.Left + (i * column) + (column * 0.18), plot.Bottom - h, column * 0.64, h);
            var color = chosen ? ChartInk.Gold : ChartInk.Cyan;
            dc.DrawRoundedRectangle(Solid((byte)(chosen ? 240 : 140), color.R, color.G, color.B), null, bar, 2, 2);
            AxisLabel(dc, Number(i + 1, "0"), new Point(bar.Left + (bar.Width / 2), plot.Bottom + 10), center: true);
            if (chosen) Label(dc, Number(counts[i], "0.0"), new Point(bar.Left + (bar.Width / 2), bar.Top - 8), ChartInk.Gold, center: true, size: 10);
        }
    }
}
