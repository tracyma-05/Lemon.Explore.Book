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

namespace Lemon.Explore.Book.Chapters.Expansion;

/// <summary>
/// The animated diagram of the expanding-universe page. Hubble: a patch of galaxies drifting apart as space stretches,
/// seen from a home galaxy that can be any of them, with their speeds growing with distance
/// (<c>ExpansionDiagram.Hubble.cs</c>). Redshift: light crossing stretching space from a distant galaxy to us, its waves
/// stretched with it, and the spectrum it brings (<c>ExpansionDiagram.Redshift.cs</c>). History: the universe's size
/// through time as a horn, with a window into what space looked like at any moment (<c>ExpansionDiagram.History.cs</c>).
/// </summary>
/// <remarks>
/// The expansion is <see cref="Universe"/>'s, the real ΛCDM model; galaxies are drawn far larger than they are, and in the
/// Hubble view each dot stands for a whole cluster. The mouse and the clickable things are in
/// <c>ExpansionDiagram.Objects.cs</c>.
/// </remarks>
public sealed partial class ExpansionDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
        else if (change.Property == HomeProperty)
        {
            OnHomeChanged(change.GetOldValue<GalaxyCell>());
        }
        else if (change.Property == RedshiftProperty)
        {
            OnRedshiftChanged();
        }
        else if (change.Property == UniverseProperty)
        {
            OnHornChanged();
        }
        else if (change.Property == AxisProperty)
        {
            OnHornChanged();
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    #region dependency properties

    public static readonly StyledProperty<ExpansionMode> ModeProperty =
        AvaloniaProperty.Register<ExpansionDiagram, ExpansionMode>(nameof(Mode), ExpansionMode.Hubble);

    /// <summary>Hubble: how far through the span the patch is, 0 (0.6 of today's size) to 1 (now).</summary>
    public static readonly StyledProperty<double> HubbleProgressProperty =
        AvaloniaProperty.Register<ExpansionDiagram, double>(nameof(HubbleProgress), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Hubble: the galaxy everything is seen from. Clicking another galaxy moves home there.</summary>
    public static readonly StyledProperty<GalaxyCell> HomeProperty =
        AvaloniaProperty.Register<ExpansionDiagram, GalaxyCell>(nameof(Home), GalaxyCell.MilkyWay, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Hubble: each galaxy's speed away from home as an arrow.</summary>
    public static readonly StyledProperty<bool> ShowArrowsProperty =
        AvaloniaProperty.Register<ExpansionDiagram, bool>(nameof(ShowArrows), true);

    /// <summary>Hubble: where every galaxy was at the start of the span, laid under the picture (two photos on top of each other).</summary>
    public static readonly StyledProperty<bool> ShowOverlayProperty =
        AvaloniaProperty.Register<ExpansionDiagram, bool>(nameof(ShowOverlay), false);

    /// <summary>Hubble: a grid drawn on space itself, stretching with it.</summary>
    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<ExpansionDiagram, bool>(nameof(ShowGrid), false);

    /// <summary>Hubble: tape measures from home to three galaxies with their distances and speeds.</summary>
    public static readonly StyledProperty<bool> ShowRulersProperty =
        AvaloniaProperty.Register<ExpansionDiagram, bool>(nameof(ShowRulers), true);

    /// <summary>Redshift: how much the chosen galaxy's light is stretched on arrival (z).</summary>
    public static readonly StyledProperty<double> RedshiftProperty =
        AvaloniaProperty.Register<ExpansionDiagram, double>(nameof(Redshift), 1.0);

    /// <summary>Redshift: the galaxy's name key part, or null for a redshift picked by hand.</summary>
    public static readonly StyledProperty<string?> SourceIdProperty =
        AvaloniaProperty.Register<ExpansionDiagram, string?>(nameof(SourceId), "Halfway");

    /// <summary>Redshift: how far through its journey the light is, 0 (just left) to 1 (here).</summary>
    public static readonly StyledProperty<double> JourneyProperty =
        AvaloniaProperty.Register<ExpansionDiagram, double>(nameof(Journey), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>History: which universe.</summary>
    public static readonly StyledProperty<UniverseKind> UniverseProperty =
        AvaloniaProperty.Register<ExpansionDiagram, UniverseKind>(nameof(Universe), UniverseKind.Ours);

    /// <summary>History: how time is spread along the axis.</summary>
    public static readonly StyledProperty<TimeAxis> AxisProperty =
        AvaloniaProperty.Register<ExpansionDiagram, TimeAxis>(nameof(Axis), TimeAxis.Linear);

    /// <summary>History: the moment looked at, Gyr after the start. Dragging along the horn moves it.</summary>
    public static readonly StyledProperty<double> CosmicTimeProperty =
        AvaloniaProperty.Register<ExpansionDiagram, double>(nameof(CosmicTime), 13.8, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<ExpansionDiagram, bool>(nameof(ShowLabels), true);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<ExpansionDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<ExpansionDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<ExpansionDiagram>(ModeProperty, HubbleProgressProperty, HomeProperty, ShowArrowsProperty, ShowOverlayProperty, ShowGridProperty, ShowRulersProperty, RedshiftProperty, SourceIdProperty, JourneyProperty, UniverseProperty, AxisProperty, CosmicTimeProperty, ShowLabelsProperty, ToolbarHeightProperty);
        return true;
    }



    public ExpansionMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double HubbleProgress
    {
        get => GetValue(HubbleProgressProperty);
        set => SetValue(HubbleProgressProperty, value);
    }

    public GalaxyCell Home
    {
        get => GetValue(HomeProperty);
        set => SetValue(HomeProperty, value);
    }

    public bool ShowArrows
    {
        get => GetValue(ShowArrowsProperty);
        set => SetValue(ShowArrowsProperty, value);
    }

    public bool ShowOverlay
    {
        get => GetValue(ShowOverlayProperty);
        set => SetValue(ShowOverlayProperty, value);
    }

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public bool ShowRulers
    {
        get => GetValue(ShowRulersProperty);
        set => SetValue(ShowRulersProperty, value);
    }

    public double Redshift
    {
        get => GetValue(RedshiftProperty);
        set => SetValue(RedshiftProperty, value);
    }

    public string? SourceId
    {
        get => GetValue(SourceIdProperty);
        set => SetValue(SourceIdProperty, value);
    }

    public double Journey
    {
        get => GetValue(JourneyProperty);
        set => SetValue(JourneyProperty, value);
    }

    public UniverseKind Universe
    {
        get => GetValue(UniverseProperty);
        set => SetValue(UniverseProperty, value);
    }

    public TimeAxis Axis
    {
        get => GetValue(AxisProperty);
        set => SetValue(AxisProperty, value);
    }

    public double CosmicTime
    {
        get => GetValue(CosmicTimeProperty);
        set => SetValue(CosmicTimeProperty, value);
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
    private readonly StarBackdrop _stars = new();
    private double _lastSeconds;

    public ExpansionDiagram()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
        SizeChanged += (_, _) =>
        {
            _stars.OnSizeChanged(_clock.Elapsed.TotalSeconds);
            OnHornChanged();
        };
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
        _hold = 0;
    }

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);
        StepPan(dt);
        if (IsPlaying && _drag is not (DragKind.Time or DragKind.Journey or DragKind.Cursor))
        {
            switch (Mode)
            {
                case ExpansionMode.Hubble:
                    StepHubble(dt);
                    break;

                case ExpansionMode.Redshift:
                    StepRedshift(dt);
                    break;

                case ExpansionMode.History:
                    StepHistory(dt);
                    break;
            }
        }

        InvalidateVisual();
    }

    /// <summary>Seconds the animation has waited at the end of a run before starting over.</summary>
    private double _hold;

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
            case ExpansionMode.Redshift:
                _stars.Draw(dc, width, height, RenderScale(), now, 0.55);
                DrawRedshiftMode(dc, now);
                break;

            case ExpansionMode.History:
                _stars.Draw(dc, width, height, RenderScale(), now, 0.35);
                DrawHistoryMode(dc, now);
                break;

            default:
                DrawHubbleMode(dc, now);
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

    /// <summary>The size of a pill for <paramref name="text"/>, without drawing it.</summary>
    private Size PillSize(string text, double size = 11.5)
    {
        var formatted = _labels.Text(text, size, Ink.Text);
        return new Size(formatted.Width + 12, formatted.Height + 4);
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

    /// <summary>The colour of light of a wavelength (nm): the rainbow from violet to red, dim red-brown for infrared.</summary>
    internal static Color LightColour(double nanometres)
    {
        if (nanometres > 750) return Ink.Infrared;
        if (nanometres < 380) return Ink.Ultraviolet;

        // Bruton's ramp, softened at the ends.
        double r, g, b;
        var w = nanometres;
        if (w < 440)
        {
            r = -(w - 440) / 60;
            g = 0;
            b = 1;
        }
        else if (w < 490)
        {
            r = 0;
            g = (w - 440) / 50;
            b = 1;
        }
        else if (w < 510)
        {
            r = 0;
            g = 1;
            b = -(w - 510) / 20;
        }
        else if (w < 580)
        {
            r = (w - 510) / 70;
            g = 1;
            b = 0;
        }
        else if (w < 645)
        {
            r = 1;
            g = -(w - 645) / 65;
            b = 0;
        }
        else
        {
            r = 1;
            g = 0;
            b = 0;
        }

        var fade = w < 420 ? 0.35 + (0.65 * (w - 380) / 40) : w > 700 ? 0.35 + (0.65 * (750 - w) / 50) : 1;
        return Color.FromRgb((byte)(255 * Math.Pow(r * fade, 0.8)), (byte)(255 * Math.Pow(g * fade, 0.8)), (byte)(255 * Math.Pow(b * fade, 0.8)));
    }

    internal static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    private static double Smooth(double t) => t <= 0 ? 0 : t >= 1 ? 1 : t * t * (3 - (2 * t));

    #endregion

    /// <summary>
    /// Short lines gathered by pen and drawn as one geometry per pen: hundreds of separate translucent lines keep the render
    /// thread busy, a couple of dozen geometries do not.
    /// </summary>
    private sealed class LineBatch
    {
        private readonly Dictionary<IPen, List<(Point From, Point To)>> _lines = [];

        public void Add(IPen pen, Point from, Point to)
        {
            if (!_lines.TryGetValue(pen, out var list)) _lines[pen] = list = [];
            list.Add((from, to));
        }

        public void Draw(PaintContext dc)
        {
            foreach (var (pen, list) in _lines)
            {
                if (list.Count == 0) continue;
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    foreach (var (from, to) in list)
                    {
                        context.BeginFigure(from, false);
                        context.LineTo(to);
                        context.EndFigure(false);
                    }
                }
                dc.DrawGeometry(null, pen, geometry);
                list.Clear();
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
        public static readonly Color Infrared = Color.FromRgb(0x9C, 0x3B, 0x2E);
        public static readonly Color Ultraviolet = Color.FromRgb(0x6A, 0x4C, 0xB8);

        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x0E, 0x11, 0x22)), new ImmutableGradientStop(1, Color.FromRgb(0x03, 0x04, 0x0A))],
            center: new RelativePoint(0.45, 0.45, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.45, 0.45, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.85, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.85, RelativeUnit.Relative));

        public static readonly IBrush Panel = Solid(170, 8, 12, 22);
        public static readonly IPen Frame = MakePen(Solid(46, 255, 255, 255), 1);
        public static readonly IPen Axis = MakePen(Solid(80, 255, 255, 255), 1, dashed: true);
        public static readonly IPen Faint = MakePen(Solid(40, 255, 255, 255), 1);
    }
}
