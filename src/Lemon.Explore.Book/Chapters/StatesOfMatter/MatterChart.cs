using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Lemon.Book.Shell.Infrastructures.Localization;
using Lemon.Explore.Book.Controls.Space;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.StatesOfMatter;

/// <summary>
/// The side panel's chart, one per view. Particles: a small phase diagram with the cylinder's temperature and pressure
/// as a dot to drag. Heating: where the heat goes, a bar for each stretch of the heating curve, filled as far as the
/// heating has got. Diagram: the boiling point (for carbon dioxide, the point where dry ice turns to gas) against the
/// height above sea level; clicking a height puts the dot there.
/// </summary>
public sealed class MatterChart : Control
{
    static MatterChart()
    {
        AffectsRender<MatterChart>(ModeProperty, SubstanceProperty, CelsiusProperty, PressureLogProperty, PlaceProperty, HeatKjProperty);
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

    public static readonly StyledProperty<MatterMode> ModeProperty =
        AvaloniaProperty.Register<MatterChart, MatterMode>(nameof(Mode), MatterMode.Particles);

    public static readonly StyledProperty<MatterSubstance> SubstanceProperty =
        AvaloniaProperty.Register<MatterChart, MatterSubstance>(nameof(Substance), MatterSubstance.Water);

    public static readonly StyledProperty<double> CelsiusProperty =
        AvaloniaProperty.Register<MatterChart, double>(nameof(Celsius), 20.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> PressureLogProperty =
        AvaloniaProperty.Register<MatterChart, double>(nameof(PressureLog), Math.Log10(MatterModel.Atmosphere), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<HeatingPlace> PlaceProperty =
        AvaloniaProperty.Register<MatterChart, HeatingPlace>(nameof(Place), HeatingPlace.SeaLevel);

    public static readonly StyledProperty<double> HeatKjProperty =
        AvaloniaProperty.Register<MatterChart, double>(nameof(HeatKj), 0.0);

    /// <summary>Run when the viewer clicks or drags (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<MatterChart, ICommand?>(nameof(InteractionCommand));

    public MatterMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public MatterSubstance Substance
    {
        get => GetValue(SubstanceProperty);
        set => SetValue(SubstanceProperty, value);
    }

    public double Celsius
    {
        get => GetValue(CelsiusProperty);
        set => SetValue(CelsiusProperty, value);
    }

    public double PressureLog
    {
        get => GetValue(PressureLogProperty);
        set => SetValue(PressureLogProperty, value);
    }

    public HeatingPlace Place
    {
        get => GetValue(PlaceProperty);
        set => SetValue(PlaceProperty, value);
    }

    public double HeatKj
    {
        get => GetValue(HeatKjProperty);
        set => SetValue(HeatKjProperty, value);
    }

    public ICommand? InteractionCommand
    {
        get => GetValue(InteractionCommandProperty);
        set => SetValue(InteractionCommandProperty, value);
    }

    private static readonly Color DimColor = Color.FromRgb(0x9A, 0xA5, 0xB5);
    private static readonly Color TextColor = Color.FromRgb(0xEE, 0xF1, 0xF6);
    private static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
    private static readonly Color Orange = Color.FromRgb(0xFF, 0x9A, 0x4A);
    private static readonly Color Green = Color.FromRgb(0x7C, 0xFF, 0x9E);
    private static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
    private static readonly IPen Frame = MakePen(Solid(60, 255, 255, 255), 1);
    private static readonly IPen OrangePen = MakePen(Solid(255, Orange.R, Orange.G, Orange.B), 2.5);

    /// <summary>The heights on the boiling-point chart: sea level to the top of Everest, metres.</summary>
    private const double TopHeight = 9000;

    private readonly LabelCache _labels;
    private bool _dragging;

    public MatterChart()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
    }

    private MatterModel.Substance Model => MatterModel.Of(Substance);

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        _labels.Clear();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        if (ActualWidth < 1 || ActualHeight < 1) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        switch (Mode)
        {
            case MatterMode.Heating:
                DrawHeatShares(dc);
                break;

            case MatterMode.Diagram:
                DrawAltitude(dc);
                break;

            default:
                DrawSmallPhases(dc);
                break;
        }
    }

    #region the charts

    private Rect PhaseArea() => new(64, 8, Math.Max(10, ActualWidth - 76), Math.Max(10, ActualHeight - 30));

    /// <summary>The substance's phase diagram, small, with the dot.</summary>
    private void DrawSmallPhases(PaintContext dc)
    {
        var plot = new PhasePlot(Model, PhaseArea());
        plot.Draw(dc, 1.5);
        foreach (var p in PhasePlot.PressureTicks())
        {
            Label(dc, PhasePlot.PressureTickText(p), new Point(plot.Area.Left - 5, plot.Y(p)), DimColor, right: true);
        }

        foreach (var c in plot.CelsiusTicks())
        {
            Label(dc, LocalizationService.Instance.Format("Matter_Value_Celsius", c.ToString("0", System.Globalization.CultureInfo.InvariantCulture)),
                new Point(plot.X(c + MatterModel.ZeroCelsius), plot.Area.Bottom + 11), DimColor, center: true);
        }

        var kelvin = Celsius + MatterModel.ZeroCelsius;
        var pressure = Math.Pow(10, PressureLog);
        var state = MatterModel.StateAt(Model, kelvin, pressure);
        var dot = plot.At(kelvin, pressure);
        var brush = MatterViewModel.StateBrush(state);
        dc.DrawEllipse(Brushes.White, MakePen(brush, 3), dot, 6, 6);
    }

    /// <summary>Where the heat goes: one bar per stretch of the heating curve, on one scale, filled as far as it has got.</summary>
    private void DrawHeatShares(PaintContext dc)
    {
        var curve = MatterModel.HeatingCurve(Model, MatterModel.PressureAt(Place));
        var labelWidth = 104.0;
        var plot = new Rect(labelWidth, 6, Math.Max(10, ActualWidth - labelWidth - 64), Math.Max(10, ActualHeight - 12));
        var row = plot.Height / curve.Count;
        var largest = curve.Max(s => s.Kj);
        for (var i = 0; i < curve.Count; i++)
        {
            var stretch = curve[i];
            var y = plot.Top + (i * row);
            var width = plot.Width * stretch.Kj / largest;
            var bar = new Rect(plot.Left, y + (row * 0.2), Math.Max(1, width), row * 0.6);
            var color = stretch.IsChange ? Gold : Orange;
            var done = Math.Clamp((HeatKj - stretch.StartKj) / Math.Max(1e-9, stretch.Kj), 0, 1);
            dc.DrawRectangle(Solid(40, 255, 255, 255), null, bar);
            if (done > 0) dc.DrawRectangle(Solid(210, color.R, color.G, color.B), null, bar.WithWidth(bar.Width * done));
            Label(dc, Loc($"Matter_Short_{stretch.Kind}"), new Point(plot.Left - 8, bar.Center.Y), stretch.IsChange ? Gold : DimColor, right: true);
            Label(dc, LocalizationService.Instance.Format("Matter_Value_Kj", stretch.Kj.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)),
                new Point(bar.Right + 6, bar.Center.Y), TextColor);
        }
    }

    private Rect AltitudeArea() => new(46, 10, Math.Max(10, ActualWidth - 60), Math.Max(10, ActualHeight - 34));

    /// <summary>The temperature at which the liquid boils (or the solid sublimes) at each height, with towns and peaks marked.</summary>
    private void DrawAltitude(PaintContext dc)
    {
        var s = Model;
        var area = AltitudeArea();
        double ChangeC(double height)
        {
            var p = MatterModel.PressureAtHeight(height);
            var t = p < s.TripleP ? MatterModel.SublimationTemperature(s, p) : MatterModel.BoilingTemperature(s, p);
            return t - MatterModel.ZeroCelsius;
        }

        var high = Math.Ceiling(ChangeC(0) / 10) * 10 + 5;
        var low = Math.Floor(ChangeC(TopHeight) / 10) * 10 - 5;
        double X(double h) => area.Left + (h / TopHeight * area.Width);
        double Y(double c) => area.Bottom - ((c - low) / (high - low) * area.Height);

        dc.DrawRectangle(null, Frame, area);
        for (var c = Math.Ceiling(low / 10) * 10; c <= high; c += 10)
        {
            dc.DrawLine(Grid, new Point(area.Left, Y(c)), new Point(area.Right, Y(c)));
            Label(dc, LocalizationService.Instance.Format("Matter_Value_Celsius", c.ToString("0", System.Globalization.CultureInfo.InvariantCulture)), new Point(area.Left - 5, Y(c)), DimColor, right: true);
        }

        for (var h = 0; h <= TopHeight; h += 3000)
        {
            Label(dc, LocalizationService.Instance.Format("Matter_Value_Km", (h / 1000).ToString(System.Globalization.CultureInfo.InvariantCulture)), new Point(X(h), area.Bottom + 11), DimColor, center: true);
        }

        var points = Enumerable.Range(0, 46).Select(i => i * TopHeight / 45).Select(h => new Point(X(h), Y(ChangeC(h)))).ToList();
        var line = new StreamGeometry();
        using (var g = line.Open())
        {
            g.BeginFigure(points[0], false);
            foreach (var p in points.Skip(1)) g.LineTo(p);
            g.EndFigure(false);
        }

        dc.DrawGeometry(null, OrangePen, line);
        foreach (var (key, height) in new[] { ("Matter_Town_Sea", 0.0), ("Matter_Town_Lhasa", 3650), ("Matter_Town_Everest", 8849) })
        {
            var at = new Point(X(height), Y(ChangeC(height)));
            dc.DrawEllipse(Brushes.White, null, at, 3.5, 3.5);
            var label = $"{Loc(key)} {ChangeC(height).ToString("0", System.Globalization.CultureInfo.InvariantCulture)}°";
            Label(dc, label, at + new Vector(height > 6000 ? -6 : 6, -10), Green, right: height > 6000);
        }

        // The dot's pressure as a height, when it is in the chart's range.
        var pressure = Math.Pow(10, PressureLog);
        var dotHeight = MatterModel.HeightAtPressure(pressure);
        if (dotHeight >= -50 && dotHeight <= TopHeight)
        {
            var x = X(Math.Max(0, dotHeight));
            dc.DrawLine(MakePen(Solid(200, 255, 255, 255), 1.5, dashed: true), new Point(x, area.Top), new Point(x, area.Bottom));
        }
    }

    #endregion

    #region the mouse

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Mode == MatterMode.Heating) return;
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
        _dragging = true;
        e.Pointer.Capture(this);
        Follow(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging) Follow(e.GetPosition(this));
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

    /// <summary>The dot to the point (small phase diagram), or to boiling at that height (altitude chart).</summary>
    private void Follow(Point point)
    {
        if (Mode == MatterMode.Diagram)
        {
            var area = AltitudeArea();
            var height = Math.Clamp((point.X - area.Left) / area.Width, 0, 1) * TopHeight;
            var p = MatterModel.PressureAtHeight(height);
            var s = Model;
            var t = p < s.TripleP ? MatterModel.SublimationTemperature(s, p) : MatterModel.BoilingTemperature(s, p);
            SetCurrentValue(CelsiusProperty, Math.Round((t - MatterModel.ZeroCelsius) * 2) / 2);
            SetCurrentValue(PressureLogProperty, Math.Log10(p));
            return;
        }

        var plot = new PhasePlot(Model, PhaseArea());
        SetCurrentValue(CelsiusProperty, Math.Round(plot.Kelvin(point.X) - MatterModel.ZeroCelsius));
        SetCurrentValue(PressureLogProperty, Math.Round(plot.PressureLog(point.Y) * 100) / 100);
    }

    #endregion

    private void Label(PaintContext dc, string text, Point at, Color color, bool right = false, bool center = false)
    {
        var formatted = _labels.Text(text, 10.5, color);
        var x = right ? at.X - formatted.Width : center ? at.X - (formatted.Width / 2) : at.X;
        dc.DrawText(formatted, new Point(x, at.Y - (formatted.Height / 2)));
    }

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);
}
