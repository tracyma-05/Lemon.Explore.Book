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

namespace Lemon.Explore.Book.Chapters.WaterCycle;

/// <summary>A small chart of the water-cycle side panel: redrawn when a property changes or the language does.</summary>
public abstract class WaterChart : Control
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

    protected WaterChart()
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
        var text = Labels.Plain(value, 10, ChartInk.Dim);
        var x = alignRight ? at.X - text.Width : center ? at.X - (text.Width / 2) : at.X;
        dc.DrawText(text, new Point(x, at.Y - (text.Height / 2)));
    }

    /// <summary>A localized label beside a point, kept inside the plot.</summary>
    private protected void PointLabel(PaintContext dc, string value, Point point, Rect plot, Color color, bool below = false, bool left = false)
    {
        var text = Labels.Text(value, 11, color, true);
        var at = new Point(left ? point.X - text.Width - 8 : point.X + 8, below ? point.Y + 4 : point.Y - text.Height - 3);
        if (at.X + text.Width > plot.Right) at = at.WithX(point.X - text.Width - 8);
        if (at.X < plot.Left) at = at.WithX(point.X + 8);
        at = at.WithY(Math.Clamp(at.Y, plot.Top - 4, plot.Bottom - text.Height));
        dc.DrawText(text, at);
    }

    private protected static void Marker(PaintContext dc, Point point, Color color, double radius = 4.5)
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

    private protected static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private protected static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private protected static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    private protected static class ChartInk
    {
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
        public static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
        public static readonly Color Green = Color.FromRgb(0x8B, 0xE0, 0x8F);
        public static readonly Color Orange = Color.FromRgb(0xFF, 0x9F, 0x5A);
        public static readonly Color Blue = Color.FromRgb(0x5A, 0xB0, 0xFF);
        public static readonly IPen Grid = MakePen(Solid(28, 255, 255, 255), 1);
        public static readonly IPen Zero = MakePen(Solid(90, 0x9F, 0xD8, 0xFF), 1, dashed: true);
        public static readonly IPen Guide = MakePen(Solid(110, 255, 255, 255), 1, dashed: true);
    }
}

/// <summary>
/// How much water vapour a cubic metre of air can hold against its temperature: the curve gets steeper and steeper,
/// about 7 % more for every degree. Marks today's sea-level air and the warmer (or colder) world of the page.
/// </summary>
public sealed class VapourChart : WaterChart
{
    static VapourChart()
    {
        AffectsRender<VapourChart>(WarmingProperty);
    }

    public static readonly StyledProperty<double> WarmingProperty =
        AvaloniaProperty.Register<VapourChart, double>(nameof(Warming), 0.0);

    public double Warming
    {
        get => GetValue(WarmingProperty);
        set => SetValue(WarmingProperty, value);
    }

    private const double Low = -20;
    private const double High = 40;
    private const double Top = 55;

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(36, 12, width - 48, height - 34);
        double X(double t) => plot.Left + ((t - Low) / (High - Low) * plot.Width);
        double Y(double grams) => plot.Bottom - (grams / Top * plot.Height);

        for (var g = 0; g <= 50; g += 10)
        {
            dc.DrawLine(ChartInk.Grid, new Point(plot.Left, Y(g)), new Point(plot.Right, Y(g)));
            AxisLabel(dc, $"{g}", new Point(plot.Left - 5, Y(g)), alignRight: true);
        }

        for (var t = (int)Low; t <= High; t += 10)
        {
            AxisLabel(dc, $"{t}°", new Point(X(t), plot.Bottom + 9), center: true);
        }

        dc.DrawLine(ChartInk.Zero, new Point(X(0), plot.Top), new Point(X(0), plot.Bottom));
        AxisLabel(dc, Loc("Water_Chart_Vapour_Unit"), new Point(plot.Left + 4, plot.Top + 2));

        var points = new List<Point>();
        for (var t = Low; t <= High + 1e-9; t += 0.5) points.Add(new Point(X(t), Y(WaterCycleModel.SaturationDensity(t))));
        dc.DrawGeometry(null, MakePen(Solid(255, ChartInk.Cyan.R, ChartInk.Cyan.G, ChartInk.Cyan.B), 2), Curve(points));

        var today = CycleScene.SeaLevelTemperature;
        var now = today + Warming;
        var todayPoint = new Point(X(today), Y(WaterCycleModel.SaturationDensity(today)));
        var nowPoint = new Point(X(now), Y(WaterCycleModel.SaturationDensity(now)));
        dc.DrawLine(ChartInk.Guide, new Point(todayPoint.X, plot.Bottom), todayPoint);
        Marker(dc, todayPoint, ChartInk.Text, 3.5);
        if (Math.Abs(Warming) > 0.05)
        {
            Marker(dc, nowPoint, Warming > 0 ? ChartInk.Orange : ChartInk.Cyan);
            var change = (WaterCycleModel.VapourFactor(today, Warming) - 1) * 100;
            PointLabel(dc, LocFormat("Water_Chart_Vapour_Change", (change >= 0 ? "+" : string.Empty) + Number(change, "0")), nowPoint, plot,
                Warming > 0 ? ChartInk.Orange : ChartInk.Cyan, left: true);
        }
        else
        {
            PointLabel(dc, LocFormat("Water_Chart_Vapour_Today", Number(WaterCycleModel.SaturationDensity(today), "0.0")), todayPoint, plot, ChartInk.Text, left: true);
        }
    }
}

/// <summary>
/// The lifted parcel's temperature and dew point against height: the two lines close in on the way up and meet at the
/// cloud base, then go up together more slowly. Down the far side of the mountain the temperature comes back warmer.
/// </summary>
public sealed class ParcelChart : WaterChart
{
    static ParcelChart()
    {
        AffectsRender<ParcelChart>(LiftProperty, GroundTemperatureProperty, HumidityProperty, TimeProperty);
    }

    public static readonly StyledProperty<CloudLift> LiftProperty =
        AvaloniaProperty.Register<ParcelChart, CloudLift>(nameof(Lift), CloudLift.Thermal);
    public static readonly StyledProperty<double> GroundTemperatureProperty =
        AvaloniaProperty.Register<ParcelChart, double>(nameof(GroundTemperature), 20.0);
    public static readonly StyledProperty<double> HumidityProperty =
        AvaloniaProperty.Register<ParcelChart, double>(nameof(Humidity), 0.6);
    public static readonly StyledProperty<double> TimeProperty =
        AvaloniaProperty.Register<ParcelChart, double>(nameof(Time), 0.0);

    public CloudLift Lift
    {
        get => GetValue(LiftProperty);
        set => SetValue(LiftProperty, value);
    }

    public double GroundTemperature
    {
        get => GetValue(GroundTemperatureProperty);
        set => SetValue(GroundTemperatureProperty, value);
    }

    public double Humidity
    {
        get => GetValue(HumidityProperty);
        set => SetValue(HumidityProperty, value);
    }

    public double Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var ascent = WaterCycleModel.Lift(GroundTemperature, Humidity, WaterCycleModel.LiftTop(Lift));
        var descent = Lift == CloudLift.Mountain ? WaterCycleModel.OverMountain(ascent) : [];
        var all = ascent.Points.Concat(descent).ToList();
        var low = Math.Floor((all.Min(p => Math.Min(p.Temperature, p.DewPoint)) - 2) / 10) * 10;
        var high = Math.Ceiling((all.Max(p => p.Temperature) + 2) / 10) * 10;
        var top = Math.Ceiling(ascent.Top.Height / 1000) * 1000;

        var plot = new Rect(40, 12, width - 52, height - 34);
        double X(double t) => plot.Left + ((t - low) / (high - low) * plot.Width);
        double Y(double z) => plot.Bottom - (z / top * plot.Height);

        var stepKm = top > 4000 ? 2000 : 1000;
        for (var z = 0.0; z <= top + 1e-9; z += stepKm)
        {
            dc.DrawLine(ChartInk.Grid, new Point(plot.Left, Y(z)), new Point(plot.Right, Y(z)));
            AxisLabel(dc, LocFormat("Water_Km", Number(z / 1000, "0")), new Point(plot.Left - 5, Y(z)), alignRight: true);
        }

        var stepT = high - low > 40 ? 20 : 10;
        for (var t = low; t <= high + 1e-9; t += stepT)
        {
            AxisLabel(dc, $"{Number(t, "0")}°", new Point(X(t), plot.Bottom + 9), center: true);
        }

        if (low < 0 && high > 0) dc.DrawLine(ChartInk.Zero, new Point(X(0), plot.Top), new Point(X(0), plot.Bottom));
        if (ascent.HasCloud)
        {
            var y = Y(ascent.CloudBase);
            dc.DrawLine(ChartInk.Guide, new Point(plot.Left, y), new Point(plot.Right, y));
            AxisLabel(dc, Loc("Water_Chart_Parcel_Base"), new Point(plot.Right - 2, y - 8), alignRight: true);
        }

        var orange = Solid(255, ChartInk.Orange.R, ChartInk.Orange.G, ChartInk.Orange.B);
        var green = Solid(255, ChartInk.Green.R, ChartInk.Green.G, ChartInk.Green.B);
        dc.DrawGeometry(null, MakePen(green, 1.8, dashed: true), Curve(ascent.Points.Select(p => new Point(X(p.DewPoint), Y(p.Height)))));
        dc.DrawGeometry(null, MakePen(orange, 2.2), Curve(ascent.Points.Select(p => new Point(X(p.Temperature), Y(p.Height)))));
        if (descent.Count > 0)
        {
            dc.DrawGeometry(null, MakePen(Solid(170, ChartInk.Orange.R, ChartInk.Orange.G, ChartInk.Orange.B), 1.6, dashed: true),
                Curve(descent.Select(p => new Point(X(p.Temperature), Y(p.Height)))));
            dc.DrawGeometry(null, MakePen(Solid(130, ChartInk.Green.R, ChartInk.Green.G, ChartInk.Green.B), 1.2, dashed: true),
                Curve(descent.Select(p => new Point(X(p.DewPoint), Y(p.Height)))));
        }

        // Where the parcel is now.
        var place = CloudScene.AtTime(Lift, Time);
        var state = CloudScene.State(ascent, descent, place);
        var at = new Point(X(state.Temperature), Y(state.Height));
        dc.DrawLine(ChartInk.Guide, new Point(X(state.DewPoint), at.Y), at);
        Marker(dc, new Point(X(state.DewPoint), at.Y), ChartInk.Green, 3);
        Marker(dc, at, place.Descending ? ChartInk.Orange : ChartInk.Gold);

        // The key, top right where the lines never go (high up the air is cold).
        var temperatureKey = Labels.Text(Loc("Water_Chart_Parcel_Temperature"), 10.5, ChartInk.Text);
        var dewKey = Labels.Text(Loc("Water_Chart_Parcel_DewPoint"), 10.5, ChartInk.Text);
        var keyLeft = plot.Right - Math.Max(temperatureKey.Width, dewKey.Width) - 30;
        var keyY = plot.Top + 6;
        dc.DrawLine(MakePen(orange, 2.2), new Point(keyLeft, keyY), new Point(keyLeft + 16, keyY));
        dc.DrawText(temperatureKey, new Point(keyLeft + 20, keyY - (temperatureKey.Height / 2)));
        dc.DrawLine(MakePen(green, 1.8, dashed: true), new Point(keyLeft, keyY + 15), new Point(keyLeft + 16, keyY + 15));
        dc.DrawText(dewKey, new Point(keyLeft + 20, keyY + 15 - (dewKey.Height / 2)));
    }
}

/// <summary>If all of Earth's water were one litre: how many millilitres each store would hold, on a scale of powers of ten.</summary>
public sealed class LitreChart : WaterChart
{
    private static readonly WaterStore[] Stores =
    [
        WaterStore.Oceans, WaterStore.IceAndSnow, WaterStore.Groundwater, WaterStore.GroundIce, WaterStore.Lakes,
        WaterStore.SoilMoisture, WaterStore.Atmosphere, WaterStore.Swamps, WaterStore.Rivers, WaterStore.Living,
    ];

    /// <summary>A drop from a dropper, millilitres.</summary>
    private const double DropVolume = 0.05;

    protected override void Draw(PaintContext dc, double width, double height)
    {
        const double lowPower = -4;
        const double highPower = 3;
        var nameWidth = Stores.Max(s => Labels.Text(Loc($"Water_StoreName_{s}"), 11, ChartInk.Text).Width) + 8;
        var plot = new Rect(nameWidth, 4, width - nameWidth - 6, height - 26);
        double X(double ml) => plot.Left + ((Math.Log10(Math.Max(ml, 1e-6)) - lowPower) / (highPower - lowPower) * plot.Width);

        for (var power = lowPower; power <= highPower; power++)
        {
            var x = X(Math.Pow(10, power));
            dc.DrawLine(ChartInk.Grid, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var label = power >= 0 ? Number(Math.Pow(10, power), "0") : Number(Math.Pow(10, power), "0.####");
            AxisLabel(dc, label, new Point(x, plot.Bottom + 9), center: true);
        }


        var row = plot.Height / Stores.Length;
        for (var i = 0; i < Stores.Length; i++)
        {
            var store = Stores[i];
            var ml = WaterCycleModel.Share(store) * 1000;
            var y = plot.Top + (row * (i + 0.5));
            var name = Labels.Text(Loc($"Water_StoreName_{store}"), 11, ChartInk.Text);
            dc.DrawText(name, new Point(plot.Left - name.Width - 6, y - (name.Height / 2)));

            var color = store switch
            {
                WaterStore.Oceans => ChartInk.Blue,
                WaterStore.IceAndSnow or WaterStore.GroundIce => ChartInk.Text,
                WaterStore.Atmosphere => ChartInk.Cyan,
                WaterStore.Living or WaterStore.SoilMoisture => ChartInk.Green,
                _ => ChartInk.Cyan,
            };
            var bar = new Rect(plot.Left, y - (row * 0.3), Math.Max(1.5, X(ml) - plot.Left), row * 0.6);
            dc.DrawRectangle(Solid(150, color.R, color.G, color.B), null, bar);

            var value = ml >= 1
                ? LocFormat("Water_Chart_Litre_Ml", Number(ml, ml >= 100 ? "0" : "0.#"))
                : LocFormat("Water_Chart_Litre_Drops", Number(ml, ml >= 0.01 ? "0.###" : "0.####"), Number(ml / DropVolume, ml / DropVolume >= 1 ? "0.#" : "0.##"));
            var text = Labels.Text(value, 10.5, ChartInk.Dim);
            var tx = bar.Right + 5;
            if (tx + text.Width > plot.Right) tx = bar.Right - text.Width - 5;
            dc.DrawText(Labels.Text(value, 10.5, tx < bar.Right ? ChartInk.Text : ChartInk.Dim), new Point(tx, y - (text.Height / 2)));
        }
    }
}
