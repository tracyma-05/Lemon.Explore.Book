using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Book.Shell.Infrastructures.Localization;
using Lemon.Book.Shell.Infrastructures.Timing;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.StatesOfMatter;

/// <summary>
/// The animated diagram of the states of matter page. Particles: a cylinder of molecules on a heater, under a piston,
/// with its thermometer and pressure gauge (<c>MatterDiagram.Particles.cs</c>). Heating: a beaker of ice (or dry ice) on
/// a burner, a window on its molecules, and its heating curve drawn as the heat goes in (<c>MatterDiagram.Heating.cs</c>).
/// Diagram: the substance's phase diagram, a dot to drag about it, and a window on the molecules at the dot
/// (<c>MatterDiagram.Phases.cs</c>).
/// </summary>
/// <remarks>
/// The physics is <see cref="MatterModel"/>'s; the page's state (temperature, pressure, the heat put in) belongs to the
/// view model, and the diagram animates it: a lit burner runs <see cref="HeatKj"/> up, and dragging the dot or the
/// thermometer sets the temperature and pressure. The molecules are a <see cref="MoleculeBox"/>. The mouse and the
/// clickable things are in <c>MatterDiagram.Objects.cs</c>.
/// </remarks>
public sealed partial class MatterDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty || change.Property == SubstanceProperty)
        {
            _drag = DragKind.None;
            _settle = true;
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    #region dependency properties

    public static readonly StyledProperty<MatterMode> ModeProperty =
        AvaloniaProperty.Register<MatterDiagram, MatterMode>(nameof(Mode), MatterMode.Particles);

    public static readonly StyledProperty<MatterSubstance> SubstanceProperty =
        AvaloniaProperty.Register<MatterDiagram, MatterSubstance>(nameof(Substance), MatterSubstance.Water);

    /// <summary>Particles and diagram: the temperature, °C; dragging the thermometer or the dot sets it.</summary>
    public static readonly StyledProperty<double> CelsiusProperty =
        AvaloniaProperty.Register<MatterDiagram, double>(nameof(Celsius), 20.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Particles and diagram: the pressure as a power of ten of pascals; dragging the piston or the dot sets it.</summary>
    public static readonly StyledProperty<double> PressureLogProperty =
        AvaloniaProperty.Register<MatterDiagram, double>(nameof(PressureLog), Math.Log10(MatterModel.Atmosphere), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Heating: where it happens (the air pressure).</summary>
    public static readonly StyledProperty<HeatingPlace> PlaceProperty =
        AvaloniaProperty.Register<MatterDiagram, HeatingPlace>(nameof(Place), HeatingPlace.SeaLevel, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Heating: the burner; it goes out by itself at the end of the curve.</summary>
    public static readonly StyledProperty<HeatSetting> HeatProperty =
        AvaloniaProperty.Register<MatterDiagram, HeatSetting>(nameof(Heat), HeatSetting.Off, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Heating: the heat put in so far, kJ; a lit burner runs it up.</summary>
    public static readonly StyledProperty<double> HeatKjProperty =
        AvaloniaProperty.Register<MatterDiagram, double>(nameof(HeatKj), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The bonds between a solid's molecules.</summary>
    public static readonly StyledProperty<bool> ShowBondsProperty =
        AvaloniaProperty.Register<MatterDiagram, bool>(nameof(ShowBonds), true);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<MatterDiagram, bool>(nameof(ShowLabels), true);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<MatterDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<MatterDiagram>(ModeProperty, SubstanceProperty, CelsiusProperty, PressureLogProperty, PlaceProperty, HeatProperty, HeatKjProperty,
            ShowBondsProperty, ShowLabelsProperty, ToolbarHeightProperty);
        return true;
    }

    public MatterMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public MatterSubstance Substance
    {
        get => GetValue(SubstanceProperty);
        set => SetValue(SubstanceProperty, value);
    }

    public double Celsius
    {
        get => GetValue(CelsiusProperty);
        set => SetValue(CelsiusProperty, value);
    }

    public double PressureLog
    {
        get => GetValue(PressureLogProperty);
        set => SetValue(PressureLogProperty, value);
    }

    public HeatingPlace Place
    {
        get => GetValue(PlaceProperty);
        set => SetValue(PlaceProperty, value);
    }

    public HeatSetting Heat
    {
        get => GetValue(HeatProperty);
        set => SetValue(HeatProperty, value);
    }

    public double HeatKj
    {
        get => GetValue(HeatKjProperty);
        set => SetValue(HeatKjProperty, value);
    }

    public bool ShowBonds
    {
        get => GetValue(ShowBondsProperty);
        set => SetValue(ShowBondsProperty, value);
    }

    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    public double ToolbarHeight
    {
        get => GetValue(ToolbarHeightProperty);
        set => SetValue(ToolbarHeightProperty, value);
    }

    #endregion

    private readonly BookStopwatch _clock = new();
    private readonly LabelCache _labels;
    private readonly MoleculeBox _box = new();
    private double _lastSeconds;

    /// <summary>The molecules are put straight into their state on the next frame (the view or the substance changed).</summary>
    private bool _settle = true;

    private MatterModel.Substance Model => MatterModel.Of(Substance);

    private double Kelvin => Celsius + MatterModel.ZeroCelsius;

    private double Pressure => Math.Pow(10, PressureLog);

    private IReadOnlyList<MatterModel.Stretch> Curve => MatterModel.HeatingCurve(Model, MatterModel.PressureAt(Place));

    public MatterDiagram()
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
        _settle = true;
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

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);
        if (Mode == MatterMode.Heating) RunBurner(dt);
        AimMolecules();
        _box.Step(dt);
        _box.Fade(dt);
        InvalidateVisual();
    }

    /// <summary>A lit burner puts its heat in; at the end of the curve it goes out.</summary>
    private void RunBurner(double dt)
    {
        if (Heat == HeatSetting.Off || _drag == DragKind.Scrub) return;
        var total = MatterModel.TotalKj(Curve);
        var kj = Math.Min(total, HeatKj + (MatterModel.BurnerKw(Heat) * dt));
        SetCurrentValue(HeatKjProperty, kj);
        if (kj >= total) SetCurrentValue(HeatProperty, HeatSetting.Off);
    }

    /// <summary>Tells the molecules what to become: the state at the dot, or the heating's mix.</summary>
    private void AimMolecules()
    {
        _box.Substance = Substance;
        if (Mode == MatterMode.Heating)
        {
            var curve = Curve;
            var (solid, liquid, gas) = MatterModel.FractionsAt(curve, HeatKj);
            _box.Temperature = MatterModel.CelsiusAt(curve, HeatKj) + MatterModel.ZeroCelsius;
            _box.Supercritical = false;
            _box.SetTarget(solid, liquid, gas);
        }
        else
        {
            var state = MatterModel.StateAt(Model, Kelvin, Pressure);
            _box.Temperature = Kelvin;
            _box.Supercritical = state == MatterState.Supercritical;
            _box.SetTarget(state == MatterState.Solid ? 1 : 0, state == MatterState.Liquid ? 1 : 0, state is MatterState.Gas or MatterState.Supercritical ? 1 : 0);
        }

        if (_settle)
        {
            _box.Settle();
            _settle = false;
        }
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
            case MatterMode.Heating:
                DrawHeatingMode(dc, now);
                break;

            case MatterMode.Diagram:
                DrawPhasesMode(dc, now);
                break;

            default:
                DrawParticlesMode(dc, now);
                break;
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, now, width);
    }

    #region shared pictures

    /// <summary>
    /// A window on the molecules: a dark rounded box with the molecules inside, and the state's name under it. Registered
    /// as the target <paramref name="id"/>.
    /// </summary>
    private void DrawWindow(PaintContext dc, Rect window, string id, double now, string? caption)
    {
        dc.DrawRoundedRectangle(Ink.Lens, null, window, 10, 10);
        var inner = window.Deflate(6);
        _box.Ceiling = inner.Height / inner.Width;
        dc.PushClip(inner);
        _box.Draw(dc, inner, ShowBonds);
        dc.Pop();
        dc.DrawRoundedRectangle(null, Glows(id) ? ExploreTargets.GlowPen(now) : Ink.LensRim, window, 10, 10);
        AddBoxTarget(id, window);
        if (caption is not null && ShowLabels) DrawPill(dc, caption, new Point(window.Center.X, window.Bottom + 14), Ink.Text, 11);
    }

    /// <summary>A thermometer: a glass tube with a bulb, filled to <paramref name="fill"/> (0 to 1) in red.</summary>
    private void DrawThermometer(PaintContext dc, Rect tube, double fill, string id, double now)
    {
        var bulb = new Point(tube.Center.X, tube.Bottom + (tube.Width * 0.9));
        var radius = tube.Width * 1.1;
        dc.DrawRoundedRectangle(Ink.GlassFill, Glows(id) ? ExploreTargets.GlowPen(now) : Ink.GlassPen, tube, tube.Width / 2, tube.Width / 2);
        var column = new Rect(tube.X + (tube.Width * 0.3), tube.Bottom - (tube.Height * Math.Clamp(fill, 0, 1)), tube.Width * 0.4, (tube.Height * Math.Clamp(fill, 0, 1)) + 2);
        dc.DrawRectangle(Ink.Mercury, null, column);
        dc.DrawEllipse(Ink.Mercury, Ink.GlassPen, bulb, radius, radius);
        AddBoxTarget(id, new Rect(tube.X - 6, tube.Y, tube.Width + 12, tube.Height + (radius * 2.2)));
    }

    /// <summary>A gas flame of <paramref name="size"/> (0 for none) on a burner under <paramref name="center"/>, flickering.</summary>
    private void DrawFlame(PaintContext dc, Point center, double width, double size, double now)
    {
        if (size <= 0.01) return;
        var count = Math.Max(3, (int)(width / 14));
        for (var i = 0; i < count; i++)
        {
            var x = center.X - (width / 2) + ((i + 0.5) * width / count);
            var flicker = 0.8 + (0.2 * Math.Sin((now * 11) + (i * 1.7)));
            var h = size * 30 * flicker;
            var w = width / count * 0.45;
            var outer = Polygon(new Point(x - w, center.Y), new Point(x, center.Y - h), new Point(x + w, center.Y));
            var inner = Polygon(new Point(x - (w * 0.5), center.Y), new Point(x, center.Y - (h * 0.55)), new Point(x + (w * 0.5), center.Y));
            dc.DrawGeometry(Ink.FlameOuter, null, outer);
            dc.DrawGeometry(Ink.FlameInner, null, inner);
        }
    }

    /// <summary>A state's colour on the page.</summary>
    private static Color StateColor(MatterState state) => state switch
    {
        MatterState.Solid => Ink.Cyan,
        MatterState.Liquid => Ink.Blue,
        MatterState.Gas => Ink.Gold,
        _ => Ink.Violet,
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

    private readonly Dictionary<(Color Color, int Alpha, double Thickness, bool Dashed), IPen> _fadedPens = [];
    private readonly Dictionary<(Color Color, int Alpha), IBrush> _fadedBrushes = [];

    /// <summary>A frozen pen of <paramref name="color"/> at <paramref name="alpha"/> (rounded to twentieths), made once and kept.</summary>
    private IPen FadedPen(Color color, double alpha, double thickness, bool dashed = false)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20), Math.Round(thickness, 1), dashed);
        if (_fadedPens.TryGetValue(key, out var pen)) return pen;
        var brush = Solid((byte)(key.Item2 * 12.75), color.R, color.G, color.B);
        pen = new ImmutablePen(((IBrush)brush).ToImmutable(), key.Item3, dashed ? new ImmutableDashStyle([4, 3], 0) : null, PenLineCap.Round, PenLineJoin.Round);
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

        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x10, 0x14, 0x22)), new ImmutableGradientStop(1, Color.FromRgb(0x04, 0x05, 0x0A))],
            center: new RelativePoint(0.45, 0.45, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.45, 0.45, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.9, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.9, RelativeUnit.Relative));

        public static readonly IBrush PlotBack = Solid(150, 6, 8, 16);
        public static readonly IPen Frame = MakePen(Solid(46, 255, 255, 255), 1);
        public static readonly IPen Faint = MakePen(Solid(60, 255, 255, 255), 1);
        public static readonly IPen Grid = MakePen(Solid(22, 255, 255, 255), 1);
        public static readonly IBrush Lens = Solid(255, 0x07, 0x0B, 0x16);
        public static readonly IPen LensRim = MakePen(Solid(150, 0xC9, 0xD2, 0xDE), 2.5);
        public static readonly IBrush GlassFill = Solid(22, 0xC8, 0xE4, 0xFF);
        public static readonly IPen GlassPen = MakePen(Solid(170, 0xD8, 0xE6, 0xF4), 2);
        public static readonly IBrush Mercury = Solid(255, 0xFF, 0x5A, 0x5F);
        public static readonly IBrush Metal = SpaceDrawing.LinearGradient(Color.FromRgb(0x9C, 0xA6, 0xB4), Color.FromRgb(0x4A, 0x52, 0x5E), 0);
        public static readonly IBrush Dial = SpaceDrawing.LinearGradient(Color.FromRgb(0x2C, 0x34, 0x44), Color.FromRgb(0x16, 0x1A, 0x24), 90);
        public static readonly IBrush FlameOuter = Solid(210, 0x4F, 0x8C, 0xFF);
        public static readonly IBrush FlameInner = Solid(230, 0xBF, 0xE4, 0xFF);
        public static readonly IBrush Water = SpaceDrawing.LinearGradient(Color.FromArgb(150, 0x4A, 0x8C, 0xE8), Color.FromArgb(190, 0x2A, 0x5C, 0xB8), 90);
        public static readonly IBrush Ice = Solid(200, 0xD8, 0xF0, 0xFF);
        public static readonly IPen IceRim = MakePen(Solid(230, 0xFF, 0xFF, 0xFF), 1.2);
        public static readonly IBrush DryIce = Solid(230, 0xE8, 0xEC, 0xF2);
        public static readonly IBrush Bench = SpaceDrawing.LinearGradient(Color.FromArgb(200, 0x2A, 0x30, 0x3C), Color.FromArgb(0, 0x2A, 0x30, 0x3C), 90);
    }
}
