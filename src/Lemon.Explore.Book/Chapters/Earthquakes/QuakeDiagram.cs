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

namespace Lemon.Explore.Book.Chapters.Earthquakes;

/// <summary>The three views of the earthquake page.</summary>
public enum QuakeMode
{
    /// <summary>The world map of plates, their edges, quakes and volcanoes.</summary>
    Plates,

    /// <summary>Cross-sections of the four kinds of plate edge.</summary>
    Boundaries,

    /// <summary>Waves from a quake through the Earth's layers.</summary>
    Inside,
}

/// <summary>
/// The animated diagram of the earthquake page. Plates: a world map with the plates, their boundaries by kind, which way
/// they move, a year's worth of quakes by depth and the volcanoes (<c>QuakeDiagram.Plates.cs</c>). Boundaries: a ridge,
/// a sinking plate, India meeting Asia, and a sliding fault seen from above (<c>QuakeDiagram.Boundaries.cs</c>). Inside:
/// P and S waves spreading through the Earth's layers from a quake, the shadow zones the core casts and the textbook
/// chart of wave speed with depth (<c>QuakeDiagram.Inside.cs</c>).
/// </summary>
/// <remarks>The mouse and the clickable things are in <c>QuakeDiagram.Objects.cs</c>.</remarks>
public sealed partial class QuakeDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
        else if (change.Property == SixPlatesProperty)
        {
            InvalidateMap();
        }
        else if (change.Property == SectionProperty)
        {
            OnSectionChanged();
        }
        else if (change.Property == QuakeRequestProperty)
        {
            QuakeNow();
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    #region dependency properties

    public static readonly StyledProperty<QuakeMode> ModeProperty =
        AvaloniaProperty.Register<QuakeDiagram, QuakeMode>(nameof(Mode), QuakeMode.Plates);

    /// <summary>Plates: the textbook's six big plates instead of all fifteen.</summary>
    public static readonly StyledProperty<bool> SixPlatesProperty =
        AvaloniaProperty.Register<QuakeDiagram, bool>(nameof(SixPlates), false);

    public static readonly StyledProperty<bool> ShowQuakesProperty =
        AvaloniaProperty.Register<QuakeDiagram, bool>(nameof(ShowQuakes), true);

    public static readonly StyledProperty<bool> ShowVolcanoesProperty =
        AvaloniaProperty.Register<QuakeDiagram, bool>(nameof(ShowVolcanoes), true);

    public static readonly StyledProperty<bool> ShowArrowsProperty =
        AvaloniaProperty.Register<QuakeDiagram, bool>(nameof(ShowArrows), true);

    /// <summary>Plates: the boundary picked (index into <see cref="PlateModel.Boundaries"/>), −1 for none.</summary>
    public static readonly StyledProperty<int> SelectedBoundaryProperty =
        AvaloniaProperty.Register<QuakeDiagram, int>(nameof(SelectedBoundary), -1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Plates: the plate picked (its id), or null.</summary>
    public static readonly StyledProperty<string?> SelectedPlateProperty =
        AvaloniaProperty.Register<QuakeDiagram, string?>(nameof(SelectedPlate), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Plates: the famous quake picked (its id), or null.</summary>
    public static readonly StyledProperty<string?> FamousProperty =
        AvaloniaProperty.Register<QuakeDiagram, string?>(nameof(Famous), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The viewer's place, shown on the map.</summary>
    public static readonly StyledProperty<double> PlaceLatitudeProperty =
        AvaloniaProperty.Register<QuakeDiagram, double>(nameof(PlaceLatitude), 28.23);

    public static readonly StyledProperty<double> PlaceLongitudeProperty =
        AvaloniaProperty.Register<QuakeDiagram, double>(nameof(PlaceLongitude), 112.94);

    public static readonly StyledProperty<string?> PlaceNameProperty =
        AvaloniaProperty.Register<QuakeDiagram, string?>(nameof(PlaceName));

    /// <summary>Boundaries: which cross-section.</summary>
    public static readonly StyledProperty<SectionKind> SectionProperty =
        AvaloniaProperty.Register<QuakeDiagram, SectionKind>(nameof(Section), SectionKind.Ridge);

    /// <summary>
    /// Boundaries: the section's clock. Ridge and collision: millions of years ago (counting down); the sinking plate
    /// and the fault: years (counting up). The diagram runs it and pushes it back.
    /// </summary>
    public static readonly StyledProperty<double> SectionTimeProperty =
        AvaloniaProperty.Register<QuakeDiagram, double>(nameof(SectionTime), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Boundaries: how fast each side of the ridge moves away, cm a year.</summary>
    public static readonly StyledProperty<double> HalfRateProperty =
        AvaloniaProperty.Register<QuakeDiagram, double>(nameof(HalfRate), 2.5);

    /// <summary>Boundaries: how steeply the sinking plate goes down, °.</summary>
    public static readonly StyledProperty<double> DipProperty =
        AvaloniaProperty.Register<QuakeDiagram, double>(nameof(Dip), 40.0);

    /// <summary>Boundaries: the upper plate is ocean (an island arc like Japan's) instead of a continent (the Andes).</summary>
    public static readonly StyledProperty<bool> OverOceanProperty =
        AvaloniaProperty.Register<QuakeDiagram, bool>(nameof(OverOcean), false);

    /// <summary>Boundaries: the fault slides freely (creeps) instead of sticking.</summary>
    public static readonly StyledProperty<bool> CreepProperty =
        AvaloniaProperty.Register<QuakeDiagram, bool>(nameof(Creep), false);

    /// <summary>Changed (any new value) to set off the section's quake now.</summary>
    public static readonly StyledProperty<int> QuakeRequestProperty =
        AvaloniaProperty.Register<QuakeDiagram, int>(nameof(QuakeRequest), 0);

    /// <summary>Boundaries: quakes so far on the fault or the sinking plate, pushed out.</summary>
    public static readonly StyledProperty<int> QuakeCountProperty =
        AvaloniaProperty.Register<QuakeDiagram, int>(nameof(QuakeCount), 0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Inside: where the quake is on the rim, ° clockwise from the top.</summary>
    public static readonly StyledProperty<double> QuakeAngleProperty =
        AvaloniaProperty.Register<QuakeDiagram, double>(nameof(QuakeAngle), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Inside: how far round the Earth the picked station is from the quake, ° (to the right of it).</summary>
    public static readonly StyledProperty<double> StationDistanceProperty =
        AvaloniaProperty.Register<QuakeDiagram, double>(nameof(StationDistance), 60.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Inside: seconds since the quake. The diagram runs it (a minute of wave travel a second) and pushes it back.</summary>
    public static readonly StyledProperty<double> WaveTimeProperty =
        AvaloniaProperty.Register<QuakeDiagram, double>(nameof(WaveTime), 0.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> ShowRaysProperty =
        AvaloniaProperty.Register<QuakeDiagram, bool>(nameof(ShowRays), true);

    /// <summary>Inside: the station is the viewer's place (named) rather than a point picked on the rim.</summary>
    public static readonly StyledProperty<string?> StationNameProperty =
        AvaloniaProperty.Register<QuakeDiagram, string?>(nameof(StationName));

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<QuakeDiagram, bool>(nameof(ShowLabels), true);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<QuakeDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<QuakeDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<QuakeDiagram>(ModeProperty, SixPlatesProperty, ShowQuakesProperty, ShowVolcanoesProperty, ShowArrowsProperty, SelectedBoundaryProperty, SelectedPlateProperty, FamousProperty, PlaceLatitudeProperty, PlaceLongitudeProperty, PlaceNameProperty, SectionProperty, SectionTimeProperty, HalfRateProperty, DipProperty, OverOceanProperty, CreepProperty, QuakeRequestProperty, QuakeCountProperty, QuakeAngleProperty, StationDistanceProperty, WaveTimeProperty, ShowRaysProperty, StationNameProperty, ShowLabelsProperty, IsPlayingProperty, ToolbarHeightProperty);
        return true;
    }



    public QuakeMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public bool SixPlates
    {
        get => GetValue(SixPlatesProperty);
        set => SetValue(SixPlatesProperty, value);
    }

    public bool ShowQuakes
    {
        get => GetValue(ShowQuakesProperty);
        set => SetValue(ShowQuakesProperty, value);
    }

    public bool ShowVolcanoes
    {
        get => GetValue(ShowVolcanoesProperty);
        set => SetValue(ShowVolcanoesProperty, value);
    }

    public bool ShowArrows
    {
        get => GetValue(ShowArrowsProperty);
        set => SetValue(ShowArrowsProperty, value);
    }

    public int SelectedBoundary
    {
        get => GetValue(SelectedBoundaryProperty);
        set => SetValue(SelectedBoundaryProperty, value);
    }

    public string? SelectedPlate
    {
        get => GetValue(SelectedPlateProperty);
        set => SetValue(SelectedPlateProperty, value);
    }

    public string? Famous
    {
        get => GetValue(FamousProperty);
        set => SetValue(FamousProperty, value);
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

    public string? PlaceName
    {
        get => GetValue(PlaceNameProperty);
        set => SetValue(PlaceNameProperty, value);
    }

    public SectionKind Section
    {
        get => GetValue(SectionProperty);
        set => SetValue(SectionProperty, value);
    }

    public double SectionTime
    {
        get => GetValue(SectionTimeProperty);
        set => SetValue(SectionTimeProperty, value);
    }

    public double HalfRate
    {
        get => GetValue(HalfRateProperty);
        set => SetValue(HalfRateProperty, value);
    }

    public double Dip
    {
        get => GetValue(DipProperty);
        set => SetValue(DipProperty, value);
    }

    public bool OverOcean
    {
        get => GetValue(OverOceanProperty);
        set => SetValue(OverOceanProperty, value);
    }

    public bool Creep
    {
        get => GetValue(CreepProperty);
        set => SetValue(CreepProperty, value);
    }

    public int QuakeRequest
    {
        get => GetValue(QuakeRequestProperty);
        set => SetValue(QuakeRequestProperty, value);
    }

    public int QuakeCount
    {
        get => GetValue(QuakeCountProperty);
        set => SetValue(QuakeCountProperty, value);
    }

    public double QuakeAngle
    {
        get => GetValue(QuakeAngleProperty);
        set => SetValue(QuakeAngleProperty, value);
    }

    public double StationDistance
    {
        get => GetValue(StationDistanceProperty);
        set => SetValue(StationDistanceProperty, value);
    }

    public double WaveTime
    {
        get => GetValue(WaveTimeProperty);
        set => SetValue(WaveTimeProperty, value);
    }

    public bool ShowRays
    {
        get => GetValue(ShowRaysProperty);
        set => SetValue(ShowRaysProperty, value);
    }

    public string? StationName
    {
        get => GetValue(StationNameProperty);
        set => SetValue(StationNameProperty, value);
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

    public QuakeDiagram()
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

    private void OnModeChanged() => _drag = DragKind.None;

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);
        switch (Mode)
        {
            case QuakeMode.Boundaries:
                StepSection(dt, now);
                break;

            case QuakeMode.Inside:
                StepWaves(dt);
                break;
        }

        InvalidateVisual();
    }

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
        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
        var now = _clock.Elapsed.TotalSeconds;
        switch (Mode)
        {
            case QuakeMode.Boundaries:
                DrawBoundariesMode(dc, now);
                break;

            case QuakeMode.Inside:
                DrawInsideMode(dc, now);
                break;

            default:
                DrawPlatesMode(dc, now);
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
    private IPen FadedPen(Color color, double alpha, double thickness, bool dashed = false, bool flat = false)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20), Math.Round(thickness, 1), dashed);
        if (!flat && _fadedPens.TryGetValue(key, out var pen)) return pen;
        var brush = Solid((byte)(key.Item2 * 12.75), color.R, color.G, color.B);
        var cap = flat ? PenLineCap.Flat : PenLineCap.Round;
        pen = new ImmutablePen(((IBrush)brush).ToImmutable(), key.Item3, dashed ? new ImmutableDashStyle([4, 3], 0) : null, cap, flat ? PenLineJoin.Bevel : PenLineJoin.Round);
        if (!flat) _fadedPens[key] = pen;
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

    /// <summary>An arrow from one point to another.</summary>
    private static void DrawArrow(PaintContext dc, IPen pen, Point from, Point to, double head = 7)
    {
        Vector direction = to - from;
        if (direction.Length < 1) return;
        direction = direction.Normalize();
        dc.DrawLine(pen, from, to - (direction * head * 0.6));
        DrawArrowHead(dc, pen.Brush!, to, direction, head);
    }

    /// <summary>A rounded frame for a panel inside the picture.</summary>
    private static void DrawFrame(PaintContext dc, Rect box) => dc.DrawRoundedRectangle(Ink.Panel, Ink.Frame, box, 12, 12);

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
        public static readonly Color Violet = Color.FromRgb(0xB0, 0x8C, 0xFF);
        public static readonly Color Blue = Color.FromRgb(0x5E, 0x9B, 0xFF);
        public static readonly Color Magma = Color.FromRgb(0xFF, 0x6A, 0x2A);

        /// <summary>Boundary colours: ridges red, trenches blue, collisions violet, transforms green, the rest grey.</summary>
        public static readonly Color Ridge = Color.FromRgb(0xFF, 0x5A, 0x5F);
        public static readonly Color Trench = Color.FromRgb(0x5E, 0xB8, 0xFF);
        public static readonly Color Collision = Color.FromRgb(0xC9, 0x8C, 0xFF);
        public static readonly Color Transform = Color.FromRgb(0x7C, 0xFF, 0x9E);
        public static readonly Color Diffuse = Color.FromRgb(0xC8, 0xCE, 0xD8);

        /// <summary>Quake depth colours: shallow red-orange, intermediate yellow, deep blue.</summary>
        public static readonly Color Shallow = Color.FromRgb(0xFF, 0x6B, 0x4A);
        public static readonly Color Intermediate = Color.FromRgb(0xFF, 0xD3, 0x4A);
        public static readonly Color Deep = Color.FromRgb(0x5E, 0x9B, 0xFF);

        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x12, 0x17, 0x26)), new ImmutableGradientStop(1, Color.FromRgb(0x05, 0x07, 0x0E))],
            center: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.4, 0.45, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.85, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.85, RelativeUnit.Relative));

        /// <summary>Rock and water in the cross-sections.</summary>
        public static readonly IBrush Sea = Solid(200, 0x1C, 0x4E, 0x80);
        public static readonly IBrush Mantle = SpaceDrawing.LinearGradient(Color.FromRgb(0x6A, 0x2E, 0x1E), Color.FromRgb(0x3A, 0x14, 0x10), 90);
        public static readonly IBrush Lithosphere = Solid(255, 0x4A, 0x4E, 0x58);
        public static readonly IBrush Asthenosphere = Solid(255, 0x9A, 0x48, 0x24);
        public static readonly IBrush OceanCrust = Solid(255, 0x36, 0x3A, 0x48);
        public static readonly IBrush ContinentCrust = Solid(255, 0x8A, 0x76, 0x5C);
        public static readonly IBrush IndiaCrust = Solid(255, 0xA8, 0x8C, 0x5A);
        public static readonly IBrush Slab = Solid(255, 0x2E, 0x48, 0x6A);
        public static readonly IBrush FieldA = Solid(255, 0x5C, 0x7A, 0x44);
        public static readonly IBrush FieldB = Solid(255, 0x8A, 0x7C, 0x52);

        /// <summary>The Earth's layers in the inside view.</summary>
        public static readonly IBrush CrustRing = Solid(255, 0x9A, 0x8C, 0x78);
        public static readonly IBrush UpperMantle = Solid(255, 0x8A, 0x3C, 0x26);
        public static readonly IBrush LowerMantle = Solid(255, 0x66, 0x26, 0x1C);
        public static readonly IBrush OuterCore = SpaceDrawing.RadialGradient((Color.FromRgb(0xF0, 0x9A, 0x3A), 0), (Color.FromRgb(0xC8, 0x60, 0x20), 1));
        public static readonly IBrush InnerCore = SpaceDrawing.RadialGradient((Color.FromRgb(0xFF, 0xF2, 0xC0), 0), (Color.FromRgb(0xF2, 0xC8, 0x60), 1));

        public static readonly IBrush Panel = Solid(150, 8, 12, 22);
        public static readonly IPen Frame = MakePen(Solid(46, 255, 255, 255), 1);
        public static readonly IPen Axis = MakePen(Solid(80, 255, 255, 255), 1, dashed: true);
        public static readonly IPen Faint = MakePen(Solid(40, 255, 255, 255), 1);
        public static readonly IPen Tick = MakePen(Solid(110, 255, 255, 255), 1);
    }

    /// <summary>The colour a boundary is drawn in.</summary>
    internal static Color BoundaryColour(BoundaryKind kind) => kind switch
    {
        BoundaryKind.Ridge => Ink.Ridge,
        BoundaryKind.Trench => Ink.Trench,
        BoundaryKind.Collision => Ink.Collision,
        BoundaryKind.Transform => Ink.Transform,
        BoundaryKind.Rift => Ink.Orange,
        _ => Ink.Diffuse,
    };

    /// <summary>The colour a quake is drawn in by its depth (km).</summary>
    internal static Color DepthColour(double depth) => depth switch
    {
        < 70 => Ink.Shallow,
        < 300 => Ink.Intermediate,
        _ => Ink.Deep,
    };
}
