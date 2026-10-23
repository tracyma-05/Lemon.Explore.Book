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

namespace Lemon.Explore.Book.Chapters.Relativity;

/// <summary>
/// The side panel's chart, one per view. Light clock: γ against the speed, flat for a long way and then shooting up near
/// light's speed. The train: how long the 600 m train is seen from the platform, against its speed. Both can be clicked or
/// dragged to set the speed. The twins: each twin's age against Earth's time; the traveller's line is less steep, and
/// clicking or dragging moves through the trip.
/// </summary>
public sealed class RelativityChart : Control
{
    static RelativityChart()
    {
        AffectsRender<RelativityChart>(ModeProperty, BetaProperty, DistanceProperty, TwinTimeProperty);
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

    public static readonly StyledProperty<RelativityMode> ModeProperty =
        AvaloniaProperty.Register<RelativityChart, RelativityMode>(nameof(Mode), RelativityMode.LightClock);

    public static readonly StyledProperty<double> BetaProperty =
        AvaloniaProperty.Register<RelativityChart, double>(nameof(Beta), 0.6, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> DistanceProperty =
        AvaloniaProperty.Register<RelativityChart, double>(nameof(Distance), 4.0);

    public static readonly StyledProperty<double> TwinTimeProperty =
        AvaloniaProperty.Register<RelativityChart, double>(nameof(TwinTime), 0.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<RelativityChart, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer starts dragging or clicks (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<RelativityChart, ICommand?>(nameof(InteractionCommand));



    public RelativityMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double Beta
    {
        get => GetValue(BetaProperty);
        set => SetValue(BetaProperty, value);
    }

    public double Distance
    {
        get => GetValue(DistanceProperty);
        set => SetValue(DistanceProperty, value);
    }

    public double TwinTime
    {
        get => GetValue(TwinTimeProperty);
        set => SetValue(TwinTimeProperty, value);
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
    private static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
    private static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
    private static readonly Color Green = Color.FromRgb(0x7C, 0xFF, 0x9E);
    private static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
    private static readonly IPen Axis = MakePen(Solid(70, 255, 255, 255), 1);
    private static readonly IPen GoldPen = MakePen(Solid(255, Gold.R, Gold.G, Gold.B), 2);
    private static readonly IPen CyanPen = MakePen(Solid(230, Cyan.R, Cyan.G, Cyan.B), 2);
    private static readonly IPen GreenPen = MakePen(Solid(230, Green.R, Green.G, Green.B), 2);
    private static readonly IPen DimPen = MakePen(Solid(200, DimColor.R, DimColor.G, DimColor.B), 1.2, dashed: true);

    private readonly LabelCache _labels;
    private bool _dragging;

    public RelativityChart()
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

    private TwinTrip Trip() => RelativityModel.Trip(Distance, RelativityDiagram.TripSpeed(Beta));

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        if (ActualWidth < 1 || ActualHeight < 1) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        switch (Mode)
        {
            case RelativityMode.Simultaneity:
                DrawLength(dc);
                break;

            case RelativityMode.Twins:
                DrawAges(dc);
                break;

            default:
                DrawGamma(dc);
                break;
        }
    }

    #region the charts

    /// <summary>γ (1 to 8) against the speed (0 to 1 c).</summary>
    private void DrawGamma(PaintContext dc)
    {
        var plot = Plot;
        const double top = 8;
        Point P(double b, double g) => new(plot.Left + (b * plot.Width), plot.Bottom - ((Math.Min(g, top) - 1) / (top - 1) * plot.Height));
        Axes(dc, plot, [0, 0.25, 0.5, 0.75, 1], b => P(b, 1).X, "0.00", [1, 2, 4, 6, 8], g => P(0, g).Y, "0", "Rel_Chart_Speed", "Rel_Chart_Gamma");

        var curve = new List<Point>();
        for (var b = 0.0; b < 0.9922; b += 0.002) curve.Add(P(b, RelativityModel.Gamma(b)));
        dc.DrawGeometry(null, GoldPen, Polyline(curve));
        dc.DrawLine(DimPen, P(1, 1), P(1, top));
        Label(dc, Loc("Rel_Chart_LightLimit"), P(1, top * 0.6) + new Vector(-4, 0), DimColor, right: true);
        Label(dc, Loc("Rel_Chart_Everyday"), P(0.02, 1) + new Vector(0, -12), Green);

        var gamma = RelativityModel.Gamma(Beta);
        var dot = P(Beta, gamma);
        dc.DrawEllipse(Brushes.White, CyanPen, dot, 4.5, 4.5);
        Label(dc, LocalizationService.Instance.Format("Rel_Chart_GammaDot", Number(gamma, "0.00")), dot + new Vector(Beta > 0.7 ? -10 : 10, -12), Cyan, right: Beta > 0.7);
    }

    /// <summary>The 600 m train's length seen from the platform (m) against its speed.</summary>
    private void DrawLength(PaintContext dc)
    {
        var plot = Plot;
        var rest = 2 * RelativityDiagram.MetresPerUnit;
        Point P(double b, double m) => new(plot.Left + (b * plot.Width), plot.Bottom - (m / 600 * plot.Height));
        Axes(dc, plot, [0, 0.25, 0.5, 0.75, 1], b => P(b, 0).X, "0.00", [0, 150, 300, 450, 600], m => P(0, m).Y, "0", "Rel_Chart_Speed", "Rel_Chart_Metres");

        var curve = new List<Point>();
        for (var b = 0.0; b <= 1.0001; b += 0.002) curve.Add(P(b, rest * Math.Sqrt(Math.Max(0, 1 - (b * b)))));
        dc.DrawGeometry(null, CyanPen, Polyline(curve));
        var length = rest / RelativityModel.Gamma(Beta);
        var dot = P(Beta, length);
        dc.DrawEllipse(Brushes.White, GoldPen, dot, 4.5, 4.5);
        Label(dc, LocalizationService.Instance.Format("Rel_Chart_MetresDot", Number(length, "0")), dot + new Vector(Beta > 0.6 ? -10 : 10, -12), Gold, right: Beta > 0.6);
    }

    /// <summary>Each twin's age since the start against Earth's time.</summary>
    private void DrawAges(PaintContext dc)
    {
        var plot = Plot;
        var trip = Trip();
        var end = trip.EarthYears;
        Point P(double t, double age) => new(plot.Left + (t / end * plot.Width), plot.Bottom - (age / end * plot.Height));
        var step = end <= 12 ? 2 : end <= 30 ? 5 : end <= 60 ? 10 : 20;
        var ticks = new List<double>();
        for (var v = 0.0; v <= end + 1e-9; v += step) ticks.Add(v);
        Axes(dc, plot, [.. ticks], t => P(t, 0).X, "0", [.. ticks], a => P(0, a).Y, "0", "Rel_Chart_EarthYears", "Rel_Chart_Age");

        dc.DrawLine(GreenPen, P(0, 0), P(end, end));
        dc.DrawLine(CyanPen, P(0, 0), P(end, trip.ShipYears));
        dc.DrawLine(DimPen, P(trip.Turn, 0), P(trip.Turn, end));
        Label(dc, Loc("Rel_Chart_EarthTwin"), P(end * 0.62, end * 0.62) + new Vector(-6, -10), Green, right: true);
        Label(dc, Loc("Rel_Chart_Traveller"), P(end * 0.72, trip.ShipYears * 0.72) + new Vector(6, 8), Cyan);

        var t = Math.Clamp(TwinTime, 0, end);
        dc.DrawEllipse(Brushes.White, GreenPen, P(t, t), 4, 4);
        dc.DrawEllipse(Brushes.White, CyanPen, P(t, trip.ShipTime(t)), 4, 4);
    }

    #endregion

    #region mouse

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Pointer.Capture(this);
        _dragging = true;
        e.Handled = true;
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
        if (Mode == RelativityMode.Twins) SetCurrentValue(IsPlayingProperty, false);
        MoveTo(e.GetPosition(this));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Cursor = new Cursor(StandardCursorType.SizeWestEast);
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
        if (Mode == RelativityMode.Twins) SetCurrentValue(TwinTimeProperty, f * Trip().EarthYears);
        else SetCurrentValue(BetaProperty, Math.Round(Math.Min(f, 0.99), 2));
    }

    #endregion

    #region helpers

    private void Axes(PaintContext dc, Rect plot, double[] xs, Func<double, double> x, string xFormat, double[] ys, Func<double, double> y, string yFormat, string xKey, string yKey)
    {
        foreach (var v in ys)
        {
            var py = y(v);
            dc.DrawLine(Grid, new Point(plot.Left, py), new Point(plot.Right, py));
            var text = _labels.Text(Number(v, yFormat), 9.5, DimColor);
            dc.DrawText(text, new Point(plot.Left - text.Width - 5, py - (text.Height / 2)));
        }

        foreach (var v in xs)
        {
            var px = x(v);
            dc.DrawLine(Grid, new Point(px, plot.Top), new Point(px, plot.Bottom));
            var text = _labels.Text(Number(v, xFormat), 9.5, DimColor);
            dc.DrawText(text, new Point(px - (text.Width / 2), plot.Bottom + 3));
        }

        dc.DrawLine(Axis, plot.BottomLeft, plot.BottomRight);
        dc.DrawLine(Axis, plot.BottomLeft, plot.TopLeft);
        var xLabel = _labels.Text(Loc(xKey), 10, DimColor);
        dc.DrawText(xLabel, new Point(plot.Right - xLabel.Width, plot.Bottom + 16));
        var yLabel = _labels.Text(Loc(yKey), 10, DimColor);
        dc.DrawText(yLabel, new Point(plot.Left + 4, plot.Top - 4));
    }

    private void Label(PaintContext dc, string text, Point at, Color color, bool right = false)
    {
        var formatted = _labels.Text(text, 10, color);
        var x = right ? at.X - formatted.Width : at.X;
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
