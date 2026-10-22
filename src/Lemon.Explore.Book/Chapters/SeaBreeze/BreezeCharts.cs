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

namespace Lemon.Explore.Book.Chapters.SeaBreeze;

/// <summary>A small chart of the sea-breeze side panel: redrawn when a property changes or the language does.</summary>
public abstract class BreezeChart : Control
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

    protected BreezeChart()
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

    /// <summary>A localized label with its left edge (or right edge) at a point, its middle at the point's height.</summary>
    private protected void Label(PaintContext dc, string value, Point at, Color color, bool alignRight = false, double size = 10.5, bool bold = false)
    {
        var text = Labels.Text(value, size, color, bold);
        dc.DrawText(text, new Point(alignRight ? at.X - text.Width : at.X, at.Y - (text.Height / 2)));
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

    private protected static IPen LinePen(Color color, double thickness, bool dashed = false) =>
        MakePen(Solid(255, color.R, color.G, color.B), thickness, dashed);

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
        public static readonly Color Blue = Color.FromRgb(0x5A, 0xB0, 0xFF);
        public static readonly Color Violet = Color.FromRgb(0xC8, 0xB8, 0xFF);
        public static readonly IPen Grid = MakePen(Solid(28, 255, 255, 255), 1);
        public static readonly IPen Zero = MakePen(Solid(110, 255, 255, 255), 1);
        public static readonly IPen Guide = MakePen(Solid(150, 0xFF, 0xD3, 0x6B), 1, dashed: true);
        public static readonly IBrush Night = Solid(40, 0x5A, 0x6A, 0xA0);
    }
}

/// <summary>
/// The coast's day: the land's air and the sea's surface through 24 hours (the land swings, the sea hardly moves), and
/// under it the wind at the beach they drive, onshore by day, offshore by night. Marks the time shown.
/// </summary>
public sealed class DayChart : BreezeChart
{
    static DayChart()
    {
        AffectsRender<DayChart>(HourProperty, SkyProperty);
    }

    public static readonly StyledProperty<double> HourProperty =
        AvaloniaProperty.Register<DayChart, double>(nameof(Hour), 14.0);
    public static readonly StyledProperty<BreezeSky> SkyProperty =
        AvaloniaProperty.Register<DayChart, BreezeSky>(nameof(Sky), BreezeSky.Clear);

    public double Hour
    {
        get => GetValue(HourProperty);
        set => SetValue(HourProperty, value);
    }

    public BreezeSky Sky
    {
        get => GetValue(SkyProperty);
        set => SetValue(SkyProperty, value);
    }

    private const double Cold = 17;
    private const double Hot = 33;
    private const double WindLow = -3;
    private const double WindHigh = 6;

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var temperatures = new Rect(34, 10, width - 46, (height * 0.52) - 14);
        var winds = new Rect(34, (height * 0.52) + 8, width - 46, (height * 0.48) - 30);
        double X(double hour) => temperatures.Left + (hour / 24 * temperatures.Width);
        double YT(double celsius) => temperatures.Bottom - ((celsius - Cold) / (Hot - Cold) * temperatures.Height);
        double YW(double speed) => winds.Bottom - ((speed - WindLow) / (WindHigh - WindLow) * winds.Height);

        // Night bands.
        foreach (var plot in new[] { temperatures, winds })
        {
            dc.DrawRectangle(ChartInk.Night, null, new Rect(plot.Left, plot.Top, X(BreezeModel.Sunrise) - plot.Left, plot.Height));
            dc.DrawRectangle(ChartInk.Night, null, new Rect(X(BreezeModel.Sunset), plot.Top, plot.Right - X(BreezeModel.Sunset), plot.Height));
        }

        for (var c = 20; c <= 30; c += 5)
        {
            dc.DrawLine(ChartInk.Grid, new Point(temperatures.Left, YT(c)), new Point(temperatures.Right, YT(c)));
            AxisLabel(dc, $"{c}°", new Point(temperatures.Left - 4, YT(c)), alignRight: true);
        }

        foreach (var speed in new[] { -2, 0, 2, 4 })
        {
            dc.DrawLine(speed == 0 ? ChartInk.Zero : ChartInk.Grid, new Point(winds.Left, YW(speed)), new Point(winds.Right, YW(speed)));
            AxisLabel(dc, $"{speed}", new Point(winds.Left - 4, YW(speed)), alignRight: true);
        }

        for (var hour = 0; hour <= 24; hour += 6)
        {
            AxisLabel(dc, $"{hour:00}:00", new Point(X(hour), winds.Bottom + 10), center: true);
        }

        // The gap between land and sea, warm where the land is warmer.
        const double Step = 0.25;
        for (var hour = 0.0; hour < 24 - 1e-9; hour += Step)
        {
            var land0 = BreezeModel.LandTemperature(hour, Sky);
            var land1 = BreezeModel.LandTemperature(hour + Step, Sky);
            var sea0 = BreezeModel.SeaTemperature(hour, Sky);
            var sea1 = BreezeModel.SeaTemperature(hour + Step, Sky);
            var warm = land0 + land1 > sea0 + sea1;
            var color = warm ? ChartInk.Orange : ChartInk.Blue;
            dc.DrawGeometry(Solid(60, color.R, color.G, color.B), null, Polygon(
                new Point(X(hour), YT(land0)), new Point(X(hour + Step), YT(land1)), new Point(X(hour + Step), YT(sea1)), new Point(X(hour), YT(sea0))));
        }

        var land = new List<Point>();
        var sea = new List<Point>();
        var wind = new List<Point>();
        for (var hour = 0.0; hour <= 24 + 1e-9; hour += 0.1)
        {
            land.Add(new Point(X(hour), YT(BreezeModel.LandTemperature(hour, Sky))));
            sea.Add(new Point(X(hour), YT(BreezeModel.SeaTemperature(hour, Sky))));
            wind.Add(new Point(X(hour), YW(BreezeModel.ShoreWind(hour, Sky))));
        }

        // Wind: filled above the zero line (sea breeze) and below it (land breeze).
        var zero = YW(0);
        foreach (var (color, above) in new[] { (ChartInk.Cyan, true), (ChartInk.Violet, false) })
        {
            var area = new StreamGeometry();
            using (var context = area.Open())
            {
                context.BeginFigure(new Point(wind[0].X, zero), true);
                foreach (var point in wind) context.LineTo(new Point(point.X, above ? Math.Min(point.Y, zero) : Math.Max(point.Y, zero)));
                context.LineTo(new Point(wind[^1].X, zero));

                context.EndFigure(true);
            }
            dc.DrawGeometry(Solid(70, color.R, color.G, color.B), null, area);
        }

        dc.DrawGeometry(null, LinePen(ChartInk.Text, 1.6), Curve(wind));
        dc.DrawGeometry(null, LinePen(ChartInk.Cyan, 2), Curve(sea));
        dc.DrawGeometry(null, LinePen(ChartInk.Orange, 2), Curve(land));

        Label(dc, Loc("Breeze_Chart_Land"), new Point(X(14) + 4, YT(BreezeModel.LandTemperature(14, Sky)) - 9), ChartInk.Orange);
        Label(dc, Loc("Breeze_Chart_Sea"), new Point(temperatures.Right - 2, YT(BreezeModel.SeaTemperature(23, Sky)) - 9), ChartInk.Cyan, alignRight: true);
        Label(dc, Loc("Breeze_Chart_SeaBreeze"), new Point(winds.Left + 4, YW(WindHigh) + 8), ChartInk.Cyan);
        Label(dc, Loc("Breeze_Chart_LandBreeze"), new Point(winds.Left + 4, YW(WindLow) - 8), ChartInk.Violet);
        AxisLabel(dc, "m/s", new Point(winds.Right - 2, YW(WindHigh) + 7), alignRight: true);

        // Now.
        var now = BreezeModel.Wrap(Hour);
        var x = X(now);
        dc.DrawLine(ChartInk.Guide, new Point(x, temperatures.Top), new Point(x, winds.Bottom));
        Marker(dc, new Point(x, YT(BreezeModel.LandTemperature(now, Sky))), ChartInk.Orange, 3.5);
        Marker(dc, new Point(x, YT(BreezeModel.SeaTemperature(now, Sky))), ChartInk.Cyan, 3.5);
        Marker(dc, new Point(x, YW(BreezeModel.ShoreWind(now, Sky))), ChartInk.Text, 3.5);
    }
}

/// <summary>
/// How much higher the pressure over the land is than over the sea at each height: just after heating (dashed) the
/// columns still match at the ground and the warm land's is higher aloft; once the air has moved (solid) it is lower at the
/// ground and higher aloft, changing sign part of the way up.
/// </summary>
public sealed class PressureProfileChart : BreezeChart
{
    static PressureProfileChart()
    {
        AffectsRender<PressureProfileChart>(LandTemperatureProperty, SeaTemperatureProperty, StepTimeProperty);
    }

    public static readonly StyledProperty<double> LandTemperatureProperty =
        AvaloniaProperty.Register<PressureProfileChart, double>(nameof(LandTemperature), 32.0);
    public static readonly StyledProperty<double> SeaTemperatureProperty =
        AvaloniaProperty.Register<PressureProfileChart, double>(nameof(SeaTemperature), 24.0);
    public static readonly StyledProperty<double> StepTimeProperty =
        AvaloniaProperty.Register<PressureProfileChart, double>(nameof(StepTime), 0.0);

    public double LandTemperature
    {
        get => GetValue(LandTemperatureProperty);
        set => SetValue(LandTemperatureProperty, value);
    }

    public double SeaTemperature
    {
        get => GetValue(SeaTemperatureProperty);
        set => SetValue(SeaTemperatureProperty, value);
    }

    public double StepTime
    {
        get => GetValue(StepTimeProperty);
        set => SetValue(StepTimeProperty, value);
    }

    private const double Top = 1800;

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(46, 24, width - 58, height - 58);
        var before = new List<(double Z, double Dp)>();
        var after = new List<(double Z, double Dp)>();
        for (var z = 0.0; z <= Top + 1e-9; z += 50)
        {
            before.Add((z, BreezeModel.PressureDifference(z, LandTemperature, SeaTemperature, settled: false)));
            after.Add((z, BreezeModel.PressureDifference(z, LandTemperature, SeaTemperature, settled: true)));
        }

        var largest = Math.Max(before.Max(p => Math.Abs(p.Dp)), after.Max(p => Math.Abs(p.Dp)));
        var range = Math.Max(0.5, Math.Ceiling(largest * 1.25 * 2) / 2);
        double X(double dp) => plot.Left + ((dp + range) / (2 * range) * plot.Width);
        double Y(double z) => plot.Bottom - (z / Top * plot.Height);

        for (var z = 0; z <= Top; z += 500)
        {
            dc.DrawLine(ChartInk.Grid, new Point(plot.Left, Y(z)), new Point(plot.Right, Y(z)));
            AxisLabel(dc, $"{z} m", new Point(plot.Left - 4, Y(z)), alignRight: true);
        }

        var tick = range > 1.5 ? 1 : 0.5;
        for (var dp = -range; dp <= range + 1e-9; dp += tick)
        {
            AxisLabel(dc, Number(dp, "+0.#;−0.#;0"), new Point(X(dp), plot.Bottom + 9), center: true);
        }

        dc.DrawLine(ChartInk.Zero, new Point(X(0), plot.Top), new Point(X(0), plot.Bottom));
        Label(dc, Loc("Breeze_Chart_LandHigher"), new Point(plot.Right - 2, plot.Top - 12), ChartInk.Orange, alignRight: true, size: 10);
        Label(dc, Loc("Breeze_Chart_LandLower"), new Point(plot.Left + 4, plot.Top - 12), ChartInk.Blue, size: 10);

        var settled = BreezeModel.StageAt(StepTime) >= CellStage.SurfacePressure;
        var beforePen = LinePen(ChartInk.Gold, settled ? 1.2 : 2.4, dashed: true);
        var afterPen = LinePen(ChartInk.Cyan, settled ? 2.4 : 1.2);
        dc.PushOpacity(settled ? 0.55 : 1);
        dc.DrawGeometry(null, beforePen, Curve(before.Select(p => new Point(X(p.Dp), Y(p.Z)))));
        dc.Pop();
        dc.PushOpacity(settled ? 1 : 0.45);
        dc.DrawGeometry(null, afterPen, Curve(after.Select(p => new Point(X(p.Dp), Y(p.Z)))));
        dc.Pop();

        // Each curve's name beside it, on the side toward the zero line.
        var beforeAt = before.First(p => p.Z >= 1300);
        var afterAt = after.First(p => p.Z >= 250);
        Label(dc, Loc("Breeze_Chart_JustHeated"), new Point(X(beforeAt.Dp) + (beforeAt.Dp >= 0 ? -6 : 6), Y(beforeAt.Z)), ChartInk.Gold,
            alignRight: beforeAt.Dp >= 0, size: 10);
        Label(dc, Loc("Breeze_Chart_AfterFlow"), new Point(X(afterAt.Dp) + (afterAt.Dp >= 0 ? -6 : 6), Y(afterAt.Z)), ChartInk.Cyan,
            alignRight: afterAt.Dp >= 0, size: 10);
        AxisLabel(dc, Loc("Breeze_Chart_PressureAxis"), new Point(plot.Left + (plot.Width / 2), height - 4), center: true);
    }
}

/// <summary>A city's typical rainfall month by month, the month shown picked out, and its yearly total.</summary>
public sealed class RainChart : BreezeChart
{
    static RainChart()
    {
        AffectsRender<RainChart>(CityProperty, MonthProperty);
    }

    public static readonly StyledProperty<MonsoonCity> CityProperty =
        AvaloniaProperty.Register<RainChart, MonsoonCity>(nameof(City), MonsoonCity.Mumbai);
    public static readonly StyledProperty<double> MonthProperty =
        AvaloniaProperty.Register<RainChart, double>(nameof(Month), 6.5);

    public MonsoonCity City
    {
        get => GetValue(CityProperty);
        set => SetValue(CityProperty, value);
    }

    public double Month
    {
        get => GetValue(MonthProperty);
        set => SetValue(MonthProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var city = BreezeModel.City(City);
        var plot = new Rect(38, 22, width - 48, height - 46);
        var top = Math.Max(100, Math.Ceiling(city.Rain.Max() * 1.1 / 100) * 100);
        double Y(double mm) => plot.Bottom - (mm / top * plot.Height);

        var step = top > 600 ? 200 : 100;
        for (var mm = 0; mm <= top; mm += step)
        {
            dc.DrawLine(ChartInk.Grid, new Point(plot.Left, Y(mm)), new Point(plot.Right, Y(mm)));
            AxisLabel(dc, $"{mm}", new Point(plot.Left - 4, Y(mm)), alignRight: true);
        }

        var current = BreezeModel.MonthNumber(Month);
        var slot = plot.Width / 12;
        var culture = LocalizationService.Instance.CurrentCulture;
        for (var i = 0; i < 12; i++)
        {
            var mm = city.Rain[i];
            var picked = i + 1 == current;
            var color = picked ? ChartInk.Gold : ChartInk.Blue;
            var bar = new Rect(plot.Left + (i * slot) + (slot * 0.18), Y(mm), slot * 0.64, Math.Max(0.5, plot.Bottom - Y(mm)));
            dc.DrawRectangle(Solid(picked ? (byte)255 : (byte)190, color.R, color.G, color.B), null, bar);
            var name = culture.TwoLetterISOLanguageName == "zh" ? $"{i + 1}" : culture.DateTimeFormat.GetAbbreviatedMonthName(i + 1)[..1];
            AxisLabel(dc, name, new Point(bar.Left + (bar.Width / 2), plot.Bottom + 9), center: true);
            if (picked) Label(dc, Number(mm, "0"), new Point(bar.Left + (bar.Width / 2) - 8, bar.Top - 8), ChartInk.Gold, size: 10, bold: true);
        }

        Label(dc, LocFormat("Breeze_Chart_Year", Number(city.YearRain, "N0")), new Point(plot.Right, 9), ChartInk.Text, alignRight: true, size: 10.5);
        AxisLabel(dc, "mm", new Point(plot.Left - 4, 9), alignRight: true);
    }
}
