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

namespace Lemon.Explore.Book.Chapters.PowerGrid;

/// <summary>A small chart of the generator-and-grid side panel: redrawn when a property changes or the language does.</summary>
public abstract class GridChart : Control
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

    protected GridChart()
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
        public static readonly Color Green = Color.FromRgb(0x8B, 0xE0, 0x8F);
        public static readonly Color Scope = Color.FromRgb(0x6A, 0xFF, 0xB0);
        public static readonly IPen Grid = MakePen(Solid(28, 255, 255, 255), 1);
        public static readonly IPen Axis = MakePen(Solid(110, 255, 255, 255), 1);
    }
}

/// <summary>
/// One turn of the coil: the field through it (dashed) and the voltage it makes (solid), the voltage biggest where the
/// field through it changes fastest; the coil's angle now marked. For the three-phase machine, the three voltages.
/// </summary>
public sealed class FluxChart : GridChart
{
    static FluxChart()
    {
        AffectsRender<FluxChart>(AngleProperty, KindProperty, PolePairsProperty);
    }

    public static readonly StyledProperty<double> AngleProperty =
        AvaloniaProperty.Register<FluxChart, double>(nameof(Angle), 0.0);
    public static readonly StyledProperty<GeneratorKind> KindProperty =
        AvaloniaProperty.Register<FluxChart, GeneratorKind>(nameof(Kind), GeneratorKind.Ac);
    public static readonly StyledProperty<int> PolePairsProperty =
        AvaloniaProperty.Register<FluxChart, int>(nameof(PolePairs), 1);

    public double Angle
    {
        get => GetValue(AngleProperty);
        set => SetValue(AngleProperty, value);
    }

    public GeneratorKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public int PolePairs
    {
        get => GetValue(PolePairsProperty);
        set => SetValue(PolePairsProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(14, 22, width - 26, height - 44);
        var mid = plot.Top + (plot.Height / 2);
        dc.DrawLine(ChartInk.Axis, new Point(plot.Left, mid), new Point(plot.Right, mid));
        for (var i = 0; i <= 4; i++)
        {
            var x = plot.Left + (plot.Width * i / 4);
            dc.DrawLine(ChartInk.Grid, new Point(x, plot.Top), new Point(x, plot.Bottom));
            Label(dc, $"{i * 90}°", new Point(x, plot.Bottom + 10), ChartInk.Dim, center: true, size: 9.5);
        }

        Point Map(double degrees, double value) => new(plot.Left + (degrees / 360 * plot.Width), mid - (value * plot.Height / 2 * 0.9));
        var angles = Enumerable.Range(0, 121).Select(i => i * 3.0).ToList();
        if (Kind == GeneratorKind.ThreePhase)
        {
            Color[] phases = [Color.FromRgb(0xFF, 0xD3, 0x4A), Color.FromRgb(0x5A, 0xD8, 0x7A), Color.FromRgb(0xFF, 0x5A, 0x5A)];
            var pairs = Math.Max(1, PolePairs);
            for (var k = 0; k < 3; k++)
            {
                var curve = angles.Select(a => Map(a, Math.Sin((a * pairs * Math.PI / 180) - (k * 2 * Math.PI / 3))));
                dc.DrawGeometry(null, LinePen(phases[k], 1.8), Curve(curve));
                var (va, vb, vc) = GeneratorModel.ThreePhase(1, Angle * Math.PI / 180, pairs);
                Marker(dc, Map(Angle, k switch { 0 => va, 1 => vb, _ => vc }), phases[k], 3);
            }

            Label(dc, Loc("Grid_Chart_Phases_Legend"), new Point(plot.Left, 10), ChartInk.Dim, size: 10);
        }
        else
        {
            var flux = angles.Select(a => Map(a, Math.Cos(a * Math.PI / 180)));
            dc.DrawGeometry(null, LinePen(ChartInk.Gold, 1.6, dashed: true), Curve(flux));
            var volts = angles.Select(a => Map(a, GeneratorModel.Output(Kind, Math.Sin(a * Math.PI / 180))));
            dc.DrawGeometry(null, LinePen(ChartInk.Scope, 2), Curve(volts));
            var phi = Angle * Math.PI / 180;
            Marker(dc, Map(Angle, Math.Cos(phi)), ChartInk.Gold, 3);
            Marker(dc, Map(Angle, GeneratorModel.Output(Kind, Math.Sin(phi))), ChartInk.Scope, 3.5);
            Label(dc, Loc("Grid_Chart_Flux_Legend"), new Point(plot.Left, 10), ChartInk.Gold, size: 10);
            Label(dc, Loc("Grid_Chart_Volts_Legend"), new Point(plot.Right, 10), ChartInk.Scope, alignRight: true, size: 10);
        }

        var cursor = Map(Angle, 0).X;
        dc.DrawLine(LinePen(ChartInk.Text, 1, alpha: 90), new Point(cursor, plot.Top), new Point(cursor, plot.Bottom));
    }
}

/// <summary>
/// What share of the power is lost on the way at each standard voltage, for the power and distance chosen (log scale):
/// each step up the ladder cuts it by the square of the step; the chosen voltage marked.
/// </summary>
public sealed class LossChart : GridChart
{
    static LossChart()
    {
        AffectsRender<LossChart>(LineKvProperty, PowerProperty, DistanceProperty);
    }

    public static readonly StyledProperty<double> LineKvProperty =
        AvaloniaProperty.Register<LossChart, double>(nameof(LineKv), TransmissionModel.DefaultKv);
    public static readonly StyledProperty<double> PowerProperty =
        AvaloniaProperty.Register<LossChart, double>(nameof(Power), TransmissionModel.DefaultPower);
    public static readonly StyledProperty<double> DistanceProperty =
        AvaloniaProperty.Register<LossChart, double>(nameof(Distance), TransmissionModel.DefaultDistance);

    public double LineKv
    {
        get => GetValue(LineKvProperty);
        set => SetValue(LineKvProperty, value);
    }

    public double Power
    {
        get => GetValue(PowerProperty);
        set => SetValue(PowerProperty, value);
    }

    public double Distance
    {
        get => GetValue(DistanceProperty);
        set => SetValue(DistanceProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(40, 12, width - 52, height - 36);

        // Log scale from 0.01% to 100%.
        const double low = -2, high = 2;
        double Y(double percent) => plot.Bottom - ((Math.Log10(Math.Clamp(percent, 0.01, 100)) - low) / (high - low) * plot.Height);
        foreach (var percent in new[] { 0.01, 0.1, 1.0, 10.0, 100.0 })
        {
            var y = Y(percent);
            dc.DrawLine(percent >= 100 ? LinePen(ChartInk.Red, 1, dashed: true, alpha: 160) : ChartInk.Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            Label(dc, $"{Number(percent, percent < 1 ? "0.##" : "0")}%", new Point(plot.Left - 4, y), ChartInk.Dim, alignRight: true, size: 9.5);
        }

        var levels = TransmissionModel.Levels;
        var slot = plot.Width / levels.Count;
        for (var i = 0; i < levels.Count; i++)
        {
            var kv = levels[i];
            var line = TransmissionModel.Send(Power, kv, Distance);
            var percent = line.LossFraction * 100;
            var x = plot.Left + (slot * (i + 0.5));
            var top = Y(Math.Min(100, percent));
            var chosen = Math.Abs(kv - LineKv) < 0.5;
            var color = line.Impossible ? ChartInk.Red : chosen ? ChartInk.Gold : ChartInk.Orange;
            var bar = new Rect(x - (slot * 0.3), top, slot * 0.6, Math.Max(1, plot.Bottom - top));
            dc.DrawRectangle(Solid((byte)(chosen ? 230 : 140), color.R, color.G, color.B), null, bar);
            if (line.Impossible) Label(dc, "✕", new Point(x, top - 8), ChartInk.Red, center: true, size: 10, bold: true);
            Label(dc, Number(kv, "0"), new Point(x, plot.Bottom + 10), chosen ? ChartInk.Gold : ChartInk.Dim, center: true, size: 9.5, bold: chosen);
        }

        Label(dc, "kV", new Point(plot.Right, plot.Bottom + 22), ChartInk.Dim, alignRight: true, size: 9.5);
    }
}

/// <summary>
/// The grid's day: what each kind of station makes hour by hour, stacked, with the demand as a line and pumping as a
/// dip below zero; the hour now marked.
/// </summary>
public sealed class DayMixChart : GridChart
{
    static DayMixChart()
    {
        AffectsRender<DayMixChart>(HourProperty, SeasonProperty, SolarCapacityProperty, LatitudeProperty);
    }

    public static readonly StyledProperty<double> HourProperty =
        AvaloniaProperty.Register<DayMixChart, double>(nameof(Hour), 12.0);
    public static readonly StyledProperty<GridSeason> SeasonProperty =
        AvaloniaProperty.Register<DayMixChart, GridSeason>(nameof(Season), GridSeason.Summer);
    public static readonly StyledProperty<double> SolarCapacityProperty =
        AvaloniaProperty.Register<DayMixChart, double>(nameof(SolarCapacity), GridDayModel.DefaultSolar);
    public static readonly StyledProperty<double> LatitudeProperty =
        AvaloniaProperty.Register<DayMixChart, double>(nameof(Latitude), 28.2);

    private (GridSeason, double, double) _key = (GridSeason.Summer, double.NaN, double.NaN);
    private GridMix[] _mixes = [];

    public double Hour
    {
        get => GetValue(HourProperty);
        set => SetValue(HourProperty, value);
    }

    public GridSeason Season
    {
        get => GetValue(SeasonProperty);
        set => SetValue(SeasonProperty, value);
    }

    public double SolarCapacity
    {
        get => GetValue(SolarCapacityProperty);
        set => SetValue(SolarCapacityProperty, value);
    }

    public double Latitude
    {
        get => GetValue(LatitudeProperty);
        set => SetValue(LatitudeProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var key = (Season, SolarCapacity, Latitude);
        if (key != _key)
        {
            _key = key;
            _mixes = Enumerable.Range(0, 97).Select(i => GridDayModel.Mix(Season, Latitude, i / 4.0, SolarCapacity)).ToArray();
        }

        var plot = new Rect(30, 10, width - 40, height - 32);
        const double top = 45, bottom = -3;
        double Y(double gw) => plot.Top + ((top - gw) / (top - bottom) * plot.Height);
        double X(double hour) => plot.Left + (hour / 24 * plot.Width);
        foreach (var gw in new[] { 0.0, 10, 20, 30, 40 })
        {
            dc.DrawLine(gw == 0 ? ChartInk.Axis : ChartInk.Grid, new Point(plot.Left, Y(gw)), new Point(plot.Right, Y(gw)));
            Label(dc, Number(gw, "0"), new Point(plot.Left - 4, Y(gw)), ChartInk.Dim, alignRight: true, size: 9.5);
        }

        for (var h = 0; h <= 24; h += 6)
        {
            Label(dc, $"{h:00}:00", new Point(X(h), plot.Bottom + 10), ChartInk.Dim, center: true, size: 9.5);
        }

        // The stack, bottom up in the rows' order (pumping drawn below zero instead).
        var below = new double[_mixes.Length];
        var stacked = new double[_mixes.Length];
        for (var source = 0; source < GridDiagram.Sources.Length; source++)
        {
            var color = GridDiagram.SourceColors[source];
            var lower = new List<Point>();
            var upper = new List<Point>();
            for (var i = 0; i < _mixes.Length; i++)
            {
                var value = GridDiagram.Output(_mixes[i], source);
                var hour = i / 4.0;
                if (source == 4 && value < 0)
                {
                    lower.Add(new Point(X(hour), Y(0)));
                    upper.Add(new Point(X(hour), Y(value)));
                    below[i] = value;
                    continue;
                }

                lower.Add(new Point(X(hour), Y(stacked[i])));
                stacked[i] += Math.Max(0, value);
                upper.Add(new Point(X(hour), Y(stacked[i])));
            }

            upper.Reverse();
            dc.DrawGeometry(Solid(210, color.R, color.G, color.B), null, Curve(lower.Concat(upper), closed: true));
        }

        var demand = Enumerable.Range(0, _mixes.Length).Select(i => new Point(X(i / 4.0), Y(_mixes[i].Demand)));
        dc.DrawGeometry(null, LinePen(ChartInk.Text, 1.6, dashed: true), Curve(demand));
        var x = X(GridDayModel.Wrap(Hour));
        dc.DrawLine(LinePen(ChartInk.Text, 1.2, alpha: 200), new Point(x, plot.Top), new Point(x, plot.Bottom));
        Label(dc, "GW", new Point(4, plot.Top - 2), ChartInk.Dim, size: 9.5);
    }
}

/// <summary>
/// The frequency after the last event: falling, caught by the governors, brought back by the control centre; the lowest
/// point and the shedding stages marked, the moment now as a dot.
/// </summary>
public sealed class FrequencyChart : GridChart
{
    static FrequencyChart()
    {
        AffectsRender<FrequencyChart>(TraceProperty, TimeProperty);
    }

    public static readonly StyledProperty<IReadOnlyList<FrequencyPoint>?> TraceProperty =
        AvaloniaProperty.Register<FrequencyChart, IReadOnlyList<FrequencyPoint>?>(nameof(Trace));
    public static readonly StyledProperty<double> TimeProperty =
        AvaloniaProperty.Register<FrequencyChart, double>(nameof(Time), 0.0);

    public IReadOnlyList<FrequencyPoint>? Trace
    {
        get => GetValue(TraceProperty);
        set => SetValue(TraceProperty, value);
    }

    public double Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(40, 12, width - 52, height - 36);
        if (Trace is not { Count: > 1 } trace)
        {
            Label(dc, Loc("Grid_Chart_Freq_Empty"), new Point(width / 2, height / 2), ChartInk.Dim, center: true, size: 11);
            return;
        }

        var lowest = FrequencyModel.Lowest(trace);
        var floor = Math.Min(49.6, Math.Floor((lowest.Frequency - 0.05) * 10) / 10);
        double Y(double hz) => plot.Top + ((50.1 - hz) / (50.1 - floor) * plot.Height);
        double X(double t) => plot.Left + (t / FrequencyModel.Duration * plot.Width);
        for (var hz = floor; hz <= 50.101; hz += 0.1)
        {
            var major = Math.Abs((hz * 10) - Math.Round(hz * 10)) < 0.01 && (Math.Round(hz * 10) % 2 == 0 || floor > 49.5);
            dc.DrawLine(Math.Abs(hz - 50) < 0.001 ? ChartInk.Axis : ChartInk.Grid, new Point(plot.Left, Y(hz)), new Point(plot.Right, Y(hz)));
            if (major) Label(dc, Number(hz, "0.0"), new Point(plot.Left - 4, Y(hz)), ChartInk.Dim, alignRight: true, size: 9.5);
        }

        foreach (var stage in FrequencyModel.SheddingStages.Where(s => s > floor))
        {
            dc.DrawLine(LinePen(ChartInk.Red, 1, dashed: true, alpha: 170), new Point(plot.Left, Y(stage)), new Point(plot.Right, Y(stage)));
        }

        foreach (var t in new[] { 0.0, 60, 120, 180 })
        {
            Label(dc, $"{t:0} s", new Point(X(t), plot.Bottom + 10), ChartInk.Dim, center: true, size: 9.5);
        }

        // The whole trace faint, the part played so far bright.
        var all = trace.Where((_, i) => i % 6 == 0).Select(p => new Point(X(p.Time), Y(p.Frequency))).ToList();
        dc.DrawGeometry(null, LinePen(ChartInk.Cyan, 1.2, alpha: 70), Curve(all));
        var played = trace.Where(p => p.Time <= Time).Where((_, i) => i % 3 == 0).Select(p => new Point(X(p.Time), Y(p.Frequency))).ToList();
        if (played.Count > 1) dc.DrawGeometry(null, LinePen(ChartInk.Cyan, 2), Curve(played));
        var now = FrequencyModel.At(trace, Time);
        Marker(dc, new Point(X(now.Time), Y(now.Frequency)), ChartInk.Cyan, 3.5);
        var nadir = new Point(X(lowest.Time), Y(lowest.Frequency));
        Marker(dc, nadir, ChartInk.Orange, 3);
        Label(dc, LocFormat("Grid_Chart_Freq_Lowest", Number(lowest.Frequency, "0.00"), Number(lowest.Time, "0")), nadir + new Vector(8, 10), ChartInk.Orange, size: 10);
    }
}
