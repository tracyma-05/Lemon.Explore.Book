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

namespace Lemon.Explore.Book.Chapters.Lightning;

/// <summary>
/// The animated diagram of the lightning and static page. Static mode: a rod rubbed on a cloth, then held over paper
/// bits, beside a stream of water and up to an earthed metal ball (<c>LightningDiagram.Static.cs</c>). Cloud mode: a
/// thundercloud in slice, charging up and flashing (<c>LightningDiagram.Cloud.cs</c>). Strike mode: one strike in slow
/// motion and its thunder on the way to you (<c>LightningDiagram.Strike.cs</c>).
/// </summary>
/// <remarks>
/// The diagram owns the clock and pushes back what it simulates: the rod's charge and what it is doing, the cloud's
/// field at the meter and its flashes, and the strike's story time. Static mode: dragging moves the rod (and stops the
/// demonstrating hand). Cloud mode: clicking the ground moves the field meter. Strike mode: dragging sideways turns time.
/// Clicking a named thing explains it (<c>LightningDiagram.Objects.cs</c>).
/// </remarks>
public sealed partial class LightningDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
        else if (change.Property == AutoHandProperty)
        {
            OnAutoHandChanged(change.GetNewValue<bool>());
        }
        else if (change.Property == ChargeProperty)
        {
            OnChargeChanged(change.GetNewValue<double>());
        }
        else if (change.Property == CloudTopProperty)
        {
            _cloudBalls = null;
        }
        else if (change.Property == PeakCurrentProperty)
        {
            OnStrikeChanged();
        }
        else if (change.Property == LightningRodProperty)
        {
            OnStrikeChanged();
        }
        else if (change.Property == StrokesProperty)
        {
            OnStrikeChanged();
        }
        else if (change.Property == StrikeSeedProperty)
        {
            OnStrikeChanged();
        }
        else if (change.Property == StrikeTimeProperty)
        {
            OnStrikeTimeChanged(change.GetNewValue<double>());
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    #region dependency properties

    public static readonly StyledProperty<LightningMode> ModeProperty =
        AvaloniaProperty.Register<LightningDiagram, LightningMode>(nameof(Mode), LightningMode.Static);

    /// <summary>Static: what the rod is made of.</summary>
    public static readonly StyledProperty<Material> RodProperty =
        AvaloniaProperty.Register<LightningDiagram, Material>(nameof(Rod), Material.Balloon);

    /// <summary>Static: what it is rubbed with.</summary>
    public static readonly StyledProperty<Material> ClothProperty =
        AvaloniaProperty.Register<LightningDiagram, Material>(nameof(Cloth), Material.Hair);

    /// <summary>Static: the air's relative humidity, %.</summary>
    public static readonly StyledProperty<double> HumidityProperty =
        AvaloniaProperty.Register<LightningDiagram, double>(nameof(Humidity), 30.0);

    /// <summary>Static: the hand moves the rod round the bench by itself (cleared when the rod is dragged).</summary>
    public static readonly StyledProperty<bool> AutoHandProperty =
        AvaloniaProperty.Register<LightningDiagram, bool>(nameof(AutoHand), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Static: the rod's charge, nC (pushed back as it changes; setting it to 0 earths the rod).</summary>
    public static readonly StyledProperty<double> ChargeProperty =
        AvaloniaProperty.Register<LightningDiagram, double>(nameof(Charge), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Static: what the rod is doing (pushed back).</summary>
    public static readonly StyledProperty<StaticState> StateProperty =
        AvaloniaProperty.Register<LightningDiagram, StaticState>(nameof(State), StaticState.Neutral, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Static: seconds on the bench's clock (pushed back, for the charge's history).</summary>
    public static readonly StyledProperty<double> StaticTimeProperty =
        AvaloniaProperty.Register<LightningDiagram, double>(nameof(StaticTime), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Cloud: the height of the storm's top, km.</summary>
    public static readonly StyledProperty<double> CloudTopProperty =
        AvaloniaProperty.Register<LightningDiagram, double>(nameof(CloudTop), 12.0);

    public static readonly StyledProperty<bool> ShowChargesProperty =
        AvaloniaProperty.Register<LightningDiagram, bool>(nameof(ShowCharges), true);

    /// <summary>Cloud: where the field meter stands, km from under the storm (pushed back when the ground is clicked).</summary>
    public static readonly StyledProperty<double> MeterXProperty =
        AvaloniaProperty.Register<LightningDiagram, double>(nameof(MeterX), 2.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Cloud: the field at the meter, kV/m, positive pointing up (pushed back).</summary>
    public static readonly StyledProperty<double> GroundFieldProperty =
        AvaloniaProperty.Register<LightningDiagram, double>(nameof(GroundField), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Cloud: seconds on the storm's clock (pushed back, for the field's history).</summary>
    public static readonly StyledProperty<double> CloudTimeProperty =
        AvaloniaProperty.Register<LightningDiagram, double>(nameof(CloudTime), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Cloud: the latest flash (pushed back).</summary>
    public static readonly StyledProperty<FlashKind> LastFlashProperty =
        AvaloniaProperty.Register<LightningDiagram, FlashKind>(nameof(LastFlash), FlashKind.None, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Cloud: flashes so far (pushed back).</summary>
    public static readonly StyledProperty<int> FlashCountProperty =
        AvaloniaProperty.Register<LightningDiagram, int>(nameof(FlashCount), 0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Cloud: the shared location's latitude, for the share of flashes that reach the ground.</summary>
    public static readonly StyledProperty<double> LatitudeProperty =
        AvaloniaProperty.Register<LightningDiagram, double>(nameof(Latitude), 28.2);

    /// <summary>Strike: the first stroke's peak current, kA.</summary>
    public static readonly StyledProperty<double> PeakCurrentProperty =
        AvaloniaProperty.Register<LightningDiagram, double>(nameof(PeakCurrent), 30.0);

    /// <summary>Strike: the house has a lightning rod.</summary>
    public static readonly StyledProperty<bool> LightningRodProperty =
        AvaloniaProperty.Register<LightningDiagram, bool>(nameof(LightningRod), true);

    /// <summary>Strike: show the rolling sphere and the zone it cannot reach.</summary>
    public static readonly StyledProperty<bool> ShowSphereProperty =
        AvaloniaProperty.Register<LightningDiagram, bool>(nameof(ShowSphere), true);

    /// <summary>Strike: how far away you are, km.</summary>
    public static readonly StyledProperty<double> DistanceProperty =
        AvaloniaProperty.Register<LightningDiagram, double>(nameof(Distance), 3.0);

    /// <summary>Strike: return strokes in the flash.</summary>
    public static readonly StyledProperty<int> StrokesProperty =
        AvaloniaProperty.Register<LightningDiagram, int>(nameof(Strokes), 3);

    /// <summary>Strike: the seed of the leader's random path.</summary>
    public static readonly StyledProperty<int> StrikeSeedProperty =
        AvaloniaProperty.Register<LightningDiagram, int>(nameof(StrikeSeed), 7);

    /// <summary>Strike: story seconds since the leader started (pushed back as they run).</summary>
    public static readonly StyledProperty<double> StrikeTimeProperty =
        AvaloniaProperty.Register<LightningDiagram, double>(nameof(StrikeTime), 0.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<LightningDiagram, bool>(nameof(ShowLabels), true);

    /// <summary>Playback speed: 1 is the normal pace.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<LightningDiagram, double>(nameof(Speed), 1.0);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<LightningDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<LightningDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<LightningDiagram>(ModeProperty, RodProperty, ClothProperty, HumidityProperty, CloudTopProperty, ShowChargesProperty, MeterXProperty, LatitudeProperty, PeakCurrentProperty, LightningRodProperty, ShowSphereProperty, DistanceProperty, StrokesProperty, StrikeSeedProperty, StrikeTimeProperty, ShowLabelsProperty, SpeedProperty, ToolbarHeightProperty);
        return true;
    }



    public LightningMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public Material Rod
    {
        get => GetValue(RodProperty);
        set => SetValue(RodProperty, value);
    }

    public Material Cloth
    {
        get => GetValue(ClothProperty);
        set => SetValue(ClothProperty, value);
    }

    public double Humidity
    {
        get => GetValue(HumidityProperty);
        set => SetValue(HumidityProperty, value);
    }

    public bool AutoHand
    {
        get => GetValue(AutoHandProperty);
        set => SetValue(AutoHandProperty, value);
    }

    public double Charge
    {
        get => GetValue(ChargeProperty);
        set => SetValue(ChargeProperty, value);
    }

    public StaticState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public double StaticTime
    {
        get => GetValue(StaticTimeProperty);
        set => SetValue(StaticTimeProperty, value);
    }

    public double CloudTop
    {
        get => GetValue(CloudTopProperty);
        set => SetValue(CloudTopProperty, value);
    }

    public bool ShowCharges
    {
        get => GetValue(ShowChargesProperty);
        set => SetValue(ShowChargesProperty, value);
    }

    public double MeterX
    {
        get => GetValue(MeterXProperty);
        set => SetValue(MeterXProperty, value);
    }

    public double GroundField
    {
        get => GetValue(GroundFieldProperty);
        set => SetValue(GroundFieldProperty, value);
    }

    public double CloudTime
    {
        get => GetValue(CloudTimeProperty);
        set => SetValue(CloudTimeProperty, value);
    }

    public FlashKind LastFlash
    {
        get => GetValue(LastFlashProperty);
        set => SetValue(LastFlashProperty, value);
    }

    public int FlashCount
    {
        get => GetValue(FlashCountProperty);
        set => SetValue(FlashCountProperty, value);
    }

    public double Latitude
    {
        get => GetValue(LatitudeProperty);
        set => SetValue(LatitudeProperty, value);
    }

    public double PeakCurrent
    {
        get => GetValue(PeakCurrentProperty);
        set => SetValue(PeakCurrentProperty, value);
    }

    public bool LightningRod
    {
        get => GetValue(LightningRodProperty);
        set => SetValue(LightningRodProperty, value);
    }

    public bool ShowSphere
    {
        get => GetValue(ShowSphereProperty);
        set => SetValue(ShowSphereProperty, value);
    }

    public double Distance
    {
        get => GetValue(DistanceProperty);
        set => SetValue(DistanceProperty, value);
    }

    public int Strokes
    {
        get => GetValue(StrokesProperty);
        set => SetValue(StrokesProperty, value);
    }

    public int StrikeSeed
    {
        get => GetValue(StrikeSeedProperty);
        set => SetValue(StrikeSeedProperty, value);
    }

    public double StrikeTime
    {
        get => GetValue(StrikeTimeProperty);
        set => SetValue(StrikeTimeProperty, value);
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

    /// <summary>Animation seconds (scaled by the speed, stopped while paused).</summary>
    private double _t;

    private bool _dragging;
    private Point _dragStart;
    private double _dragStartValue;
    private bool _dragMoved;

    public LightningDiagram()
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
        _cloudBalls = null;
        _sphereZone = null;
    }

    private void OnModeChanged()
    {
        _dragging = false;
        _pressed = null;
        _targets.Hover = null;
        _flash = null;
    }

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);

        // The bench and the storm keep going during a walkthrough; the strike's clock is held where it talks about.
        var step = IsPlaying || (IsLessonActive && Mode != LightningMode.Strike) ? dt * Math.Clamp(Speed, 0.05, 8) : 0;
        switch (Mode)
        {
            case LightningMode.Static:
                // The bench keeps running while the rod is dragged by hand, so it can be rubbed.
                if (_dragging && !IsPlaying) step = dt * Math.Clamp(Speed, 0.05, 8);
                _t += step;
                StepStatic(step);
                break;

            case LightningMode.Cloud:
                _t += step;
                StepCloud(step);
                break;

            default:
                if (_dragging) step = 0;
                _t += step;
                if (step > 0) StepStrikeClock(step);
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
            case LightningMode.Cloud:
                DrawCloudMode(dc, width, height);
                break;

            case LightningMode.Strike:
                DrawStrikeMode(dc, width, height);
                break;

            default:
                DrawStaticMode(dc, width, height);
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

    /// <summary>A plus or minus sign of <paramref name="size"/> px, drawn with lines (crisp at any size).</summary>
    private void DrawSign(PaintContext dc, Point at, bool positive, double size, double alpha = 1)
    {
        var pen = FadedPen(positive ? Ink.Plus : Ink.Minus, alpha, Math.Max(1.2, size * 0.24), flat: true);
        var h = size / 2;
        dc.DrawLine(pen, new Point(at.X - h, at.Y), new Point(at.X + h, at.Y));
        if (positive) dc.DrawLine(pen, new Point(at.X, at.Y - h), new Point(at.X, at.Y + h));
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

    private readonly Dictionary<(Color Color, int Alpha), IBrush> _glows = [];

    /// <summary>A soft round glow of a colour, fading to nothing at its edge (cached by alpha in twentieths).</summary>
    private IBrush Glow(Color color, double alpha)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20));
        if (_glows.TryGetValue(key, out var brush)) return brush;
        var a = (byte)(key.Item2 * 12.75);
        brush = SpaceDrawing.RadialGradient(
            (Color.FromArgb(a, color.R, color.G, color.B), 0),
            (Color.FromArgb((byte)(a * 0.35), color.R, color.G, color.B), 0.45),
            (Color.FromArgb(0, color.R, color.G, color.B), 1));
        _glows[key] = brush;
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

        public void Draw(PaintContext dc, LightningDiagram owner, double thickness)
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

    /// <summary>A soft cloud of balls: all rims first, then the cores with one top-to-bottom shade (so the balls merge).</summary>
    private static void DrawPuffs(PaintContext dc, IReadOnlyList<(Point At, double R)> balls, double top, double bottom, double alpha, Color light, Color dark)
    {
        if (balls.Count == 0) return;
        var rim = Solid((byte)(60 * alpha), light.R, light.G, light.B);
        foreach (var (at, r) in balls) dc.DrawEllipse(rim, null, at, r * 1.15, r * 1.15);
        var shade = new ImmutableLinearGradientBrush(
        [
            new ImmutableGradientStop(0, Color.FromArgb((byte)(240 * alpha), light.R, light.G, light.B)),
            new ImmutableGradientStop(1, Color.FromArgb((byte)(235 * alpha), dark.R, dark.G, dark.B)),
        ],
            startPoint: new RelativePoint(0, top, RelativeUnit.Absolute),
            endPoint: new RelativePoint(0, bottom, RelativeUnit.Absolute));
        foreach (var (at, r) in balls) dc.DrawEllipse(shade, null, at, r, r);
    }

    /// <summary>
    /// A jagged line from <paramref name="from"/> to <paramref name="to"/>: the straight line split in
    /// <paramref name="pieces"/> and kinked sideways by up to <paramref name="kink"/> of its length (seeded, so it holds still).
    /// </summary>
    private static Point[] Zigzag(Point from, Point to, int pieces, double kink, int seed)
    {
        Vector v = to - from;
        var normal = new Vector(-v.Y, v.X);
        if (normal.Length > 0) normal /= normal.Length;
        var points = new Point[pieces + 1];
        for (var i = 0; i <= pieces; i++)
        {
            var f = i / (double)pieces;
            var off = i == 0 || i == pieces ? 0 : (Hash(seed + i, 3.7) - 0.5) * 2 * kink * v.Length;
            points[i] = from + (v * f) + (normal * off);
        }

        return points;
    }

    /// <summary>The fractional part, for looping animations.</summary>
    private static double Frac(double value) => value - Math.Floor(value);

    /// <summary>0 below <paramref name="a"/>, 1 above <paramref name="b"/>, smooth in between.</summary>
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

    /// <summary>A material's localized name.</summary>
    internal static string MaterialName(Material material) => Loc($"Lightning_Material_{material}");

    /// <summary>A voltage as kV (or V when small), signed.</summary>
    internal static string VoltageText(double volts) => Math.Abs(volts) >= 1000
        ? $"{(volts < 0 ? "−" : volts > 0 ? "+" : string.Empty)}{Number(Math.Abs(volts) / 1000, Math.Abs(volts) >= 10000 ? "0" : "0.0")} kV"
        : $"{(volts < 0 ? "−" : volts > 0 ? "+" : string.Empty)}{Number(Math.Abs(volts), "0")} V";

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
        public static readonly Color White = Color.FromRgb(0xFF, 0xFF, 0xFF);
        public static readonly Color Violet = Color.FromRgb(0xB3, 0x9D, 0xFF);

        /// <summary>Positive charge: red, as in most textbooks; negative: blue.</summary>
        public static readonly Color Plus = Color.FromRgb(0xFF, 0x6A, 0x5A);

        public static readonly Color Minus = Color.FromRgb(0x5A, 0xB0, 0xFF);

        /// <summary>The light of a spark or a lightning channel.</summary>
        public static readonly Color Bolt = Color.FromRgb(0xE8, 0xE4, 0xFF);

        public static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
        public static readonly IPen Frame = MakePen(Solid(60, 255, 255, 255), 1);
    }
}
