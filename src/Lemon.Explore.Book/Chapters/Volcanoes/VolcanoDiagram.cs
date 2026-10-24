using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Timing;

namespace Lemon.Explore.Book.Chapters.Volcanoes;

/// <summary>
/// The animated diagram of the volcanoes and rocks page. Eruption: a volcano cut in half, its magma chamber filling, the
/// magma rising up the conduit with gas bubbling out of it, and the eruption its magma makes, from lava fountains to an
/// ash column (<c>VolcanoDiagram.Eruption.cs</c>). Cycle: a landscape from the deep sea over mountains to a volcano with
/// the rock cycle's steps on it, and a grain to follow round it (<c>VolcanoDiagram.Cycle.cs</c>). Magnifier: magma
/// crystallising, mud settling and hardening in a tank, rock squeezed and heated (<c>VolcanoDiagram.Magnifier.cs</c>).
/// </summary>
/// <remarks>
/// The science is <see cref="VolcanoModel"/>'s; the simulations of the magnifier (<see cref="CrystalSim"/>,
/// <see cref="SedimentSim"/>) belong to the view model and the diagram only runs and draws them. While playing, the
/// diagram moves <see cref="Progress"/> on (through the eruption's round, along the followed grain's journey, or through the
/// cooling). The mouse and the clickable things are in <c>VolcanoDiagram.Objects.cs</c>.
/// </remarks>
public sealed partial class VolcanoDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
        else if (change.Property == SilicaProperty)
        {
            OnMagmaChanged();
        }
        else if (change.Property == WaterProperty)
        {
            OnMagmaChanged();
        }
        else if (change.Property == JourneyIdProperty)
        {
            OnJourneyChanged();
        }
        else if (change.Property == ProcessProperty)
        {
            OnModeChanged();
        }
        else if (change.Property == SqueezeParentProperty)
        {
            _grainKey = null;
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    #region dependency properties

    public static readonly StyledProperty<VolcanoMode> ModeProperty =
        AvaloniaProperty.Register<VolcanoDiagram, VolcanoMode>(nameof(Mode), VolcanoMode.Eruption);

    /// <summary>Whether time runs: the eruption's round, the grain's journey, the cooling, the sinking grains.</summary>
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<VolcanoDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How far on, 0 to 1: through the eruption's round, the followed grain's journey, or the magma's cooling.</summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<VolcanoDiagram, double>(nameof(Progress), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Eruption: the magma's silica, % by weight.</summary>
    public static readonly StyledProperty<double> SilicaProperty =
        AvaloniaProperty.Register<VolcanoDiagram, double>(nameof(Silica), 50.0);

    /// <summary>Eruption: the water dissolved in the magma, % by weight.</summary>
    public static readonly StyledProperty<double> WaterProperty =
        AvaloniaProperty.Register<VolcanoDiagram, double>(nameof(Water), 0.5);

    /// <summary>Eruption: the depths where bubbles start and where the froth tears apart, marked on the conduit.</summary>
    public static readonly StyledProperty<bool> ShowLevelsProperty =
        AvaloniaProperty.Register<VolcanoDiagram, bool>(nameof(ShowLevels), true);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<VolcanoDiagram, bool>(nameof(ShowLabels), true);

    /// <summary>Cycle: the grain being followed (a <see cref="Journey.Id"/>), or null.</summary>
    public static readonly StyledProperty<string?> JourneyIdProperty =
        AvaloniaProperty.Register<VolcanoDiagram, string?>(nameof(JourneyId));

    /// <summary>Magnifier: what is being watched.</summary>
    public static readonly StyledProperty<MagnifierProcess> ProcessProperty =
        AvaloniaProperty.Register<VolcanoDiagram, MagnifierProcess>(nameof(Process), MagnifierProcess.Cooling);

    /// <summary>Magnifier, cooling: the cooling magma (the view model makes a new one when a setting changes).</summary>
    public static readonly StyledProperty<CrystalSim?> CrystalsProperty =
        AvaloniaProperty.Register<VolcanoDiagram, CrystalSim?>(nameof(Crystals));

    /// <summary>Magnifier, settling: the tank (the view model pours and empties it).</summary>
    public static readonly StyledProperty<SedimentSim?> TankProperty =
        AvaloniaProperty.Register<VolcanoDiagram, SedimentSim?>(nameof(Tank));

    /// <summary>Magnifier, settling: how deep the layers have been buried, km.</summary>
    public static readonly StyledProperty<double> BurialKmProperty =
        AvaloniaProperty.Register<VolcanoDiagram, double>(nameof(BurialKm), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Magnifier, squeezing: the rock buried (Shale, Sandstone or Limestone).</summary>
    public static readonly StyledProperty<string> SqueezeParentProperty =
        AvaloniaProperty.Register<VolcanoDiagram, string>(nameof(SqueezeParent), "Shale");

    /// <summary>Magnifier, squeezing: how deep it is, km.</summary>
    public static readonly StyledProperty<double> SqueezeDepthKmProperty =
        AvaloniaProperty.Register<VolcanoDiagram, double>(nameof(SqueezeDepthKm), 4.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<VolcanoDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<VolcanoDiagram>(ModeProperty, ProgressProperty, SilicaProperty, WaterProperty, ShowLevelsProperty, ShowLabelsProperty, JourneyIdProperty, ProcessProperty, CrystalsProperty, TankProperty, BurialKmProperty, SqueezeParentProperty, SqueezeDepthKmProperty, ToolbarHeightProperty);
        return true;
    }


    public VolcanoMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public double Silica
    {
        get => GetValue(SilicaProperty);
        set => SetValue(SilicaProperty, value);
    }

    public double Water
    {
        get => GetValue(WaterProperty);
        set => SetValue(WaterProperty, value);
    }

    public bool ShowLevels
    {
        get => GetValue(ShowLevelsProperty);
        set => SetValue(ShowLevelsProperty, value);
    }

    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    public string? JourneyId
    {
        get => GetValue(JourneyIdProperty);
        set => SetValue(JourneyIdProperty, value);
    }

    public MagnifierProcess Process
    {
        get => GetValue(ProcessProperty);
        set => SetValue(ProcessProperty, value);
    }

    public CrystalSim? Crystals
    {
        get => GetValue(CrystalsProperty);
        set => SetValue(CrystalsProperty, value);
    }

    public SedimentSim? Tank
    {
        get => GetValue(TankProperty);
        set => SetValue(TankProperty, value);
    }

    public double BurialKm
    {
        get => GetValue(BurialKmProperty);
        set => SetValue(BurialKmProperty, value);
    }

    public string SqueezeParent
    {
        get => GetValue(SqueezeParentProperty);
        set => SetValue(SqueezeParentProperty, value);
    }

    public double SqueezeDepthKm
    {
        get => GetValue(SqueezeDepthKmProperty);
        set => SetValue(SqueezeDepthKmProperty, value);
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

    /// <summary>Seconds since the page started, for things that move whatever the state (waves, rain, glow).</summary>
    private double _drift;

    public VolcanoDiagram()
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
        _puffs.Clear();
        _sparks.Clear();
    }

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);
        _drift += dt;
        switch (Mode)
        {
            case VolcanoMode.Eruption:
                StepEruption(dt);
                break;

            case VolcanoMode.Cycle:
                StepCycle(dt);
                break;

            default:
                StepMagnifier(dt);
                break;
        }

        InvalidateVisual();
    }

    /// <summary>Moves <see cref="Progress"/> on by <paramref name="amount"/> while playing; at the end it stops (or wraps round).</summary>
    private void Advance(double amount, bool wrap)
    {
        if (!IsPlaying || _drag == DragKind.Scrub || amount <= 0) return;
        var next = Progress + amount;
        if (next >= 1)
        {
            if (wrap) next -= Math.Floor(next);
            else
            {
                next = 1;
                SetCurrentValue(IsPlayingProperty, false);
            }
        }

        SetCurrentValue(ProgressProperty, next);
    }

    #endregion

    /// <summary>The room for the picture: below the title, above the toolbar (and a walkthrough's subtitles).</summary>
    private Rect Stage()
    {
        var top = TitleSpace;
        var bottom = Math.Max(ToolbarSpace, ToolbarHeight + 18 + 16) + _reserve.Current;
        return new Rect(16, top, Math.Max(100, ActualWidth - 32), Math.Max(200, ActualHeight - top - bottom));
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
            case VolcanoMode.Cycle:
                DrawCycleMode(dc, now);
                break;

            case VolcanoMode.Magnifier:
                DrawMagnifierMode(dc, now);
                break;

            default:
                DrawEruptionMode(dc, now);
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

    /// <summary>A pill that is also a target for <paramref name="id"/>; it glows when the walkthrough points at it.</summary>
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
        if (_fadedPens.Count > 600) _fadedPens.Clear();
        _fadedPens[key] = pen;
        return pen;
    }

    /// <summary>A frozen brush of <paramref name="color"/> at <paramref name="alpha"/> (rounded to twentieths), made once and kept.</summary>
    private IBrush FadedBrush(Color color, double alpha)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20));
        if (_fadedBrushes.TryGetValue(key, out var brush)) return brush;
        brush = Solid((byte)(key.Item2 * 12.75), color.R, color.G, color.B);
        if (_fadedBrushes.Count > 600) _fadedBrushes.Clear();
        _fadedBrushes[key] = brush;
        return brush;
    }

    private readonly Dictionary<(Color Color, int Alpha), IBrush> _softBrushes = [];

    /// <summary>A round soft blob of <paramref name="color"/>: solid in the middle, fading to nothing at the edge (smoke, ash, glow).</summary>
    private IBrush SoftBrush(Color color, double alpha)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20));
        if (_softBrushes.TryGetValue(key, out var brush)) return brush;
        var a = (byte)(key.Item2 * 12.75);
        brush = RadialGradient(
            (Color.FromArgb(a, color.R, color.G, color.B), 0),
            (Color.FromArgb((byte)(a * 0.75), color.R, color.G, color.B), 0.55),
            (Color.FromArgb(0, color.R, color.G, color.B), 1));
        if (_softBrushes.Count > 800) _softBrushes.Clear();
        _softBrushes[key] = brush;
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

    /// <summary>A closed outline through the points, unstroked between them so a pen strokes it all round when drawn.</summary>
    private static StreamGeometry Shape(IEnumerable<Point> points) => Line(points, closed: true, filled: true);

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

    /// <summary>A curved arrow along a quadratic curve through <paramref name="control"/>.</summary>
    private void DrawCurvedArrow(PaintContext dc, Point from, Point control, Point to, Color color, double thickness = 2.5, double alpha = 1)
    {
        var points = new List<Point>();
        for (var i = 0; i <= 24; i++)
        {
            var t = i / 24.0;
            var a = from + ((control - from) * t);
            var b = control + ((to - control) * t);
            points.Add(a + ((b - a) * t));
        }

        var end = points[^1];
        var before = points[^3];
        Vector direction = end - before;
        direction = direction.Normalize();
        var head = Math.Max(7, thickness * 3.4);
        points[^1] = end - (direction * head * 0.7);
        dc.DrawGeometry(null, FadedPen(color, alpha, thickness), Line(points));
        DrawArrowHead(dc, FadedBrush(color, alpha), end, direction, head);
    }

    /// <summary>A small burst for an earthquake.</summary>
    private void DrawQuake(PaintContext dc, Point at, double size, double alpha)
    {
        if (alpha <= 0.02) return;
        var pen = FadedPen(Ink.Gold, alpha, 1.6);
        for (var i = 0; i < 8; i++)
        {
            var a = i * Math.PI / 4;
            var r = i % 2 == 0 ? size : size * 0.5;
            dc.DrawLine(pen, at + new Vector(Math.Cos(a) * size * 0.25, Math.Sin(a) * size * 0.25), at + new Vector(Math.Cos(a) * r, Math.Sin(a) * r));
        }
    }

    internal static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    /// <summary>Kilometres for a label: one decimal under 10, whole numbers above.</summary>
    internal static string Km(double km) => Number(km, km < 10 ? "0.#" : "0");

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
        public static readonly Color Pink = Color.FromRgb(0xFF, 0x8F, 0xC8);

        public static readonly Color Lava = Color.FromRgb(0xFF, 0x7A, 0x1E);
        public static readonly Color LavaHot = Color.FromRgb(0xFF, 0xD2, 0x5A);
        public static readonly Color LavaDark = Color.FromRgb(0x8A, 0x22, 0x0C);
        public static readonly Color Crust = Color.FromRgb(0x2E, 0x22, 0x20);
        public static readonly Color Ash = Color.FromRgb(0x8C, 0x86, 0x80);
        public static readonly Color AshDark = Color.FromRgb(0x55, 0x4F, 0x4A);
        public static readonly Color Steam = Color.FromRgb(0xE4, 0xE8, 0xEE);
        public static readonly Color Rock = Color.FromRgb(0x4A, 0x3E, 0x36);
        public static readonly Color RockDeep = Color.FromRgb(0x2C, 0x25, 0x22);
        public static readonly Color Sea = Color.FromRgb(0x1E, 0x5A, 0x9A);
        public static readonly Color SeaDeep = Color.FromRgb(0x0C, 0x26, 0x4A);
        public static readonly Color Sand = Color.FromRgb(0xD9, 0xBE, 0x84);
        public static readonly Color Mud = Color.FromRgb(0x6E, 0x66, 0x58);
        public static readonly Color Lime = Color.FromRgb(0xCF, 0xCB, 0xB4);
        public static readonly Color Granite = Color.FromRgb(0xC4, 0xA6, 0x9A);
        public static readonly Color Marble = Color.FromRgb(0xEE, 0xEC, 0xE6);
        public static readonly Color Schist = Color.FromRgb(0x6C, 0x5E, 0x82);
        public static readonly Color Grass = Color.FromRgb(0x3E, 0x6B, 0x38);

        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x10, 0x14, 0x22)), new ImmutableGradientStop(1, Color.FromRgb(0x04, 0x05, 0x0A))],
            center: new RelativePoint(0.45, 0.45, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.45, 0.45, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.9, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.9, RelativeUnit.Relative));

        public static readonly IBrush Sky = SpaceDrawing.LinearGradient(Color.FromRgb(0x0B, 0x16, 0x2C), Color.FromRgb(0x33, 0x4A, 0x6A), 90);
        public static readonly IBrush Panel = Solid(185, 8, 11, 20);
        public static readonly IPen Frame = MakePen(Solid(46, 255, 255, 255), 1);
        public static readonly IPen Faint = MakePen(Solid(60, 255, 255, 255), 1);
        public static readonly IPen Grid = MakePen(Solid(22, 255, 255, 255), 1);
        public static readonly IPen Edge = MakePen(Solid(120, 0, 0, 0), 1.2);
        public static readonly IPen Surface = MakePen(Solid(150, 0xD8, 0xC8, 0xB0), 1.4);
        public static readonly IBrush Lens = Solid(255, 0x07, 0x0B, 0x16);
        public static readonly IPen LensRim = MakePen(Solid(150, 0xC9, 0xD2, 0xDE), 2.5);
        public static readonly IBrush Glass = Solid(30, 0xC8, 0xE4, 0xFF);
        public static readonly IPen GlassPen = MakePen(Solid(170, 0xD8, 0xE6, 0xF4), 2);
        public static readonly IBrush Metal = SpaceDrawing.LinearGradient(Color.FromRgb(0x9C, 0xA6, 0xB4), Color.FromRgb(0x4A, 0x52, 0x5E), 90);
    }
}
