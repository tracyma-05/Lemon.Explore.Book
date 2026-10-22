using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.BlackHole;

/// <summary>
/// The side panel's chart, one per view. Up close: how far light is turned against how close it passes, shooting up at
/// 5.2 M where it starts to be caught, beside Einstein's far-out rule 4M/b. Light and orbits: the probe's effective
/// potential with relativity and with Newton, its energy as a line and where it is as a dot; relativity's curve has a top
/// Newton's lacks, and over it the probe falls in. Lensing: how much brighter the source's middle looks as the hole drifts
/// along its track; clicking the chart puts the hole there.
/// </summary>
public sealed class BlackHoleChart : Control
{
    static BlackHoleChart()
    {
        AffectsRender<BlackHoleChart>(ModeProperty, StartRadiusProperty, StartRadialSpeedProperty, StartAngularMomentumProperty, ProbeRadiusProperty, LensXProperty, LensYProperty, EinsteinRadiusProperty);
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

    public static readonly StyledProperty<BlackHoleMode> ModeProperty =
        AvaloniaProperty.Register<BlackHoleChart, BlackHoleMode>(nameof(Mode), BlackHoleMode.Shadow);

    public static readonly StyledProperty<double> StartRadiusProperty =
        AvaloniaProperty.Register<BlackHoleChart, double>(nameof(StartRadius), 10.0);

    public static readonly StyledProperty<double> StartRadialSpeedProperty =
        AvaloniaProperty.Register<BlackHoleChart, double>(nameof(StartRadialSpeed), 0.0);

    public static readonly StyledProperty<double> StartAngularMomentumProperty =
        AvaloniaProperty.Register<BlackHoleChart, double>(nameof(StartAngularMomentum), 3.78);

    public static readonly StyledProperty<double> ProbeRadiusProperty =
        AvaloniaProperty.Register<BlackHoleChart, double>(nameof(ProbeRadius), 10.0);

    /// <summary>Lensing: the hole's place; clicking or dragging along the chart moves it along the track.</summary>
    public static readonly StyledProperty<double> LensXProperty =
        AvaloniaProperty.Register<BlackHoleChart, double>(nameof(LensX), -1.6, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> LensYProperty =
        AvaloniaProperty.Register<BlackHoleChart, double>(nameof(LensY), BlackHoleDiagram.TrackY, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> EinsteinRadiusProperty =
        AvaloniaProperty.Register<BlackHoleChart, double>(nameof(EinsteinRadius), 0.32);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<BlackHoleChart, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Run when the viewer starts dragging or clicks (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<BlackHoleChart, ICommand?>(nameof(InteractionCommand));



    public BlackHoleMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double StartRadius
    {
        get => GetValue(StartRadiusProperty);
        set => SetValue(StartRadiusProperty, value);
    }

    public double StartRadialSpeed
    {
        get => GetValue(StartRadialSpeedProperty);
        set => SetValue(StartRadialSpeedProperty, value);
    }

    public double StartAngularMomentum
    {
        get => GetValue(StartAngularMomentumProperty);
        set => SetValue(StartAngularMomentumProperty, value);
    }

    public double ProbeRadius
    {
        get => GetValue(ProbeRadiusProperty);
        set => SetValue(ProbeRadiusProperty, value);
    }

    public double LensX
    {
        get => GetValue(LensXProperty);
        set => SetValue(LensXProperty, value);
    }

    public double LensY
    {
        get => GetValue(LensYProperty);
        set => SetValue(LensYProperty, value);
    }

    public double EinsteinRadius
    {
        get => GetValue(EinsteinRadiusProperty);
        set => SetValue(EinsteinRadiusProperty, value);
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

    private static readonly Color TextColor = Color.FromRgb(0xEE, 0xF1, 0xF6);
    private static readonly Color DimColor = Color.FromRgb(0x9A, 0xA5, 0xB5);
    private static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
    private static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
    private static readonly Color Red = Color.FromRgb(0xFF, 0x5A, 0x5F);
    private static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
    private static readonly IPen Axis = MakePen(Solid(70, 255, 255, 255), 1);
    private static readonly IPen GoldPen = MakePen(Solid(255, Gold.R, Gold.G, Gold.B), 2);
    private static readonly IPen CyanPen = MakePen(Solid(220, Cyan.R, Cyan.G, Cyan.B), 2);
    private static readonly IPen DimPen = MakePen(Solid(200, DimColor.R, DimColor.G, DimColor.B), 1.4, dashed: true);
    private static readonly IPen RedPen = MakePen(Solid(170, Red.R, Red.G, Red.B), 1.2, dashed: true);
    private static readonly IBrush Caught = Solid(30, Red.R, Red.G, Red.B);

    private readonly LabelCache _labels;
    private bool _dragging;

    public BlackHoleChart()
    {
        _labels = new LabelCache(this);
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        _labels.Clear();
        InvalidateVisual();
    }

    /// <summary>The plotting area inside the axes' labels.</summary>
    private Rect Plot => new(40, 12, Math.Max(10, ActualWidth - 54), Math.Max(10, ActualHeight - 40));

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        if (ActualWidth < 1 || ActualHeight < 1) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        switch (Mode)
        {
            case BlackHoleMode.Orbits:
                DrawPotential(dc);
                break;

            case BlackHoleMode.Lensing:
                DrawLightCurve(dc);
                break;

            default:
                DrawDeflection(dc);
                break;
        }
    }

    #region the charts

    /// <summary>The real deflection, worked out once: (impact parameter, degrees) from just outside the critical ray to 16 M.</summary>
    private static readonly Lazy<IReadOnlyList<(double B, double Degrees)>> DeflectionCurve = new(() =>
    {
        var list = new List<(double, double)>();
        for (var b = BlackHoleModel.CriticalImpact + 0.005; b <= 16; b += b < 6 ? 0.02 : 0.1)
        {
            if (BlackHoleModel.Deflection(b, 0.004) is { } d) list.Add((b, d * 180 / Math.PI));
        }

        return list;
    });

    /// <summary>Deflection (degrees, 0–360) against impact parameter (M, 0–16).</summary>
    private void DrawDeflection(PaintContext dc)
    {
        var plot = Plot;
        const double bMax = 16;
        const double aMax = 360;
        Point P(double b, double a) => new(plot.Left + (b / bMax * plot.Width), plot.Bottom - (Math.Min(a, aMax) / aMax * plot.Height));
        var bc = BlackHoleModel.CriticalImpact;
        dc.DrawRectangle(Caught, null, new Rect(P(0, aMax), P(bc, 0)));
        Axes(dc, plot, [0, 4, 8, 12, 16], b => P(b, 0).X, [0, 90, 180, 270, 360], a => P(0, a).Y, "0", "Hole_Chart_Impact", "Hole_Chart_Degrees");

        var curve = DeflectionCurve.Value.Select(c => P(c.B, c.Degrees)).ToList();

        var weak = new List<Point>();
        for (var b = 4 / (aMax * Math.PI / 180); b <= bMax; b += 0.1) weak.Add(P(b, 4 / b * 180 / Math.PI));
        dc.DrawGeometry(null, DimPen, Polyline(weak));
        dc.DrawGeometry(null, GoldPen, Polyline(curve));
        dc.DrawLine(RedPen, P(bc, 0), P(bc, aMax));
        Label(dc, Loc("Hole_Chart_Caught"), P(bc / 2, aMax * 0.86), Red, center: true);
        Label(dc, Loc("Hole_Chart_Einstein"), P(9.2, 4 / 9.2 * 180 / Math.PI) + new Vector(0, -16), DimColor);
        Label(dc, Loc("Hole_Chart_Real"), P(6.6, 125), Gold);
    }

    /// <summary>The effective potential (E²) against r for the probe's angular momentum, both theories.</summary>
    private void DrawPotential(PaintContext dc)
    {
        var plot = Plot;
        const double rMin = 2;
        const double rMax = 26;
        var l = StartAngularMomentum;
        var r0 = Math.Max(StartRadius, 2.3);
        var energy2 = (StartRadialSpeed * StartRadialSpeed) + BlackHoleModel.EffectivePotential(r0, l);
        var newton2 = (StartRadialSpeed * StartRadialSpeed) + BlackHoleModel.NewtonPotential(r0, l);
        var top = Math.Max(1.08, Math.Max(energy2, newton2) + 0.04);
        var turns = BlackHoleModel.PotentialTurns(l);
        if (turns is { } t) top = Math.Max(top, Math.Min(1.6, BlackHoleModel.EffectivePotential(t.Barrier, l) + 0.04));
        var bottom = Math.Min(0.8, BlackHoleModel.EffectivePotential(Math.Clamp(turns?.Well ?? r0, rMin, rMax), l) - 0.04);
        Point P(double r, double v) => new(plot.Left + ((r - rMin) / (rMax - rMin) * plot.Width), plot.Bottom - ((Math.Clamp(v, bottom - 0.05, top + 0.05) - bottom) / (top - bottom) * plot.Height));

        var ticks = Enumerable.Range(0, 5).Select(i => bottom + ((top - bottom) * i / 4)).ToArray();
        Axes(dc, plot, [2, 6, 10, 14, 18, 22, 26], r => P(r, bottom).X, ticks, v => P(rMin, v).Y, "0.00", "Hole_Chart_Radius", "Hole_Chart_Potential");
        dc.PushClip(new RectangleGeometry(new Rect(plot.Left, plot.Top - 2, plot.Width + 2, plot.Height + 4)));

        var gr = new List<Point>();
        var nw = new List<Point>();
        for (var r = rMin; r <= rMax; r += 0.05)
        {
            gr.Add(P(r, BlackHoleModel.EffectivePotential(r, l)));
            if (r > 2.2) nw.Add(P(r, BlackHoleModel.NewtonPotential(r, l)));
        }

        dc.DrawGeometry(null, DimPen, Polyline(nw));
        dc.DrawGeometry(null, GoldPen, Polyline(gr));
        dc.DrawLine(CyanPen, P(rMin, energy2), P(rMax, energy2));
        dc.Pop();

        // Where the probe is, on its energy line.
        var dot = P(Math.Clamp(ProbeRadius, rMin, rMax), energy2);
        dc.DrawEllipse(Brushes.White, CyanPen, dot, 4.5, 4.5);
        Label(dc, Loc("Hole_Chart_Energy"), P(rMax, energy2) + new Vector(-4, -11), Cyan, right: true);
        Label(dc, Loc("Hole_Chart_Relativity"), P(3.2, BlackHoleModel.EffectivePotential(3.2, l)) + new Vector(6, 0), Gold);
        Label(dc, Loc("Hole_Chart_Newton"), P(rMax - 9, BlackHoleModel.NewtonPotential(rMax - 9, l)) + new Vector(0, -10), DimColor, center: true);
        if (turns is { } turn && turn.Barrier > rMin)
        {
            var peak = P(turn.Barrier, BlackHoleModel.EffectivePotential(turn.Barrier, l));
            Label(dc, Loc("Hole_Chart_Barrier"), peak + new Vector(0, -12), Gold, center: true);
        }
    }

    /// <summary>How much brighter the source's middle looks against where the hole is along the track.</summary>
    private void DrawLightCurve(PaintContext dc)
    {
        var plot = Plot;
        var end = BlackHoleDiagram.TrackEnd;
        var e = Math.Max(1e-3, EinsteinRadius);
        double Gain(double x, double y) => BlackHoleModel.Magnification(Math.Sqrt((x * x) + (y * y)) / e);
        var peak = Gain(0, BlackHoleDiagram.TrackY);
        var now = Gain(LensX, LensY);
        var top = Math.Ceiling(Math.Max(peak, Math.Min(now, 12)) + 0.5);
        Point P(double x, double g) => new(plot.Left + ((x + end) / (2 * end) * plot.Width), plot.Bottom - ((Math.Min(g, top) - 1) / (top - 1) * plot.Height));
        var step = top <= 4 ? 1 : top <= 8 ? 2 : 4;
        var ticks = new List<double>();
        for (var g = 1.0; g <= top + 1e-9; g += step) ticks.Add(g);
        Axes(dc, plot, [-2, -1, 0, 1, 2], x => P(x, 1).X, [.. ticks], g => P(0, g).Y, "0", "Hole_Chart_Track", "Hole_Chart_Brighter");

        var curve = new List<Point>();
        for (var x = -end; x <= end; x += 0.01) curve.Add(P(x, Gain(x, BlackHoleDiagram.TrackY)));
        dc.DrawGeometry(null, GoldPen, Polyline(curve));
        var dot = P(Math.Clamp(LensX, -end, end), now);
        dc.DrawEllipse(Brushes.White, CyanPen, dot, 4.5, 4.5);
        var text = now > 99 ? Loc("Hole_Chart_TimesHuge") : LocalizationService.Instance.Format("Hole_Chart_Times", Number(now, "0.0"));
        Label(dc, text, dot + new Vector(8, -10), Cyan, right: dot.X > plot.Right - 60);
    }

    #endregion

    #region mouse

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (Mode != BlackHoleMode.Lensing) return;
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
        Cursor = Mode == BlackHoleMode.Lensing ? new Cursor(StandardCursorType.SizeWestEast) : null;
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
        var end = BlackHoleDiagram.TrackEnd;
        var x = ((point.X - plot.Left) / plot.Width * 2 * end) - end;
        SetCurrentValue(LensXProperty, Math.Clamp(x, -end, end));
        SetCurrentValue(LensYProperty, BlackHoleDiagram.TrackY);
    }

    #endregion

    #region helpers

    private void Axes(PaintContext dc, Rect plot, double[] xs, Func<double, double> x, double[] ys, Func<double, double> y, string yFormat, string xKey, string yKey)
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
            var text = _labels.Text(Number(v, "0"), 9.5, DimColor);
            dc.DrawText(text, new Point(px - (text.Width / 2), plot.Bottom + 3));
        }

        dc.DrawLine(Axis, plot.BottomLeft, plot.BottomRight);
        dc.DrawLine(Axis, plot.BottomLeft, plot.TopLeft);
        var xLabel = _labels.Text(Loc(xKey), 10, DimColor);
        dc.DrawText(xLabel, new Point(plot.Right - xLabel.Width, plot.Bottom + 15));
        var yLabel = _labels.Text(Loc(yKey), 10, DimColor);
        dc.DrawText(yLabel, new Point(plot.Left + 4, plot.Top - 2));
    }

    private void Label(PaintContext dc, string text, Point at, Color color, bool center = false, bool right = false)
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
