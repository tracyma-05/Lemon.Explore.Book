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
using Lemon.Explore.Book.Infrastructures.Timing;

namespace Lemon.Explore.Book.Chapters.Relativity;

/// <summary>
/// The animated diagram of the relativity page. Light clock: a clock standing still and one riding a ship, the moving
/// one's light running slanted so it ticks slower (<c>RelativityDiagram.Clock.cs</c>). The train: a flash from the
/// middle of a fast train reaching its two ends together on the train, the back first on the platform
/// (<c>RelativityDiagram.Train.cs</c>). The twins: a spacetime diagram of a trip to a star and back, with a strip below it
/// showing where everyone is now (<c>RelativityDiagram.Twins.cs</c>).
/// </summary>
/// <remarks>
/// Light's speed is 1 in every view (<see cref="RelativityModel"/>). The light clock and the train can be watched from the
/// ground or from the ship (<see cref="Frame"/>); the picture is worked out in that frame, so both are equally right.
/// The mouse and the clickable things are in <c>RelativityDiagram.Objects.cs</c>.
/// </remarks>
public sealed partial class RelativityDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
        else if (change.Property == BetaProperty)
        {
            OnSpeedChanged();
        }
        else if (change.Property == FrameProperty)
        {
            OnSpeedChanged();
        }
        else if (change.Property == ResetCountProperty)
        {
            ResetClocks();
        }
        else if (change.Property == FlashCountProperty)
        {
            RestartTrain();
        }
        else if (change.Property == TrainHoldProperty)
        {
            OnTrainHold();
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    #region dependency properties

    public static readonly StyledProperty<RelativityMode> ModeProperty =
        AvaloniaProperty.Register<RelativityDiagram, RelativityMode>(nameof(Mode), RelativityMode.LightClock);

    /// <summary>The ship's (or train's) speed as a fraction of light's.</summary>
    public static readonly StyledProperty<double> BetaProperty =
        AvaloniaProperty.Register<RelativityDiagram, double>(nameof(Beta), 0.6, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whose point of view the light clock and the train are drawn from.</summary>
    public static readonly StyledProperty<RelativityFrame> FrameProperty =
        AvaloniaProperty.Register<RelativityDiagram, RelativityFrame>(nameof(Frame), RelativityFrame.Ground, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Light clock: the light-clock triangle on the moving clock's light.</summary>
    public static readonly StyledProperty<bool> ShowTriangleProperty =
        AvaloniaProperty.Register<RelativityDiagram, bool>(nameof(ShowTriangle), true);

    /// <summary>Light clock: the zigzag the moving clock's light has drawn.</summary>
    public static readonly StyledProperty<bool> ShowTrailProperty =
        AvaloniaProperty.Register<RelativityDiagram, bool>(nameof(ShowTrail), true);

    /// <summary>Light clock: changed to set both clocks back to 0.</summary>
    public static readonly StyledProperty<int> ResetCountProperty =
        AvaloniaProperty.Register<RelativityDiagram, int>(nameof(ResetCount), 0);

    /// <summary>Light clock: ticks of the clock standing still in this frame, reported back.</summary>
    public static readonly StyledProperty<int> StillTicksProperty =
        AvaloniaProperty.Register<RelativityDiagram, int>(nameof(StillTicks), 0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Light clock: ticks of the moving clock, reported back.</summary>
    public static readonly StyledProperty<int> MovingTicksProperty =
        AvaloniaProperty.Register<RelativityDiagram, int>(nameof(MovingTicks), 0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The train: clocks on the train and the platform.</summary>
    public static readonly StyledProperty<bool> ShowClocksProperty =
        AvaloniaProperty.Register<RelativityDiagram, bool>(nameof(ShowClocks), false);

    /// <summary>The train: lengths measured against the rest length.</summary>
    public static readonly StyledProperty<bool> ShowLengthsProperty =
        AvaloniaProperty.Register<RelativityDiagram, bool>(nameof(ShowLengths), true);

    /// <summary>The train: changed to run the flash again from the start.</summary>
    public static readonly StyledProperty<int> FlashCountProperty =
        AvaloniaProperty.Register<RelativityDiagram, int>(nameof(FlashCount), 0);

    /// <summary>The train: "Before", "Flight", "RearHit" or "BothHit", reported back.</summary>
    public static readonly StyledProperty<string> TrainPhaseProperty =
        AvaloniaProperty.Register<RelativityDiagram, string>(nameof(TrainPhase), "Before", defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The train: hold the run at this frame time (≥ 0 holds, NaN plays). Set by walkthrough steps.</summary>
    public static readonly StyledProperty<double> TrainHoldProperty =
        AvaloniaProperty.Register<RelativityDiagram, double>(nameof(TrainHold), double.NaN);

    /// <summary>The twins: how far away the star is, light-years.</summary>
    public static readonly StyledProperty<double> DistanceProperty =
        AvaloniaProperty.Register<RelativityDiagram, double>(nameof(Distance), 4.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The twins: Earth's time since the ship left, years. Dragging the diagram or the chart scrubs it.</summary>
    public static readonly StyledProperty<double> TwinTimeProperty =
        AvaloniaProperty.Register<RelativityDiagram, double>(nameof(TwinTime), 0.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> ShowSignalsProperty =
        AvaloniaProperty.Register<RelativityDiagram, bool>(nameof(ShowSignals), false);

    /// <summary>The twins: the traveller's line of "now" and its jump at the turn.</summary>
    public static readonly StyledProperty<bool> ShowNowLinesProperty =
        AvaloniaProperty.Register<RelativityDiagram, bool>(nameof(ShowNowLines), false);

    /// <summary>The twins: the ship's own time and space axes on the way out.</summary>
    public static readonly StyledProperty<bool> ShowAxesProperty =
        AvaloniaProperty.Register<RelativityDiagram, bool>(nameof(ShowAxes), false);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<RelativityDiagram, bool>(nameof(ShowLabels), true);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<RelativityDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<RelativityDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<RelativityDiagram>(ModeProperty, BetaProperty, FrameProperty, ShowTriangleProperty, ShowTrailProperty, ResetCountProperty, ShowClocksProperty, ShowLengthsProperty, FlashCountProperty, TrainHoldProperty, DistanceProperty, TwinTimeProperty, ShowSignalsProperty, ShowNowLinesProperty, ShowAxesProperty, ShowLabelsProperty, ToolbarHeightProperty);
        return true;
    }




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

    public RelativityFrame Frame
    {
        get => GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    public bool ShowTriangle
    {
        get => GetValue(ShowTriangleProperty);
        set => SetValue(ShowTriangleProperty, value);
    }

    public bool ShowTrail
    {
        get => GetValue(ShowTrailProperty);
        set => SetValue(ShowTrailProperty, value);
    }

    public int ResetCount
    {
        get => GetValue(ResetCountProperty);
        set => SetValue(ResetCountProperty, value);
    }

    public int StillTicks
    {
        get => GetValue(StillTicksProperty);
        set => SetValue(StillTicksProperty, value);
    }

    public int MovingTicks
    {
        get => GetValue(MovingTicksProperty);
        set => SetValue(MovingTicksProperty, value);
    }

    public bool ShowClocks
    {
        get => GetValue(ShowClocksProperty);
        set => SetValue(ShowClocksProperty, value);
    }

    public bool ShowLengths
    {
        get => GetValue(ShowLengthsProperty);
        set => SetValue(ShowLengthsProperty, value);
    }

    public int FlashCount
    {
        get => GetValue(FlashCountProperty);
        set => SetValue(FlashCountProperty, value);
    }

    public string TrainPhase
    {
        get => GetValue(TrainPhaseProperty);
        set => SetValue(TrainPhaseProperty, value);
    }

    public double TrainHold
    {
        get => GetValue(TrainHoldProperty);
        set => SetValue(TrainHoldProperty, value);
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

    public bool ShowSignals
    {
        get => GetValue(ShowSignalsProperty);
        set => SetValue(ShowSignalsProperty, value);
    }

    public bool ShowNowLines
    {
        get => GetValue(ShowNowLinesProperty);
        set => SetValue(ShowNowLinesProperty, value);
    }

    public bool ShowAxes
    {
        get => GetValue(ShowAxesProperty);
        set => SetValue(ShowAxesProperty, value);
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

    public RelativityDiagram()
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

    private void OnModeChanged()
    {
        _drag = DragKind.None;
        ResetClocks();
        RestartTrain();
    }

    /// <summary>A new speed or point of view starts the clocks and the flash over: what was drawn belongs to the old one.</summary>
    private void OnSpeedChanged()
    {
        ResetClocks();
        RestartTrain();
    }

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);
        if (IsPlaying && _drag is DragKind.None or DragKind.Pending)
        {
            switch (Mode)
            {
                case RelativityMode.LightClock:
                    StepClocks(dt);
                    break;

                case RelativityMode.Simultaneity:
                    StepTrain(dt);
                    break;

                case RelativityMode.Twins:
                    StepTwins(dt);
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
        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
        switch (Mode)
        {
            case RelativityMode.Simultaneity:
                DrawTrainMode(dc, now);
                break;

            case RelativityMode.Twins:
                DrawTwinsMode(dc, now);
                break;

            default:
                DrawClockMode(dc, now);
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

    /// <summary>Pills in a row from right to left, the first one's right edge at <paramref name="right"/>, all centred on <paramref name="y"/>.</summary>
    private void DrawPillRow(PaintContext dc, double right, double y, params (string Text, Color Color, string Id)[] pills)
    {
        foreach (var (text, color, id) in pills)
        {
            var width = _labels.Text(text, 12, color).Width + 12;
            var box = DrawTargetPill(dc, text, new Point(right - (width / 2), y), color, id, 12);
            right = box.Left - 8;
        }
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

    /// <summary>A glowing dot of light: a soft halo and a white-hot core.</summary>
    private void DrawFlash(PaintContext dc, Point at, double radius, Color color)
    {
        dc.DrawEllipse(FadedBrush(color, 0.16), null, at, radius * 3.2, radius * 3.2);
        dc.DrawEllipse(FadedBrush(color, 0.35), null, at, radius * 1.9, radius * 1.9);
        dc.DrawEllipse(FadedBrush(color, 1), null, at, radius, radius);
        dc.DrawEllipse(Brushes.White, null, at, radius * 0.55, radius * 0.55);
    }

    /// <summary>A small clock face: its hand goes round once per <paramref name="perTurn"/> of <paramref name="time"/>, with the time written under it.</summary>
    private Rect DrawDial(PaintContext dc, Point center, double radius, double time, double perTurn, Color color, string? caption)
    {
        dc.DrawEllipse(Ink.DialFace, FadedPen(color, 0.9, 1.6), center, radius, radius);
        for (var i = 0; i < 12; i++)
        {
            var a = i * Math.PI / 6;
            var d = new Vector(Math.Sin(a), -Math.Cos(a));
            dc.DrawLine(FadedPen(Ink.Dim, 0.8, 1), center + (d * radius * 0.78), center + (d * radius * 0.92));
        }

        var angle = 2 * Math.PI * (time / perTurn);
        var hand = new Vector(Math.Sin(angle), -Math.Cos(angle));
        dc.DrawLine(FadedPen(color, 1, 2.2), center, center + (hand * radius * 0.8));
        dc.DrawEllipse(FadedBrush(color, 1), null, center, 2.2, 2.2);
        var box = new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2);
        if (caption is not null) box.Union(DrawText(dc, caption, new Point(center.X, center.Y + radius + 10), color, 11, TextAnchor.Center, bold: true));
        return box;
    }

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
        public static readonly Color Light = Color.FromRgb(0xFF, 0xF2, 0xA8);
        public static readonly Color Steel = Color.FromRgb(0xC9, 0xD2, 0xDE);

        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x0D, 0x11, 0x22)), new ImmutableGradientStop(1, Color.FromRgb(0x03, 0x04, 0x09))],
            center: new RelativePoint(0.45, 0.45, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.45, 0.45, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.9, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.9, RelativeUnit.Relative));

        public static readonly IBrush Panel = Solid(185, 8, 11, 20);
        public static readonly IBrush DialFace = Solid(230, 14, 18, 30);
        public static readonly IPen Frame = MakePen(Solid(46, 255, 255, 255), 1);
        public static readonly IPen Faint = MakePen(Solid(34, 255, 255, 255), 1);
        public static readonly IPen Grid = MakePen(Solid(22, 255, 255, 255), 1);

        public static readonly IBrush SpaceLane = SpaceDrawing.LinearGradient(Color.FromRgb(0x07, 0x09, 0x16), Color.FromRgb(0x0E, 0x12, 0x26), 90);
        public static readonly IBrush GroundSky = SpaceDrawing.LinearGradient(Color.FromRgb(0x10, 0x1A, 0x2E), Color.FromRgb(0x1C, 0x2A, 0x3C), 90);
        public static readonly IBrush Platform = Solid(255, 0x1A, 0x1F, 0x27);
        public static readonly IBrush PlatformSlab = Solid(255, 0x3A, 0x40, 0x4A);
        public static readonly IBrush PlotBack = Solid(150, 6, 8, 16);
        public static readonly IBrush Glass = Solid(40, 0x9F, 0xD8, 0xFF);
        public static readonly IBrush MirrorBack = Solid(255, 0x5A, 0x63, 0x70);
        public static readonly IBrush Hull = SpaceDrawing.LinearGradient(Color.FromRgb(0x3A, 0x45, 0x58), Color.FromRgb(0x22, 0x29, 0x36), 90);
        public static readonly IBrush Fin = Solid(255, 0xC0, 0x4A, 0x4F);
        public static readonly IBrush Window = Solid(255, 0x16, 0x3A, 0x58);
        public static readonly IBrush Flame = SpaceDrawing.LinearGradient(Color.FromArgb(0, 0xFF, 0x7A, 0x2A), Color.FromArgb(230, 0xFF, 0xC8, 0x5A), 0);
        public static readonly IBrush Stand = Solid(255, 0x3A, 0x40, 0x4A);
        public static readonly IBrush Carriage = SpaceDrawing.LinearGradient(Color.FromRgb(0x2E, 0x4F, 0x78), Color.FromRgb(0x1C, 0x31, 0x4C), 90);
        public static readonly IBrush TrainWindow = Solid(255, 0x0E, 0x16, 0x24);
        public static readonly IBrush Wheel = Solid(255, 0x14, 0x17, 0x1D);
        public static readonly IBrush EarthBall = SpaceDrawing.RadialGradient((Color.FromRgb(0x6C, 0xB8, 0xFF), 0), (Color.FromRgb(0x1E, 0x5A, 0x3A), 1));
    }
}
