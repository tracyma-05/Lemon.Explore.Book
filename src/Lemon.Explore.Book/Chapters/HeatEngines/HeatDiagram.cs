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

namespace Lemon.Explore.Book.Chapters.HeatEngines;

/// <summary>
/// The animated diagram of the engine-and-fridge page. Engine mode: one cylinder of a four-stroke engine cut open
/// (<c>HeatDiagram.Engine.cs</c>). Fridge mode: a fridge-freezer from the side with its refrigerant going round
/// (<c>HeatDiagram.Fridge.cs</c>). Cycle mode: the ideal cycle between a hot and a cold place, as an engine or run
/// backwards as a fridge or heat pump (<c>HeatDiagram.Cycle.cs</c>).
/// </summary>
/// <remarks>
/// The diagram owns the clock and pushes back <see cref="Angle"/> (engine) and <see cref="Phase"/> (cycle) as it runs;
/// dragging sideways turns them by hand. Clicking the fridge's door opens or shuts it (<see cref="DoorOpen"/>); the
/// mouse and the clickable things are in <c>HeatDiagram.Objects.cs</c>.
/// </remarks>
public sealed partial class HeatDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
        else if (change.Property == AngleProperty)
        {
            OnAngleChanged(change.GetNewValue<double>());
        }
        else if (change.Property == PhaseProperty)
        {
            OnPhaseChanged(change.GetNewValue<double>());
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    /// <summary>Engine: crank degrees per second of playback at 1× (a whole cycle, two turns, in 4 s).</summary>
    private const double DegreesPerSecond = 180;

    /// <summary>Cycle: steps per second at 1× (the four steps in 8 s).</summary>
    private const double StepsPerSecond = 0.5;

    /// <summary>Fridge: how far round the loop the refrigerant goes per second at 1×.</summary>
    private const double LoopPerSecond = 0.035;

    #region dependency properties

    public static readonly StyledProperty<HeatMode> ModeProperty =
        AvaloniaProperty.Register<HeatDiagram, HeatMode>(nameof(Mode), HeatMode.Engine);

    /// <summary>Engine: the crank angle, 0…720° (pushed back as it turns).</summary>
    public static readonly StyledProperty<double> AngleProperty =
        AvaloniaProperty.Register<HeatDiagram, double>(nameof(Angle), 0.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> CompressionRatioProperty =
        AvaloniaProperty.Register<HeatDiagram, double>(nameof(CompressionRatio), EngineModel.DefaultCompressionRatio);

    /// <summary>Engine: the outside air's pressure, bar (lower high up).</summary>
    public static readonly StyledProperty<double> AmbientProperty =
        AvaloniaProperty.Register<HeatDiagram, double>(nameof(Ambient), EngineModel.SeaLevelPressure);

    /// <summary>Fridge: the room's temperature, °C.</summary>
    public static readonly StyledProperty<double> RoomProperty =
        AvaloniaProperty.Register<HeatDiagram, double>(nameof(Room), FridgeModel.DefaultRoom);

    /// <summary>Fridge: the door stands open (clicking it toggles this).</summary>
    public static readonly StyledProperty<bool> DoorOpenProperty =
        AvaloniaProperty.Register<HeatDiagram, bool>(nameof(DoorOpen), false, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Cycle: where in the engine's cycle the gas is, 0…4 (pushed back as it runs; a fridge runs it backwards).</summary>
    public static readonly StyledProperty<double> PhaseProperty =
        AvaloniaProperty.Register<HeatDiagram, double>(nameof(Phase), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Cycle: the hot place's temperature, K.</summary>
    public static readonly StyledProperty<double> HotProperty =
        AvaloniaProperty.Register<HeatDiagram, double>(nameof(Hot), 500.0);

    /// <summary>Cycle: the cold place's temperature, K.</summary>
    public static readonly StyledProperty<double> ColdProperty =
        AvaloniaProperty.Register<HeatDiagram, double>(nameof(Cold), 300.0);

    public static readonly StyledProperty<CycleDirection> DirectionProperty =
        AvaloniaProperty.Register<HeatDiagram, CycleDirection>(nameof(Direction), CycleDirection.Engine);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<HeatDiagram, bool>(nameof(ShowLabels), true);

    /// <summary>Playback speed: 1 is the normal pace.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<HeatDiagram, double>(nameof(Speed), 1.0);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<HeatDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<HeatDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<HeatDiagram>(ModeProperty, AngleProperty, CompressionRatioProperty, AmbientProperty, RoomProperty, DoorOpenProperty, PhaseProperty, HotProperty, ColdProperty, DirectionProperty, ShowLabelsProperty, SpeedProperty, ToolbarHeightProperty);
        return true;
    }


    public HeatMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

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

    public double Phase
    {
        get => GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }

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

    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    public double Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
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
    private readonly StarBackdrop _stars = new();
    private readonly LabelCache _labels;

    private double _lastSeconds;
    private bool _pushing;

    /// <summary>Animation seconds (scaled by the speed, stopped while paused): particles, flames, flowing arrows.</summary>
    private double _t;

    /// <summary>The clocks of the modes, kept here while they run and pushed back to the page.</summary>
    private double _angle;

    private double _phase;
    private double _loop;

    private bool _dragging;
    private Point _dragStart;
    private double _dragStartValue;
    private bool _dragMoved;

    public HeatDiagram()
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

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _stars.OnSizeChanged(_clock.Elapsed.TotalSeconds);
        _room = null;
    }

    private void OnModeChanged() => _dragging = false;

    private void OnAngleChanged(double value)
    {
        if (!_pushing) _angle = EngineModel.Wrap(value);
    }

    private void OnPhaseChanged(double value)
    {
        if (!_pushing) _phase = ((value % 4) + 4) % 4;
    }

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);

        // A walkthrough holds the crank or the cycle at the moment it talks about, but the gas keeps jiggling.
        var step = IsPlaying && !_dragging ? dt * Math.Clamp(Speed, 0.05, 8) : 0;
        _t += (IsPlaying || IsLessonActive) && !_dragging ? dt * Math.Clamp(Speed, 0.05, 8) : 0;
        if (step > 0)
        {
            switch (Mode)
            {
                case HeatMode.Engine:
                    _angle = EngineModel.Wrap(_angle + (step * DegreesPerSecond));
                    Push(AngleProperty, Math.Round(_angle, 3));
                    break;

                case HeatMode.Fridge:
                    _loop += step * LoopPerSecond;
                    break;

                case HeatMode.Cycle:
                    // A fridge or heat pump goes round the same loop the other way.
                    var sign = Direction == CycleDirection.Engine ? 1 : -1;
                    _phase = (((_phase + (sign * step * StepsPerSecond)) % 4) + 4) % 4;
                    Push(PhaseProperty, Math.Round(_phase, 4));
                    break;
            }
        }

        InvalidateVisual();
    }

    private void Push(AvaloniaProperty property, object value)
    {
        if (Equals(GetValue(property), value)) return;
        _pushing = true;
        try
        {
            SetCurrentValue(property, value);
        }
        finally
        {
            _pushing = false;
        }
    }

    #endregion

    /// <summary>The room for the picture: below the title, above the toolbar (and a walkthrough's subtitles).</summary>
    private Rect Stage()
    {
        var top = TitleSpace;
        var bottom = Math.Max(ToolbarSpace, ToolbarHeight + 18 + 16) + _reserve.Current;
        return new Rect(0, top, Math.Max(100, ActualWidth), Math.Max(160, ActualHeight - top - bottom));
    }

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1) return;

        _targets.Clear();
        switch (Mode)
        {
            case HeatMode.Fridge:
                DrawFridgeMode(dc, width, height);
                break;

            case HeatMode.Cycle:
                DrawCycleMode(dc, width, height);
                break;

            default:
                DrawEngineMode(dc, width, height);
                break;
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, _clock.Elapsed.TotalSeconds, width);
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

    /// <summary>Text with its left edge (or its centre) at a point, its middle at the point's height; where it went.</summary>
    private Rect DrawText(PaintContext dc, string text, Point at, Color color, double size = 11, bool center = false, bool bold = false)
    {
        var formatted = _labels.Text(text, size, color, bold);
        var topLeft = new Point(center ? at.X - (formatted.Width / 2) : at.X, at.Y - (formatted.Height / 2));
        dc.DrawText(formatted, topLeft);
        return new Rect(topLeft, new Size(formatted.Width, formatted.Height));
    }

    /// <summary>Text whose right edge is at <paramref name="at"/>, its middle at the point's height; where it went.</summary>
    private Rect DrawTextRight(PaintContext dc, string text, Point at, Color color, double size = 11, bool bold = false)
    {
        var formatted = _labels.Text(text, size, color, bold);
        var topLeft = new Point(at.X - formatted.Width, at.Y - (formatted.Height / 2));
        dc.DrawText(formatted, topLeft);
        return new Rect(topLeft, new Size(formatted.Width, formatted.Height));
    }

    /// <summary>A label with a thin leader line from the part it names; the label is a target for <paramref name="id"/>.</summary>
    private void DrawCallout(PaintContext dc, string text, Point part, Point label, bool right, string id)
    {
        dc.DrawLine(Ink.Leader, part, label);
        dc.DrawEllipse(Ink.LeaderDot, null, part, 2.2, 2.2);
        var formatted = _labels.Text(text, 11.5, Ink.Text);
        var x = right ? label.X + 4 : label.X - formatted.Width - 16;

        // The book's page is narrower than the original window: a label that would run off the plate moves in over the
        // picture, its leader still pointing at the part.
        x = Math.Clamp(x, 6, Math.Max(6, ActualWidth - formatted.Width - 18));
        var topLeft = new Point(x, label.Y - (formatted.Height / 2) - 2);
        _labels.DrawTextPill(dc, text, topLeft, 11.5, Ink.Text);
        AddBoxTarget(id, new Rect(topLeft, new Size(formatted.Width + 12, formatted.Height + 4)));
    }

    /// <summary>An arrow from <paramref name="from"/> to <paramref name="to"/>.</summary>
    private static void DrawArrow(PaintContext dc, Point from, Point to, IBrush brush, double thickness = 2, double head = 9)
    {
        Vector v = to - from;
        if (v.Length < 3) return;
        var direction = v / v.Length;
        dc.DrawLine(MakePen(brush, thickness), from, to - (direction * head * 0.6));
        DrawArrowHead(dc, brush, to, direction, head);
    }

    /// <summary>
    /// A wavy "heat" arrow from <paramref name="from"/> to <paramref name="to"/>, its waves running along it with time.
    /// </summary>
    private void DrawHeatArrow(PaintContext dc, Point from, Point to, Color color, double thickness, double opacity = 1)
    {
        if (opacity <= 0.02) return;
        Vector v = to - from;
        var length = v.Length;
        if (length < 8) return;
        var along = v / length;
        var across = new Vector(-along.Y, along.X);
        var head = Math.Max(8, thickness * 3);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            const int steps = 40;
            for (var i = 0; i <= steps; i++)
            {
                var s = i / (double)steps * (length - (head * 0.7));
                var wave = Math.Sin((s / 9) - (_t * 6)) * thickness * 0.9 * Math.Min(1, s / 12);
                var point = from + (along * s) + (across * wave);
                if (i == 0) context.BeginFigure(point, false);
                else context.LineTo(point);
            }

            context.EndFigure(false);
        }
        var brush = FadedBrush(color, opacity);
        dc.DrawGeometry(null, FadedPen(color, opacity, thickness), geometry);
        DrawArrowHead(dc, brush, to, along, head);
    }

    /// <summary>A thick straight arrow whose width shows an amount of energy, its body broken by moving dashes.</summary>
    private void DrawFlowArrow(PaintContext dc, Point from, Point to, Color color, double width, double opacity)
    {
        Vector v = to - from;
        var length = v.Length;
        if (length < 6 || width < 0.5) return;
        var along = v / length;
        var across = new Vector(-along.Y, along.X);
        var head = Math.Min(length * 0.45, Math.Max(12, width * 1.1));
        var neck = to - (along * head);
        var half = width / 2;
        var headHalf = Math.Max(half + 5, half * 1.7);
        var body = Shape(from + (across * half), neck + (across * half), neck + (across * headHalf), to, neck - (across * headHalf),
            neck - (across * half), from - (across * half));
        dc.DrawGeometry(FadedBrush(color, 0.35 * opacity), FadedPen(color, 0.9 * opacity, 1.2), body);

        // Bright dashes moving along the arrow show the flow.
        var dash = Math.Max(8, width * 0.9);
        var offset = Frac(_t * 0.9) * dash * 2;
        for (var s = offset; s < length - head; s += dash * 2)
        {
            var a = from + (along * s);
            var b = from + (along * Math.Min(s + dash, length - head));
            dc.DrawLine(FadedPen(color, 0.8 * opacity, Math.Max(1.5, width * 0.45), flat: true), a, b);
        }
    }

    /// <summary>A small thermometer whose red column stands at <paramref name="celsius"/> on a −30…50 °C scale.</summary>
    private static void DrawThermometer(PaintContext dc, Point bulb, double height, double celsius)
    {
        var tube = new Rect(bulb.X - 3, bulb.Y - height, 6, height);
        dc.DrawRoundedRectangle(Ink.Glass, Ink.GlassEdge, tube, 3, 3);
        var level = Math.Clamp((celsius + 30) / 80, 0.05, 1);
        var column = celsius < 0 ? Ink.ColdColumn : Ink.Mercury;
        dc.DrawRectangle(column, null, new Rect(bulb.X - 1.6, bulb.Y - (height * level), 3.2, height * level));
        dc.DrawEllipse(column, Ink.GlassEdge, bulb, 5, 5);
    }

    /// <summary>The colour of a gas or refrigerant at a temperature, °C: icy blue, white near 10 °C, orange, red, yellow-white when very hot.</summary>
    internal static Color HeatColor(double celsius)
    {
        (double T, Color C)[] stops =
        [
            (-40, Color.FromRgb(0x4A, 0x8B, 0xFF)),
            (-10, Color.FromRgb(0x7F, 0xC8, 0xFF)),
            (15, Color.FromRgb(0xE6, 0xEE, 0xF6)),
            (45, Color.FromRgb(0xFF, 0xB0, 0x5A)),
            (90, Color.FromRgb(0xFF, 0x6A, 0x4A)),
            (400, Color.FromRgb(0xE0, 0x4A, 0x6A)),
            (1000, Color.FromRgb(0xFF, 0x7A, 0x2A)),
            (1800, Color.FromRgb(0xFF, 0xC8, 0x3A)),
            (2400, Color.FromRgb(0xFF, 0xF4, 0xC0)),
        ];
        if (celsius <= stops[0].T) return stops[0].C;
        for (var i = 1; i < stops.Length; i++)
        {
            if (celsius <= stops[i].T) return Lerp(stops[i - 1].C, stops[i].C, (celsius - stops[i - 1].T) / (stops[i].T - stops[i - 1].T));
        }

        return stops[^1].C;
    }

    private readonly Dictionary<(Color Color, int Alpha, double Thickness, bool Flat), IPen> _fadedPens = [];
    private readonly Dictionary<(Color Color, int Alpha), IBrush> _fadedBrushes = [];

    /// <summary>A frozen pen of <paramref name="color"/> at <paramref name="alpha"/> (rounded to twentieths), made once and kept.</summary>
    private IPen FadedPen(Color color, double alpha, double thickness, bool flat = false)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20), Math.Round(thickness, 1), flat);
        if (_fadedPens.TryGetValue(key, out var pen)) return pen;
        var brush = Solid((byte)(key.Item2 * 12.75), color.R, color.G, color.B);
        pen = flat ? new ImmutablePen(((IBrush)brush).ToImmutable(), key.Item3) : MakePen(brush, key.Item3);
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

    /// <summary>A colour rounded to a 32-step grid, so the pen and brush caches stay small while colours change smoothly.</summary>
    private static Color Quantize(Color color) => Color.FromRgb((byte)(color.R & 0xF8), (byte)(color.G & 0xF8), (byte)(color.B & 0xF8));

    /// <summary>
    /// A closed shape whose every edge is stroked (<see cref="SpaceDrawing.Polygon"/> strokes only the closing one).
    /// </summary>
    private static StreamGeometry Shape(params Point[] points)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], true);
            for (var i = 1; i < points.Length; i++) context.LineTo(points[i]);

            context.EndFigure(true);
        }
        return geometry;
    }

    /// <summary>An open line through the points, as one figure.</summary>
    private static StreamGeometry Line(IEnumerable<Point> points)
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

    /// <summary>The fractional part, for looping animations.</summary>
    private static double Frac(double value) => value - Math.Floor(value);

    private static double Smooth(double a, double b, double value) => EngineModel.Smooth(a, b, value);

    private static double Mix(double a, double b, double t) => a + ((b - a) * t);

    private static Point Mix(Point a, Point b, double t) => new(Mix(a.X, b.X, t), Mix(a.Y, b.Y, t));

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
        public static readonly Color Green = Color.FromRgb(0x8B, 0xE0, 0x8F);
        public static readonly Color Orange = Color.FromRgb(0xFF, 0x9F, 0x5A);
        public static readonly Color Red = Color.FromRgb(0xFF, 0x6A, 0x5A);
        public static readonly Color Blue = Color.FromRgb(0x5A, 0xA8, 0xFF);
        public static readonly Color Violet = Color.FromRgb(0xB3, 0x9D, 0xFF);
        public static readonly Color Spark = Color.FromRgb(0xFF, 0xF6, 0xC8);

        /// <summary>The four strokes' colours, also used by the side panel's charts.</summary>
        public static readonly Color[] Strokes =
        [
            Color.FromRgb(0x5A, 0xB0, 0xFF),
            Color.FromRgb(0xB3, 0x9D, 0xFF),
            Color.FromRgb(0xFF, 0x9F, 0x5A),
            Color.FromRgb(0xA8, 0xB0, 0xBC),
        ];

        public static readonly IBrush Metal = SpaceDrawing.LinearGradient(Color.FromRgb(0x8A, 0x93, 0xA2), Color.FromRgb(0x4E, 0x56, 0x64), 0);
        public static readonly IBrush MetalDark = SpaceDrawing.LinearGradient(Color.FromRgb(0x5E, 0x66, 0x74), Color.FromRgb(0x30, 0x36, 0x40), 90);
        public static readonly IBrush Block = SpaceDrawing.LinearGradient(Color.FromRgb(0x48, 0x50, 0x5E), Color.FromRgb(0x2A, 0x30, 0x3A), 90);
        public static readonly IBrush Head = SpaceDrawing.LinearGradient(Color.FromRgb(0x56, 0x5E, 0x6C), Color.FromRgb(0x36, 0x3C, 0x48), 90);
        public static readonly IBrush Piston = SpaceDrawing.LinearGradient(Color.FromRgb(0xC8, 0xCE, 0xD8), Color.FromRgb(0x82, 0x8A, 0x98), 0);
        public static readonly IBrush Rod = SpaceDrawing.LinearGradient(Color.FromRgb(0xB0, 0xB8, 0xC4), Color.FromRgb(0x70, 0x78, 0x86), 0);
        public static readonly IBrush Crank = Solid(255, 0x6A, 0x72, 0x80);
        public static readonly IBrush Counterweight = Solid(255, 0x52, 0x5A, 0x66);
        public static readonly IBrush Water = Solid(150, 0x3A, 0x7C, 0xD8);
        public static readonly IBrush Oil = SpaceDrawing.LinearGradient(Color.FromArgb(160, 0xC8, 0x90, 0x2A), Color.FromArgb(200, 0x7A, 0x52, 0x10), 90);
        public static readonly IBrush Port = Solid(255, 0x14, 0x18, 0x22);
        public static readonly IPen Edge = MakePen(Solid(200, 0x1A, 0x1E, 0x26), 1.2);
        public static readonly IPen LightEdge = MakePen(Solid(120, 0xEE, 0xF1, 0xF6), 1);
        public static readonly IPen Ring = MakePen(Solid(255, 0x3A, 0x40, 0x4A), 1.5);
        public static readonly IPen Spring = MakePen(Solid(255, 0xB8, 0xC0, 0xCC), 1.4);
        public static readonly IBrush Valve = SpaceDrawing.LinearGradient(Color.FromRgb(0xD0, 0xD6, 0xDE), Color.FromRgb(0x8A, 0x92, 0x9E), 0);
        public static readonly IBrush Cam = Solid(255, 0x7A, 0x82, 0x90);
        public static readonly IBrush Plug = Solid(255, 0xE8, 0xEC, 0xF2);
        public static readonly IBrush PlugMetal = Solid(255, 0x9A, 0xA2, 0xAE);

        public static readonly IPen Leader = MakePen(Solid(150, 0xEE, 0xF1, 0xF6), 1);
        public static readonly IBrush LeaderDot = Solid(220, 0xEE, 0xF1, 0xF6);

        public static readonly IBrush Glass = Solid(150, 0xEE, 0xF1, 0xF6);
        public static readonly IPen GlassEdge = MakePen(Solid(200, 0x30, 0x38, 0x48), 1);
        public static readonly IBrush Mercury = Solid(255, 0xFF, 0x5A, 0x4A);
        public static readonly IBrush ColdColumn = Solid(255, 0x6F, 0xB8, 0xFF);

        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x1A, 0x22, 0x32)), new ImmutableGradientStop(1, Color.FromRgb(0x06, 0x09, 0x10))],
            center: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.8, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.8, RelativeUnit.Relative));

        public static readonly IBrush CrankcaseInside = Solid(255, 0x1A, 0x1E, 0x26);
        public static readonly IBrush CrankPin = Solid(255, 0xB0, 0xB8, 0xC4);
        public static readonly IPen Liner = MakePen(Solid(255, 0xA0, 0xA8, 0xB4), 1.6);
        public static readonly IBrush Cover = Solid(255, 0x3E, 0x44, 0x50);

        public static readonly IBrush Floor = SpaceDrawing.LinearGradient(Color.FromRgb(0x4A, 0x3C, 0x30), Color.FromRgb(0x2A, 0x22, 0x1C), 90);
        public static readonly IPen FloorEdge = MakePen(Solid(120, 0xEE, 0xF1, 0xF6), 1);
        public static readonly IBrush BackWall = Solid(60, 0x00, 0x00, 0x00);
        public static readonly IBrush Socket = Solid(255, 0xE8, 0xEC, 0xF2);
        public static readonly IBrush Insulation = SpaceDrawing.LinearGradient(Color.FromRgb(0xE6, 0xDC, 0xB8), Color.FromRgb(0xC8, 0xBC, 0x92), 0);
        public static readonly IBrush Frost = Solid(70, 0xE8, 0xF4, 0xFF);
        public static readonly IPen Shelf = MakePen(Solid(170, 0xC8, 0xE4, 0xFF), 2);
        public static readonly IBrush Door = SpaceDrawing.LinearGradient(Color.FromRgb(0xE4, 0xE8, 0xEE), Color.FromRgb(0xB8, 0xC0, 0xCA), 0);
        public static readonly IBrush DoorAjar = SpaceDrawing.LinearGradient(Color.FromArgb(220, 0xE4, 0xE8, 0xEE), Color.FromArgb(220, 0xB8, 0xC0, 0xCA), 0);
        public static readonly IBrush Handle = Solid(255, 0x70, 0x78, 0x86);
        public static readonly IPen Seal = MakePen(Solid(255, 0x5A, 0x62, 0x6E), 2);
        public static readonly IPen Grid2 = MakePen(Solid(90, 0xC9, 0xD1, 0xDC), 1);
        public static readonly IPen Cable = MakePen(Solid(255, 0x24, 0x28, 0x30), 3);
        public static readonly IBrush Carton = Solid(255, 0xF2, 0xF4, 0xF8);
        public static readonly IBrush CartonBand = Solid(255, 0x5A, 0xA8, 0xFF);
        public static readonly IBrush Apple = SpaceDrawing.RadialGradient((Color.FromRgb(0xFF, 0x7A, 0x6A), 0), (Color.FromRgb(0xB8, 0x2A, 0x2A), 1));
        public static readonly IBrush Bowl = Solid(255, 0xD8, 0xC0, 0x8A);
        public static readonly IBrush Box = Solid(255, 0x8B, 0xC8, 0x8F);
        public static readonly IBrush Box2 = Solid(255, 0xE8, 0xC0, 0x6A);
        public static readonly IBrush Bottle = Solid(200, 0x6A, 0xC8, 0x8A);
        public static readonly IBrush Compressor = SpaceDrawing.LinearGradient(Color.FromRgb(0x3A, 0x40, 0x4C), Color.FromRgb(0x10, 0x12, 0x18), 0);
        public static readonly IBrush CompressorFoot = Solid(255, 0x2A, 0x2E, 0x36);

        public static readonly IPen Axis = MakePen(Solid(130, 0xC9, 0xD1, 0xDC), 1.2);
        public static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
        public static readonly IPen Frame = MakePen(Solid(60, 255, 255, 255), 1);
    }
}
