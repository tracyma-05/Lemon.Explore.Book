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

namespace Lemon.Explore.Book.Chapters.StarTrails;

/// <summary>
/// The animated diagram of the star-trails page. Camera mode: a long exposure of the sky from the viewer's latitude, the
/// stars drawn out into arcs round the celestial pole (<c>TrailsDiagram.Camera.cs</c>). Sphere mode: the celestial sphere
/// round an observer, its daily circles tilted by the latitude (<c>TrailsDiagram.Sphere.cs</c>). Year mode: Earth going
/// round the Sun, the evening sky at 21:00 sliding west through the year, and the sidereal day
/// (<c>TrailsDiagram.Year.cs</c>).
/// </summary>
/// <remarks>
/// The diagram owns the clocks and pushes back <see cref="Exposure"/> (camera), <see cref="SphereTime"/> (sphere) and
/// <see cref="Day"/> (year) as they run. Dragging turns the camera, the sphere, or moves Earth round its orbit; the mouse
/// and the clickable things are in <c>TrailsDiagram.Objects.cs</c>.
/// </remarks>
public sealed partial class TrailsDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
        else if (change.Property == DayProperty)
        {
            OnDayChanged(change.GetNewValue<double>());
        }
        else if (change.Property == ExposureProperty)
        {
            OnExposureChanged(change.GetNewValue<double>());
        }
        else if (change.Property == SphereTimeProperty)
        {
            OnSphereTimeChanged(change.GetNewValue<double>());
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    /// <summary>Camera: hours of exposure per second of playback at 1×.</summary>
    private const double ExposureHoursPerSecond = 0.7;

    /// <summary>The longest exposure, hours (a long winter night).</summary>
    public const double MaxExposure = 12;

    /// <summary>Sphere: sidereal hours per second at 1× (a turn in 12 s).</summary>
    private const double SphereHoursPerSecond = 2;

    /// <summary>Year: days per second at 1×.</summary>
    private const double DaysPerSecond = 10;

    #region dependency properties

    public static readonly StyledProperty<TrailsMode> ModeProperty =
        AvaloniaProperty.Register<TrailsDiagram, TrailsMode>(nameof(Mode), TrailsMode.Camera);

    /// <summary>The observer's latitude, degrees.</summary>
    public static readonly StyledProperty<double> LatitudeProperty =
        AvaloniaProperty.Register<TrailsDiagram, double>(nameof(Latitude), 28.2);

    /// <summary>The day of the year (1 = 1 January); the year view runs it.</summary>
    public static readonly StyledProperty<double> DayProperty =
        AvaloniaProperty.Register<TrailsDiagram, double>(nameof(Day), 1.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Camera: when the shutter opened, local time, hours.</summary>
    public static readonly StyledProperty<double> StartHourProperty =
        AvaloniaProperty.Register<TrailsDiagram, double>(nameof(StartHour), 20.0);

    /// <summary>Camera: how long the shutter has been open, hours (pushed back as it runs).</summary>
    public static readonly StyledProperty<double> ExposureProperty =
        AvaloniaProperty.Register<TrailsDiagram, double>(nameof(Exposure), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Camera: the azimuth it points to, degrees from north through east (dragging turns it).</summary>
    public static readonly StyledProperty<double> ViewAzimuthProperty =
        AvaloniaProperty.Register<TrailsDiagram, double>(nameof(ViewAzimuth), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Camera: the altitude it points to, degrees (dragging tilts it).</summary>
    public static readonly StyledProperty<double> ViewAltitudeProperty =
        AvaloniaProperty.Register<TrailsDiagram, double>(nameof(ViewAltitude), 30.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Sphere: the sidereal time, hours (pushed back as it turns).</summary>
    public static readonly StyledProperty<double> SphereTimeProperty =
        AvaloniaProperty.Register<TrailsDiagram, double>(nameof(SphereTime), 0.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> ShowFiguresProperty =
        AvaloniaProperty.Register<TrailsDiagram, bool>(nameof(ShowFigures), true);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<TrailsDiagram, bool>(nameof(ShowLabels), true);

    /// <summary>Playback speed: 1 is the normal pace.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<TrailsDiagram, double>(nameof(Speed), 1.0);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<TrailsDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<TrailsDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<TrailsDiagram>(ModeProperty, LatitudeProperty, DayProperty, StartHourProperty, ExposureProperty, ViewAzimuthProperty, ViewAltitudeProperty, SphereTimeProperty, ShowFiguresProperty, ShowLabelsProperty, SpeedProperty, ToolbarHeightProperty);
        return true;
    }


    public TrailsMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double Latitude
    {
        get => GetValue(LatitudeProperty);
        set => SetValue(LatitudeProperty, value);
    }

    public double Day
    {
        get => GetValue(DayProperty);
        set => SetValue(DayProperty, value);
    }

    public double StartHour
    {
        get => GetValue(StartHourProperty);
        set => SetValue(StartHourProperty, value);
    }

    public double Exposure
    {
        get => GetValue(ExposureProperty);
        set => SetValue(ExposureProperty, value);
    }

    public double ViewAzimuth
    {
        get => GetValue(ViewAzimuthProperty);
        set => SetValue(ViewAzimuthProperty, value);
    }

    public double ViewAltitude
    {
        get => GetValue(ViewAltitudeProperty);
        set => SetValue(ViewAltitudeProperty, value);
    }

    public double SphereTime
    {
        get => GetValue(SphereTimeProperty);
        set => SetValue(SphereTimeProperty, value);
    }

    public bool ShowFigures
    {
        get => GetValue(ShowFiguresProperty);
        set => SetValue(ShowFiguresProperty, value);
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

    /// <summary>Animation seconds (scaled by the speed, stopped while paused): twinkling, the sidereal-day loop.</summary>
    private double _t;

    /// <summary>The clocks of the modes, kept here while they run and pushed back to the page.</summary>
    private double _exposure;

    private double _sphereTime;
    private double _day = 1;

    private bool _dragging;
    private Point _dragStart;
    private (double A, double B) _dragStartValue;
    private bool _dragMoved;

    public TrailsDiagram()
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

    private void OnModeChanged() => _dragging = false;

    private void OnDayChanged(double value)
    {
        if (!_pushing) _day = WrapDay(value);
    }

    private void OnExposureChanged(double value)
    {
        if (!_pushing) _exposure = Math.Clamp(value, 0, MaxExposure);
    }

    private void OnSphereTimeChanged(double value)
    {
        if (!_pushing) _sphereTime = SkyModel.Wrap24(value);
    }

    /// <summary>A day of the year wrapped into 1 … 366.</summary>
    internal static double WrapDay(double day) => ((((day - 1) % 365.25) + 365.25) % 365.25) + 1;

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);

        var speed = Math.Clamp(Speed, 0.05, 8);
        var step = IsPlaying && !_dragging ? dt * speed : 0;
        _t += (IsPlaying || IsLessonActive) && !_dragging ? dt * speed : 0;
        if (step > 0)
        {
            switch (Mode)
            {
                case TrailsMode.Camera:
                    _exposure = Math.Min(MaxExposure, _exposure + (step * ExposureHoursPerSecond));
                    Push(ExposureProperty, Math.Round(_exposure, 4));

                    // A whole night gone: the shutter closes.
                    if (_exposure >= MaxExposure) SetCurrentValue(IsPlayingProperty, false);
                    break;

                case TrailsMode.Sphere:
                    _sphereTime = SkyModel.Wrap24(_sphereTime + (step * SphereHoursPerSecond));
                    Push(SphereTimeProperty, Math.Round(_sphereTime, 4));
                    break;

                case TrailsMode.Year:
                    _day = WrapDay(_day + (step * DaysPerSecond));
                    Push(DayProperty, Math.Round(_day, 3));
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
            case TrailsMode.Sphere:
                DrawSphereMode(dc, width, height);
                break;

            case TrailsMode.Year:
                DrawYearMode(dc, width, height);
                break;

            default:
                DrawCameraMode(dc, width, height);
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

    /// <summary>An arrow from <paramref name="from"/> to <paramref name="to"/>.</summary>
    private static void DrawArrow(PaintContext dc, Point from, Point to, IBrush brush, double thickness = 2, double head = 9)
    {
        Vector v = to - from;
        if (v.Length < 3) return;
        var direction = v / v.Length;
        dc.DrawLine(MakePen(brush, thickness), from, to - (direction * head * 0.6));
        DrawArrowHead(dc, brush, to, direction, head);
    }

    private readonly Dictionary<(Color Color, int Alpha, double Thickness, bool Dashed), IPen> _fadedPens = [];
    private readonly Dictionary<(Color Color, int Alpha), IBrush> _fadedBrushes = [];

    /// <summary>A frozen round-capped pen of <paramref name="color"/> at <paramref name="alpha"/> (rounded to twentieths), made once and kept.</summary>
    private IPen FadedPen(Color color, double alpha, double thickness, bool dashed = false)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20), Math.Round(thickness, 1), dashed);
        if (_fadedPens.TryGetValue(key, out var pen)) return pen;
        var brush = Solid((byte)(key.Item2 * 12.75), color.R, color.G, color.B);
        pen = MakePen(brush, key.Item3, dashed);
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

    /// <summary>An open line through the points, as one figure.</summary>
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

    /// <summary>A star's colour from its B−V index: blue-white, white, yellow, orange, red.</summary>
    internal static Color StarColor(double colorIndex)
    {
        (double I, Color C)[] stops =
        [
            (-0.3, Color.FromRgb(0x9B, 0xB8, 0xFF)),
            (0.0, Color.FromRgb(0xCA, 0xD8, 0xFF)),
            (0.4, Color.FromRgb(0xF8, 0xF7, 0xFF)),
            (0.8, Color.FromRgb(0xFF, 0xEE, 0xC8)),
            (1.2, Color.FromRgb(0xFF, 0xD2, 0x9A)),
            (1.8, Color.FromRgb(0xFF, 0xB0, 0x78)),
        ];
        if (colorIndex <= stops[0].I) return stops[0].C;
        for (var i = 1; i < stops.Length; i++)
        {
            if (colorIndex <= stops[i].I) return Lerp(stops[i - 1].C, stops[i].C, (colorIndex - stops[i - 1].I) / (stops[i].I - stops[i - 1].I));
        }

        return stops[^1].C;
    }

    /// <summary>The fractional part, for looping animations.</summary>
    private static double Frac(double value) => value - Math.Floor(value);

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
        public static readonly Color Figure = Color.FromRgb(0x8F, 0xB4, 0xE8);

        public static readonly IBrush NightSky = SpaceDrawing.LinearGradient(Color.FromRgb(0x02, 0x04, 0x0C), Color.FromRgb(0x10, 0x1A, 0x30), 90);
        public static readonly IBrush Ground = Solid(255, 0x04, 0x06, 0x09);
        public static readonly IPen GroundEdge = MakePen(Solid(90, 0x5A, 0x6A, 0x86), 1.2);
        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x14, 0x1C, 0x2E)), new ImmutableGradientStop(1, Color.FromRgb(0x05, 0x07, 0x0E))],
            center: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.8, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.8, RelativeUnit.Relative));

        public static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
        public static readonly IPen Frame = MakePen(Solid(60, 255, 255, 255), 1);
    }
}
