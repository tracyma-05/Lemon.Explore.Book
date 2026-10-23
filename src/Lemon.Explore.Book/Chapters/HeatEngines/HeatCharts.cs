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

namespace Lemon.Explore.Book.Chapters.HeatEngines;

/// <summary>A small chart of the engine-and-fridge side panel: redrawn when a property changes or the language does.</summary>
public abstract class HeatChart : Control
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

    protected HeatChart()
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
        public static readonly IPen Grid = MakePen(Solid(28, 255, 255, 255), 1);
        public static readonly IPen Axis = MakePen(Solid(110, 255, 255, 255), 1);

        public static readonly Color[] Strokes =
        [
            Color.FromRgb(0x5A, 0xB0, 0xFF),
            Color.FromRgb(0xB3, 0x9D, 0xFF),
            Color.FromRgb(0xFF, 0x9F, 0x5A),
            Color.FromRgb(0xA8, 0xB0, 0xBC),
        ];
    }
}

/// <summary>
/// The engine's pressure against the cylinder's volume through the four strokes (the "indicator diagram"), each stroke
/// in its colour, with the gas now marked. The big loop's area is the work the gas does on the piston each cycle.
/// </summary>
public sealed class EnginePvChart : HeatChart
{
    static EnginePvChart()
    {
        AffectsRender<EnginePvChart>(AngleProperty, CompressionRatioProperty, AmbientProperty);
    }

    public static readonly StyledProperty<double> AngleProperty =
        AvaloniaProperty.Register<EnginePvChart, double>(nameof(Angle), 0.0);
    public static readonly StyledProperty<double> CompressionRatioProperty =
        AvaloniaProperty.Register<EnginePvChart, double>(nameof(CompressionRatio), EngineModel.DefaultCompressionRatio);
    public static readonly StyledProperty<double> AmbientProperty =
        AvaloniaProperty.Register<EnginePvChart, double>(nameof(Ambient), EngineModel.SeaLevelPressure);

    private (double Ratio, double Ambient) _key = (double.NaN, double.NaN);
    private EngineState[] _states = [];

    public double Angle
    {
        get => GetValue(AngleProperty);
        set => SetValue(AngleProperty, value);
    }

    public double CompressionRatio
    {
        get => GetValue(CompressionRatioProperty);
        set => SetValue(CompressionRatioProperty, value);
    }

    public double Ambient
    {
        get => GetValue(AmbientProperty);
        set => SetValue(AmbientProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        if (_key != (CompressionRatio, Ambient))
        {
            _key = (CompressionRatio, Ambient);
            _states = Enumerable.Range(0, 721).Select(i => EngineModel.At(i, CompressionRatio, Ambient)).ToArray();
        }

        var plot = new Rect(38, 14, width - 52, height - 40);
        var maxP = Math.Max(20, Math.Ceiling(_states.Max(s => s.Pressure) / 20) * 20);
        var maxV = 600.0;
        Point P(double volume, double pressure) => new(plot.Left + (volume * 1e6 / maxV * plot.Width), plot.Bottom - (pressure / maxP * plot.Height));

        for (var p = 0.0; p <= maxP + 1e-9; p += 20)
        {
            dc.DrawLine(ChartInk.Grid, P(0, p), P(maxV / 1e6, p));
            AxisLabel(dc, Number(p, "0"), new Point(plot.Left - 6, P(0, p).Y), alignRight: true);
        }

        for (var v = 0.0; v <= maxV; v += 100)
        {
            AxisLabel(dc, Number(v, "0"), new Point(P(v / 1e6, 0).X, plot.Bottom + 10), center: true);
        }

        AxisLabel(dc, "bar", new Point(plot.Left - 6, plot.Top - 6), alignRight: true);
        AxisLabel(dc, "cm³", new Point(plot.Right, plot.Bottom + 22), alignRight: true);

        // The work: the area inside the loop.
        dc.DrawGeometry(Solid(36, ChartInk.Orange.R, ChartInk.Orange.G, ChartInk.Orange.B), null,
            Curve(_states.Skip(180).Take(361).Select(s => P(s.Volume, s.Pressure)), closed: true));

        for (var stroke = 0; stroke < 4; stroke++)
        {
            var points = _states.Skip(stroke * 180).Take(181).Select(s => P(s.Volume, s.Pressure));
            dc.DrawGeometry(null, LinePen(ChartInk.Strokes[stroke], stroke == (int)EngineModel.StrokeAt(Angle) ? 2.4 : 1.5), Curve(points));
        }

        var now = EngineModel.At(Angle, CompressionRatio, Ambient);
        Marker(dc, P(now.Volume, now.Pressure), ChartInk.Text, 3.5);

        // Where the spark comes, and the area's meaning.
        var spark = EngineModel.At(EngineModel.SparkAngle, CompressionRatio, Ambient);
        Label(dc, Loc("Heat_Chart_Pv_Spark"), P(spark.Volume, spark.Pressure) + new Vector(8, 0), ChartInk.Gold, size: 10);
        Label(dc, Loc("Heat_Chart_Pv_Work"), P(220e-6, maxP * 0.32), ChartInk.Orange, size: 10.5);
    }
}

/// <summary>
/// How much of the fuel's heat becomes work, from the ideal cycle down to the wheels: the ideal Otto cycle at this
/// compression ratio, this page's engine (burning takes time, the exhaust opens early), and typical shares once the
/// walls' cooling and the friction are taken off.
/// </summary>
public sealed class EfficiencyChart : HeatChart
{
    static EfficiencyChart()
    {
        AffectsRender<EfficiencyChart>(CompressionRatioProperty);
    }

    public static readonly StyledProperty<double> CompressionRatioProperty =
        AvaloniaProperty.Register<EfficiencyChart, double>(nameof(CompressionRatio), EngineModel.DefaultCompressionRatio);

    /// <summary>Of the work on the pistons in this model, the share left after heat lost to the walls (typical).</summary>
    internal const double WallShare = 0.8;

    /// <summary>Of that, the share left after friction and the pumps (typical).</summary>
    internal const double FrictionShare = 0.85;

    public double CompressionRatio
    {
        get => GetValue(CompressionRatioProperty);
        set => SetValue(CompressionRatioProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var ratio = CompressionRatio;
        var model = EngineModel.Efficiency(ratio);
        (string Key, double Value, Color Color)[] rows =
        [
            ("Heat_Chart_Eff_Otto", EngineModel.OttoEfficiency(ratio), ChartInk.Gold),
            ("Heat_Chart_Eff_Model", model, ChartInk.Orange),
            ("Heat_Chart_Eff_Walls", model * WallShare, ChartInk.Red),
            ("Heat_Chart_Eff_Wheels", model * WallShare * FrictionShare, ChartInk.Green),
        ];

        var left = 12.0;
        var barLeft = Math.Min(width * 0.5, 170);
        var barWidth = width - barLeft - 46;
        var rowHeight = (height - 16) / rows.Length;
        for (var i = 0; i < rows.Length; i++)
        {
            var (key, value, color) = rows[i];
            var y = 10 + (i * rowHeight) + (rowHeight / 2);
            Label(dc, Loc(key), new Point(left, y), ChartInk.Text, size: 10.5);
            dc.DrawRoundedRectangle(Solid(28, 255, 255, 255), null, new Rect(barLeft, y - 7, barWidth, 14), 4, 4);
            dc.DrawRoundedRectangle(Solid(230, color.R, color.G, color.B), null, new Rect(barLeft, y - 7, Math.Max(2, barWidth * value), 14), 4, 4);
            Label(dc, $"{Number(value * 100, "0")}%", new Point(barLeft + (barWidth * value) + 6, y), color, size: 10.5, bold: true);
        }
    }
}

/// <summary>
/// The refrigerant's temperature all the way round the loop, against the room's and the freezer's: where it is
/// warmer than the room it gives heat out (red), where it is colder than the freezer it takes heat in (blue).
/// </summary>
public sealed class FridgeLoopChart : HeatChart
{
    static FridgeLoopChart()
    {
        AffectsRender<FridgeLoopChart>(RoomProperty);
    }

    public static readonly StyledProperty<double> RoomProperty =
        AvaloniaProperty.Register<FridgeLoopChart, double>(nameof(Room), FridgeModel.DefaultRoom);

    public double Room
    {
        get => GetValue(RoomProperty);
        set => SetValue(RoomProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(34, 30, width - 44, height - 58);
        const double minT = -40, maxT = 90;
        Point P(double s, double t) => new(plot.Left + (s * plot.Width), plot.Bottom - ((t - minT) / (maxT - minT) * plot.Height));

        // The parts as bands along the bottom, and the high- and low-pressure sides along the top.
        for (var part = 0; part < 6; part++)
        {
            var start = FridgeModel.PartStart((FridgePart)part);
            var end = FridgeModel.PartEnd((FridgePart)part);
            if (part % 2 == 0) dc.DrawRectangle(Solid(14, 255, 255, 255), null, new Rect(P(start, maxT), P(end, minT)));
            if ((FridgePart)part is FridgePart.Compressor or FridgePart.Discharge or FridgePart.Suction) continue;
            AxisLabel(dc, Loc($"Heat_Fridge_Part_{(FridgePart)part}"), new Point(P((start + end) / 2, 0).X, plot.Bottom + 10), center: true);
        }

        AxisLabel(dc, Loc("Heat_Fridge_Part_Compressor"), new Point(P(0.06, 0).X, plot.Bottom + 10), center: true);
        var high = FridgeModel.HighPressure(Room);
        var low = FridgeModel.LowPressure;
        Label(dc, LocFormat("Heat_Chart_Loop_High", Number(high, "0.0")), new Point(P(0.27, 0).X, 12), ChartInk.Orange, size: 10, center: true);
        Label(dc, LocFormat("Heat_Chart_Loop_Low", Number(low, "0.0")), new Point(P(0.76, 0).X, 12), ChartInk.Cyan, size: 10, center: true);

        for (var t = -40.0; t <= maxT; t += 20)
        {
            dc.DrawLine(ChartInk.Grid, P(0, t), P(1, t));
            AxisLabel(dc, Number(t, "0"), new Point(plot.Left - 5, P(0, t).Y), alignRight: true);
        }

        AxisLabel(dc, "°C", new Point(plot.Left - 5, plot.Top - 10), alignRight: true);

        var samples = Enumerable.Range(0, 301).Select(i => (S: i / 300.0, T: FridgeModel.At(Math.Min(0.99999, i / 300.0), Room).Temperature)).ToList();

        // Shade the gaps that drive heat: above the room, below the freezer.
        var room = Room;
        var freezer = (double)FridgeModel.FreezerTemperature;
        var hotSide = new List<Point>();
        var coldSide = new List<Point>();
        foreach (var (s, t) in samples)
        {
            hotSide.Add(P(s, Math.Max(t, room)));
            coldSide.Add(P(s, Math.Min(t, freezer)));
        }

        hotSide.Add(P(1, room));
        hotSide.Add(P(0, room));
        coldSide.Add(P(1, freezer));
        coldSide.Add(P(0, freezer));
        dc.DrawGeometry(Solid(70, ChartInk.Red.R, ChartInk.Red.G, ChartInk.Red.B), null, Curve(hotSide, closed: true));
        dc.DrawGeometry(Solid(70, ChartInk.Blue.R, ChartInk.Blue.G, ChartInk.Blue.B), null, Curve(coldSide, closed: true));

        dc.DrawLine(LinePen(ChartInk.Orange, 1.2, dashed: true), P(0, room), P(1, room));
        dc.DrawLine(LinePen(ChartInk.Cyan, 1.2, dashed: true), P(0, freezer), P(1, freezer));
        Label(dc, LocFormat("Heat_Chart_Loop_Room", Number(room, "0")), P(1, room) + new Vector(-2, -9), ChartInk.Orange, alignRight: true, size: 10);
        Label(dc, LocFormat("Heat_Chart_Loop_Freezer", Number(freezer, "0")), P(0.02, freezer) + new Vector(0, 9), ChartInk.Cyan, size: 10);

        dc.DrawGeometry(null, LinePen(ChartInk.Text, 2), Curve(samples.Select(x => P(x.S, x.T))));
    }
}

/// <summary>Electricity per day against the room's temperature: a warmer room leaks more heat in and makes the work harder.</summary>
public sealed class FridgeEnergyChart : HeatChart
{
    static FridgeEnergyChart()
    {
        AffectsRender<FridgeEnergyChart>(RoomProperty, DoorOpenProperty);
    }

    public static readonly StyledProperty<double> RoomProperty =
        AvaloniaProperty.Register<FridgeEnergyChart, double>(nameof(Room), FridgeModel.DefaultRoom);
    public static readonly StyledProperty<bool> DoorOpenProperty =
        AvaloniaProperty.Register<FridgeEnergyChart, bool>(nameof(DoorOpen), false);

    public double Room
    {
        get => GetValue(RoomProperty);
        set => SetValue(RoomProperty, value);
    }

    public bool DoorOpen
    {
        get => GetValue(DoorOpenProperty);
        set => SetValue(DoorOpenProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var plot = new Rect(34, 14, width - 46, height - 40);
        const double minRoom = 10, maxRoom = 40, maxEnergy = 2;
        Point P(double room, double kwh) => new(plot.Left + ((room - minRoom) / (maxRoom - minRoom) * plot.Width), plot.Bottom - (Math.Min(kwh, maxEnergy) / maxEnergy * plot.Height));

        for (var e = 0.0; e <= maxEnergy + 1e-9; e += 0.5)
        {
            dc.DrawLine(ChartInk.Grid, P(minRoom, e), P(maxRoom, e));
            AxisLabel(dc, Number(e, "0.0"), new Point(plot.Left - 5, P(minRoom, e).Y), alignRight: true);
        }

        for (var r = minRoom; r <= maxRoom; r += 10)
        {
            AxisLabel(dc, $"{Number(r, "0")} °C", new Point(P(r, 0).X, plot.Bottom + 11), center: true);
        }

        AxisLabel(dc, "kWh", new Point(plot.Left - 5, plot.Top - 6), alignRight: true);

        var closed = Enumerable.Range(0, 61).Select(i => minRoom + (i / 60.0 * (maxRoom - minRoom))).Select(r => P(r, FridgeModel.DailyEnergy(r)));
        dc.DrawGeometry(null, LinePen(ChartInk.Cyan, 2), Curve(closed));
        Label(dc, Loc("Heat_Chart_Energy_Closed"), P(13, FridgeModel.DailyEnergy(13)) + new Vector(0, -12), ChartInk.Cyan, size: 10);

        var now = DoorOpen ? FridgeModel.DutyCycle(Room, true) * FridgeModel.CompressorPower(Room) * 24 / 1000 : FridgeModel.DailyEnergy(Room);
        Marker(dc, P(Room, now), DoorOpen ? ChartInk.Orange : ChartInk.Text, 3.5);
        Label(dc, LocFormat(DoorOpen ? "Heat_Chart_Energy_Open" : "Heat_Chart_Energy_Now", Number(now, "0.00")), P(Room, now) + new Vector(Room > 30 ? -8 : 8, -12),
            DoorOpen ? ChartInk.Orange : ChartInk.Text, alignRight: Room > 30, size: 10);
    }
}

/// <summary>
/// The limits of any machine working between the two temperatures: the best engine efficiency, and the most heat a fridge
/// or heat pump can move per unit of work; real machines marked against them.
/// </summary>
public sealed class CarnotLimitChart : HeatChart
{
    static CarnotLimitChart()
    {
        AffectsRender<CarnotLimitChart>(HotProperty, ColdProperty, DirectionProperty);
    }

    public static readonly StyledProperty<double> HotProperty =
        AvaloniaProperty.Register<CarnotLimitChart, double>(nameof(Hot), 500.0);
    public static readonly StyledProperty<double> ColdProperty =
        AvaloniaProperty.Register<CarnotLimitChart, double>(nameof(Cold), 300.0);
    public static readonly StyledProperty<CycleDirection> DirectionProperty =
        AvaloniaProperty.Register<CarnotLimitChart, CycleDirection>(nameof(Direction), CycleDirection.Engine);

    public double Hot
    {
        get => GetValue(HotProperty);
        set => SetValue(HotProperty, value);
    }

    public double Cold
    {
        get => GetValue(ColdProperty);
        set => SetValue(ColdProperty, value);
    }

    public CycleDirection Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    protected override void Draw(PaintContext dc, double width, double height)
    {
        var hot = Math.Max(Hot, Cold + 1);
        var cold = Cold;
        (string Key, double Value, double Max, string Text, CycleDirection Of, Color Color)[] rows =
        [
            ("Heat_Chart_Limit_Engine", CarnotModel.Efficiency(hot, cold), 1, $"{Number(CarnotModel.Efficiency(hot, cold) * 100, "0")}%", CycleDirection.Engine, ChartInk.Gold),
            ("Heat_Chart_Limit_Fridge", CarnotModel.FridgeCop(hot, cold), 10, Number(CarnotModel.FridgeCop(hot, cold), "0.0"), CycleDirection.Fridge, ChartInk.Cyan),
            ("Heat_Chart_Limit_HeatPump", CarnotModel.HeatPumpCop(hot, cold), 10, Number(CarnotModel.HeatPumpCop(hot, cold), "0.0"), CycleDirection.HeatPump, ChartInk.Orange),
        ];

        var barLeft = Math.Min(width * 0.42, 150);
        var barWidth = width - barLeft - 52;
        var rowHeight = (height - 10) / rows.Length;
        for (var i = 0; i < rows.Length; i++)
        {
            var (key, value, max, text, of, color) = rows[i];
            var y = 6 + (i * rowHeight) + (rowHeight / 2);
            var current = of == Direction;
            Label(dc, Loc(key), new Point(8, y), current ? ChartInk.Text : ChartInk.Dim, size: 10.5, bold: current);
            dc.DrawRoundedRectangle(Solid(28, 255, 255, 255), null, new Rect(barLeft, y - 7, barWidth, 14), 4, 4);
            var fill = Math.Clamp(value / max, 0, 1);
            dc.DrawRoundedRectangle(Solid((byte)(current ? 235 : 130), color.R, color.G, color.B), null, new Rect(barLeft, y - 7, Math.Max(2, barWidth * fill), 14), 4, 4);
            Label(dc, value > max ? $"{text}+" : text, new Point(barLeft + (barWidth * fill) + 6, y), color, size: 10.5, bold: true);
        }
    }
}
