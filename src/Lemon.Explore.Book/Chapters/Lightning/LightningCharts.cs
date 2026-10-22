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

namespace Lemon.Explore.Book.Chapters.Lightning;

/// <summary>A small chart of the lightning side panel: redrawn when a property changes or the language does.</summary>
public abstract class LightningChart : Control
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

    protected LightningChart()
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

    private protected static StreamGeometry Curve(IEnumerable<Point> points)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var first = true;
            foreach (var point in points)
            {
                if (first) context.BeginFigure(point, false);
                else context.LineTo(point);
                first = false;
            }

            if (!first) context.EndFigure(false);
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
        public static readonly Color Violet = Color.FromRgb(0xB3, 0x9D, 0xFF);
        public static readonly Color Plus = Color.FromRgb(0xFF, 0x6A, 0x5A);
        public static readonly Color Minus = Color.FromRgb(0x5A, 0xB0, 0xFF);
        public static readonly IPen Grid = MakePen(Solid(28, 255, 255, 255), 1);
        public static readonly IPen Zero = MakePen(Solid(90, 255, 255, 255), 1);
    }
}

/// <summary>The triboelectric series as bars, the rod and the cloth picked out, an arrow for the way the electrons go.</summary>
public sealed class SeriesChart : LightningChart
{
    static SeriesChart()
    {
        AffectsRender<SeriesChart>(RodProperty, ClothProperty);
    }

    public static readonly StyledProperty<Material> RodProperty =
        AvaloniaProperty.Register<SeriesChart, Material>(nameof(Rod), Material.Balloon);
    public static readonly StyledProperty<Material> ClothProperty =
        AvaloniaProperty.Register<SeriesChart, Material>(nameof(Cloth), Material.Hair);

    public Material Rod
    {
        get => GetValue(RodProperty);
        set => SetValue(RodProperty, value);
    }

    public Material Cloth
    {
        get => GetValue(ClothProperty);
        set => SetValue(ClothProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var series = StaticModel.Series;
        var top = 22.0;
        var row = (height - top - 8) / series.Count;
        var names = 12 + series.Max(m => Labels.Text(LightningDiagram.MaterialName(m), 10, ChartInk.Dim, true).Width);
        var zero = names + ((width - names - 30) * 0.42);
        var unit = (width - names - 30) * 0.58 / 100;

        Label(dc, Loc("Lightning_Chart_Series_Gives"), new Point(zero + 8, 9), ChartInk.Plus, size: 9.5);
        Label(dc, Loc("Lightning_Chart_Series_Takes"), new Point(zero - 8, 9), ChartInk.Minus, alignRight: true, size: 9.5);
        dc.DrawLine(ChartInk.Zero, new Point(zero, top - 2), new Point(zero, height - 6));

        double RowY(Material m) => top + ((Array.IndexOf(series.ToArray(), m) + 0.5) * row);
        foreach (var material in series)
        {
            var y = RowY(material);
            var a = StaticModel.Affinity(material);
            var chosen = material == Rod || material == Cloth;
            var color = a >= 0 ? ChartInk.Plus : ChartInk.Minus;
            var bar = a >= 0 ? new Rect(zero, y - (row * 0.3), a * unit, row * 0.6) : new Rect(zero + (a * unit), y - (row * 0.3), -a * unit, row * 0.6);
            dc.DrawRoundedRectangle(Solid((byte)(chosen ? 230 : 90), color.R, color.G, color.B), null, bar, 2, 2);
            var name = LightningDiagram.MaterialName(material);
            Label(dc, name, new Point(names - 6, y), material == Rod ? ChartInk.Gold : material == Cloth ? ChartInk.Cyan : ChartInk.Dim, alignRight: true, size: 10, bold: chosen);
        }

        // The electrons go from the higher (giver) to the lower (taker).
        var rodY = RowY(Rod);
        var clothY = RowY(Cloth);
        if (Math.Abs(rodY - clothY) < 1) return;
        var (from, to) = StaticModel.Affinity(Rod) > StaticModel.Affinity(Cloth) ? (rodY, clothY) : (clothY, rodY);
        var x = width - 14;
        var pen = LinePen(ChartInk.Minus, 1.6);
        dc.DrawLine(pen, new Point(x, from), new Point(x, to - (Math.Sign(to - from) * 6)));
        DrawArrowHead(dc, Solid(255, ChartInk.Minus.R, ChartInk.Minus.G, ChartInk.Minus.B), new Point(x, to), new Vector(0, Math.Sign(to - from)), 7);
        Label(dc, "e⁻", new Point(x - 4, ((from + to) / 2) - 8), ChartInk.Minus, alignRight: true, size: 10);
    }
}

/// <summary>A value over the last stretch of a clock, drawn as it comes in (the rod's charge, the field at the meter).</summary>
public sealed class HistoryChart : LightningChart
{
    static HistoryChart()
    {
        AffectsRender<HistoryChart>(SpanProperty, MinimumProperty, MaximumProperty, StepProperty, UnitProperty, MarkProperty, MarkTextProperty, BySignProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TimeProperty)
        {
            OnTimeChanged(change.GetOldValue<double>(), change.GetNewValue<double>());
        }
    }

    public static readonly StyledProperty<double> TimeProperty =
        AvaloniaProperty.Register<HistoryChart, double>(nameof(Time), 0.0);

    /// <summary>The value now; recorded (and drawn) when <see cref="Time"/> moves on, not on every change.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<HistoryChart, double>(nameof(Value), 0.0);
    public static readonly StyledProperty<double> SpanProperty =
        AvaloniaProperty.Register<HistoryChart, double>(nameof(Span), 30.0);
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<HistoryChart, double>(nameof(Minimum), -300.0);
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<HistoryChart, double>(nameof(Maximum), 300.0);
    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<HistoryChart, double>(nameof(Step), 100.0);
    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<HistoryChart, string>(nameof(Unit), string.Empty);

    /// <summary>A level to mark with a dashed line (e.g. where the field gets dangerous); NaN for none.</summary>
    public static readonly StyledProperty<double> MarkProperty =
        AvaloniaProperty.Register<HistoryChart, double>(nameof(Mark), double.NaN);

    public static readonly StyledProperty<string> MarkTextProperty =
        AvaloniaProperty.Register<HistoryChart, string>(nameof(MarkText), string.Empty);

    /// <summary>Colour the trace by sign: red above zero, blue below (for a charge).</summary>
    public static readonly StyledProperty<bool> BySignProperty =
        AvaloniaProperty.Register<HistoryChart, bool>(nameof(BySign), true);

    private readonly List<(double T, double V)> _points = [];
    private double _drawnAt = double.NegativeInfinity;

    public double Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Span
    {
        get => GetValue(SpanProperty);
        set => SetValue(SpanProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public string Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public double Mark
    {
        get => GetValue(MarkProperty);
        set => SetValue(MarkProperty, value);
    }

    public string MarkText
    {
        get => GetValue(MarkTextProperty);
        set => SetValue(MarkTextProperty, value);
    }

    public bool BySign
    {
        get => GetValue(BySignProperty);
        set => SetValue(BySignProperty, value);
    }

    private void OnTimeChanged(double old, double now)
    {
        if (now < old - 1e-9) _points.Clear();
        _points.Add((now, Value));
        while (_points.Count > 2 && _points[0].T < now - Span - 1) _points.RemoveAt(0);
        if (_points.Count > 4000) _points.RemoveRange(0, 1000);

        // Redrawn about 20 times a second, not every frame.
        if (now - _drawnAt >= 0.05 || now < _drawnAt)
        {
            _drawnAt = now;
            InvalidateVisual();
        }
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(40, 20, width - 52, height - 42);
        var lo = Minimum;
        var hi = Maximum;
        var now = Time;
        var span = Math.Max(1, Span);
        double X(double t) => plot.Right - ((now - t) / span * plot.Width);
        double Y(double v) => plot.Bottom - ((Math.Clamp(v, lo, hi) - lo) / (hi - lo) * plot.Height);

        for (var v = Math.Ceiling(lo / Step) * Step; v <= hi + 1e-9; v += Step)
        {
            dc.DrawLine(Math.Abs(v) < 1e-9 ? ChartInk.Zero : ChartInk.Grid, new Point(plot.Left, Y(v)), new Point(plot.Right, Y(v)));
            AxisLabel(dc, Number(v, "0"), new Point(plot.Left - 5, Y(v)), alignRight: true);
        }

        AxisLabel(dc, Unit, new Point(plot.Left - 5, plot.Top - 13), alignRight: true);
        for (var s = 0.0; s <= span + 1e-9; s += span / 3) AxisLabel(dc, s == 0 ? Loc("Lightning_Chart_Now") : LocFormat("Lightning_Chart_Ago", Number(s, "0")), new Point(X(now - s), plot.Bottom + 11), center: true);

        if (!double.IsNaN(Mark))
        {
            dc.DrawLine(LinePen(ChartInk.Plus, 1, dashed: true, alpha: 200), new Point(plot.Left, Y(Mark)), new Point(plot.Right, Y(Mark)));
            if (MarkText.Length > 0) Label(dc, MarkText, new Point(plot.Left + 4, Y(Mark) - 8), ChartInk.Plus, size: 9.5);
        }

        var visible = _points.Where(p => p.T >= now - span).Select(p => new Point(X(p.T), Y(p.V))).ToList();
        if (visible.Count > 1)
        {
            if (BySign)
            {
                dc.PushClip(new RectangleGeometry(new Rect(plot.Left, plot.Top, plot.Width, Y(0) - plot.Top)));
                dc.DrawGeometry(null, LinePen(ChartInk.Plus, 2), Curve(visible));
                dc.Pop();
                dc.PushClip(new RectangleGeometry(new Rect(plot.Left, Y(0), plot.Width, plot.Bottom - Y(0))));
                dc.DrawGeometry(null, LinePen(ChartInk.Minus, 2), Curve(visible));
                dc.Pop();
            }
            else
            {
                dc.DrawGeometry(null, LinePen(ChartInk.Gold, 2), Curve(visible));
            }
        }

        Marker(dc, new Point(plot.Right, Y(Value)), ChartInk.Gold, 3.5);
    }
}

/// <summary>Flashes a minute against the height of the storm's top (Price and Rind 1992), the storm now marked.</summary>
public sealed class FlashRateChart : LightningChart
{
    static FlashRateChart()
    {
        AffectsRender<FlashRateChart>(TopProperty);
    }

    public static readonly StyledProperty<double> TopProperty =
        AvaloniaProperty.Register<FlashRateChart, double>(nameof(Top), 12.0);

    public double Top
    {
        get => GetValue(TopProperty);
        set => SetValue(TopProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(34, 22, width - 46, height - 54);
        const double from = 6;
        const double to = 17;
        const double top = 40;
        double X(double km) => plot.Left + ((km - from) / (to - from) * plot.Width);
        double Y(double rate) => plot.Bottom - (Math.Min(top, rate) / top * plot.Height);

        for (var v = 0.0; v <= top; v += 10)
        {
            dc.DrawLine(ChartInk.Grid, new Point(plot.Left, Y(v)), new Point(plot.Right, Y(v)));
            AxisLabel(dc, Number(v, "0"), new Point(plot.Left - 5, Y(v)), alignRight: true);
        }

        for (var km = 6; km <= 16; km += 2) AxisLabel(dc, Number(km, "0"), new Point(X(km), plot.Bottom + 10), center: true);
        AxisLabel(dc, Loc("Lightning_Chart_Rate_Axis"), new Point(plot.Right, plot.Bottom + 24), alignRight: true);
        AxisLabel(dc, Loc("Lightning_Chart_Rate_Unit"), new Point(plot.Left - 5, plot.Top - 14), alignRight: true);

        var samples = Enumerable.Range(0, 121).Select(i => from + (i * (to - from) / 120)).Where(km => CloudModel.FlashRate(km) <= top * 1.02);
        dc.DrawGeometry(null, LinePen(ChartInk.Violet, 2.2), Curve(samples.Select(km => new Point(X(km), Y(CloudModel.FlashRate(km))))));
        var rate = CloudModel.FlashRate(Top);
        Marker(dc, new Point(X(Top), Y(rate)), ChartInk.Gold, 4);
        Label(dc, LocFormat("Lightning_Chart_Rate_Value", Number(rate, rate < 10 ? "0.0" : "0")), new Point(X(Top) - 8, Y(rate) - 12), ChartInk.Gold, alignRight: true, size: 10);
    }
}

/// <summary>The current of the first return stroke and a later one, on a log time scale (Heidler / IEC 62305 shapes).</summary>
public sealed class CurrentChart : LightningChart
{
    static CurrentChart()
    {
        AffectsRender<CurrentChart>(PeakProperty);
    }

    public static readonly StyledProperty<double> PeakProperty =
        AvaloniaProperty.Register<CurrentChart, double>(nameof(Peak), 30.0);

    public double Peak
    {
        get => GetValue(PeakProperty);
        set => SetValue(PeakProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(36, 22, width - 48, height - 54);
        const double from = 0.1;
        const double to = 1000;
        var top = Math.Max(20, Math.Ceiling(Peak * 1.15 / 20) * 20);
        double X(double us) => plot.Left + (Math.Log10(us / from) / Math.Log10(to / from) * plot.Width);
        double Y(double kiloAmps) => plot.Bottom - (kiloAmps / top * plot.Height);

        var step = top > 100 ? 50 : top > 40 ? 20 : 10;
        for (var v = 0.0; v <= top; v += step)
        {
            dc.DrawLine(ChartInk.Grid, new Point(plot.Left, Y(v)), new Point(plot.Right, Y(v)));
            AxisLabel(dc, Number(v, "0"), new Point(plot.Left - 5, Y(v)), alignRight: true);
        }

        foreach (var us in new[] { 0.1, 1, 10, 100, 1000 })
        {
            dc.DrawLine(ChartInk.Grid, new Point(X(us), plot.Top), new Point(X(us), plot.Bottom));
            AxisLabel(dc, Number(us, us < 1 ? "0.0" : "0"), new Point(X(us), plot.Bottom + 10), center: true);
        }

        AxisLabel(dc, "µs", new Point(plot.Right, plot.Bottom + 24), alignRight: true);
        AxisLabel(dc, "kA", new Point(plot.Left - 5, plot.Top - 14), alignRight: true);

        var times = Enumerable.Range(0, 241).Select(i => from * Math.Pow(to / from, i / 240.0)).ToArray();
        var later = StrikeModel.LaterPeak(Peak);
        dc.DrawGeometry(null, LinePen(ChartInk.Violet, 1.6, dashed: true), Curve(times.Select(t => new Point(X(t), Y(StrikeModel.Current(t, later, false))))));
        dc.DrawGeometry(null, LinePen(ChartInk.Gold, 2.2), Curve(times.Select(t => new Point(X(t), Y(StrikeModel.Current(t, Peak, true))))));

        Label(dc, LocFormat("Lightning_Chart_Current_First", Number(Peak, "0")), new Point(plot.Right - 4, plot.Top + 6), ChartInk.Gold, alignRight: true, size: 10);
        Label(dc, LocFormat("Lightning_Chart_Current_Later", Number(later, "0")), new Point(plot.Right - 4, plot.Top + 20), ChartInk.Violet, alignRight: true, size: 10);
    }
}

/// <summary>Striking distance (the rolling sphere's radius) against the peak current, with the IEC protection classes.</summary>
public sealed class ReachChart : LightningChart
{
    static ReachChart()
    {
        AffectsRender<ReachChart>(PeakProperty);
    }

    public static readonly StyledProperty<double> PeakProperty =
        AvaloniaProperty.Register<ReachChart, double>(nameof(Peak), 30.0);

    public double Peak
    {
        get => GetValue(PeakProperty);
        set => SetValue(PeakProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(36, 22, width - 48, height - 54);
        const double from = 2;
        const double to = 200;
        const double top = 300;
        double X(double kiloAmps) => plot.Left + (Math.Log10(kiloAmps / from) / Math.Log10(to / from) * plot.Width);
        double Y(double m) => plot.Bottom - (m / top * plot.Height);

        for (var v = 0.0; v <= top; v += 100)
        {
            dc.DrawLine(ChartInk.Grid, new Point(plot.Left, Y(v)), new Point(plot.Right, Y(v)));
            AxisLabel(dc, Number(v, "0"), new Point(plot.Left - 5, Y(v)), alignRight: true);
        }

        foreach (var ka in new[] { 2.0, 5, 10, 20, 50, 100, 200 }) AxisLabel(dc, Number(ka, "0"), new Point(X(ka), plot.Bottom + 10), center: true);
        AxisLabel(dc, "kA", new Point(plot.Right, plot.Bottom + 24), alignRight: true);
        AxisLabel(dc, "m", new Point(plot.Left - 5, plot.Top - 14), alignRight: true);

        // The rolling spheres of the four protection classes.
        foreach (var (name, radius) in new[] { ("I", 20.0), ("II", 30.0), ("III", 45.0), ("IV", 60.0) })
        {
            var ka = Math.Pow(radius / 10, 1 / 0.65);
            var p = new Point(X(ka), Y(radius));
            dc.DrawEllipse(Solid(255, ChartInk.Cyan.R, ChartInk.Cyan.G, ChartInk.Cyan.B), null, p, 2.5, 2.5);
            Label(dc, name, p + new Vector(0, -9), ChartInk.Cyan, center: true, size: 9);
        }

        var samples = Enumerable.Range(0, 121).Select(i => from * Math.Pow(to / from, i / 120.0));
        dc.DrawGeometry(null, LinePen(ChartInk.Orange, 2.2), Curve(samples.Select(ka => new Point(X(ka), Y(StrikeModel.StrikingDistance(ka))))));
        var reach = StrikeModel.StrikingDistance(Peak);
        Marker(dc, new Point(X(Math.Clamp(Peak, from, to)), Y(reach)), ChartInk.Gold, 4);
        Label(dc, LocFormat("Lightning_Chart_Reach_Value", Number(reach, "0")), new Point(X(Math.Clamp(Peak, from, to)) - 8, Y(reach) - 12), ChartInk.Gold, alignRight: true, size: 10);
        Label(dc, Loc("Lightning_Chart_Reach_Classes"), new Point(plot.Right - 4, plot.Bottom - 10), ChartInk.Cyan, alignRight: true, size: 9.5);
    }
}
