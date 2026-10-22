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

namespace Lemon.Explore.Book.Chapters.Retrograde;

/// <summary>
/// The animated diagram of the retrograde page. Sky mode: a strip of the zodiac along the ecliptic with the planet's real
/// path through one loop (<c>RetroDiagram.Sky.cs</c>). Above mode: the Sun, Earth's and the planet's orbits to scale, the
/// line of sight from Earth through the planet out to the stars, and where it pointed at equal steps
/// (<c>RetroDiagram.Above.cs</c>). Epicycle mode: the same motion with Earth or the Sun held still, Ptolemy's deferent and
/// epicycle, and the frame sliding from one to the other (<c>RetroDiagram.Epicycle.cs</c>).
/// </summary>
/// <remarks>
/// The diagram owns the clock and pushes <see cref="Day"/> back as it runs: in the sky and from above it repeats the loop
/// between <see cref="WindowStart"/> and <see cref="WindowEnd"/>, round the epicycles it runs on. Dragging scrubs the
/// date (from above it moves Earth round its orbit); the mouse and the clickable things are in <c>RetroDiagram.Objects.cs</c>.
/// </remarks>
public sealed partial class RetroDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
        else if (change.Property == PlanetProperty)
        {
            OnPlanetChanged();
        }
        else if (change.Property == DayProperty)
        {
            OnDayChanged(change.GetNewValue<double>());
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    /// <summary>Seconds the frame takes to slide between Earth and the Sun.</summary>
    private const double FrameSlideSeconds = 1.4;

    #region dependency properties

    public static readonly StyledProperty<RetroMode> ModeProperty =
        AvaloniaProperty.Register<RetroDiagram, RetroMode>(nameof(Mode), RetroMode.Sky);

    public static readonly StyledProperty<RetroPlanet> PlanetProperty =
        AvaloniaProperty.Register<RetroDiagram, RetroPlanet>(nameof(Planet), RetroPlanet.Mars);

    /// <summary>The date, days since J2000 (pushed back as it runs).</summary>
    public static readonly StyledProperty<double> DayProperty =
        AvaloniaProperty.Register<RetroDiagram, double>(nameof(Day), 9000.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The first day of the loop being shown (the sky and from above repeat it).</summary>
    public static readonly StyledProperty<double> WindowStartProperty =
        AvaloniaProperty.Register<RetroDiagram, double>(nameof(WindowStart), 8800.0);

    /// <summary>The last day of the loop being shown.</summary>
    public static readonly StyledProperty<double> WindowEndProperty =
        AvaloniaProperty.Register<RetroDiagram, double>(nameof(WindowEnd), 9200.0);

    /// <summary>Epicycles: the day the drawn trail begins (it is never longer than the planet's pattern).</summary>
    public static readonly StyledProperty<double> TrailStartProperty =
        AvaloniaProperty.Register<RetroDiagram, double>(nameof(TrailStart), 9000.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Epicycles: the Sun held still (Copernicus) rather than Earth (Ptolemy); the frame slides between them.</summary>
    public static readonly StyledProperty<bool> SunCenteredProperty =
        AvaloniaProperty.Register<RetroDiagram, bool>(nameof(SunCentered), false);

    /// <summary>The sky with east on the left, as seen facing the planet from the northern hemisphere (else on the right).</summary>
    public static readonly StyledProperty<bool> EastLeftProperty =
        AvaloniaProperty.Register<RetroDiagram, bool>(nameof(EastLeft), true);

    public static readonly StyledProperty<bool> ShowWholePathProperty =
        AvaloniaProperty.Register<RetroDiagram, bool>(nameof(ShowWholePath), true);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<RetroDiagram, bool>(nameof(ShowLabels), true);

    /// <summary>From above: the earlier lines of sight; round the epicycles: the deferent and the epicycle.</summary>
    public static readonly StyledProperty<bool> ShowGuidesProperty =
        AvaloniaProperty.Register<RetroDiagram, bool>(nameof(ShowGuides), true);

    /// <summary>Playback speed: 1 is the normal pace.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<RetroDiagram, double>(nameof(Speed), 1.0);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<RetroDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<RetroDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<RetroDiagram>(ModeProperty, PlanetProperty, DayProperty, WindowStartProperty, WindowEndProperty, TrailStartProperty, SunCenteredProperty, EastLeftProperty, ShowWholePathProperty, ShowLabelsProperty, ShowGuidesProperty, SpeedProperty, ToolbarHeightProperty);
        return true;
    }


    public RetroMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public RetroPlanet Planet
    {
        get => GetValue(PlanetProperty);
        set => SetValue(PlanetProperty, value);
    }

    public double Day
    {
        get => GetValue(DayProperty);
        set => SetValue(DayProperty, value);
    }

    public double WindowStart
    {
        get => GetValue(WindowStartProperty);
        set => SetValue(WindowStartProperty, value);
    }

    public double WindowEnd
    {
        get => GetValue(WindowEndProperty);
        set => SetValue(WindowEndProperty, value);
    }

    public double TrailStart
    {
        get => GetValue(TrailStartProperty);
        set => SetValue(TrailStartProperty, value);
    }

    public bool SunCentered
    {
        get => GetValue(SunCenteredProperty);
        set => SetValue(SunCenteredProperty, value);
    }

    public bool EastLeft
    {
        get => GetValue(EastLeftProperty);
        set => SetValue(EastLeftProperty, value);
    }

    public bool ShowWholePath
    {
        get => GetValue(ShowWholePathProperty);
        set => SetValue(ShowWholePathProperty, value);
    }

    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    public bool ShowGuides
    {
        get => GetValue(ShowGuidesProperty);
        set => SetValue(ShowGuidesProperty, value);
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

    private readonly Stopwatch _clock = new();
    private readonly LabelCache _labels;
    private readonly Dictionary<string, PlanetRenderer> _renderers = [];

    private double _lastSeconds;
    private bool _pushing;

    /// <summary>The date, kept here while it runs and pushed back to the page.</summary>
    private double _day = 9000;

    /// <summary>Epicycles: where the frame is, 0 = Earth held still, 1 = the Sun.</summary>
    private double _frame;

    private bool _dragging;
    private Point _dragStart;
    private double _dragStartDay;
    private bool _dragMoved;

    public RetroDiagram()
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
        _frame = SunCentered ? 1 : 0;
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

    private void OnPlanetChanged()
    {
        _dragging = false;
        _skyPath = null;
        _orbits = null;
    }

    private void OnDayChanged(double value)
    {
        if (!_pushing) _day = Math.Clamp(value, RetroModel.MinDay, RetroModel.MaxDay);
    }

    /// <summary>Days of the sky's and the top view's loop per second at 1×: each loop takes 20–25 s.</summary>
    internal static double LoopDaysPerSecond(RetroPlanet planet) => planet switch
    {
        RetroPlanet.Mercury => 2.5,
        RetroPlanet.Venus => 4,
        RetroPlanet.Mars => 8,
        _ => 11,
    };

    /// <summary>Days per second round the epicycles at 1×: a whole pattern takes 12–30 s.</summary>
    internal static double PatternDaysPerSecond(RetroPlanet planet) => planet switch
    {
        RetroPlanet.Mercury => 15,
        RetroPlanet.Venus => 120,
        RetroPlanet.Mars => 220,
        RetroPlanet.Jupiter => 300,
        _ => 800,
    };

    /// <summary>How long a trail round the epicycles is kept: the time the planet's pattern takes to close, days.</summary>
    internal static double PatternDays(RetroPlanet planet) => planet switch
    {
        RetroPlanet.Mercury => 365.25,
        RetroPlanet.Venus => 8 * 365.25,
        RetroPlanet.Mars => 15 * 365.25,
        RetroPlanet.Jupiter => 11.86 * 365.25,
        _ => 29.46 * 365.25,
    };

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);
        var target = SunCentered ? 1.0 : 0.0;
        _frame = target > _frame ? Math.Min(target, _frame + (dt / FrameSlideSeconds)) : Math.Max(target, _frame - (dt / FrameSlideSeconds));

        var speed = Math.Clamp(Speed, 0.05, 8);
        var step = IsPlaying && !_dragging ? dt * speed : 0;
        if (step > 0)
        {
            if (Mode == RetroMode.Epicycle)
            {
                _day = Math.Min(RetroModel.MaxDay, _day + (step * PatternDaysPerSecond(Planet)));
                if (_day >= RetroModel.MaxDay) SetCurrentValue(IsPlayingProperty, false);
            }
            else
            {
                // The loop repeats: past its last day it starts again from the first.
                _day += step * LoopDaysPerSecond(Planet);
                if (_day > WindowEnd || _day < WindowStart - 1) _day = WindowStart;
            }

            Push(DayProperty, Math.Round(_day, 3));
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
        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
        switch (Mode)
        {
            case RetroMode.Above:
                DrawAboveMode(dc);
                break;

            case RetroMode.Epicycle:
                DrawEpicycleMode(dc);
                break;

            default:
                DrawSkyMode(dc);
                break;
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, _clock.Elapsed.TotalSeconds, width);
    }

    #region bodies

    /// <summary>A ray-traced body, one renderer per catalogue id.</summary>
    private PlanetRenderer Renderer(string id)
    {
        if (!_renderers.TryGetValue(id, out var renderer))
        {
            renderer = new PlanetRenderer(Body(id));
            _renderers[id] = renderer;
        }

        return renderer;
    }

    /// <summary>A planet (or Earth) as a lit ball, lit from the screen point <paramref name="sun"/>.</summary>
    private void DrawBody(PaintContext dc, string id, Point at, double radius, Point sun)
    {
        DrawSphere(dc, Renderer(id), at, radius, LightFrom(at, sun, 0.15), RenderScale());
    }

    /// <summary>The Sun: a glow and a bright ball.</summary>
    private void DrawSun(PaintContext dc, Point at, double radius)
    {
        dc.DrawEllipse(Ink.SunGlow, null, at, radius * 3.4, radius * 3.4);
        DrawSphere(dc, Renderer("Sun"), at, radius, new LightVector(0, 0, 1), RenderScale(), _clock.Elapsed.TotalSeconds * 0.05);
    }

    /// <summary>The planet's colour on the page.</summary>
    internal static Color PlanetColor(RetroPlanet planet) => planet switch
    {
        RetroPlanet.Mercury => Color.FromRgb(0xC8, 0xC0, 0xB6),
        RetroPlanet.Venus => Color.FromRgb(0xF4, 0xE6, 0xB8),
        RetroPlanet.Mars => Color.FromRgb(0xFF, 0x8A, 0x5C),
        RetroPlanet.Jupiter => Color.FromRgb(0xF0, 0xD2, 0xA8),
        _ => Color.FromRgb(0xE8, 0xD0, 0x8A),
    };

    #endregion

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
    private static Color StarColor(double colorIndex) => StarTrails.TrailsDiagram.StarColor(colorIndex);

    /// <summary>A date as text in the page's language, e.g. 16 Jan 2025 / 2025年1月16日.</summary>
    internal static string DateText(double day) => LocFormat("Retro_Date", RetroModel.DateOf(day));

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
        public static readonly Color Orange = Color.FromRgb(0xFF, 0x7A, 0x4A);
        public static readonly Color Blue = Color.FromRgb(0x5A, 0xA8, 0xFF);
        public static readonly Color Violet = Color.FromRgb(0xB3, 0x9D, 0xFF);
        public static readonly Color Figure = Color.FromRgb(0x8F, 0xB4, 0xE8);
        public static readonly Color Sun = Color.FromRgb(0xFF, 0xD8, 0x6A);

        public static readonly IBrush NightSky = SpaceDrawing.LinearGradient(Color.FromRgb(0x03, 0x05, 0x10), Color.FromRgb(0x0C, 0x14, 0x28), 90);
        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x14, 0x1C, 0x2E)), new ImmutableGradientStop(1, Color.FromRgb(0x05, 0x07, 0x0E))],
            center: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.8, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.8, RelativeUnit.Relative));

        public static readonly IBrush SunGlow = SpaceDrawing.RadialGradient((Color.FromArgb(150, 0xFF, 0xD8, 0x6A), 0), (Color.FromArgb(0, 0xFF, 0xA8, 0x3A), 1));

        public static readonly IPen Grid = MakePen(Solid(24, 255, 255, 255), 1);
        public static readonly IPen Frame = MakePen(Solid(60, 255, 255, 255), 1);
    }
}
