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
using Lemon.Explore.Book.Infrastructures.Timing;

namespace Lemon.Explore.Book.Chapters.Typhoon;

/// <summary>
/// The animated diagram of the typhoon page. Storm mode: a typhoon from above with its winds and pressure
/// (<c>TyphoonDiagram.Storm.cs</c>). Engine mode: a slice through it, warm sea to the top of the clouds
/// (<c>TyphoonDiagram.Engine.cs</c>). Track mode: a map of the north-west Pacific with the storm's path round the
/// subtropical high (<c>TyphoonDiagram.Track.cs</c>).
/// </summary>
/// <remarks>
/// The diagram owns the clock and pushes back <see cref="LandfallHours"/> and <see cref="TrackHour"/> as they run.
/// Storm mode: clicking or dragging moves the probe. Engine mode: dragging sideways turns the hours after landfall.
/// Track mode: dragging sideways turns the storm's hours; a click on the sea starts a new storm there. Clicking a named
/// thing explains it (<c>TyphoonDiagram.Objects.cs</c>).
/// </remarks>
public sealed partial class TyphoonDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
        else if (change.Property == SouthernProperty)
        {
            _particles.Clear();
        }
        else if (change.Property == SpinProperty)
        {
            _particles.Clear();
        }
        else if (change.Property == LandfallProperty)
        {
            OnLandfallChanged();
        }
        else if (change.Property == LandfallHoursProperty)
        {
            OnLandfallHoursChanged(change.GetNewValue<double>());
        }
        else if (change.Property == MonthProperty)
        {
            OnTrackChanged();
        }
        else if (change.Property == RidgeWestProperty)
        {
            OnTrackChanged();
        }
        else if (change.Property == StartLatitudeProperty)
        {
            OnTrackChanged();
        }
        else if (change.Property == StartLongitudeProperty)
        {
            OnTrackChanged();
        }
        else if (change.Property == TrackHourProperty)
        {
            OnTrackHourChanged(change.GetNewValue<double>());
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    /// <summary>Engine: hours after landfall per second of playback at 1×.</summary>
    private const double LandfallHoursPerSecond = 2;

    /// <summary>Track: hours of the storm's life per second of playback at 1×.</summary>
    private const double TrackHoursPerSecond = 6;

    /// <summary>Engine: how long the landfall story runs, hours.</summary>
    public const double LandfallSpan = 48;

    #region dependency properties

    public static readonly StyledProperty<TyphoonMode> ModeProperty =
        AvaloniaProperty.Register<TyphoonDiagram, TyphoonMode>(nameof(Mode), TyphoonMode.Storm);

    /// <summary>Storm: the central pressure, hPa.</summary>
    public static readonly StyledProperty<double> CentralPressureProperty =
        AvaloniaProperty.Register<TyphoonDiagram, double>(nameof(CentralPressure), 950.0);

    /// <summary>Storm: south of the equator the storm turns the other way.</summary>
    public static readonly StyledProperty<bool> SouthernProperty =
        AvaloniaProperty.Register<TyphoonDiagram, bool>(nameof(Southern), false);

    /// <summary>Storm: the Earth's turn (Coriolis) is on; without it the air rushes straight in.</summary>
    public static readonly StyledProperty<bool> SpinProperty =
        AvaloniaProperty.Register<TyphoonDiagram, bool>(nameof(Spin), true);

    /// <summary>Storm: the storm moves north-west at 20 km/h, so its winds are stronger on the right.</summary>
    public static readonly StyledProperty<bool> MovingProperty =
        AvaloniaProperty.Register<TyphoonDiagram, bool>(nameof(Moving), false);

    public static readonly StyledProperty<bool> ShowIsobarsProperty =
        AvaloniaProperty.Register<TyphoonDiagram, bool>(nameof(ShowIsobars), true);

    /// <summary>Storm: the probe's place east of the centre, km (pushed back when dragged).</summary>
    public static readonly StyledProperty<double> ProbeXProperty =
        AvaloniaProperty.Register<TyphoonDiagram, double>(nameof(ProbeX), 160.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Storm: the probe's place north of the centre, km.</summary>
    public static readonly StyledProperty<double> ProbeYProperty =
        AvaloniaProperty.Register<TyphoonDiagram, double>(nameof(ProbeY), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Engine: the sea's surface temperature, °C.</summary>
    public static readonly StyledProperty<double> SeaTemperatureProperty =
        AvaloniaProperty.Register<TyphoonDiagram, double>(nameof(SeaTemperature), 29.0);

    /// <summary>Engine: the storm has come ashore.</summary>
    public static readonly StyledProperty<bool> LandfallProperty =
        AvaloniaProperty.Register<TyphoonDiagram, bool>(nameof(Landfall), false);

    /// <summary>Engine: hours since landfall (pushed back as they run).</summary>
    public static readonly StyledProperty<double> LandfallHoursProperty =
        AvaloniaProperty.Register<TyphoonDiagram, double>(nameof(LandfallHours), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Track: the month (6…10).</summary>
    public static readonly StyledProperty<int> MonthProperty =
        AvaloniaProperty.Register<TyphoonDiagram, int>(nameof(Month), 8);

    /// <summary>Track: the longitude of the subtropical high's west end, °E.</summary>
    public static readonly StyledProperty<double> RidgeWestProperty =
        AvaloniaProperty.Register<TyphoonDiagram, double>(nameof(RidgeWest), 125.0);

    public static readonly StyledProperty<double> StartLatitudeProperty =
        AvaloniaProperty.Register<TyphoonDiagram, double>(nameof(StartLatitude), 13.5, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> StartLongitudeProperty =
        AvaloniaProperty.Register<TyphoonDiagram, double>(nameof(StartLongitude), 144.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Track: hours since the storm formed (pushed back as they run).</summary>
    public static readonly StyledProperty<double> TrackHourProperty =
        AvaloniaProperty.Register<TyphoonDiagram, double>(nameof(TrackHour), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Track: the shared observer location.</summary>
    public static readonly StyledProperty<double> PlaceLatitudeProperty =
        AvaloniaProperty.Register<TyphoonDiagram, double>(nameof(PlaceLatitude), 28.23);

    public static readonly StyledProperty<double> PlaceLongitudeProperty =
        AvaloniaProperty.Register<TyphoonDiagram, double>(nameof(PlaceLongitude), 112.94);

    /// <summary>Track: the observer location's localized name.</summary>
    public static readonly StyledProperty<string> PlaceNameProperty =
        AvaloniaProperty.Register<TyphoonDiagram, string>(nameof(PlaceName), string.Empty);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<TyphoonDiagram, bool>(nameof(ShowLabels), true);

    /// <summary>Playback speed: 1 is the normal pace.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<TyphoonDiagram, double>(nameof(Speed), 1.0);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<TyphoonDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<TyphoonDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<TyphoonDiagram>(ModeProperty, CentralPressureProperty, SouthernProperty, SpinProperty, MovingProperty, ShowIsobarsProperty, ProbeXProperty, ProbeYProperty, SeaTemperatureProperty, LandfallProperty, LandfallHoursProperty, MonthProperty, RidgeWestProperty, StartLatitudeProperty, StartLongitudeProperty, TrackHourProperty, PlaceLatitudeProperty, PlaceLongitudeProperty, PlaceNameProperty, ShowLabelsProperty, SpeedProperty, ToolbarHeightProperty);
        return true;
    }



    public TyphoonMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double CentralPressure
    {
        get => GetValue(CentralPressureProperty);
        set => SetValue(CentralPressureProperty, value);
    }

    public bool Southern
    {
        get => GetValue(SouthernProperty);
        set => SetValue(SouthernProperty, value);
    }

    public bool Spin
    {
        get => GetValue(SpinProperty);
        set => SetValue(SpinProperty, value);
    }

    public bool Moving
    {
        get => GetValue(MovingProperty);
        set => SetValue(MovingProperty, value);
    }

    public bool ShowIsobars
    {
        get => GetValue(ShowIsobarsProperty);
        set => SetValue(ShowIsobarsProperty, value);
    }

    public double ProbeX
    {
        get => GetValue(ProbeXProperty);
        set => SetValue(ProbeXProperty, value);
    }

    public double ProbeY
    {
        get => GetValue(ProbeYProperty);
        set => SetValue(ProbeYProperty, value);
    }

    public double SeaTemperature
    {
        get => GetValue(SeaTemperatureProperty);
        set => SetValue(SeaTemperatureProperty, value);
    }

    public bool Landfall
    {
        get => GetValue(LandfallProperty);
        set => SetValue(LandfallProperty, value);
    }

    public double LandfallHours
    {
        get => GetValue(LandfallHoursProperty);
        set => SetValue(LandfallHoursProperty, value);
    }

    public int Month
    {
        get => GetValue(MonthProperty);
        set => SetValue(MonthProperty, value);
    }

    public double RidgeWest
    {
        get => GetValue(RidgeWestProperty);
        set => SetValue(RidgeWestProperty, value);
    }

    public double StartLatitude
    {
        get => GetValue(StartLatitudeProperty);
        set => SetValue(StartLatitudeProperty, value);
    }

    public double StartLongitude
    {
        get => GetValue(StartLongitudeProperty);
        set => SetValue(StartLongitudeProperty, value);
    }

    public double TrackHour
    {
        get => GetValue(TrackHourProperty);
        set => SetValue(TrackHourProperty, value);
    }

    public double PlaceLatitude
    {
        get => GetValue(PlaceLatitudeProperty);
        set => SetValue(PlaceLatitudeProperty, value);
    }

    public double PlaceLongitude
    {
        get => GetValue(PlaceLongitudeProperty);
        set => SetValue(PlaceLongitudeProperty, value);
    }

    public string PlaceName
    {
        get => GetValue(PlaceNameProperty);
        set => SetValue(PlaceNameProperty, value);
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
    private readonly Random _random = new(20261009);
    private readonly StormCloud _cloud = new();

    private double _lastSeconds;
    private bool _pushing;

    /// <summary>Animation seconds (scaled by the speed, stopped while paused): spinning cloud, particles, rain.</summary>
    private double _t;

    /// <summary>The clocks of the engine and track modes, kept here while they run and pushed back to the page.</summary>
    private double _landfallHours;

    private double _trackHour;

    /// <summary>Track: playback seconds spent holding the finished track before it starts again.</summary>
    private double _trackRest;

    private bool _dragging;
    private Point _dragStart;
    private double _dragStartValue;
    private bool _dragMoved;

    public TyphoonDiagram()
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
        _mapBitmap = null;
        _seaBitmap = null;
    }

    private void OnModeChanged()
    {
        _dragging = false;
        _pressed = null;
        _targets.Hover = null;
        _particles.Clear();
        _parcels.Clear();
    }

    private void OnLandfallChanged()
    {
        _landfallHours = 0;
        Push(LandfallHoursProperty, 0.0);
    }

    private void OnLandfallHoursChanged(double value)
    {
        if (!_pushing) _landfallHours = Math.Clamp(value, 0, LandfallSpan);
    }

    private void OnTrackChanged()
    {
        _seaBitmap = null;
        _trackHour = 0;
        _trackRest = 0;
        Push(TrackHourProperty, 0.0);
    }

    private void OnTrackHourChanged(double value)
    {
        if (!_pushing) _trackHour = Math.Max(0, value);
    }

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);

        // The air, cloud and rain keep moving during a walkthrough, which holds the hours it talks about.
        var pace = dt * Math.Clamp(Speed, 0.05, 8);
        var step = IsPlaying && !_dragging ? pace : 0;
        var airStep = (IsPlaying || IsLessonActive) && !_dragging ? pace : 0;
        _t += airStep;
        switch (Mode)
        {
            case TyphoonMode.Storm:
                StepStorm(airStep);
                break;

            case TyphoonMode.Engine:
                if (step > 0 && Landfall && _landfallHours < LandfallSpan)
                {
                    _landfallHours = Math.Min(LandfallSpan, _landfallHours + (step * LandfallHoursPerSecond));
                    Push(LandfallHoursProperty, Math.Round(_landfallHours, 3));
                }

                StepEngine(airStep);
                break;

            default:
                if (step > 0) StepTrackClock(step);
                break;
        }

        InvalidateVisual();
    }

    /// <summary>The track runs to its end, rests a moment, then the storm starts over.</summary>
    private void StepTrackClock(double step)
    {
        var end = CurrentTrack[^1].Hour;
        if (_trackHour >= end)
        {
            _trackRest += step;
            if (_trackRest < 2.5) return;
            _trackRest = 0;
            _trackHour = 0;
        }
        else
        {
            _trackHour = Math.Min(end, _trackHour + (step * TrackHoursPerSecond));
        }

        Push(TrackHourProperty, Math.Round(_trackHour, 3));
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
            case TyphoonMode.Engine:
                DrawEngineMode(dc, width, height);
                break;

            case TyphoonMode.Track:
                DrawTrackMode(dc, width, height);
                break;

            default:
                DrawStormMode(dc, width, height);
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

    /// <summary>Text with its left edge (or its centre) at a point, its middle at the point's height; returns where it went.</summary>
    private Rect DrawText(PaintContext dc, string text, Point at, Color color, double size = 11, bool center = false, bool bold = false)
    {
        var formatted = _labels.Text(text, size, color, bold);
        var origin = new Point(center ? at.X - (formatted.Width / 2) : at.X, at.Y - (formatted.Height / 2));
        dc.DrawText(formatted, origin);
        return new Rect(origin, new Size(formatted.Width, formatted.Height));
    }

    /// <summary>Text whose right edge is at <paramref name="at"/>, its middle at the point's height; returns where it went.</summary>
    private Rect DrawTextRight(PaintContext dc, string text, Point at, Color color, double size = 11, bool bold = false)
    {
        var formatted = _labels.Text(text, size, color, bold);
        var origin = new Point(at.X - formatted.Width, at.Y - (formatted.Height / 2));
        dc.DrawText(formatted, origin);
        return new Rect(origin, new Size(formatted.Width, formatted.Height));
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

    /// <summary>A closed shape whose every edge is stroked (<see cref="SpaceDrawing.Polygon"/> strokes only the closing one).</summary>
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

    /// <summary>
    /// Many short strokes of a few colours drawn as one geometry per pen, so hundreds of particles cost a few draw calls
    /// (alpha rounded to tenths).
    /// </summary>
    private sealed class LineBatch
    {
        private readonly Dictionary<(Color Color, int Alpha), List<Point[]>> _lines = [];

        public void Add(Color color, double alpha, Point[] points)
        {
            if (alpha <= 0.04 || points.Length < 2) return;
            var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 10));
            if (!_lines.TryGetValue(key, out var list)) _lines[key] = list = [];
            list.Add(points);
        }

        public void Draw(PaintContext dc, TyphoonDiagram owner, double thickness)
        {
            foreach (var ((color, alpha), list) in _lines)
            {
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    foreach (var points in list)
                    {
                        context.BeginFigure(points[0], false);
                        for (var i = 1; i < points.Length; i++) context.LineTo(points[i]);
                        context.EndFigure(false);
                    }
                }
                dc.DrawGeometry(null, owner.FadedPen(color, alpha / 10.0, thickness, flat: true), geometry);
            }

            _lines.Clear();
        }
    }

    private readonly LineBatch _batch = new();

    /// <summary>The colour of a storm grade (as on typhoon maps: blue, green, yellow, orange, red, magenta).</summary>
    internal static Color GradeColor(StormGrade grade) => grade switch
    {
        StormGrade.Depression => Color.FromRgb(0x6F, 0xB8, 0xFF),
        StormGrade.TropicalStorm => Color.FromRgb(0x6F, 0xE0, 0x8A),
        StormGrade.SevereTropicalStorm => Color.FromRgb(0xFF, 0xE0, 0x5A),
        StormGrade.Typhoon => Color.FromRgb(0xFF, 0xA0, 0x4A),
        StormGrade.SevereTyphoon => Color.FromRgb(0xFF, 0x5A, 0x5A),
        StormGrade.SuperTyphoon => Color.FromRgb(0xE0, 0x6A, 0xFF),
        _ => Color.FromRgb(0x9A, 0xA5, 0xB5),
    };

    /// <summary>The colour of a wind speed, by its grade.</summary>
    internal static Color WindColor(double speed) => GradeColor(StormModel.Grade(speed));

    /// <summary>The fractional part, for looping animations.</summary>
    private static double Frac(double value) => value - Math.Floor(value);

    private static double Smooth(double a, double b, double value) => StormModel.Smooth(a, b, value);

    private static double Mix(double a, double b, double t) => a + ((b - a) * t);

    private static Point Mix(Point a, Point b, double t) => new(Mix(a.X, b.X, t), Mix(a.Y, b.Y, t));

    /// <summary>A hash in 0…1 for the n-th scattered thing, so it stays put from frame to frame.</summary>
    private static double Hash(int n, double salt) => Frac(Math.Sin((n * 12.9898) + (salt * 78.233)) * 43758.5453);

    internal static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    /// <summary>A grade's localized name.</summary>
    internal static string GradeName(StormGrade grade) => Loc($"Typhoon_Grade_{grade}");

    /// <summary>A Beaufort force as words, e.g. 12 级 or "force 12"; beyond 17 as "above 17".</summary>
    internal static string ForceText(int force) => force > 17 ? Loc("Typhoon_Force_Above") : LocFormat("Typhoon_Force", force);

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
        public static readonly Color White = Color.FromRgb(0xFF, 0xFF, 0xFF);
        public static readonly Color Violet = Color.FromRgb(0xB3, 0x9D, 0xFF);

        public static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
        public static readonly IPen Frame = MakePen(Solid(60, 255, 255, 255), 1);
        public static readonly IPen Axis = MakePen(Solid(130, 0xC9, 0xD1, 0xDC), 1.2);

        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x1A, 0x22, 0x32)), new ImmutableGradientStop(1, Color.FromRgb(0x06, 0x09, 0x10))],
            center: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.8, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.8, RelativeUnit.Relative));
    }
}
