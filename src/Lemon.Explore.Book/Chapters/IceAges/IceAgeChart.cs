using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.IceAges;

/// <summary>
/// The side panel's chart, one per view. Orbit: midsummer sunshine from the equator to the pole, then against today.
/// Sunshine: the sunshine at 65° N through the year, then against today (the summer peak moves with the perihelion).
/// Ice sheet: the last glacial cycle's ice and the ground's sinking under it, which lags thousands of years behind;
/// clicking or dragging there moves through that cycle.
/// </summary>
public sealed class IceAgeChart : Control
{
    static IceAgeChart()
    {
        AffectsRender<IceAgeChart>(ModeProperty, TimeKaProperty, CycleProperty);
    }

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

    /// <summary>The ice-sheet chart's window, thousand years from 1950.</summary>
    private const double CycleFrom = -135;

    private const double CycleTo = 10;

    public static readonly StyledProperty<IceAgeMode> ModeProperty =
        AvaloniaProperty.Register<IceAgeChart, IceAgeMode>(nameof(Mode), IceAgeMode.Orbit);

    public static readonly StyledProperty<double> TimeKaProperty =
        AvaloniaProperty.Register<IceAgeChart, double>(nameof(TimeKa), 0.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IceAgeCycle> CycleProperty =
        AvaloniaProperty.Register<IceAgeChart, IceAgeCycle>(nameof(Cycle), IceAgeCycle.All);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<IceAgeChart, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer starts dragging or clicks (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<IceAgeChart, ICommand?>(nameof(InteractionCommand));


    public IceAgeMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double TimeKa
    {
        get => GetValue(TimeKaProperty);
        set => SetValue(TimeKaProperty, value);
    }

    public IceAgeCycle Cycle
    {
        get => GetValue(CycleProperty);
        set => SetValue(CycleProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public ICommand? InteractionCommand
    {
        get => GetValue(InteractionCommandProperty);
        set => SetValue(InteractionCommandProperty, value);
    }

    private static readonly Color DimColor = Color.FromRgb(0x9A, 0xA5, 0xB5);
    private static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
    private static readonly Color Ice = Color.FromRgb(0xDD, 0xEE, 0xFA);
    private static readonly Color Orange = Color.FromRgb(0xFF, 0x9A, 0x4A);
    private static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
    private static readonly IPen Axis = MakePen(Solid(70, 255, 255, 255), 1);
    private static readonly IPen GoldPen = MakePen(Solid(255, Gold.R, Gold.G, Gold.B), 2);
    private static readonly IPen IcePen = MakePen(Solid(240, Ice.R, Ice.G, Ice.B), 2);
    private static readonly IPen OrangePen = MakePen(Solid(230, Orange.R, Orange.G, Orange.B), 2);
    private static readonly IPen DimPen = MakePen(Solid(200, DimColor.R, DimColor.G, DimColor.B), 1.4, dashed: true);
    private static readonly IPen CursorPen = MakePen(Solid(220, 0xEE, 0xF1, 0xF6), 1.2);

    private readonly LabelCache _labels;
    private bool _dragging;

    public IceAgeChart()
    {
        _labels = new LabelCache(this);
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        _labels.Clear();
        InvalidateVisual();
    }

    /// <summary>The plotting area inside the axes' labels.</summary>
    private Rect Plot => new(40, 14, Math.Max(10, ActualWidth - 54), Math.Max(10, ActualHeight - 42));

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        if (ActualWidth < 1 || ActualHeight < 1) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        switch (Mode)
        {
            case IceAgeMode.Sunshine:
                DrawYear(dc);
                break;

            case IceAgeMode.Sheet:
                DrawCycle(dc);
                break;

            default:
                DrawLatitudes(dc);
                break;
        }
    }

    #region the charts

    /// <summary>Midsummer sunshine (W/m²) from the equator to the North Pole, then (gold) and today (dashed).</summary>
    private void DrawLatitudes(PaintContext dc)
    {
        var plot = Plot;
        const double low = 360, high = 580;
        Point P(double lat, double w) => new(plot.Left + (lat / 90 * plot.Width), plot.Bottom - ((Math.Clamp(w, low, high) - low) / (high - low) * plot.Height));
        Axes(dc, plot, [0, 30, 60, 90], lat => P(lat, low).X, v => Number(v, "0") + "°", [400, 450, 500, 550], w => P(0, w).Y, "Ice_Chart_Latitude", "Ice_Chart_Wm2");

        var today = IceAgeModel.Elements(0);
        var then = IceAgeModel.Elements(TimeKa, Cycle);
        var now = new List<Point>();
        var past = new List<Point>();
        for (var lat = 0.0; lat <= 90.001; lat += 1)
        {
            now.Add(P(lat, IceAgeModel.DailyInsolation(today, lat)));
            past.Add(P(lat, IceAgeModel.DailyInsolation(then, lat)));
        }

        dc.DrawGeometry(null, DimPen, Polyline(now));
        dc.DrawGeometry(null, GoldPen, Polyline(past));
        var key = P(IceAgeModel.KeyLatitude, IceAgeModel.DailyInsolation(then, IceAgeModel.KeyLatitude));
        dc.DrawLine(Grid, new Point(key.X, plot.Top), new Point(key.X, plot.Bottom));
        dc.DrawEllipse(Brushes.White, GoldPen, key, 4, 4);
        Label(dc, Loc("Ice_Chart_Today"), P(12, IceAgeModel.DailyInsolation(today, 12)) + new Vector(0, 10), DimColor);
        Label(dc, Number(IceAgeModel.DailyInsolation(then, IceAgeModel.KeyLatitude), "0"), key + new Vector(-8, -12), Gold, right: true);
    }

    /// <summary>Daily sunshine at 65° N through the year (W/m²), then (gold) and today (dashed), months along the bottom.</summary>
    private void DrawYear(PaintContext dc)
    {
        var plot = Plot;
        const double high = 580;
        Point P(double day, double w) => new(plot.Left + (day / 365.25 * plot.Width), plot.Bottom - (Math.Clamp(w, 0, high) / high * plot.Height));
        var months = Enumerable.Range(0, 12).Select(m => (double)new DateTime(2001, m + 1, 1).DayOfYear - 1).ToArray();
        Axes(dc, plot, [.. months.Where((_, i) => i % 2 == 0)], d => P(d, 0).X, d => MonthText(d), [0, 200, 400], w => P(0, w).Y, "Ice_Chart_Month", "Ice_Chart_Wm2");

        Polyline(dc, DimPen, IceAgeModel.Elements(0), P);
        var then = IceAgeModel.Elements(TimeKa);
        Polyline(dc, GoldPen, then, P);
        Label(dc, Loc("Ice_Chart_Today"), P(330, 30) + new Vector(0, -8), DimColor, right: true);

        // The summer peak, at the northern summer solstice.
        var solstice = (IceAgeModel.EquinoxDay + IceAgeModel.DaysAfterEquinox(then, 90)) % 365.25;
        var peak = P(solstice, IceAgeModel.DailyInsolation(then, IceAgeModel.KeyLatitude));
        dc.DrawEllipse(Brushes.White, GoldPen, peak, 4, 4);
        Label(dc, Number(IceAgeModel.DailyInsolation(then, IceAgeModel.KeyLatitude), "0"), peak + new Vector(8, -10), Gold);
    }

    private static void Polyline(PaintContext dc, IPen pen, OrbitalElements orbit, Func<double, double, Point> p)
    {
        var points = new List<(double Day, double W)>();
        for (var l = 0.0; l < 360; l += 3)
        {
            var day = (IceAgeModel.EquinoxDay + IceAgeModel.DaysAfterEquinox(orbit, l)) % 365.25;
            points.Add((day, IceAgeModel.DailyInsolation(orbit, IceAgeModel.KeyLatitude, l)));
        }

        dc.DrawGeometry(null, pen, Polyline(points.OrderBy(q => q.Day).Select(q => p(q.Day, q.W)).ToList()));
    }

    /// <summary>The last glacial cycle: the ice (white) and the ground's sinking (orange, as ice it has caught up with).</summary>
    private void DrawCycle(PaintContext dc)
    {
        var plot = Plot;
        const double high = 1.2;
        Point P(double kyr, double share) => new(plot.Left + ((kyr - CycleFrom) / (CycleTo - CycleFrom) * plot.Width), plot.Bottom - (Math.Clamp(share, 0, high) / high * plot.Height));
        Axes(dc, plot, [-120, -80, -40, 0], k => P(k, 0).X, k => IceAgeDiagram.TickText(k), [0, 0.5, 1], s => P(CycleFrom, s).Y, "Ice_Chart_Time", "Ice_Chart_IceShare");

        var record = IceRecord.Instance;
        var ice = new List<Point>();
        var ground = new List<Point>();
        for (var k = CycleFrom; k <= CycleTo + 1e-9; k += 0.5)
        {
            var m = record.At(k);
            ice.Add(P(k, m.IceShare));
            ground.Add(P(k, m.GroundShare));
        }

        dc.DrawGeometry(null, OrangePen, Polyline(ground));
        dc.DrawGeometry(null, IcePen, Polyline(ice));
        Label(dc, Loc("Ice_Chart_IceLine"), P(-60, record.At(-60).IceShare) + new Vector(0, -12), Ice);
        Label(dc, Loc("Ice_Chart_GroundLine"), P(-8, record.At(-8).GroundShare) + new Vector(4, -8), Orange);

        if (TimeKa >= CycleFrom && TimeKa <= CycleTo)
        {
            var m = record.At(TimeKa);
            var x = P(TimeKa, 0).X;
            dc.DrawLine(CursorPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            dc.DrawEllipse(Brushes.White, IcePen, P(TimeKa, m.IceShare), 3.5, 3.5);
            dc.DrawEllipse(Brushes.White, OrangePen, P(TimeKa, m.GroundShare), 3.5, 3.5);
        }
        else
        {
            Label(dc, Loc("Ice_Chart_OutOfWindow"), new Point(plot.Left + (plot.Width / 2), plot.Top + 8), DimColor, center: true);
        }
    }

    private static string MonthText(double day) => new DateTime(2001, 1, 1).AddDays(day).ToString(Loc("Ice_Month_Format"), LocalizationService.Instance.CurrentCulture);

    #endregion

    #region mouse

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (Mode != IceAgeMode.Sheet) return;
        e.Pointer.Capture(this);
        _dragging = true;
        e.Handled = true;
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
        SetCurrentValue(IsPlayingProperty, false);
        MoveTo(e.GetPosition(this));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Cursor = Mode == IceAgeMode.Sheet ? new Cursor(StandardCursorType.SizeWestEast) : null;
        if (_dragging) MoveTo(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    private void MoveTo(Point point)
    {
        var plot = Plot;
        var f = Math.Clamp((point.X - plot.Left) / plot.Width, 0, 1);
        SetCurrentValue(TimeKaProperty, Math.Round(CycleFrom + (f * (CycleTo - CycleFrom)), 1));
    }

    #endregion

    #region helpers

    private void Axes(PaintContext dc, Rect plot, double[] xs, Func<double, double> x, Func<double, string> xText, double[] ys, Func<double, double> y, string xKey, string yKey)
    {
        foreach (var v in ys)
        {
            var py = y(v);
            dc.DrawLine(Grid, new Point(plot.Left, py), new Point(plot.Right, py));
            var text = _labels.Text(Number(v, v < 10 && v % 1 != 0 ? "0.0" : "0"), 9.5, DimColor);
            dc.DrawText(text, new Point(plot.Left - text.Width - 5, py - (text.Height / 2)));
        }

        foreach (var v in xs)
        {
            var px = x(v);
            dc.DrawLine(Grid, new Point(px, plot.Top), new Point(px, plot.Bottom));
            var text = _labels.Text(xText(v), 9.5, DimColor);
            dc.DrawText(text, new Point(Math.Clamp(px - (text.Width / 2), 0, Math.Max(0, ActualWidth - text.Width)), plot.Bottom + 3));
        }

        dc.DrawLine(Axis, plot.BottomLeft, plot.BottomRight);
        dc.DrawLine(Axis, plot.BottomLeft, plot.TopLeft);
        var xLabel = _labels.Text(Loc(xKey), 10, DimColor);
        dc.DrawText(xLabel, new Point(plot.Right - xLabel.Width, plot.Bottom + 16));
        var yLabel = _labels.Text(Loc(yKey), 10, DimColor);
        dc.DrawText(yLabel, new Point(plot.Left + 4, plot.Top - 4));
    }

    private void Label(PaintContext dc, string text, Point at, Color color, bool right = false, bool center = false)
    {
        var formatted = _labels.Text(text, 10, color);
        var x = center ? at.X - (formatted.Width / 2) : right ? at.X - formatted.Width : at.X;
        x = Math.Clamp(x, 0, Math.Max(0, ActualWidth - formatted.Width));
        dc.DrawText(formatted, new Point(x, at.Y - (formatted.Height / 2)));
    }

    private static StreamGeometry Polyline(IReadOnlyList<Point> points)
    {
        var geometry = new StreamGeometry();
        if (points.Count > 1)
        {
            using var context = geometry.Open();
            context.BeginFigure(points[0], false);
            for (var i = 1; i < points.Count; i++) context.LineTo(points[i]);
            context.EndFigure(false);
        }
        return geometry;
    }

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    #endregion
}
