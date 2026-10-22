using System.ComponentModel;
using System.Diagnostics;
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

namespace Lemon.Explore.Book.Chapters.Greenhouse;

/// <summary>
/// The animated diagram of the greenhouse-effect page. Planet mode: a slice of ground, air and space with sunlight
/// coming in and the ground's infrared caught and sent on by the greenhouse gases (<c>GreenhouseDiagram.Planet.cs</c>).
/// Molecules mode: one molecule wiggling, with the ground's heat rays caught or passing, over the spectrum of the
/// ground's glow (<c>GreenhouseDiagram.Molecules.cs</c>). Glass-house mode: a greenhouse and a parked car through a
/// sunny day (<c>GreenhouseDiagram.GlassHouse.cs</c>).
/// </summary>
/// <remarks>
/// The diagram owns the clock and pushes back <see cref="Hour"/> (glass house) as it runs; dragging sideways turns the
/// day by hand. Clicking the greenhouse's roof vents or the car's window opens or shuts them; the mouse and the clickable
/// things are in <c>GreenhouseDiagram.Objects.cs</c>.
/// </remarks>
public sealed partial class GreenhouseDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
        else if (change.Property == GasProperty)
        {
            OnGasChanged();
        }
        else if (change.Property == HourProperty)
        {
            OnHourChanged(change.GetNewValue<double>());
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    /// <summary>Glass house: hours of the day per second of playback at 1×.</summary>
    private const double HoursPerSecond = 0.8;

    #region dependency properties

    public static readonly StyledProperty<GreenhouseMode> ModeProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, GreenhouseMode>(nameof(Mode), GreenhouseMode.Planet);

    /// <summary>Planet: CO₂ in the air, ppm.</summary>
    public static readonly StyledProperty<double> Co2Property =
        AvaloniaProperty.Register<GreenhouseDiagram, double>(nameof(Co2), PlanetModel.TodayCo2);

    /// <summary>Planet: no greenhouse gases at all.</summary>
    public static readonly StyledProperty<bool> NoGasesProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, bool>(nameof(NoGases), false);

    /// <summary>Planet: the average energy flows drawn as arrows, W/m².</summary>
    public static readonly StyledProperty<bool> ShowBudgetProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, bool>(nameof(ShowBudget), false);

    /// <summary>Molecules: the gas shown.</summary>
    public static readonly StyledProperty<Gas> GasProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, Gas>(nameof(Gas), Gas.CO2);

    /// <summary>Molecules: the way of wiggling shown (a <see cref="Vibration.Id"/>); clicking one on the spectrum sets it.</summary>
    public static readonly StyledProperty<string> VibrationIdProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, string>(nameof(VibrationId), "Bend", defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Glass house: the hour of the day, 0…24 (pushed back as it runs).</summary>
    public static readonly StyledProperty<double> HourProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, double>(nameof(Hour), 10.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Glass house: the latitude of the shared observer location, degrees.</summary>
    public static readonly StyledProperty<double> LatitudeProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, double>(nameof(Latitude), 28.2);

    public static readonly StyledProperty<Season> SeasonProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, Season>(nameof(Season), Season.Summer);

    /// <summary>Glass house: the roof vents stand open (clicking them toggles this).</summary>
    public static readonly StyledProperty<bool> VentsOpenProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, bool>(nameof(VentsOpen), false, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Glass house: the roof is rock salt, which lets infrared through (Wood's 1909 experiment).</summary>
    public static readonly StyledProperty<bool> SaltPaneProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, bool>(nameof(SaltPane), false);

    /// <summary>Glass house: the car's window is open a crack (clicking it toggles this).</summary>
    public static readonly StyledProperty<bool> CarWindowOpenProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, bool>(nameof(CarWindowOpen), false, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, bool>(nameof(ShowLabels), true);

    /// <summary>Playback speed: 1 is the normal pace.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, double>(nameof(Speed), 1.0);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<GreenhouseDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<GreenhouseDiagram>(ModeProperty, Co2Property, NoGasesProperty, ShowBudgetProperty, GasProperty, VibrationIdProperty, HourProperty, LatitudeProperty, SeasonProperty, VentsOpenProperty, SaltPaneProperty, CarWindowOpenProperty, ShowLabelsProperty, SpeedProperty, ToolbarHeightProperty);
        return true;
    }



    public GreenhouseMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double Co2
    {
        get => GetValue(Co2Property);
        set => SetValue(Co2Property, value);
    }

    public bool NoGases
    {
        get => GetValue(NoGasesProperty);
        set => SetValue(NoGasesProperty, value);
    }

    public bool ShowBudget
    {
        get => GetValue(ShowBudgetProperty);
        set => SetValue(ShowBudgetProperty, value);
    }

    public Gas Gas
    {
        get => GetValue(GasProperty);
        set => SetValue(GasProperty, value);
    }

    public string VibrationId
    {
        get => GetValue(VibrationIdProperty);
        set => SetValue(VibrationIdProperty, value);
    }

    public double Hour
    {
        get => GetValue(HourProperty);
        set => SetValue(HourProperty, value);
    }

    public double Latitude
    {
        get => GetValue(LatitudeProperty);
        set => SetValue(LatitudeProperty, value);
    }

    public Season Season
    {
        get => GetValue(SeasonProperty);
        set => SetValue(SeasonProperty, value);
    }

    public bool VentsOpen
    {
        get => GetValue(VentsOpenProperty);
        set => SetValue(VentsOpenProperty, value);
    }

    public bool SaltPane
    {
        get => GetValue(SaltPaneProperty);
        set => SetValue(SaltPaneProperty, value);
    }

    public bool CarWindowOpen
    {
        get => GetValue(CarWindowOpenProperty);
        set => SetValue(CarWindowOpenProperty, value);
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

    private readonly Stopwatch _clock = new();
    private readonly StarBackdrop _stars = new();
    private readonly LabelCache _labels;
    private readonly Random _random = new(20261009);

    private double _lastSeconds;
    private bool _pushing;

    /// <summary>Animation seconds (scaled by the speed, stopped while paused): photons, wiggles, rising air.</summary>
    private double _t;

    /// <summary>The glass house's hour, kept here while it runs and pushed back to the page.</summary>
    private double _hour = 10;

    private bool _dragging;
    private Point _dragStart;
    private double _dragStartHour;
    private bool _dragMoved;

    public GreenhouseDiagram()
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
        _planetLayout = null;
        ResetPlanetPhotons();
        _rays.Clear();
        _airBlobs.Clear();
    }

    private void OnModeChanged()
    {
        _dragging = false;
        ResetPlanetPhotons();
        _rays.Clear();
        _airBlobs.Clear();
        _beam.Clear();
    }

    private void OnHourChanged(double value)
    {
        if (!_pushing) _hour = Math.Clamp(value, 0, 24);
    }

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);
        if (Math.Abs(_reserve.Current - _laidOutReserve) > 0.5)
        {
            _laidOutReserve = _reserve.Current;
            _planetLayout = null;
        }

        // A walkthrough holds the hour it talks about, but the light and the air keep moving.
        var step = IsPlaying && !_dragging ? dt * Math.Clamp(Speed, 0.05, 8) : 0;
        var moving = (IsPlaying || IsLessonActive) && !_dragging ? dt * Math.Clamp(Speed, 0.05, 8) : 0;
        _t += moving;
        switch (Mode)
        {
            case GreenhouseMode.Planet:
                StepPlanet(moving);
                break;

            case GreenhouseMode.Molecules:
                StepMolecules(moving);
                break;

            default:
                if (step > 0)
                {
                    _hour = (_hour + (step * HoursPerSecond)) % 24;
                    Push(HourProperty, Math.Round(_hour, 4));
                }

                StepGlassHouse(moving);
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
            case GreenhouseMode.Molecules:
                DrawMoleculesMode(dc, width, height);
                break;

            case GreenhouseMode.GlassHouse:
                DrawGlassHouseMode(dc, width, height);
                break;

            default:
                DrawPlanetMode(dc, width, height);
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

    /// <summary>An arrow from <paramref name="from"/> to <paramref name="to"/>.</summary>
    private static void DrawArrow(PaintContext dc, Point from, Point to, IBrush brush, double thickness = 2, double head = 9)
    {
        Vector v = to - from;
        if (v.Length < 3) return;
        var direction = v / v.Length;
        dc.DrawLine(MakePen(brush, thickness), from, to - (direction * head * 0.6));
        DrawArrowHead(dc, brush, to, direction, head);
    }

    /// <summary>A thick straight arrow whose width shows an amount of energy, its body broken by moving dashes.</summary>
    private void DrawFlowArrow(PaintContext dc, Point from, Point to, Color color, double width, double opacity = 1)
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
        dc.DrawGeometry(FadedBrush(color, 0.4 * opacity), FadedPen(color, 0.95 * opacity, 1.2), body);

        // Bright dashes moving along the arrow show the flow.
        var dash = Math.Max(8, width * 0.9);
        var offset = Frac(_t * 0.9) * dash * 2;
        for (var s = offset; s < length - head; s += dash * 2)
        {
            var a = from + (along * s);
            var b = from + (along * Math.Min(s + dash, length - head));
            dc.DrawLine(FadedPen(color, 0.75 * opacity, Math.Max(1.5, width * 0.45), flat: true), a, b);
        }
    }

    /// <summary>A small thermometer whose column stands at <paramref name="celsius"/> on a <paramref name="low"/>…<paramref name="high"/> °C scale.</summary>
    private static void DrawThermometer(PaintContext dc, Point bulb, double height, double celsius, double low = -30, double high = 60)
    {
        var tube = new Rect(bulb.X - 3, bulb.Y - height, 6, height);
        dc.DrawRoundedRectangle(Ink.Glass, Ink.GlassEdge, tube, 3, 3);
        var level = Math.Clamp((celsius - low) / (high - low), 0.05, 1);
        var column = celsius < 0 ? Ink.ColdColumn : Ink.Mercury;
        dc.DrawRectangle(column, null, new Rect(bulb.X - 1.6, bulb.Y - (height * level), 3.2, height * level));
        dc.DrawEllipse(column, Ink.GlassEdge, bulb, 5, 5);
    }

    /// <summary>The colour of something at a temperature, °C: icy blue, white near 10 °C, orange, red.</summary>
    internal static Color HeatColor(double celsius)
    {
        (double T, Color C)[] stops =
        [
            (-30, Color.FromRgb(0x4A, 0x8B, 0xFF)),
            (-10, Color.FromRgb(0x7F, 0xC8, 0xFF)),
            (12, Color.FromRgb(0xE6, 0xEE, 0xF6)),
            (30, Color.FromRgb(0xFF, 0xC0, 0x6A)),
            (45, Color.FromRgb(0xFF, 0x8A, 0x4A)),
            (60, Color.FromRgb(0xFF, 0x5A, 0x4A)),
        ];
        if (celsius <= stops[0].T) return stops[0].C;
        for (var i = 1; i < stops.Length; i++)
        {
            if (celsius <= stops[i].T) return Lerp(stops[i - 1].C, stops[i].C, (celsius - stops[i - 1].T) / (stops[i].T - stops[i - 1].T));
        }

        return stops[^1].C;
    }

    /// <summary>A false colour for infrared light of a wavelength (µm), violet at 3 µm through to deep red at 40 µm.</summary>
    internal static Color InfraredColor(double micrometres)
    {
        var t = Math.Clamp(Math.Log(micrometres / 3) / Math.Log(40.0 / 3), 0, 1);
        (double T, Color C)[] stops =
        [
            (0, Color.FromRgb(0xB3, 0x8B, 0xFF)),
            (0.25, Color.FromRgb(0x5A, 0xB0, 0xFF)),
            (0.45, Color.FromRgb(0x6F, 0xE0, 0xB0)),
            (0.62, Color.FromRgb(0xFF, 0xE0, 0x6A)),
            (0.8, Color.FromRgb(0xFF, 0x9A, 0x4A)),
            (1, Color.FromRgb(0xFF, 0x4A, 0x5A)),
        ];
        for (var i = 1; i < stops.Length; i++)
        {
            if (t <= stops[i].T) return Lerp(stops[i - 1].C, stops[i].C, (t - stops[i - 1].T) / (stops[i].T - stops[i - 1].T));
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
    /// Many short strokes of a few colours drawn as one geometry per pen, so hundreds of photons cost a few draw calls
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

        public void Draw(PaintContext dc, GreenhouseDiagram owner, double thickness)
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

    /// <summary>A wavy photon: a short sine wiggle along <paramref name="direction"/> ending at <paramref name="head"/>.</summary>
    private static Point[] Wiggle(Point head, Vector direction, double length, double period, double amplitude, double phase)
    {
        const int steps = 14;
        var across = new Vector(-direction.Y, direction.X);
        var points = new Point[steps + 1];
        for (var i = 0; i <= steps; i++)
        {
            var s = length * i / steps;
            var envelope = Math.Sin(Math.PI * i / steps);
            points[i] = head - (direction * s) + (across * (amplitude * envelope * Math.Sin((2 * Math.PI * s / period) + phase)));
        }

        return points;
    }

    /// <summary>The fractional part, for looping animations.</summary>
    private static double Frac(double value) => value - Math.Floor(value);

    private static double Smooth(double a, double b, double value)
    {
        var t = Math.Clamp((value - a) / (b - a), 0, 1);
        return t * t * (3 - (2 * t));
    }

    private static double Mix(double a, double b, double t) => a + ((b - a) * t);

    private static Point Mix(Point a, Point b, double t) => new(Mix(a.X, b.X, t), Mix(a.Y, b.Y, t));

    /// <summary>A hash in 0…1 for the n-th scattered thing, so it stays put from frame to frame.</summary>
    private static double Hash(int n, double salt) => Frac(Math.Sin((n * 12.9898) + (salt * 78.233)) * 43758.5453);

    internal static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    #endregion

    private static class Ink
    {
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
        public static readonly Color Sun = Color.FromRgb(0xFF, 0xE2, 0x7A);
        public static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
        public static readonly Color Green = Color.FromRgb(0x8B, 0xE0, 0x8F);
        public static readonly Color Orange = Color.FromRgb(0xFF, 0x9F, 0x5A);
        public static readonly Color Red = Color.FromRgb(0xFF, 0x6A, 0x5A);
        public static readonly Color Infrared = Color.FromRgb(0xFF, 0x6A, 0x4A);
        public static readonly Color Blue = Color.FromRgb(0x5A, 0xA8, 0xFF);
        public static readonly Color Violet = Color.FromRgb(0xB3, 0x9D, 0xFF);
        public static readonly Color White = Color.FromRgb(0xFF, 0xFF, 0xFF);

        public static readonly IBrush Glass = Solid(150, 0xEE, 0xF1, 0xF6);
        public static readonly IPen GlassEdge = MakePen(Solid(200, 0x30, 0x38, 0x48), 1);
        public static readonly IBrush Mercury = Solid(255, 0xFF, 0x5A, 0x4A);
        public static readonly IBrush ColdColumn = Solid(255, 0x6F, 0xB8, 0xFF);

        public static readonly IPen Leader = MakePen(Solid(150, 0xEE, 0xF1, 0xF6), 1);
        public static readonly IPen Axis = MakePen(Solid(130, 0xC9, 0xD1, 0xDC), 1.2);
        public static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
        public static readonly IPen Frame = MakePen(Solid(60, 255, 255, 255), 1);

        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x1A, 0x22, 0x32)), new ImmutableGradientStop(1, Color.FromRgb(0x06, 0x09, 0x10))],
            center: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.8, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.8, RelativeUnit.Relative));
    }
}
