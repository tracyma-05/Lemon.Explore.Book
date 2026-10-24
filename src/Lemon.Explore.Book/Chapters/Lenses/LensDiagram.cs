using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Infrastructures.Timing;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Lenses;

/// <summary>How the bench shows the light: the three rays every textbook draws, many real rays from the flame, or sunlight.</summary>
public enum RayStyle
{
    Principal,
    Fan,
    Sunlight,
}

/// <summary>
/// The animated diagram of the lenses page. Bench: a candle, a convex or concave lens, the rays through it, the image and
/// a screen with what it shows (<c>LensDiagram.Bench.cs</c>). Eye: the eye in cross-section focusing light from a chart
/// near or far, with its lens changing shape, short or long sight, age and glasses, and what the eye sees
/// (<c>LensDiagram.Eye.cs</c>). Camera: the light from four distances through the lens and aperture to the sensor, the
/// depth of field, and the photo (<c>LensDiagram.Camera.cs</c>).
/// </summary>
/// <remarks>
/// The bench is in centimetres with heights drawn 2.5 times larger (straight lines stay straight, so the rays still meet
/// where they really do). The eye is drawn to scale; the distances outside it are squeezed. The camera's distances
/// behind the lens are stretched so the focus can be seen. The mouse and the clickable things are in
/// <c>LensDiagram.Objects.cs</c>.
/// </remarks>
public sealed partial class LensDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    #region dependency properties

    public static readonly StyledProperty<LensMode> ModeProperty =
        AvaloniaProperty.Register<LensDiagram, LensMode>(nameof(Mode), LensMode.Bench);

    public static readonly StyledProperty<LensKind> LensKindProperty =
        AvaloniaProperty.Register<LensDiagram, LensKind>(nameof(LensKind), LensKind.Convex);

    /// <summary>Bench: the lens's focal length, cm (its size; the kind gives the sign). Dragging an F sets it.</summary>
    public static readonly StyledProperty<double> FocalLengthProperty =
        AvaloniaProperty.Register<LensDiagram, double>(nameof(FocalLength), 15.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Bench: how far the candle is from the lens, cm. Dragging the candle sets it.</summary>
    public static readonly StyledProperty<double> ObjectDistanceProperty =
        AvaloniaProperty.Register<LensDiagram, double>(nameof(ObjectDistance), 45.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Bench: the candle's height to the flame's tip, cm.</summary>
    public static readonly StyledProperty<double> ObjectHeightProperty =
        AvaloniaProperty.Register<LensDiagram, double>(nameof(ObjectHeight), 3.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Bench: how far behind the lens the screen stands, cm. Dragging the screen sets it.</summary>
    public static readonly StyledProperty<double> ScreenDistanceProperty =
        AvaloniaProperty.Register<LensDiagram, double>(nameof(ScreenDistance), 22.5, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> ShowScreenProperty =
        AvaloniaProperty.Register<LensDiagram, bool>(nameof(ShowScreen), true);

    public static readonly StyledProperty<RayStyle> RayStyleProperty =
        AvaloniaProperty.Register<LensDiagram, RayStyle>(nameof(RayStyle), RayStyle.Principal);

    /// <summary>Bench: the rays in three colours, the glass's spread of colours exaggerated.</summary>
    public static readonly StyledProperty<bool> ShowColoursProperty =
        AvaloniaProperty.Register<LensDiagram, bool>(nameof(ShowColours), false);

    /// <summary>Eye: how far the chart is from the eye, m (+∞ for far away). Dragging the chart sets it.</summary>
    public static readonly StyledProperty<double> DistanceProperty =
        AvaloniaProperty.Register<LensDiagram, double>(nameof(Distance), double.PositiveInfinity, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Eye: the person's age, years (how much the lens can still change).</summary>
    public static readonly StyledProperty<double> AgeProperty =
        AvaloniaProperty.Register<LensDiagram, double>(nameof(Age), 20.0);

    /// <summary>Eye: short (negative) or long (positive) sight, dioptres.</summary>
    public static readonly StyledProperty<double> ErrorProperty =
        AvaloniaProperty.Register<LensDiagram, double>(nameof(Error), 0.0);

    public static readonly StyledProperty<Spectacles> GlassesProperty =
        AvaloniaProperty.Register<LensDiagram, Spectacles>(nameof(Glasses), Spectacles.None);

    public static readonly StyledProperty<LightLevel> LightProperty =
        AvaloniaProperty.Register<LensDiagram, LightLevel>(nameof(Light), LightLevel.Indoor);

    /// <summary>Camera: the lens's focal length, mm.</summary>
    public static readonly StyledProperty<double> FocalMmProperty =
        AvaloniaProperty.Register<LensDiagram, double>(nameof(FocalMm), 50.0);

    public static readonly StyledProperty<double> FNumberProperty =
        AvaloniaProperty.Register<LensDiagram, double>(nameof(FNumber), 5.6);

    /// <summary>Camera: the distance the lens is focused at, m (+∞ for infinity). Dragging the focus mark or tapping the photo sets it.</summary>
    public static readonly StyledProperty<double> FocusDistanceProperty =
        AvaloniaProperty.Register<LensDiagram, double>(nameof(FocusDistance), 5.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Camera: the shutter time, s.</summary>
    public static readonly StyledProperty<double> ShutterProperty =
        AvaloniaProperty.Register<LensDiagram, double>(nameof(Shutter), 1.0 / 1000);

    public static readonly StyledProperty<int> IsoProperty =
        AvaloniaProperty.Register<LensDiagram, int>(nameof(Iso), 100);

    public static readonly StyledProperty<SceneLight> SceneProperty =
        AvaloniaProperty.Register<LensDiagram, SceneLight>(nameof(Scene), SceneLight.Sun);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<LensDiagram, bool>(nameof(ShowLabels), true);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<LensDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<LensDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<LensDiagram>(ModeProperty, LensKindProperty, FocalLengthProperty, ObjectDistanceProperty, ObjectHeightProperty, ScreenDistanceProperty, ShowScreenProperty, RayStyleProperty, ShowColoursProperty, DistanceProperty, AgeProperty, ErrorProperty, GlassesProperty, LightProperty, FocalMmProperty, FNumberProperty, FocusDistanceProperty, ShutterProperty, IsoProperty, SceneProperty, ShowLabelsProperty, ToolbarHeightProperty);
        return true;
    }



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

    public double ObjectHeight
    {
        get => GetValue(ObjectHeightProperty);
        set => SetValue(ObjectHeightProperty, value);
    }

    public double ScreenDistance
    {
        get => GetValue(ScreenDistanceProperty);
        set => SetValue(ScreenDistanceProperty, value);
    }

    public bool ShowScreen
    {
        get => GetValue(ShowScreenProperty);
        set => SetValue(ShowScreenProperty, value);
    }

    public RayStyle RayStyle
    {
        get => GetValue(RayStyleProperty);
        set => SetValue(RayStyleProperty, value);
    }

    public bool ShowColours
    {
        get => GetValue(ShowColoursProperty);
        set => SetValue(ShowColoursProperty, value);
    }

    public double Distance
    {
        get => GetValue(DistanceProperty);
        set => SetValue(DistanceProperty, value);
    }

    public double Age
    {
        get => GetValue(AgeProperty);
        set => SetValue(AgeProperty, value);
    }

    public double Error
    {
        get => GetValue(ErrorProperty);
        set => SetValue(ErrorProperty, value);
    }

    public Spectacles Glasses
    {
        get => GetValue(GlassesProperty);
        set => SetValue(GlassesProperty, value);
    }

    public LightLevel Light
    {
        get => GetValue(LightProperty);
        set => SetValue(LightProperty, value);
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

    public double Shutter
    {
        get => GetValue(ShutterProperty);
        set => SetValue(ShutterProperty, value);
    }

    public int Iso
    {
        get => GetValue(IsoProperty);
        set => SetValue(IsoProperty, value);
    }

    public SceneLight Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public double ToolbarHeight
    {
        get => GetValue(ToolbarHeightProperty);
        set => SetValue(ToolbarHeightProperty, value);
    }

    #endregion

    private readonly BookStopwatch _clock = new();
    private readonly LabelCache _labels;
    private double _lastSeconds;

    /// <summary>The playing time, s: the flame's flicker and the ball's flight.</summary>
    private double _time;

    public LensDiagram()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
    }

    #region lifecycle

    private bool _attached;

    private void RequestFrame()
    {
        if (_attached)
        {
            TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
        }
    }

    private void OnFrame(TimeSpan time)
    {
        if (!_attached)
        {
            return;
        }

        OnRendering();
        RequestFrame();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _clock.Start();
        _lastSeconds = _clock.Elapsed.TotalSeconds;
        RequestFrame();
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        _clock.Stop();
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => _labels.Clear();

    private void OnModeChanged() => _drag = DragKind.None;

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);
        if (IsPlaying) _time += dt;
        InvalidateVisual();
    }

    #endregion

    /// <summary>The room for the picture: below the title, above the toolbar (and a walkthrough's subtitles).</summary>
    private Rect Stage()
    {
        var top = TitleSpace;
        var bottom = Math.Max(ToolbarSpace, ToolbarHeight + 18 + 16) + _reserve.Current;
        return new Rect(0, top, Math.Max(100, ActualWidth), Math.Max(200, ActualHeight - top - bottom));
    }

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1) return;

        _targets.Clear();
        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
        var now = _clock.Elapsed.TotalSeconds;
        switch (Mode)
        {
            case LensMode.Eye:
                DrawEyeMode(dc, now);
                break;

            case LensMode.Camera:
                DrawCameraMode(dc, now);
                break;

            default:
                DrawBenchMode(dc, now);
                break;
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, now, width);
    }

    #region drawing helpers

    /// <summary>Already localized text on a dark pill, centred on <paramref name="center"/> and kept inside the control.</summary>
    private Rect DrawPill(PaintContext dc, string text, Point center, Color color, double size = 11.5)
    {
        var formatted = _labels.Text(text, size, color);
        var x = Math.Clamp(center.X - (formatted.Width / 2) - 6, 2, Math.Max(2, ActualWidth - formatted.Width - 14));
        var y = Math.Clamp(center.Y - (formatted.Height / 2) - 2, 2, Math.Max(2, ActualHeight - formatted.Height - 6));
        _labels.DrawTextPill(dc, text, new Point(x, y), size, color);
        return new Rect(x, y, formatted.Width + 12, formatted.Height + 4);
    }

    /// <summary>A pill that is also a target for <paramref name="id"/>.</summary>
    private Rect DrawTargetPill(PaintContext dc, string text, Point center, Color color, string id, double size = 11.5)
    {
        var box = DrawPill(dc, text, center, color, size);
        AddBoxTarget(id, box);
        return box;
    }

    /// <summary>Text with its left edge (or its centre, or its right edge) at a point, its middle at the point's height.</summary>
    private Rect DrawText(PaintContext dc, string text, Point at, Color color, double size = 11, TextAnchor anchor = TextAnchor.Left, bool bold = false)
    {
        var formatted = _labels.Text(text, size, color, bold);
        var x = anchor switch
        {
            TextAnchor.Center => at.X - (formatted.Width / 2),
            TextAnchor.Right => at.X - formatted.Width,
            _ => at.X,
        };
        var topLeft = new Point(x, at.Y - (formatted.Height / 2));
        dc.DrawText(formatted, topLeft);
        return new Rect(topLeft, new Size(formatted.Width, formatted.Height));
    }

    private enum TextAnchor
    {
        Left,
        Center,
        Right,
    }

    private readonly Dictionary<(Color Color, int Alpha, double Thickness, bool Dashed), IPen> _fadedPens = [];
    private readonly Dictionary<(Color Color, int Alpha), IBrush> _fadedBrushes = [];

    /// <summary>A frozen pen of <paramref name="color"/> at <paramref name="alpha"/> (rounded to twentieths), made once and kept.</summary>
    private IPen FadedPen(Color color, double alpha, double thickness, bool dashed = false)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20), Math.Round(thickness, 1), dashed);
        if (_fadedPens.TryGetValue(key, out var pen)) return pen;
        var brush = Solid((byte)(key.Item2 * 12.75), color.R, color.G, color.B);
        pen = new ImmutablePen(((IBrush)brush).ToImmutable(), key.Item3, dashed ? new ImmutableDashStyle([4, 3], 0) : null, PenLineCap.Round, PenLineJoin.Round);
        _fadedPens[key] = pen;
        return pen;
    }

    /// <summary>A frozen brush of <paramref name="color"/> at <paramref name="alpha"/> (rounded to twentieths), made once and kept.</summary>
    private IBrush FadedBrush(Color color, double alpha)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20));
        if (_fadedBrushes.TryGetValue(key, out var brush)) return brush;
        brush = Solid((byte)(key.Item2 * 12.75), color.R, color.G, color.B);
        _fadedBrushes[key] = brush;
        return brush;
    }

    /// <summary>A line through the points, as one figure.</summary>
    private static StreamGeometry Line(IEnumerable<Point> points, bool closed = false, bool filled = false)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var first = true;
            foreach (var point in points)
            {
                if (first) context.BeginFigure(point, filled);
                else context.LineTo(point);
                first = false;
            }

            if (!first) context.EndFigure(closed);
        }
        return geometry;
    }

    /// <summary>A rounded frame for a panel inside the picture.</summary>
    private static void DrawFrame(PaintContext dc, Rect box) => dc.DrawRoundedRectangle(Ink.Panel, Ink.Frame, box, 12, 12);

    /// <summary>A double-headed arrow from <paramref name="a"/> to <paramref name="b"/> (a span such as a distance).</summary>
    private static void DrawSpan(PaintContext dc, Point a, Point b, Color color, double thickness = 1.2)
    {
        Vector v = b - a;
        if (v.Length < 8) return;
        var direction = v / v.Length;
        var brush = Solid(255, color.R, color.G, color.B);
        dc.DrawLine(MakePen(brush, thickness), a + (direction * 3), b - (direction * 3));
        DrawArrowHead(dc, brush, b, direction, 6);
        DrawArrowHead(dc, brush, a, -direction, 6);
    }

    /// <summary>A length in cm (or m past a metre).</summary>
    internal static string Centimetres(double cm) => Math.Abs(cm) >= 100
        ? LocFormat("Lens_Value_M", Number(cm / 100, "0.00"))
        : LocFormat("Lens_Value_Cm", Number(cm, Math.Abs(cm) < 10 ? "0.0" : "0.#"));

    /// <summary>A distance in m: cm under a metre, ∞ for infinity.</summary>
    internal static string Metres(double m) => double.IsPositiveInfinity(m) ? Loc("Lens_Value_Infinity")
        : m < 1 ? LocFormat("Lens_Value_Cm", Number(m * 100, "0"))
        : m < 10 ? LocFormat("Lens_Value_M", Number(m, "0.0"))
        : m < 1000 ? LocFormat("Lens_Value_M", Number(m, "0"))
        : LocFormat("Lens_Value_Km", Number(m / 1000, "0.#"));

    /// <summary>A shutter time as photographers write it: 1/125 s, 0.5 s, 1 s.</summary>
    internal static string ShutterText(double seconds) => seconds >= 0.5
        ? LocFormat("Lens_Value_S", Number(seconds, "0.#"))
        : LocFormat("Lens_Value_S", "1/" + Number(1 / seconds, "0"));

    /// <summary>An f-number: f/2.8, f/11.</summary>
    internal static string FText(double fNumber) => "f/" + Number(fNumber, "0.#");

    internal static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    #endregion

    private static class Ink
    {
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
        public static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
        public static readonly Color Green = Color.FromRgb(0x7C, 0xFF, 0x9E);
        public static readonly Color Orange = Color.FromRgb(0xFF, 0x9A, 0x4A);
        public static readonly Color Red = Color.FromRgb(0xFF, 0x5A, 0x5F);
        public static readonly Color Blue = Color.FromRgb(0x5A, 0xA8, 0xFF);
        public static readonly Color Violet = Color.FromRgb(0xB0, 0x8C, 0xFF);
        public static readonly Color Pink = Color.FromRgb(0xFF, 0x7E, 0xC0);
        public static readonly Color Glass = Color.FromRgb(0x9F, 0xE3, 0xFF);
        public static readonly Color Ivory = Color.FromRgb(0xF2, 0xEA, 0xD8);
        public static readonly Color Flame = Color.FromRgb(0xFF, 0xC8, 0x5A);

        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x12, 0x17, 0x26)), new ImmutableGradientStop(1, Color.FromRgb(0x05, 0x07, 0x0E))],
            center: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.85, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.85, RelativeUnit.Relative));

        public static readonly IBrush Panel = Solid(150, 8, 12, 22);
        public static readonly IPen Frame = MakePen(Solid(46, 255, 255, 255), 1);
        public static readonly IPen Axis = MakePen(Solid(80, 255, 255, 255), 1, dashed: true);
        public static readonly IPen Faint = MakePen(Solid(40, 255, 255, 255), 1);
    }
}
