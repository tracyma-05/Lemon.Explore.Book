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

namespace Lemon.Explore.Book.Chapters.BlackHole;

/// <summary>
/// The animated diagram of the black-hole page. Up close: the hole's shadow and a glowing disk of gas round it, every
/// pixel ray traced along light's real bent path (<c>BlackHoleDiagram.Shadow.cs</c>). Light and orbits: the hole from
/// above with light passing it at different distances, and a probe in orbit next to the path Newton would give it
/// (<c>BlackHoleDiagram.Orbits.cs</c>). Lensing: a black hole far away in front of a galaxy, a star or a grid, bending
/// their light into arcs, two images and the Einstein ring (<c>BlackHoleDiagram.Lensing.cs</c>).
/// </summary>
/// <remarks>
/// All in units of the hole's mass (G = c = M = 1, <see cref="BlackHoleModel"/>): the picture is the same for a hole of
/// any mass, only its size in km changes. The mouse and the clickable things are in <c>BlackHoleDiagram.Objects.cs</c>.
/// </remarks>
public sealed partial class BlackHoleDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
        else if (change.Property == StartRadiusProperty)
        {
            ResetProbes();
        }
        else if (change.Property == StartRadialSpeedProperty)
        {
            ResetProbes();
        }
        else if (change.Property == StartAngularMomentumProperty)
        {
            ResetProbes();
        }
        else if (change.Property == LaunchCountProperty)
        {
            ResetProbes();
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    #region dependency properties

    public static readonly StyledProperty<BlackHoleMode> ModeProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, BlackHoleMode>(nameof(Mode), BlackHoleMode.Shadow);

    /// <summary>Up close: degrees between the disk's axis and the line of sight (0 from above, 90 edge on). Dragging up and down tilts.</summary>
    public static readonly StyledProperty<double> InclinationProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, double>(nameof(Inclination), 80.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Light bends round the hole (true), or goes straight as if gravity did not pull on it.</summary>
    public static readonly StyledProperty<bool> BendLightProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, bool>(nameof(BendLight), true);

    public static readonly StyledProperty<bool> ShowDiskProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, bool>(nameof(ShowDisk), true);

    /// <summary>The approaching side's light squeezed bluer and brighter, the other side's stretched redder and dimmer.</summary>
    public static readonly StyledProperty<bool> DopplerProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, bool>(nameof(Doppler), true);

    public static readonly StyledProperty<bool> ShowStarsProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, bool>(nameof(ShowStars), true);

    /// <summary>Orbits: the probe's start radius, outward speed (dr/dτ) and angular momentum. Dragging the probe or its arrow sets them.</summary>
    public static readonly StyledProperty<double> StartRadiusProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, double>(nameof(StartRadius), 10.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> StartRadialSpeedProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, double>(nameof(StartRadialSpeed), 0.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> StartAngularMomentumProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, double>(nameof(StartAngularMomentum), BlackHoleModel.CircularAngularMomentum(10), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Orbits: changed to send the probe off again from its start.</summary>
    public static readonly StyledProperty<int> LaunchCountProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, int>(nameof(LaunchCount), 0);

    public static readonly StyledProperty<bool> ShowRaysProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, bool>(nameof(ShowRays), true);

    public static readonly StyledProperty<bool> ShowNewtonProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, bool>(nameof(ShowNewton), true);

    /// <summary>Orbits: where the probe is now (r), reported back.</summary>
    public static readonly StyledProperty<double> ProbeRadiusProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, double>(nameof(ProbeRadius), 10.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Orbits: the probe's own time so far (M), reported back.</summary>
    public static readonly StyledProperty<double> ProbeTimeProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, double>(nameof(ProbeTime), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Orbits: a far-away clock's time so far (M; infinity once the probe is through the horizon), reported back.</summary>
    public static readonly StyledProperty<double> FarTimeProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, double>(nameof(FarTime), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Orbits: "Orbiting", "Lost" (through the horizon) or "Escaped", reported back.</summary>
    public static readonly StyledProperty<string> ProbeStateProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, string>(nameof(ProbeState), "Orbiting", defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Lensing: what is behind the hole.</summary>
    public static readonly StyledProperty<LensSource> LensSourceProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, LensSource>(nameof(LensSource), LensSource.Galaxy);

    /// <summary>Lensing: where the hole is in front of the sky (view units, the source at 0, 0). Dragging moves it.</summary>
    public static readonly StyledProperty<double> LensXProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, double>(nameof(LensX), -1.6, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> LensYProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, double>(nameof(LensY), TrackY, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Lensing: the Einstein radius in view units (the view is 2 units tall).</summary>
    public static readonly StyledProperty<double> EinsteinRadiusProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, double>(nameof(EinsteinRadius), 0.32);

    public static readonly StyledProperty<bool> LensOnProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, bool>(nameof(LensOn), true);

    public static readonly StyledProperty<bool> ShowRingProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, bool>(nameof(ShowRing), true);

    public static readonly StyledProperty<bool> ShowTruePositionProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, bool>(nameof(ShowTruePosition), true);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, bool>(nameof(ShowLabels), true);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<BlackHoleDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<BlackHoleDiagram>(ModeProperty, InclinationProperty, BendLightProperty, ShowDiskProperty, DopplerProperty, ShowStarsProperty, StartRadiusProperty, StartRadialSpeedProperty, StartAngularMomentumProperty, LaunchCountProperty, ShowRaysProperty, ShowNewtonProperty, ProbeRadiusProperty, ProbeTimeProperty, FarTimeProperty, LensSourceProperty, LensXProperty, LensYProperty, EinsteinRadiusProperty, LensOnProperty, ShowRingProperty, ShowTruePositionProperty, ShowLabelsProperty, ToolbarHeightProperty);
        return true;
    }



    public BlackHoleMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double Inclination
    {
        get => GetValue(InclinationProperty);
        set => SetValue(InclinationProperty, value);
    }

    public bool BendLight
    {
        get => GetValue(BendLightProperty);
        set => SetValue(BendLightProperty, value);
    }

    public bool ShowDisk
    {
        get => GetValue(ShowDiskProperty);
        set => SetValue(ShowDiskProperty, value);
    }

    public bool Doppler
    {
        get => GetValue(DopplerProperty);
        set => SetValue(DopplerProperty, value);
    }

    public bool ShowStars
    {
        get => GetValue(ShowStarsProperty);
        set => SetValue(ShowStarsProperty, value);
    }

    public double StartRadius
    {
        get => GetValue(StartRadiusProperty);
        set => SetValue(StartRadiusProperty, value);
    }

    public double StartRadialSpeed
    {
        get => GetValue(StartRadialSpeedProperty);
        set => SetValue(StartRadialSpeedProperty, value);
    }

    public double StartAngularMomentum
    {
        get => GetValue(StartAngularMomentumProperty);
        set => SetValue(StartAngularMomentumProperty, value);
    }

    public int LaunchCount
    {
        get => GetValue(LaunchCountProperty);
        set => SetValue(LaunchCountProperty, value);
    }

    public bool ShowRays
    {
        get => GetValue(ShowRaysProperty);
        set => SetValue(ShowRaysProperty, value);
    }

    public bool ShowNewton
    {
        get => GetValue(ShowNewtonProperty);
        set => SetValue(ShowNewtonProperty, value);
    }

    public double ProbeRadius
    {
        get => GetValue(ProbeRadiusProperty);
        set => SetValue(ProbeRadiusProperty, value);
    }

    public double ProbeTime
    {
        get => GetValue(ProbeTimeProperty);
        set => SetValue(ProbeTimeProperty, value);
    }

    public double FarTime
    {
        get => GetValue(FarTimeProperty);
        set => SetValue(FarTimeProperty, value);
    }

    public string ProbeState
    {
        get => GetValue(ProbeStateProperty);
        set => SetValue(ProbeStateProperty, value);
    }

    public LensSource LensSource
    {
        get => GetValue(LensSourceProperty);
        set => SetValue(LensSourceProperty, value);
    }

    public double LensX
    {
        get => GetValue(LensXProperty);
        set => SetValue(LensXProperty, value);
    }

    public double LensY
    {
        get => GetValue(LensYProperty);
        set => SetValue(LensYProperty, value);
    }

    public double EinsteinRadius
    {
        get => GetValue(EinsteinRadiusProperty);
        set => SetValue(EinsteinRadiusProperty, value);
    }

    public bool LensOn
    {
        get => GetValue(LensOnProperty);
        set => SetValue(LensOnProperty, value);
    }

    public bool ShowRing
    {
        get => GetValue(ShowRingProperty);
        set => SetValue(ShowRingProperty, value);
    }

    public bool ShowTruePosition
    {
        get => GetValue(ShowTruePositionProperty);
        set => SetValue(ShowTruePositionProperty, value);
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

    public BlackHoleDiagram()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
        ResetProbes();
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
        if (IsPlaying && _drag is DragKind.None or DragKind.Pending)
        {
            switch (Mode)
            {
                case BlackHoleMode.Shadow:
                    _diskTime += dt;
                    break;

                case BlackHoleMode.Orbits:
                    StepOrbits(dt);
                    break;

                case BlackHoleMode.Lensing:
                    StepLensing(dt);
                    break;
            }
        }

        // Light keeps flowing past the hole even with the probe paused.
        if (Mode == BlackHoleMode.Orbits) _rayTime += dt;

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
            case BlackHoleMode.Orbits:
                DrawOrbitsMode(dc, now);
                break;

            case BlackHoleMode.Lensing:
                DrawLensingMode(dc, now);
                break;

            default:
                DrawShadowMode(dc, now);
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

    /// <summary>A pill joined to the point it names by a thin line, the pill at <paramref name="pill"/>.</summary>
    private Rect DrawCallout(PaintContext dc, string text, Point at, Point pill, Color color, string id)
    {
        dc.DrawLine(FadedPen(color, 0.7, 1), at, pill);
        dc.DrawEllipse(FadedBrush(color, 0.9), null, at, 2.5, 2.5);
        return DrawTargetPill(dc, text, pill, color, id);
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

        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x0B, 0x0D, 0x1A)), new ImmutableGradientStop(1, Color.FromRgb(0x02, 0x03, 0x07))],
            center: new RelativePoint(0.45, 0.5, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.45, 0.5, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.85, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.85, RelativeUnit.Relative));

        public static readonly IBrush Panel = Solid(185, 8, 11, 20);
        public static readonly IPen Frame = MakePen(Solid(46, 255, 255, 255), 1);
        public static readonly IPen Faint = MakePen(Solid(34, 255, 255, 255), 1);
    }
}

/// <summary>Colour helpers shared by the black-hole renderers.</summary>
internal static class BlackHoleDrawing
{
    /// <summary>
    /// The colour of a blackbody at <paramref name="kelvin"/>, each channel 0 to 1 (white near 6500 K): Tanner Helland's
    /// fit to the CIE colours of Planck's curve.
    /// </summary>
    public static (double R, double G, double B) Blackbody(double kelvin)
    {
        var t = Math.Clamp(kelvin, 1000, 40000) / 100;
        double r, g, b;
        if (t <= 66)
        {
            r = 255;
            g = (99.4708025861 * Math.Log(t)) - 161.1195681661;
        }
        else
        {
            r = 329.698727446 * Math.Pow(t - 60, -0.1332047592);
            g = 288.1221695283 * Math.Pow(t - 60, -0.0755148492);
        }

        b = t >= 66 ? 255 : t <= 19 ? 0 : (138.5177312231 * Math.Log(t - 10)) - 305.0447927307;
        return (Math.Clamp(r, 0, 255) / 255, Math.Clamp(g, 0, 255) / 255, Math.Clamp(b, 0, 255) / 255);
    }
}
