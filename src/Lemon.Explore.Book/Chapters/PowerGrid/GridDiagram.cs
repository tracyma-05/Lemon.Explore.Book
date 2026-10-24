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

namespace Lemon.Explore.Book.Chapters.PowerGrid;

/// <summary>
/// The animated diagram of the generator-and-grid page. Generator mode: a coil turning between a magnet's poles lighting
/// a lamp, or a power station's magnet turning inside three coils (<c>GridDiagram.Generator.cs</c>). Transmission mode: a
/// power station, a step-up transformer, a long line and a town, with a transformer cut open
/// (<c>GridDiagram.Transmission.cs</c>). Balance mode: a regional grid's stations, its frequency and its city through a
/// day, and what happens when something trips (<c>GridDiagram.Balance.cs</c>).
/// </summary>
/// <remarks>
/// The diagram owns the clock and pushes back <see cref="Angle"/> (the coil), <see cref="Hour"/> (the day) and
/// <see cref="EventTime"/> (seconds after a trip) as it runs; dragging sideways turns the coil or moves through the day.
/// Clicking the lamp switches it, clicking the transformer's input swaps AC and DC; the mouse and the clickable things
/// are in <c>GridDiagram.Objects.cs</c>.
/// </remarks>
public sealed partial class GridDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
        else if (change.Property == KindProperty)
        {
            ClearScope();
        }
        else if (change.Property == AngleProperty)
        {
            OnAngleChanged(change.GetNewValue<double>());
        }
        else if (change.Property == TurnsProperty)
        {
            ClearScope();
        }
        else if (change.Property == FieldProperty)
        {
            ClearScope();
        }
        else if (change.Property == PolePairsProperty)
        {
            ClearScope();
        }
        else if (change.Property == HourProperty)
        {
            OnHourChanged(change.GetNewValue<double>());
        }
        else if (change.Property == EventTimeProperty)
        {
            OnEventTimeChanged(change.GetNewValue<double>());
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    /// <summary>Balance: hours of the day per second of playback at 1× (a day in a minute).</summary>
    private const double HoursPerSecond = 0.4;

    /// <summary>Balance: an event's first seconds play in real time, the slow correction after them this much faster.</summary>
    private const double EventRealTime = 20;

    private const double EventFastForward = 12;

    #region dependency properties

    public static readonly StyledProperty<GridMode> ModeProperty =
        AvaloniaProperty.Register<GridDiagram, GridMode>(nameof(Mode), GridMode.Generator);

    public static readonly StyledProperty<GeneratorKind> KindProperty =
        AvaloniaProperty.Register<GridDiagram, GeneratorKind>(nameof(Kind), GeneratorKind.Ac);

    /// <summary>Generator: the coil's (or the magnet's) angle, 0…360° from facing the field (pushed back as it turns).</summary>
    public static readonly StyledProperty<double> AngleProperty =
        AvaloniaProperty.Register<GridDiagram, double>(nameof(Angle), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Generator: turns per second while playing (the real machine's speed, before the playback speed).</summary>
    public static readonly StyledProperty<double> CoilSpeedProperty =
        AvaloniaProperty.Register<GridDiagram, double>(nameof(CoilSpeed), GeneratorModel.DefaultSpeed);

    public static readonly StyledProperty<int> TurnsProperty =
        AvaloniaProperty.Register<GridDiagram, int>(nameof(Turns), GeneratorModel.DefaultTurns);

    /// <summary>Generator: the magnet's field, T.</summary>
    public static readonly StyledProperty<double> FieldProperty =
        AvaloniaProperty.Register<GridDiagram, double>(nameof(Field), GeneratorModel.DefaultField);

    /// <summary>Generator: the lamp's switch is closed (clicking the lamp toggles this).</summary>
    public static readonly StyledProperty<bool> LampOnProperty =
        AvaloniaProperty.Register<GridDiagram, bool>(nameof(LampOn), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Generator, three-phase: the magnet's pairs of poles (1 or 2).</summary>
    public static readonly StyledProperty<int> PolePairsProperty =
        AvaloniaProperty.Register<GridDiagram, int>(nameof(PolePairs), 1);

    /// <summary>Transmission: the line's voltage, kV.</summary>
    public static readonly StyledProperty<double> LineKvProperty =
        AvaloniaProperty.Register<GridDiagram, double>(nameof(LineKv), TransmissionModel.DefaultKv);

    /// <summary>Transmission: the power sent, MW.</summary>
    public static readonly StyledProperty<double> PowerProperty =
        AvaloniaProperty.Register<GridDiagram, double>(nameof(Power), TransmissionModel.DefaultPower);

    /// <summary>Transmission: how far it is sent, km.</summary>
    public static readonly StyledProperty<double> DistanceProperty =
        AvaloniaProperty.Register<GridDiagram, double>(nameof(Distance), TransmissionModel.DefaultDistance);

    /// <summary>Transmission: the transformer is fed steady direct current (clicking its input coil toggles this).</summary>
    public static readonly StyledProperty<bool> FeedDcProperty =
        AvaloniaProperty.Register<GridDiagram, bool>(nameof(FeedDc), false, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<GridSeason> SeasonProperty =
        AvaloniaProperty.Register<GridDiagram, GridSeason>(nameof(Season), GridSeason.Summer);

    /// <summary>Balance: the hour of the day, local solar time, 0…24 (pushed back as the day runs).</summary>
    public static readonly StyledProperty<double> HourProperty =
        AvaloniaProperty.Register<GridDiagram, double>(nameof(Hour), 12.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Balance: the solar farms' capacity, GW.</summary>
    public static readonly StyledProperty<double> SolarCapacityProperty =
        AvaloniaProperty.Register<GridDiagram, double>(nameof(SolarCapacity), GridDayModel.DefaultSolar);

    /// <summary>Balance: the viewer's latitude, for the Sun's height.</summary>
    public static readonly StyledProperty<double> LatitudeProperty =
        AvaloniaProperty.Register<GridDiagram, double>(nameof(Latitude), 28.2);

    /// <summary>Balance: the event being followed, or <see cref="GridEvent.None"/>.</summary>
    public static readonly StyledProperty<GridEvent> EventKindProperty =
        AvaloniaProperty.Register<GridDiagram, GridEvent>(nameof(EventKind), GridEvent.None);

    /// <summary>Balance: the frequency after the event, from <see cref="FrequencyModel.Simulate"/>.</summary>
    public static readonly StyledProperty<IReadOnlyList<FrequencyPoint>?> EventTraceProperty =
        AvaloniaProperty.Register<GridDiagram, IReadOnlyList<FrequencyPoint>?>(nameof(EventTrace));

    /// <summary>Balance: seconds since the event (pushed back as it plays).</summary>
    public static readonly StyledProperty<double> EventTimeProperty =
        AvaloniaProperty.Register<GridDiagram, double>(nameof(EventTime), 0.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<GridDiagram, bool>(nameof(ShowLabels), true);

    /// <summary>Playback speed: 1 is the normal pace.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<GridDiagram, double>(nameof(Speed), 1.0);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<GridDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<GridDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<GridDiagram>(ModeProperty, KindProperty, AngleProperty, CoilSpeedProperty, TurnsProperty, FieldProperty, LampOnProperty, PolePairsProperty, LineKvProperty, PowerProperty, DistanceProperty, FeedDcProperty, SeasonProperty, HourProperty, SolarCapacityProperty, LatitudeProperty, EventKindProperty, EventTraceProperty, EventTimeProperty, ShowLabelsProperty, SpeedProperty, ToolbarHeightProperty);
        return true;
    }


    public GridMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public GeneratorKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public double Angle
    {
        get => GetValue(AngleProperty);
        set => SetValue(AngleProperty, value);
    }

    public double CoilSpeed
    {
        get => GetValue(CoilSpeedProperty);
        set => SetValue(CoilSpeedProperty, value);
    }

    public int Turns
    {
        get => GetValue(TurnsProperty);
        set => SetValue(TurnsProperty, value);
    }

    public double Field
    {
        get => GetValue(FieldProperty);
        set => SetValue(FieldProperty, value);
    }

    public bool LampOn
    {
        get => GetValue(LampOnProperty);
        set => SetValue(LampOnProperty, value);
    }

    public int PolePairs
    {
        get => GetValue(PolePairsProperty);
        set => SetValue(PolePairsProperty, value);
    }

    public double LineKv
    {
        get => GetValue(LineKvProperty);
        set => SetValue(LineKvProperty, value);
    }

    public double Power
    {
        get => GetValue(PowerProperty);
        set => SetValue(PowerProperty, value);
    }

    public double Distance
    {
        get => GetValue(DistanceProperty);
        set => SetValue(DistanceProperty, value);
    }

    public bool FeedDc
    {
        get => GetValue(FeedDcProperty);
        set => SetValue(FeedDcProperty, value);
    }

    public GridSeason Season
    {
        get => GetValue(SeasonProperty);
        set => SetValue(SeasonProperty, value);
    }

    public double Hour
    {
        get => GetValue(HourProperty);
        set => SetValue(HourProperty, value);
    }

    public double SolarCapacity
    {
        get => GetValue(SolarCapacityProperty);
        set => SetValue(SolarCapacityProperty, value);
    }

    public double Latitude
    {
        get => GetValue(LatitudeProperty);
        set => SetValue(LatitudeProperty, value);
    }

    public GridEvent EventKind
    {
        get => GetValue(EventKindProperty);
        set => SetValue(EventKindProperty, value);
    }

    public IReadOnlyList<FrequencyPoint>? EventTrace
    {
        get => GetValue(EventTraceProperty);
        set => SetValue(EventTraceProperty, value);
    }

    public double EventTime
    {
        get => GetValue(EventTimeProperty);
        set => SetValue(EventTimeProperty, value);
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
    private readonly LabelCache _labels;

    private double _lastSeconds;
    private bool _pushing;

    /// <summary>Animation seconds (scaled by the speed, stopped while paused): flowing current, heat, blades, flux.</summary>
    private double _t;

    /// <summary>The clocks of the modes, kept here while they run and pushed back to the page.</summary>
    private double _angle;

    private double _hour = 12;
    private double _eventTime;

    /// <summary>Generator: how fast the coil really turned this frame, turns per second (a drag counts too).</summary>
    private double _turnRate;

    private double _lastAngle;

    private bool _dragging;
    private Point _dragStart;
    private double _dragStartValue;
    private bool _dragMoved;

    public GridDiagram()
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
        _dragging = false;
        ClearScope();
    }

    private void OnAngleChanged(double value)
    {
        if (!_pushing) _angle = Wrap360(value);
    }

    private void OnHourChanged(double value)
    {
        if (!_pushing) _hour = GridDayModel.Wrap(value);
    }

    private void OnEventTimeChanged(double value)
    {
        if (!_pushing) _eventTime = Math.Clamp(value, 0, FrequencyModel.Duration);
    }

    /// <summary>Whether an event is being followed and has not yet played to its end.</summary>
    private bool EventRunning => EventKind != GridEvent.None && EventTrace is { Count: > 0 } && _eventTime < FrequencyModel.Duration;

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);

        // A walkthrough holds the hour or the coil it talks about, but current keeps flowing and blades keep turning.
        var speed = Math.Clamp(Speed, 0.05, 8);
        var step = IsPlaying && !_dragging ? dt * speed : 0;
        _t += (IsPlaying || IsLessonActive) && !_dragging ? dt * speed : 0;
        switch (Mode)
        {
            case GridMode.Generator:
                if (step > 0)
                {
                    _angle = Wrap360(_angle + (step * 360 * CoilSpeed));
                    Push(AngleProperty, Math.Round(_angle, 3));
                }

                // How fast it really turned (by hand too): the scope and the lamp follow this, not the setting.
                var moved = Wrap360(_angle - _lastAngle + 180) - 180;
                var physical = _dragging ? dt : dt * speed;
                var rate = physical > 0 ? moved / 360 / physical : 0;
                _turnRate = dt > 0 ? Mix(_turnRate, rate, Math.Min(1, dt * 12)) : _turnRate;
                _lastAngle = _angle;
                if (step > 0 || _dragging)
                {
                    RecordScope(physical > 0 ? physical : dt);
                }
                else
                {
                    // Paused: a still picture of the coil turning at its set speed (the scope holds what it had).
                    _turnRate = CoilSpeed;
                    Sample(CoilSpeed);
                }

                break;

            case GridMode.Balance:
                if (step > 0)
                {
                    if (EventRunning)
                    {
                        var rateNow = _eventTime < EventRealTime ? 1 : EventFastForward;
                        _eventTime = Math.Min(FrequencyModel.Duration, _eventTime + (step * rateNow));
                        Push(EventTimeProperty, Math.Round(_eventTime, 3));
                    }
                    else if (EventKind == GridEvent.None || EventTrace is null)
                    {
                        _hour = GridDayModel.Wrap(_hour + (step * HoursPerSecond));
                        Push(HourProperty, Math.Round(_hour, 4));
                    }
                }

                break;
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
            case GridMode.Transmission:
                DrawTransmissionMode(dc, width, height);
                break;

            case GridMode.Balance:
                DrawBalanceMode(dc, width, height);
                break;

            default:
                DrawGeneratorMode(dc, width, height);
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

    /// <summary>A pill that is also a target for <paramref name="id"/>.</summary>
    private Rect DrawTargetPill(PaintContext dc, string text, Point center, Color color, string id, double size = 11.5)
    {
        var box = DrawPill(dc, text, center, color, size);
        AddBoxTarget(id, box);
        return box;
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
        x = Math.Clamp(x, 2, Math.Max(2, ActualWidth - formatted.Width - 14));
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

    /// <summary>A wavy "heat" squiggle rising from <paramref name="from"/>, its waves running up with time.</summary>
    private void DrawHeatSquiggle(PaintContext dc, Point from, double length, Color color, double opacity, double phase)
    {
        if (opacity <= 0.02 || length < 6) return;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            const int steps = 14;
            for (var i = 0; i <= steps; i++)
            {
                var s = i / (double)steps * length;
                var point = new Point(from.X + (Math.Sin((s / 5) - (_t * 5) + phase) * 2.6), from.Y - s);
                if (i == 0) context.BeginFigure(point, false);
                else context.LineTo(point);
            }

            context.EndFigure(false);
        }
        dc.DrawGeometry(null, FadedPen(color, opacity, 1.6), geometry);
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

    /// <summary>The point a fraction <paramref name="s"/> of the way along a polyline.</summary>
    private static Point Along(IReadOnlyList<Point> points, double s)
    {
        if (points.Count == 1) return points[0];
        var total = 0.0;
        for (var i = 1; i < points.Count; i++) total += ((Vector)(points[i] - points[i - 1])).Length;
        var target = Math.Clamp(s, 0, 1) * total;
        for (var i = 1; i < points.Count; i++)
        {
            var length = ((Vector)(points[i] - points[i - 1])).Length;
            if (target <= length || i == points.Count - 1) return Mix(points[i - 1], points[i], length < 1e-9 ? 0 : Math.Min(1, target / length));
            target -= length;
        }

        return points[^1];
    }

    /// <summary>The fractional part, for looping animations.</summary>
    private static double Frac(double value) => value - Math.Floor(value);

    private static double Wrap360(double degrees) => ((degrees % 360) + 360) % 360;

    private static double Mix(double a, double b, double t) => a + ((b - a) * t);

    private static Point Mix(Point a, Point b, double t) => new(Mix(a.X, b.X, t), Mix(a.Y, b.Y, t));

    private static double Smooth(double a, double b, double value)
    {
        var t = Math.Clamp((value - a) / (b - a), 0, 1);
        return t * t * (3 - (2 * t));
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
        public static readonly Color Green = Color.FromRgb(0x8B, 0xE0, 0x8F);
        public static readonly Color Orange = Color.FromRgb(0xFF, 0x9F, 0x5A);
        public static readonly Color Red = Color.FromRgb(0xFF, 0x6A, 0x5A);
        public static readonly Color Blue = Color.FromRgb(0x5A, 0xA8, 0xFF);
        public static readonly Color Violet = Color.FromRgb(0xB3, 0x9D, 0xFF);
        public static readonly Color Copper = Color.FromRgb(0xE0, 0x8A, 0x4A);
        public static readonly Color CopperLight = Color.FromRgb(0xFF, 0xB8, 0x7A);
        public static readonly Color Field = Color.FromRgb(0x9F, 0xC8, 0xFF);

        /// <summary>The three phases in China's colours: A yellow, B green, C red.</summary>
        public static readonly Color[] Phases =
        [
            Color.FromRgb(0xFF, 0xD3, 0x4A),
            Color.FromRgb(0x5A, 0xD8, 0x7A),
            Color.FromRgb(0xFF, 0x5A, 0x5A),
        ];

        public static readonly IBrush NorthPole = SpaceDrawing.LinearGradient(Color.FromRgb(0xE8, 0x4A, 0x4A), Color.FromRgb(0x9A, 0x22, 0x26), 90);
        public static readonly IBrush NorthTop = Solid(255, 0xF2, 0x7A, 0x72);
        public static readonly IBrush NorthSide = Solid(255, 0x7A, 0x1A, 0x1E);
        public static readonly IBrush SouthPole = SpaceDrawing.LinearGradient(Color.FromRgb(0x4A, 0x7E, 0xE8), Color.FromRgb(0x22, 0x40, 0x9A), 90);
        public static readonly IBrush SouthTop = Solid(255, 0x7A, 0xA4, 0xF2);
        public static readonly IBrush SouthSide = Solid(255, 0x1A, 0x30, 0x7A);
        public static readonly IBrush Metal = SpaceDrawing.LinearGradient(Color.FromRgb(0xB8, 0xC0, 0xCC), Color.FromRgb(0x6A, 0x72, 0x80), 90);
        public static readonly IBrush MetalDark = SpaceDrawing.LinearGradient(Color.FromRgb(0x6A, 0x72, 0x80), Color.FromRgb(0x34, 0x3A, 0x44), 90);
        public static readonly IBrush Iron = SpaceDrawing.LinearGradient(Color.FromRgb(0x6E, 0x76, 0x84), Color.FromRgb(0x44, 0x4A, 0x56), 0);
        public static readonly IBrush Brass = SpaceDrawing.LinearGradient(Color.FromRgb(0xF0, 0xC8, 0x6A), Color.FromRgb(0xA8, 0x7A, 0x2A), 90);
        public static readonly IBrush Carbon = Solid(255, 0x2E, 0x30, 0x36);
        public static readonly IPen Edge = MakePen(Solid(200, 0x10, 0x14, 0x1C), 1.2);
        public static readonly IPen LightEdge = MakePen(Solid(120, 0xEE, 0xF1, 0xF6), 1);
        public static readonly IPen Wire = MakePen(Solid(255, 0x30, 0x34, 0x3C), 3.4);
        public static readonly IPen WireCore = MakePen(Solid(255, 0xC8, 0x7A, 0x3A), 1.6);
        public static readonly IBrush ScopeScreen = Solid(255, 0x06, 0x14, 0x10);
        public static readonly IBrush ScopeBody = SpaceDrawing.LinearGradient(Color.FromRgb(0x3A, 0x40, 0x4C), Color.FromRgb(0x22, 0x26, 0x2E), 90);
        public static readonly IPen ScopeGrid = MakePen(Solid(40, 0x6A, 0xFF, 0xB0), 1);
        public static readonly IPen ScopeAxis = MakePen(Solid(90, 0x6A, 0xFF, 0xB0), 1);
        public static readonly IBrush Glass = Solid(60, 0xEE, 0xF1, 0xF6);
        public static readonly IPen GlassEdge = MakePen(Solid(170, 0xEE, 0xF1, 0xF6), 1.2);

        public static readonly IPen Leader = MakePen(Solid(150, 0xEE, 0xF1, 0xF6), 1);
        public static readonly IBrush LeaderDot = Solid(220, 0xEE, 0xF1, 0xF6);

        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x1A, 0x22, 0x32)), new ImmutableGradientStop(1, Color.FromRgb(0x06, 0x09, 0x10))],
            center: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.8, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.8, RelativeUnit.Relative));

        public static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
        public static readonly IPen Frame = MakePen(Solid(60, 255, 255, 255), 1);
    }
}
