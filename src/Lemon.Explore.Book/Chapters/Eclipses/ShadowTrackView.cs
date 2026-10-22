using System.ComponentModel;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Eclipses;

/// <summary>
/// Earth's day side seen from the Sun during a solar eclipse: the Moon's penumbra and the small umbra (or antumbra)
/// sweeping across it, the path they trace, and the observer, who can be dragged north or south of that path.
/// </summary>
public sealed class ShadowTrackView : Control
{
    static ShadowTrackView()
    {
        AffectsRender<ShadowTrackView>(MoonAngleProperty, IsMoonNearProperty, ObserverOffsetProperty);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    public static readonly StyledProperty<double> MoonAngleProperty =
        AvaloniaProperty.Register<ShadowTrackView, double>(nameof(MoonAngle), 0.0);

    public static readonly StyledProperty<bool> IsMoonNearProperty =
        AvaloniaProperty.Register<ShadowTrackView, bool>(nameof(IsMoonNear), true);

    /// <summary>The observer north (+) or south (−) of the orbital plane, −1…1; dragging the marker changes it.</summary>
    public static readonly StyledProperty<double> ObserverOffsetProperty =
        AvaloniaProperty.Register<ShadowTrackView, double>(nameof(ObserverOffset), 0.0, defaultBindingMode: BindingMode.TwoWay);

    public double MoonAngle
    {
        get => GetValue(MoonAngleProperty);
        set => SetValue(MoonAngleProperty, value);
    }

    public bool IsMoonNear
    {
        get => GetValue(IsMoonNearProperty);
        set => SetValue(IsMoonNearProperty, value);
    }

    public double ObserverOffset
    {
        get => GetValue(ObserverOffsetProperty);
        set => SetValue(ObserverOffsetProperty, value);
    }

    private readonly PlanetRenderer _earth = new(Body("Earth"));
    private readonly LabelCache _labels;

    public ShadowTrackView()
    {
        _labels = new LabelCache(this);
        Focusable = true;
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        _labels.Clear();
        InvalidateVisual();
    }

    private Point Center => new(ActualWidth / 2, ActualHeight / 2);

    private double DiscRadius => Math.Min(ActualWidth, ActualHeight) * 0.42;

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        if (ActualWidth < 1 || ActualHeight < 1) return;

        dc.DrawRectangle(Paint.Background, null, new Rect(0, 0, ActualWidth, ActualHeight));

        var center = Center;
        var radius = DiscRadius;
        var scale = radius / EclipseGeometry.EarthRadius;
        dc.DrawEllipse(Paint.Atmosphere, null, center, radius * 1.12, radius * 1.12);
        DrawSphere(dc, _earth, center, radius, new LightVector(0, 0, 1), RenderScale());

        var shadow = EclipseGeometry.MoonShadowOnEarth(MoonAngle * Math.PI / 180, IsMoonNear);
        if (shadow.Reaches)
        {
            dc.PushClip(SpaceDrawing.Ellipse(center, radius, radius));

            // The path the umbra (antumbra) traces as the Moon moves: along the orbital plane, across the disc.
            var halfPath = Math.Max(Math.Abs(shadow.Umbra) * scale, 1);
            var pathTop = center.Y - (shadow.CentreZ * scale) - halfPath;
            dc.DrawRectangle(Paint.PathFill, null, new Rect(center.X - radius, pathTop, radius * 2, halfPath * 2));
            dc.DrawLine(Paint.PathEdge, new Point(center.X - radius, pathTop), new Point(center.X + radius, pathTop));
            dc.DrawLine(Paint.PathEdge, new Point(center.X - radius, pathTop + (halfPath * 2)), new Point(center.X + radius, pathTop + (halfPath * 2)));

            // Seen from the Sun with north up, the orbital plane's +Y is to the left.
            var spot = new Point(center.X - (shadow.CentreY * scale), center.Y - (shadow.CentreZ * scale));
            var penumbra = shadow.Penumbra * scale;
            dc.DrawEllipse(Paint.Penumbra(Math.Abs(shadow.Umbra) / shadow.Penumbra), Paint.PenumbraEdge, spot, penumbra, penumbra);

            var core = Math.Max(Math.Abs(shadow.Umbra) * scale, 2.5);
            dc.DrawEllipse(shadow.Umbra > 0 ? Paint.Umbra : Paint.Antumbra, shadow.Umbra > 0 ? null : Paint.AntumbraEdge, spot, core, core);
            dc.Pop();

            var key = shadow.Umbra > 0 ? "Eclipse_Label_TotalityPath" : "Eclipse_Label_AnnularPath";
            var text = _labels.Get(key, 11, Paint.Text);
            dc.DrawText(text, new Point(center.X - radius + 6, pathTop + (halfPath * 2) + 1));
        }

        // The observer, on the meridian facing the Sun.
        var observer = EclipseGeometry.Observer(ObserverOffset);
        var at = new Point(center.X, center.Y - (observer.Z * scale));
        dc.DrawEllipse(Paint.ObserverHalo, null, at, 9, 9);
        dc.DrawEllipse(Paint.ObserverFill, Paint.ObserverPen, at, 4.5, 4.5);
        _labels.DrawPill(dc, "Eclipse_Label_Observer", new Point(at.X + 10, at.Y - 10), 11, Paint.Text);
    }

    #region input

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus();
        e.Pointer.Capture(this);
        MoveObserverTo(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (ReferenceEquals(e.Pointer.Captured, this)) MoveObserverTo(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var step = e.Key switch
        {
            Key.Up => 0.05,
            Key.Down => -0.05,
            _ => 0,
        };
        if (step == 0) return;

        SetCurrentValue(ObserverOffsetProperty, Math.Clamp(Math.Round((ObserverOffset + step) * 20) / 20, -1, 1));
        e.Handled = true;
    }

    private void MoveObserverTo(Point point)
    {
        var reach = DiscRadius * EclipseGeometry.MaxObserverOffset;
        if (reach < 1) return;

        var offset = Math.Clamp((Center.Y - point.Y) / reach, -1, 1);

        // Snap to the centre line, which is only a few pixels wide.
        if (Math.Abs(offset) < 0.03) offset = 0;
        SetCurrentValue(ObserverOffsetProperty, offset);
    }

    #endregion

    private static class Paint
    {
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);

        public static readonly IBrush Background = SpaceDrawing.LinearGradient(Color.FromRgb(0x05, 0x08, 0x12), Color.FromRgb(0x0A, 0x10, 0x20), 90);

        public static readonly IBrush Atmosphere = SpaceDrawing.RadialGradient(
            (Color.FromArgb(90, 120, 180, 255), 0.86),
            (Color.FromArgb(0, 120, 180, 255), 1));

        public static readonly IBrush PathFill = Solid(40, 0, 0, 0);
        public static readonly IPen PathEdge = MakePen(Solid(120, 255, 214, 130), 0.8, dashed: true);
        public static readonly IBrush Umbra = Solid(240, 0, 0, 0);
        public static readonly IBrush Antumbra = Solid(150, 30, 14, 0);
        public static readonly IPen AntumbraEdge = MakePen(Solid(220, 255, 170, 70), 1.2);
        public static readonly IBrush ObserverHalo = Solid(70, 255, 255, 255);
        public static readonly IBrush ObserverFill = Solid(255, 255, 255, 255);
        public static readonly IPen ObserverPen = MakePen(Solid(255, 230, 60, 60), 2);

        public static readonly IPen PenumbraEdge = MakePen(Solid(150, 255, 214, 130), 1, dashed: true);

        /// <summary>The penumbra: darkest next to the umbra, fading to nothing at its edge.</summary>
        public static IBrush Penumbra(double coreShare) => SpaceDrawing.RadialGradient(
            (Color.FromArgb(215, 0, 0, 0), Math.Clamp(coreShare, 0, 1)),
            (Color.FromArgb(150, 0, 0, 0), 0.5),
            (Color.FromArgb(40, 0, 0, 0), 0.9),
            (Color.FromArgb(0, 0, 0, 0), 1));
    }
}
