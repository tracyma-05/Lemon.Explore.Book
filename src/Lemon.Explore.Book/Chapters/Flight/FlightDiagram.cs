using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Flight;

/// <summary>
/// The animated diagram of the flight page. Wing: a wing's cross-section in a wind tunnel with the air flowing round it
/// as streamlines and smoke, the pressure as colour and arrows, the lift, and the flow tearing off at the stall
/// (<c>FlightDiagram.Wing.cs</c>). Forces: a small plane in flight with its four forces, from the take-off run through
/// climb, cruise and glide to the stall (<c>FlightDiagram.Forces.cs</c>). Turn: the plane in 3D, its ailerons, elevator
/// and rudder, and the banked turn with its lift split in two (<c>FlightDiagram.Turn.cs</c>).
/// </summary>
/// <remarks>
/// The wing's flow is the exact ideal-flow solution (<see cref="Airfoil"/>); after the stall the torn-off wake is drawn,
/// not computed. The plane flies by <see cref="Trainer.Step"/>; it is drawn larger than the scenery it passes. The mouse
/// and the clickable things are in <c>FlightDiagram.Objects.cs</c>.
/// </remarks>
public sealed partial class FlightDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
        else if (change.Property == ShapeProperty)
        {
            OnWingChanged();
        }
        else if (change.Property == InvertedProperty)
        {
            OnWingChanged();
        }
        else if (change.Property == AlphaProperty)
        {
            OnWingChanged();
        }
        else if (change.Property == ShowTimelinesProperty)
        {
            OnTimelinesChanged();
        }
        else if (change.Property == SpeedProperty)
        {
            OnStateSet();
        }
        else if (change.Property == AltitudeProperty)
        {
            OnStateSet();
        }
        else if (change.Property == PathAngleProperty)
        {
            OnStateSet();
        }
        else if (change.Property == OnGroundProperty)
        {
            OnStateSet();
        }
        else if (change.Property == PositionXProperty)
        {
            OnStateSet();
        }
        else if (change.Property == DemoProperty)
        {
            OnDemoChanged();
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    #region dependency properties

    public static readonly StyledProperty<FlightMode> ModeProperty =
        AvaloniaProperty.Register<FlightDiagram, FlightMode>(nameof(Mode), FlightMode.Wing);

    public static readonly StyledProperty<WingShape> ShapeProperty =
        AvaloniaProperty.Register<FlightDiagram, WingShape>(nameof(Shape), WingShape.Cambered);

    /// <summary>Wing: the section turned upside down (as a plane flying inverted).</summary>
    public static readonly StyledProperty<bool> InvertedProperty =
        AvaloniaProperty.Register<FlightDiagram, bool>(nameof(Inverted), false);

    /// <summary>Wing: the angle of attack, degrees. Dragging the wing round sets it.</summary>
    public static readonly StyledProperty<double> AlphaProperty =
        AvaloniaProperty.Register<FlightDiagram, double>(nameof(Alpha), 4.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Wing: the wind tunnel's air speed, km/h (how fast the smoke moves and how strong the forces are).</summary>
    public static readonly StyledProperty<double> AirSpeedProperty =
        AvaloniaProperty.Register<FlightDiagram, double>(nameof(AirSpeed), 200.0);

    public static readonly StyledProperty<bool> ShowPressureProperty =
        AvaloniaProperty.Register<FlightDiagram, bool>(nameof(ShowPressure), true);

    public static readonly StyledProperty<bool> ShowStreamlinesProperty =
        AvaloniaProperty.Register<FlightDiagram, bool>(nameof(ShowStreamlines), true);

    public static readonly StyledProperty<bool> ShowSmokeProperty =
        AvaloniaProperty.Register<FlightDiagram, bool>(nameof(ShowSmoke), true);

    /// <summary>Wing: columns of smoke released together every few seconds, to see which air gets to the back first.</summary>
    public static readonly StyledProperty<bool> ShowTimelinesProperty =
        AvaloniaProperty.Register<FlightDiagram, bool>(nameof(ShowTimelines), false);

    /// <summary>Wing: arrows round the surface showing where the air pushes (red) and where it pulls (blue).</summary>
    public static readonly StyledProperty<bool> ShowArrowsProperty =
        AvaloniaProperty.Register<FlightDiagram, bool>(nameof(ShowArrows), false);

    /// <summary>Forces: the throttle, 0 to 1. Dragging the lever sets it.</summary>
    public static readonly StyledProperty<double> ThrottleProperty =
        AvaloniaProperty.Register<FlightDiagram, double>(nameof(Throttle), 0.7, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Forces: the plane's angle of attack, degrees. Dragging the plane's nose up or down sets it.</summary>
    public static readonly StyledProperty<double> PlaneAlphaProperty =
        AvaloniaProperty.Register<FlightDiagram, double>(nameof(PlaneAlpha), 1.3, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Forces: the plane's mass with its load, kg.</summary>
    public static readonly StyledProperty<double> MassProperty =
        AvaloniaProperty.Register<FlightDiagram, double>(nameof(Mass), 1000.0);

    /// <summary>Forces: the air speed, m/s; the diagram moves it as the plane flies, and a new value puts the plane there.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<FlightDiagram, double>(nameof(Speed), 55.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Forces: the height above the runway, m.</summary>
    public static readonly StyledProperty<double> AltitudeProperty =
        AvaloniaProperty.Register<FlightDiagram, double>(nameof(Altitude), 1000.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Forces: the climb angle, rad (negative going down).</summary>
    public static readonly StyledProperty<double> PathAngleProperty =
        AvaloniaProperty.Register<FlightDiagram, double>(nameof(PathAngle), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Forces: rolling on the runway rather than flying.</summary>
    public static readonly StyledProperty<bool> OnGroundProperty =
        AvaloniaProperty.Register<FlightDiagram, bool>(nameof(OnGround), false, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Forces: how far along the ground the plane is from the start of the runway, m.</summary>
    public static readonly StyledProperty<double> PositionXProperty =
        AvaloniaProperty.Register<FlightDiagram, double>(nameof(PositionX), 2000.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Turn: the bank the pilot asks for, degrees (right wing down positive). Dragging sideways sets it.</summary>
    public static readonly StyledProperty<double> BankProperty =
        AvaloniaProperty.Register<FlightDiagram, double>(nameof(Bank), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Turn: the air speed, km/h.</summary>
    public static readonly StyledProperty<double> TurnSpeedProperty =
        AvaloniaProperty.Register<FlightDiagram, double>(nameof(TurnSpeed), 180.0);

    /// <summary>Turn: what the plane shows; clicking a control surface shows that one.</summary>
    public static readonly StyledProperty<TurnDemo> DemoProperty =
        AvaloniaProperty.Register<FlightDiagram, TurnDemo>(nameof(Demo), TurnDemo.Turn, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<FlightDiagram, bool>(nameof(ShowLabels), true);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<FlightDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<FlightDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<FlightDiagram>(ModeProperty, ShapeProperty, InvertedProperty, AlphaProperty, AirSpeedProperty, ShowPressureProperty, ShowStreamlinesProperty, ShowSmokeProperty, ShowTimelinesProperty, ShowArrowsProperty, ThrottleProperty, PlaneAlphaProperty, MassProperty, SpeedProperty, AltitudeProperty, PathAngleProperty, PositionXProperty, BankProperty, TurnSpeedProperty, DemoProperty, ShowLabelsProperty, ToolbarHeightProperty);
        return true;
    }



    public FlightMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public WingShape Shape
    {
        get => GetValue(ShapeProperty);
        set => SetValue(ShapeProperty, value);
    }

    public bool Inverted
    {
        get => GetValue(InvertedProperty);
        set => SetValue(InvertedProperty, value);
    }

    public double Alpha
    {
        get => GetValue(AlphaProperty);
        set => SetValue(AlphaProperty, value);
    }

    public double AirSpeed
    {
        get => GetValue(AirSpeedProperty);
        set => SetValue(AirSpeedProperty, value);
    }

    public bool ShowPressure
    {
        get => GetValue(ShowPressureProperty);
        set => SetValue(ShowPressureProperty, value);
    }

    public bool ShowStreamlines
    {
        get => GetValue(ShowStreamlinesProperty);
        set => SetValue(ShowStreamlinesProperty, value);
    }

    public bool ShowSmoke
    {
        get => GetValue(ShowSmokeProperty);
        set => SetValue(ShowSmokeProperty, value);
    }

    public bool ShowTimelines
    {
        get => GetValue(ShowTimelinesProperty);
        set => SetValue(ShowTimelinesProperty, value);
    }

    public bool ShowArrows
    {
        get => GetValue(ShowArrowsProperty);
        set => SetValue(ShowArrowsProperty, value);
    }

    public double Throttle
    {
        get => GetValue(ThrottleProperty);
        set => SetValue(ThrottleProperty, value);
    }

    public double PlaneAlpha
    {
        get => GetValue(PlaneAlphaProperty);
        set => SetValue(PlaneAlphaProperty, value);
    }

    public double Mass
    {
        get => GetValue(MassProperty);
        set => SetValue(MassProperty, value);
    }

    public double Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public double Altitude
    {
        get => GetValue(AltitudeProperty);
        set => SetValue(AltitudeProperty, value);
    }

    public double PathAngle
    {
        get => GetValue(PathAngleProperty);
        set => SetValue(PathAngleProperty, value);
    }

    public bool OnGround
    {
        get => GetValue(OnGroundProperty);
        set => SetValue(OnGroundProperty, value);
    }

    public double PositionX
    {
        get => GetValue(PositionXProperty);
        set => SetValue(PositionXProperty, value);
    }

    public double Bank
    {
        get => GetValue(BankProperty);
        set => SetValue(BankProperty, value);
    }

    public double TurnSpeed
    {
        get => GetValue(TurnSpeedProperty);
        set => SetValue(TurnSpeedProperty, value);
    }

    public TurnDemo Demo
    {
        get => GetValue(DemoProperty);
        set => SetValue(DemoProperty, value);
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

    private readonly Stopwatch _clock = new();
    private readonly LabelCache _labels;
    private double _lastSeconds;

    /// <summary>The playing time, s: the smoke, the propeller and the demonstrations.</summary>
    private double _time;

    public FlightDiagram()
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
        OnStateSet();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        _clock.Stop();
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => _labels.Clear();

    private void OnModeChanged()
    {
        _drag = DragKind.None;
        _wand.Clear();
    }

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);
        if (IsPlaying)
        {
            _time += dt;
            switch (Mode)
            {
                case FlightMode.Wing:
                    StepWing(dt);
                    break;

                case FlightMode.Forces:
                    StepForces(dt);
                    break;

                case FlightMode.Turn:
                    StepTurn(dt);
                    break;
            }
        }

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
        var now = _clock.Elapsed.TotalSeconds;
        switch (Mode)
        {
            case FlightMode.Forces:
                DrawForcesMode(dc, now);
                break;

            case FlightMode.Turn:
                DrawTurnMode(dc, now);
                break;

            default:
                dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
                DrawWingMode(dc, now);
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

    private readonly Dictionary<(Color Color, int Alpha, double Thickness, bool Dashed, bool Flat), IPen> _fadedPens = [];
    private readonly Dictionary<(Color Color, int Alpha), IBrush> _fadedBrushes = [];

    /// <summary>A frozen pen of <paramref name="color"/> at <paramref name="alpha"/> (rounded to twentieths), made once and kept.</summary>
    private IPen FadedPen(Color color, double alpha, double thickness, bool dashed = false, bool flat = false)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20), Math.Round(thickness, 1), dashed, flat);
        if (_fadedPens.TryGetValue(key, out var pen)) return pen;
        var brush = Solid((byte)(key.Item2 * 12.75), color.R, color.G, color.B);
        var cap = flat ? PenLineCap.Flat : PenLineCap.Round;
        pen = new ImmutablePen(((IBrush)brush).ToImmutable(), key.Item3, dashed ? new ImmutableDashStyle([4, 3], 0) : null, cap, PenLineJoin.Round);
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

    /// <summary>An arrow from <paramref name="from"/> to <paramref name="to"/> with its head at the end.</summary>
    private void DrawArrow(PaintContext dc, Point from, Point to, Color color, double thickness = 2.5, double alpha = 1, bool dashed = false)
    {
        Vector v = to - from;
        if (v.Length < 2) return;
        var direction = v / v.Length;
        var head = Math.Min(Math.Max(7, thickness * 3.4), v.Length * 0.6);
        dc.DrawLine(FadedPen(color, alpha, thickness, dashed), from, to - (direction * head * 0.7));
        DrawArrowHead(dc, FadedBrush(color, alpha), to, direction, head);
    }

    /// <summary>Kilonewtons (or newtons below one kN).</summary>
    internal static string Newtons(double newtons) => Math.Abs(newtons) >= 1000
        ? LocFormat("Flight_Value_KN", Number(newtons / 1000, Math.Abs(newtons) >= 10_000 ? "0.0" : "0.00"))
        : LocFormat("Flight_Value_N", Number(newtons, "0"));

    internal static string Kmh(double metresPerSecond) => LocFormat("Flight_Value_Kmh", Number(metresPerSecond * 3.6, "0"));

    internal static string Metres(double metres) => double.IsPositiveInfinity(metres)
        ? Loc("Flight_Value_Infinity")
        : Math.Abs(metres) >= 10_000
            ? LocFormat("Flight_Value_Km", Number(metres / 1000, "0.0"))
            : LocFormat("Flight_Value_M", Number(metres, "0"));

    internal static string Degrees(double degrees) => LocFormat("Flight_Value_Degrees", Number(degrees, "0.#"));

    internal static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    #endregion

    /// <summary>
    /// Short lines gathered by pen and drawn as one geometry per pen: hundreds of separate translucent lines keep the render
    /// thread busy, a couple of dozen geometries do not.
    /// </summary>
    private sealed class LineBatch
    {
        private readonly Dictionary<IPen, (List<Point> Points, List<int> Starts)> _lines = [];

        public void Add(IPen pen, Point from, Point to)
        {
            var (points, starts) = Lists(pen);
            starts.Add(points.Count);
            points.Add(from);
            points.Add(to);
        }

        public void Add(IPen pen, IReadOnlyList<Point> line)
        {
            if (line.Count < 2) return;
            var (points, starts) = Lists(pen);
            starts.Add(points.Count);
            foreach (var point in line) points.Add(point);
        }

        private (List<Point> Points, List<int> Starts) Lists(IPen pen)
        {
            if (!_lines.TryGetValue(pen, out var lists)) _lines[pen] = lists = ([], []);
            return lists;
        }

        public void Draw(PaintContext dc)
        {
            foreach (var (pen, (points, starts)) in _lines)
            {
                if (starts.Count == 0) continue;
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    for (var i = 0; i < starts.Count; i++)
                    {
                        var end = i + 1 < starts.Count ? starts[i + 1] : points.Count;
                        context.BeginFigure(points[starts[i]], false);
                        for (var j = starts[i] + 1; j < end; j++) context.LineTo(points[j]);
                        context.EndFigure(false);
                    }
                }
                dc.DrawGeometry(null, pen, geometry);
            }

            foreach (var (points, starts) in _lines.Values)
            {
                points.Clear();
                starts.Clear();
            }
        }
    }

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
        public static readonly Color Smoke = Color.FromRgb(0xE6, 0xEE, 0xF8);
        public static readonly Color Body = Color.FromRgb(0xE9, 0xEE, 0xF4);

        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x12, 0x17, 0x26)), new ImmutableGradientStop(1, Color.FromRgb(0x05, 0x07, 0x0E))],
            center: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.85, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.85, RelativeUnit.Relative));

        public static readonly IBrush Panel = Solid(170, 8, 12, 22);
        public static readonly IPen Frame = MakePen(Solid(46, 255, 255, 255), 1);
        public static readonly IPen Axis = MakePen(Solid(80, 255, 255, 255), 1, dashed: true);
        public static readonly IPen Faint = MakePen(Solid(40, 255, 255, 255), 1);
    }
}
