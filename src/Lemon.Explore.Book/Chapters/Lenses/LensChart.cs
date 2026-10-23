using System.ComponentModel;
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

namespace Lemon.Explore.Book.Chapters.Lenses;

/// <summary>
/// The side panel's chart, one per view. Bench: where the image is for every place of the candle (both in focal lengths),
/// the candle as a dot that drags. Eye: how much the eye can focus closer at each age, and the near point that gives;
/// the age is a dot that drags. Camera: what looks sharp, nearest to farthest, at each f-number for this focus; the
/// f-number drags.
/// </summary>
public sealed class LensChart : Control
{
    static LensChart()
    {
        AffectsRender<LensChart>(ModeProperty, LensKindProperty, FocalLengthProperty, ObjectDistanceProperty, AgeProperty, FocalMmProperty, FNumberProperty, FocusDistanceProperty);
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

    public static readonly StyledProperty<LensMode> ModeProperty =
        AvaloniaProperty.Register<LensChart, LensMode>(nameof(Mode), LensMode.Bench);

    public static readonly StyledProperty<LensKind> LensKindProperty =
        AvaloniaProperty.Register<LensChart, LensKind>(nameof(LensKind), LensKind.Convex);

    public static readonly StyledProperty<double> FocalLengthProperty =
        AvaloniaProperty.Register<LensChart, double>(nameof(FocalLength), 15.0);

    public static readonly StyledProperty<double> ObjectDistanceProperty =
        AvaloniaProperty.Register<LensChart, double>(nameof(ObjectDistance), 45.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> AgeProperty =
        AvaloniaProperty.Register<LensChart, double>(nameof(Age), 20.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> FocalMmProperty =
        AvaloniaProperty.Register<LensChart, double>(nameof(FocalMm), 50.0);

    public static readonly StyledProperty<double> FNumberProperty =
        AvaloniaProperty.Register<LensChart, double>(nameof(FNumber), 2.8, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> FocusDistanceProperty =
        AvaloniaProperty.Register<LensChart, double>(nameof(FocusDistance), 5.0);

    /// <summary>Run when the viewer starts dragging the dot (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<LensChart, ICommand?>(nameof(InteractionCommand));



    public LensMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public LensKind LensKind
    {
        get => GetValue(LensKindProperty);
        set => SetValue(LensKindProperty, value);
    }

    public double FocalLength
    {
        get => GetValue(FocalLengthProperty);
        set => SetValue(FocalLengthProperty, value);
    }

    public double ObjectDistance
    {
        get => GetValue(ObjectDistanceProperty);
        set => SetValue(ObjectDistanceProperty, value);
    }

    public double Age
    {
        get => GetValue(AgeProperty);
        set => SetValue(AgeProperty, value);
    }

    public double FocalMm
    {
        get => GetValue(FocalMmProperty);
        set => SetValue(FocalMmProperty, value);
    }

    public double FNumber
    {
        get => GetValue(FNumberProperty);
        set => SetValue(FNumberProperty, value);
    }

    public double FocusDistance
    {
        get => GetValue(FocusDistanceProperty);
        set => SetValue(FocusDistanceProperty, value);
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
    private static readonly Color Green = Color.FromRgb(0x7C, 0xFF, 0x9E);
    private static readonly Color Orange = Color.FromRgb(0xFF, 0x9A, 0x4A);
    private static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
    private static readonly IPen Axis = MakePen(Solid(70, 255, 255, 255), 1);

    private readonly LabelCache _labels;
    private bool _dragging;

    public LensChart()
    {
        _labels = new LabelCache(this);
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        _labels.Clear();
        InvalidateVisual();
    }

    /// <summary>The plotting area inside the axes' labels.</summary>
    private Rect Plot => new(38, 12, Math.Max(10, ActualWidth - 50), Math.Max(10, ActualHeight - 40));

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        if (ActualWidth < 1 || ActualHeight < 1) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        switch (Mode)
        {
            case LensMode.Eye:
                DrawAges(dc);
                break;

            case LensMode.Camera:
                DrawDepth(dc);
                break;

            default:
                DrawImages(dc);
                break;
        }
    }

    #region bench: where the image is

    private const double MaxU = 5;
    private const double MinV = -4;
    private const double MaxV = 5;

    private Point ImagePoint(double u, double v)
    {
        var plot = Plot;
        return new Point(plot.X + (u / MaxU * plot.Width), plot.Bottom - ((Math.Clamp(v, MinV - 0.5, MaxV + 0.5) - MinV) / (MaxV - MinV) * plot.Height));
    }

    private void DrawImages(PaintContext dc)
    {
        var plot = Plot;
        dc.PushClip(new RectangleGeometry(new Rect(plot.X - 2, plot.Y - 4, plot.Width + 6, plot.Height + 6)));
        for (var u = 1; u <= MaxU; u++) dc.DrawLine(Grid, ImagePoint(u, MinV), ImagePoint(u, MaxV));
        for (var v = (int)MinV; v <= MaxV; v++) dc.DrawLine(v == 0 ? Axis : Grid, ImagePoint(0, v), ImagePoint(MaxU, v));

        var convex = LensKind == LensKind.Convex;
        if (convex)
        {
            // The asymptotes: a candle at F sends its image to infinity; a far candle's image is at F.
            var dashed = MakePen(Solid(90, Orange.R, Orange.G, Orange.B), 1, dashed: true);
            dc.DrawLine(dashed, ImagePoint(1, MinV), ImagePoint(1, MaxV));
            dc.DrawLine(dashed, ImagePoint(0, 1), ImagePoint(MaxU, 1));
            dc.DrawLine(MakePen(Solid(40, 255, 255, 255), 1, dashed: true), ImagePoint(0, 0), ImagePoint(MaxU, MaxU));
        }

        var real = new List<Point>();
        var virtualPart = new List<Point>();
        for (var i = 1; i <= 300; i++)
        {
            var u = MaxU * i / 300.0;
            var v = convex ? u / (u - 1) : -u / (u + 1);
            if (convex && Math.Abs(u - 1) < 0.004) continue;
            (v > 0 ? real : virtualPart).Add(ImagePoint(u, v));
        }

        DrawCurve(dc, real, Gold);
        DrawCurve(dc, virtualPart, Cyan);
        if (convex) dc.DrawEllipse(null, MakePen(Solid(200, 255, 255, 255), 1), ImagePoint(2, 2), 3, 3);

        var uNow = ObjectDistance / FocalLength;
        var vNow = convex ? uNow / (uNow - 1) : -uNow / (uNow + 1);
        var dot = ImagePoint(Math.Min(uNow, MaxU), double.IsFinite(vNow) ? vNow : MaxV);
        dc.Pop();
        dc.DrawEllipse(Solid(255, 255, 255, 255), MakePen(Solid(255, vNow > 0 ? Gold.R : Cyan.R, vNow > 0 ? Gold.G : Cyan.G, vNow > 0 ? Gold.B : Cyan.B), 2.5), dot, 5, 5);

        foreach (var u in new[] { 1, 2, 3, 4, 5 })
        {
            DrawLabel(dc, u == 1 ? Loc("Lens_Chart_F") : LocalizationService.Instance.Format("Lens_Chart_NF", u), ImagePoint(u, MinV) + new Vector(0, 9), DimColor, center: true);
        }

        foreach (var v in new[] { -4, -2, 0, 2, 4 })
        {
            DrawLabel(dc, v == 0 ? "0" : LocalizationService.Instance.Format("Lens_Chart_NF", v), ImagePoint(0, v) + new Vector(-6, 0), DimColor, right: true);
        }

        DrawLabel(dc, Loc("Lens_Chart_ImageAxis"), new Point(plot.X + 6, plot.Y + 6), TextColor);
        DrawLabel(dc, Loc("Lens_Chart_CandleAxis"), new Point(plot.Right, plot.Bottom - 10), TextColor, right: true);
        DrawLabel(dc, Loc(convex ? "Lens_Chart_RealBelow" : "Lens_Chart_AlwaysVirtual"), ImagePoint(convex ? 2.6 : 2.2, convex ? -2.6 : -2.2), Cyan);
    }

    private static void DrawCurve(PaintContext dc, List<Point> points, Color colour)
    {
        if (points.Count < 2) return;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], false);
            for (var i = 1; i < points.Count; i++)
            {
                // A jump across the asymptote is not part of the curve.
                context.LineTo(points[i], Math.Abs(points[i].Y - points[i - 1].Y) < 60);
            }

            context.EndFigure(false);
        }
        dc.DrawGeometry(null, MakePen(Solid(255, colour.R, colour.G, colour.B), 2), geometry);
    }

    #endregion

    #region eye: focusing range by age

    private const double MaxAge = 80;
    private const double MaxAmplitude = 20;

    private Point AgePoint(double age, double amplitude)
    {
        var plot = Plot;
        return new Point(plot.X + (age / MaxAge * plot.Width), plot.Bottom - (Math.Clamp(amplitude, 0, MaxAmplitude) / MaxAmplitude * plot.Height));
    }

    private void DrawAges(PaintContext dc)
    {
        var plot = Plot;
        for (var age = 10; age <= MaxAge; age += 10) dc.DrawLine(Grid, AgePoint(age, 0), AgePoint(age, MaxAmplitude));
        for (var d = 0; d <= MaxAmplitude; d += 5) dc.DrawLine(d == 0 ? Axis : Grid, AgePoint(0, d), AgePoint(MaxAge, d));

        // People differ: Hofstetter's lowest and highest lines round the average.
        var band = new List<Point>();
        for (var age = 0.0; age <= MaxAge; age += 1) band.Add(AgePoint(age, Math.Max(0, 25 - (0.4 * age))));
        for (var age = MaxAge; age >= 0; age -= 1) band.Add(AgePoint(age, Math.Max(0, 15 - (0.25 * age))));
        var bandGeometry = new StreamGeometry();
        using (var context = bandGeometry.Open())
        {
            context.BeginFigure(band[0], true);
            foreach (var p in band.Skip(1)) context.LineTo(p);

            context.EndFigure(true);
        }
        dc.DrawGeometry(Solid(30, Gold.R, Gold.G, Gold.B), null, bandGeometry);

        var line = new List<Point>();
        for (var age = 0.0; age <= MaxAge; age += 1) line.Add(AgePoint(age, LensModel.Amplitude(age)));
        DrawCurve(dc, line, Gold);

        // Reading at 40 cm needs 2.5 D: below this line, reading glasses.
        var reading = MakePen(Solid(150, Orange.R, Orange.G, Orange.B), 1.2, dashed: true);
        dc.DrawLine(reading, AgePoint(0, 2.5), AgePoint(MaxAge, 2.5));
        DrawLabel(dc, Loc("Lens_Chart_Reading"), AgePoint(1, 2.5) + new Vector(0, -9), Orange);

        var dot = AgePoint(Age, LensModel.Amplitude(Age));
        dc.DrawEllipse(Solid(255, 255, 255, 255), MakePen(Solid(255, Gold.R, Gold.G, Gold.B), 2.5), dot, 5, 5);
        var near = 100 / LensModel.Amplitude(Age);
        DrawLabel(dc, LensDiagram.Centimetres(near), dot + new Vector(8, -10), TextColor);

        foreach (var age in new[] { 10, 30, 50, 70 }) DrawLabel(dc, age.ToString(System.Globalization.CultureInfo.InvariantCulture), AgePoint(age, 0) + new Vector(0, 9), DimColor, center: true);
        foreach (var d in new[] { 0, 5, 10, 15, 20 }) DrawLabel(dc, d.ToString(System.Globalization.CultureInfo.InvariantCulture), AgePoint(0, d) + new Vector(-6, 0), DimColor, right: true);
        DrawLabel(dc, Loc("Lens_Chart_AmplitudeAxis"), new Point(plot.X + 6, plot.Y + 6), TextColor);
        DrawLabel(dc, Loc("Lens_Chart_AgeAxis"), new Point(plot.Right, plot.Bottom - 10), TextColor, right: true);
    }

    #endregion

    #region camera: depth of field by f-number

    private static double DistanceLog(double metres) => Math.Log10(Math.Clamp(metres, LensDiagram.CameraNear, 300));

    private Point DepthPoint(double stop, double metres)
    {
        var plot = Plot;
        var t = (DistanceLog(metres) - Math.Log10(LensDiagram.CameraNear)) / (Math.Log10(300) - Math.Log10(LensDiagram.CameraNear));
        return new Point(plot.X + (stop / (LensModel.FStops.Length - 1) * plot.Width), plot.Bottom - (t * plot.Height));
    }

    private void DrawDepth(PaintContext dc)
    {
        var plot = Plot;
        foreach (var metres in new[] { 0.5, 1, 3, 10, 30, 100 }) dc.DrawLine(Grid, DepthPoint(0, metres), DepthPoint(LensModel.FStops.Length - 1, metres));

        var near = new List<Point>();
        var far = new List<Point>();
        for (var i = 0.0; i <= LensModel.FStops.Length - 1 + 1e-9; i += 0.1)
        {
            var n = 1.4 * Math.Pow(Math.Sqrt(2), i);
            var (a, b) = LensModel.DepthOfField(FocalMm, n, FocusDistance);
            near.Add(DepthPoint(i, a));
            far.Add(DepthPoint(i, double.IsPositiveInfinity(b) ? 1e6 : b));
        }

        var band = new StreamGeometry();
        using (var context = band.Open())
        {
            context.BeginFigure(near[0], true);
            foreach (var p in near.Skip(1)) context.LineTo(p);
            for (var i = far.Count - 1; i >= 0; i--) context.LineTo(far[i]);

            context.EndFigure(true);
        }
        dc.DrawGeometry(Solid(50, Green.R, Green.G, Green.B), null, band);
        DrawCurve(dc, near, Green);
        DrawCurve(dc, far, Green);

        var focus = DepthPoint(0, double.IsPositiveInfinity(FocusDistance) ? 1e6 : FocusDistance).Y;
        dc.DrawLine(MakePen(Solid(180, Gold.R, Gold.G, Gold.B), 1.2, dashed: true), new Point(plot.X, focus), new Point(plot.Right, focus));
        DrawLabel(dc, Loc("Lens_Chart_Focus"), new Point(plot.Right, focus - 9), Gold, right: true);

        var stop = Math.Log(FNumber / 1.4) / Math.Log(Math.Sqrt(2));
        var x = DepthPoint(stop, 1).X;
        dc.DrawLine(MakePen(Solid(200, 255, 255, 255), 1.2), new Point(x, plot.Y), new Point(x, plot.Bottom));
        dc.DrawEllipse(Solid(255, 255, 255, 255), MakePen(Solid(255, Green.R, Green.G, Green.B), 2.5), new Point(x, plot.Bottom - 6), 5, 5);

        for (var i = 0; i < LensModel.FStops.Length; i += 2)
        {
            DrawLabel(dc, LensDiagram.FText(LensModel.FStops[i]), DepthPoint(i, LensDiagram.CameraNear) + new Vector(0, 9), DimColor, center: true);
        }

        foreach (var (metres, key) in new[] { (0.5, "Lens_Tick_50cm"), (3.0, "Lens_Tick_3m"), (30.0, "Lens_Tick_30m") })
        {
            DrawLabel(dc, Loc(key), DepthPoint(0, metres) + new Vector(-6, 0), DimColor, right: true);
        }

        DrawLabel(dc, Loc("Lens_Value_Infinity"), DepthPoint(0, 300) + new Vector(-6, 4), DimColor, right: true);
        DrawLabel(dc, Loc("Lens_Chart_SharpAxis"), new Point(plot.X + 6, plot.Y + 6), TextColor);
    }

    #endregion

    #region dragging

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Pointer.Capture(this);
        _dragging = true;
        if (InteractionCommand is { } command && command.CanExecute(null)) command.Execute(null);
        DragTo(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Cursor = new Cursor(StandardCursorType.SizeWestEast);
        if (_dragging) DragTo(e.GetPosition(this));
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

    private void DragTo(Point point)
    {
        var plot = Plot;
        var t = Math.Clamp((point.X - plot.X) / plot.Width, 0, 1);
        switch (Mode)
        {
            case LensMode.Eye:
                SetCurrentValue(AgeProperty, Math.Round(Math.Clamp(t * MaxAge, 5, 75)));
                break;

            case LensMode.Camera:
                SetCurrentValue(FNumberProperty, LensModel.FStops[(int)Math.Round(t * (LensModel.FStops.Length - 1))]);
                break;

            default:
                SetCurrentValue(ObjectDistanceProperty, Math.Round(Math.Clamp(t * MaxU * FocalLength, 3, 68) * 2) / 2);
                break;
        }
    }

    #endregion

    private void DrawLabel(PaintContext dc, string text, Point at, Color color, bool center = false, bool right = false)
    {
        var formatted = _labels.Text(text, 10.5, color);
        var x = center ? at.X - (formatted.Width / 2) : right ? at.X - formatted.Width : at.X;
        dc.DrawText(formatted, new Point(x, at.Y - (formatted.Height / 2)));
    }

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);
}
